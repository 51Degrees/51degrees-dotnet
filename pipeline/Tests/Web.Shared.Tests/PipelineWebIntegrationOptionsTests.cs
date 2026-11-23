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

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FiftyOne.Pipeline.Web.Shared.Tests
{
    /// <summary>
    /// The defaults of <see cref="PipelineWebIntegrationOptions"/> keep the
    /// behaviour the web integration had before each element and path
    /// became an option.
    /// </summary>
    [TestClass]
    public class PipelineWebIntegrationOptionsTests
    {
        [TestMethod]
        public void Defaults_EveryElementOnAndSpecificationPaths()
        {
            var options = new PipelineWebIntegrationOptions();

            Assert.IsTrue(options.ClientSideEvidenceEnabled);
            Assert.IsTrue(options.SequenceElementEnabled);
            Assert.IsTrue(options.SetHeadersElementEnabled);
            Assert.IsTrue(options.UseSetHeaderProperties);
            Assert.AreEqual("/51Degrees.core.js", options.JavaScriptPath);
            Assert.AreEqual("/51dpipeline/json", options.JsonPath);
        }

        [TestMethod]
        public void BlankPaths_GiveDefaults()
        {
            var options = new PipelineWebIntegrationOptions()
            {
                JavaScriptPath = " ",
                JsonPath = null
            };

            Assert.AreEqual("/51Degrees.core.js", options.JavaScriptPath);
            Assert.AreEqual("/51dpipeline/json", options.JsonPath);
        }
    }
}
