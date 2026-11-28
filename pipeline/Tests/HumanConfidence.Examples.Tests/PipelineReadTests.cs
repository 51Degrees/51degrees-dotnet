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

using Examples.HumanConfidence.HumanConfidenceWeb;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.JavaScriptBuilder.Data;
using FiftyOne.Pipeline.JavaScriptBuilder.FlowElement;
using FiftyOne.Pipeline.JsonBuilder.Data;
using FiftyOne.Pipeline.JsonBuilder.FlowElement;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace FiftyOne.HumanConfidence.Examples.Tests;

/// <summary>
/// The example reads the two Properties through the Pipeline it sets up,
/// against a recorded stand-in for the cloud, so nothing here touches the
/// network. The Pipeline asks the cloud and redeems a sealed value, and
/// the example only reads HumanConfidence and HumanConfidenceSource.
/// </summary>
[TestClass]
public class PipelineReadTests
{
    private const string Api = "https://cloud.example.test/api/v4/";
    private const string Resource = "resource-key";
    private const string License = "license-key-0123456789";
    private const string Sealed = "sealed-value-0123456789";

    private const string Properties =
        "{\"Products\":{\"derived\":{\"DataTier\":\"CloudV5Bespoke\"," +
        "\"Properties\":[{\"Name\":\"HumanConfidence\",\"Type\":\"String\"," +
        "\"Category\":\"Bot Detection\"},{\"Name\":\"HumanConfidenceSource\"," +
        "\"Type\":\"String\",\"Category\":\"Bot Detection\"}]}}}";

    private FakeCloud _cloud = null!;
    private readonly List<IDisposable> _disposables = new();

    [TestInitialize]
    public void TestInitialize()
    {
        Environment.SetEnvironmentVariable("_51DEGREES_CLOUD_ENDPOINT", null);
        _cloud = new FakeCloud();
    }

    [TestCleanup]
    public void TestCleanup()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
    }

    /// <summary>
    /// The same elements the example's configuration lists, built here
    /// against the stand-in.
    /// </summary>
    private IPipeline NewPipeline(string? license = License)
    {
        var loggers = NullLoggerFactory.Instance;
        var pipeline = new PipelineBuilder(loggers)
            .AddFlowElement(new CloudRequestEngineBuilder(
                    loggers, new HttpClient(_cloud))
                .SetEndPoint(Api)
                .SetResourceKey(Resource)
                .SetLicenseKey(license)
                .Build())
            .AddFlowElement(new DerivedCloudEngineBuilder(loggers).Build())
            .SetSuppressProcessExceptions(true)
            .Build();
        _disposables.Add(pipeline);
        return pipeline;
    }

    private static HumanConfidenceAnswer Read(
        IPipeline pipeline, params (string Key, object Value)[] evidence)
    {
        using var data = pipeline.CreateFlowData();
        data.AddEvidence("header.user-agent", "Mozilla/5.0 Chrome/142.0.0.0");
        foreach (var (key, value) in evidence)
        {
            data.AddEvidence(key, value);
        }
        data.Process();
        return HumanConfidencePipeline.Read(data);
    }

    /// <summary>
    /// Serves a page as the example does: issues the page id, then makes
    /// the call the page's client script makes with it, which carries the
    /// page's transaction.
    /// </summary>
    private static string ServePage(IPipeline pipeline)
    {
        var pageId = pipeline.GetElement<CloudRequestEngine>()
            .IssueHumanConfidencePage();
        Read(pipeline, ("query.humanconfidencepage", pageId));
        return pageId;
    }

    /// <summary>
    /// The whole path through the elements a web integration runs: the
    /// server issues a page id, the client script served with it repeats
    /// it, the script's call carries the page's transaction, and the
    /// sealed value sent back with the page id redeems. Each request's
    /// Evidence is only what the Pipeline's Evidence filter takes from the
    /// request, as the web integration takes it.
    /// </summary>
    [TestMethod]
    public void WholePath_ThroughTheEvidenceFilter_Redeems()
    {
        var loggers = NullLoggerFactory.Instance;
        var engine = new CloudRequestEngineBuilder(loggers, new HttpClient(_cloud))
            .SetEndPoint(Api)
            .SetResourceKey(Resource)
            .SetLicenseKey(License)
            .Build();
        using var pipeline = new PipelineBuilder(loggers)
            .AddFlowElement(new SequenceElementBuilder(loggers).Build())
            .AddFlowElement(engine)
            .AddFlowElement(new DerivedCloudEngineBuilder(loggers).Build())
            .AddFlowElement(new JsonBuilderElementBuilder(loggers).Build())
            .AddFlowElement(new JavaScriptBuilderElementBuilder(loggers)
                .SetEndpoint("/51dpipeline/json")
                .SetMinify(false)
                .Build())
            .SetSuppressProcessExceptions(true)
            .Build();
        _cloud.Answer = "{\"derived\":{\"humanconfidencesource\":\"Passive\"," +
            "\"humanconfidence\":\"Low\"}}";

        // The server renders the page.
        var pageId = engine.IssueHumanConfidencePage();

        // The page asks for its client script with its page id.
        string session;
        using (var script = Request(pipeline, new Dictionary<string, object>
        {
            ["query.humanconfidencepage"] = pageId,
        }))
        {
            var javaScript = script.Get<IJavaScriptBuilderElementData>().JavaScript;
            StringAssert.Contains(javaScript, "humanconfidencepage");
            StringAssert.Contains(javaScript, pageId,
                "The script repeats the page id on its calls.");
            Assert.DoesNotContain(pageId,
                script.Get<IJsonBuilderElementData>().Json,
                "The JSON Builder reads no Evidence, so it holds no page id.");
            session = (string)script.GetEvidence()[
                "query.session-id"];
        }
        var transaction = _cloud.DataCalls().Last()["transaction"];

        // The script's call, with the parameters it was given.
        using (var call = Request(pipeline, new Dictionary<string, object>
        {
            ["query.humanconfidencepage"] = pageId,
            ["query.session-id"] = session,
            ["query.sequence"] = "1",
        }))
        {
        }
        Assert.AreEqual(transaction, _cloud.DataCalls().Last()["transaction"],
            "The script's call carries the page's transaction.");

        // The page sends its sealed value back with its page id.
        _cloud.Answer = "{\"derived\":{\"humanconfidencesource\":\"Active\"," +
            "\"humanconfidence\":\"High\"}}";
        using (var redeem = Request(pipeline, new Dictionary<string, object>
        {
            ["query.humanconfidence"] = Sealed,
            ["query.humanconfidencepage"] = pageId,
        }))
        {
            var answer = HumanConfidencePipeline.Read(redeem);
            Assert.AreEqual("Active", answer.Source);
            Assert.AreEqual("High", answer.HumanConfidence);
            Assert.DoesNotContain(Sealed,
                redeem.Get<IJavaScriptBuilderElementData>().JavaScript,
                "The sealed value is not repeated in the script.");
            var json = redeem.Get<IJsonBuilderElementData>().Json;
            Assert.DoesNotContain(Sealed, json,
                "The sealed value is not in the JSON Builder's output.");
            Assert.DoesNotContain(pageId, json,
                "The page id is not in the JSON Builder's output.");
        }
        var redeeming = _cloud.DataCalls().Last();
        Assert.AreEqual(Sealed, redeeming["humanconfidence"]);
        Assert.AreEqual(transaction, redeeming["transaction"]);
        Assert.IsFalse(redeeming.ContainsKey("humanconfidencepage"));
    }

    /// <summary>
    /// A request with the values given, of which only those the Pipeline's
    /// Evidence filter takes become Evidence, beside the headers a browser
    /// sends.
    /// </summary>
    private static IFlowData Request(
        IPipeline pipeline, Dictionary<string, object> values)
    {
        var data = pipeline.CreateFlowData();
        var request = new Dictionary<string, object>(values)
        {
            ["header.user-agent"] = "Mozilla/5.0 Chrome/142.0.0.0",
            ["header.host"] = "localhost",
            ["query.not-taken"] = "x",
        };
        foreach (var entry in request.Where(e =>
            pipeline.EvidenceKeyFilter.Include(e.Key)))
        {
            data.AddEvidence(entry.Key, entry.Value);
        }
        data.Process();
        return data;
    }

    [TestMethod]
    public void Configuration_ListsTheElements_AndTheLicenseKeyOnlyForTheCloudRequestEngine()
    {
        var settings = HumanConfidencePipeline.Configuration(Resource, License, Api);

        Assert.AreEqual("CloudRequestEngine",
            settings["PipelineOptions:Elements:0:BuilderName"]);
        Assert.AreEqual("DerivedCloudEngine",
            settings["PipelineOptions:Elements:1:BuilderName"]);
        Assert.AreEqual("BrowserDerivedCloudEngine",
            settings["PipelineOptions:Elements:2:BuilderName"]);
        Assert.AreEqual(License,
            settings["PipelineOptions:Elements:0:BuildParameters:LicenseKey"]);
        Assert.AreEqual(1, settings.Values.Count(v => v == License),
            "The License Key is given to the Cloud Request Engine alone.");

        var withoutLicense = HumanConfidencePipeline.Configuration(Resource, null, Api);
        Assert.IsFalse(withoutLicense.Keys.Any(k => k.EndsWith(
            "LicenseKey", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Passive_ReadsTheSourceAndTheLevel()
    {
        _cloud.Answer = "{\"derived\":{\"humanconfidencesource\":\"Passive\"," +
            "\"humanconfidence\":\"Medium\"}}";

        var answer = Read(NewPipeline());

        Assert.AreEqual("Passive", answer.Source);
        Assert.AreEqual("Medium", answer.HumanConfidence);
        Assert.IsNull(answer.Reason);
    }

    [TestMethod]
    public void Sealed_ThePipelineRedeemsIt_AndTheExampleReadsTheLevel()
    {
        var pipeline = NewPipeline();
        // A later call from the page, which gives the page its transaction.
        var pageId = ServePage(pipeline);
        var transaction = _cloud.DataCalls().Last()["transaction"];
        _cloud.Answer = "{\"derived\":{\"humanconfidencesource\":\"Active\"," +
            "\"humanconfidence\":\"High\"}}";

        var answer = Read(pipeline,
            ("query.humanconfidence", Sealed), ("query.humanconfidencepage", pageId!));

        Assert.AreEqual("Active", answer.Source);
        Assert.AreEqual("High", answer.HumanConfidence);
        var call = _cloud.DataCalls().Last();
        Assert.AreEqual(Sealed, call["humanconfidence"]);
        Assert.AreEqual(transaction, call["transaction"]);
        Assert.AreEqual(License, call["license"]);
        Assert.IsFalse(_cloud.Urls.Any(u => u.Contains("redeem",
            StringComparison.OrdinalIgnoreCase)),
            "Nothing is sent to a redeem endpoint.");
    }

    [TestMethod]
    public void Sealed_SentAgain_IsMedium()
    {
        var pipeline = NewPipeline();
        var pageId = ServePage(pipeline);
        _cloud.Answer = "{\"derived\":{\"humanconfidencesource\":\"Active\"," +
            "\"humanconfidence\":\"High\"}}";
        Read(pipeline,
            ("query.humanconfidence", Sealed), ("query.humanconfidencepage", pageId!));

        var again = Read(pipeline,
            ("query.humanconfidence", Sealed), ("query.humanconfidencepage", pageId!));

        Assert.AreEqual("Active", again.Source);
        Assert.AreEqual("Medium", again.HumanConfidence);
        Assert.IsFalse(_cloud.DataCalls().Last().ContainsKey("humanconfidence"),
            "The page's transaction was used by the first, so nothing is " +
            "sent to redeem.");
    }

    [TestMethod]
    public void Sealed_WithoutALicenseKey_IsMedium()
    {
        var pipeline = NewPipeline(license: null);
        Assert.IsNull(pipeline.GetElement<CloudRequestEngine>()
            .IssueHumanConfidencePage(), "No page id without a License Key.");
        _cloud.Answer = "{\"derived\":{\"humanconfidencesource\":\"Active\"," +
            "\"humanconfidence\":\"High\"}}";

        var answer = Read(pipeline, ("query.humanconfidence", Sealed));

        Assert.AreEqual("Active", answer.Source);
        Assert.AreEqual("Medium", answer.HumanConfidence);
        Assert.IsFalse(_cloud.DataCalls().Last().ContainsKey("humanconfidence"));
    }

    [TestMethod]
    public void AKeyOutsideBespoke_GivesNoSource_AndTheReason()
    {
        _cloud.Answer = "{\"derived\":{\"humanconfidence\":null," +
            "\"humanconfidencenullreason\":\"HumanConfidence is a paid feature.\"," +
            "\"humanconfidencesource\":null," +
            "\"humanconfidencesourcenullreason\":" +
            "\"HumanConfidenceSource is a paid feature.\"}}";

        var answer = Read(NewPipeline());

        Assert.IsNull(answer.Source, "No source is made up.");
        Assert.IsNull(answer.HumanConfidence, "No level is made up.");
        Assert.AreEqual("HumanConfidenceSource is a paid feature.", answer.Reason);
    }

    [TestMethod]
    public void WithoutTheProperty_ReportsAReasonRatherThanALevel()
    {
        _cloud.Answer = "{\"device\":{\"ismobile\":false}}";

        var answer = Read(NewPipeline());

        Assert.IsNull(answer.Source);
        Assert.IsNull(answer.HumanConfidence);
        Assert.IsFalse(string.IsNullOrEmpty(answer.Reason));
    }

    [TestMethod]
    public void CarriesHumanConfidence_ReadsThePipelinesPropertyList()
    {
        Assert.AreEqual("yes",
            HumanConfidencePipeline.CarriesHumanConfidence(NewPipeline()));

        _cloud.Products = "{\"Products\":{\"device\":{\"DataTier\":\"CloudV5Free\"," +
            "\"Properties\":[{\"Name\":\"IsMobile\",\"Type\":\"Boolean\"}]}}}";
        Assert.AreEqual("no",
            HumanConfidencePipeline.CarriesHumanConfidence(NewPipeline()));
    }

    /// <summary>
    /// A stand-in for the cloud, which records what was sent.
    /// </summary>
    private sealed class FakeCloud : HttpMessageHandler
    {
        public string Answer { get; set; } = "{\"derived\":{}}";
        public string Products { get; set; } = Properties;
        public List<string> Urls { get; } = new();
        private readonly List<string> _bodies = new();

        public List<Dictionary<string, string>> DataCalls()
        {
            lock (_bodies)
            {
                return _bodies.Select(b =>
                {
                    var form = HttpUtility.ParseQueryString(b);
                    return form.AllKeys.Where(k => k != null).ToDictionary(
                        k => k!, k => form[k]!, StringComparer.OrdinalIgnoreCase);
                }).ToList();
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            var body = request.Content == null
                ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            string answer;
            lock (_bodies)
            {
                Urls.Add(url);
                if (url.EndsWith("/evidencekeys", StringComparison.Ordinal))
                {
                    answer = "[\"header.user-agent\"]";
                }
                else if (url.Contains("accessibleproperties", StringComparison.Ordinal))
                {
                    answer = Products;
                }
                else
                {
                    _bodies.Add(body);
                    answer = Answer;
                }
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(answer, Encoding.UTF8, "application/json"),
            };
        }
    }
}
