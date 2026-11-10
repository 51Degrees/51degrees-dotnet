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
using FiftyOne.Pipeline.Engines.TestHelpers;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Tests.FlowElements;

/// <summary>
/// The usage sharing element accepts no evidence, and shares what it finds
/// in the evidence and in the web request.
/// See the <see href="https://github.com/51Degrees/specifications/blob/5a250650766c025c42aa9d7943bd6781f4447646/pipeline-specification/pipeline-elements/usage-sharing-element.md#accepted-evidence">Specification</see>
/// </summary>
[TestClass]
public class ShareUsageAcceptsNoEvidenceTests
{
    /// <summary>
    /// The key the web request is added under, written out rather than
    /// read from the package, so the test checks the key the
    /// specification gives.
    /// </summary>
    private const string HttpRequestKey = "server.http-request";

    private const string BlockedHeader = "x-blocked";

    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Keeps the XML of every message sent.
    /// </summary>
    private sealed class Capture : HttpMessageHandler
    {
        public ConcurrentQueue<string> Xml { get; } =
            new ConcurrentQueue<string>();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync(
                cancellationToken);
            using var gzip = new GZipStream(
                new MemoryStream(bytes), CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            Xml.Enqueue(await reader.ReadToEndAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private readonly LoggerFactory _loggerFactory = new LoggerFactory();
    private Capture _capture;
    private HttpClient _http;

    [TestInitialize]
    public void Init()
    {
        _capture = new Capture();
        _http = new HttpClient(_capture);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _http.Dispose();
    }

    private ShareUsageElement NewShareUsage() =>
        new ShareUsageBuilder(_loggerFactory, _http)
            .SetSharePercentage(1)
            .SetMinimumEntriesPerMessage(1)
            .SetBlockedHttpHeader(BlockedHeader)
            .Build();

    private IPipeline NewPipeline(ShareUsageElement shareUsage) =>
        new PipelineBuilder(_loggerFactory)
            .AddFlowElement(new EmptyEngineBuilder(_loggerFactory).Build())
            .AddFlowElement(shareUsage)
            .Build();

    /// <summary>
    /// A pipeline holding the usage sharing element accepts only what its
    /// other elements accept, so a revalidation's If-None-Match or any
    /// other header no element needs takes no part in a cache key or an
    /// ETag.
    /// </summary>
    [TestMethod]
    public void ShareUsage_Pipeline_AcceptsOnlyWhatOtherElementsAccept()
    {
        var shareUsage = NewShareUsage();
        using var pipeline = NewPipeline(shareUsage);

        Assert.IsFalse(
            pipeline.EvidenceKeyFilter.Include("header.if-none-match"));
        Assert.IsFalse(pipeline.EvidenceKeyFilter.Include("header.x-test"));
        Assert.IsTrue(pipeline.EvidenceKeyFilter.Include("test.value"),
            "The test engine's evidence must still be accepted.");
        Assert.IsFalse(
            shareUsage.EvidenceKeyFilter.Include("header.user-agent"));
        Assert.IsFalse(shareUsage.EvidenceKeyFilter.Include(
            HttpRequestKey));
    }

    /// <summary>
    /// With the web request in the evidence, a header no element accepts
    /// is still shared, the cookie header, a blocked header and a cookie
    /// without the 51Degrees prefix are not, a key in both places is
    /// shared once with the evidence value, and the web request itself is
    /// never shared.
    /// </summary>
    [TestMethod]
    public void ShareUsage_WebRequest_SharesWhatItFinds()
    {
        var shareUsage = NewShareUsage();
        using var pipeline = NewPipeline(shareUsage);
        using var data = pipeline.CreateFlowData();
        data.AddEvidence("header.user-agent", "from-evidence");
        data.AddEvidence(
            HttpRequestKey,
            new ReadOnlyDictionary<string, object>(
                new Dictionary<string, object>()
                {
                    { "header.user-agent", "from-request" },
                    { "header.x-test", "test-value" },
                    { "header.cookie", "51D_ScreenPixelsWidth=1080; other=other-value" },
                    { "header." + BlockedHeader, "blocked-value" },
                    { "cookie.51D_ScreenPixelsWidth", "1080" },
                    { "cookie.other", "other-value" },
                    { Core.Constants.EVIDENCE_CLIENTIP_KEY, "1.2.3.4" },
                }));

        data.Process();
        Assert.IsTrue(shareUsage.SendDataTask.Wait(Wait),
            "The usage sharing message was not sent.");

        var xml = string.Concat(_capture.Xml);
        Assert.Contains(
            "<header Name=\"x-test\"><![CDATA[test-value]]></header>", xml);
        Assert.Contains(
            "<header Name=\"user-agent\"><![CDATA[from-evidence]]></header>",
            xml);
        Assert.Contains(
            "<cookie Name=\"51D_ScreenPixelsWidth\"><![CDATA[1080]]></cookie>",
            xml);
        Assert.Contains("<ClientIP>1.2.3.4</ClientIP>", xml);
        Assert.DoesNotContain("from-request", xml);
        Assert.DoesNotContain("other-value", xml);
        Assert.DoesNotContain("blocked-value", xml);
        Assert.IsFalse(
            xml.Contains("http-request", StringComparison.OrdinalIgnoreCase),
            "The web request itself must never be shared.");
    }

    /// <summary>
    /// The default tracker de-duplicates on the values that would be
    /// shared, which include the web request's, so a second request with
    /// the same values is not shared again and one with a different header
    /// is.
    /// </summary>
    [TestMethod]
    public void ShareUsage_WebRequest_TracksTheValuesItShares()
    {
        var shareUsage = NewShareUsage();
        using var pipeline = NewPipeline(shareUsage);

        void Share(string value)
        {
            using var data = pipeline.CreateFlowData();
            data.AddEvidence(
                HttpRequestKey,
                new ReadOnlyDictionary<string, object>(
                    new Dictionary<string, object>()
                    {
                        { "header.x-test", value },
                    }));
            data.Process();
            shareUsage.SendDataTask?.Wait(Wait);
        }

        Share("first");
        Share("first");
        Share("second");

        var xml = string.Concat(_capture.Xml);
        Assert.AreEqual(2, xml.Split("<Device>").Length - 1,
            "Two different requests should be shared, once each.");
        Assert.Contains("[first]", xml);
        Assert.Contains("[second]", xml);
    }
}
