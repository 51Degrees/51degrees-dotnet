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

using FiftyOne.IpIntelligence.Examples;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace FiftyOne.IpIntelligence.Example.Tests.OnPremise;

/// <summary>
/// Runs each console example as someone who has just cloned the repository
/// does, through its Main method with no arguments and none of the settings
/// the other tests are given. So the examples use the free data files that
/// the data repository links, and these tests check that each one runs on
/// them and says what the Enterprise data file adds.
/// </summary>
[TestClass]
[TestCategory(TestCategories.Integration)]
[DoNotParallelize]
public class TestExamplesWithoutSettings
{
    /// <summary>
    /// The environment variables that name a data file, an evidence file or
    /// a licence key, none of which a fresh clone has.
    /// </summary>
    private static readonly string[] Settings =
        new[]
        {
            Constants.IPI_PATH_ENV_VAR,
            Constants.EVIDENCE_FILE_ENV_VAR,
            Constants.GEOIP_TRUTH_EVIDENCE_FILE_ENV_VAR,
            Constants.LICENSE_KEY_ENV_VAR,
            "_51DEGREES_DD_PATH"
        }
        .Concat(Constants.OLD_IP_INTELLIGENCE_DATA_FILE_ENV_VARS)
        .ToArray();

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
    /// what it wrote to the console before that section. The section has a
    /// link to the contact page of its own, so the tests look for the
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

    /// <summary>
    /// Checks that an example that needs the Enterprise data file said so
    /// and gave the link.
    /// </summary>
    private static void AssertStopped(string output)
    {
        Assert.Contains("which only the Enterprise data file has", output,
            "The example should say which properties need the Enterprise " +
            "data file.");
        Assert.Contains(ContactUs, output,
            "The example should link to the contact page.");
    }

    [TestMethod]
    public void GettingStarted_WithoutSettings()
    {
        var output = RunWithoutSettings(
            Examples.OnPremise.GettingStartedConsole.Program.Main);
        Assert.Contains("RegisteredName:", output,
            "The Lite data file has RegisteredName.");
        Assert.Contains(
            $"IpRangeStart: {Examples.OnPremise.ExampleUtils.NEEDS_ENTERPRISE}",
            output,
            "IpRangeStart should be shown as needing the Enterprise data file.");
        Assert.Contains(ContactUs, output,
            "The example should link to the contact page.");
    }

    [TestMethod]
    public void Suspicious_WithoutSettings()
    {
        AssertStopped(RunWithoutSettings(
            Examples.OnPremise.Suspicious.Program.Main));
    }

    [TestMethod]
    public void Compare_WithoutSettings()
    {
        AssertStopped(RunWithoutSettings(
            Examples.OnPremise.Compare.Program.Main));
    }

    [TestMethod]
    public void Metrics_WithoutSettings()
    {
        AssertStopped(RunWithoutSettings(
            Examples.OnPremise.Metrics.Program.Main));
    }

    [TestMethod]
    public void LocationAnalysis_WithoutSettings()
    {
        AssertStopped(RunWithoutSettings(
            Examples.OnPremise.LocationAnalysis.Program.Main));
    }

    [TestMethod]
    public void OfflineProcessing_WithoutSettings()
    {
        var output = RunWithoutSettings(
            Examples.OnPremise.OfflineProcessing.Program.Main);
        Assert.Contains("Some of these properties need the Enterprise data file",
            output, "The example should say that Country needs the " +
            "Enterprise data file.");
        Assert.Contains(ContactUs, output,
            "The example should link to the contact page.");
    }

    [TestMethod]
    public void Metadata_WithoutSettings()
    {
        var output = RunWithoutSettings(
            Examples.OnPremise.Metadata.Program.Main);
        Assert.Contains("Property - RegisteredName", output,
            "The Lite data file has RegisteredName.");
        Assert.Contains("This example used a free data file", output,
            "The example should say what the Enterprise data file adds.");
        Assert.Contains(ContactUs, output,
            "The example should link to the contact page.");
    }

    [TestMethod]
    public void Performance_WithoutSettings()
    {
        var output = RunWithoutSettings(
            Examples.OnPremise.Performance.Program.Main);
        Assert.Contains("This example used a free data file", output,
            "The example should say what the Enterprise data file adds.");
    }

    [TestMethod]
    public void UpdateDataFile_WithoutSettings()
    {
        var exitCode = -1;
        var output = RunWithoutSettings(args =>
            exitCode = Examples.OnPremise.UpdateDataFile.Program.Main(args));
        Assert.AreEqual(0, exitCode,
            "Without a licence key the example should explain and finish.");
        Assert.Contains("Updating the data file downloads the Enterprise data file",
            output, "The example should say what an update needs.");
        Assert.Contains(ContactUs, output,
            "The example should link to the contact page.");
    }

    [TestMethod]
    public void MixedGettingStarted_WithoutSettings()
    {
        var output = RunWithoutSettings(
            Examples.Mixed.OnPremise.GettingStartedConsole.Program.Main);
        Assert.Contains("Device Detection Results:", output,
            "The example should show the Device Detection results.");
        Assert.Contains(
            $"Hardware Vendor: {Examples.OnPremise.ExampleUtils.NEEDS_ENTERPRISE}",
            output,
            "HardwareVendor should be shown as needing the Enterprise data file.");
        Assert.Contains(
            $"Country: {Examples.OnPremise.ExampleUtils.NEEDS_ENTERPRISE}",
            output,
            "Country should be shown as needing the Enterprise data file.");
        Assert.Contains(ContactUs, output,
            "The example should link to the contact page.");
    }
}
