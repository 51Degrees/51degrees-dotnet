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
// page. The cloud gives it, the 51Degrees Pipeline asks for it, and this
// server only reads the two Properties the Pipeline gives,
// HumanConfidence and HumanConfidenceSource. The Pipeline is set up in
// HumanConfidencePipeline.cs.
//
// 1. Passive. The page asks this server, whose Pipeline asks the cloud
//    with the visitor's request. HumanConfidenceSource is Passive and
//    HumanConfidence is a plain level for the request alone.
// 2. Active. This server asks the Pipeline for a page id as it serves
//    the page, and the page loads the 51Degrees client script from this
//    server's Pipeline with that id as humanconfidencepage. The script
//    runs the browser tests and sends the answers back through the
//    Pipeline, which adds the page's transaction on every call to the
//    cloud. HumanConfidenceSource is then Active and HumanConfidence
//    holds a sealed value the browser can't read or change.
// 3. Redeemed. The page sends the sealed value back to this server as
//    humanconfidence, with its page id as humanconfidencepage. The
//    Pipeline redeems it with the transaction it kept for that page and
//    the License Key, which only this server holds, and HumanConfidence
//    is the level. This server doesn't redeem anything itself. A sealed
//    value redeems once, within ten seconds, and the level is Medium
//    whenever it isn't redeemed.
//
// Environment variables:
//   _51DEGREES_RESOURCE_KEY    the Resource Key, required. It has to carry
//                              HumanConfidence, which this server checks
//                              in the Pipeline's property list, fetched
//                              from the cloud's accessibleproperties.
//   _51DEGREES_LICENSE_KEY     a License Key of the same account, needed
//                              to redeem
//   _51DEGREES_CLOUD_ENDPOINT  the API base including the /api/v4/
//                              segment, defaults to
//                              https://cloud.51degrees.com/api/v4/
//   _51DEGREES_EXAMPLE_HTTP_PORTS
//                              the ports to listen on, comma separated,
//                              defaults to 5151
//
// See ../README.md for the steps and what to expect.
using Examples.HumanConfidence.HumanConfidenceWeb;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Web;
using FiftyOne.Pipeline.Web.Services;

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
        + "Active, but nothing can be redeemed without one, so the third "
        + "row will show Medium.");
}
var endpoint = Environment.GetEnvironmentVariable("_51DEGREES_CLOUD_ENDPOINT");
var api = (string.IsNullOrWhiteSpace(endpoint)
    ? "https://cloud.51degrees.com/api/v4/"
    : endpoint).Trim().TrimEnd('/') + "/";
// The ports to listen on, read as the other 51Degrees web examples read
// them.
var portsSetting = Environment.GetEnvironmentVariable("_51DEGREES_EXAMPLE_HTTP_PORTS");
var ports = string.IsNullOrWhiteSpace(portsSetting)
    ? new[] { 5151 }
    : portsSetting.Split(',')
        .Select(p => int.Parse(
            p.Trim(), System.Globalization.CultureInfo.InvariantCulture))
        .ToArray();

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});
builder.WebHost.UseUrls(ports.Select(p => $"http://localhost:{p}").ToArray());
// The Pipeline, from configuration held in memory, so neither key is
// written to a file. The web integration adds the elements that serve the
// client script.
builder.Configuration.AddInMemoryCollection(
    HumanConfidencePipeline.Configuration(resource, license, api));
builder.Services.AddSingleton<CloudRequestEngineBuilder>();
builder.Services.AddSingleton<DerivedCloudEngineBuilder>();
builder.Services.AddSingleton<BrowserDerivedCloudEngineBuilder>();
builder.Services.AddFiftyOne(builder.Configuration);
var app = builder.Build();

// Whether the key carries HumanConfidence, from the Pipeline's property
// list. A key without it gets no level, so the page says so and links to
// contact us rather than asking for what the key doesn't carry. The page
// is told "yes", "no", or "unknown" when the cloud couldn't be asked.
const string ContactUs =
    "https://51degrees.com/contact-us?utm_source=code&utm_medium=example&utm_campaign=51degrees-dotnet&utm_content=examples-humanconfidence-humanconfidenceweb-program.cs&utm_term=human-confidence-not-in-key";
string carries;
CloudRequestEngine? engine;
try
{
    var pipeline = app.Services.GetRequiredService<IPipeline>();
    carries = HumanConfidencePipeline.CarriesHumanConfidence(pipeline);
    engine = pipeline.GetElement<CloudRequestEngine>();
}
catch (FiftyOne.Pipeline.Core.Exceptions.PipelineException ex)
{
    // The Pipeline couldn't be built, as when the cloud refuses the key.
    Console.WriteLine($"Couldn't build the Pipeline with {api}: {ex.Message}");
    return 1;
}
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

// The Pipeline runs for the client script, the JSON it posts its evidence
// to, and the page's own requests for the two Properties. The web
// integration makes one Flow Data for each of those requests, and
// disposes of it when the request ends. A Flow Data cannot be used by two
// threads at once, so work handed to another thread gets the values it
// needs, read on the request's thread, not the Flow Data.
app.UseWhen(
    context => context.Request.Path.Equals("/51Degrees.core.js", StringComparison.OrdinalIgnoreCase)
        || context.Request.Path.Equals("/51dpipeline/json", StringComparison.OrdinalIgnoreCase)
        || context.Request.Path.Equals("/humanconfidence", StringComparison.OrdinalIgnoreCase),
    pipeline => pipeline.UseFiftyOne());

app.MapGet("/", (HttpResponse response) =>
{
    // The page must never come from a cache, as each load runs the tests.
    response.Headers.CacheControl = "no-store";
    // A new page id for each page, which the Pipeline keeps a transaction
    // for. It's only a handle, so it can go in the page. Without a License
    // Key there is none, and the page still shows Passive and Active.
    var page = engine?.IssueHumanConfidencePage() ?? "";
    return Results.Content(
        ReadPage().Replace("__CARRIES__", carries).Replace("__PAGE__", page),
        "text/html; charset=utf-8");
});

app.MapGet("/examples-main.min.css", () => Results.Bytes(ReadCss(), "text/css"));

// The two Properties for this request. A request that carries a sealed
// value as humanconfidence, and its page id as humanconfidencepage, gets
// the level the Pipeline redeemed, and any other gets the passive level.
app.MapMethods("/humanconfidence", new[] { "GET", "POST" },
    (IFlowDataProvider flowData) =>
        Results.Json(HumanConfidencePipeline.Read(flowData.GetFlowData())));

Console.WriteLine($"Human Confidence demo on http://localhost:{ports[0]}/, using {api}");
app.Run();
return 0;
