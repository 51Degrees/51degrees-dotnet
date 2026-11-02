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

namespace FiftyOne.GeoLocation.Example.Tests
{
    /// <summary>
    /// This test class ensures that all examples execute successfully.
    /// </summary>
    /// <remarks>
    /// Note that these test do not generally ensure the correctness 
    /// of the example, only that the example will run without 
    /// crashing or throwing any unhandled exceptions.
    /// </remarks>
    [TestClass]
    public class TestAllExamples
    {
        private string ResourceKey;

        /// <summary>
        /// Init method - specify Resource Key to run examples here or 
        /// set a Resource Key in an environment variable called 'ResourceKey'.
        /// </summary>
        [TestInitialize]
        public void Init()
        {
            var resourceKey = Environment.GetEnvironmentVariable("SUPER_RESOURCE_KEY");
            ResourceKey = string.IsNullOrWhiteSpace(resourceKey) == false ? 
                resourceKey : "!!YOUR_RESOURCE_KEY!!";

            if (string.IsNullOrWhiteSpace(ResourceKey) == true ||
                ResourceKey.StartsWith("!!") == true)
            {
                Assert.Fail("ResourceKey must be specified in the Init method" +
                    " or as an Environment variable");
            }
        }

        /// <summary>
        /// Test that the GettingStarted example runs successfully.
        /// </summary>
        [TestMethod]
        public void GettingStarted()
        {
            var example = new GettingStarted.Program.Example();
            example.Run(ResourceKey);
        }

        /// <summary>
        /// Test the the CombiningServices example runs successfully.
        /// </summary>
        [TestMethod]
        public void CombiningServices()
        {
            var example = new CombiningServices.Program.Example();
            example.Run(ResourceKey);
        }
    }
}
