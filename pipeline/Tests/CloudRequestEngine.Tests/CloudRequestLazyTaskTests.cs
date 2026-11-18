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
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging.Abstractions;
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
    /// The cloud response is a lazy property whose task is the HTTP
    /// request. The engine starts the request while it processes and
    /// returns without waiting. Every reader waits on the one task. See
    /// the lazy properties section of thread-safety.md and the processing
    /// section of cloud-request-engine.md in the specification.
    /// </summary>
    [TestClass]
    public class CloudRequestLazyTaskTests
    {
        /// <summary>
        /// How long a test lets Process take before it counts as having
        /// waited for the held response.
        /// </summary>
        private const int ProcessReturnMs = 2000;

        /// <summary>
        /// How long the held response waits for its gate at most.
        /// </summary>
        private const int GateMs = 10000;

        private const string Response = "{'device':{'value':'1'}}";

        private const string FailedResponse = "{'errors':['Refused']}";

        /// <summary>
        /// Answers the discovery requests at once, and holds the answer to
        /// each data request until the gate is set.
        /// </summary>
        private sealed class GatedHandler : HttpMessageHandler
        {
            private int _dataRequests;

            public ManualResetEventSlim Gate { get; } =
                new ManualResetEventSlim(true);

            public string Json { get; set; } = Response;

            public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

            public int DataRequests => Volatile.Read(ref _dataRequests);

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                var path = request.RequestUri.AbsolutePath.ToLowerInvariant();
                if (path.EndsWith("evidencekeys"))
                {
                    return Answer(HttpStatusCode.OK, "['query.User-Agent']");
                }
                if (path.EndsWith("accessibleproperties"))
                {
                    return Answer(HttpStatusCode.OK,
                        "{'Products': {" +
                        "'device': {'DataTier': 'tier','Properties': " +
                        "[{'Name': 'value','Type': 'String'," +
                        "'Category': 'Device'}]}," +
                        "'other': {'DataTier': 'tier','Properties': " +
                        "[{'Name': 'value','Type': 'String'," +
                        "'Category': 'Device'}]}}}");
                }
                Interlocked.Increment(ref _dataRequests);
                var json = Json;
                var status = Status;
                await Task.Run(() => Gate.Wait(GateMs));
                return Answer(status, json);
            }

            private static HttpResponseMessage Answer(
                HttpStatusCode status, string json) =>
                new HttpResponseMessage()
                {
                    StatusCode = status,
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
        /// Process returns while the cloud service has not answered, and
        /// reading the response then waits for the answer.
        /// </summary>
        [TestMethod]
        public void Process_ReturnsBeforeTheResponse()
        {
            using var handler = new GatedHandler();
            using var engine = Builder(handler).Build();
            using var pipeline = new PipelineBuilder(
                NullLoggerFactory.Instance)
                .AddFlowElement(engine).Build();
            using var data = pipeline.CreateFlowData();
            data.AddEvidence("query.User-Agent", "iPhone");
            handler.Gate.Reset();
            bool returned;
            try
            {
                returned = Task.Run(() => data.Process())
                    .Wait(ProcessReturnMs);
            }
            finally
            {
                handler.Gate.Set();
            }

            Assert.IsTrue(returned,
                "Process must return before the cloud service answers.");
            StringAssert.Contains(
                data.GetFromElement(engine).JsonResponse, "value");
            Assert.AreEqual(1, handler.DataRequests);
        }

        /// <summary>
        /// With a results cache, Process still returns before the cloud
        /// service answers. A second request with the same Evidence is
        /// served the first response from the cache once it has arrived.
        /// </summary>
        [TestMethod]
        public void Process_WithACache_ReturnsBeforeTheResponse()
        {
            using var handler = new GatedHandler();
            using var engine = Builder(handler)
                .SetCacheSize(10)
                .SetCacheHitOrMiss(true)
                .Build();
            using var pipeline = new PipelineBuilder(
                NullLoggerFactory.Instance)
                .AddFlowElement(engine).Build();
            bool returned;
            using (var first = pipeline.CreateFlowData())
            {
                first.AddEvidence("query.User-Agent", "iPhone");
                handler.Gate.Reset();
                try
                {
                    returned = Task.Run(() => first.Process())
                        .Wait(ProcessReturnMs);
                }
                finally
                {
                    handler.Gate.Set();
                }
                StringAssert.Contains(
                    first.GetFromElement(engine).JsonResponse, "value");
            }
            Assert.IsTrue(returned,
                "Process must return before the cloud service answers.");

            using (var second = pipeline.CreateFlowData())
            {
                second.AddEvidence("query.User-Agent", "iPhone");
                second.Process();
                Assert.IsTrue(second.GetFromElement(engine).CacheHit);
                StringAssert.Contains(
                    second.GetFromElement(engine).JsonResponse, "value");
            }
            Assert.AreEqual(1, handler.DataRequests);
        }

        /// <summary>
        /// A failed request met once processing has finished cannot be
        /// recorded in the Flow Data, so it reaches only the reader. Every
        /// reader gets the same failure, and the request is sent once.
        /// </summary>
        [TestMethod]
        public void FailedRequest_AfterProcessing_ReachesOnlyTheReader()
        {
            using var handler = new GatedHandler()
            {
                Json = FailedResponse,
                Status = HttpStatusCode.BadRequest,
            };
            using var engine = Builder(handler).Build();
            using var pipeline = new PipelineBuilder(
                NullLoggerFactory.Instance)
                .AddFlowElement(engine).Build();
            using var data = pipeline.CreateFlowData();
            data.AddEvidence("query.User-Agent", "iPhone");

            data.Process();

            Assert.IsEmpty(data.Errors ?? new List<IFlowError>());
            var cloudData = data.GetFromElement(engine);
            var first = Assert.ThrowsExactly<CloudRequestException>(
                () => cloudData.JsonResponse);
            var second = Assert.ThrowsExactly<CloudRequestException>(
                () => cloudData.JsonResponse);
            Assert.AreSame(first, second);
            StringAssert.Contains(first.Message, "Refused");
            Assert.AreEqual(1, handler.DataRequests);
        }

        /// <summary>
        /// A failed request met by readers while the Flow Data is still
        /// processed is recorded once, as an error of the Cloud Request
        /// Engine and not of a reader, so the Pipeline throws it as it
        /// would any error of the engine's processing.
        /// </summary>
        [TestMethod]
        public void FailedRequest_MetWhileProcessing_RecordedOnceAsEngineError()
        {
            using var handler = new GatedHandler()
            {
                Json = FailedResponse,
                Status = HttpStatusCode.BadRequest,
            };
            using var engine = Builder(handler).Build();
            using var device = new CloudResponseReader("device");
            using var other = new CloudResponseReader("other");
            using var pipeline = new PipelineBuilder(
                NullLoggerFactory.Instance)
                .AddFlowElement(engine)
                .AddFlowElement(device)
                .AddFlowElement(other)
                .Build();
            using var data = pipeline.CreateFlowData();
            data.AddEvidence("query.User-Agent", "iPhone");

            var thrown = Assert.ThrowsExactly<AggregateException>(
                () => data.Process());

            var engineErrors = data.Errors
                .Where(e => e.FlowElement == engine)
                .ToList();
            Assert.HasCount(1, engineErrors);
            Assert.IsInstanceOfType<CloudRequestException>(
                engineErrors[0].ExceptionData);
            Assert.IsTrue(engineErrors[0].ShouldThrow);
            Assert.HasCount(1, thrown.InnerExceptions);
            Assert.AreSame(
                engineErrors[0].ExceptionData, thrown.InnerExceptions[0]);
            Assert.AreEqual(1, handler.DataRequests);
        }
    }
}
