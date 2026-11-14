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

using FiftyOne.DeviceDetection.Examples;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace FiftyOne.DeviceDetection.Example.Tests.OnPremise
{
    /// <summary>
    /// Runs each console example as someone who has just cloned the
    /// repository does, through its Main method with no arguments and none of
    /// the settings the other tests are given. So the examples use the free
    /// Lite data file that the data repository links, and these tests check
    /// that each one runs on it and says what the Enterprise data file adds.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class TestExamplesWithoutSettings
    {
        /// <summary>
        /// The environment variables that name a data file, an evidence file
        /// or a licence key, none of which a fresh clone has.
        /// </summary>
        private static readonly string[] Settings =
            Constants.DATA_FILE_ENV_VARS.Concat(new[]
            {
                Constants.EVIDENCE_FILE_ENV_VAR,
                Constants.LICENSE_KEY_ENV_VAR,
                "_51DEGREES_LICENSE_KEY"
            }).ToArray();

        /// <summary>
        /// The start of the link each example gives to ask for a copy of the
        /// Enterprise data file.
        /// </summary>
        private const string ContactUs = "https://51degrees.com/contact-us?";

        public TestContext TestContext { get; set; }

        /// <summary>
        /// The heading of the section every example ends with.
        /// </summary>
        private const string FindOutMore = "Find out more";

        /// <summary>
        /// Runs the example's Main with no arguments and the settings removed,
        /// checks that it ends by saying where to find out more, and returns
        /// what it wrote to the console before that section. The section has
        /// a link to the contact page of its own, so the tests look for the
        /// example's link in what came before it.
        /// </summary>
        private string RunWithoutSettings(Action<string[]> main)
        {
            var saved = Settings.ToDictionary(
                name => name,
                Environment.GetEnvironmentVariable);
            var console = Console.Out;
            var output = new StringWriter();
            try
            {
                foreach (var name in Settings)
                {
                    Environment.SetEnvironmentVariable(name, null);
                }
                Console.SetOut(output);
                main(Array.Empty<string>());
            }
            finally
            {
                Console.SetOut(console);
                foreach (var entry in saved)
                {
                    Environment.SetEnvironmentVariable(entry.Key, entry.Value);
                }
                TestContext.WriteLine(output.ToString());
            }
            var written = output.ToString();
            var section = written.LastIndexOf(
                FindOutMore, StringComparison.Ordinal);
            Assert.IsTrue(section >= 0,
                "The example should end by saying where to find out more.");
            return written.Substring(0, section);
        }

        [TestMethod]
        public void GettingStarted_WithoutSettings()
        {
            var output = RunWithoutSettings(
                Examples.OnPremise.GettingStartedConsole.Program.Main);
            StringAssert.Contains(output, "Mobile Device:",
                "The Lite data file has IsMobile.");
            StringAssert.Contains(output,
                $"Hardware Vendor: {ExampleUtils.NEEDS_ENTERPRISE}",
                "HardwareVendor should be shown as needing the Enterprise " +
                "data file.");
            StringAssert.Contains(output, ContactUs,
                "The example should link to the contact page.");
        }

        [TestMethod]
        public void UpdateDataFile_WithoutSettings()
        {
            var output = RunWithoutSettings(
                Examples.OnPremise.UpdateDataFile.Program.Main);
            StringAssert.Contains(output,
                "Updating the data file downloads the Enterprise data file",
                "The example should say what an update needs.");
            StringAssert.Contains(output,
                "'_51DEGREES_LICENSE_KEY'",
                "The example should name the variable that holds the key.");
            StringAssert.Contains(output, ContactUs,
                "The example should link to the contact page.");
        }

        [TestMethod]
        public void MatchMetrics_WithoutSettings()
        {
            var output = RunWithoutSettings(
                Examples.OnPremise.MatchMetrics.Program.Main);
            StringAssert.Contains(output, "This example used the free Lite data file",
                "The example should say what the Enterprise data file adds.");
            StringAssert.Contains(output, ContactUs,
                "The example should link to the contact page.");
        }

        [TestMethod]
        public void Metadata_WithoutSettings()
        {
            var output = RunWithoutSettings(
                Examples.OnPremise.Metadata.Program.Main);
            StringAssert.Contains(output, "This example used the free Lite data file",
                "The example should say what the Enterprise data file adds.");
            StringAssert.Contains(output, ContactUs,
                "The example should link to the contact page.");
        }

        [TestMethod]
        public void OfflineProcessing_WithoutSettings()
        {
            var output = RunWithoutSettings(
                Examples.OnPremise.OfflineProcessing.Program.Main);
            StringAssert.Contains(output,
                "Some of these properties need the Enterprise data file",
                "The example should say that the hardware properties need the " +
                "Enterprise data file.");
            StringAssert.Contains(output, ContactUs,
                "The example should link to the contact page.");
        }
    }
}
