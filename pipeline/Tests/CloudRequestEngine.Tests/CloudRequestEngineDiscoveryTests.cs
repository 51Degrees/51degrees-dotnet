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

using FiftyOne.Common.TestHelpers;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Net;
using System.Net.Http;

namespace FiftyOne.Pipeline.CloudRequestEngine.Tests;

/// <summary>
/// The discovery requests the engine makes when it is built are handled
/// by the kind of failure, as the revised start-up design of the cloud
/// request engine specification says. A client error (4xx) is a
/// definitive configuration error and fails the build. A server error
/// (5xx) or a timeout is transient, so the build completes with a
/// warning and the failed request is made again on first use.
/// </summary>
[TestClass]
public class CloudRequestEngineDiscoveryTests
{
    private const string ResourceKey = "resource_key";

    /// <summary>
    /// The shortest timeout the builder takes, so that a test waiting for
    /// a request to time out finishes quickly.
    /// </summary>
    private const int TimeoutSeconds = 1;

    /// <summary>
    /// Text in the warning logged when discovery will be retried.
    /// </summary>
    private const string RetryWarningText = "retry";

    /// <summary>
    /// A body carrying an error, as the cloud returns with a 4xx.
    /// </summary>
    private const string ErrorBody = "{ \"errors\": [\"refused\"] }";

    private static FlowElements.CloudRequestEngine Build(
        FakeCloud cloud, ILoggerFactory loggerFactory) =>
        new CloudRequestEngineBuilder(loggerFactory, new HttpClient(cloud))
            .SetResourceKey(ResourceKey)
            .SetTimeOutSeconds(TimeoutSeconds)
            .Build();

    private static string Other(string endpoint) =>
        endpoint == Constants.PROPERTIES_FILENAME
            ? Constants.EVIDENCE_KEYS_FILENAME
            : Constants.PROPERTIES_FILENAME;

    private static int RetryWarnings(TestLoggerFactory loggerFactory) =>
        loggerFactory.Loggers
            .SelectMany(l => l.WarningEntries)
            .Count(w => w.Contains(RetryWarningText));

    /// <summary>
    /// A client error from either discovery request fails the build with
    /// the status the cloud returned.
    /// </summary>
    [TestMethod]
    [DataRow(Constants.PROPERTIES_FILENAME, HttpStatusCode.BadRequest)]
    [DataRow(Constants.PROPERTIES_FILENAME, HttpStatusCode.Unauthorized)]
    [DataRow(Constants.PROPERTIES_FILENAME, HttpStatusCode.Forbidden)]
    [DataRow(Constants.PROPERTIES_FILENAME, HttpStatusCode.NotFound)]
    [DataRow(Constants.EVIDENCE_KEYS_FILENAME, HttpStatusCode.BadRequest)]
    [DataRow(Constants.EVIDENCE_KEYS_FILENAME, HttpStatusCode.Unauthorized)]
    [DataRow(Constants.EVIDENCE_KEYS_FILENAME, HttpStatusCode.Forbidden)]
    [DataRow(Constants.EVIDENCE_KEYS_FILENAME, HttpStatusCode.NotFound)]
    public void Build_ClientError_FailsBuild(
        string endpoint, HttpStatusCode status)
    {
        var cloud = new FakeCloud();
        cloud.For(endpoint).Status = status;
        cloud.For(endpoint).Body = ErrorBody;

        var ex = Assert.ThrowsExactly<CloudRequestException>(() =>
        {
            Build(cloud, new TestLoggerFactory());
        });

        Assert.AreEqual((int)status, ex.HttpStatusCode);
    }

    /// <summary>
    /// A server error from either discovery request does not fail the
    /// build, and the request is made again on first use.
    /// </summary>
    [TestMethod]
    [DataRow(Constants.PROPERTIES_FILENAME,
        HttpStatusCode.InternalServerError)]
    [DataRow(Constants.PROPERTIES_FILENAME, HttpStatusCode.BadGateway)]
    [DataRow(Constants.PROPERTIES_FILENAME,
        HttpStatusCode.ServiceUnavailable)]
    [DataRow(Constants.EVIDENCE_KEYS_FILENAME,
        HttpStatusCode.InternalServerError)]
    [DataRow(Constants.EVIDENCE_KEYS_FILENAME, HttpStatusCode.BadGateway)]
    [DataRow(Constants.EVIDENCE_KEYS_FILENAME,
        HttpStatusCode.ServiceUnavailable)]
    public void Build_ServerError_WarnsAndRetriesOnFirstUse(
        string endpoint, HttpStatusCode status)
    {
        var cloud = new FakeCloud();
        cloud.For(endpoint).Status = status;
        cloud.For(endpoint).Body = ErrorBody;

        AssertBuildsAndRetriesOnFirstUse(cloud, endpoint);
    }

    /// <summary>
    /// A discovery request that times out does not fail the build, and
    /// the request is made again on first use.
    /// </summary>
    [TestMethod]
    [DataRow(Constants.PROPERTIES_FILENAME)]
    [DataRow(Constants.EVIDENCE_KEYS_FILENAME)]
    public void Build_Timeout_WarnsAndRetriesOnFirstUse(string endpoint)
    {
        var cloud = new FakeCloud();
        cloud.For(endpoint).Hang = true;

        AssertBuildsAndRetriesOnFirstUse(cloud, endpoint);
    }

    /// <summary>
    /// Builds an engine while the endpoint given is failing, then lets the
    /// endpoint recover and uses the engine the way a cloud aspect engine
    /// and the web integration do.
    /// </summary>
    private static void AssertBuildsAndRetriesOnFirstUse(
        FakeCloud cloud, string endpoint)
    {
        var loggerFactory = new TestLoggerFactory();
        var engine = Build(cloud, loggerFactory);

        Assert.IsFalse(engine.IsDisposed);
        Assert.AreEqual(1, RetryWarnings(loggerFactory),
            "a warning says discovery will be retried on first use");
        Assert.AreEqual(1, cloud.Requests(endpoint));

        var answer = cloud.For(endpoint);
        answer.Status = HttpStatusCode.OK;
        answer.Hang = false;
        answer.Body = endpoint == Constants.PROPERTIES_FILENAME
            ? FakeCloud.AccessiblePropertiesBody
            : FakeCloud.EvidenceKeysBody;

        Assert.IsTrue(engine.PublicProperties.ContainsKey("device"));
        Assert.IsTrue(engine.EvidenceKeyFilter.Include("query.user-agent"));
        Assert.AreEqual(2, cloud.Requests(endpoint),
            "the failed request is made again on first use");
        Assert.AreEqual(1, cloud.Requests(Other(endpoint)),
            "the request that worked at build is not made again");
        engine.Dispose();
    }
}
