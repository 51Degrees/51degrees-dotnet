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
using FiftyOne.Pipeline.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Web.Tests;

/// <summary>
/// A parameter in the form body of a POST request reaches usage sharing
/// through the web integration. It is shared when a Flow Element in the
/// Pipeline accepts it or its name starts 51D_, and a password parameter
/// that no Flow Element accepts is never shared, even when all Evidence is
/// shared.
/// See the <see href="https://github.com/51Degrees/specifications/blob/9d12262654fe8914a5497bdacd85ca733898dd8b/pipeline-specification/automated-testing.md">Specification</see>
/// </summary>
[TestClass]
public class ShareUsageFormParameterTests
{
    /// <summary>
    /// A form parameter that a Flow Element of the Pipeline accepts.
    /// </summary>
    private const string UsedKey = "query.used-form-field";

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

    /// <summary>
    /// Accepts the key it is given and does nothing else, standing in for
    /// an engine that reads a form parameter.
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
    private Capture _capture;
    private HttpClient _http;

    [TestInitialize]
    public void Init()
    {
        _loggerFactory = new LoggerFactory();
        _capture = new Capture();
        _http = new HttpClient(_capture);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _http.Dispose();
        _loggerFactory.Dispose();
    }

    /// <summary>
    /// A POST whose parameters are only in its form body.
    /// </summary>
    private static DefaultHttpContext NewPost(string form)
    {
        var body = Encoding.UTF8.GetBytes(form);
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/form";
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.ContentLength = body.Length;
        context.Request.Body = new MemoryStream(body);
        context.Request.Headers["User-Agent"] = "user-agent-value";
        context.Request.Headers["X-Test"] = "header-value";
        context.Connection.RemoteIpAddress = IPAddress.Parse("1.2.3.4");
        return context;
    }

    /// <summary>
    /// Processes one POST through the web integration and returns the
    /// usage sharing message it was shared in.
    /// </summary>
    private string ShareOne(bool shareAllEvidence)
    {
        var shareUsage = new ShareUsageBuilder(_loggerFactory, _http)
            .SetSharePercentage(1)
            .SetMinimumEntriesPerMessage(1)
            .SetShareAllEvidence(shareAllEvidence)
            .Build();
        using var pipeline = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(new AcceptingElement(_loggerFactory, UsedKey))
            .AddFlowElement(shareUsage)
            .Build();
        var evidence = new WebRequestEvidenceService(
            _loggerFactory.CreateLogger<WebRequestEvidenceService>());
        var context = NewPost(
            "used-form-field=form-used-value" +
            "&password=form-password-value" +
            "&51D_FormValue=form-51d-value");

        using (var data = pipeline.CreateFlowData())
        {
            evidence.AddEvidenceFromRequest(data, context.Request);
            data.Process();
        }
        Assert.IsTrue(_capture.Sent.Wait(Wait),
            "The usage sharing message was not sent.");
        return string.Concat(_capture.Xml);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FormParameter_UsedByThePipeline_IsShared(
        bool shareAllEvidence)
    {
        var xml = ShareOne(shareAllEvidence);

        StringAssert.Contains(xml, "form-used-value",
            "A form parameter a Flow Element accepts must be shared.");
        StringAssert.Contains(xml, "form-51d-value",
            "A form parameter starting 51D_ must be shared.");
        StringAssert.Contains(xml, "header-value",
            "Headers are still shared.");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FormParameter_NotUsedByThePipeline_IsNotShared(
        bool shareAllEvidence)
    {
        var xml = ShareOne(shareAllEvidence);

        Assert.IsFalse(xml.Contains("form-password-value"),
            "A form parameter no Flow Element accepts must not be shared.");
    }
}
