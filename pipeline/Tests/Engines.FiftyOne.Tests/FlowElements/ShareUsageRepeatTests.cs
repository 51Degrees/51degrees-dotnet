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
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Tests.FlowElements;

/// <summary>
/// The session id and the sequence take no part in deciding whether a call
/// repeats an earlier one, and are always written to the record. Three
/// calls are made in one session through the Sequence Element and the usage
/// sharing element with its own tracker. The first starts the session, the
/// second repeats the first one's values, and the third changes one value.
/// Only the first and the third are recorded, with the same session id and
/// the sequences 1 and 3.
/// </summary>
[TestClass]
public class ShareUsageRepeatTests
{
    private const string UserAgentKey = "header.user-agent";
    private const string UserAgent = "Mozilla/5.0 (repeat test)";
    private const string WidthKey = "query.51D_ScreenPixelsWidth";
    private const string FirstWidth = "1024";
    private const string ChangedWidth = "1280";

    private readonly LoggerFactory _loggerFactory = new LoggerFactory();

    [TestMethod]
    public void ShareUsage_ThreeCallsInOneSession_RecordsTheFirstAndTheChanged()
    {
        AssertFirstAndChangedRecorded(
            Constants.EVIDENCE_SESSIONID,
            Constants.EVIDENCE_SEQUENCE);
    }

    /// <summary>
    /// Evidence keys are matched whatever their case, so a caller that
    /// gives the two names in other letters gets the same records.
    /// </summary>
    [TestMethod]
    public void ShareUsage_ThreeCallsInOneSession_NamesInOtherLetters()
    {
        AssertFirstAndChangedRecorded("query.Session-Id", "query.Sequence");
    }

    private void AssertFirstAndChangedRecorded(
        string sessionIdKey,
        string sequenceKey)
    {
        var capture = new UsageMessageCapture();
        using var http = new HttpClient(capture);
        var shareUsage = new ShareUsageBuilder(_loggerFactory, http)
            .SetSharePercentage(1)
            .SetMinimumEntriesPerMessage(1)
            .Build();
        var pipeline = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(new SequenceElementBuilder(_loggerFactory).Build())
            .AddFlowElement(shareUsage)
            .Build();
        string sessionId;
        try
        {
            // No session yet, so the Sequence Element starts one at 1.
            sessionId = Process(
                pipeline, sessionIdKey, sequenceKey, null, null, FirstWidth);
            // The same values again, with the sequence handed back, which
            // the Sequence Element moves on to 2.
            Process(
                pipeline, sessionIdKey, sequenceKey, sessionId, 1, FirstWidth);
            // One value changed, at sequence 3.
            Process(
                pipeline, sessionIdKey, sequenceKey, sessionId, 2, ChangedWidth);
        }
        finally
        {
            // Disposing sends every record still queued.
            pipeline.Dispose();
        }

        var xml = string.Concat(capture.Xml);
        var sequences = Regex.Matches(xml, "<Sequence>([0-9]+)</Sequence>")
            .Select(match => match.Groups[1].Value)
            .OrderBy(value => value, System.StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(
            new[] { "1", "3" },
            sequences,
            "The first call and the changed one should be recorded, and " +
            $"the repeat left out. Recorded: {string.Join(", ", sequences)}.");
        Assert.AreEqual(
            2,
            Regex.Matches(
                xml,
                $"<SessionId>{Regex.Escape(sessionId)}</SessionId>").Count,
            "Both records should carry the session id.");
        Assert.Contains($"<![CDATA[{ChangedWidth}]]>", xml,
            "The second record should carry the changed value.");
    }

    /// <summary>
    /// Processes one call and returns the session id it ended with.
    /// </summary>
    private static string Process(
        IPipeline pipeline,
        string sessionIdKey,
        string sequenceKey,
        string sessionId,
        int? sequence,
        string width)
    {
        using var data = pipeline.CreateFlowData();
        data.AddEvidence(UserAgentKey, UserAgent);
        data.AddEvidence(WidthKey, width);
        if (sessionId != null)
        {
            data.AddEvidence(sessionIdKey, sessionId);
        }
        if (sequence.HasValue)
        {
            data.AddEvidence(
                sequenceKey,
                sequence.Value.ToString(CultureInfo.InvariantCulture));
        }
        data.Process();
        return data.GetEvidence()[Constants.EVIDENCE_SESSIONID].ToString();
    }
}
