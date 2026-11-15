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

using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.CloudRequestEngine.Tests;

/// <summary>
/// A Cloud Request Engine can be built from the discovery results an
/// earlier build obtained, asking the cloud service for nothing, as the
/// cloud request engine specification says it should.
/// </summary>
[TestClass]
public class CloudRequestEngineDiscoveryResultsTests
{
    private const string ResourceKey = "AQS5HKcyResourceKeyForDiscovery";

    private const string OtherResourceKey = "AQS5HKcyAnotherResourceKey";

    /// <summary>
    /// Answers the two discovery requests, or fails every request, and
    /// counts the requests it is sent.
    /// </summary>
    private sealed class DiscoveryHandler : HttpMessageHandler
    {
        private int _requests;

        public int Requests => Volatile.Read(ref _requests);

        public bool Fail { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requests);
            if (Fail)
            {
                return Task.FromResult(new HttpResponseMessage(
                    HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent(string.Empty),
                });
            }
            var last = request.RequestUri.Segments.Last()
                .ToLowerInvariant();
            var content = last == Constants.EVIDENCE_KEYS_FILENAME
                ? "[\"query.user-agent\",\"header.user-agent\"]"
                : "{\"Products\":{\"device\":{\"DataTier\":" +
                    "\"CloudV4Free\",\"Properties\":[{\"Name\":" +
                    "\"IsMobile\",\"Type\":\"Boolean\",\"Category\":" +
                    "\"Device\",\"DelayExecution\":false}]}}}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content),
            });
        }
    }

    private static FlowElements.CloudRequestEngine Build(
        DiscoveryHandler handler,
        string resourceKey,
        string discoveryResults = null)
    {
        var builder = new CloudRequestEngineBuilder(
                new LoggerFactory(), new HttpClient(handler))
            .SetResourceKey(resourceKey);
        if (discoveryResults != null)
        {
            builder.SetDiscoveryResults(discoveryResults);
        }
        return builder.Build();
    }

    private static string ResultsOfABuild(string resourceKey)
    {
        using (var engine = Build(new DiscoveryHandler(), resourceKey))
        {
            return engine.GetDiscoveryResults();
        }
    }

    [TestMethod]
    public void Build_WithDiscoveryResults_AsksTheCloudServiceNothing()
    {
        var results = ResultsOfABuild(ResourceKey);
        Assert.IsNotNull(results,
            "An engine that has done its discovery gives its results.");

        var handler = new DiscoveryHandler();
        using var engine = Build(handler, ResourceKey, results);

        Assert.AreEqual(0, handler.Requests,
            "Building from the results asks the cloud service nothing.");
        CollectionAssert.AreEqual(new[] { "device" },
            engine.PublicProperties.Keys.ToList(),
            "The engine has the products of the results.");
        Assert.AreEqual("IsMobile",
            engine.PublicProperties["device"].Properties.Single().Name,
            "The engine has the properties of the results.");
        Assert.IsTrue(engine.EvidenceKeyFilter.Include("query.user-agent"),
            "The engine accepts the Evidence keys of the results.");
        Assert.IsFalse(engine.EvidenceKeyFilter.Include("query.other"),
            "The engine accepts only the Evidence keys of the results.");
        Assert.AreEqual(0, handler.Requests,
            "Reading the results asks the cloud service nothing either.");
    }

    [TestMethod]
    public void BuiltFromDiscoveryResults_GivesTheSameResults()
    {
        var results = ResultsOfABuild(ResourceKey);

        using var engine = Build(new DiscoveryHandler(), ResourceKey, results);

        Assert.AreEqual(results, engine.GetDiscoveryResults(),
            "An engine built from results gives the same results.");
    }

    [TestMethod]
    public void GetDiscoveryResults_HoldsNoKey()
    {
        var results = ResultsOfABuild(ResourceKey);

        Assert.IsFalse(results.Contains(ResourceKey),
            "The results can be stored, so they hold no key.");
    }

    [TestMethod]
    public void Build_WithResultsForOtherKeys_IsRefused()
    {
        var results = ResultsOfABuild(ResourceKey);

        Assert.ThrowsExactly<PipelineConfigurationException>(
            () => Build(new DiscoveryHandler(), OtherResourceKey, results));
    }

    [TestMethod]
    [DataRow("{\"evidenceKeys\":[]}")]
    [DataRow("not JSON")]
    public void Build_WithResultsThatAreNotACopy_IsRefused(string results)
    {
        Assert.ThrowsExactly<PipelineConfigurationException>(
            () => Build(new DiscoveryHandler(), ResourceKey, results));
    }

    [TestMethod]
    public void GetDiscoveryResults_BeforeDiscovery_IsNull()
    {
        using var engine = Build(
            new DiscoveryHandler { Fail = true }, ResourceKey);

        Assert.IsNull(engine.GetDiscoveryResults(),
            "An engine whose discovery has not completed has no results.");
    }
}
