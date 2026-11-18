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
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.CloudRequestEngine.Tests
{
    /// <summary>
    /// The Cloud Request Engine's Element Data is reused through a pool
    /// once its Flow Data is disposed. An instance whose request is still
    /// running, or that a results cache holds, is never given to another
    /// request. See the reusing Element Data section of
    /// resource-cleanup.md in the specification.
    /// </summary>
    [TestClass]
    public class CloudRequestDataPoolTests
    {
        private const int GateMs = 10000;

        /// <summary>
        /// Answers the discovery requests at once, and holds the answer to
        /// each data request until the gate is set. The answer names the
        /// User-Agent sent, so each request's response is its own.
        /// </summary>
        private sealed class GatedHandler : HttpMessageHandler
        {
            public ManualResetEventSlim Gate { get; } =
                new ManualResetEventSlim(true);

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                var path = request.RequestUri.AbsolutePath.ToLowerInvariant();
                if (path.EndsWith("evidencekeys"))
                {
                    return Answer("['query.User-Agent']");
                }
                if (path.EndsWith("accessibleproperties"))
                {
                    return Answer(
                        "{'Products': {'device': {'DataTier': 'tier'," +
                        "'Properties': [{'Name': 'value','Type': 'String'," +
                        "'Category': 'Device'}]}}}");
                }
                var body = await request.Content.ReadAsStringAsync();
                var userAgent = body.Contains("first") ? "first" :
                    body.Contains("second") ? "second" : "other";
                await Task.Run(() => Gate.Wait(GateMs));
                return Answer($"{{'device':{{'value':'{userAgent}'}}}}");
            }

            private static HttpResponseMessage Answer(string json) =>
                new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(json),
                };

            protected override void Dispose(bool disposing)
            {
                Gate.Set();
                Gate.Dispose();
                base.Dispose(disposing);
            }
        }

        private static CloudRequestEngineBuilder Builder(
            GatedHandler handler) =>
            new CloudRequestEngineBuilder(
                NullLoggerFactory.Instance, new HttpClient(handler))
                .SetResourceKey("resource_key");

        /// <summary>
        /// Requests in turn reuse one instance, and each reads only its own
        /// response.
        /// </summary>
        [TestMethod]
        public void RequestsInTurn_ReuseOneInstance()
        {
            using var handler = new GatedHandler();
            using var engine = Builder(handler).Build();
            using var pipeline = new PipelineBuilder(
                NullLoggerFactory.Instance)
                .AddFlowElement(engine).Build();

            object first;
            using (var data = pipeline.CreateFlowData())
            {
                data.AddEvidence("query.User-Agent", "first");
                data.Process();
                first = data.GetFromElement(engine);
                StringAssert.Contains(
                    data.GetFromElement(engine).JsonResponse, "first");
            }
            using (var data = pipeline.CreateFlowData())
            {
                data.AddEvidence("query.User-Agent", "second");
                data.Process();
                Assert.AreSame(first, data.GetFromElement(engine),
                    "The instance must go back to the pool and be reused.");
                var json = data.GetFromElement(engine).JsonResponse;
                StringAssert.Contains(json, "second");
                Assert.IsFalse(json.Contains("first"));
            }
        }

        /// <summary>
        /// Disposing a Flow Data whose request is still running does not
        /// wait for the request, and the instance is not given to another
        /// request until the request has finished.
        /// </summary>
        [TestMethod]
        public void RequestStillRunning_InstanceNotGivenToAnotherRequest()
        {
            using var handler = new GatedHandler();
            using var engine = Builder(handler).Build();
            using var pipeline = new PipelineBuilder(
                NullLoggerFactory.Instance)
                .AddFlowElement(engine).Build();

            handler.Gate.Reset();
            try
            {
                object first;
                var timer = Stopwatch.StartNew();
                using (var data = pipeline.CreateFlowData())
                {
                    data.AddEvidence("query.User-Agent", "first");
                    data.Process();
                    first = data.GetFromElement(engine);
                }
                Assert.IsLessThan(GateMs / 2, timer.ElapsedMilliseconds,
                    "Disposing must not wait for the running request.");

                using (var data = pipeline.CreateFlowData())
                {
                    data.AddEvidence("query.User-Agent", "second");
                    data.Process();
                    Assert.AreNotSame(first, data.GetFromElement(engine),
                        "An instance whose request is running must not " +
                        "be given to another request.");
                }
            }
            finally
            {
                handler.Gate.Set();
            }
        }

        /// <summary>
        /// An instance the results cache holds is never given to another
        /// request, and a later request with the same Evidence is served
        /// it with its response intact.
        /// </summary>
        [TestMethod]
        public void CachedInstance_NeverGivenToAnotherRequest()
        {
            using var handler = new GatedHandler();
            using var engine = Builder(handler)
                .SetCacheSize(10)
                .SetCacheHitOrMiss(true)
                .Build();
            using var pipeline = new PipelineBuilder(
                NullLoggerFactory.Instance)
                .AddFlowElement(engine).Build();

            object cached;
            using (var data = pipeline.CreateFlowData())
            {
                data.AddEvidence("query.User-Agent", "first");
                data.Process();
                cached = data.GetFromElement(engine);
                StringAssert.Contains(
                    data.GetFromElement(engine).JsonResponse, "first");
            }
            for (int i = 0; i < 3; i++)
            {
                using (var data = pipeline.CreateFlowData())
                {
                    data.AddEvidence("query.User-Agent", $"other {i}");
                    data.Process();
                    Assert.AreNotSame(cached, data.GetFromElement(engine),
                        "A cached instance must never go back to a pool.");
                    StringAssert.Contains(
                        data.GetFromElement(engine).JsonResponse, "other");
                }
            }
            using (var data = pipeline.CreateFlowData())
            {
                data.AddEvidence("query.User-Agent", "first");
                data.Process();
                Assert.IsTrue(data.GetFromElement(engine).CacheHit);
                Assert.AreSame(cached, data.GetFromElement(engine));
                StringAssert.Contains(
                    data.GetFromElement(engine).JsonResponse, "first");
            }
        }
    }
}
