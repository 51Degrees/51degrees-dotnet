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
using System.Diagnostics;

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
        /// <summary>
        /// The environment variable the tests read their resource key from,
        /// which holds the key that carries every property. Each example is
        /// given the key as the argument its Run method takes.
        /// </summary>
        private const string KeyVariable = "_51DEGREES_RESOURCE_KEY_BESPOKE";

        private string ResourceKey;

        /// <summary>
        /// The seconds the tests let a request to the cloud service take.
        /// The examples keep the default of 2 seconds, which the live
        /// service sometimes takes longer than, so the tests allow more and
        /// fail only a service that does not answer. The build log has the
        /// time each run took.
        /// </summary>
        private const int CloudRequestTimeoutSeconds = 30;

        /// <summary>
        /// Reads the resource key from <see cref="KeyVariable"/>, skipping
        /// the test when it is not set.
        /// </summary>
        [TestInitialize]
        public void Init()
        {
            ResourceKey = Environment.GetEnvironmentVariable(KeyVariable);
            if (string.IsNullOrWhiteSpace(ResourceKey))
            {
                Assert.Inconclusive(
                    "These tests call the live 51Degrees cloud service, so " +
                    $"they need a resource key, and {KeyVariable} is not " +
                    "set.");
            }
        }

        /// <summary>
        /// Test that the GettingStarted example runs successfully.
        /// </summary>
        [TestMethod]
        public void GettingStarted()
        {
            var example = new GettingStarted.Program.Example();
            var run = Stopwatch.StartNew();
            example.Run(
                ResourceKey,
                cloudRequestTimeoutSeconds: CloudRequestTimeoutSeconds);
            Console.WriteLine(
                "Build log: GeoLocation GettingStarted example ran in " +
                $"{run.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Test the the CombiningServices example runs successfully.
        /// </summary>
        [TestMethod]
        public void CombiningServices()
        {
            var example = new CombiningServices.Program.Example();
            var run = Stopwatch.StartNew();
            example.Run(
                ResourceKey,
                cloudRequestTimeoutSeconds: CloudRequestTimeoutSeconds);
            Console.WriteLine(
                "Build log: GeoLocation CombiningServices example ran in " +
                $"{run.ElapsedMilliseconds} ms.");
        }
    }
}
