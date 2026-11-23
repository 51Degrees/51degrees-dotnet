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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FiftyOne.HumanConfidence.Examples.Tests;

/// <summary>
/// The two calls the example's server makes for the passive level, made
/// against the live cloud service with a key from the environment. The
/// first checks that the key carries the property, the way the example
/// does before it serves its page, against the cloud in
/// _51DEGREES_CLOUD_ENDPOINT. The second asks for the passive level with
/// the evidence passed on as it is, and reads the source and the level,
/// and a key outside the Bespoke product is asked the same and gets no
/// source. The passive call and the call with the key outside Bespoke need
/// a cloud that serves HumanConfidenceSource and a passive level, which the
/// production cloud does not yet, so their tests are in the
/// <see cref="StagingCategory"/> category and call the staging cloud in
/// CLOUD_STAGING_ENDPOINT, with the Bespoke key in
/// RESOURCE_KEY_BESPOKE_STAGING. Only CI holds those two names, and
/// .github/workflows/cloud-human-confidence.yml runs that category.
/// Nothing here says how the cloud reaches the level. Without a name it
/// needs, a test skips and names it, and never passes, because a cloud
/// test that passes without calling the cloud has proved nothing. The key
/// is never printed.
/// </summary>
[TestClass]
public class LiveCloudTests
{
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// The category of the tests that need the staging cloud. The shared
    /// CI flow leaves it out, because the staging names never reach it.
    /// </summary>
    internal const string StagingCategory = "CloudStaging";

    /// <summary>
    /// The variable the key is read from, which holds the key that carries
    /// every property, HumanConfidence among them.
    /// </summary>
    internal const string KeyName = "_51DEGREES_RESOURCE_KEY_BESPOKE";

    /// <summary>
    /// The variable holding a key outside the Bespoke product, which
    /// carries no HumanConfidenceSource.
    /// </summary>
    internal const string BasicKeyName = "_51DEGREES_RESOURCE_KEY_BASIC";

    /// <summary>
    /// The variable holding a Bespoke key the staging cloud resolves. It
    /// is not the production key, which the staging cloud may not know.
    /// </summary>
    internal const string StagingKeyName = "RESOURCE_KEY_BESPOKE_STAGING";

    /// <summary>
    /// The variable naming the staging cloud's API base, ending /api/v4/.
    /// It has no default, so a staging test never calls another cloud.
    /// </summary>
    internal const string StagingEndpointVariable = "CLOUD_STAGING_ENDPOINT";

    /// <summary>
    /// The properties the passive request names, so the answer carries
    /// both whatever the key's own property list holds.
    /// </summary>
    private const string Values =
        "derived.humanconfidence,derived.humanconfidencesource";

    internal const string EndpointVariable = "_51DEGREES_CLOUD_ENDPOINT";
    internal const string DefaultEndpoint = "https://cloud.51degrees.com/api/v4/";

    private const string ConfigureUrl =
        "https://51degrees.com/configure?utm_source=code&utm_medium=example&utm_campaign=51degrees-dotnet&utm_content=pipeline-tests-humanconfidence.examples.tests-livecloudtests.cs&utm_term=resource-key";

    private static readonly string[] Levels = { "High", "Medium", "Low" };

    /// <summary>
    /// The API base the example calls, read the way the example reads it.
    /// </summary>
    internal static string Endpoint()
    {
        var endpoint = Environment.GetEnvironmentVariable(EndpointVariable);
        return (string.IsNullOrWhiteSpace(endpoint)
            ? DefaultEndpoint : endpoint.Trim()).TrimEnd('/') + "/";
    }

    /// <summary>
    /// The staging cloud's API base, from its own variable alone.
    /// </summary>
    internal static string StagingEndpoint()
    {
        var endpoint = Environment.GetEnvironmentVariable(
            StagingEndpointVariable);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            Assert.Inconclusive(
                "This test needs a cloud that serves HumanConfidenceSource " +
                "and a passive level, which the production cloud does not " +
                $"yet. {StagingEndpointVariable} is not set. It names the " +
                "staging cloud, ending /api/v4/, and only CI holds it.");
        }
        return endpoint!.Trim().TrimEnd('/') + "/";
    }

    private static string RequireKey(string name = KeyName)
    {
        var key = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(key))
        {
            Assert.Inconclusive(
                "This test calls the live 51Degrees cloud service, so it " +
                "needs a resource key and there is nothing to fall back " +
                $"on. {name} is not set. {EndpointVariable} names the " +
                $"service, and defaults to {DefaultEndpoint}. Create a " +
                $"key at {ConfigureUrl}.");
        }
        return key!;
    }

    /// <summary>
    /// A key for a test against the staging cloud, from the variable
    /// named.
    /// </summary>
    internal static string RequireStagingKey(string name)
    {
        var key = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(key))
        {
            Assert.Inconclusive(
                "This test calls the staging cloud in " +
                $"{StagingEndpointVariable}, so it needs a resource key " +
                $"that cloud resolves. {name} is not set.");
        }
        return key!;
    }

    /// <summary>
    /// A message with the key taken out, because a failed request can
    /// quote its URL.
    /// </summary>
    private static string Redact(string text, string key) =>
        text.Replace(key, "<key>", StringComparison.Ordinal);

    [TestMethod]
    [Timeout(60_000)]
    public async Task TheKeyCarriesHumanConfidence()
    {
        var key = RequireKey();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        string json;
        try
        {
            json = await http.GetStringAsync(
                Endpoint() + "accessibleproperties?resource=" + key);
        }
        catch (HttpRequestException ex)
        {
            Assert.Fail("accessibleproperties failed: " + Redact(ex.Message, key));
            return;
        }
        using var document = JsonDocument.Parse(json);
        var names = new List<string>();
        if (document.RootElement.TryGetProperty("Products", out var products))
        {
            foreach (var product in products.EnumerateObject())
            {
                if (product.Value.TryGetProperty("Properties", out var properties))
                {
                    names.AddRange(properties.EnumerateArray()
                        .Select(p => p.GetProperty("Name").GetString())
                        .Where(n => n != null)
                        .Select(n => n!));
                }
            }
        }
        TestContext.WriteLine($"The key carries {names.Count} properties.");
        CollectionAssert.Contains(names, "HumanConfidence",
            "The key does not carry HumanConfidence, so the example would " +
            $"show its contact us link rather than a level. Set {KeyName} " +
            "to a key that carries it.");
    }

    [TestMethod]
    [TestCategory(StagingCategory)]
    [Timeout(60_000)]
    public async Task ThePassiveCallAnswersASourceAndALevel()
    {
        var endpoint = StagingEndpoint();
        var key = RequireStagingKey(StagingKeyName);
        var (status, body) = await PassiveAsync(endpoint, key);
        Assert.AreEqual(200, status,
            "The passive call did not succeed: " + Redact(body, key));
        using var document = JsonDocument.Parse(body);
        Assert.IsTrue(
            document.RootElement.TryGetProperty("derived", out var derived),
            "The answer has no derived object: " + Redact(body, key));
        Assert.IsTrue(
            derived.TryGetProperty("humanconfidencesource", out var source),
            "The answer has no humanconfidencesource: " +
            Redact(derived.GetRawText(), key));
        Assert.AreEqual("Passive", source.GetString(),
            "A request with no browser tests is passive.");
        Assert.IsTrue(
            derived.TryGetProperty("humanconfidence", out var level),
            "The answer has no humanconfidence: " +
            Redact(derived.GetRawText(), key));
        CollectionAssert.Contains(Levels, level.GetString(),
            "A passive level is one of the three plain levels.");
        TestContext.WriteLine($"Passive level {level.GetString()}.");
    }

    [TestMethod]
    [TestCategory(StagingCategory)]
    [Timeout(60_000)]
    public async Task AKeyOutsideBespokeGetsNoSource()
    {
        var endpoint = StagingEndpoint();
        var key = RequireStagingKey(BasicKeyName);
        var (status, body) = await PassiveAsync(endpoint, key);
        Assert.AreEqual(200, status,
            "The passive call did not succeed: " + Redact(body, key));
        using var document = JsonDocument.Parse(body);
        var source = document.RootElement.TryGetProperty("derived", out var derived)
            && derived.TryGetProperty("humanconfidencesource", out var value)
            ? value : default;
        if (source.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null))
        {
            Assert.Fail(
                "A key outside the Bespoke product should get no " +
                "humanconfidencesource, but the answer has " +
                Redact(source.GetRawText(), key) + ".");
        }
    }

    /// <summary>
    /// Posts the evidence a server passes on from a visitor's request to
    /// the key's JSON path on the cloud given, as the example does, naming
    /// the two properties, and returns the status and the body.
    /// </summary>
    private static async Task<(int Status, string Body)> PassiveAsync(
        string endpoint, string key)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var form = new Dictionary<string, string>
        {
            ["user-agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/142.0.0.0 " +
                "Safari/537.36",
            ["values"] = Values,
        };
        try
        {
            using var answer = await http.PostAsync(
                endpoint + key + ".json", new FormUrlEncodedContent(form));
            return ((int)answer.StatusCode,
                await answer.Content.ReadAsStringAsync());
        }
        catch (HttpRequestException ex)
        {
            Assert.Fail("The passive call failed: " + Redact(ex.Message, key));
            throw;
        }
    }
}
