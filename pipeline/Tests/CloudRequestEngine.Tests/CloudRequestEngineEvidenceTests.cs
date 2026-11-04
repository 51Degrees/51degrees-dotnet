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
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;

namespace FiftyOne.Pipeline.CloudRequestEngine.Tests;

/// <summary>
/// The evidence sent to the cloud is the evidence the cloud accepts, as
/// its evidence keys list it, with each key's prefix removed. Where two
/// keys give the same field, the one whose prefix comes first in the
/// evidence specification's order is sent, being query, header, cookie,
/// server, fiftyone and location, then any other prefix alphabetically.
/// See the processing section of the cloud request engine specification.
/// </summary>
[TestClass]
public class CloudRequestEngineEvidenceTests
{
    private const string ResourceKey = "resource_key";

    /// <summary>
    /// The license key configured where a test needs one.
    /// </summary>
    private const string LicenseKey = "license_key";

    /// <summary>
    /// Text in the warning logged when two values give the same field.
    /// </summary>
    private const string ConflictWarningText = "evidence conflicts";

    /// <summary>
    /// The value given to the key that must be sent.
    /// </summary>
    private const string WinnerValue = "1";

    /// <summary>
    /// The value given to the key that must not be sent.
    /// </summary>
    private const string LoserValue = "2";

    private static string EvidenceKeys(params string[] keys) =>
        JsonConvert.SerializeObject(keys);

    private static IPipeline Build(
        FakeCloud cloud,
        TestLoggerFactory loggerFactory,
        string licenseKey = null,
        bool suppressProcessExceptions = false)
    {
        var builder = new CloudRequestEngineBuilder(
                loggerFactory, new HttpClient(cloud))
            .SetResourceKey(ResourceKey);
        if (licenseKey != null)
        {
#pragma warning disable CS0618 // The license key is obsolete but still sent.
            builder.SetLicenseKey(licenseKey);
#pragma warning restore CS0618
        }
        return new PipelineBuilder(loggerFactory)
            .AddFlowElement(builder.Build())
            .SetSuppressProcessExceptions(suppressProcessExceptions)
            .Build();
    }

    private static IReadOnlyList<KeyValuePair<string, string>> Process(
        IPipeline pipeline,
        FakeCloud cloud,
        params (string Key, string Value)[] evidence)
    {
        using (var data = pipeline.CreateFlowData())
        {
            foreach (var (key, value) in evidence)
            {
                data.AddEvidence(key, value);
            }
            data.Process();
        }
        return cloud.Forms.Last();
    }

    private static string[] Values(
        IEnumerable<KeyValuePair<string, string>> form, string field) =>
        form.Where(f => string.Equals(
                f.Key, field, StringComparison.OrdinalIgnoreCase))
            .Select(f => f.Value)
            .ToArray();

    private static string[] ConflictWarnings(TestLoggerFactory loggerFactory) =>
        loggerFactory.Loggers
            .SelectMany(l => l.WarningEntries)
            .Where(w => w.Contains(ConflictWarningText))
            .ToArray();

    private static string Field(string key) =>
        key.Substring(key.IndexOf('.') + 1);

    /// <summary>
    /// Evidence the cloud does not list among its evidence keys is not
    /// sent, whatever its prefix, and a key with no prefix is not sent
    /// unless the cloud lists it.
    /// </summary>
    [TestMethod]
    public void Process_SendsOnlyAcceptedEvidence()
    {
        var cloud = new FakeCloud();
        cloud.For(Constants.EVIDENCE_KEYS_FILENAME).Body =
            EvidenceKeys("query.user-agent", "header.sec-ch-ua");
        using var pipeline = Build(cloud, new TestLoggerFactory());

        var form = Process(pipeline, cloud,
            ("query.user-agent", "iPhone"),
            ("header.sec-ch-ua", "Chromium"),
            ("header.not-accepted", "1"),
            ("cookie.other", "2"),
            ("date-of-birth", "3"));

        CollectionAssert.AreEquivalent(
            new[] { "resource", "user-agent", "sec-ch-ua" },
            form.Select(f => f.Key).ToArray());
        CollectionAssert.AreEqual(new[] { "iPhone" },
            Values(form, "user-agent"));
        CollectionAssert.AreEqual(new[] { "Chromium" },
            Values(form, "sec-ch-ua"));
    }

    /// <summary>
    /// Evidence keys are not case sensitive, so evidence matches an
    /// accepted key whatever case either is written in.
    /// </summary>
    [TestMethod]
    public void Process_AcceptedKeysMatchWhateverTheirCase()
    {
        var cloud = new FakeCloud();
        cloud.For(Constants.EVIDENCE_KEYS_FILENAME).Body =
            EvidenceKeys("header.User-Agent");
        using var pipeline = Build(cloud, new TestLoggerFactory());

        var form = Process(pipeline, cloud, ("header.user-agent", "iPhone"));

        CollectionAssert.AreEqual(new[] { "iPhone" },
            Values(form, "user-agent"));
    }

    /// <summary>
    /// Of two keys that give the same field, the one whose prefix comes
    /// first in the evidence specification's order is sent, whichever
    /// was added first. A clash is logged, naming the key sent first,
    /// unless the key sent is query evidence, which is expected to
    /// replace the others.
    /// </summary>
    [TestMethod]
    [DataRow("query.x", "header.x", false)]
    [DataRow("header.x", "cookie.x", true)]
    [DataRow("cookie.x", "server.x", true)]
    [DataRow("server.x", "fiftyone.x", true)]
    [DataRow("fiftyone.x", "location.x", true)]
    [DataRow("location.x", "custom.x", true)]
    [DataRow("server.client-ip", "custom.client-ip", true)]
    [DataRow("query.x", "custom.x", false)]
    [DataRow("a.x", "b.x", true)]
    public void Process_HigherPrecedencePrefixSent(
        string winner, string loser, bool warns)
    {
        var cloud = new FakeCloud();
        cloud.For(Constants.EVIDENCE_KEYS_FILENAME).Body =
            EvidenceKeys(winner, loser);
        var loggerFactory = new TestLoggerFactory();
        using var pipeline = Build(cloud, loggerFactory);

        var orders = new[]
        {
            new[] { (winner, WinnerValue), (loser, LoserValue) },
            new[] { (loser, LoserValue), (winner, WinnerValue) },
        };
        foreach (var evidence in orders)
        {
            var form = Process(pipeline, cloud, evidence);
            CollectionAssert.AreEqual(new[] { WinnerValue },
                Values(form, Field(winner)),
                $"'{winner}' must be sent rather than '{loser}'");
        }

        var warnings = ConflictWarnings(loggerFactory);
        if (warns)
        {
            Assert.HasCount(orders.Length, warnings);
            foreach (var warning in warnings)
            {
                Assert.Contains(
                    $"'{winner}={WinnerValue}' evidence conflicts with " +
                    $"'{loser}={LoserValue}'",
                    warning);
            }
        }
        else
        {
            Assert.IsEmpty(warnings);
        }
    }

    /// <summary>
    /// A clash is reported only between evidence that is sent, so
    /// evidence the cloud does not accept is not named.
    /// </summary>
    [TestMethod]
    public void Process_ConflictWarningNamesOnlyAcceptedEvidence()
    {
        var cloud = new FakeCloud();
        cloud.For(Constants.EVIDENCE_KEYS_FILENAME).Body =
            EvidenceKeys("header.user-agent", "cookie.user-agent");
        var loggerFactory = new TestLoggerFactory();
        using var pipeline = Build(cloud, loggerFactory);

        var form = Process(pipeline, cloud,
            ("header.user-agent", "A"),
            ("cookie.user-agent", "B"),
            ("custom.user-agent", "C"));

        CollectionAssert.AreEqual(new[] { "A" }, Values(form, "user-agent"));
        var warnings = ConflictWarnings(loggerFactory);
        Assert.HasCount(1, warnings);
        Assert.AreEqual(
            "'header.user-agent=A' evidence conflicts with " +
            "'cookie.user-agent=B'",
            warnings[0]);
    }

    /// <summary>
    /// Two keys that differ only in the case of their field give the
    /// same field, so only one value is sent for it.
    /// </summary>
    [TestMethod]
    public void Process_SameFieldInAnotherCase_SentOnce()
    {
        var cloud = new FakeCloud();
        cloud.For(Constants.EVIDENCE_KEYS_FILENAME).Body =
            EvidenceKeys("query.User-Agent", "header.user-agent");
        using var pipeline = Build(cloud, new TestLoggerFactory());

        var form = Process(pipeline, cloud,
            ("query.User-Agent", "Q"),
            ("header.user-agent", "H"));

        CollectionAssert.AreEqual(new[] { "Q" }, Values(form, "user-agent"));
    }

    /// <summary>
    /// The cloud lists query.values among the evidence it accepts, and
    /// evidence named resource, license or values would put a second
    /// field of that name beside the credentials the engine sends. Such
    /// evidence is left out, so each credential is sent once, as
    /// configured.
    /// </summary>
    [TestMethod]
    public void Process_CredentialsNotTakenFromEvidence()
    {
        var cloud = new FakeCloud();
        cloud.For(Constants.EVIDENCE_KEYS_FILENAME).Body = EvidenceKeys(
            "query.resource",
            "query.license",
            "query.values",
            "header.resource",
            "query.user-agent");
        using var pipeline = Build(
            cloud, new TestLoggerFactory(), LicenseKey);

        var form = Process(pipeline, cloud,
            ("query.resource", "other_resource_key"),
            ("query.license", "other_license_key"),
            ("query.values", "device.ismobile"),
            ("header.resource", "another_resource_key"),
            ("query.user-agent", "iPhone"));

        CollectionAssert.AreEqual(new[] { ResourceKey },
            Values(form, "resource"));
        CollectionAssert.AreEqual(new[] { LicenseKey },
            Values(form, "license"));
        Assert.IsEmpty(Values(form, "values"));
        CollectionAssert.AreEqual(new[] { "iPhone" },
            Values(form, "user-agent"));
    }

    /// <summary>
    /// After the evidence keys failed at build, the first request made
    /// asks for them again before it sends any evidence, and then sends
    /// only the evidence the cloud accepts.
    /// </summary>
    [TestMethod]
    public void Process_RetriesEvidenceKeysOnFirstUse()
    {
        var cloud = new FakeCloud();
        var evidenceKeys = cloud.For(Constants.EVIDENCE_KEYS_FILENAME);
        evidenceKeys.Status = HttpStatusCode.ServiceUnavailable;
        using var pipeline = Build(cloud, new TestLoggerFactory());

        evidenceKeys.Status = HttpStatusCode.OK;
        var form = Process(pipeline, cloud,
            ("query.user-agent", "iPhone"),
            ("header.not-accepted", "1"));

        Assert.AreEqual(2, cloud.Requests(Constants.EVIDENCE_KEYS_FILENAME));
        CollectionAssert.AreEquivalent(
            new[] { "resource", "user-agent" },
            form.Select(f => f.Key).ToArray());
    }

    /// <summary>
    /// When the evidence keys still cannot be had on first use, the
    /// request fails as any other processing error does. No evidence is
    /// sent, and the error is thrown or kept on the flow data as the
    /// Pipeline's SuppressProcessExceptions setting says.
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Process_EvidenceKeysStillFailing_IsProcessError(
        bool suppressProcessExceptions)
    {
        var cloud = new FakeCloud();
        cloud.For(Constants.EVIDENCE_KEYS_FILENAME).Status =
            HttpStatusCode.ServiceUnavailable;
        using var pipeline = Build(
            cloud,
            new TestLoggerFactory(),
            suppressProcessExceptions: suppressProcessExceptions);

        using var data = pipeline.CreateFlowData();
        data.AddEvidence("query.user-agent", "iPhone");
        if (suppressProcessExceptions)
        {
            data.Process();
            Assert.IsInstanceOfType<CloudRequestException>(
                data.Errors.Single().ExceptionData);
        }
        else
        {
            var ex = Assert.ThrowsExactly<AggregateException>(
                () => data.Process());
            Assert.IsInstanceOfType<CloudRequestException>(
                ex.InnerExceptions.Single());
        }
        Assert.AreEqual(0, cloud.Requests(Constants.DATA_FILENAME),
            "no evidence is sent before the accepted keys are known");
    }
}
