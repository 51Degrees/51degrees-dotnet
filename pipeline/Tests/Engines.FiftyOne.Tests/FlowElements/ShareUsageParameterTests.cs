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

using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Tests.FlowElements;

/// <summary>
/// A query string or form parameter is shared only when its name starts
/// 51D_ or a Flow Element in the Pipeline accepts it, whatever the
/// configuration. Both arrive as 'query.' keys, in the evidence or in the
/// web request. The options that named parameters to share have no effect.
/// See the <see href="https://github.com/51Degrees/specifications/blob/d79bbe3b2aef86273570742f6b660f29bfe72816/pipeline-specification/pipeline-elements/usage-sharing-element.md#shared-evidence">Specification</see>
/// </summary>
[TestClass]
public class ShareUsageParameterTests
{
    private const string HttpRequestKey = "server.http-request";

    /// <summary>
    /// A parameter that a Flow Element of the Pipeline accepts.
    /// </summary>
    private const string UsedKey = "query.used-parameter";

    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Accepts the key it is given and does nothing else, standing in for
    /// an engine that reads a query parameter.
    /// </summary>
    private sealed class AcceptingElement :
        FlowElementBase<IElementData, IElementPropertyMetaData>
    {
        private readonly IEvidenceKeyFilter _filter;

        public AcceptingElement(ILoggerFactory loggerFactory, string key)
            : base(loggerFactory.CreateLogger<
                FlowElementBase<IElementData, IElementPropertyMetaData>>())
        {
            _filter = new EvidenceKeyFilterWhitelist(
                new List<string>() { key },
                StringComparer.OrdinalIgnoreCase);
        }

        public override string ElementDataKey => "accepting";

        public override IEvidenceKeyFilter EvidenceKeyFilter => _filter;

        public override IList<IElementPropertyMetaData> Properties { get; } =
            new List<IElementPropertyMetaData>();

        protected override void ProcessInternal(IFlowData data) { }

        protected override void ManagedResourcesCleanup() { }

        protected override void UnmanagedResourcesCleanup() { }
    }

    private LoggerFactory _loggerFactory;
    private UsageMessageCapture _capture;
    private HttpClient _http;

    [TestInitialize]
    public void Init()
    {
        _loggerFactory = new LoggerFactory();
        _capture = new UsageMessageCapture();
        _http = new HttpClient(_capture);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _http.Dispose();
        _loggerFactory.Dispose();
    }

    /// <summary>
    /// The usage sharing element with the named configuration.
    /// </summary>
    private ShareUsageElement NewShareUsage(string configuration)
    {
        var builder = new ShareUsageBuilder(_loggerFactory, _http);
        builder.SetSharePercentage(1).SetMinimumEntriesPerMessage(1);
        switch (configuration)
        {
            case "ShareAllQueryStringParameters":
                builder.SetShareAllQueryStringParameters(true);
                break;
            case "IncludedQueryStringParameters":
                builder.SetShareAllQueryStringParameters(false);
                builder.SetIncludedQueryStringParameters("password,other");
                break;
            case "ShareAllEvidence":
                builder.SetShareAllEvidence(true);
                break;
        }
        return builder.Build();
    }

    private IPipeline NewPipeline(ShareUsageElement shareUsage) =>
        new PipelineBuilder(_loggerFactory)
            .AddFlowElement(new AcceptingElement(_loggerFactory, UsedKey))
            .AddFlowElement(shareUsage)
            .Build();

    private static IReadOnlyDictionary<string, object> Request(
        Dictionary<string, object> values) =>
        new ReadOnlyDictionary<string, object>(values);

    /// <summary>
    /// Processes one request and returns the message it was shared in.
    /// </summary>
    private string ShareOne(
        string configuration,
        Action<IFlowData> addEvidence)
    {
        var shareUsage = NewShareUsage(configuration);
        using var pipeline = NewPipeline(shareUsage);
        using (var data = pipeline.CreateFlowData())
        {
            addEvidence(data);
            data.Process();
        }
        Assert.IsTrue(shareUsage.SendDataTask.Wait(Wait),
            "The usage sharing message was not sent.");
        return string.Concat(_capture.Xml);
    }

    private static void AssertOnlyAllowedParameters(string xml)
    {
        Assert.Contains(
            "<query Name=\"51D_ScreenPixelsWidth\"><![CDATA[1080]]></query>",
            xml);
        Assert.Contains(
            "<query Name=\"used-parameter\"><![CDATA[used-value]]></query>",
            xml);
        Assert.DoesNotContain("password-value", xml,
            "A parameter no Flow Element accepts must not be shared.");
        Assert.DoesNotContain("other-value", xml,
            "A parameter no Flow Element accepts must not be shared.");
        Assert.Contains("header-value", xml,
            "Headers are still shared.");
    }

    /// <summary>
    /// Parameters in the web request, which carries the query string of a
    /// GET and the form of a POST as 'query.' keys.
    /// </summary>
    [TestMethod]
    [DataRow("Default")]
    [DataRow("ShareAllQueryStringParameters")]
    [DataRow("IncludedQueryStringParameters")]
    [DataRow("ShareAllEvidence")]
    public void Parameters_InTheWebRequest_OnlyAllowedAreShared(
        string configuration)
    {
        var xml = ShareOne(configuration, data => data.AddEvidence(
            HttpRequestKey,
            Request(new Dictionary<string, object>()
            {
                { "query.51D_ScreenPixelsWidth", "1080" },
                { UsedKey, "used-value" },
                { "query.password", "password-value" },
                { "query.other", "other-value" },
                { "header.x-test", "header-value" },
            })));

        AssertOnlyAllowedParameters(xml);
    }

    /// <summary>
    /// Parameters added to the evidence directly.
    /// </summary>
    [TestMethod]
    [DataRow("Default")]
    [DataRow("ShareAllQueryStringParameters")]
    [DataRow("IncludedQueryStringParameters")]
    [DataRow("ShareAllEvidence")]
    public void Parameters_InTheEvidence_OnlyAllowedAreShared(
        string configuration)
    {
        var xml = ShareOne(configuration, data =>
        {
            data.AddEvidence("query.51D_ScreenPixelsWidth", "1080");
            data.AddEvidence(UsedKey, "used-value");
            data.AddEvidence("query.password", "password-value");
            data.AddEvidence("query.other", "other-value");
            data.AddEvidence("header.x-test", "header-value");
        });

        AssertOnlyAllowedParameters(xml);
    }

    /// <summary>
    /// A parameter that is not shared takes no part in telling one request
    /// from another, so two requests that differ only in it are one repeat.
    /// </summary>
    [TestMethod]
    public void Parameters_NotShared_DoNotMakeARequestNew()
    {
        var shareUsage = NewShareUsage("Default");
        using var pipeline = NewPipeline(shareUsage);

        void Share(string password)
        {
            using var data = pipeline.CreateFlowData();
            data.AddEvidence(
                HttpRequestKey,
                Request(new Dictionary<string, object>()
                {
                    { "header.x-test", "header-value" },
                    { "query.password", password },
                }));
            data.Process();
            shareUsage.SendDataTask?.Wait(Wait);
        }

        Share("first");
        Share("second");

        var xml = string.Concat(_capture.Xml);
        Assert.AreEqual(1, xml.Split("<Device>").Length - 1,
            "Requests that differ only in a parameter that is not " +
            "shared should be shared once.");
    }

    /// <summary>
    /// Shares one request carrying a session id and a sequence, through a
    /// Pipeline with or without the Sequence Element, and returns the
    /// message.
    /// </summary>
    private string ShareSession(bool withSequenceElement)
    {
        var shareUsage = NewShareUsage("Default");
        var builder = new PipelineBuilder(_loggerFactory);
        if (withSequenceElement)
        {
            builder.AddFlowElement(
                new SequenceElementBuilder(_loggerFactory).Build());
        }
        using var pipeline = builder
            .AddFlowElement(new AcceptingElement(_loggerFactory, UsedKey))
            .AddFlowElement(shareUsage)
            .Build();
        using (var data = pipeline.CreateFlowData())
        {
            data.AddEvidence("query.session-id", "session-value");
            data.AddEvidence("query.sequence", "7");
            data.AddEvidence("header.x-test", "header-value");
            data.Process();
        }
        Assert.IsTrue(shareUsage.SendDataTask.Wait(Wait),
            "The usage sharing message was not sent.");
        return string.Concat(_capture.Xml);
    }

    /// <summary>
    /// With no Sequence Element no Flow Element uses the session id or the
    /// sequence, so neither is written to the record.
    /// </summary>
    [TestMethod]
    public void SessionIdAndSequence_NoSequenceElement_NeitherWritten()
    {
        var xml = ShareSession(false);

        Assert.DoesNotContain("session-value", xml);
        Assert.DoesNotContain("<Sequence>7</Sequence>", xml);
        Assert.Contains("header-value", xml);
    }

    /// <summary>
    /// The Sequence Element uses the session id and the sequence, so both
    /// are written to the record, the sequence as the element moved it on.
    /// </summary>
    [TestMethod]
    public void SessionIdAndSequence_WithSequenceElement_BothWritten()
    {
        var xml = ShareSession(true);

        Assert.Contains("<SessionId>session-value</SessionId>", xml);
        Assert.Contains("<Sequence>8</Sequence>", xml);
    }
}
