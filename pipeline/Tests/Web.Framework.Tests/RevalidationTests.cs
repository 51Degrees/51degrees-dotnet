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
using FiftyOne.Pipeline.Web.Framework.Providers;
using FiftyOne.Pipeline.Web.Shared.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Web;
using System.Web.Hosting;

namespace FiftyOne.Pipeline.Web.Framework.Tests
{
    /// <summary>
    /// The .NET Framework integration answers a request for the JavaScript
    /// or JSON whose If-None-Match matches without processing it, because
    /// the web integration specification's cache header detail says the
    /// endpoint "MUST NOT process a request whose 'If-None-Match' header
    /// matches". The request is read by System.Web from a worker request.
    /// The provider is reached by name, so that a build without the check
    /// fails these tests rather than the build.
    /// </summary>
    [TestClass]
    public class RevalidationTests
    {
        private const string ServeNotModifiedName = "ServeNotModified";
        private const string AnsweredName = "AnsweredWithoutProcessing";
        private const string UserAgentKey = "header.user-agent";
        private const string OtherETag = "W/\"0000000000000000\"";

        /// <summary>
        /// Counts the requests it processes.
        /// </summary>
        private sealed class CountingElement
            : FlowElementBase<IElementData, IElementPropertyMetaData>
        {
            private readonly IEvidenceKeyFilter _filter =
                new EvidenceKeyFilterWhitelist(
                    new List<string>() { UserAgentKey });

            private readonly IList<IElementPropertyMetaData> _properties =
                new List<IElementPropertyMetaData>();

            public CountingElement()
                : base(NullLogger<CountingElement>.Instance)
            {
            }

            public int Processed { get; private set; }

            public override string ElementDataKey => "counting";

            public override IEvidenceKeyFilter EvidenceKeyFilter => _filter;

            public override IList<IElementPropertyMetaData> Properties =>
                _properties;

            protected override void ProcessInternal(IFlowData data)
            {
                Processed++;
            }

            protected override void ManagedResourcesCleanup()
            {
            }

            protected override void UnmanagedResourcesCleanup()
            {
            }
        }

        /// <summary>
        /// A request for the JavaScript, as IIS hands it to System.Web,
        /// with a user agent and the If-None-Match given.
        /// </summary>
        private sealed class WorkerRequest : SimpleWorkerRequest
        {
            private readonly string _ifNoneMatch;

            public WorkerRequest(string ifNoneMatch)
                : base(
                    "/",
                    AppDomain.CurrentDomain.BaseDirectory,
                    "51Degrees.core.js",
                    string.Empty,
                    TextWriter.Null)
            {
                _ifNoneMatch = ifNoneMatch;
            }

            public override string GetKnownRequestHeader(int index)
            {
                switch (index)
                {
                    case HeaderUserAgent:
                        return "Mozilla/5.0";
                    case HeaderIfNoneMatch:
                        return _ifNoneMatch;
                    default:
                        return null;
                }
            }
        }

        private CountingElement _element;
        private IPipeline _pipeline;
        private ClientsidePropertyService _service;

        [TestInitialize]
        public void Init()
        {
            _element = new CountingElement();
            _pipeline = new PipelineBuilder(NullLoggerFactory.Instance)
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
            _service = new ClientsidePropertyService(
                _pipeline,
                NullLogger<ClientsidePropertyService>.Instance);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _pipeline.Dispose();
        }

        /// <summary>
        /// A request whose If-None-Match matches is answered with a 304,
        /// is not processed, and is marked so that the end of the request
        /// does not process it either.
        /// </summary>
        [TestMethod]
        public void MatchingIfNoneMatch_NotModifiedAndNotProcessed()
        {
            var context = new HttpContext(new WorkerRequest("*"));

            Assert.IsTrue(ServeNotModified(context));
            Assert.AreEqual(304, context.Response.StatusCode);
            Assert.AreEqual(0, _element.Processed,
                "A request whose If-None-Match matched was processed.");
            Assert.IsTrue(AnsweredWithoutProcessing(context));
        }

        /// <summary>
        /// A request whose If-None-Match does not match, or that has none,
        /// is left to be processed and served as before.
        /// </summary>
        [DataTestMethod]
        [DataRow(OtherETag)]
        [DataRow(null)]
        public void IfNoneMatchNotMatching_LeftToProcessing(
            string ifNoneMatch)
        {
            var context = new HttpContext(new WorkerRequest(ifNoneMatch));

            Assert.IsFalse(ServeNotModified(context));
            Assert.AreEqual(200, context.Response.StatusCode);
            Assert.AreEqual(0, _element.Processed);
            Assert.IsFalse(AnsweredWithoutProcessing(context));
        }

        private bool ServeNotModified(HttpContext context)
        {
            var method = typeof(FiftyOneJsProvider).GetMethod(
                ServeNotModifiedName,
                BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
            {
                Assert.Fail(
                    "FiftyOneJsProvider has no " + ServeNotModifiedName +
                    ", so a request whose If-None-Match matches is " +
                    "processed.");
            }
            return (bool)method.Invoke(
                null,
                new object[] { context, _pipeline, _service });
        }

        private static bool AnsweredWithoutProcessing(HttpContext context)
        {
            var method = typeof(FiftyOneJsProvider).GetMethod(
                AnsweredName,
                BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
            {
                Assert.Fail("FiftyOneJsProvider has no " + AnsweredName + ".");
            }
            return (bool)method.Invoke(null, new object[] { context });
        }
    }
}
