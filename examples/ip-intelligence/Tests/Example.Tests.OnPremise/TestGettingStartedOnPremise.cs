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

// Ignore Spelling: Ip Ipi

using FiftyOne.IpIntelligence.Engine.OnPremise.FlowElements;
using FiftyOne.IpIntelligence.Examples;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using ExampleConstants = FiftyOne.IpIntelligence.Examples.Constants;

namespace FiftyOne.IpIntelligence.Example.Tests.OnPremise;

/// <summary>
/// End-to-end "happy path" tests for on-premise IP Intelligence that go beyond
/// the smoke tests in <see cref="TestExamples"/> by asserting on the actual
/// detection results for known IP addresses.
/// </summary>
/// <remarks>
/// To keep these integration tests lightweight, a single <c>LowMemory</c>
/// pipeline is built once for the whole class (<see cref="ClassInit"/>) and
/// shared across every test, rather than each test loading the (multi-gigabyte)
/// data file into its own engine. The <c>LowMemory</c> profile is used
/// deliberately so the data file is paged from disk instead of loaded wholly
/// into RAM.
/// </remarks>
[TestClass]
[TestCategory(TestCategories.Integration)]
[DoNotParallelize]
public class TestGettingStartedOnPremise
{
    public TestContext TestContext { get; set; }

    // A public IPv4 address known to resolve to the United Kingdom - this is
    // the same address the Cloud examples test against.
    private const string PublicIpV4 = "82.12.34.23";

    // A private (RFC 1918) address that should not resolve to a geographic
    // location.
    private const string PrivateIpV4 = "10.0.0.1";

    private const string EvidenceKey = "query.client-ip";

    private static ILoggerFactory _loggerFactory;
    private static IPipeline _pipeline;
    private static string _dataFile;

    // When the shared pipeline cannot be created (no data file, or the data
    // file is an unsupported version), this holds the reason so each test can
    // report it as inconclusive rather than failing with a class-init error.
    private static string _skipReason;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        _dataFile = ExampleUtils.FindDataFile(
            ExampleConstants.ENTERPRISE_IPI_DATA_FILE_NAME);

        if (string.IsNullOrWhiteSpace(_dataFile) || File.Exists(_dataFile) == false)
        {
            _skipReason =
                $"This test requires an on-premise IP Intelligence data file. " +
                $"Place a '{ExampleConstants.ENTERPRISE_IPI_DATA_FILE_NAME}' file in " +
                $"the repository or set the " +
                $"'{ExampleConstants.IP_INTELLIGENCE_DATA_FILE_ENV_VAR}' environment " +
                $"variable to its path.";
            return;
        }

        _loggerFactory = new LoggerFactory();

        try
        {
            var ipEngine = new IpiOnPremiseEngineBuilder(_loggerFactory)
                // LowMemory keeps the data file on disk rather than loading the
                // whole (large) file into memory - appropriate for a test engine.
                .SetPerformanceProfile(PerformanceProfiles.LowMemory)
                .SetAutoUpdate(false)
                .SetDataFileSystemWatcher(false)
                .Build(_dataFile, false);

            _pipeline = new PipelineBuilder(_loggerFactory)
                .AddFlowElement(ipEngine)
                .Build();
        }
        catch (Exception ex) when (IsDataFileVersionError(ex))
        {
            // A data/engine version mismatch is an environment problem, not a
            // test failure - skip cleanly with the underlying cause surfaced.
            _skipReason =
                $"The IP Intelligence data file '{_dataFile}' could not be loaded " +
                $"by the native engine (likely a data/engine version mismatch). " +
                $"Update the ip-intelligence-data submodule or the engine package. " +
                $"Details: {ex.Message}";
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

    [ClassCleanup]
    public static void ClassCleanup()
    {
        _pipeline?.Dispose();
        _loggerFactory?.Dispose();
    }

    /// <summary>
    /// Skip (inconclusive) when the data file required to build the shared
    /// pipeline was not available at class initialization.
    /// </summary>
    private void RequirePipeline()
    {
        if (_pipeline is null)
        {
            Assert.Inconclusive(_skipReason ??
                "The shared IP Intelligence pipeline is not available.");
        }
    }

    private IIpIntelligenceData Process(string ipAddress)
    {
        using var data = _pipeline.CreateFlowData();
        data.AddEvidence(EvidenceKey, ipAddress);
        data.Process();
        return data.Get<IIpIntelligenceData>();
    }

    // The engine returns a result for every query: addresses it cannot
    // geo-locate (private ranges, malformed/out-of-range input) come back with
    // the "Unknown" sentinel rather than no value, so "resolved a real country"
    // means HasValue AND not the Unknown sentinel.
    private const string UnknownCountry = "Unknown";

    private static bool IsKnownCountry(IAspectPropertyValue<string> country)
    {
        return country.HasValue &&
            string.IsNullOrWhiteSpace(country.Value) == false &&
            country.Value.Equals(UnknownCountry, StringComparison.OrdinalIgnoreCase) == false;
    }

    /// <summary>
    /// A public IP address should resolve to a country and a containing IP
    /// range, and the engine should echo the queried IPv4 address back in the
    /// <c>Ip</c> property.
    /// </summary>
    [TestMethod]
    public void GettingStarted_PublicIpV4_ResolvesCountryAndRange()
    {
        RequirePipeline();

        var ipData = Process(PublicIpV4);

        Assert.IsTrue(IsKnownCountry(ipData.Country),
            $"Expected a known Country for public IP {PublicIpV4} but got " +
            $"'{(ipData.Country.HasValue ? ipData.Country.Value : ipData.Country.NoValueMessage)}'.");
        TestContext.WriteLine($"{PublicIpV4} -> Country: {ipData.Country.Value}");

        Assert.IsTrue(ipData.IpRangeStart.HasValue && ipData.IpRangeEnd.HasValue,
            "A matched IP should report a start and end of its range.");

        var queried = IPAddress.Parse(PublicIpV4);
        Assert.IsTrue(
            IsWithinRange(queried, ipData.IpRangeStart.Value, ipData.IpRangeEnd.Value),
            $"Queried IP {PublicIpV4} should fall within the returned range " +
            $"{ipData.IpRangeStart.Value} - {ipData.IpRangeEnd.Value}.");
    }

    /// <summary>
    /// The properties highlighted by the project manager (Country,
    /// ConnectionType, ContinentName) should be queryable for a public IP
    /// without throwing, and Country should be populated.
    /// </summary>
    [TestMethod]
    public void GettingStarted_PublicIpV4_ExposesExpectedProperties()
    {
        RequirePipeline();

        var ipData = Process(PublicIpV4);

        // These must be accessible without throwing; values are logged so the
        // expected data (Country / ConnectionType / ContinentName) is visible
        // in the test output as requested.
        TestContext.WriteLine($"Country:       " +
            $"{(ipData.Country.HasValue ? ipData.Country.Value : ipData.Country.NoValueMessage)}");
        TestContext.WriteLine($"ConnectionType: " +
            $"{(ipData.ConnectionType.HasValue ? ipData.ConnectionType.Value : ipData.ConnectionType.NoValueMessage)}");
        TestContext.WriteLine($"ContinentName: " +
            $"{(ipData.ContinentName.HasValue ? ipData.ContinentName.Value : ipData.ContinentName.NoValueMessage)}");

        Assert.IsTrue(IsKnownCountry(ipData.Country),
            "Country should be populated for a public IP address.");
    }

    /// <summary>
    /// IPv6 evidence should be accepted and processed without error. The
    /// documentation-range address used here is not expected to produce a
    /// geographic hit, so the test asserts the IPv6 path works rather than a
    /// specific value.
    /// </summary>
    [TestMethod]
    [DataRow("2001:0db8:085a:0000:0000:8a2e:0370:7334")]
    [DataRow("2a00:1450:4009:81f::200e")]
    public void GettingStarted_IpV6Evidence_IsProcessed(string ipV6)
    {
        RequirePipeline();

        // Should not throw for a syntactically valid IPv6 address.
        var ipData = Process(ipV6);

        // Touch the result to ensure the IPv6 path is fully exercised.
        var hasCountry = ipData.Country.HasValue;
        TestContext.WriteLine($"{ipV6} -> Country.HasValue={hasCountry}");

        // If the address matched a range, that range must be IPv6 too.
        if (ipData.IpRangeStart.HasValue)
        {
            Assert.AreEqual(
                System.Net.Sockets.AddressFamily.InterNetworkV6,
                ipData.IpRangeStart.Value.AddressFamily,
                "An IPv6 query that matches a range should return an IPv6 range start.");
        }
    }

    /// <summary>
    /// A private (RFC 1918) address should not resolve to a known country. The
    /// engine returns the "Unknown" sentinel rather than no value for such
    /// addresses.
    /// </summary>
    [TestMethod]
    public void GettingStarted_PrivateIp_HasNoKnownCountry()
    {
        RequirePipeline();

        var ipData = Process(PrivateIpV4);

        Assert.IsFalse(IsKnownCountry(ipData.Country),
            $"A private IP ({PrivateIpV4}) should not resolve to a known country, " +
            $"but returned '{(ipData.Country.HasValue ? ipData.Country.Value : null)}'.");
    }

    /// <summary>
    /// Processing flow data with no evidence at all should not throw and should
    /// report no value for the properties.
    /// </summary>
    [TestMethod]
    public void GettingStarted_NoEvidence_ReturnsNoValue()
    {
        RequirePipeline();

        using var data = _pipeline.CreateFlowData();
        // Deliberately add no evidence.
        data.Process();
        var ipData = data.Get<IIpIntelligenceData>();

        Assert.IsFalse(ipData.Country.HasValue,
            "Country should have no value when no evidence is supplied.");
        Assert.IsFalse(ipData.IpRangeStart.HasValue,
            "IpRangeStart should have no value when no evidence is supplied.");
    }

    /// <summary>
    /// Malformed IP evidence should be handled gracefully (no unhandled
    /// exception) and should not produce a known country.
    /// </summary>
    [TestMethod]
    [DataRow("not-an-ip")]
    [DataRow("999.999.999.999")]
    [DataRow("")]
    public void GettingStarted_MalformedEvidence_DoesNotThrow(string malformed)
    {
        RequirePipeline();

        // Reaching this point means Process did not throw for the malformed
        // input, which is the primary guarantee under test.
        var ipData = Process(malformed);

        Assert.IsFalse(IsKnownCountry(ipData.Country),
            $"Malformed evidence '{malformed}' should not resolve to a known country.");
    }

    /// <summary>
    /// The shared engine should be safe to use from many threads concurrently
    /// (the data file is opened once but flow data is created per request).
    /// This exercises the parallelism that the example pipelines rely on.
    /// </summary>
    [TestMethod]
    public void GettingStarted_ParallelProcessing_IsConsistent()
    {
        RequirePipeline();

        // Establish the expected answer on a single thread first.
        var expectedCountry = Process(PublicIpV4).Country.Value;

        var countries = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.For(0, 64, new ParallelOptions { MaxDegreeOfParallelism = 8 }, _ =>
        {
            var ipData = Process(PublicIpV4);
            countries.Add(ipData.Country.HasValue ? ipData.Country.Value : null);
        });

        Assert.HasCount(64, countries, "Every parallel request should complete.");
        Assert.IsTrue(countries.All(c => c == expectedCountry),
            "Every parallel request should return the same Country as the " +
            $"single-threaded result ('{expectedCountry}').");
    }

    /// <summary>
    /// Returns true if <paramref name="address"/> is within the inclusive range
    /// [<paramref name="start"/>, <paramref name="end"/>]. Addresses of
    /// different families are treated as out of range.
    /// </summary>
    private static bool IsWithinRange(IPAddress address, IPAddress start, IPAddress end)
    {
        var a = address.GetAddressBytes();
        var s = start.GetAddressBytes();
        var e = end.GetAddressBytes();
        if (a.Length != s.Length || a.Length != e.Length)
        {
            return false;
        }
        return CompareBytes(a, s) >= 0 && CompareBytes(a, e) <= 0;
    }

    private static int CompareBytes(IReadOnlyList<byte> left, IReadOnlyList<byte> right)
    {
        for (int i = 0; i < left.Count; i++)
        {
            int diff = left[i] - right[i];
            if (diff != 0)
            {
                return diff;
            }
        }
        return 0;
    }
}
