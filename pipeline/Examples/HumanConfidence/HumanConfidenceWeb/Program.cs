/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2026 51 Degrees Mobile Experts Limited, Davidson House,
 * Forbury Square, Reading, Berkshire, United Kingdom RG1 3EU.
 *
 * This Original Work is licensed under the European Union Public Licence
 * (EUPL) v.1.2 and is subject to its terms as set out below.
 *
 * If a copy of the EUPL was not distributed with this file, You can obtain
 * one at https://opensource.org/licenses/EUPL-1.2.
 *
 * The 'Compatible Licences' set out in the Appendix to the EUPL (as may be
 * amended by the European Commission) shall be deemed incompatible for
 * the purposes of the Work and the provisions of the compatibility
 * clause in Article 5 of the EUPL shall not apply.
 *
 * If using the Work as, or as part of, a network application, by
 * including the attribution notice(s) required under Article 5 of the EUPL
 * in the end user terms of the application under an appropriate heading,
 * such notice(s) shall fulfill the requirements of that article.
 * ********************************************************************* */

// Human Confidence demo server. ASP.NET Core minimal API. Run:
//   dotnet run    then open http://localhost:5110/
//
// Human Confidence says how likely it is that a person is looking at the
// page. It comes in two ways, and this demo shows both.
//
// 1. Passive. This server asks the cloud itself, sending the visitor's
//    request headers and IP address. HumanConfidenceSource is Passive and
//    HumanConfidence is a plain level, worked out from the request alone.
//    The server asks for it rather than reading it from the page, because
//    a level a browser sends can be forged.
// 2. Active. The page loads the 51Degrees client script, which runs the
//    browser tests and sends the answers to the cloud. When the script
//    completes, HumanConfidenceSource is Active and HumanConfidence holds
//    a sealed value the browser can't read or change.
// 3. Redeem. As soon as HumanConfidence isn't a plain level, the page
//    hands the sealed value to this server, which calls
//    hc/redeem with it and the account's License Key, and gets the true
//    level back. A sealed value redeems once, within ten seconds. This
//    server is the only party holding the License Key, which the browser
//    never sees.
//
// Environment variables:
//   _51DEGREES_RESOURCE_KEY    the Resource Key, required
//   _51DEGREES_LICENSE_KEY     a License Key of the same account, required
//                              to redeem
//   _51DEGREES_CLOUD_ENDPOINT  the API base including the /api/v4/
//                              segment, defaults to
//                              https://cloud.51degrees.com/api/v4/
//   PORT                       the port to listen on, defaults to 5110
//
// See ../README.md for the steps and what to expect.
using System.Net;
using System.Text.Json;

var resource = Environment.GetEnvironmentVariable("_51DEGREES_RESOURCE_KEY");
if (string.IsNullOrWhiteSpace(resource))
{
    Console.Error.WriteLine(
        "Set _51DEGREES_RESOURCE_KEY to the Resource Key of the page.");
    return 1;
}
var license = Environment.GetEnvironmentVariable("_51DEGREES_LICENSE_KEY");
if (string.IsNullOrWhiteSpace(license))
{
    Console.WriteLine(
        "No _51DEGREES_LICENSE_KEY set. The page still shows Passive and "
        + "Active, but an account that holds License Keys can't redeem "
        + "without one, so the redeemed level will be unreadable.");
}
var endpoint = Environment.GetEnvironmentVariable("_51DEGREES_CLOUD_ENDPOINT");
var api = (string.IsNullOrWhiteSpace(endpoint)
    ? "https://cloud.51degrees.com/api/v4/"
    : endpoint.Trim()).TrimEnd('/') + "/";
var port = Environment.GetEnvironmentVariable("PORT") ?? "5110";

// One client for the process, with a short limit, so a slow cloud can't
// hang the demo.
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

// The request headers the cloud reads, from its own list, fetched once.
// Only these are passed on, so headers such as Authorization stay here.
HashSet<string>? headerNames = null;
async Task<HashSet<string>> CloudHeaders()
{
    if (headerNames == null)
    {
        var keys = JsonSerializer.Deserialize<string[]>(
            await http.GetStringAsync(api + "evidencekeys")) ?? [];
        headerNames = keys
            .Where(k => k.StartsWith("header.", StringComparison.OrdinalIgnoreCase))
            .Select(k => k[7..].ToLowerInvariant())
            .ToHashSet();
    }
    return headerNames;
}

// Read per request, so an edit to the page shows on the next load.
string ReadPage() => File.ReadAllText(
    Path.Combine(AppContext.BaseDirectory, "page.html"));
byte[] ReadCss() => File.ReadAllBytes(
    Path.Combine(AppContext.BaseDirectory, "examples-main.min.css"));

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://localhost:{port}");
var app = builder.Build();

app.MapGet("/", (HttpResponse response) =>
{
    // The page must never come from a cache, as each load runs the tests.
    response.Headers.CacheControl = "no-store";
    return Results.Content(
        ReadPage().Replace("__RESOURCE__", resource).Replace("__API__", api),
        "text/html; charset=utf-8");
});

app.MapGet("/examples-main.min.css", () => Results.Bytes(ReadCss(), "text/css"));

// Passive: this server asks the cloud for the visitor, with the visitor's
// headers and IP address, and reads the plain level from the answer.
app.MapGet("/passive", async (HttpContext context) =>
{
    try
    {
        var names = await CloudHeaders();
        var evidence = new Dictionary<string, string>();
        foreach (var (name, value) in context.Request.Headers)
        {
            if (names.Contains(name.ToLowerInvariant()))
            {
                evidence[name.ToLowerInvariant()] = value.ToString();
            }
        }
        // Behind a load balancer or CDN, use the visitor's address it
        // passes on. Set last, so no header can replace it.
        var ip = context.Connection.RemoteIpAddress;
        if (ip != null)
        {
            evidence["client-ip"] = (ip.IsIPv4MappedToIPv6
                ? ip.MapToIPv4() : ip).ToString();
        }
        using var answer = await http.PostAsync(
            api + resource + ".json", new FormUrlEncodedContent(evidence));
        var body = await answer.Content.ReadAsStringAsync();
        if (answer.IsSuccessStatusCode == false)
        {
            return Results.Json(new { error = $"HTTP {(int)answer.StatusCode}: {Trim(body)}" });
        }
        using var json = JsonDocument.Parse(body);
        var derived = json.RootElement.TryGetProperty("derived", out var d) ? d : default;
        return Results.Json(new
        {
            source = Read(derived, "humanconfidencesource"),
            humanConfidence = Read(derived, "humanconfidence"),
        });
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
    {
        return Results.Json(new { error = ex.Message });
    }
});

// Redeem: the page sends the sealed value, and this server redeems it with
// the License Key. The answer goes back to the page as the cloud gave it.
app.MapPost("/redeem", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    var value = form["value"].ToString();
    var fields = new Dictionary<string, string> { ["value"] = value };
    if (string.IsNullOrWhiteSpace(license) == false)
    {
        fields["license"] = license;
    }
    try
    {
        using var answer = await http.PostAsync(
            api + "hc/redeem", new FormUrlEncodedContent(fields));
        var body = await answer.Content.ReadAsStringAsync();
        if (answer.StatusCode == HttpStatusCode.NotFound)
        {
            return Results.Json(new { error = $"The service at {api} doesn't redeem Human Confidence." });
        }
        return Results.Content(body, "application/json", statusCode: (int)answer.StatusCode);
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
    {
        return Results.Json(new { error = ex.Message }, statusCode: 502);
    }
});

Console.WriteLine($"Human Confidence demo on http://localhost:{port}/, using {api}");
app.Run();
return 0;

static string? Read(JsonElement parent, string name) =>
    parent.ValueKind == JsonValueKind.Object
        && parent.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
        ? value.GetString() : null;

static string Trim(string text) => text.Length > 200 ? text[..200] : text;
