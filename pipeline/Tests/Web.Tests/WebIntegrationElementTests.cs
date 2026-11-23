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

using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.Engines.TestHelpers;
using FiftyOne.Pipeline.JavaScriptBuilder.FlowElement;
using FiftyOne.Pipeline.JsonBuilder.FlowElement;
using FiftyOne.Pipeline.Web.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FiftyOne.Pipeline.Web.Tests
{
    /// <summary>
    /// The web integration lets a user stop each element it adds, and set
    /// the two paths it intercepts.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/rc/pipeline-specification/features/web-integration.md#pipeline-configuration">Specification</see>
    /// </summary>
    [TestClass]
    public class WebIntegrationElementTests
    {
        private static ServiceProvider CreateProvider(
            Dictionary<string, string> settings)
        {
            var config = new Dictionary<string, string>
            {
                ["PipelineOptions:BuildParameters:ShareUsage"] = "False",
                ["PipelineOptions:Elements:0:BuilderName"] = "EmptyEngine"
            };
            foreach (var setting in settings)
            {
                config[setting.Key] = setting.Value;
            }
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(config)
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<EmptyEngineBuilder>();
            services.AddFiftyOne(configuration);
            return services.BuildServiceProvider();
        }

        private static bool Has(IPipeline pipeline, string elementDataKey) =>
            pipeline.FlowElements.Any(e => e.ElementDataKey == elementDataKey);

        /// <summary>
        /// With no options set, every element is added.
        /// </summary>
        [TestMethod]
        public void Defaults_AddEveryElement()
        {
            using (var provider = CreateProvider(new Dictionary<string, string>()))
            {
                var pipeline = provider.GetRequiredService<IPipeline>();

                Assert.IsTrue(Has(pipeline, SequenceElement.DEFAULT_ELEMENT_DATA_KEY));
                Assert.IsTrue(Has(pipeline, JsonBuilderElement.DEFAULT_ELEMENT_DATA_KEY));
                Assert.IsTrue(Has(pipeline, JavaScriptBuilderElement.DEFAULT_ELEMENT_DATA_KEY));
                Assert.IsTrue(Has(pipeline, SetHeadersElement.DEFAULT_ELEMENT_DATA_KEY));
                Assert.AreEqual(
                    SequenceElement.DEFAULT_ELEMENT_DATA_KEY,
                    pipeline.FlowElements.First().ElementDataKey,
                    "The Sequence Element should be first.");
            }
        }

        /// <summary>
        /// Turning off the Set Headers Element leaves it out and keeps the
        /// others.
        /// </summary>
        [TestMethod]
        public void SetHeadersElementDisabled_NotAdded()
        {
            using (var provider = CreateProvider(new Dictionary<string, string>
            {
                ["PipelineWebIntegrationOptions:SetHeadersElementEnabled"] = "False"
            }))
            {
                var pipeline = provider.GetRequiredService<IPipeline>();

                Assert.IsFalse(Has(pipeline, SetHeadersElement.DEFAULT_ELEMENT_DATA_KEY));
                Assert.IsTrue(Has(pipeline, SequenceElement.DEFAULT_ELEMENT_DATA_KEY));
                Assert.IsTrue(Has(pipeline, JsonBuilderElement.DEFAULT_ELEMENT_DATA_KEY));
                Assert.IsTrue(Has(pipeline, JavaScriptBuilderElement.DEFAULT_ELEMENT_DATA_KEY));
            }
        }

        /// <summary>
        /// Turning off the Sequence Element leaves it out, even though the
        /// default pipeline builder would add it, once client-side
        /// evidence is off as well.
        /// </summary>
        [TestMethod]
        public void SequenceElementDisabled_NotAdded()
        {
            using (var provider = CreateProvider(new Dictionary<string, string>
            {
                ["PipelineWebIntegrationOptions:SequenceElementEnabled"] = "False",
                ["PipelineWebIntegrationOptions:ClientSideEvidenceEnabled"] = "False"
            }))
            {
                var pipeline = provider.GetRequiredService<IPipeline>();

                Assert.IsFalse(Has(pipeline, SequenceElement.DEFAULT_ELEMENT_DATA_KEY));
                Assert.IsTrue(Has(pipeline, SetHeadersElement.DEFAULT_ELEMENT_DATA_KEY));
            }
        }

        /// <summary>
        /// The client-side switch still removes only the JSON and
        /// JavaScript builders.
        /// </summary>
        [TestMethod]
        public void ClientSideEvidenceDisabled_RemovesOnlyBuilders()
        {
            using (var provider = CreateProvider(new Dictionary<string, string>
            {
                ["PipelineWebIntegrationOptions:ClientSideEvidenceEnabled"] = "False"
            }))
            {
                var pipeline = provider.GetRequiredService<IPipeline>();

                Assert.IsFalse(Has(pipeline, JsonBuilderElement.DEFAULT_ELEMENT_DATA_KEY));
                Assert.IsFalse(Has(pipeline, JavaScriptBuilderElement.DEFAULT_ELEMENT_DATA_KEY));
                Assert.IsTrue(Has(pipeline, SequenceElement.DEFAULT_ELEMENT_DATA_KEY));
                Assert.IsTrue(Has(pipeline, SetHeadersElement.DEFAULT_ELEMENT_DATA_KEY));
            }
        }

        /// <summary>
        /// The JSON Builder Element fails without a sequence number, so
        /// leaving out the Sequence Element while client-side evidence is
        /// on is refused when the Pipeline is built.
        /// </summary>
        [TestMethod]
        public void SequenceElementDisabled_ClientSideEnabled_Refused()
        {
            using (var provider = CreateProvider(new Dictionary<string, string>
            {
                ["PipelineWebIntegrationOptions:SequenceElementEnabled"] = "False"
            }))
            {
                Exception caught = null;
                try
                {
                    provider.GetRequiredService<IPipeline>();
                }
                catch (Exception ex)
                {
                    caught = ex;
                }

                Assert.IsNotNull(caught, "The Pipeline should not build.");
                Assert.IsInstanceOfType<PipelineConfigurationException>(caught);
            }
        }

        /// <summary>
        /// The services receive the options from the configuration,
        /// including the two paths, which default to the specification's.
        /// </summary>
        [TestMethod]
        public void Options_BoundFromConfiguration()
        {
            using (var provider = CreateProvider(new Dictionary<string, string>
            {
                ["PipelineWebIntegrationOptions:JavaScriptPath"] = "/scripts/device.js",
                ["PipelineWebIntegrationOptions:JsonPath"] = "/api/device"
            }))
            {
                var options = provider
                    .GetRequiredService<IOptions<PipelineWebIntegrationOptions>>()
                    .Value;

                Assert.AreEqual("/scripts/device.js", options.JavaScriptPath);
                Assert.AreEqual("/api/device", options.JsonPath);
            }
            using (var provider = CreateProvider(new Dictionary<string, string>()))
            {
                var options = provider
                    .GetRequiredService<IOptions<PipelineWebIntegrationOptions>>()
                    .Value;

                Assert.AreEqual("/51Degrees.core.js", options.JavaScriptPath);
                Assert.AreEqual("/51dpipeline/json", options.JsonPath);
            }
        }

        /// <summary>
        /// A JSON path that is not the default is given to the JavaScript
        /// Builder Element the web integration adds, so the script calls
        /// back to the path that is intercepted.
        /// </summary>
        [TestMethod]
        public void JsonPath_Configured_JavaScriptBuilderCallsBackToIt()
        {
            using (var provider = CreateProvider(new Dictionary<string, string>
            {
                ["PipelineWebIntegrationOptions:JsonPath"] = "/api/device"
            }))
            {
                var pipeline = provider.GetRequiredService<IPipeline>();
                var builder = pipeline.FlowElements
                    .OfType<JavaScriptBuilderElement>()
                    .Single();

                // The endpoint is protected, so read it by reflection.
                var endpoint = typeof(JavaScriptBuilderElement)
                    .GetProperty("Endpoint",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(builder);
                Assert.AreEqual("/api/device", endpoint);
            }
        }
    }
}
