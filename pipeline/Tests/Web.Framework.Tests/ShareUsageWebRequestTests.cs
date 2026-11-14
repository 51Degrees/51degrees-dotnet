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

using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;

namespace FiftyOne.Pipeline.Web.Framework.Tests
{
    /// <summary>
    /// The .NET Framework web integration adds the web request to the
    /// evidence for the usage sharing element, which accepts no evidence
    /// itself, so a header no element reads still reaches the usage
    /// sharing message. The request is read by System.Web from a worker
    /// request, which is how IIS hands one over, so the integration reads
    /// its headers, cookies and address as it reads a real request's.
    /// </summary>
    [TestClass]
    public class ShareUsageWebRequestTests
    {
        /// <summary>
        /// The key the web request is added under, written out rather than
        /// read from the package, so the test checks the key the
        /// specification gives.
        /// </summary>
        private const string HttpRequestKey = "server.http-request";

        /// <summary>
        /// How long the usage sharing message is waited for.
        /// </summary>
        private const int SendWaitSeconds = 30;

        /// <summary>
        /// The address the worker request gives as the client's.
        /// </summary>
        private const string ClientIp = "1.2.3.4";

        /// <summary>
        /// Keeps the XML of every usage sharing message sent.
        /// </summary>
        private sealed class Capture : HttpMessageHandler
        {
            public ConcurrentQueue<string> Xml { get; } =
                new ConcurrentQueue<string>();

            public SemaphoreSlim Sent { get; } = new SemaphoreSlim(0);

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                var bytes = await request.Content.ReadAsByteArrayAsync();
                using (var gzip = new GZipStream(
                    new MemoryStream(bytes), CompressionMode.Decompress))
                using (var reader = new StreamReader(gzip))
                {
                    Xml.Enqueue(await reader.ReadToEndAsync());
                }
                Sent.Release();
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        }

        /// <summary>
        /// A request as IIS hands it to System.Web, with the headers and
        /// the query string given and <see cref="ClientIp"/> as the
        /// client's address.
        /// </summary>
        private sealed class WorkerRequest : SimpleWorkerRequest
        {
            private readonly Dictionary<int, string> _known;
            private readonly string[][] _unknown;

            public WorkerRequest(
                string query,
                Dictionary<int, string> known,
                string[][] unknown)
                : base(
                    "/",
                    AppDomain.CurrentDomain.BaseDirectory,
                    "page",
                    query,
                    TextWriter.Null)
            {
                _known = known;
                _unknown = unknown;
            }

            public override string GetKnownRequestHeader(int index) =>
                _known.TryGetValue(index, out var value) ? value : null;

            public override string[][] GetUnknownRequestHeaders() =>
                _unknown;

            public override string GetRemoteAddress() => ClientIp;
        }

        private readonly ILoggerFactory _loggerFactory =
            NullLoggerFactory.Instance;
        private Capture _capture;
        private HttpClient _http;
        private IPipeline _pipeline;

        [TestInitialize]
        public void Init()
        {
            _capture = new Capture();
            _http = new HttpClient(_capture);
            _pipeline = new PipelineBuilder(_loggerFactory)
                .AddFlowElement(new SequenceElementBuilder(_loggerFactory)
                    .Build())
                .AddFlowElement(new ShareUsageBuilder(_loggerFactory, _http)
                    .SetSharePercentage(1)
                    .SetMinimumEntriesPerMessage(1)
                    .SetBlockedHttpHeader("x-blocked")
                    .Build())
                .Build();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _pipeline.Dispose();
            _http.Dispose();
        }

        /// <summary>
        /// A header no element accepts is not evidence, but the web
        /// request is, and usage sharing still shares the header from it,
        /// leaving out the cookie header, a blocked header and a cookie
        /// without the 51Degrees prefix.
        /// </summary>
        [TestMethod]
        public void WebRequest_UsageSharingSharesAHeaderNoElementAccepts()
        {
            var worker = new WorkerRequest(
                "session-id=abc&sequence=1",
                new Dictionary<int, string>
                {
                    {
                        HttpWorkerRequest.HeaderUserAgent,
                        "user-agent-value"
                    },
                    {
                        HttpWorkerRequest.HeaderCookie,
                        "51D_ScreenPixelsWidth=1080; other=other-value"
                    },
                },
                new[]
                {
                    new[] { "X-Test", "test-value" },
                    new[] { "X-Blocked", "blocked-value" },
                });
            var request = new HttpContext(worker).Request;
            Assert.AreEqual("test-value", request.Headers["X-Test"],
                "System.Web must read the header from the worker request.");

            using (var data = _pipeline.CreateFlowData())
            {
                WebPipeline.AddEvidenceFromRequest(data, request);
                data.Process();

                var evidence = data.GetEvidence().AsDictionary();
                Assert.IsFalse(evidence.ContainsKey("header.X-Test"),
                    "No element accepts the header, so it is not evidence.");
                Assert.IsTrue(
                    evidence.TryGetValue(HttpRequestKey, out var view),
                    "The web request must be in the evidence.");
                Assert.IsInstanceOfType(
                    view, typeof(IReadOnlyDictionary<string, object>));
            }

            Assert.IsTrue(
                _capture.Sent.Wait(TimeSpan.FromSeconds(SendWaitSeconds)),
                "The usage sharing message was not sent.");
            var xml = string.Concat(_capture.Xml);
            StringAssert.Contains(xml, "test-value");
            StringAssert.Contains(xml, "user-agent-value");
            StringAssert.Contains(xml, "Name=\"51D_ScreenPixelsWidth\"");
            StringAssert.Contains(xml, "<ClientIP>" + ClientIp + "</ClientIP>");
            Assert.IsFalse(xml.Contains("blocked-value"),
                "A blocked header must not be shared.");
            Assert.IsFalse(xml.Contains("other-value"),
                "The cookie header, and a cookie without the 51Degrees " +
                "prefix, must not be shared.");
            Assert.IsFalse(
                xml.IndexOf("http-request", StringComparison.OrdinalIgnoreCase)
                    >= 0,
                "The web request itself must never be shared.");
        }
    }
}
