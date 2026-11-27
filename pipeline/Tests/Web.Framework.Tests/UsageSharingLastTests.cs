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

using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.JavaScriptBuilder.FlowElement;
using FiftyOne.Pipeline.JsonBuilder.FlowElement;
using FiftyOne.Pipeline.Web.Framework.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace FiftyOne.Pipeline.Web.Framework.Tests
{
    /// <summary>
    /// The .NET Framework integration adds the elements it needs before a
    /// usage sharing element that the configuration names, so usage sharing
    /// is the last Flow Element of the Pipeline it builds.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/52e9b047743db365592f9bd1c737fc7ddb6a2de4/pipeline-specification/pipeline-elements/usage-sharing-element.md#position-in-the-pipeline">Specification</see>
    /// </summary>
    [TestClass]
    public class UsageSharingLastTests
    {
        /// <summary>
        /// Names usage sharing last. The integration adds the Sequence,
        /// JSON Builder, JavaScript Builder and Set Headers elements.
        /// </summary>
        private const string Configuration =
            "{\"PipelineOptions\":{\"Elements\":[" +
            "{\"BuilderName\":\"MathElement\"}," +
            "{\"BuilderName\":\"ShareUsageBuilder\"}]}}";

        private string _folder;

        private Func<string> _baseDirectory;

        [TestInitialize]
        public void Init()
        {
            // Builders named in a configuration are found by reflection
            // over the loaded assemblies.
            AppDomain.CurrentDomain.Load(
                typeof(FiftyOne.Pipeline.Math.MathElementBuilder)
                    .Assembly.GetName());
            AppDomain.CurrentDomain.Load(
                typeof(JavaScriptBuilderElement).Assembly.GetName());
            AppDomain.CurrentDomain.Load(
                typeof(JsonBuilderElement).Assembly.GetName());
            AppDomain.CurrentDomain.Load(
                typeof(ShareUsageBuilder).Assembly.GetName());

            _folder = Path.Combine(
                Path.GetTempPath(),
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
            File.WriteAllText(
                Path.Combine(
                    _folder,
                    Constants.ConfigFileNames.First() + "." +
                    Constants.JsonFileExtensions.First()),
                Configuration);

            // Read the configuration from the folder rather than the
            // hosted application's App_Data.
            _baseDirectory = Extensions.BaseDirectory;
            Extensions.BaseDirectory = () => _folder;
        }

        [TestCleanup]
        public void Cleanup()
        {
            Extensions.BaseDirectory = _baseDirectory;
            Directory.Delete(_folder, true);
        }

        /// <summary>
        /// The Pipeline the integration builds ends with the usage sharing
        /// element that the configuration names.
        /// </summary>
        [TestMethod]
        public void UsageSharingConfigured_PipelineEndsWithIt()
        {
            // A new instance rather than the shared one, which another test
            // may already have built from a different configuration.
            var webPipeline = (WebPipeline)Activator.CreateInstance(
                typeof(WebPipeline),
                nonPublic: true);

            using (var pipeline = webPipeline.Pipeline)
            {
                Assert.IsInstanceOfType(
                    pipeline.FlowElements.Last(),
                    typeof(ShareUsageElement),
                    "The usage sharing element should be the last Flow " +
                    "Element, but the elements are " +
                    string.Join(", ", pipeline.FlowElements.Select(
                        e => e.GetType().Name)) + ".");
                Assert.AreEqual(
                    1,
                    pipeline.FlowElements.Count(e => e is ShareUsageElement));
            }
        }
    }
}
