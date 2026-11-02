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
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.CloudRequestEngine.Tests
{
    /// <summary>
    /// Tests for the settings a fluent cloud pipeline builder forwards to
    /// the cloud request engine it creates.
    /// </summary>
    [TestClass]
    public class CloudPipelineBuilderBaseTests
    {
        /// <summary>
        /// The smallest builder the base class allows.
        /// </summary>
        private class TestCloudPipelineBuilder :
            CloudPipelineBuilderBase<TestCloudPipelineBuilder>
        {
            public TestCloudPipelineBuilder(
                ILoggerFactory loggerFactory,
                HttpClient httpClient)
                : base(loggerFactory, httpClient)
            {
            }
        }

        private HttpClient _httpClient;

        [TestInitialize]
        public void Init()
        {
            var handler = new Mock<HttpMessageHandler>();
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.RequestUri.Segments.Last() == Constants.PROPERTIES_FILENAME),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(@"{""Products"":{""device"":{""DataTier"":""CloudV4Free"",""Properties"":[{""Name"":""IsMobile"",""Type"":""Boolean"",""Category"":""Device"",""DelayExecution"":false}]}}}"),
                });
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.RequestUri.Segments.Last() == Constants.EVIDENCE_KEYS_FILENAME),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(@"[""fiftyone.resource-key""]"),
                });
            _httpClient = new HttpClient(handler.Object);
            Environment.SetEnvironmentVariable(Constants.FOD_CLOUD_API_URL, "");
        }

        /// <summary>
        /// The timeout set on the pipeline builder reaches the cloud
        /// request engine, which applies it to the HTTP client.
        /// </summary>
        [TestMethod]
        public void CloudPipelineBuilder_TimeoutForwardedToEngine()
        {
            using (new TestCloudPipelineBuilder(new LoggerFactory(), _httpClient)
                .SetResourceKey("resource_key")
                .SetCloudRequestTimeOutSeconds(10)
                .Build())
            {
                Assert.AreEqual(TimeSpan.FromSeconds(10), _httpClient.Timeout);
            }
        }

        /// <summary>
        /// Without the setter the engine keeps its own default, so existing
        /// callers see no change.
        /// </summary>
        [TestMethod]
        public void CloudPipelineBuilder_TimeoutDefaultUnchanged()
        {
            using (new TestCloudPipelineBuilder(new LoggerFactory(), _httpClient)
                .SetResourceKey("resource_key")
                .Build())
            {
                Assert.AreEqual(
                    TimeSpan.FromSeconds(Constants.CLOUD_REQUEST_TIMEOUT_DEFAULT_SECONDS),
                    _httpClient.Timeout);
            }
        }
    }
}
