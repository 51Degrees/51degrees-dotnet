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

using Examples.CloudEngine.Data;
using Examples.CloudEngine.FlowElements;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.CloudRequestEngine.Examples.Tests;

/// <summary>
/// The custom flow element example's cloud engine, given a response with no
/// section for it, leaves its data without values and raises no error.
/// </summary>
[TestClass]
public class SimpleCloudEngineMissingSectionTests
{
    [TestMethod]
    public void SimpleCloudEngine_ResponseWithNoSection_LeavesDataWithoutValues()
    {
        var data = Process("{ \"other\": { \"value\": true } }");

        Assert.IsEmpty(data.Errors ?? new List<IFlowError>(),
            "A response with no section for the engine is not an error.");
        Assert.IsEmpty(data.Get<IStarSignData>().AsDictionary(),
            "The engine's data has no values.");
    }

    [TestMethod]
    public void SimpleCloudEngine_ResponseWithNullSection_LeavesDataWithoutValues()
    {
        var data = Process("{ \"starsign\": null }");

        Assert.IsEmpty(data.Errors ?? new List<IFlowError>(),
            "A response whose section for the engine is null is not an " +
            "error.");
        Assert.IsEmpty(data.Get<IStarSignData>().AsDictionary(),
            "The engine's data has no values.");
    }

    [TestMethod]
    public void SimpleCloudEngine_ResponseWithSection_GivesTheStarSign()
    {
        var data = Process(
            "{ \"starsign\": { \"starsign\": \"Sagittarius\" } }");

        Assert.AreEqual("Sagittarius", data.Get<IStarSignData>().StarSign);
    }

    /// <summary>
    /// Process a request through a Pipeline of a cloud request engine,
    /// whose HTTP client gives the response, and the example's engine.
    /// </summary>
    private static IFlowData Process(string response)
    {
        var loggerFactory = new LoggerFactory();
        var requestEngine = new CloudRequestEngineBuilder(
                loggerFactory, new HttpClient(new StarSignService(response)))
            .SetEndPoint("https://localhost/starsign/api/")
            .SetResourceKey("resource_key")
            .Build();
        var engine = new SimpleCloudEngineBuilder(loggerFactory, requestEngine)
            .Build();
        var pipeline = new PipelineBuilder(loggerFactory)
            .AddFlowElement(requestEngine)
            .AddFlowElement(engine)
            .Build();
        var data = pipeline.CreateFlowData();
        data.AddEvidence("query.date-of-birth", "18/12/1992");
        data.Process();
        return data;
    }

    /// <summary>
    /// Answers the three endpoints the cloud request engine calls, so the
    /// example's engine runs without the star sign service. The product
    /// list has the engine's product, and the response is the one given.
    /// </summary>
    private sealed class StarSignService : HttpMessageHandler
    {
        private readonly string _response;

        public StarSignService(string response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri.AbsolutePath.ToLowerInvariant();
            string body;
            if (path.EndsWith("evidencekeys"))
            {
                body = "[\"query.date-of-birth\"]";
            }
            else if (path.EndsWith("accessibleproperties"))
            {
                body = "{\"Products\": {\"starsign\": {" +
                    "\"DataTier\": \"tier\", \"Properties\": [{" +
                    "\"Name\": \"starsign\", \"Type\": \"String\", " +
                    "\"Category\": \"starsign\"}]}}}";
            }
            else
            {
                body = _response;
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body),
            });
        }
    }
}
