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

using FiftyOne.Caching;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace FiftyOne.Pipeline.CloudRequestEngine.Tests;

/// <summary>
/// On the server route a publisher's server passes the page's JavaScript to
/// the browser, and the browser's answers back to the cloud through this
/// engine. The page script posts the session id, the sequence and every
/// stored value whose name starts <c>51D_</c>, including the browser test
/// answers, whose names change with each script. The server may also supply
/// the visitor's address, JA4 and HTTP/2 settings as query evidence. Each
/// iteration has to reach the cloud with all of them.
/// </summary>
[TestClass]
public class CloudRequestEngineBrowserTestPassThroughTests
{
    private const string SessionId = "3f2a5c1e-8d4b-4a6f-9c2e-1b7d0e5f4a3c";

    /// <summary>
    /// A browser test answer name. The part after <c>51D_</c> changes with
    /// each script, so no list of evidence keys can name it.
    /// </summary>
    private const string AnswerName = "51D_k7Qz2";

    private const string AnswerValue = "3z|eyJ0IjoiYWJjIn0";

    private const string CookieAnswerName = "51D_m4Rt9";

    private const string Ja4 = "t13d1516h2_8daaf6152771_02713d6af862";

    private const string H2 = "1:65536;2:0;4:6291456;6:262144|15663105|0|m,a,s,p";

    private const string VisitorIp = "203.0.113.7";

    /// <summary>
    /// Answers the endpoints the engine calls, keeping the form of every
    /// json request. The evidence keys are the kind the cloud lists, which
    /// name no browser test answer, session id or sequence.
    /// </summary>
    private sealed class Cloud : HttpMessageHandler
    {
        public ConcurrentQueue<string> JsonBodies { get; } =
            new ConcurrentQueue<string>();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri.AbsolutePath.ToLowerInvariant();
            string body;
            if (path.EndsWith("evidencekeys"))
            {
                body = "['query.user-agent', 'header.user-agent', " +
                    "'query.51D_ScreenPixelsWidth', " +
                    "'cookie.51D_ScreenPixelsWidth', 'query.client-ip']";
            }
            else if (path.EndsWith("accessibleproperties"))
            {
                body = "{'Products': {'device': {'DataTier': 'tier'," +
                    "'Properties': [{'Name': 'value','Type': 'String'," +
                    "'Category': 'Device'}]}}}";
            }
            else
            {
                JsonBodies.Enqueue(await request.Content.ReadAsStringAsync(
                    cancellationToken));
                body = "{'device':{'value':'1'}}";
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body),
            };
        }
    }

    private static FlowElements.CloudRequestEngine Engine(
        ILoggerFactory loggerFactory,
        HttpClient http,
        bool cache)
    {
        var builder = new CloudRequestEngineBuilder(loggerFactory, http)
            .SetResourceKey("resource_key");
        if (cache)
        {
            builder.SetCache(new CacheConfiguration()
            {
                Builder = new LruPutCacheBuilder(),
                Size = 10,
            });
        }
        return builder.Build();
    }

    /// <summary>
    /// One page iteration as a server passes it on: the request's headers,
    /// the session id and sequence the page script posted, and from the
    /// second iteration the browser test answers it posted, plus the
    /// visitor's address, JA4 and HTTP/2 settings the server supplies.
    /// </summary>
    private static void AddIteration(
        Core.Data.IFlowData data,
        int sequence)
    {
        data.AddEvidence("header.user-agent", "Mozilla/5.0");
        data.AddEvidence("query.session-id", SessionId);
        data.AddEvidence("query.sequence", sequence.ToString());
        if (sequence > 1)
        {
            data.AddEvidence("query." + AnswerName, AnswerValue + sequence);
            data.AddEvidence("cookie." + CookieAnswerName, AnswerValue);
        }
        data.AddEvidence("query.client-ip", VisitorIp);
        data.AddEvidence("query.ja4", Ja4);
        data.AddEvidence("query.h2", H2);
    }

    /// <summary>
    /// Every iteration reaches the cloud with the session id, its own
    /// sequence, the answers it carries and the values the server supplied,
    /// with and without the engine's cache. With the cache on, iterations
    /// that differ only in the sequence or the answers must not be answered
    /// from the cache, or the iteration carrying the answers never reaches
    /// the cloud.
    /// </summary>
    [DataTestMethod]
    [DataRow(false, DisplayName = "No cache")]
    [DataRow(true, DisplayName = "Cache")]
    public void CloudRequestEngine_ServerRoute_SendsEveryIteration(bool cache)
    {
        var loggerFactory = new LoggerFactory();
        var cloud = new Cloud();
        using var http = new HttpClient(cloud);
        var engine = Engine(loggerFactory, http, cache);

        using (var pipeline = new PipelineBuilder(loggerFactory)
            .AddFlowElement(engine)
            .Build())
        {
            for (var sequence = 1; sequence <= 3; sequence++)
            {
                using var data = pipeline.CreateFlowData();
                AddIteration(data, sequence);
                data.Process();
            }
        }

        var bodies = cloud.JsonBodies
            .Select(b => HttpUtility.ParseQueryString(b))
            .ToList();
        Assert.AreEqual(3, bodies.Count,
            "Each iteration must reach the cloud.");
        for (var i = 0; i < bodies.Count; i++)
        {
            var form = bodies[i];
            var sequence = i + 1;
            Assert.AreEqual(SessionId, form["session-id"]);
            Assert.AreEqual(sequence.ToString(), form["sequence"]);
            Assert.AreEqual(VisitorIp, form["client-ip"]);
            Assert.AreEqual(Ja4, form["ja4"]);
            Assert.AreEqual(H2, form["h2"]);
            if (sequence > 1)
            {
                Assert.AreEqual(AnswerValue + sequence, form[AnswerName]);
                Assert.AreEqual(AnswerValue, form[CookieAnswerName]);
            }
            else
            {
                Assert.IsNull(form[AnswerName]);
            }
        }
    }

    /// <summary>
    /// A web integration copies a request's values into evidence only for
    /// the keys its pipeline accepts, which for this engine are the keys
    /// the cloud lists. The browser test answers arrive under names no list
    /// can hold, as a posted field or a cookie, so the engine accepts every
    /// <c>query.51D_</c> and <c>cookie.51D_</c> key, as the cloud does. It
    /// still refuses the values a server supplies, so a browser can never
    /// choose its own JA4 or HTTP/2 settings through the page's request.
    /// </summary>
    [TestMethod]
    public void CloudRequestEngine_EvidenceKeyFilter_AcceptsBrowserTestAnswers()
    {
        var loggerFactory = new LoggerFactory();
        var cloud = new Cloud();
        using var http = new HttpClient(cloud);
        var engine = Engine(loggerFactory, http, false);

        var filter = engine.EvidenceKeyFilter;
        Assert.IsTrue(filter.Include("query." + AnswerName));
        Assert.IsTrue(filter.Include("cookie." + CookieAnswerName));
        Assert.IsTrue(filter.Include("QUERY.51d_k7Qz2"),
            "Evidence keys ignore letter case.");
        Assert.IsTrue(filter.Include("query.51D_ScreenPixelsWidth"),
            "A key the cloud lists is still accepted.");
        Assert.IsTrue(filter.Include("header.user-agent"));
        Assert.IsFalse(filter.Include("query.51D_"),
            "The prefix alone is no answer.");
        Assert.IsFalse(filter.Include("header.51D_k7Qz2"),
            "Answers arrive as posted fields or cookies only.");
        Assert.IsFalse(filter.Include("query.ja4"));
        Assert.IsFalse(filter.Include("query.h2"));
        Assert.IsFalse(filter.Include("query.unlisted"));
    }
    /// <summary>
    /// A web integration asks the flow data's filter, which is the
    /// pipeline's, so the answers are accepted there too.
    /// </summary>
    [TestMethod]
    public void CloudRequestEngine_PipelineFilter_AcceptsBrowserTestAnswers()
    {
        var loggerFactory = new LoggerFactory();
        var cloud = new Cloud();
        using var http = new HttpClient(cloud);
        var engine = Engine(loggerFactory, http, false);
        using var pipeline = new PipelineBuilder(loggerFactory)
            .AddFlowElement(engine)
            .Build();
        using var data = pipeline.CreateFlowData();

        Assert.IsTrue(data.EvidenceKeyFilter.Include("query." + AnswerName));
        Assert.IsTrue(data.EvidenceKeyFilter.Include("cookie." + CookieAnswerName));
        Assert.IsFalse(data.EvidenceKeyFilter.Include("query.ja4"));
    }

    /// <summary>
    /// The results cache is still keyed on the keys the cloud lists, so a
    /// request that is not a page iteration and differs only in a stored
    /// value the cloud doesn't list is answered from the cache, as before.
    /// </summary>
    [TestMethod]
    public void CloudRequestEngine_Cache_KeyedOnListedKeysOnly()
    {
        var loggerFactory = new LoggerFactory();
        var cloud = new Cloud();
        using var http = new HttpClient(cloud);
        var engine = Engine(loggerFactory, http, true);
        using (var pipeline = new PipelineBuilder(loggerFactory)
            .AddFlowElement(engine)
            .Build())
        {
            foreach (var value in new[] { "first", "second" })
            {
                using var data = pipeline.CreateFlowData();
                data.AddEvidence("header.user-agent", "Mozilla/5.0");
                data.AddEvidence("query." + AnswerName, value);
                data.Process();
            }
        }
        Assert.AreEqual(1, cloud.JsonBodies.Count);
    }
}
