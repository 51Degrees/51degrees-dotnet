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
//   dotnet run    then open http://localhost:5151/
//
// Human Confidence says how likely it is that a person is looking at the
// page. It comes in two ways, and this demo shows both. The cloud gives
// the level, and this server only passes the evidence on and reads it.
// The calls themselves are in HumanConfidenceCloud.cs.
//
// 1. Passive. This server asks the cloud itself, sending the visitor's
//    request headers and IP address. HumanConfidenceSource is Passive and
//    HumanConfidence is a plain level for the request alone. The server
//    asks for it rather than reading it from the page, because a level a
//    browser sends can be forged.
// 2. Active. The page loads the 51Degrees client script, which runs the
//    browser tests and sends the answers to the cloud. When the script
//    completes, HumanConfidenceSource is Active and HumanConfidence holds
//    a sealed value the browser can't read or change.
// 3. Redeem. The page hands the sealed value to this server, which calls
//    hc/redeem with it and the account's License Key, and gets the true
//    level back. A sealed value redeems once, within ten seconds. This
//    server is the only party holding the License Key, which the browser
//    never sees.
//
// Environment variables:
//   _51DEGREES_RESOURCE_KEY    the Resource Key, required. It has to carry
//                              HumanConfidence, which this server checks
//                              with accessibleproperties at start-up.
//   _51DEGREES_LICENSE_KEY     a License Key of the same account, required
//                              to redeem
//   _51DEGREES_CLOUD_ENDPOINT  the API base including the /api/v4/
//                              segment, defaults to
//                              https://cloud.51degrees.com/api/v4/
//   PORT                       the port to listen on, defaults to 5151
//
// See ../README.md for the steps and what to expect.
using Examples.HumanConfidence.HumanConfidenceWeb;

var resource = Environment.GetEnvironmentVariable("_51DEGREES_RESOURCE_KEY");
if (string.IsNullOrWhiteSpace(resource))
{
    // A fresh clone has no key, so this says where to create one and
    // ends without a server.
    Console.WriteLine(
        "Set _51DEGREES_RESOURCE_KEY to the Resource Key of the page. "
        + "A resource key can be created at "
        + "https://51degrees.com/configure?utm_source=code&utm_medium=example&utm_campaign=51degrees-dotnet&utm_content=examples-humanconfidence-humanconfidenceweb-program.cs&utm_term=resource-key-required");
    return 0;
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
var api = string.IsNullOrWhiteSpace(endpoint)
    ? "https://cloud.51degrees.com/api/v4/"
    : endpoint;
var port = Environment.GetEnvironmentVariable("PORT") ?? "5151";

// One client for the process, with a short limit, so a slow cloud can't
// hang the demo.
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
var cloud = new HumanConfidenceCloud(http, api, resource, license);
api = cloud.Api;

// Whether the key carries HumanConfidence, asked once of the cloud's
// accessibleproperties. A key without it gets no level, so the page says
// so and links to contact us rather than asking for what the key doesn't
// carry. The answer is "yes", "no", or "unknown" when the cloud couldn't
// be asked, and the page is told which.
const string ContactUs =
    "https://51degrees.com/contact-us?utm_source=code&utm_medium=example&utm_campaign=51degrees-dotnet&utm_content=examples-humanconfidence-humanconfidenceweb-program.cs&utm_term=human-confidence-not-in-key";
var carries = await cloud.CarriesHumanConfidenceAsync();
if (carries == "unknown")
{
    Console.WriteLine($"Couldn't check the Resource Key with {api}accessibleproperties.");
}
if (carries == "no")
{
    Console.WriteLine(
        "The Resource Key doesn't carry HumanConfidence, so the page will "
        + "show no level. To add it, talk to 51Degrees at " + ContactUs);
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
        ReadPage().Replace("__RESOURCE__", resource).Replace("__API__", api)
            .Replace("__CARRIES__", carries),
        "text/html; charset=utf-8");
});

app.MapGet("/examples-main.min.css", () => Results.Bytes(ReadCss(), "text/css"));

// Passive: this server asks the cloud for the visitor, with the visitor's
// headers and IP address, and reads the plain level from the answer.
app.MapGet("/passive", async (HttpContext context) =>
{
    try
    {
        // Behind a load balancer or CDN, use the visitor's address it
        // passes on.
        var evidence = await cloud.EvidenceAsync(
            context.Request.Headers.Select(h =>
                new KeyValuePair<string, string>(h.Key, h.Value.ToString())),
            context.Connection.RemoteIpAddress);
        var answer = await cloud.PassiveAsync(evidence);
        if (answer.Error != null)
        {
            return Results.Json(new { error = answer.Error });
        }
        return Results.Json(new
        {
            source = answer.Source,
            humanConfidence = answer.HumanConfidence,
        });
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
    {
        // The cloud's header list couldn't be read.
        return Results.Json(new { error = ex.Message });
    }
});

// Redeem: the page sends the sealed value, and this server redeems it with
// the License Key. The answer goes back to the page as the cloud gave it.
app.MapPost("/redeem", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    var answer = await cloud.RedeemAsync(form["value"].ToString());
    if (answer.Error != null)
    {
        return Results.Json(new { error = answer.Error }, statusCode: answer.StatusCode);
    }
    return Results.Content(answer.Body, "application/json", statusCode: answer.StatusCode);
});

Console.WriteLine($"Human Confidence demo on http://localhost:{port}/, using {api}");
app.Run();
return 0;
