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


// Ignore Spelling: Metadata Offline

using FiftyOne.IpIntelligence.Examples;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

// Run this assembly's tests serially. Each on-premise example builds its own
// engine against the multi-gigabyte enterprise data file; running them in
// parallel (the previous [assembly: Parallelize] behaviour) loads several such
// engines at once and exhausts memory on CI runners, causing the job to time
// out. Concurrency is still covered by
// TestGettingStartedOnPremise.GettingStarted_ParallelProcessing_IsConsistent,
// which exercises many concurrent requests against a single shared engine.
[assembly: DoNotParallelize]
namespace FiftyOne.IpIntelligence.Example.Tests.OnPremise;

/// <summary>
/// This test class ensures that the on-premise examples execute successfully
/// and, where practical, produce the output a user would expect.
/// </summary>
/// <remarks>
/// These are integration tests: each example builds a real on-premise IP
/// Intelligence engine against the enterprise data file. They therefore
/// require the data file to be present (located automatically, or via the
/// <c>51DEGREES_IPI_PATH</c> environment variable) and are comparatively
/// slow. They go beyond pure smoke testing by asserting on the example output
/// (expected section headers, echoed IPv4 evidence and known property names)
/// rather than just that the example does not crash.
/// </remarks>
[TestClass]
[TestCategory(TestCategories.Integration)]
public class TestExamples
{
    public TestContext TestContext { get; set; }
    private StringBuilder OutputString { get; set; }
    private StringWriter OutputWriter { get; set; }

    private string LicenseKey;

    private string DataFile;

    private string EvidenceFile;
    private string GeoIpTruthEvidenceFile;

    /// <summary>
    /// Init method - specify License Key to run examples here or
    /// set a License Key in an environment variable called 'ResourceKey'.
    /// Set data file for hash examples.
    /// </summary>
    [TestInitialize]
    public void Init()
    {
        OutputString = new StringBuilder();
        OutputWriter = new StringWriter(OutputString);
        // Set license key for autoupdate examples.
        var licenseKey = Environment.GetEnvironmentVariable(
            Constants.LICENSE_KEY_ENV_VAR);
        LicenseKey = string.IsNullOrWhiteSpace(licenseKey) == false ?
            licenseKey: "!!YOUR_LICENSE_KEY!!";

        // Set IP Intelligence Data file
        DataFile = ExampleUtils.FindDataFile(
            Constants.ENTERPRISE_IPI_DATA_FILE_NAME);

        File.WriteAllText($"{nameof(TestExamples)}_DataFileName.txt", DataFile);

        // Set evidence file for offline processing example.
        EvidenceFile = Environment.GetEnvironmentVariable(
            Constants.EVIDENCE_FILE_ENV_VAR);
        if (string.IsNullOrWhiteSpace(EvidenceFile))
        {
            EvidenceFile = ExampleUtils.FindFile(
                Constants.YAML_EVIDENCE_FILE_NAME);
        }

        GeoIpTruthEvidenceFile = Environment.GetEnvironmentVariable(
            Constants.GEOIP_TRUTH_EVIDENCE_FILE_ENV_VAR);
        if (string.IsNullOrWhiteSpace(GeoIpTruthEvidenceFile))
        {
            GeoIpTruthEvidenceFile = ExampleUtils.FindFile(
                Constants.GEOIP_COMPARISON_EVIDENCE_FILE_NAME);
        }
    }

    [TestCleanup]
    public void Cleanup()
    {
        TestContext.WriteLine(OutputString.ToString());
    }

    /// <summary>
    /// Fail with an explanatory message if the data file required by every
    /// integration test in this class could not be located.
    /// </summary>
    private void VerifyDataFileAvailable()
    {
        Assert.IsFalse(
            string.IsNullOrWhiteSpace(DataFile) || File.Exists(DataFile) == false,
            $"This test requires an on-premise IP Intelligence data file. Place a " +
            $"'{Constants.ENTERPRISE_IPI_DATA_FILE_NAME}' file in the repository or " +
            $"set the '{Constants.IP_INTELLIGENCE_DATA_FILE_ENV_VAR}' environment " +
            $"variable to its path.");
    }

    /// <summary>
    /// Run an example, converting a data/engine version mismatch into an
    /// inconclusive result (rather than a hard failure) so the gap is surfaced
    /// without masquerading as a regression in the example itself.
    /// </summary>
    private static void RunResilient(Action run)
    {
        try
        {
            run();
        }
        catch (Exception ex) when (IsDataFileVersionError(ex))
        {
            Assert.Inconclusive(
                "The configured IP Intelligence data file could not be loaded by " +
                "the native engine (likely a data/engine version mismatch). Update " +
                "the ip-intelligence-data submodule or the engine package. " +
                $"Details: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns true if the exception (or any inner exception) indicates the
    /// data file is missing or an unsupported version.
    /// </summary>
    private static bool IsDataFileVersionError(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e.Message.Contains("unsupported version", StringComparison.OrdinalIgnoreCase) ||
                e.Message.Contains("Check you have the latest data", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns true if the exception (or any inner exception) is the result of
    /// an example being stopped by its cancellation token.
    /// </summary>
    private static bool IsCancellation(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is OperationCanceledException)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns true if the exception (or any inner exception) indicates the
    /// configured license key was rejected by the 51Degrees data update service
    /// (e.g. it is not entitled to IP Intelligence data). This is an environment
    /// / credential limitation rather than a fault in the example.
    /// </summary>
    private static bool IsLicenseOrUpdateError(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e.GetType().Name.Contains("DataUpdateException", StringComparison.OrdinalIgnoreCase) ||
                e.Message.Contains("license key", StringComparison.OrdinalIgnoreCase) ||
                e.Message.Contains("data update service", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Test the GettingStarted Example.
    /// Verifies the example runs and prints the expected report structure for
    /// the IPv4 evidence it analyses (input echo + results section + a known
    /// property name).
    /// </summary>
    [TestMethod]
    public void Example_OnPremise_GettingStarted()
    {
        VerifyDataFileAvailable();

        var example = new Examples.OnPremise.GettingStartedConsole.Program.Example();
        RunResilient(() => example.Run(DataFile, new LoggerFactory(), OutputWriter));

        var output = OutputString.ToString();
        Assert.Contains("Input values:", output,
            "Output should echo the input evidence values.");
        Assert.Contains("Results:", output,
            "Output should contain a results section.");
        Assert.Contains("query.client-ip", output,
            "Output should echo the client IP evidence key.");
        // The example analyses a fixed set of IPv4 addresses - at least the
        // first should be echoed back verbatim in the input section.
        Assert.Contains("194.209.0.1", output,
            "Output should echo back the IPv4 evidence that was supplied.");
        Assert.Contains("Country", output,
            "Output should contain the Country property.");
        Assert.Contains("IpRangeStart", output,
            "Output should contain the matched IP range start.");
    }

    /// <summary>
    /// Test the OfflineProcessing Example.
    /// Uses a self-contained YAML evidence file (so the test does not depend on
    /// the contents of the shipped evidence file) and asserts the example
    /// processed the records and emitted the expected YAML document markers and
    /// property output.
    /// </summary>
    [TestMethod]
    public void Example_OnPremise_OfflineProcessing()
    {
        VerifyDataFileAvailable();

        var evidenceFile = WriteTempEvidenceYaml(
            "82.12.34.23",
            "116.154.188.222",
            "2001:0db8:085a:0000:0000:8a2e:0370:7334");
        try
        {
            var example = new Examples.OnPremise.OfflineProcessing.Program.Example();
            using (var reader = new StreamReader(File.OpenRead(evidenceFile)))
            {
                RunResilient(() =>
                    example.Run(DataFile, reader, new LoggerFactory(), OutputWriter));
            }
        }
        finally
        {
            File.Delete(evidenceFile);
        }

        var output = OutputString.ToString();
        Assert.Contains("query.client-ip", output,
            "Offline output should echo each evidence record's client IP.");
        Assert.Contains("82.12.34.23", output,
            "Offline output should contain the IPv4 evidence that was supplied.");
        Assert.Contains("RegisteredName", output,
            "Offline output should contain the requested RegisteredName property.");
        Assert.Contains("...", output,
            "Offline output should contain the YAML end-of-stream marker, " +
            "indicating all records were processed.");
    }

    /// <summary>
    /// Test the Metadata Example.
    /// Asserts the example prints the engine metadata sections (accepted
    /// evidence keys, profile counts and component/property listings).
    /// </summary>
    [TestMethod]
    public void Example_OnPremise_Metadata()
    {
        VerifyDataFileAvailable();

        var example = new Examples.OnPremise.Metadata.Program.Example();
        RunResilient(() => example.Run(DataFile, new LoggerFactory(), OutputWriter));

        var output = OutputString.ToString();
        Assert.Contains("Accepted evidence keys:", output,
            "Metadata output should list the accepted evidence keys.");
        Assert.Contains("Profile counts:", output,
            "Metadata output should list profile counts.");
        Assert.Contains("Property -", output,
            "Metadata output should list the available properties.");
    }

    /// <summary>
    /// Test the Comparison Example.
    /// Asserts the comparison wrote a non-empty report for the GeoIP truth
    /// evidence rather than just completing.
    /// </summary>
    [TestMethod]
    public void Example_OnPremise_CompareConsole()
    {
        VerifyDataFileAvailable();

        var tempfile = Path.GetTempFileName();
        try
        {
            using (var writer = new StreamWriter(File.Create(tempfile)))
            {
                RunResilient(() =>
                    Examples.OnPremise.Compare.Program.Example.Run(
                        DataFile,
                        GeoIpTruthEvidenceFile,
                        writer,
                        new LoggerFactory(),
                        CancellationToken.None).Wait());
            }

            Assert.IsGreaterThan(0, new FileInfo(tempfile).Length,
                "Comparison example should write a non-empty comparison report.");
        }
        finally
        {
            File.Delete(tempfile);
        }
    }

    /// <summary>
    /// Test the LocationAnalysis Example.
    /// Runs the analysis with a very low sample percentage and a bounded
    /// run time so the test completes quickly, then asserts that a CSV
    /// report with the expected header and at least one data row was
    /// produced.
    /// </summary>
    [TestMethod]
    public void Example_OnPremise_LocationAnalysisConsole()
    {
        VerifyDataFileAvailable();

        var tempfile = Path.GetTempFileName();
        try
        {
            using (var writer = new StreamWriter(File.Create(tempfile)))
            using (var cancellationSource =
                new CancellationTokenSource(TimeSpan.FromMinutes(3)))
            {
                RunResilient(() =>
                    Examples.OnPremise.LocationAnalysis.Program.Example.Run(
                        DataFile,
                        writer,
                        new LoggerFactory(),
                        // Sample 0.05% of the address space so the test
                        // completes quickly.
                        0.0005,
                        null,
                        cancellationSource.Token));
            }

            var lines = File.ReadAllLines(tempfile);
            Assert.IsGreaterThan(1, lines.Length,
                "The location analysis should write a header and at least " +
                "one group row.");
            Assert.Contains("IpCount", lines[0],
                "The CSV header should contain the IpCount column.");
            Assert.Contains("AverageAreaSqKm", lines[0],
                "The CSV header should contain the AverageAreaSqKm column.");
        }
        finally
        {
            File.Delete(tempfile);
        }
    }

    /// <summary>
    /// Test the Metrics Example.
    /// A complete run analyses every IP range in the data file, which takes
    /// far longer than a test should, so the example is stopped after a fixed
    /// budget and the metrics gathered up to that point are checked. The
    /// example writes its CSV whether it is stopped early or runs to the end.
    /// </summary>
    [TestMethod]
    public void Example_OnPremise_MetricsConsole()
    {
        VerifyDataFileAvailable();

        var tempfile = Path.GetTempFileName();
        // This budget is always spent in full - the example never runs out of
        // ranges first - so the test's runtime is this value plus the cost of
        // unwinding. It has to stay clear of the CI blame hang timeout, which is
        // the only ceiling above it that applies on CI and which used to be set
        // to this same 5 minutes; see ci/run-unit-tests.ps1 before changing it.
        //
        // Note that a runner slow enough to still be building the area index
        // when this fires reports Inconclusive below rather than asserting on
        // any metrics, which leaves CI green. Shortening this budget makes that
        // outcome more likely.
        using var cancellation = new CancellationTokenSource(
            TimeSpan.FromMinutes(5));
        try
        {
            using (var writer = new StreamWriter(File.Create(tempfile)))
            {
                RunResilient(() => Examples.OnPremise.Metrics.Program.Example.Run(
                    DataFile,
                    new LoggerFactory(),
                    writer,
                    // Sample 0.01% of the addresses in a range...
                    0.0001,
                    // ...and never more than one address from any one range.
                    1,
                    // Take a hundredth of the ranges. Discarding a range
                    // before its values are read is what keeps this short.
                    0.01,
                    // Give up once the ranges have run out rather than
                    // scanning the profiles that follow them.
                    100_000,
                    // Keep the data file on disk. The test does not need the
                    // memory a faster profile would hold.
                    FiftyOne.Pipeline.Engines.PerformanceProfiles.LowMemory,
                    // Include all the possible IP ranges.
                    (_) => true,
                    cancellation.Token).Wait());
            }

            var lines = File.ReadAllLines(tempfile);
            Assert.IsGreaterThan(
                1,
                lines.Length,
                "Metrics output should contain a header and at least one group.");
            Assert.Contains("AverageAreaKm", lines[0],
                "Metrics output should contain the average area column.");
        }
        catch (Exception ex) when (IsCancellation(ex))
        {
            Assert.Inconclusive(
                "The metrics example did not get as far as processing any IP " +
                "ranges within the time allowed for this test.");
        }
        finally
        {
            File.Delete(tempfile);
        }
    }

    /// <summary>
    /// Test the Suspicious Example.
    /// Asserts the example prints the suspicious-IP report including the
    /// IsSuspicious verdict line.
    /// </summary>
    [TestMethod]
    public void Example_OnPremise_Suspicious()
    {
        VerifyDataFileAvailable();

        var example = new Examples.OnPremise.Suspicious.Program.Example();
        RunResilient(() => example.Run(DataFile, new LoggerFactory(), OutputWriter));

        var output = OutputString.ToString();
        Assert.Contains("Input values:", output,
            "Suspicious output should echo the input evidence.");
        Assert.Contains("IsSuspicious:", output,
            "Suspicious output should contain the IsSuspicious verdict.");
    }

    /// <summary>
    /// Test the UpdateDataFile Example.
    /// This requires a 51Degrees license key; when one is not configured the
    /// test reports inconclusive (rather than being permanently ignored) so the
    /// gap is visible in CI.
    /// </summary>
    [TestMethod]
    public void Example_OnPremise_UpdateDataFile()
    {
        VerifyLicenseKeyAvailable();
        VerifyDataFileAvailable();
        try
        {
            Examples.OnPremise.UpdateDataFile.Program.Initialize(
                DataFile, LicenseKey, null, false);
        }
        catch (Exception ex) when (IsDataFileVersionError(ex) || IsLicenseOrUpdateError(ex))
        {
            Assert.Inconclusive(
                "The UpdateDataFile example could not complete because the " +
                "configured license key was not accepted by the 51Degrees data " +
                "update service (it must be a key entitled to IP Intelligence " +
                $"data). Details: {ex.Message}");
        }
    }

    private void VerifyLicenseKeyAvailable()
    {
        if (string.IsNullOrWhiteSpace(LicenseKey) == true ||
            LicenseKey.StartsWith("!!") == true)
        {
            Assert.Inconclusive("This test requires a 51Degrees license key. This can be " +
                "specified in the TestHashExamples.Init method or by setting an Environment " +
                $"variable called '{Constants.LICENSE_KEY_ENV_VAR}'");
        }
    }

    /// <summary>
    /// Test the Performance Example.
    /// Runs the benchmark over a tiny, self-contained evidence set on a single
    /// thread (just enough to prove the example executes and produces results,
    /// without turning the test into a real performance run) and asserts the
    /// returned benchmark results show detections were processed.
    /// </summary>
    [TestMethod]
    public void Example_OnPremise_Performance()
    {
        VerifyDataFileAvailable();

        var evidenceFile = WriteTempEvidenceYaml(
            "82.12.34.23",
            "116.154.188.222",
            "8.8.8.8");
        List<Examples.OnPremise.Performance.Program.BenchmarkResult> results = null;
        try
        {
            // LowMemory is the only profile the example benchmarks (the data
            // file must never be loaded entirely into memory); request only a
            // single property to keep the run light.
            var config = new Examples.OnPremise.Performance.PerformanceConfiguration(
                FiftyOne.Pipeline.Engines.PerformanceProfiles.LowMemory, false);
            RunResilient(() =>
                results = Examples.OnPremise.Performance.Program.Example.Run(
                    DataFile, evidenceFile, config, OutputWriter, (ushort)1));
        }
        finally
        {
            File.Delete(evidenceFile);
        }

        Assert.IsNotNull(results,
            "Performance example should return benchmark results.");
        Assert.IsGreaterThan(0, results.Count,
            "Performance example should return at least one benchmark result.");
        Assert.IsGreaterThan(0, results.Sum(r => r.Count),
            "Performance example should have processed at least one detection.");
    }

    /// <summary>
    /// Test the combined (device detection + IP intelligence) GettingStarted
    /// Example. This additionally requires a device detection data file; when
    /// one is not available the test reports inconclusive (rather than failing)
    /// so the gap is visible without masquerading as a regression.
    /// </summary>
    [TestMethod]
    public void Example_OnPremise_MixedGettingStarted()
    {
        VerifyDataFileAvailable();

        var deviceDataFile = FindDeviceDataFile();
        if (string.IsNullOrWhiteSpace(deviceDataFile) ||
            File.Exists(deviceDataFile) == false)
        {
            Assert.Inconclusive(
                "The combined example also requires a device detection data file " +
                "(one of " +
                $"'{string.Join("', '", Examples.Mixed.OnPremise.GettingStartedConsole.Constants.HASH_FILE_NAMES)}'). " +
                "It was not found. Supply it (CI fetches it as an asset) to run " +
                "this test.");
        }

        var example =
            new Examples.Mixed.OnPremise.GettingStartedConsole.Program.Example();
        RunResilient(() =>
            example.Run(deviceDataFile, DataFile, new LoggerFactory(), OutputWriter));

        var output = OutputString.ToString();
        Assert.Contains("Input values:", output,
            "Combined output should echo the input evidence.");
        Assert.Contains("Device Detection Results:", output,
            "Combined output should contain the device detection section.");
        Assert.Contains("IP Intelligence Results:", output,
            "Combined output should contain the IP intelligence section.");
        Assert.Contains("62.61.32.31", output,
            "Combined output should echo back the IPv4 evidence it analyses.");
    }

    /// <summary>
    /// Locate a device detection data file using the same candidate file names
    /// and search the combined example itself uses. Returns null when none can
    /// be found.
    /// </summary>
    private static string FindDeviceDataFile()
    {
        foreach (var fileName in
            Examples.Mixed.OnPremise.GettingStartedConsole.Constants.HASH_FILE_NAMES)
        {
            var path = ExampleUtils.FindFile(fileName);
            if (string.IsNullOrWhiteSpace(path) == false)
            {
                return path;
            }
        }
        return null;
    }

    /// <summary>
    /// Write a small, self-contained YAML evidence file (one document per IP)
    /// to a temporary path and return that path. The caller is responsible for
    /// deleting it.
    /// </summary>
    private static string WriteTempEvidenceYaml(params string[] ipAddresses)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"ipi-evidence-{Guid.NewGuid():N}.yml");
        var builder = new StringBuilder();
        foreach (var ip in ipAddresses)
        {
            builder.AppendLine("---");
            builder.AppendLine($"query.client-ip: {ip}");
        }
        builder.AppendLine("...");
        File.WriteAllText(path, builder.ToString());
        return path;
    }
}
