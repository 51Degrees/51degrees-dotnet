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
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.CloudRequestEngine.Tests;

/// <summary>
/// The web request a web integration adds to evidence for usage sharing
/// is not evidence for the cloud. The engine sends every evidence value it
/// is given, so it has to leave this one out, and the value must take no
/// part in the engine's cache key.
/// </summary>
[TestClass]
public class CloudRequestEngineHttpRequestTests
{
    /// <summary>
    /// The key the web request is added under, written out rather than
    /// read from the package, so the test checks the key the
    /// specification gives.
    /// </summary>
    private const string HttpRequestKey = "server.http-request";

    /// <summary>
    /// Answers the three endpoints the engine calls, and keeps the body of
    /// every json request.
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
                body = "['query.user-agent', 'header.user-agent']";
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

    private static ReadOnlyDictionary<string, object> Request(string value) =>
        new ReadOnlyDictionary<string, object>(
            new Dictionary<string, object>()
            {
                { "header.user-agent", "iPhone" },
                { "header.x-test", value },
            });

    /// <summary>
    /// The web request is not sent to the cloud, and two requests that
    /// differ only in it share the engine's cache entry.
    /// </summary>
    [TestMethod]
    public void CloudRequestEngine_WebRequest_IsNotSentOrCached()
    {
        var loggerFactory = new LoggerFactory();
        var cloud = new Cloud();
        using var http = new HttpClient(cloud);
        var engine = new CloudRequestEngineBuilder(loggerFactory, http)
            .SetResourceKey("resource_key")
            .SetCache(new CacheConfiguration()
            {
                Builder = new LruPutCacheBuilder(),
                Size = 10,
            })
            .Build();

        using (var pipeline = new PipelineBuilder(loggerFactory)
            .AddFlowElement(engine)
            .Build())
        {
            foreach (var value in new[] { "first", "second" })
            {
                using var data = pipeline.CreateFlowData();
                data.AddEvidence("header.user-agent", "iPhone");
                data.AddEvidence(HttpRequestKey, Request(value));
                data.Process();
            }
        }

        Assert.AreEqual(1, cloud.JsonBodies.Count,
            "The second request differs only in the web request, so it " +
            "must be answered from the cache.");
        cloud.JsonBodies.TryPeek(out var sent);
        StringAssert.Contains(sent, "user-agent=iPhone");
        Assert.IsFalse(sent.Contains("http-request"),
            $"The web request must not be sent to the cloud: {sent}");
        Assert.IsFalse(sent.Contains("first"),
            $"Values only the web request holds must not be sent: {sent}");
    }
}
