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
using FiftyOne.Pipeline.JavaScriptBuilder.FlowElement;
using FiftyOne.Pipeline.JsonBuilder.FlowElement;
using FiftyOne.Pipeline.Web.Services;
using FiftyOne.Pipeline.Web.Shared;
using FiftyOne.Pipeline.Web.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Web.Tests;

/// <summary>
/// A request for the JavaScript or JSON whose If-None-Match matches the
/// ETag of the evidence it supplies is answered without processing it,
/// because the web integration specification's cache header detail says
/// the endpoint "MUST NOT process a request whose 'If-None-Match' header
/// matches" (RFC 9110 sections 13.1.2 and 13.2.1). These go through the
/// middleware with the built-in services and count how many requests an
/// element processes.
/// </summary>
[TestClass]
public class ClientsideRevalidationTests
{
    private const string UserAgentKey = "header.user-agent";
    private const string UserAgent = "Mozilla/5.0";
    private const string JavaScriptPath =
        PipelineWebIntegrationOptions.DefaultJavaScriptPath;
    private const string JsonPath =
        PipelineWebIntegrationOptions.DefaultJsonPath;
    private const string Cached = "private,max-age=1800";
    private const string NotCached = "private, no-cache";
    private const string OtherETag = "W/\"0000000000000000\"";

    /// <summary>
    /// Counts the requests it processes, and fails when told to.
    /// </summary>
    private sealed class CountingElement
        : FlowElementBase<IElementData, IElementPropertyMetaData>
    {
        public CountingElement()
            : base(NullLogger<CountingElement>.Instance)
        {
        }

        public int Processed { get; private set; }

        public bool Fail { get; set; }

        public override string ElementDataKey => "counting";

        public override IEvidenceKeyFilter EvidenceKeyFilter { get; } =
            new EvidenceKeyFilterWhitelist(
                new List<string>() { UserAgentKey });

        public override IList<IElementPropertyMetaData> Properties { get; } =
            new List<IElementPropertyMetaData>();

        protected override void ProcessInternal(IFlowData data)
        {
            Processed++;
            if (Fail)
            {
                throw new InvalidOperationException("The element failed.");
            }
        }

        protected override void ManagedResourcesCleanup()
        {
        }

        protected override void UnmanagedResourcesCleanup()
        {
        }
    }

    private CountingElement _element;
    private IPipeline _pipeline;
    private HttpContextAccessor _accessor;
    private FiftyOneMiddleware _middleware;
    private int _nextCalls;

    [TestInitialize]
    public void Init()
    {
        _element = new CountingElement();
        _pipeline = new PipelineBuilder(NullLoggerFactory.Instance)
            .SetSuppressProcessExceptions(true)
            .AddFlowElement(_element)
            .AddFlowElement(
                new SequenceElementBuilder(NullLoggerFactory.Instance)
                    .Build())
            .AddFlowElement(
                new JsonBuilderElementBuilder(NullLoggerFactory.Instance)
                    .Build())
            .AddFlowElement(
                new JavaScriptBuilderElementBuilder(
                    NullLoggerFactory.Instance).Build())
            .Build();
        _accessor = new HttpContextAccessor();
        var flowDataProvider = new FlowDataProvider(_accessor);
        var jsService = new FiftyOneJSService(
            new ClientsidePropertyService(
                _pipeline,
                NullLogger<ClientsidePropertyService>.Instance),
            Options.Create(new PipelineWebIntegrationOptions()),
            flowDataProvider);
        var resultService = new PipelineResultService(
            new WebRequestEvidenceService(
                NullLogger<WebRequestEvidenceService>.Instance),
            _pipeline);
        _nextCalls = 0;
        _middleware = new FiftyOneMiddleware(
            context =>
            {
                _nextCalls++;
                return Task.CompletedTask;
            },
            resultService,
            jsService,
            flowDataProvider,
            new Mock<ISetHeadersService>().Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _pipeline.Dispose();
    }

    /// <summary>
    /// A GET or HEAD whose If-None-Match matches gets a 304 with the
    /// caching headers of the 200, and the request is not processed.
    /// </summary>
    [DataTestMethod]
    [DataRow(JavaScriptPath, "GET")]
    [DataRow(JavaScriptPath, "HEAD")]
    [DataRow(JsonPath, "GET")]
    public void MatchingIfNoneMatch_NotModifiedAndNotProcessed(
        string path, string method)
    {
        var ok = Send(path, "GET");
        var eTag = Header(ok, "ETag");
        var processed = _element.Processed;

        var context = Send(path, method, eTag);

        Assert.AreEqual(200, ok.Response.StatusCode);
        Assert.AreEqual(1, processed);
        Assert.AreEqual(304, context.Response.StatusCode);
        Assert.AreEqual(processed, _element.Processed,
            "A request whose If-None-Match matched was processed.");
        Assert.AreEqual(Cached, Header(context, "Cache-Control"));
        Assert.AreEqual(eTag, Header(context, "ETag"));
        Assert.AreEqual(Header(ok, "Vary"), Header(context, "Vary"));
        Assert.AreEqual(0, _nextCalls);
    }

    /// <summary>
    /// A POST whose If-None-Match matches gets a 412 (RFC 9110 section
    /// 13.1.2), and the request is not processed.
    /// </summary>
    [TestMethod]
    public void MatchingIfNoneMatch_Post_PreconditionFailedAndNotProcessed()
    {
        var eTag = Header(Send(JsonPath, "POST"), "ETag");
        var processed = _element.Processed;

        var context = Send(JsonPath, "POST", eTag);

        Assert.AreEqual(412, context.Response.StatusCode);
        Assert.AreEqual(processed, _element.Processed,
            "A request whose If-None-Match matched was processed.");
    }

    /// <summary>
    /// A request whose If-None-Match does not match is processed and gets
    /// the content.
    /// </summary>
    [TestMethod]
    public void IfNoneMatchNotMatching_Processed()
    {
        var context = Send(JavaScriptPath, "GET", OtherETag);

        Assert.AreEqual(200, context.Response.StatusCode);
        Assert.AreEqual(1, _element.Processed);
        Assert.AreNotEqual(string.Empty, Header(context, "ETag"));
    }

    /// <summary>
    /// While processing fails, a request whose If-None-Match matches still
    /// gets a 304 carrying the ETag and max-age, so a browser holding a
    /// good response keeps it, and a request without one gets a response
    /// that is not cached.
    /// </summary>
    [TestMethod]
    public void ProcessingFailing_MatchingIfNoneMatch_KeepsETagAndMaxAge()
    {
        var eTag = Header(Send(JavaScriptPath, "GET"), "ETag");
        _element.Fail = true;

        var matched = Send(JavaScriptPath, "GET", eTag);
        var processed = _element.Processed;
        var failed = Send(JavaScriptPath, "GET");

        Assert.AreEqual(304, matched.Response.StatusCode);
        Assert.AreEqual(Cached, Header(matched, "Cache-Control"));
        Assert.AreEqual(eTag, Header(matched, "ETag"));
        Assert.AreEqual(1, processed,
            "A request whose If-None-Match matched was processed.");
        Assert.AreEqual(200, failed.Response.StatusCode);
        Assert.AreEqual(NotCached, Header(failed, "Cache-Control"));
        Assert.AreEqual(2, _element.Processed);
    }

    /// <summary>
    /// Any other page is processed, with or without If-None-Match, and
    /// passed on to the next middleware.
    /// </summary>
    [TestMethod]
    public void OtherPage_ProcessedAndPassedOn()
    {
        Send("/page", "GET", OtherETag);

        Assert.AreEqual(1, _element.Processed);
        Assert.AreEqual(1, _nextCalls);
    }

    private HttpContext Send(
        string path, string method, string ifNoneMatch = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Headers["User-Agent"] = UserAgent;
        if (ifNoneMatch != null)
        {
            context.Request.Headers["If-None-Match"] = ifNoneMatch;
        }
        context.Response.Body = new MemoryStream();
        _accessor.HttpContext = context;
        _middleware.Invoke(context).GetAwaiter().GetResult();
        return context;
    }

    private static string Header(HttpContext context, string name)
    {
        return context.Response.Headers[name].ToString();
    }
}
