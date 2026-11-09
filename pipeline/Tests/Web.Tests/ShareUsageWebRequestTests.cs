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
using FiftyOne.Pipeline.Engines.TestHelpers;
using FiftyOne.Pipeline.JsonBuilder.FlowElement;
using FiftyOne.Pipeline.Web.Adapters;
using FiftyOne.Pipeline.Web.Services;
using FiftyOne.Pipeline.Web.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Web.Tests;

/// <summary>
/// The web integration adds the web request to the evidence for the
/// usage sharing element, which accepts no evidence itself, so the
/// client-side endpoint's ETag does not depend on headers such as
/// If-None-Match.
/// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/web-integration.md#populating-evidence">Specification</see>
/// </summary>
[TestClass]
public class ShareUsageWebRequestTests
{
    /// <summary>
    /// The key the web request is added under, written out rather than
    /// read from the package, so the test checks the key the
    /// specification gives.
    /// </summary>
    private const string HttpRequestKey = "server.http-request";

    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Keeps the XML of every usage sharing message sent.
    /// </summary>
    private sealed class Capture : HttpMessageHandler
    {
        public ConcurrentQueue<string> Xml { get; } =
            new ConcurrentQueue<string>();

        public SemaphoreSlim Sent { get; } = new SemaphoreSlim(0);

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
            Sent.Release();
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private readonly LoggerFactory _loggerFactory = new LoggerFactory();
    private Capture _capture;
    private HttpClient _http;
    private IPipeline _pipeline;
    private WebRequestEvidenceService _evidence;

    [TestInitialize]
    public void Init()
    {
        _capture = new Capture();
        _http = new HttpClient(_capture);
        _pipeline = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(new SequenceElementBuilder(_loggerFactory)
                .Build())
            .AddFlowElement(new EmptyEngineBuilder(_loggerFactory)
                .Build())
            .AddFlowElement(new ShareUsageBuilder(_loggerFactory, _http)
                .SetSharePercentage(1)
                .SetMinimumEntriesPerMessage(1)
                .SetBlockedHttpHeader("x-blocked")
                .Build())
            .AddFlowElement(new JsonBuilderElementBuilder(_loggerFactory)
                .Build())
            .Build();
        _evidence = new WebRequestEvidenceService(
            _loggerFactory.CreateLogger<WebRequestEvidenceService>());
    }

    [TestCleanup]
    public void Cleanup()
    {
        _pipeline.Dispose();
        _http.Dispose();
    }

    private static DefaultHttpContext NewContext(
        params (string Name, string Value)[] headers)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/51dpipeline/json";
        context.Request.QueryString =
            new QueryString("?session-id=abc&sequence=1");
        context.Connection.RemoteIpAddress = IPAddress.Parse("1.2.3.4");
        context.Response.Body = new MemoryStream();
        foreach (var (name, value) in headers)
        {
            context.Request.Headers[name] = value;
        }
        return context;
    }

    private IFlowData Process(HttpContext context)
    {
        var data = _pipeline.CreateFlowData();
        _evidence.AddEvidenceFromRequest(data, context.Request);
        data.Process();
        return data;
    }

    /// <summary>
    /// A header no element accepts is not evidence, but the web request
    /// is, and usage sharing still shares the header from it, leaving
    /// out the cookie header, a blocked header and a cookie without the
    /// 51Degrees prefix.
    /// </summary>
    [TestMethod]
    public void WebRequest_UsageSharingSharesAHeaderNoElementAccepts()
    {
        var context = NewContext(
            ("User-Agent", "user-agent-value"),
            ("X-Test", "test-value"),
            ("X-Blocked", "blocked-value"),
            ("Cookie", "51D_ScreenPixelsWidth=1080; other=other-value"));

        using var data = Process(context);

        var evidence = data.GetEvidence().AsDictionary();
        Assert.IsFalse(evidence.ContainsKey("header.X-Test"),
            "No element accepts the header, so it is not evidence.");
        Assert.IsTrue(
            evidence.TryGetValue(
                HttpRequestKey,
                out var request),
            "The web request must be in the evidence.");
        var view = request as IReadOnlyDictionary<string, object>;
        Assert.IsNotNull(view);
        Assert.AreEqual("test-value", view["header.x-test"]);

        Assert.IsTrue(_capture.Sent.Wait(Wait),
            "The usage sharing message was not sent.");
        var xml = string.Concat(_capture.Xml);
        StringAssert.Contains(xml, "test-value");
        StringAssert.Contains(xml, "user-agent-value");
        StringAssert.Contains(xml, "Name=\"51D_ScreenPixelsWidth\"");
        StringAssert.Contains(xml, "<ClientIP>1.2.3.4</ClientIP>");
        Assert.IsFalse(xml.Contains("blocked-value"),
            "A blocked header must not be shared.");
        Assert.IsFalse(xml.Contains("other-value"),
            "The cookie header, and a cookie without the 51Degrees " +
            "prefix, must not be shared.");
        Assert.IsFalse(
            xml.Contains("http-request", StringComparison.OrdinalIgnoreCase),
            "The web request itself must never be shared.");
    }

    /// <summary>
    /// A browser revalidates by sending the ETag back in If-None-Match.
    /// That header changes no result, so the ETag of the revalidation
    /// is the one it sends, and it gets a 304.
    /// </summary>
    [TestMethod]
    public void ClientsideEndpoint_Revalidation_GetsNotModified()
    {
        var service = new ClientsidePropertyService(
            _pipeline, NullLogger<ClientsidePropertyService>.Instance);

        var first = NewContext(
            ("User-Agent", "user-agent-value"),
            ("X-Test", "test-value"));
        using (var data = Process(first))
        {
            service.ServeJson(new ContextAdapter(first), data);
        }
        Assert.AreEqual(200, first.Response.StatusCode);
        var etag = first.Response.Headers.ETag.ToString();
        Assert.IsFalse(string.IsNullOrEmpty(etag),
            "The first response must carry an ETag.");

        var revalidation = NewContext(
            ("User-Agent", "user-agent-value"),
            ("X-Test", "test-value"),
            ("If-None-Match", etag));
        using (var data = Process(revalidation))
        {
            service.ServeJson(new ContextAdapter(revalidation), data);
        }
        Assert.AreEqual(304, revalidation.Response.StatusCode,
            "The revalidation carried the ETag of the same response, " +
            "so it must get a 304.");
    }
}
