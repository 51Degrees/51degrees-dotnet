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

using FiftyOne.Pipeline.Web.Services;
using FiftyOne.Pipeline.Web.Shared;
using FiftyOne.Pipeline.Web.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace FiftyOne.Pipeline.Web.Tests
{
    /// <summary>
    /// The web integration intercepts the two paths in the options, which
    /// default to the paths the specification gives.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/rc/pipeline-specification/features/web-integration.md#intercepted-urls">Specification</see>
    /// </summary>
    [TestClass]
    public class FiftyOneJSServiceTests
    {
        private Mock<IClientsidePropertyService> _clientside;

        [TestInitialize]
        public void Init()
        {
            _clientside = new Mock<IClientsidePropertyService>();
        }

        private FiftyOneJSService CreateService(
            PipelineWebIntegrationOptions options) =>
            new FiftyOneJSService(
                _clientside.Object,
                Options.Create(options),
                new Mock<IFlowDataProvider>().Object);

        private static HttpContext ContextFor(string path)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = path;
            return context;
        }

        /// <summary>
        /// The default options intercept the default JavaScript path.
        /// </summary>
        [TestMethod]
        [DataRow("/51Degrees.core.js")]
        [DataRow("/51degrees.CORE.js")]
        [DataRow("/site/51Degrees.core.js")]
        public void ServeJS_DefaultPath_Intercepted(string path)
        {
            var service = CreateService(new PipelineWebIntegrationOptions());

            Assert.IsTrue(service.ServeJS(ContextFor(path)));
            _clientside.Verify(c => c.ServeJavascript(
                It.IsAny<FiftyOne.Pipeline.Web.Shared.Adapters.IContextAdapter>(),
                It.IsAny<FiftyOne.Pipeline.Core.Data.IFlowData>()),
                Times.Once());
        }

        /// <summary>
        /// The default options intercept the default JSON path.
        /// </summary>
        [TestMethod]
        [DataRow("/51dpipeline/json")]
        [DataRow("/site/51dpipeline/json")]
        public void ServeJson_DefaultPath_Intercepted(string path)
        {
            var service = CreateService(new PipelineWebIntegrationOptions());

            Assert.IsTrue(service.ServeJson(ContextFor(path)));
            _clientside.Verify(c => c.ServeJson(
                It.IsAny<FiftyOne.Pipeline.Web.Shared.Adapters.IContextAdapter>(),
                It.IsAny<FiftyOne.Pipeline.Core.Data.IFlowData>()),
                Times.Once());
        }

        /// <summary>
        /// A configured JavaScript path is intercepted and the default path
        /// is left for the application.
        /// </summary>
        [TestMethod]
        public void ServeJS_ConfiguredPath_InterceptedInsteadOfDefault()
        {
            var service = CreateService(new PipelineWebIntegrationOptions()
            {
                JavaScriptPath = "/scripts/device.js"
            });

            Assert.IsTrue(service.ServeJS(ContextFor("/scripts/device.js")));
            Assert.IsFalse(service.ServeJS(ContextFor("/51Degrees.core.js")));
        }

        /// <summary>
        /// A configured JSON path is intercepted and the default path is
        /// left for the application.
        /// </summary>
        [TestMethod]
        public void ServeJson_ConfiguredPath_InterceptedInsteadOfDefault()
        {
            var service = CreateService(new PipelineWebIntegrationOptions()
            {
                JsonPath = "/api/device"
            });

            Assert.IsTrue(service.ServeJson(ContextFor("/api/device")));
            Assert.IsFalse(service.ServeJson(ContextFor("/51dpipeline/json")));
        }

        /// <summary>
        /// A request for neither path is not intercepted.
        /// </summary>
        [TestMethod]
        public void Serve_OtherPath_NotIntercepted()
        {
            var service = CreateService(new PipelineWebIntegrationOptions());

            Assert.IsFalse(service.ServeJS(ContextFor("/index.html")));
            Assert.IsFalse(service.ServeJson(ContextFor("/index.html")));
        }
    }
}
