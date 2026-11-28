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

using FiftyOne.Pipeline.CloudRequestEngine.Data;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines;
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.CloudRequestEngine.Tests
{
    /// <summary>
    /// The Cloud Request Engine redeems a sealed Human Confidence result
    /// through its own JSON call, and the Derived Cloud Engine gives the
    /// two Properties. The cloud service is a recorded stand-in, so no
    /// test touches the network.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/human-confidence.md#required-tests">Specification</see>
    /// </summary>
    [TestClass]
    public class HumanConfidenceTests
    {
        private const string LicenseKey = "LICENSEkeyZXCV98765";
        private const string ResourceKey = "resource_key";
        private const string SealedResult = "SEALEDresultQWERTY123456";
        private const string Page = "1b4e28ba-2fa1-11d2-883f-0016d3cca427";
        private const string OtherPage = "6fa459ea-ee8a-3ca4-894e-db77e160355e";
        private const string UserAgent = "Mozilla/5.0 (X11; Linux x86_64)";

        /// <summary>
        /// The answer to a call that asks the cloud service for Human
        /// Confidence, before the browser tests.
        /// </summary>
        private const string PassiveAnswer =
            "{\"device\":{\"ismobile\":false},\"derived\":{" +
            "\"humanconfidence\":\"Low\"," +
            "\"humanconfidencesource\":\"Passive\"}}";

        /// <summary>
        /// Records every request, with its body, and answers the data
        /// endpoint with <see cref="Answer"/>.
        /// </summary>
        private sealed class RecordingHandler : HttpMessageHandler
        {
            public List<(HttpRequestMessage Request, string Body)> Requests
            { get; } = new List<(HttpRequestMessage, string)>();

            public Func<HttpResponseMessage> Answer { get; set; } =
                () => Json(PassiveAnswer);

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                var body = request.Content == null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(
                        cancellationToken);
                lock (Requests)
                {
                    Requests.Add((request, body));
                }
                switch (request.RequestUri.Segments.Last())
                {
                    case Constants.EVIDENCE_KEYS_FILENAME:
                        return Json("[\"query.user-agent\"," +
                            "\"header.user-agent\"]");
                    case Constants.PROPERTIES_FILENAME:
                        return Json("{\"Products\":{" +
                            "\"device\":{\"DataTier\":\"CloudV4\"," +
                            "\"Properties\":[{\"Name\":\"IsMobile\"," +
                            "\"Type\":\"Boolean\",\"Category\":\"Device\"}]}," +
                            "\"derived\":{\"DataTier\":\"CloudV4\"," +
                            "\"Properties\":[{\"Name\":\"HumanConfidence\"," +
                            "\"Type\":\"String\",\"Category\":\"Bot Detection\"}," +
                            "{\"Name\":\"HumanConfidenceSource\"," +
                            "\"Type\":\"String\",\"Category\":\"Bot Detection\"}]}," +
                            "\"browserderived\":{\"DataTier\":\"CloudV4\"," +
                            "\"Properties\":[{\"Name\":\"BrowserTestsJavaScript\"," +
                            "\"Type\":\"JavaScript\",\"Category\":\"Bot Detection\"}]}}}");
                    default:
                        return Answer();
                }
            }

            /// <summary>
            /// The bodies of the calls to the data endpoint, as fields.
            /// </summary>
            public List<Dictionary<string, string>> DataCalls()
            {
                lock (Requests)
                {
                    return Requests
                        .Where(r => r.Request.RequestUri.Segments.Last() ==
                            Constants.DATA_FILENAME)
                        .Select(r => Fields(r.Body))
                        .ToList();
                }
            }

            /// <summary>
            /// The raw bodies of the calls to the data endpoint.
            /// </summary>
            public List<string> DataBodies()
            {
                lock (Requests)
                {
                    return Requests
                        .Where(r => r.Request.RequestUri.Segments.Last() ==
                            Constants.DATA_FILENAME)
                        .Select(r => r.Body)
                        .ToList();
                }
            }
        }

        private sealed class RecordingLogger : ILogger
        {
            public List<string> Entries { get; } = new List<string>();

            public IDisposable BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception exception,
                Func<TState, Exception, string> formatter)
            {
                lock (Entries)
                {
                    Entries.Add(logLevel + " " + formatter(state, exception) +
                        " " + exception);
                }
            }
        }

        private sealed class RecordingLoggerFactory : ILoggerFactory
        {
            public RecordingLogger Logger { get; } = new RecordingLogger();
            public void AddProvider(ILoggerProvider provider) { }
            public ILogger CreateLogger(string categoryName) => Logger;
            public void Dispose() { }
        }

        private RecordingHandler _handler;
        private RecordingLoggerFactory _loggers;
        private readonly List<IDisposable> _disposables =
            new List<IDisposable>();

        [TestInitialize]
        public void Init()
        {
            Environment.SetEnvironmentVariable(
                Constants.CLOUD_ENDPOINT_ENV_VAR, null);
            _handler = new RecordingHandler();
            _loggers = new RecordingLoggerFactory();
        }

        [TestCleanup]
        public void Cleanup()
        {
            foreach (var disposable in _disposables)
            {
                disposable.Dispose();
            }
        }

        private static HttpResponseMessage Json(
            string body,
            HttpStatusCode status = HttpStatusCode.OK) =>
            new HttpResponseMessage(status)
            {
                Content = new StringContent(body),
            };

        private static Dictionary<string, string> Fields(string body)
        {
            return body.Split('&')
                .Where(p => p.Length > 0)
                .Select(p => p.Split('=', 2))
                .ToDictionary(
                    p => WebUtility.UrlDecode(p[0]),
                    p => WebUtility.UrlDecode(p[1]),
                    StringComparer.OrdinalIgnoreCase);
        }

        private static string RedeemAnswer(string value) =>
            "{\"device\":{\"ismobile\":false},\"derived\":{" +
            "\"humanconfidence\":" + value + "," +
            "\"humanconfidencesource\":\"Active\"}}";

        private CloudRequestEngineBuilder NewEngineBuilder(
            string licenseKey = LicenseKey)
        {
            return new CloudRequestEngineBuilder(
                _loggers, new HttpClient(_handler))
                .SetResourceKey(ResourceKey)
                .SetLicenseKey(licenseKey);
        }

        /// <summary>
        /// A Pipeline of a Cloud Request Engine and a Derived Cloud
        /// Engine, with exceptions suppressed so a test can read the Flow
        /// Data after a failed request.
        /// </summary>
        private IPipeline NewPipeline(
            CloudRequestEngineBuilder engineBuilder = null,
            Action<DerivedCloudEngineBuilder> configureDerived = null,
            bool suppress = true)
        {
            var engine = (engineBuilder ?? NewEngineBuilder()).Build();
            var derivedBuilder = new DerivedCloudEngineBuilder(_loggers);
            configureDerived?.Invoke(derivedBuilder);
            var pipeline = new PipelineBuilder(_loggers)
                .AddFlowElement(engine)
                .AddFlowElement(derivedBuilder.Build())
                .SetSuppressProcessExceptions(suppress)
                .Build();
            _disposables.Add(pipeline);
            return pipeline;
        }

        private static IFlowData Process(
            IPipeline pipeline,
            params (string Key, object Value)[] evidence)
        {
            var data = pipeline.CreateFlowData();
            data.AddEvidence("header.User-Agent", UserAgent);
            foreach (var (key, value) in evidence)
            {
                data.AddEvidence(key, value);
            }
            data.Process();
            return data;
        }

        /// <summary>
        /// The session id every page's calls carry. Two tabs can share one,
        /// where the second is given the first's cached client script, so
        /// the tests give every page the same one.
        /// </summary>
        private const string SharedSession = "2c5ea4c0-4067-11e9-8bad-9b1deb4d3b7d";

        /// <summary>
        /// The page id each page was issued, by the page's name in a test.
        /// </summary>
        private readonly Dictionary<string, string> _pageIds =
            new Dictionary<string, string>();

        /// <summary>
        /// The pages whose sealed result has been sent back, so a later
        /// call from the page is from a page rendered again.
        /// </summary>
        private readonly HashSet<string> _sent = new HashSet<string>();

        /// <summary>
        /// Renders the page named, so the server issues it a page id, as a
        /// server does before it writes the client script's address into
        /// the page.
        /// </summary>
        private string RenderPage(IPipeline pipeline, string page = Page)
        {
            _pageIds[page] = pipeline
                .GetElement<FlowElements.CloudRequestEngine>()
                .IssueHumanConfidencePage();
            _sent.Remove(page);
            return _pageIds[page];
        }

        /// <summary>
        /// A call from the page named, as its client script makes one with
        /// the page id from the script's address, rendering the page first
        /// where it has not been or its sealed result was sent back.
        /// Returns the transaction the call carried.
        /// </summary>
        private string CallFromPage(
            IPipeline pipeline,
            string page = Page,
            string userAgent = UserAgent)
        {
            if (_pageIds.ContainsKey(page) == false || _sent.Contains(page))
            {
                RenderPage(pipeline, page);
            }
            var before = _handler.DataCalls().Count;
            using (var data = Process(pipeline,
                ("query.session-id", SharedSession), ("query.sequence", 2),
                ("query.humanconfidencepage", _pageIds[page]),
                ("header.User-Agent", userAgent)))
            {
                // Waits for a lazy loaded result.
                _ = Derived(data).HumanConfidenceSource;
            }
            var call = _handler.DataCalls().Skip(before).Single();
            Assert.IsTrue(call.TryGetValue("transaction", out var transaction),
                "A call from a page carries the page's transaction.");
            return transaction;
        }

        /// <summary>
        /// The page's request that sends the sealed result back, with the
        /// page id the page was issued, where it was issued one.
        /// </summary>
        private IFlowData Redeem(
            IPipeline pipeline,
            string page = Page,
            params (string Key, object Value)[] more)
        {
            var evidence = new List<(string, object)>
            {
                ("query.humanconfidence", SealedResult),
            };
            if (_pageIds.TryGetValue(page, out var pageId))
            {
                evidence.Add(("query.humanconfidencepage", pageId));
                _sent.Add(page);
            }
            return Process(pipeline, evidence.Concat(more).ToArray());
        }

        /// <summary>
        /// The call to the cloud service made no redemption: it carried
        /// neither the sealed result nor a transaction.
        /// </summary>
        private void AssertNoRedeemingCall()
        {
            var call = _handler.DataCalls().Last();
            Assert.IsFalse(call.ContainsKey("humanconfidence"),
                "The sealed result is not sent.");
            Assert.IsFalse(call.ContainsKey("transaction"),
                "No transaction is sent.");
        }

        private static IDerivedCloudData Derived(IFlowData data) =>
            data.Get<IDerivedCloudData>();

        /// <summary>
        /// Both Properties are Medium and Active, and no error comes from
        /// the Derived Cloud Engine.
        /// </summary>
        private static void AssertNotRedeemed(IFlowData data)
        {
            var derived = Derived(data);
            Assert.AreEqual("Medium", derived.HumanConfidence.Value);
            Assert.AreEqual("Active", derived.HumanConfidenceSource.Value);
            Assert.IsFalse(
                data.Errors?.Any(e => e.FlowElement is DerivedCloudEngine) ?? false,
                "A read of the two Properties adds no error of its own.");
        }

        /// <summary>
        /// A sealed result that redeems gives the Value in the response as
        /// HumanConfidence, with HumanConfidenceSource Active. The call is
        /// the engine's one call to the json endpoint, holding the sealed
        /// result exactly as it arrived, the transaction kept for the page
        /// and the License Key from the configuration.
        /// </summary>
        [TestMethod]
        [DataRow("High")]
        [DataRow("Medium")]
        [DataRow("Low")]
        public void Redeemed_GivesTheValueAndActive(string value)
        {
            var pipeline = NewPipeline();
            var transaction = CallFromPage(pipeline);
            var callsBefore = _handler.Requests.Count;
            _handler.Answer = () => Json(RedeemAnswer("\"" + value + "\""));

            using (var data = Redeem(pipeline))
            {
                Assert.AreEqual(value, Derived(data).HumanConfidence.Value);
                Assert.AreEqual("Active",
                    Derived(data).HumanConfidenceSource.Value);
                Assert.AreEqual(0, data.Errors?.Count ?? 0);
            }

            var requests = _handler.Requests.Skip(callsBefore).ToList();
            Assert.HasCount(1, requests,
                "Redemption makes no request of its own.");
            Assert.AreEqual(Constants.DATA_ENDPOINT_DEFAULT,
                requests[0].Request.RequestUri.ToString(),
                "Redemption uses the json endpoint and no other.");
            var call = Fields(requests[0].Body);
            Assert.AreEqual(SealedResult, call["humanconfidence"]);
            Assert.AreEqual(transaction, call["transaction"]);
            Assert.AreEqual(LicenseKey, call["license"]);
            Assert.AreEqual(ResourceKey, call["resource"]);
            Assert.IsFalse(call.ContainsKey("humanconfidencepage"),
                "The page id stays on this server.");
        }

        /// <summary>
        /// The Derived Cloud Engine gives the two Properties only after a
        /// Cloud Request Engine, so a Pipeline with no Cloud Request
        /// Engine is refused when it is built, saying why, rather than
        /// giving a Value it could not have redeemed.
        /// </summary>
        [TestMethod]
        public void NotRedeemed_NoCloudRequestEngine()
        {
            var derived = new DerivedCloudEngineBuilder(_loggers).Build();
            _disposables.Add(derived);

            var refused = Assert.ThrowsExactly<
                Core.Exceptions.PipelineConfigurationException>(
                () => new PipelineBuilder(_loggers)
                    .AddFlowElement(derived)
                    .SetSuppressProcessExceptions(true)
                    .Build());

            StringAssert.Contains(refused.Message, "CloudRequestEngine");
            Assert.IsEmpty(_handler.Requests);
        }

        /// <summary>
        /// Redemption needs the License Key, so an engine without one
        /// redeems nothing, even where the Evidence offers one, and its
        /// call carries neither the sealed result nor a transaction.
        /// </summary>
        [TestMethod]
        public void NotRedeemed_NoLicenseKey()
        {
            var pipeline = NewPipeline(NewEngineBuilder(licenseKey: null));
            using (var page = Process(pipeline,
                ("query.session-id", Page), ("query.sequence", 2)))
            {
            }
            _handler.Answer = () => Json(RedeemAnswer("\"High\""));

            using var data = Redeem(pipeline, Page,
                ("query.license", "EVIDENCELICENSE1234"),
                ("header.licence", "EVIDENCELICENSE5678"),
                ("query.licensekeys", "EVIDENCELICENSE9012"));

            AssertNotRedeemed(data);
            foreach (var call in _handler.DataCalls())
            {
                Assert.IsFalse(call.ContainsKey("license"),
                    "The License Key is never taken from Evidence.");
                Assert.IsFalse(call.ContainsKey("licence"));
                Assert.IsFalse(call.ContainsKey("transaction"));
                Assert.IsFalse(call.ContainsKey("humanconfidence"));
            }
        }

        /// <summary>
        /// The License Key in the call is the configured one, whatever the
        /// Evidence holds.
        /// </summary>
        [TestMethod]
        public void Redeemed_LicenseKeyNeverFromEvidence()
        {
            var pipeline = NewPipeline();
            CallFromPage(pipeline);
            _handler.Answer = () => Json(RedeemAnswer("\"High\""));

            using var data = Redeem(pipeline, Page,
                ("query.license", "EVIDENCELICENSE1234"),
                ("cookie.licence", "EVIDENCELICENSE5678"));

            Assert.AreEqual(LicenseKey,
                _handler.DataCalls().Last()["license"]);
            Assert.IsFalse(_handler.DataCalls().Last().ContainsKey("licence"));
        }

        /// <summary>
        /// A page id the engine did not issue, or one it has dropped, has
        /// no transaction kept for it, so nothing is redeemed and the
        /// sealed result is not sent.
        /// </summary>
        [TestMethod]
        public void NotRedeemed_UnknownPageId()
        {
            var pipeline = NewPipeline();
            CallFromPage(pipeline);
            _handler.Answer = () => Json(RedeemAnswer("\"High\""));

            using var data = Process(pipeline,
                ("query.humanconfidence", SealedResult),
                ("query.humanconfidencepage", "0123456789abcdef0123456789abcdef"));

            AssertNotRedeemed(data);
            AssertNoRedeemingCall();
        }

        /// <summary>
        /// A request with a sealed result but no page id names no page, so
        /// nothing is redeemed, whatever transaction or session id it
        /// holds.
        /// </summary>
        [TestMethod]
        public void NotRedeemed_NoPageId()
        {
            var pipeline = NewPipeline();
            var transaction = CallFromPage(pipeline);
            _handler.Answer = () => Json(RedeemAnswer("\"High\""));

            using var data = Process(pipeline,
                ("query.humanconfidence", SealedResult),
                ("query.session-id", Page),
                ("query.transaction", transaction));

            AssertNotRedeemed(data);
            AssertNoRedeemingCall();
        }

        /// <summary>
        /// A transaction is used once, so a second sealed result from the
        /// same page is not redeemed and is not sent.
        /// </summary>
        [TestMethod]
        public void NotRedeemed_TransactionUsedBefore()
        {
            var pipeline = NewPipeline();
            CallFromPage(pipeline);
            _handler.Answer = () => Json(RedeemAnswer("\"High\""));
            using (var first = Redeem(pipeline))
            {
                Assert.AreEqual("High", Derived(first).HumanConfidence.Value);
            }

            using var second = Redeem(pipeline);

            AssertNotRedeemed(second);
            AssertNoRedeemingCall();
        }

        /// <summary>
        /// Each page has its own transaction, so two pages open at once, as
        /// in two tabs, both redeem, each with its own transaction, though
        /// their client scripts carry the same session id, as they do where
        /// the second tab is given the first's cached script.
        /// </summary>
        [TestMethod]
        public void TwoPagesOpenAtOnce_BothRedeem()
        {
            var pipeline = NewPipeline();
            // Both tabs are open before either calls.
            RenderPage(pipeline, Page);
            RenderPage(pipeline, OtherPage);
            var first = CallFromPage(pipeline, Page);
            var second = CallFromPage(pipeline, OtherPage);
            Assert.AreNotEqual(first, second);
            _handler.Answer = () => Json(RedeemAnswer("\"High\""));

            using (var data = Redeem(pipeline, Page))
            {
                Assert.AreEqual("High", Derived(data).HumanConfidence.Value);
                Assert.AreEqual(first, _handler.DataCalls().Last()["transaction"]);
            }
            using (var data = Redeem(pipeline, OtherPage))
            {
                Assert.AreEqual("High", Derived(data).HumanConfidence.Value);
                Assert.AreEqual(second, _handler.DataCalls().Last()["transaction"]);
            }
            Assert.AreNotEqual(_pageIds[Page], _pageIds[OtherPage]);
        }

        /// <summary>
        /// In a recovery period the engine makes no request, so nothing is
        /// redeemed. The engine's error is recorded once, as on any other
        /// request.
        /// </summary>
        [TestMethod]
        public void NotRedeemed_RecoveryPeriod()
        {
            var pipeline = NewPipeline(NewEngineBuilder()
                .SetUseExponentialBackoff(false)
                .SetRecoverySeconds(30)
                .SetFailuresToEnterRecovery(1));
            CallFromPage(pipeline);
            _handler.Answer = () => Json("{\"errors\":[\"Fault\"]}",
                HttpStatusCode.InternalServerError);
            using (var failed = Process(pipeline))
            {
            }
            var calls = _handler.DataCalls().Count;
            _handler.Answer = () => Json(RedeemAnswer("\"High\""));

            using var data = Redeem(pipeline);

            AssertNotRedeemed(data);
            Assert.HasCount(calls, _handler.DataCalls(),
                "No request is made in a recovery period.");
            Assert.AreEqual(1, data.Errors.Count(e =>
                e.FlowElement is FlowElements.CloudRequestEngine));
        }

        /// <summary>
        /// A request that fails, or does not complete in time, redeems
        /// nothing. The engine's error is recorded once.
        /// </summary>
        [TestMethod]
        [DataRow("status")]
        [DataRow("timeout")]
        [DataRow("transport")]
        [DataRow("not json")]
        public void NotRedeemed_RequestFailed(string failure)
        {
            var pipeline = NewPipeline();
            CallFromPage(pipeline);
            _handler.Answer = () =>
            {
                switch (failure)
                {
                    case "status":
                        return Json(RedeemAnswer("\"High\""),
                            HttpStatusCode.InternalServerError);
                    case "timeout":
                        // What HttpClient throws when its Timeout passes.
                        throw new TaskCanceledException(
                            "The request was canceled due to the " +
                            "configured HttpClient.Timeout.");
                    case "transport":
                        throw new HttpRequestException("Connection refused");
                    default:
                        return Json("High");
                }
            };

            using var data = Redeem(pipeline);

            AssertNotRedeemed(data);
            Assert.AreEqual(1, data.Errors.Count(e =>
                e.FlowElement is FlowElements.CloudRequestEngine));
        }

        /// <summary>
        /// With exceptions not suppressed, the engine's failure is thrown
        /// from Process as on any other request, and the two Properties
        /// still read Medium and Active afterwards.
        /// </summary>
        [TestMethod]
        public void NotRedeemed_RequestFailed_ExceptionsNotSuppressed()
        {
            var pipeline = NewPipeline(suppress: false);
            CallFromPage(pipeline);
            _handler.Answer = () => Json("{\"errors\":[\"Fault\"]}",
                HttpStatusCode.InternalServerError);
            using var data = pipeline.CreateFlowData();
            data.AddEvidence("query.humanconfidence", SealedResult);
            data.AddEvidence("query.humanconfidencepage", _pageIds[Page]);

            Assert.ThrowsExactly<AggregateException>(() => data.Process());

            AssertNotRedeemed(data);
        }

        /// <summary>
        /// With lazy loading, a failure met after processing has finished
        /// would reach the reader from the read. For the two Properties on
        /// a request that carries a sealed result, the read gives Medium
        /// and Active.
        /// </summary>
        [TestMethod]
        public void NotRedeemed_RequestFailed_LazyLoading()
        {
            var pipeline = NewPipeline(
                NewEngineBuilder().SetLazyLoading(
                    new LazyLoadingConfiguration(5000)),
                d => d.SetLazyLoading(new LazyLoadingConfiguration(5000)));
            CallFromPage(pipeline);
            _handler.Answer = () => throw new HttpRequestException("Refused");

            using var data = Redeem(pipeline);

            var derived = Derived(data);
            Assert.AreEqual("Medium", derived.HumanConfidence.Value);
            Assert.AreEqual("Active", derived.HumanConfidenceSource.Value);
        }

        /// <summary>
        /// A response whose errors list says HumanConfidence is not
        /// covered by the credential redeems nothing. The engine treats a
        /// response with any errors list as a failed request, so this case
        /// takes the same path as every other failure, and the engine
        /// records the error once.
        /// </summary>
        [TestMethod]
        public void NotRedeemed_ErrorsSayNotCovered()
        {
            var pipeline = NewPipeline();
            CallFromPage(pipeline);
            _handler.Answer = () => Json(
                "{\"errors\":[\"HumanConfidence is not covered by the " +
                "credential.\"],\"derived\":{\"humanconfidence\":\"High\"}}");

            using var data = Redeem(pipeline);

            AssertNotRedeemed(data);
            var error = data.Errors.Single(e =>
                e.FlowElement is FlowElements.CloudRequestEngine);
            StringAssert.Contains(error.ExceptionData.Message,
                "HumanConfidence is not covered");
        }

        /// <summary>
        /// A response with no derived member, or no Value a sealed result
        /// redeems to, redeems nothing.
        /// </summary>
        [TestMethod]
        [DataRow("{\"device\":{\"ismobile\":false}}",
            DisplayName = "No derived member")]
        [DataRow("{\"derived\":{\"humanconfidencesource\":\"Active\"}}",
            DisplayName = "No humanconfidence")]
        [DataRow("{\"derived\":{\"humanconfidence\":null," +
            "\"humanconfidencenullreason\":\"Not in the key\"}}",
            DisplayName = "Null")]
        [DataRow("{\"derived\":{\"humanconfidence\":\"Unknown\"}}",
            DisplayName = "Unknown")]
        [DataRow("{\"derived\":{\"humanconfidence\":\"high\"}}",
            DisplayName = "Wrong case")]
        [DataRow("{\"derived\":{\"humanconfidence\":\"SEALEDagain\"}}",
            DisplayName = "Another sealed result")]
        [DataRow("{\"derived\":{\"humanconfidence\":3}}",
            DisplayName = "A number")]
        [DataRow("{\"derived\":\"High\"}",
            DisplayName = "Derived not an object")]
        public void NotRedeemed_ResponseHoldsNoValue(string answer)
        {
            var pipeline = NewPipeline();
            CallFromPage(pipeline);
            _handler.Answer = () => Json(answer);

            using var data = Redeem(pipeline);

            AssertNotRedeemed(data);
        }

        /// <summary>
        /// The sealed result is redeemed once for a request. Every read
        /// gives the same Value and makes no further request.
        /// </summary>
        [TestMethod]
        public void TwoReads_MakeOneRequest()
        {
            var pipeline = NewPipeline();
            CallFromPage(pipeline);
            _handler.Answer = () => Json(RedeemAnswer("\"High\""));
            var before = _handler.Requests.Count;

            using var data = Redeem(pipeline);
            var first = Derived(data).HumanConfidence.Value;
            var second = data.Get<IDerivedCloudData>().HumanConfidence.Value;
            var byName = ((IAspectPropertyValue<string>)
                data.Get(Constants.DERIVED_ELEMENT_DATA_KEY)
                [Constants.HUMAN_CONFIDENCE]).Value;

            Assert.AreEqual("High", first);
            Assert.AreEqual(first, second);
            Assert.AreEqual(first, byName);
            Assert.HasCount(before + 1, _handler.Requests);
        }

        /// <summary>
        /// A request that carries a sealed result is neither served from
        /// the results cache, though an earlier request with the same
        /// cached Evidence is in it, nor stored in it.
        /// </summary>
        [TestMethod]
        public void Cache_RequestWithSealedResult_NeverStoredOrServed()
        {
            var pipeline = NewPipeline(NewEngineBuilder()
                .SetCacheSize(10)
                .SetCacheHitOrMiss(true));
            CallFromPage(pipeline);
            using (var cached = Process(pipeline))
            {
            }
            using (var hit = Process(pipeline))
            {
                Assert.IsTrue(hit.GetFromElement(
                    pipeline.GetElement<FlowElements.CloudRequestEngine>())
                    .CacheHit, "The cache is in use.");
            }
            _handler.Answer = () => Json(RedeemAnswer("\"High\""));
            var before = _handler.DataCalls().Count;

            using (var first = Redeem(pipeline))
            {
                Assert.AreEqual("High", Derived(first).HumanConfidence.Value);
            }
            // Another user agent, so the page's call is not answered from
            // the cache.
            CallFromPage(pipeline, Page, "Another user agent");
            _handler.Answer = () => Json(RedeemAnswer("\"Low\""));
            using (var second = Redeem(pipeline))
            {
                Assert.AreEqual("Low", Derived(second).HumanConfidence.Value);
            }

            Assert.HasCount(before + 3, _handler.DataCalls(),
                "Each redemption, and the call from the page between them, " +
                "goes to the cloud service.");

            // A redemption's response is not stored either, so a later
            // request with the same cached Evidence and no sealed result
            // goes to the cloud service and gets its own answer.
            CallFromPage(pipeline, OtherPage, "A third user agent");
            _handler.Answer = () => Json(RedeemAnswer("\"High\""));
            using (var redeemed = Redeem(pipeline, OtherPage,
                ("header.User-Agent", "Redeeming agent")))
            {
                Assert.AreEqual("High", Derived(redeemed).HumanConfidence.Value);
            }
            _handler.Answer = () => Json(PassiveAnswer);
            using (var later = Process(pipeline,
                ("header.User-Agent", "Redeeming agent")))
            {
                Assert.IsFalse(later.GetFromElement(
                    pipeline.GetElement<FlowElements.CloudRequestEngine>())
                    .CacheHit, "The redemption's response was not stored.");
                Assert.AreEqual("Passive",
                    Derived(later).HumanConfidenceSource.Value);
            }
        }

        /// <summary>
        /// The Derived and Browser Derived Cloud Engines take no results
        /// cache, because their Evidence filters do not say what the
        /// response depends on, and one cache entry would answer every
        /// request.
        /// </summary>
        [TestMethod]
        public void Cache_RefusedByTheEnginesThatReadTheResponse()
        {
            var derived = Assert.ThrowsExactly<
                Core.Exceptions.PipelineConfigurationException>(() =>
                new DerivedCloudEngineBuilder(_loggers).SetCacheSize(10).Build());
            StringAssert.Contains(derived.Message, "CloudRequestEngine");
            Assert.ThrowsExactly<Core.Exceptions.PipelineConfigurationException>(
                () => new BrowserDerivedCloudEngineBuilder(_loggers)
                    .SetCacheSize(10).Build());
        }

        /// <summary>
        /// A Pipeline that does not give HumanConfidence issues no page id
        /// and keeps nothing, whatever its License Key.
        /// </summary>
        [TestMethod]
        public void Transaction_NoneWithoutHumanConfidence()
        {
            var engine = NewEngineBuilder().Build();
            using var pipeline = new PipelineBuilder(_loggers)
                .AddFlowElement(engine)
                .Build();

            Assert.IsNull(engine.IssueHumanConfidencePage());
            using (var data = Process(pipeline,
                ("query.humanconfidencepage", "0123456789abcdef0123456789abcdef")))
            {
            }

            Assert.IsFalse(_handler.DataCalls().Single()
                .ContainsKey("transaction"));
            Assert.AreEqual(0, engine.KeptTransactions);
        }

        /// <summary>
        /// An engine with no License Key issues no page id, as nothing
        /// could be redeemed for the page.
        /// </summary>
        [TestMethod]
        public void Transaction_NoneWithoutALicenseKey()
        {
            var pipeline = NewPipeline(NewEngineBuilder(licenseKey: null));
            var engine = pipeline.GetElement<FlowElements.CloudRequestEngine>();

            Assert.IsNull(engine.IssueHumanConfidencePage());
            Assert.AreEqual(0, engine.KeptTransactions);
        }

        /// <summary>
        /// Requested Properties that name HumanConfidence are not enough
        /// for page ids to be issued. Without a Derived Cloud Engine, the
        /// Pipeline's Evidence filter takes neither the page id nor the
        /// sealed result from a request, so a page id would never come
        /// back. With one, it takes both.
        /// </summary>
        [TestMethod]
        public void Transaction_NeedsADerivedCloudEngine()
        {
            var engine = NewEngineBuilder()
                .SetRequestedProperties(new List<string>
                    { "Derived.HumanConfidence" })
                .Build();
            using var without = new PipelineBuilder(_loggers)
                .AddFlowElement(engine)
                .Build();

            Assert.IsFalse(without.EvidenceKeyFilter.Include(
                "query.humanconfidencepage"));
            Assert.IsNull(engine.IssueHumanConfidencePage());
            Assert.AreEqual(0, engine.KeptTransactions);

            var with = NewPipeline();
            Assert.IsTrue(with.EvidenceKeyFilter.Include(
                "query.humanconfidencepage"));
            Assert.IsTrue(with.EvidenceKeyFilter.Include(
                "query.humanconfidence"));
            Assert.IsNotNull(with.GetElement<FlowElements.CloudRequestEngine>()
                .IssueHumanConfidencePage());
        }

        /// <summary>
        /// The number of pages kept, and how long, can be set on the
        /// builder, and the oldest page gives way when every place is
        /// taken.
        /// </summary>
        [TestMethod]
        public void Builder_SetsHowManyPagesAreKept()
        {
            var pipeline = NewPipeline(NewEngineBuilder()
                .SetHumanConfidencePageCapacity(2)
                .SetHumanConfidencePageLifetimeSeconds(60));
            var engine = pipeline.GetElement<FlowElements.CloudRequestEngine>();

            var first = engine.IssueHumanConfidencePage();
            engine.IssueHumanConfidencePage();
            engine.IssueHumanConfidencePage();

            Assert.AreEqual(2, engine.HumanConfidencePageCapacity);
            Assert.AreEqual(2, engine.KeptTransactions);
            using (var data = Process(pipeline,
                ("query.humanconfidencepage", first)))
            {
            }
            Assert.IsFalse(_handler.DataCalls().Last().ContainsKey("transaction"),
                "The oldest page gave way.");
            Assert.AreEqual(Constants.HUMAN_CONFIDENCE_PAGE_CAPACITY_DEFAULT,
                NewEngineBuilder().Build().HumanConfidencePageCapacity);
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => NewEngineBuilder().SetHumanConfidencePageCapacity(0));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => NewEngineBuilder().SetHumanConfidencePageLifetimeSeconds(0));
        }

        /// <summary>
        /// A warning about two pieces of Evidence with the same name names
        /// only keys whose whole name matches, so the page id, whose name
        /// merely ends the same way, is not written to the log.
        /// </summary>
        [TestMethod]
        public void ConflictWarning_MatchesWholeNames_AndHoldsNoPageId()
        {
            var pipeline = NewPipeline();
            var pageId = RenderPage(pipeline);

            using (var data = Process(pipeline,
                ("cookie.page", "fromcookie"),
                ("header.page", "fromheader"),
                ("query.humanconfidencepage", pageId)))
            {
            }

            string warning;
            lock (_loggers.Logger.Entries)
            {
                warning = _loggers.Logger.Entries.Single(e =>
                    e.Contains("evidence conflicts"));
            }
            StringAssert.Contains(warning, "cookie.page=fromcookie");
            Assert.DoesNotContain("humanconfidencepage", warning);
            Assert.DoesNotContain(pageId, warning);
        }

        /// <summary>
        /// A call carries a transaction only with a page id the engine
        /// issued and still keeps. A page id in another form, however long,
        /// or one it never issued, looks up nothing.
        /// </summary>
        [TestMethod]
        [DataRow("0123456789abcdef0123456789abcdef")]
        [DataRow("0123456789ABCDEF0123456789ABCDEF")]
        [DataRow("not a page id")]
        public void Transaction_NoneForAPageIdNotIssued(string pageId)
        {
            var pipeline = NewPipeline();
            var engine = pipeline.GetElement<FlowElements.CloudRequestEngine>();
            RenderPage(pipeline);

            using (var data = Process(pipeline,
                ("query.humanconfidencepage", pageId)))
            {
            }
            using (var data = Process(pipeline,
                ("query.humanconfidencepage", new string('x', 5000))))
            {
            }

            foreach (var call in _handler.DataCalls())
            {
                Assert.IsFalse(call.ContainsKey("transaction"));
            }
            Assert.AreEqual(1, engine.KeptTransactions);
        }

        /// <summary>
        /// Ordinary traffic in a web Pipeline, where the Sequence Element
        /// gives every request a session id, issues no page and keeps
        /// nothing, and the results cache still answers repeated requests.
        /// </summary>
        [TestMethod]
        public void OrdinaryTraffic_WithASequenceElement_KeepsNothingAndIsCached()
        {
            var engine = NewEngineBuilder()
                .SetCacheSize(10)
                .SetCacheHitOrMiss(true)
                .Build();
            using var pipeline = new PipelineBuilder(_loggers)
                .AddFlowElement(new Engines.FiftyOne.FlowElements.SequenceElementBuilder(
                    _loggers).Build())
                .AddFlowElement(engine)
                .AddFlowElement(new DerivedCloudEngineBuilder(_loggers).Build())
                .Build();

            for (var i = 0; i < 5; i++)
            {
                using var data = Process(pipeline);
                Assert.IsTrue(data.TryGetEvidence(
                    "query.session-id", out object session) &&
                    session is string text && text.Length == 36,
                    "The Sequence Element gave the request a session id.");
                Assert.AreEqual(i > 0, data.GetFromElement(engine).CacheHit,
                    "Only the first request goes to the cloud service.");
            }

            Assert.HasCount(1, _handler.DataCalls());
            Assert.IsFalse(_handler.DataCalls().Single().ContainsKey("transaction"));
            Assert.AreEqual(0, engine.KeptTransactions);
        }

        /// <summary>
        /// The call holds one transaction, being the one the engine kept,
        /// sent as transaction, where the Evidence also holds transactions
        /// and challenges under any prefix and in any letter case.
        /// </summary>
        [TestMethod]
        public void Transaction_OnlyTheKeptOne()
        {
            var pipeline = NewPipeline();
            var transaction = CallFromPage(pipeline);
            _handler.Answer = () => Json(RedeemAnswer("\"High\""));

            using var data = Redeem(pipeline, Page,
                ("query.transaction", "fromquery"),
                ("header.Transaction", "fromheader"),
                ("cookie.TRANSACTION", "fromcookie"),
                ("query.challenge", "challengequery"),
                ("server.Challenge", "challengeserver"));

            Assert.AreEqual("High", Derived(data).HumanConfidence.Value,
                "The call was made and redeemed.");
            var body = _handler.DataBodies().Last();
            var names = body.Split('&')
                .Select(p => WebUtility.UrlDecode(p.Split('=')[0]))
                .ToList();
            Assert.AreEqual(1, names.Count(n => n.Equals(
                "transaction", StringComparison.OrdinalIgnoreCase)));
            Assert.AreEqual(0, names.Count(n => n.Equals(
                "challenge", StringComparison.OrdinalIgnoreCase)));
            Assert.AreEqual(transaction,
                _handler.DataCalls().Last()["transaction"]);
            foreach (var other in new[] { "fromquery", "fromheader",
                "fromcookie", "challengequery", "challengeserver" })
            {
                Assert.DoesNotContain(other, body);
            }
        }

        /// <summary>
        /// A page's calls carry its own transaction and not one from the
        /// Evidence. Each page has a transaction of its own, and its later
        /// calls carry the same one.
        /// </summary>
        [TestMethod]
        public void Transaction_OnePerPage()
        {
            var pipeline = NewPipeline();

            var first = CallFromPage(pipeline);
            var again = CallFromPage(pipeline);
            var other = CallFromPage(pipeline, OtherPage);
            using (var data = Process(pipeline,
                ("query.humanconfidencepage", _pageIds[Page]),
                ("query.transaction", "fromquery")))
            {
            }

            Assert.AreEqual(first, again);
            Assert.AreNotEqual(first, other);
            Assert.AreEqual(first, _handler.DataCalls().Last()["transaction"]);
            Assert.AreEqual(32, first.Length);
        }

        /// <summary>
        /// A request that carries a page id may carry the page's
        /// transaction, so it is neither stored in nor served from the
        /// results cache, though another request with the same cached
        /// Evidence is in it.
        /// </summary>
        [TestMethod]
        public void Cache_RequestWithAPageId_NeverStoredOrServed()
        {
            var pipeline = NewPipeline(NewEngineBuilder()
                .SetCacheSize(10)
                .SetCacheHitOrMiss(true));
            var engine = pipeline.GetElement<FlowElements.CloudRequestEngine>();
            using (var cached = Process(pipeline))
            {
            }

            var first = CallFromPage(pipeline, Page);
            var second = CallFromPage(pipeline, OtherPage);
            using (var data = Process(pipeline,
                ("query.humanconfidencepage", _pageIds[Page])))
            {
                Assert.IsFalse(data.GetFromElement(engine).CacheHit);
            }

            Assert.HasCount(4, _handler.DataCalls());
            Assert.AreNotEqual(first, second);
            Assert.AreEqual(first, _handler.DataCalls().Last()["transaction"]);
        }

        /// <summary>
        /// A call that redeems names HumanConfidence where the configured
        /// Requested Properties do not, and the name it adds is left out
        /// of the report of properties the cloud service did not return.
        /// </summary>
        [TestMethod]
        public void RequestedProperties_HumanConfidenceAddedForRedemption()
        {
            var pipeline = NewPipeline(NewEngineBuilder()
                .SetRequestedProperties(new List<string> { "device.ismobile" }));
            CallFromPage(pipeline);
            Assert.AreEqual("device.ismobile",
                _handler.DataCalls().Last()["values"]);
            _handler.Answer = () => Json(
                "{\"device\":{\"ismobile\":false}}");

            using var data = Redeem(pipeline);

            Assert.AreEqual("device.ismobile,derived.HumanConfidence",
                _handler.DataCalls().Last()["values"]);
            Assert.IsFalse(_loggers.Logger.Entries.Any(e =>
                e.Contains("derived.HumanConfidence",
                    StringComparison.OrdinalIgnoreCase)),
                "The added name is not reported as missing.");
        }

        /// <summary>
        /// No log entry and no error message holds the sealed result, the
        /// License Key, or the transaction, in whole or in part, even where
        /// the cloud service repeats them in its answer.
        /// </summary>
        [TestMethod]
        [DataRow(HttpStatusCode.BadRequest, true)]
        [DataRow(HttpStatusCode.OK, false)]
        public void NoLogOrErrorHoldsTheSecrets(
            HttpStatusCode status,
            bool error)
        {
            var pipeline = NewPipeline(suppress: false);
            var transaction = CallFromPage(pipeline);
            var pageId = _pageIds[Page];
            var echo = $"{SealedResult} {transaction} {LicenseKey} {pageId} " +
                WebUtility.UrlEncode(SealedResult + "=" + transaction);
            _handler.Answer = () => Json(error
                ? $"{{\"errors\":[\"Refused {echo}\"]}}"
                : $"{{\"warnings\":[\"Seen {echo}\"]," +
                    "\"derived\":{\"humanconfidence\":\"High\"}}",
                status);

            var text = new System.Text.StringBuilder();
            using (var data = pipeline.CreateFlowData())
            {
                data.AddEvidence("query.humanconfidence", SealedResult);
                data.AddEvidence("header.humanconfidence", SealedResult);
                data.AddEvidence("query.humanconfidencepage", pageId);
                try
                {
                    data.Process();
                }
                catch (AggregateException ex)
                {
                    text.AppendLine(ex.ToString());
                }
                foreach (var e in data.Errors ?? new List<IFlowError>())
                {
                    text.AppendLine(e.ExceptionData?.ToString());
                }
                _ = Derived(data).HumanConfidence;
            }
            lock (_loggers.Logger.Entries)
            {
                text.AppendLine(string.Join(Environment.NewLine,
                    _loggers.Logger.Entries));
            }

            var all = text.ToString();
            StringAssert.Contains(all, error ? "Refused" : "Seen",
                "The answer's text is in the log or the error.");
            foreach (var secret in new[]
                { SealedResult, LicenseKey, transaction, pageId })
            {
                foreach (var part in new[]
                {
                    secret,
                    secret.Substring(0, 8),
                    secret.Substring(secret.Length - 6),
                })
                {
                    Assert.DoesNotContain(part, all,
                        $"Part of a secret appears in:{Environment.NewLine}{all}");
                }
            }
        }

        /// <summary>
        /// A read of either Property with a credential that does not carry
        /// it gives no value and the cloud service's reason.
        /// </summary>
        [TestMethod]
        public void CredentialWithoutTheProperty_NoValueAndTheReason()
        {
            var pipeline = NewPipeline();
            _handler.Answer = () => Json("{\"derived\":{" +
                "\"humanconfidence\":null," +
                "\"humanconfidencenullreason\":\"HumanConfidence is a paid property.\"," +
                "\"humanconfidencesource\":null," +
                "\"humanconfidencesourcenullreason\":\"HumanConfidenceSource is a paid property.\"}}");

            using var data = Process(pipeline);

            var derived = Derived(data);
            Assert.IsFalse(derived.HumanConfidence.HasValue);
            Assert.AreEqual("HumanConfidence is a paid property.",
                derived.HumanConfidence.NoValueMessage);
            Assert.IsFalse(derived.HumanConfidenceSource.HasValue);
            Assert.AreEqual("HumanConfidenceSource is a paid property.",
                derived.HumanConfidenceSource.NoValueMessage);
        }

        /// <summary>
        /// A response that leaves the Properties out gives no value, and a
        /// read says why.
        /// </summary>
        [TestMethod]
        public void CredentialWithoutTheProperty_LeftOut_SaysWhy()
        {
            var pipeline = NewPipeline();
            _handler.Answer = () => Json("{\"device\":{\"ismobile\":false}}");

            using var data = Process(pipeline);

            var missing = Assert.ThrowsExactly<PropertyMissingException>(
                () => Derived(data).HumanConfidence);
            Assert.IsFalse(string.IsNullOrEmpty(missing.Message));
            StringAssert.Contains(missing.Message, Constants.HUMAN_CONFIDENCE);
        }

        /// <summary>
        /// A request that carries no sealed result gives the two Properties
        /// from the derived member of the response, as the Pipeline does
        /// without Human Confidence.
        /// </summary>
        [TestMethod]
        public void NoSealedResult_PropertiesFromTheResponse()
        {
            var pipeline = NewPipeline();

            using var data = Process(pipeline);

            Assert.AreEqual("Low", Derived(data).HumanConfidence.Value);
            Assert.AreEqual("Passive",
                Derived(data).HumanConfidenceSource.Value);
            Assert.IsFalse(_handler.DataCalls().Single()
                .ContainsKey("humanconfidence"));
        }

        /// <summary>
        /// A request that names HumanConfidence is given the browser tests
        /// as the JavaScript Property browserderived.BrowserTestsJavaScript,
        /// which the Browser Derived Cloud Engine gives, so a client script
        /// served by the Pipeline runs them.
        /// </summary>
        [TestMethod]
        public void BrowserTests_GivenAsAJavaScriptProperty()
        {
            var engine = NewEngineBuilder().Build();
            using var pipeline = new PipelineBuilder(_loggers)
                .AddFlowElement(engine)
                .AddFlowElement(new DerivedCloudEngineBuilder(_loggers).Build())
                .AddFlowElement(
                    new BrowserDerivedCloudEngineBuilder(_loggers).Build())
                .Build();
            _handler.Answer = () => Json("{\"derived\":{" +
                "\"humanconfidence\":\"Medium\"," +
                "\"humanconfidencesource\":\"Passive\"}," +
                "\"browserderived\":{\"browsertestsjavascript\":" +
                "\"fod.tests();\"}}");

            using var data = Process(pipeline);

            var tests = data.Get<IBrowserDerivedCloudData>()
                .BrowserTestsJavaScript;
            Assert.IsTrue(tests.HasValue);
            Assert.AreEqual("fod.tests();", tests.Value.ToString());
            Assert.IsTrue(pipeline.ElementAvailableProperties
                [BrowserDerivedCloudEngine.ELEMENT_DATA_KEY]
                .ContainsKey("BrowserTestsJavaScript"));
        }

        /// <summary>
        /// The sealed result reaches no Element Data.
        /// </summary>
        [TestMethod]
        public void SealedResult_InNoElementData()
        {
            var pipeline = NewPipeline();
            CallFromPage(pipeline);
            _handler.Answer = () => Json(RedeemAnswer("\"High\""));

            using var data = Redeem(pipeline);

            foreach (var element in data.ElementDataAsDictionary().Values)
            {
                foreach (var value in ((IElementData)element)
                    .AsDictionary().Values)
                {
                    var text = value is IAspectPropertyValue apv && apv.HasValue
                        ? apv.Value?.ToString()
                        : value?.ToString();
                    Assert.IsFalse(text?.Contains(SealedResult) ?? false);
                }
            }
        }
    }
}
