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

using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.Engines.Trackers;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Tests.FlowElements;

/// <summary>
/// The values that do not change while the element lives are written to its
/// first record and again once an hour after that, and every record carries
/// the identifier the element created when it was built.
/// The specification is pipeline-specification/pipeline-elements/
/// usage-sharing-element.md in the specifications repository at 07e1fced,
/// and the line numbers below are of that file at that commit.
/// The element's clock is the test's, so no test waits an hour.
/// </summary>
[TestClass]
public class ShareUsageInvariantValuesTests
{
    /// <summary>
    /// The names of the elements that hold the invariant values, from the
    /// list at lines 138 to 145: "Version", "Product", "Flow Elements",
    /// "Packages", "Language", "LanguageVersion", "ServerIP", "Platform".
    /// </summary>
    private static readonly string[] InvariantElements = new[]
    {
        "Version",
        "Product",
        "FlowElement",
        "Package",
        "Language",
        "LanguageVersion",
        "ServerIP",
        "Platform",
    };

    /// <summary>
    /// The string format of RFC 9562 section 4 for a version 4 UUID, as
    /// lines 119 to 126 require, in either letter case.
    /// </summary>
    private static readonly Regex Version4Uuid = new Regex(
        "^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$",
        RegexOptions.IgnoreCase);

    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    private readonly LoggerFactory _loggerFactory = new LoggerFactory();

    /// <summary>
    /// Lines 150 and 115 to 116: "The invariant values MUST be written to
    /// the first record the element writes." and the element "MUST write
    /// it to every record as the `ProcessId` element."
    /// </summary>
    [TestMethod]
    public void TheFirstRecord_CarriesTheInvariantValuesAndTheProcessId()
    {
        using var test = new ElementUnderTest(_loggerFactory);

        var records = test.Send("10.1.0.1");

        Assert.HasCount(1, records);
        AssertCarriesInvariantValues(records[0]);
        StringAssert.Contains(records[0], "<Package Name=\"FiftyOne.Pipeline.Core\">");
        StringAssert.Contains(records[0], "<ServerIP>192.0.2.1</ServerIP>");
        StringAssert.Matches(ProcessId(records[0]), Version4Uuid);
    }

    /// <summary>
    /// Lines 152 to 153: "They MUST NOT be written to any other record."
    /// Neither the other records of the first message, nor those of a
    /// message sent before the hour is up, carry them.
    /// </summary>
    [TestMethod]
    public void TheRecordsAfterTheFirst_LeaveOutTheInvariantValues_UntilTheHourIsUp()
    {
        using var test = new ElementUnderTest(_loggerFactory);

        var first = test.Send("10.1.0.1", "10.1.0.2", "10.1.0.3");
        test.Clock += Hour - TimeSpan.FromSeconds(1);
        var later = test.Send("10.1.0.4", "10.1.0.5");

        Assert.HasCount(3, first);
        Assert.HasCount(2, later);
        AssertCarriesInvariantValues(first[0]);
        foreach (var record in first.Skip(1).Concat(later))
        {
            AssertLeavesOutInvariantValues(record);
            // The values that change from record to record are all there.
            StringAssert.Matches(record, new Regex("<ClientIP>10\\.1\\.0\\.\\d</ClientIP>"));
            StringAssert.Contains(record, "<DateSent>");
            StringAssert.Contains(record, "<Sequence>");
        }
    }

    /// <summary>
    /// Lines 151 to 152: "They MUST be written again to the first record
    /// the element writes once an hour or more has passed since it last
    /// wrote them." and lines 159 to 160: "The hour is measured from when
    /// the values were last written to a record".
    /// </summary>
    [TestMethod]
    public void AnHourAfterTheyWereLastWritten_TheInvariantValuesAreWrittenAgain()
    {
        using var test = new ElementUnderTest(_loggerFactory);

        var first = test.Send("10.2.0.1");
        test.Clock += Hour;
        var anHourOn = test.Send("10.2.0.2", "10.2.0.3");
        test.Clock += Hour - TimeSpan.FromSeconds(1);
        var lessThanAnHourOn = test.Send("10.2.0.4");
        test.Clock += TimeSpan.FromSeconds(1);
        var anotherHourOn = test.Send("10.2.0.5");
        test.Clock += TimeSpan.FromHours(5);
        var hoursOn = test.Send("10.2.0.6", "10.2.0.7");

        AssertCarriesInvariantValues(first[0]);
        AssertCarriesInvariantValues(anHourOn[0]);
        AssertLeavesOutInvariantValues(anHourOn[1]);
        AssertLeavesOutInvariantValues(lessThanAnHourOn[0]);
        AssertCarriesInvariantValues(anotherHourOn[0]);
        AssertCarriesInvariantValues(hoursOn[0]);
        AssertLeavesOutInvariantValues(hoursOn[1]);
    }

    /// <summary>
    /// Lines 238 to 239: "**ProcessId** - The process identifier, the same
    /// in every record the element writes."
    /// </summary>
    [TestMethod]
    public void OneElement_WritesOneProcessIdToEveryRecord()
    {
        using var test = new ElementUnderTest(_loggerFactory);

        var records = test.Send("10.3.0.1", "10.3.0.2")
            .Concat(test.Send("10.3.0.3"))
            .Concat(Advance(test, Hour).Send("10.3.0.4", "10.3.0.5"))
            .ToList();

        Assert.HasCount(5, records);
        var ids = records.Select(ProcessId).ToList();
        StringAssert.Matches(ids[0], Version4Uuid);
        foreach (var id in ids)
        {
            Assert.AreEqual(ids[0], id, ignoreCase: true);
        }
        // Each record carries it once.
        foreach (var record in records)
        {
            Assert.HasCount(1, Regex.Matches(record, "<ProcessId>"), record);
        }
    }

    /// <summary>
    /// Lines 128 to 131: "The identifier is never stored, so a process that
    /// starts again creates a new one. A process that builds more than one
    /// Usage Sharing Element ... creates an identifier for each". A fresh
    /// element stands for a process that starts again, as each builds its
    /// own, and its first record carries the invariant values again.
    /// </summary>
    [TestMethod]
    public void AFreshElement_HasANewProcessId_AndWritesTheInvariantValuesAgain()
    {
        string firstId;
        using (var first = new ElementUnderTest(_loggerFactory))
        {
            firstId = ProcessId(first.Send("10.4.0.1")[0]);
        }
        using var restarted = new ElementUnderTest(_loggerFactory);
        using var alongside = new ElementUnderTest(_loggerFactory);

        var restartedRecord = restarted.Send("10.4.0.2")[0];
        var alongsideRecord = alongside.Send("10.4.0.3")[0];

        var ids = new[]
        {
            firstId,
            ProcessId(restartedRecord),
            ProcessId(alongsideRecord),
        };
        foreach (var id in ids)
        {
            StringAssert.Matches(id, Version4Uuid);
        }
        Assert.HasCount(3, ids.Distinct(StringComparer.OrdinalIgnoreCase),
            string.Join(", ", ids));
        AssertCarriesInvariantValues(restartedRecord);
        AssertCarriesInvariantValues(alongsideRecord);
    }

    private static ElementUnderTest Advance(ElementUnderTest test, TimeSpan by)
    {
        test.Clock += by;
        return test;
    }

    private static string ProcessId(string record)
    {
        var match = Regex.Match(record, "<ProcessId>([^<]*)</ProcessId>");
        Assert.IsTrue(match.Success, "The record has no ProcessId. " + record);
        return match.Groups[1].Value;
    }

    private static void AssertCarriesInvariantValues(string record)
    {
        foreach (var name in InvariantElements)
        {
            StringAssert.Matches(record, new Regex($"<{name}[ >/]"),
                $"The record does not carry {name}. {record}");
        }
    }

    private static void AssertLeavesOutInvariantValues(string record)
    {
        foreach (var name in InvariantElements)
        {
            StringAssert.DoesNotMatch(record, new Regex($"<{name}[ >/]"),
                $"The record carries {name}. {record}");
        }
    }

    /// <summary>
    /// A Pipeline with the Sequence Element and an element whose clock and
    /// machine address the test sets. Entries are put on its queue directly
    /// and sent one message at a time, so what each message holds does not
    /// depend on thread timing.
    /// </summary>
    private sealed class ElementUnderTest : IDisposable
    {
        private readonly UsageMessageCapture _capture = new UsageMessageCapture();
        private readonly HttpClient _http;
        private readonly IPipeline _pipeline;
        private readonly ClockElement _element;

        public ElementUnderTest(ILoggerFactory loggerFactory)
        {
            _http = new HttpClient(_capture);
            _element = new ClockElement(
                loggerFactory.CreateLogger<ShareUsageElement>(), _http);
            _pipeline = new PipelineBuilder(loggerFactory)
                .AddFlowElement(new SequenceElementBuilder(loggerFactory).Build())
                .AddFlowElement(_element)
                .Build();
        }

        /// <summary>
        /// The element's clock, starting at an arbitrary point.
        /// </summary>
        public TimeSpan Clock
        {
            get => _element.Time;
            set => _element.Time = value;
        }

        /// <summary>
        /// Sends one message holding an entry for each client IP, and
        /// returns its records in order.
        /// </summary>
        public List<string> Send(params string[] clientIps)
        {
            foreach (var ip in clientIps)
            {
                _element.Enqueue(ip);
            }
            var before = _capture.Xml.Count;
            _element.SendAsync().Wait();
            Assert.AreEqual(before + 1, _capture.Xml.Count,
                "One message should be sent.");
            var xml = _capture.Xml.Last();
            var records = Regex.Matches(xml, "<Device>.*?</Device>",
                    RegexOptions.Singleline)
                .Select(m => m.Value)
                .ToList();
            Assert.HasCount(clientIps.Length, records, xml);
            return records;
        }

        public void Dispose()
        {
            _pipeline.Dispose();
            _http.Dispose();
        }
    }

    private sealed class ClockElement : ShareUsageElement
    {
        private static readonly HostAddressLookup FixedAddress =
            new HostAddressLookup(() => "192.0.2.1");

        public ClockElement(ILogger<ShareUsageBase> logger, HttpClient http)
            : base(logger, http, 1, 100, 1000, 100, 100, 1, false,
                  "http://51Degrees.com/test", new List<string>(),
                  new List<string>(), new List<KeyValuePair<string, string>>(),
                  Engines.Constants.DEFAULT_ASP_COOKIE_NAME,
                  new Mock<ITracker>().Object, false)
        {
        }

        public TimeSpan Time { get; set; } = TimeSpan.FromDays(3);

        internal override TimeSpan ClockNow => Time;

        internal override HostAddressLookup HostAddressSource => FixedAddress;

        /// <summary>
        /// Long enough for the lookup, which runs on the thread pool, to
        /// finish on a busy machine, so the first record has the address.
        /// </summary>
        internal override int HostAddressWaitMilliseconds => 30000;

        public void Enqueue(string clientIp) =>
            EvidenceCollection.Add(new ShareUsageData()
            {
                ClientIP = clientIp,
                SessionId = "session",
                Sequence = 1,
            });

        public System.Threading.Tasks.Task SendAsync() => BuildAndSendXmlAsync();
    }
}
