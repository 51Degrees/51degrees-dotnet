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

// Ignore Spelling: Wkt

using CsvHelper;
using CsvHelper.Configuration;
using Examples.OnPremise.Areas;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static FiftyOne.IpIntelligence.Examples.OnPremise.Compare.Program;
using static FiftyOne.IpIntelligence.Examples.OnPremise.Compare.Program.Example;
using Result = FiftyOne.IpIntelligence.Examples.OnPremise.Compare.Program.Result;

namespace FiftyOne.IpIntelligence.Example.Tests.OnPremise;

/// <summary>
/// Checks that a truth record the Compare example can not read or compare is
/// logged and skipped, and the records after it are still compared.
/// </summary>
[TestClass]
[TestCategory(TestCategories.Unit)]
public class TestCompareBadTruths
{
    private const string Header =
        "DateTimeUtc,Latitude,Longitude,Ip,AddressFamily,Continent,Country";

    /// <summary>
    /// A valid square of one tenth of a degree.
    /// </summary>
    private const string ValidWkt =
        "POLYGON ((0.1 51.1, 0.2 51.1, 0.2 51.2, 0.1 51.2, 0.1 51.1))";

    [TestMethod]
    public void ReadTruths_SkipsMalformedRecords()
    {
        var csv = String.Join("\n",
            Header,
            "09/23/2026 14:10:00,32.78,-96.81,1.2.3.4,InterNetwork,North America,United States",
            "09/23/2026 14:10:00,32.78,-96.81",
            "09/23/2026 14:10:00,not-a-number,-96.81,1.2.3.5,InterNetwork,North America,United States",
            "09/23/2026 14:10:00,32.78,-96.81,not-an-ip,InterNetwork,North America,United States",
            "09/23/2026 14:10:00,132.78,-96.81,1.2.3.6,InterNetwork,North America,United States",
            "09/23/2026 14:10:00,51.5,-0.12,2a03:2880:f812:20::,InterNetworkV6,Europe,United Kingdom");
        var logger = new ListLogger();
        var skipped = new SkippedTruths(logger);

        var truths = Read(csv, skipped);

        CollectionAssert.AreEqual(
            new[] { "1.2.3.4", "2a03:2880:f812:20::" },
            truths.Select(i => i.Ip).ToArray());
        CollectionAssert.AreEqual(
            new long[] { 2, 7 },
            truths.Select(i => i.LineNumber).ToArray());
        Assert.AreEqual(4, skipped.Count);
        Assert.AreEqual(2, skipped.Reasons["Malformed record"]);
        Assert.AreEqual(1, skipped.Reasons["Invalid IP address"]);
        Assert.AreEqual(1, skipped.Reasons["Invalid latitude or longitude"]);
        Assert.IsTrue(logger.Warnings.Any(i => i.Contains("line '3'")));
        Assert.IsTrue(logger.Warnings.Any(i => i.Contains("line '4'")));
        Assert.IsTrue(logger.Warnings.Any(i =>
            i.Contains("line '5'") && i.Contains("not-an-ip")));
    }

    [TestMethod]
    public void ReadTruths_MisnamedColumnFailsTheRun()
    {
        // A misnamed column must not read as zero for every record.
        var csv = String.Join("\n",
            Header.Replace("Latitude", "Lat"),
            "09/23/2026 14:10:00,32.78,-96.81,1.2.3.4,InterNetwork,North America,United States");

        Assert.ThrowsExactly<HeaderValidationException>(() =>
            Read(csv, new SkippedTruths(new ListLogger())));
    }

    [TestMethod]
    public async Task ProduceAndConsume_ProducerFailureWaitsForConsumers()
    {
        var truth = new BlockingCollection<Truth>(1);
        using var stopping = new CancellationTokenSource();
        var consumers = CreateConsumers(
            _ => new Result(),
            truth,
            new SkippedTruths(new ListLogger()),
            stopping);

        var ex = await Assert.ThrowsExactlyAsync<IOException>(() =>
            ProduceAndConsume(
                () =>
                {
                    truth.Add(new Truth { Ip = "1.1.1.1" });
                    throw new IOException("truth file");
                },
                truth,
                consumers,
                stopping));

        // The producer's failure is reported, and only once every consumer
        // has stopped so that the caller can dispose the engine safely.
        Assert.AreEqual("truth file", ex.Message);
        Assert.IsTrue(truth.IsAddingCompleted);
        Assert.IsTrue(consumers.All(i => i.Task.IsCompleted));
        Assert.IsTrue(stopping.IsCancellationRequested);
    }

    [TestMethod]
    public void ProcessTruth_SkipsBadTruthAndContinues()
    {
        var source = new BlockingCollection<Truth>();
        foreach (var ip in new[] { "1.1.1.1", "2.2.2.2", "3.3.3.3" })
        {
            source.Add(new Truth { Ip = ip, LineNumber = source.Count + 2 });
        }
        source.CompleteAdding();
        var logger = new ListLogger();
        var skipped = new SkippedTruths(logger);

        var output = ProcessTruth(
            i => i.Ip == "2.2.2.2"
                ? throw new InvalidDataException(
                    "Area could not be compared",
                    new IndexOutOfRangeException())
                : new Result(),
            source,
            skipped,
            CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "1.1.1.1", "3.3.3.3" },
            output.Select(i => i.Truth.Ip).ToArray());
        Assert.AreEqual(1, skipped.Count);
        Assert.Contains("line '3'", logger.Warnings.Single());
        Assert.Contains("2.2.2.2", logger.Warnings.Single());
    }

    [TestMethod]
    public void ProcessTruth_OtherFailuresStillStopTheConsumer()
    {
        var source = new BlockingCollection<Truth>
        {
            new Truth { Ip = "1.1.1.1" }
        };
        source.CompleteAdding();

        Assert.ThrowsExactly<InvalidOperationException>(() => ProcessTruth(
            _ => throw new InvalidOperationException(),
            source,
            new SkippedTruths(new ListLogger()),
            CancellationToken.None));
    }

    [TestMethod]
    public void Compare_ValidArea()
    {
        var truth = new Truth
        {
            Ip = "1.2.3.4",
            Latitude = 51.15,
            Longitude = 0.15
        };

        var result = Compare(truth, 51.15, 0.15, ValidWkt, "High");

        Assert.IsTrue(result.Contains);
        Assert.AreEqual(1, result.Geometries);
        Assert.AreEqual("High", result.Confidence);
        Assert.AreEqual("InterNetwork", truth.AddressFamily);
    }

    [TestMethod]
    [DataRow(
        "POLYGON ((0.1 51.1, 0.2 51.1, 0.2 51.2, 0.1 51.2))",
        "closed",
        DisplayName = "Unclosed ring")]
    [DataRow(
        "POLYGON ((0.1 51.1, 0.2 51.1",
        "could not be parsed",
        DisplayName = "Truncated WKT")]
    public void Compare_BadArea_ThrowsInvalidData(string wkt, string reason)
    {
        var truth = new Truth
        {
            Ip = "1.2.3.4",
            Latitude = 51.15,
            Longitude = 0.15
        };

        var ex = Assert.ThrowsExactly<InvalidDataException>(() =>
            Compare(truth, 51.15, 0.15, wkt, null));

        Assert.IsInstanceOfType<InvalidDataException>(ex.InnerException);
        Assert.Contains(reason, ex.InnerException.Message);

        // The failure is cached with the WKT and repeated for the next record
        // with the same area.
        Assert.ThrowsExactly<InvalidDataException>(() =>
            Compare(truth, 51.15, 0.15, wkt, null));
    }

    [TestMethod]
    public void SkippedTruths_LimitsRecordsLogged()
    {
        var logger = new ListLogger();
        var skipped = new SkippedTruths(logger, 2);

        for (var i = 0; i < 5; i++)
        {
            skipped.Add(i, "1.2.3.4", "Invalid IP address", null);
        }
        skipped.LogSummary("source.csv");

        Assert.AreEqual(5, skipped.Count);
        // Two records, the notice that no more will be logged, and the
        // summary.
        Assert.HasCount(4, logger.Warnings);
        Assert.Contains("'5' Invalid IP address", logger.Warnings.Last());
    }

    [TestMethod]
    public void SkippedTruths_TooManySkippedFailsTheRun()
    {
        // Half of the records may be skipped once two have been.
        var skipped = new SkippedTruths(
            new ListLogger(),
            maxShare: 0.5,
            minimumSkipped: 2);
        for (var i = 0; i < 4; i++)
        {
            skipped.Read();
        }

        skipped.Add(2, "1.2.3.4", "Invalid IP address", null);
        skipped.Add(3, "1.2.3.5", "Invalid IP address", null);
        var ex = Assert.ThrowsExactly<InvalidDataException>(() =>
            skipped.Add(4, "1.2.3.6", "Invalid IP address", null));

        Assert.Contains("'3' of '4'", ex.Message);
        Assert.Contains("line '4'", ex.Message);
    }

    [TestMethod]
    public void ProcessTruth_TooManySkippedStopsTheConsumer()
    {
        var source = new BlockingCollection<Truth>();
        var skipped = new SkippedTruths(
            new ListLogger(),
            maxShare: 0.5,
            minimumSkipped: 1);
        for (var i = 0; i < 2; i++)
        {
            skipped.Read();
            source.Add(new Truth { Ip = "1.1.1.1", LineNumber = i + 2 });
        }
        source.CompleteAdding();

        // Every record fails, so the second one fails the run rather than
        // being skipped.
        Assert.ThrowsExactly<InvalidDataException>(() => ProcessTruth(
            _ => throw new InvalidDataException("Area could not be compared"),
            source,
            skipped,
            CancellationToken.None));
        Assert.AreEqual(2, skipped.Count);
    }

    private static List<Truth> Read(string csv, SkippedTruths skipped)
    {
        using var reader = new StringReader(csv);
        var config = CsvConfiguration.FromAttributes<Truth>(
            CultureInfo.InvariantCulture);
        config.MissingFieldFound = null;
        using var source = new CsvReader(reader, config);
        return ReadTruths(source, skipped).ToList();
    }

    /// <summary>
    /// Keeps the warnings logged so that tests can check them.
    /// </summary>
    private class ListLogger : ILogger
    {
        public List<string> Warnings { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                lock (Warnings)
                {
                    Warnings.Add(formatter(state, exception));
                }
            }
        }
    }
}
