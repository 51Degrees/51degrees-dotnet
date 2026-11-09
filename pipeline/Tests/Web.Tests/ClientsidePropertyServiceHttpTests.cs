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
using FiftyOne.Pipeline.JavaScriptBuilder.Data;
using FiftyOne.Pipeline.JavaScriptBuilder.FlowElement;
using FiftyOne.Pipeline.JsonBuilder.Data;
using FiftyOne.Pipeline.JsonBuilder.FlowElement;
using FiftyOne.Pipeline.Web.Adapters;
using FiftyOne.Pipeline.Web.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;

namespace FiftyOne.Pipeline.Web.Tests
{
    /// <summary>
    /// Tests of the caching headers and conditional requests of the
    /// client-side JavaScript and JSON endpoints, through the ASP.NET Core
    /// adapter, against the web integration specification's cache header
    /// detail and RFC 9110 sections 13.1.2 and 15.4.5.
    /// </summary>
    [TestClass]
    public class ClientsidePropertyServiceHttpTests
    {
        private const string USER_AGENT_KEY = "header.User-Agent";
        private const string USER_AGENT = "Mozilla/5.0";
        private const string WIDTH_KEY = "cookie.51D_ScreenPixelsWidth";
        private const string WIDTH = "1920";

        /// <summary>
        /// The ETag of the evidence above, worked out from the definition
        /// of the hash rather than by running it. A process that seeds its
        /// hash at random cannot give it.
        /// </summary>
        private const string EXPECTED_ETAG = "W/\"b083455b4a776eb1\"";

        private const string JS_CONTENT = "JAVASCRIPT CONTENT";
        private const string JSON_CONTENT = "JSON CONTENT";
        private const string CACHED = "private,max-age=1800";
        private const string NOT_CACHED = "private, no-cache";
        private const string ORIGIN = "https://example.com";

        private ClientsidePropertyService _service;
        private Mock<IPipeline> _pipeline;
        private Mock<IFlowData> _flowData;
        private JavaScriptBuilderElement _jsElement;
        private JsonBuilderElement _jsonElement;
        private Dictionary<string, object> _evidence;
        private List<IFlowError> _errors;

        [TestInitialize]
        public void Init()
        {
            _evidence = new Dictionary<string, object>(
                StringComparer.OrdinalIgnoreCase)
            {
                { USER_AGENT_KEY, USER_AGENT },
                { WIDTH_KEY, WIDTH },
            };
            _errors = new List<IFlowError>();

            var filter = new EvidenceKeyFilterWhitelist(
                new List<string>() { USER_AGENT_KEY, WIDTH_KEY });
            var element = new Mock<IFlowElement>();
            element.Setup(e => e.EvidenceKeyFilter).Returns(filter);

            _jsElement = new JavaScriptBuilderElementBuilder(
                NullLoggerFactory.Instance).Build();
            _jsonElement = new JsonBuilderElementBuilder(
                NullLoggerFactory.Instance).Build();

            _pipeline = new Mock<IPipeline>();
            _pipeline.Setup(p => p.FlowElements).Returns(
                new List<IFlowElement>() { element.Object });
            _pipeline.Setup(p => p.EvidenceKeyFilter).Returns(filter);
            _pipeline.Setup(p => p.GetElement<JavaScriptBuilderElement>())
                .Returns(_jsElement);
            _pipeline.Setup(p => p.GetElement<JsonBuilderElement>())
                .Returns(_jsonElement);

            var jsData = new Mock<IJavaScriptBuilderElementData>();
            jsData.Setup(d => d.JavaScript).Returns(JS_CONTENT);
            var jsonData = new Mock<IJsonBuilderElementData>();
            jsonData.Setup(d => d.Json).Returns(JSON_CONTENT);
            var evidence = new Mock<IEvidence>();
            evidence.Setup(e => e.AsDictionary()).Returns(() => _evidence);

            _flowData = new Mock<IFlowData>();
            _flowData.Setup(d => d.Pipeline).Returns(_pipeline.Object);
            _flowData.Setup(d => d.Errors).Returns(_errors);
            _flowData.Setup(d => d.GetEvidence()).Returns(evidence.Object);
            // A real flow data's key is made from the same evidence.
            _flowData.Setup(d => d.GenerateKey(
                It.IsAny<IEvidenceKeyFilter>())).Returns(() =>
                {
                    var builder = new DataKeyBuilder();
                    foreach (var entry in _evidence)
                    {
                        builder.Add(100, entry.Key, entry.Value);
                    }
                    return builder.Build();
                });
            _flowData.Setup(d => d.GetFromElement(_jsElement))
                .Returns(jsData.Object);
            _flowData.Setup(d => d.GetFromElement(_jsonElement))
                .Returns(jsonData.Object);

            _service = new ClientsidePropertyService(
                _pipeline.Object,
                NullLogger<ClientsidePropertyService>.Instance);
        }

        /// <summary>
        /// The same evidence gives the same ETag in every process. The
        /// expected value is fixed, so a hash seeded afresh in each process,
        /// as string.GetHashCode is, fails.
        /// </summary>
        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void Get_ETagIsTheSameInEveryProcess(bool javaScript)
        {
            var context = Serve(javaScript, "GET");

            Assert.AreEqual(200, context.Response.StatusCode);
            Assert.AreEqual(EXPECTED_ETAG, Header(context, "ETag"));
            Assert.AreEqual(CACHED, Header(context, "Cache-Control"));
        }

        /// <summary>
        /// The ETag does not depend on the order the evidence was added in,
        /// or on the case of its names, which a request can send in any
        /// order and case.
        /// </summary>
        [TestMethod]
        public void Get_ETagIgnoresEvidenceOrderAndNameCase()
        {
            _evidence = new Dictionary<string, object>(
                StringComparer.OrdinalIgnoreCase)
            {
                { WIDTH_KEY.ToUpperInvariant(), WIDTH },
                { USER_AGENT_KEY.ToLowerInvariant(), USER_AGENT },
            };

            var context = Serve(true, "GET");

            Assert.AreEqual(EXPECTED_ETAG, Header(context, "ETag"));
        }

        /// <summary>
        /// A response to a request whose processing recorded errors carries
        /// 'Cache-Control: private, no-cache' and no ETag.
        /// </summary>
        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void ProcessingErrors_NotCachedAndNoETag(bool javaScript)
        {
            AddError();

            var context = Serve(javaScript, "GET");

            Assert.AreEqual(200, context.Response.StatusCode);
            Assert.AreEqual(NOT_CACHED, Header(context, "Cache-Control"));
            Assert.IsFalse(context.Response.Headers.ContainsKey("ETag"),
                $"A failed response carried the ETag " +
                $"'{Header(context, "ETag")}'.");
            Assert.AreEqual(javaScript ? JS_CONTENT : JSON_CONTENT,
                Body(context));
        }

        /// <summary>
        /// A request whose If-None-Match matches gets its 304 while
        /// processing is failing, so a browser keeps the good response it
        /// holds. The 304 carries the headers the failed 200 would, so
        /// 'private, no-cache' and no ETag.
        /// </summary>
        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void ProcessingErrors_MatchingIfNoneMatch_NotModified(
            bool javaScript)
        {
            var eTag = Header(Serve(javaScript, "GET"), "ETag");
            AddError();

            var context = Serve(javaScript, "GET", eTag);

            Assert.AreEqual(304, context.Response.StatusCode);
            Assert.AreEqual(NOT_CACHED, Header(context, "Cache-Control"));
            Assert.IsFalse(context.Response.Headers.ContainsKey("ETag"));
            Assert.AreEqual("User-Agent", Header(context, "Vary"));
            Assert.AreEqual(string.Empty, Body(context));
        }

        /// <summary>
        /// A builder that left no data makes the 304 not cached, as it makes
        /// the 200 not cached.
        /// </summary>
        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void BuilderLeftNoData_MatchingIfNoneMatch_NotCached(
            bool javaScript)
        {
            var eTag = Header(Serve(javaScript, "GET"), "ETag");
            _flowData.Setup(d => d.GetFromElement(_jsElement))
                .Throws(new KeyNotFoundException());
            _flowData.Setup(d => d.GetFromElement(_jsonElement))
                .Throws(new KeyNotFoundException());

            var context = Serve(javaScript, "GET", eTag);

            Assert.AreEqual(304, context.Response.StatusCode);
            Assert.AreEqual(NOT_CACHED, Header(context, "Cache-Control"));
            Assert.IsFalse(context.Response.Headers.ContainsKey("ETag"));
        }

        /// <summary>
        /// A GET or HEAD whose If-None-Match matches gets a 304 with no
        /// content, carrying the Cache-Control, Vary and ETag headers the
        /// 200 would carry (RFC 9110 section 15.4.5), and the CORS header,
        /// but no Content-Type or Content-Length.
        /// </summary>
        [DataTestMethod]
        [DataRow(true, "GET")]
        [DataRow(false, "GET")]
        [DataRow(true, "HEAD")]
        [DataRow(false, "HEAD")]
        public void MatchingIfNoneMatch_NotModifiedWithCachingHeaders(
            bool javaScript, string method)
        {
            var ok = Serve(javaScript, "GET", origin: ORIGIN);
            var eTag = Header(ok, "ETag");

            var context = Serve(javaScript, method, eTag, ORIGIN);

            Assert.AreEqual(304, context.Response.StatusCode);
            Assert.AreEqual(Header(ok, "Cache-Control"),
                Header(context, "Cache-Control"));
            Assert.AreEqual(Header(ok, "Vary"), Header(context, "Vary"));
            Assert.AreEqual(eTag, Header(context, "ETag"));
            Assert.AreEqual(ORIGIN,
                Header(context, "Access-Control-Allow-Origin"));
            Assert.IsFalse(
                context.Response.Headers.ContainsKey("Content-Type"));
            Assert.IsFalse(
                context.Response.Headers.ContainsKey("Content-Length"));
            Assert.AreEqual(string.Empty, Body(context));
        }

        /// <summary>
        /// A method other than GET or HEAD whose If-None-Match matches gets
        /// a 412 (RFC 9110 section 13.1.2), because a response to a POST is
        /// never a 304 (section 9.3.3). It has no content and carries only
        /// the CORS header, so a script on another origin can read it.
        /// </summary>
        [DataTestMethod]
        [DataRow(true, "POST")]
        [DataRow(false, "POST")]
        [DataRow(false, "PUT")]
        public void MatchingIfNoneMatch_OtherMethod_PreconditionFailed(
            bool javaScript, string method)
        {
            var eTag = Header(Serve(javaScript, "GET"), "ETag");

            var context = Serve(javaScript, method, eTag, ORIGIN);

            Assert.AreEqual(412, context.Response.StatusCode);
            Assert.AreEqual(string.Empty, Body(context));
            Assert.AreEqual(ORIGIN,
                Header(context, "Access-Control-Allow-Origin"));
            Assert.AreEqual(1, context.Response.Headers.Count,
                "The 412 carried headers other than the CORS header: " +
                string.Join(", ", context.Response.Headers.Keys));
        }

        /// <summary>
        /// If-None-Match matches when it is "*", or a list holding the ETag,
        /// compared weakly so the W/ prefix makes no difference (RFC 9110
        /// sections 8.8.3.2 and 13.1.2).
        /// </summary>
        [DataTestMethod]
        [DataRow("{0}")]
        [DataRow("\"other\", {0}")]
        [DataRow("{0},W/\"other\"")]
        [DataRow("*")]
        [DataRow("STRONG")]
        public void IfNoneMatch_Forms_NotModified(string format)
        {
            var eTag = Header(Serve(true, "GET"), "ETag");
            var value = format == "STRONG" ?
                eTag.Substring("W/".Length) :
                string.Format(format, eTag);

            var context = Serve(true, "GET", value);

            Assert.AreEqual(304, context.Response.StatusCode,
                $"'{value}' did not match the ETag '{eTag}'.");
        }

        /// <summary>
        /// A list separated by optional whitespace, tabs included, matches
        /// (RFC 9110 section 5.6.1).
        /// </summary>
        [TestMethod]
        public void IfNoneMatch_ListWithTab_NotModified()
        {
            var eTag = Header(Serve(true, "GET"), "ETag");
            var value = "\"other\",	" + eTag;

            var context = Serve(true, "GET", value);

            Assert.AreEqual(304, context.Response.StatusCode,
                $"'{value}' did not match the ETag '{eTag}'.");
        }

        /// <summary>
        /// A field value that is neither "*" nor a comma separated list of
        /// entity tags is not a valid If-None-Match, so the condition is
        /// true and the response is the full 200 (RFC 9110 section 13.1.2
        /// and section 13.2.2 step 3). A valid list of other tags, and an
        /// empty value, also get the 200.
        /// </summary>
        [DataTestMethod]
        [DataRow("{0}, other", DisplayName = "tag then a non-tag")]
        [DataRow("\"other\" {0}", DisplayName = "two tags, no comma")]
        [DataRow("other, {0}", DisplayName = "non-tag then the tag")]
        [DataRow("\"oth er\", {0}", DisplayName = "space inside quotes")]
        [DataRow("\"other\", *", DisplayName = "star in a list")]
        [DataRow("w/{0}", DisplayName = "lower case weak prefix")]
        [DataRow("UNQUOTED", DisplayName = "unquoted tag")]
        [DataRow("W/\"other\"", DisplayName = "other tag")]
        [DataRow("\"other\", W/\"another\"", DisplayName = "other tags")]
        [DataRow("", DisplayName = "empty value")]
        public void IfNoneMatch_NotMatchingOrInvalid_Ok(string format)
        {
            var eTag = Header(Serve(true, "GET"), "ETag");
            var value = format == "UNQUOTED" ?
                eTag.Substring("W/".Length).Trim('"') :
                format.Replace("{0}", eTag);

            var context = Serve(true, "GET", value);

            Assert.AreEqual(200, context.Response.StatusCode,
                $"'{value}' gave a response other than 200.");
            Assert.AreEqual(JS_CONTENT, Body(context));
        }

        /// <summary>
        /// An If-None-Match that holds a different ETag gets the full
        /// response.
        /// </summary>
        [TestMethod]
        public void IfNoneMatch_Different_Ok()
        {
            var context = Serve(true, "GET", "W/\"0000000000000000\"");

            Assert.AreEqual(200, context.Response.StatusCode);
            Assert.AreEqual(JS_CONTENT, Body(context));
        }

        private void AddError()
        {
            _errors.Add(new FlowError(
                new Exception("Processing failed."),
                new Mock<IFlowElement>().Object,
                false));
        }

        private HttpContext Serve(
            bool javaScript,
            string method,
            string ifNoneMatch = null,
            string origin = null)
        {
            var context = new DefaultHttpContext();
            context.Request.Method = method;
            if (ifNoneMatch != null)
            {
                context.Request.Headers["If-None-Match"] = ifNoneMatch;
            }
            if (origin != null)
            {
                context.Request.Headers["Origin"] = origin;
            }
            context.Response.Body = new MemoryStream();
            var adapter = new ContextAdapter(context);
            if (javaScript)
            {
                _service.ServeJavascript(adapter, _flowData.Object);
            }
            else
            {
                _service.ServeJson(adapter, _flowData.Object);
            }
            return context;
        }

        private static string Header(HttpContext context, string name)
        {
            return context.Response.Headers[name].ToString();
        }

        private static string Body(HttpContext context)
        {
            context.Response.Body.Position = 0;
            using (var reader = new StreamReader(
                context.Response.Body, leaveOpen: true))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
