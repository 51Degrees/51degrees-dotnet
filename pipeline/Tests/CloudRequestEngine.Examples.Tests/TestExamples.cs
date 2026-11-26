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
using Ex = Examples;

namespace FiftyOne.Pipeline.CloudRequestEngine.Examples.Tests
{
    /// <summary>
    /// This test class ensures that all examples execute successfully.
    /// </summary>
    /// <remarks>
    /// Note that these test do not generally ensure the correctness 
    /// of the example, only that the example will run without 
    /// crashing or throwing any unhandled exceptions. The test reads the
    /// Resource Key from _51DEGREES_RESOURCE_KEY_BESPOKE, gives it to the
    /// example in _51DEGREES_RESOURCE_KEY, and is skipped when it is not
    /// set. It changes the environment of the whole process, so it runs
    /// apart from the other tests.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public class TestExamples
    {
        /// <summary>
        /// The environment variable the test reads the key from.
        /// </summary>
        private const string KeyVariable = "_51DEGREES_RESOURCE_KEY_BESPOKE";

        /// <summary>
        /// The environment variable the example reads its key from.
        /// </summary>
        private const string ExampleKeyVariable = "_51DEGREES_RESOURCE_KEY";

        /// <summary>
        /// Test that the cloud engine example runs successfully against the
        /// live cloud service.
        /// </summary>
        [TestMethod]
        public void TestCloudEngineExample()
        {
            var key = Environment.GetEnvironmentVariable(KeyVariable);
            if (string.IsNullOrWhiteSpace(key))
            {
                Assert.Inconclusive(
                    "This test runs the cloud engine example against the " +
                    "live cloud service, so it needs a Resource Key, and " +
                    $"{KeyVariable} is not set.");
            }
            var previous = Environment.GetEnvironmentVariable(
                ExampleKeyVariable);
            Environment.SetEnvironmentVariable(ExampleKeyVariable, key);
            try
            {
                var example = new Ex.CloudEngine.Program();
                example.RunExample();
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    ExampleKeyVariable, previous);
            }
        }
    }
}
