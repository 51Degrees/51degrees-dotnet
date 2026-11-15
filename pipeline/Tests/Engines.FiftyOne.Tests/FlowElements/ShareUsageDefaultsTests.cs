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
using System;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Tests.FlowElements;

/// <summary>
/// What the usage sharing element shares when the builder's defaults are
/// kept, or when only share all Evidence, share all query string
/// parameters or track session is changed. Each test sends calls through a
/// Pipeline and reads the records the element sends.
/// </summary>
[TestClass]
public class ShareUsageDefaultsTests
{
    private const string UserAgentKey = "header.user-agent";
    private const string UserAgent = "Mozilla/5.0 (defaults test)";
    private const string SessionCookieKey = "cookie.asp.net_sessionid";

    private readonly LoggerFactory _loggerFactory = new LoggerFactory();

    /// <summary>
    /// Share all query string parameters is on by default, so a parameter
    /// that does not start 51D_ and was never named is shared.
    /// </summary>
    [TestMethod]
    public void ByDefault_EveryQueryStringParameterIsShared()
    {
        var xml = ShareOne(
            builder => builder,
            ("query.utm_campaign", "spring"),
            ("query.51D_ScreenPixelsWidth", "1024"));

        Assert.Contains(
            "<query Name=\"utm_campaign\"><![CDATA[spring]]></query>", xml);
        Assert.Contains("<![CDATA[1024]]>", xml);
    }

    /// <summary>
    /// With share all query string parameters off, only the parameters
    /// that start 51D_ and the ones named are shared. A name given while
    /// every parameter is shared takes effect once that is turned off.
    /// </summary>
    [TestMethod]
    public void ShareAllQueryStringParametersOff_SharesThe51DAndNamedOnes()
    {
        var xml = ShareOne(
            builder => builder
                .SetShareAllQueryStringParameters(true)
                .SetIncludedQueryStringParameter("utm_source")
                .SetShareAllQueryStringParameters(false),
            ("query.utm_source", "newsletter"),
            ("query.utm_campaign", "spring"),
            ("query.51D_ScreenPixelsWidth", "1024"));

        Assert.Contains(
            "<query Name=\"utm_source\"><![CDATA[newsletter]]></query>", xml);
        Assert.DoesNotContain("spring", xml);
        Assert.Contains("<![CDATA[1024]]>", xml);
    }

    /// <summary>
    /// A cookie whose name does not start 51D_ is never shared, even when
    /// all Evidence is, and that includes the session cookie.
    /// </summary>
    [TestMethod]
    public void ShareAllEvidence_SharesNoCookieButThe51DOnes()
    {
        var xml = ShareOne(
            builder => builder.SetShareAllEvidence(true),
            ("cookie.51D_ScreenPixelsWidth", "1024"),
            ("cookie.site_theme", "dark-mode"),
            (SessionCookieKey, "session-a"));

        Assert.Contains("<![CDATA[1024]]>", xml);
        Assert.DoesNotContain("site_theme", xml);
        Assert.DoesNotContain("dark-mode", xml);
        Assert.DoesNotContain("session-a", xml);
    }

    /// <summary>
    /// Track session works the same whether or not all Evidence is shared.
    /// The same values from a second session are shared again, a repeat
    /// within one session is not, and the session cookie is never shared.
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TrackSession_TheSameWhetherOrNotAllEvidenceIsShared(
        bool shareAllEvidence)
    {
        var xml = Share(
            builder => builder
                .SetShareAllEvidence(shareAllEvidence)
                .SetTrackSession(true),
            new[] { (SessionCookieKey, "session-a") },
            new[] { (SessionCookieKey, "session-a") },
            new[] { (SessionCookieKey, "session-b") });

        var records = Regex.Matches(xml, "<Device>").Count;
        Assert.AreEqual(2, records,
            "The first call of each session should be shared, and the " +
            "repeat in the first session left out.");
        Assert.DoesNotContain("session-a", xml);
        Assert.DoesNotContain("session-b", xml);
    }

    /// <summary>
    /// Processes one call and returns what was sent, checking that it was
    /// sent as one record.
    /// </summary>
    private string ShareOne(
        Func<ShareUsageBuilder, ShareUsageBuilderBase<ShareUsageElement>>
            configure,
        params (string Key, string Value)[] evidence)
    {
        var xml = Share(configure, evidence);
        var records = Regex.Matches(xml, "<Device>").Count;
        Assert.AreEqual(1, records, "One record should be sent.");
        return xml;
    }

    /// <summary>
    /// Processes one call for each set of evidence, each with the same
    /// User-Agent, and returns every message the element sent.
    /// </summary>
    private string Share(
        Func<ShareUsageBuilder, ShareUsageBuilderBase<ShareUsageElement>>
            configure,
        params (string Key, string Value)[][] calls)
    {
        var capture = new UsageMessageCapture();
        using var http = new HttpClient(capture);
        var builder = new ShareUsageBuilder(_loggerFactory, http);
        builder.SetSharePercentage(1).SetMinimumEntriesPerMessage(1);
        var pipeline = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(new SequenceElementBuilder(_loggerFactory).Build())
            .AddFlowElement(configure(builder).Build())
            .Build();
        try
        {
            foreach (var evidence in calls)
            {
                using var data = pipeline.CreateFlowData();
                data.AddEvidence(UserAgentKey, UserAgent);
                foreach (var (key, value) in evidence)
                {
                    data.AddEvidence(key, value);
                }
                data.Process();
            }
        }
        finally
        {
            // Disposing sends every record still queued.
            pipeline.Dispose();
        }
        return string.Concat(capture.Xml);
    }
}
