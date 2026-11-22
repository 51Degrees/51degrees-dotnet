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
/// does before it serves its page, and the second asks for the passive
/// level the way the example asks, with the evidence passed on as it is,
/// and reads the result. Nothing here says how the cloud reaches the
/// level. With no key the tests skip and name the variables, and never
/// pass, because a cloud test that passes without calling the cloud has
/// proved nothing. The key is never printed.
/// </summary>
[TestClass]
public class LiveCloudTests
{
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// The variables the key is read from, in the order tried. The first
    /// is the name the example reads. The second is the CI secret that
    /// carries the most properties, HumanConfidence among them.
    /// </summary>
    internal static readonly string[] KeyNames =
    {
        "_51DEGREES_RESOURCE_KEY",
        "_51DEGREES_RESOURCE_KEY_BESPOKE",
    };

    private const string EndpointVariable = "_51DEGREES_CLOUD_ENDPOINT";
    private const string DefaultEndpoint = "https://cloud.51degrees.com/api/v4/";

    private const string ConfigureUrl =
        "https://51degrees.com/configure?utm_source=code&utm_medium=example&utm_campaign=51degrees-dotnet&utm_content=pipeline-tests-humanconfidence.examples.tests-livecloudtests.cs&utm_term=resource-key";

    private static readonly string[] Levels = { "High", "Medium", "Low" };

    /// <summary>
    /// The API base the example calls, read the way the example reads it.
    /// </summary>
    private static string Endpoint()
    {
        var endpoint = Environment.GetEnvironmentVariable(EndpointVariable);
        return (string.IsNullOrWhiteSpace(endpoint)
            ? DefaultEndpoint : endpoint.Trim()).TrimEnd('/') + "/";
    }

    private static string RequireKey()
    {
        var key = KeyNames
            .Select(n => Environment.GetEnvironmentVariable(n))
            .FirstOrDefault(v => string.IsNullOrWhiteSpace(v) == false);
        if (key == null)
        {
            Assert.Inconclusive(
                "This test calls the live 51Degrees cloud service, so it " +
                "needs a resource key that carries HumanConfidence and " +
                $"there is nothing to fall back on. None of " +
                $"{string.Join(", ", KeyNames)} is set, tried in that " +
                $"order. {EndpointVariable} names the service, and " +
                $"defaults to {DefaultEndpoint}. Create a key at " +
                $"{ConfigureUrl}.");
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
            "show its contact us link rather than a level. Set one of " +
            $"{string.Join(", ", KeyNames)} to a key that carries it.");
        CollectionAssert.Contains(names, "HumanConfidenceSource",
            "The key does not carry HumanConfidenceSource, which the " +
            "example reads to tell passive from active.");
    }

    [TestMethod]
    [Timeout(60_000)]
    public async Task ThePassiveCallAnswersASourceAndALevel()
    {
        var key = RequireKey();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        // The evidence a server passes on from a visitor's request, sent
        // as the example sends it, which is as the cloud's own query
        // evidence, and left as it is.
        var evidence = new Dictionary<string, string>
        {
            ["user-agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/142.0.0.0 " +
                "Safari/537.36",
        };
        string body;
        int status;
        try
        {
            using var answer = await http.PostAsync(
                Endpoint() + key + ".json", new FormUrlEncodedContent(evidence));
            status = (int)answer.StatusCode;
            body = await answer.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException ex)
        {
            Assert.Fail("The passive call failed: " + Redact(ex.Message, key));
            return;
        }
        Assert.AreEqual(200, status,
            "The passive call did not succeed: " + Redact(body, key));
        using var document = JsonDocument.Parse(body);
        Assert.IsTrue(
            document.RootElement.TryGetProperty("derived", out var derived),
            "The answer has no derived object: " + Redact(body, key));
        Assert.IsTrue(
            derived.TryGetProperty("humanconfidencesource", out var source),
            "The answer has no humanconfidencesource: " + Redact(body, key));
        Assert.AreEqual("Passive", source.GetString(),
            "A request with no browser tests is passive.");
        Assert.IsTrue(
            derived.TryGetProperty("humanconfidence", out var level),
            "The answer has no humanconfidence: " + Redact(body, key));
        CollectionAssert.Contains(Levels, level.GetString(),
            "A passive level is one of the three plain levels.");
        TestContext.WriteLine($"Passive level {level.GetString()}.");
    }
}
