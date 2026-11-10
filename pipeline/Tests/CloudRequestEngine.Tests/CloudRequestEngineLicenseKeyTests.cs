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

using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
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
    /// Tests for an engine that authenticates with a license key alone and
    /// names the properties it wants.
    /// </summary>
    [TestClass]
    public class CloudRequestEngineLicenseKeyTests
    {
        private const string LicenseKey = "LICENSE-KEY-0123456789";
        private const string ResourceKey = "resource_key";
        private const string Properties = "device.ismobile";
        private const string AnswerBody =
            "{\"device\":{\"ismobile\":true,\"iscrawler\":null}}";

        /// <summary>
        /// Records every request, with its body, and answers each endpoint
        /// with canned content.
        /// </summary>
        private sealed class RecordingHandler : HttpMessageHandler
        {
            public List<HttpRequestMessage> Requests { get; } =
                new List<HttpRequestMessage>();
            public List<string> Bodies { get; } = new List<string>();
            public string DataAnswer { get; set; } = AnswerBody;
            public HttpStatusCode DataStatus { get; set; } = HttpStatusCode.OK;

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
                    Requests.Add(request);
                    Bodies.Add(body);
                }
                var last = request.RequestUri.Segments.Last()
                    .ToLowerInvariant();
                string content;
                var status = HttpStatusCode.OK;
                if (last == Constants.EVIDENCE_KEYS_FILENAME)
                {
                    content = "[\"query.user-agent\"]";
                }
                else if (last == Constants.PROPERTIES_FILENAME)
                {
                    content = "{\"Products\":{\"device\":{\"DataTier\":" +
                        "\"CloudV4Free\",\"Properties\":[{\"Name\":" +
                        "\"IsMobile\",\"Type\":\"Boolean\",\"Category\":" +
                        "\"Device\",\"DelayExecution\":false}]}}}";
                }
                else
                {
                    content = DataAnswer;
                    status = DataStatus;
                }
                return new HttpResponseMessage(status)
                {
                    Content = new StringContent(content),
                };
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

        private readonly RecordingHandler _handler = new RecordingHandler();
        private readonly RecordingLoggerFactory _loggers =
            new RecordingLoggerFactory();

        [TestInitialize]
        public void Init()
        {
            Environment.SetEnvironmentVariable(
                Constants.FOD_CLOUD_API_URL, null);
        }

        private CloudRequestEngineBuilder NewBuilder()
        {
            return new CloudRequestEngineBuilder(
                _loggers, new HttpClient(_handler));
        }

        private string ProcessOnce(FlowElements.CloudRequestEngine engine)
        {
            using (var pipeline = new PipelineBuilder(_loggers)
                .AddFlowElement(engine).Build())
            {
                var data = pipeline.CreateFlowData();
                data.AddEvidence("query.User-Agent", "iPhone");
                data.Process();
                return data.GetFromElement(engine).JsonResponse;
            }
        }

        private static Dictionary<string, string> Fields(string body)
        {
            return body.Split('&')
                .Select(p => p.Split('=', 2))
                .ToDictionary(
                    p => WebUtility.UrlDecode(p[0]),
                    p => WebUtility.UrlDecode(p[1]));
        }

        private IEnumerable<string> DataBodies()
        {
            return _handler.Requests
                .Select((r, i) => new { r, b = _handler.Bodies[i] })
                .Where(x => x.r.Method == HttpMethod.Post &&
                    x.r.RequestUri.Segments.Last() == Constants.DATA_FILENAME)
                .Select(x => x.b);
        }

        /// <summary>
        /// The six rows of the table in the specification, where a blank
        /// value counts as absent.
        /// </summary>
        [TestMethod]
        [DataRow(ResourceKey, null, null, true, DisplayName = "Resource key")]
        [DataRow(ResourceKey, LicenseKey, null, true,
            DisplayName = "Resource and license keys")]
        [DataRow(null, LicenseKey, Properties, true,
            DisplayName = "License key and properties")]
        [DataRow(null, LicenseKey, null, false,
            DisplayName = "License key alone")]
        [DataRow(ResourceKey, null, Properties, false,
            DisplayName = "Resource key and properties")]
        [DataRow(ResourceKey, LicenseKey, Properties, false,
            DisplayName = "All three")]
        [DataRow(null, null, Properties, false,
            DisplayName = "Properties alone")]
        [DataRow(null, null, null, false, DisplayName = "Nothing")]
        [DataRow("  ", " \n", Properties, false,
            DisplayName = "Blank keys count as absent")]
        public void Build_Combinations(
            string resourceKey,
            string licenseKey,
            string properties,
            bool valid)
        {
            var builder = NewBuilder()
                .SetResourceKey(resourceKey)
                .SetLicenseKey(licenseKey);
            if (properties != null)
            {
                builder.SetProperties(new List<string> { properties });
            }

            if (valid)
            {
                Assert.IsNotNull(builder.Build());
            }
            else
            {
                Assert.ThrowsExactly<PipelineConfigurationException>(
                    () => builder.Build());
                Assert.IsEmpty(_handler.Requests,
                    "A refused build must send no request.");
            }
        }

        /// <summary>
        /// Each refusal explains why the combination cannot work, and no
        /// message holds a key.
        /// </summary>
        [TestMethod]
        public void Build_Refusals_ExplainWhy()
        {
            var none = Assert.ThrowsExactly<PipelineConfigurationException>(
                () => NewBuilder().Build());
            StringAssert.Contains(none.Message, "401");

            var noList = Assert.ThrowsExactly<PipelineConfigurationException>(
                () => NewBuilder().SetLicenseKey(LicenseKey).Build());
            StringAssert.Contains(noList.Message, "400");
            StringAssert.Contains(noList.Message, "requested properties");

            var ignored = Assert.ThrowsExactly<PipelineConfigurationException>(
                () => NewBuilder().SetResourceKey(ResourceKey)
                    .SetProperties(new List<string> { Properties }).Build());
            StringAssert.Contains(ignored.Message, "ignores the list");

            foreach (var e in new[] { none, noList, ignored })
            {
                Assert.DoesNotContain(LicenseKey, e.Message);
                Assert.DoesNotContain(ResourceKey, e.Message);
            }
        }

        /// <summary>
        /// A license key alone posts the license key and the property list
        /// and no resource key. Discovery posts the license key in the body
        /// of the accessible properties request, and no URL holds a key.
        /// </summary>
        [TestMethod]
        public void Process_LicenseKeyAlone_SendsLicenseAndValues()
        {
            var engine = NewBuilder()
                .SetLicenseKey(LicenseKey)
                .SetProperties(new List<string>
                    { "Device.IsMobile", " device.iscrawler ", "" })
                .Build();

            Assert.IsNotEmpty(engine.PublicProperties);
            Assert.IsNotNull(engine.EvidenceKeyFilter);
            var discovery = _handler.Requests
                .Select((r, i) => new { r, b = _handler.Bodies[i] })
                .Single(x => x.r.RequestUri.Segments.Last() ==
                    Constants.PROPERTIES_FILENAME);
            Assert.AreEqual(HttpMethod.Post, discovery.r.Method);
            Assert.AreEqual(string.Empty, discovery.r.RequestUri.Query);
            Assert.AreEqual(LicenseKey, Fields(discovery.b)["license"]);
            Assert.AreEqual(1, _handler.Requests.Count(r =>
                r.RequestUri.Segments.Last() ==
                    Constants.EVIDENCE_KEYS_FILENAME));

            ProcessOnce(engine);

            var fields = Fields(DataBodies().Single());
            Assert.AreEqual(LicenseKey, fields["license"]);
            Assert.AreEqual("device.ismobile,device.iscrawler",
                fields["values"]);
            Assert.IsFalse(fields.ContainsKey("resource"));
            Assert.AreEqual("iPhone", fields["User-Agent"]);
            Assert.AreEqual(Constants.DATA_ENDPOINT_DEFAULT,
                _handler.Requests.Single(r =>
                    r.Method == HttpMethod.Post &&
                    r.RequestUri.Segments.Last() ==
                        Constants.DATA_FILENAME)
                .RequestUri.ToString());
            Assert.IsFalse(_handler.Requests.Any(r =>
                r.RequestUri.ToString().Contains(LicenseKey)),
                "The license key must never appear in a URL.");
        }

        /// <summary>
        /// The named option adds to the same list as SetProperties.
        /// </summary>
        [TestMethod]
        public void Process_SetRequestedProperties_Sent()
        {
            var engine = NewBuilder()
                .SetLicenseKey(LicenseKey)
                .SetRequestedProperties(new[] { "Device.IsMobile" })
                .SetRequestedProperties(new[] { "device.iscrawler" })
                .Build();

            ProcessOnce(engine);

            Assert.AreEqual("device.ismobile,device.iscrawler",
                Fields(DataBodies().Single())["values"]);
        }

        /// <summary>
        /// A license key that arrives with a trailing newline is trimmed.
        /// </summary>
        [TestMethod]
        public void Process_LicenseKeyWithNewline_Trimmed()
        {
            var engine = NewBuilder()
                .SetLicenseKey(LicenseKey + "\n")
                .SetProperties(new List<string> { Properties })
                .Build();

            ProcessOnce(engine);

            Assert.AreEqual(LicenseKey, Fields(DataBodies().Single())["license"]);
        }

        /// <summary>
        /// A license key beside a resource key is sent with it.
        /// </summary>
        [TestMethod]
        public void Process_ResourceAndLicenseKeys_BothSent()
        {
            var engine = NewBuilder()
                .SetResourceKey(ResourceKey)
                .SetLicenseKey(LicenseKey)
                .Build();

            ProcessOnce(engine);

            var fields = Fields(DataBodies().Single());
            Assert.AreEqual(ResourceKey, fields["resource"]);
            Assert.AreEqual(LicenseKey, fields["license"]);
            Assert.IsFalse(fields.ContainsKey("values"));
        }

        /// <summary>
        /// A resource key is sent as it always was, with no property list,
        /// and its accessible properties are requested.
        /// </summary>
        [TestMethod]
        public void Process_ResourceKey_Unchanged()
        {
            var engine = NewBuilder().SetResourceKey(ResourceKey).Build();

            Assert.IsNotEmpty(engine.PublicProperties);
            ProcessOnce(engine);

            var fields = Fields(DataBodies().Single());
            Assert.AreEqual(ResourceKey, fields["resource"]);
            Assert.IsFalse(fields.ContainsKey("license"));
            Assert.IsFalse(fields.ContainsKey("values"));
            Assert.IsTrue(_handler.Requests.Any(r =>
                r.Method == HttpMethod.Get &&
                r.RequestUri.Query == "?resource=" + ResourceKey));
        }

        /// <summary>
        /// A property missing from the answer is reported once, ignoring
        /// letter case, and a property present with a null value counts as
        /// answered. The request still succeeds.
        /// </summary>
        [TestMethod]
        public void Process_PropertyDropped_ReportedOnce()
        {
            var engine = NewBuilder()
                .SetLicenseKey(LicenseKey)
                .SetProperties(new List<string>
                {
                    "Device.IsMobile", "device.iscrawler", "device.hardwarename",
                })
                .Build();

            Assert.AreEqual(AnswerBody, ProcessOnce(engine));
            ProcessOnce(engine);

            var warnings = _loggers.Logger.Entries
                .Where(e => e.Contains("did not return")).ToList();
            Assert.HasCount(1, warnings);
            StringAssert.Contains(warnings[0], "device.hardwarename");
            Assert.DoesNotContain("device.iscrawler", warnings[0]);
            Assert.DoesNotContain("device.ismobile", warnings[0]);
        }

        /// <summary>
        /// No log entry or exception message, from the build, a good
        /// request or a failed one, holds the license key.
        /// </summary>
        [TestMethod]
        public void Logs_NeverHoldLicenseKey()
        {
            var engine = NewBuilder()
                .SetLicenseKey(LicenseKey)
                .SetProperties(new List<string> { Properties, "device.other" })
                .Build();
            ProcessOnce(engine);

            _handler.DataStatus = HttpStatusCode.InternalServerError;
            _handler.DataAnswer = "failure";
            Exception failure = null;
            try
            {
                ProcessOnce(engine);
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            Assert.IsNotNull(failure, "The failed request should throw.");
            Assert.DoesNotContain(LicenseKey, failure.ToString());
            Assert.IsNotEmpty(_loggers.Logger.Entries);
            foreach (var entry in _loggers.Logger.Entries)
            {
                Assert.DoesNotContain(LicenseKey, entry);
            }
        }
    }
}
