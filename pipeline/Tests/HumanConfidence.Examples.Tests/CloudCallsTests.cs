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

using Examples.HumanConfidence.HumanConfidenceWeb;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace FiftyOne.HumanConfidence.Examples.Tests;

/// <summary>
/// The calls the example's server makes to the cloud, against a recorded
/// transport so nothing here touches the network. The server passes the
/// evidence on as it is, to the cloud's own paths, and reads the result
/// as the cloud wrote it. The rule being pinned is that the level is the
/// cloud's to give, so nothing here works one out.
/// </summary>
[TestClass]
public class CloudCallsTests
{
    private const string Api = "https://cloud.example.test/api/v4/";
    private const string Resource = "resource-key";
    private const string License = "license-key";

    private const string EvidenceKeys =
        "[\"header.User-Agent\",\"header.Sec-CH-UA\",\"header.client-ip\"," +
        "\"query.client-ip\",\"server.client-ip\",\"cookie.51D_IsVisible\"]";

    private FakeHandler _handler = null!;
    private HumanConfidenceCloud _cloud = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _handler = new FakeHandler();
        _cloud = new HumanConfidenceCloud(
            new HttpClient(_handler), Api, Resource, License);
    }

    private static KeyValuePair<string, string> Header(string name, string value)
        => new KeyValuePair<string, string>(name, value);

    [TestMethod]
    public async Task Evidence_HoldsOnlyTheHeadersTheCloudReads_WithTheirValuesAsTheyCame()
    {
        _handler.Enqueue(HttpStatusCode.OK, EvidenceKeys);
        var userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/142.0.0.0";
        var secChUa = "\"Chromium\";v=\"142\", \"Not_A Brand\";v=\"24\"";

        var evidence = await _cloud.EvidenceAsync(new[]
        {
            Header("User-Agent", userAgent),
            Header("Sec-CH-UA", secChUa),
            Header("Authorization", "Bearer secret"),
            Header("Cookie", "session=1"),
        }, null);

        Assert.AreEqual(userAgent, evidence["user-agent"],
            "The User-Agent is passed on as it came.");
        Assert.AreEqual(secChUa, evidence["sec-ch-ua"],
            "A client hint is passed on as it came, quotes and all.");
        Assert.IsFalse(evidence.ContainsKey("authorization"),
            "A header the cloud does not read stays on the server.");
        Assert.IsFalse(evidence.ContainsKey("cookie"),
            "A header the cloud does not read stays on the server.");
        Assert.AreEqual(2, evidence.Count);
        Assert.AreEqual(Api + "evidencekeys", _handler.Requests[0].Url,
            "The headers the cloud reads come from its own list.");
    }

    [TestMethod]
    public async Task Evidence_SetsTheVisitorsAddressLast_SoNoHeaderCanReplaceIt()
    {
        _handler.Enqueue(HttpStatusCode.OK, EvidenceKeys);

        var evidence = await _cloud.EvidenceAsync(new[]
        {
            Header("client-ip", "198.51.100.9"),
        }, IPAddress.Parse("::ffff:203.0.113.5"));

        Assert.AreEqual("203.0.113.5", evidence["client-ip"],
            "The address the connection came from wins over a header, " +
            "and an IPv4 address mapped to IPv6 is sent as IPv4.");
    }

    [TestMethod]
    public async Task Evidence_FetchesTheCloudsHeaderListOnce()
    {
        _handler.Enqueue(HttpStatusCode.OK, EvidenceKeys);
        await _cloud.EvidenceAsync(new[] { Header("User-Agent", "a") }, null);
        await _cloud.EvidenceAsync(new[] { Header("User-Agent", "b") }, null);
        Assert.AreEqual(1, _handler.Requests.Count,
            "The list is fetched once for the process.");
    }

    [TestMethod]
    public async Task Passive_PostsTheEvidenceToTheKeysJsonPath_AndReadsTheSourceAndTheLevel()
    {
        _handler.Enqueue(HttpStatusCode.OK,
            "{\"device\":{\"ismobile\":false}," +
            "\"derived\":{\"humanconfidencesource\":\"Passive\"," +
            "\"humanconfidence\":\"Medium\"}}");
        var evidence = new Dictionary<string, string>
        {
            ["user-agent"] = "Mozilla/5.0 (X11; Linux x86_64) Chrome/142.0.0.0",
            ["client-ip"] = "203.0.113.5",
        };

        var answer = await _cloud.PassiveAsync(evidence);

        var request = _handler.Requests[0];
        Assert.AreEqual("POST", request.Method);
        Assert.AreEqual(Api + Resource + ".json", request.Url,
            "The passive level comes from the key's JSON path.");
        var form = HttpUtility.ParseQueryString(request.Body);
        Assert.AreEqual(evidence["user-agent"], form["user-agent"],
            "The evidence is posted as it is.");
        Assert.AreEqual(evidence["client-ip"], form["client-ip"],
            "The evidence is posted as it is.");
        Assert.AreEqual(2, form.Count, "Nothing is added to the evidence.");
        Assert.IsNull(answer.Error);
        Assert.AreEqual("Passive", answer.Source,
            "The source is read from the answer's derived object.");
        Assert.AreEqual("Medium", answer.HumanConfidence,
            "The level is read from the answer's derived object as the " +
            "cloud wrote it.");
    }

    [TestMethod]
    public async Task Passive_WithoutTheProperty_ReportsNoLevelRatherThanOne()
    {
        _handler.Enqueue(HttpStatusCode.OK, "{\"device\":{\"ismobile\":false}}");

        var answer = await _cloud.PassiveAsync(new Dictionary<string, string>());

        Assert.IsNull(answer.Error);
        Assert.IsNull(answer.Source);
        Assert.IsNull(answer.HumanConfidence,
            "A key without the property gets no level, and none is made up.");
    }

    [TestMethod]
    public async Task Passive_ForAKeyOutsideBespoke_ReportsNoSourceRatherThanOne()
    {
        _handler.Enqueue(HttpStatusCode.OK,
            "{\"derived\":{\"humanconfidence\":null," +
            "\"humanconfidencenullreason\":\"HumanConfidence is a paid feature.\"," +
            "\"humanconfidencesource\":null," +
            "\"humanconfidencesourcenullreason\":" +
            "\"HumanConfidenceSource is a paid feature.\"}}");

        var answer = await _cloud.PassiveAsync(new Dictionary<string, string>());

        Assert.IsNull(answer.Error);
        Assert.IsNull(answer.Source,
            "A key outside the Bespoke product gets no source, and none " +
            "is made up.");
        Assert.IsNull(answer.HumanConfidence);
    }

    [TestMethod]
    public async Task Passive_WithAnErrorStatus_ReportsTheStatus()
    {
        _handler.Enqueue(HttpStatusCode.Forbidden, "{\"errors\":[\"refused\"]}");

        var answer = await _cloud.PassiveAsync(new Dictionary<string, string>());

        Assert.IsNotNull(answer.Error);
        StringAssert.StartsWith(answer.Error, "HTTP 403");
        Assert.IsNull(answer.HumanConfidence);
    }

    [TestMethod]
    public async Task Redeem_PostsTheSealedResultAndTheLicenseToHcRedeem_AndReturnsTheAnswerAsItCame()
    {
        var body = "{\"humanConfidence\":\"High\",\"status\":\"success\","
            + "\"verifiedAt\":\"2026-11-25T09:15:32Z\",\"secondsSinceVerified\":2}";
        _handler.Enqueue(HttpStatusCode.OK, body);

        var answer = await _cloud.RedeemAsync("sealed-value");

        var request = _handler.Requests[0];
        Assert.AreEqual("POST", request.Method);
        Assert.AreEqual(Api + "hc/redeem", request.Url);
        var form = HttpUtility.ParseQueryString(request.Body);
        Assert.AreEqual("sealed-value", form["sealedResult"],
            "The sealed result is sent as it came from the page.");
        Assert.AreEqual("sealed-value", form["value"],
            "The same text goes as value, the name earlier cloud releases read.");
        Assert.AreEqual(License, form["license"],
            "The License Key goes with it, from the server only.");
        Assert.AreEqual(3, form.Count);
        Assert.IsNull(answer.Error);
        Assert.AreEqual(200, answer.StatusCode);
        Assert.AreEqual(body, answer.Body,
            "The page gets the cloud's answer as the cloud gave it.");
    }

    [TestMethod]
    public async Task Redeem_WithoutALicense_SendsOnlyTheSealedResult()
    {
        var cloud = new HumanConfidenceCloud(
            new HttpClient(_handler), Api, Resource, null);
        _handler.Enqueue(HttpStatusCode.OK,
            "{\"humanConfidence\":\"Medium\",\"status\":\"unreadable\"}");

        await cloud.RedeemAsync("sealed-value");

        var form = HttpUtility.ParseQueryString(_handler.Requests[0].Body);
        Assert.AreEqual(2, form.Count);
        Assert.AreEqual("sealed-value", form["sealedResult"]);
        Assert.AreEqual("sealed-value", form["value"]);
        Assert.IsNull(form["challenge"]);
    }

    [TestMethod]
    public async Task Redeem_WhereTheServiceHasNoRedeem_SaysSo()
    {
        _handler.Enqueue(HttpStatusCode.NotFound, "");

        var answer = await _cloud.RedeemAsync("sealed-value");

        Assert.AreEqual(404, answer.StatusCode);
        Assert.IsNotNull(answer.Error);
        StringAssert.Contains(answer.Error, "doesn't redeem");
        StringAssert.Contains(answer.Error, Api,
            "The message names the service, so a staging address that " +
            "has no redeem yet is seen for what it is.");
    }

    [TestMethod]
    public async Task CarriesHumanConfidence_ReadsTheKeysPropertiesFromAccessibleProperties()
    {
        _handler.Enqueue(HttpStatusCode.OK,
            "{\"Products\":{\"device\":{\"DataTier\":\"CloudV5Bespoke\"," +
            "\"Properties\":[{\"Name\":\"IsMobile\",\"Type\":\"Boolean\"}," +
            "{\"Name\":\"HumanConfidence\",\"Type\":\"String\"}," +
            "{\"Name\":\"HumanConfidenceSource\",\"Type\":\"String\"}]}}}");

        Assert.AreEqual("yes", await _cloud.CarriesHumanConfidenceAsync());
        Assert.AreEqual(Api + "accessibleproperties?resource=" + Resource,
            _handler.Requests[0].Url);
    }

    [TestMethod]
    public async Task CarriesHumanConfidence_IsNoForAKeyWithoutIt()
    {
        _handler.Enqueue(HttpStatusCode.OK,
            "{\"Products\":{\"device\":{\"DataTier\":\"CloudV5Free\"," +
            "\"Properties\":[{\"Name\":\"IsMobile\",\"Type\":\"Boolean\"}]}}}");

        Assert.AreEqual("no", await _cloud.CarriesHumanConfidenceAsync());
    }

    [TestMethod]
    public async Task CarriesHumanConfidence_IsUnknownWhenTheCloudCannotBeAsked()
    {
        _handler.Enqueue(HttpStatusCode.ServiceUnavailable, "");

        Assert.AreEqual("unknown", await _cloud.CarriesHumanConfidenceAsync(),
            "A cloud that cannot be asked is not a key without the property.");
    }

    /// <summary>
    /// A transport that answers from a queue and records what was sent.
    /// </summary>
    private sealed class FakeHandler : HttpMessageHandler
    {
        public sealed record Recorded(string Method, string Url, string Body);

        private readonly Queue<(HttpStatusCode Status, string Body)> _answers = new();

        public List<Recorded> Requests { get; } = new();

        public void Enqueue(HttpStatusCode status, string body)
            => _answers.Enqueue((status, body));

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content == null
                ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new Recorded(
                request.Method.Method, request.RequestUri!.ToString(), body));
            if (_answers.Count == 0)
            {
                throw new InvalidOperationException(
                    "No answer was queued for " + request.RequestUri);
            }
            var (status, answer) = _answers.Dequeue();
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(answer, Encoding.UTF8, "application/json"),
            };
        }
    }
}
