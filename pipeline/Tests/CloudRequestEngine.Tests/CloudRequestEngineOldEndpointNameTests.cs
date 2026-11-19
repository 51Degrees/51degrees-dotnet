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
using System;
using System.Reflection;

namespace FiftyOne.Pipeline.CloudRequestEngine.Tests
{
    /// <summary>
    /// Tests that the old constant for the cloud endpoint's environment
    /// variable keeps its name and value, and points at the new one. The
    /// variable it names is no longer read.
    /// </summary>
    [TestClass]
    public class CloudRequestEngineOldEndpointNameTests
    {
        private const string NewName = "_51DEGREES_CLOUD_ENDPOINT";
        private const string OldName = "FOD_CLOUD_API_URL";

        [TestMethod]
        public void OldConstant_HoldsTheOldName()
        {
#pragma warning disable CS0618 // The deprecated constant is the subject.
            Assert.AreEqual(OldName, Constants.FOD_CLOUD_API_URL);
#pragma warning restore CS0618
            Assert.AreEqual(NewName, Constants.CLOUD_ENDPOINT_ENV_VAR);
        }

        [TestMethod]
        public void OldConstant_IsObsoleteAndPointsAtTheNewOne()
        {
            var obsolete = typeof(Constants)
                .GetField("FOD_CLOUD_API_URL")
                .GetCustomAttribute<ObsoleteAttribute>();

            Assert.IsNotNull(obsolete);
            Assert.IsFalse(
                obsolete.IsError,
                "Code that uses the old constant must still compile.");
            StringAssert.Contains(
                obsolete.Message, nameof(Constants.CLOUD_ENDPOINT_ENV_VAR));
        }
    }
}