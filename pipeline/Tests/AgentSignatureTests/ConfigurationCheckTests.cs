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

using FiftyOne.Pipeline.AgentSignature.Data;
using FiftyOne.Pipeline.AgentSignature.FlowElement;
using FiftyOne.Pipeline.AgentSignature.Keys;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.AgentSignature.Tests;

/// <summary>
/// The element checks its periods and its registry addresses however it is
/// built, so a configuration handed straight to the element's public
/// constructor is refused at start-up as the builder's setters refuse it,
/// and a registry address is checked before it is fetched.
/// </summary>
[TestClass]
public class ConfigurationCheckTests
{
    private const string GoodRegistry =
        "https://registry.example.com/agents.txt";

    private static readonly string[] PeriodNames =
    {
        nameof(AgentSignatureConfiguration.CacheLifetime),
        nameof(AgentSignatureConfiguration.NegativeCacheLifetime),
        nameof(AgentSignatureConfiguration.WaitBudget),
        nameof(AgentSignatureConfiguration.FetchTimeout),
        nameof(AgentSignatureConfiguration.ClockSkew),
        nameof(AgentSignatureConfiguration.MaxLifetime),
    };

    /// <summary>
    /// Counts the requests sent, and answers each with a registry.
    /// </summary>
    private sealed class CountingHandler : HttpMessageHandler
    {
        private int _requests;

        public int Requests => _requests;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requests);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "https://card.example.com/card.json\n"),
            });
        }
    }

    public static IEnumerable<object[]> Periods() =>
        PeriodNames.Select(name => new object[] { name });

    /// <summary>
    /// The element refuses a configuration holding a negative period.
    /// </summary>
    [DataTestMethod]
    [DynamicData(nameof(Periods), DynamicDataSourceType.Method)]
    public void Element_NegativePeriod_Refused(string name)
    {
        AssertRefused(name, TimeSpan.FromSeconds(-1));
    }

    /// <summary>
    /// The element refuses a configuration holding a period longer than a
    /// year.
    /// </summary>
    [DataTestMethod]
    [DynamicData(nameof(Periods), DynamicDataSourceType.Method)]
    public void Element_PeriodBeyondTheMaximum_Refused(string name)
    {
        AssertRefused(
            name, Constants.MAXIMUM_PERIOD + TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// The default configuration, and one holding the longest period
    /// allowed and an HTTPS registry, build an element.
    /// </summary>
    [TestMethod]
    public void Element_PeriodsAndRegistryAllowed_Built()
    {
        using (CreateElement(new AgentSignatureConfiguration()))
        {
        }
        var configuration = new AgentSignatureConfiguration();
        foreach (var name in PeriodNames)
        {
            SetPeriod(configuration, name, Constants.MAXIMUM_PERIOD);
        }
        configuration.Registries.Add(GoodRegistry);
        using (CreateElement(configuration))
        {
        }
    }

    /// <summary>
    /// The element refuses a configured registry address that is not
    /// HTTPS, carries user information or is a private address.
    /// </summary>
    [DataTestMethod]
    [DataRow("http://registry.example.com/agents.txt")]
    [DataRow("https://user@registry.example.com/agents.txt")]
    [DataRow("https://user:secret@registry.example.com/agents.txt")]
    [DataRow("https://10.0.0.1/agents.txt")]
    [DataRow("not an address")]
    public void Element_UnsafeRegistry_Refused(string url)
    {
        var configuration = new AgentSignatureConfiguration();
        configuration.Registries.Add(url);

        var failure = Assert.ThrowsExactly<ArgumentException>(
            () => CreateElement(configuration).Dispose(),
            $"The element was built with the registry '{url}'.");

        StringAssert.Contains(failure.Message, url);
    }

    /// <summary>
    /// The builder refuses the same registry addresses when they are set.
    /// </summary>
    [DataTestMethod]
    [DataRow("http://registry.example.com/agents.txt")]
    [DataRow("https://user@registry.example.com/agents.txt")]
    [DataRow("https://10.0.0.1/agents.txt")]
    public void Builder_UnsafeRegistry_Refused(string url)
    {
        var builder = new AgentSignatureElementBuilder(
            NullLoggerFactory.Instance);

        Assert.ThrowsExactly<ArgumentException>(
            () => { builder.SetRegistry(url); },
            $"The builder accepted the registry '{url}'.");
    }

    /// <summary>
    /// A registry address is checked before a request is made, so an
    /// address that is not HTTPS, carries user information or is a
    /// private address is never asked for and gives no card addresses.
    /// </summary>
    [DataTestMethod]
    [DataRow("http://registry.example.com/agents.txt")]
    [DataRow("https://user@registry.example.com/agents.txt")]
    [DataRow("https://10.0.0.1/agents.txt")]
    public async Task Fetcher_UnsafeRegistry_NotRequested(string url)
    {
        var handler = new CountingHandler();
        using var client = new HttpClient(handler);
        var fetcher = new DirectoryFetcher(
            client,
            NullLogger.Instance,
            () => DateTimeOffset.UtcNow,
            Constants.DEFAULT_MAX_RESPONSE_BYTES);

        var cards = await fetcher.FetchRegistryAsync(
            url, CancellationToken.None);

        Assert.AreEqual(0, handler.Requests,
            $"The registry '{url}' was requested.");
        Assert.AreEqual(0, cards.Count);
    }

    private static void AssertRefused(string name, TimeSpan period)
    {
        var configuration = new AgentSignatureConfiguration();
        SetPeriod(configuration, name, period);

        var failure = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => CreateElement(configuration).Dispose(),
            $"The element was built with {name} set to {period}.");

        StringAssert.Contains(failure.ParamName, name);
    }

    private static void SetPeriod(
        AgentSignatureConfiguration configuration,
        string name,
        TimeSpan period)
    {
        typeof(AgentSignatureConfiguration)
            .GetProperty(name)
            .SetValue(configuration, period);
    }

    private static AgentSignatureElement CreateElement(
        AgentSignatureConfiguration configuration)
    {
        return new AgentSignatureElement(
            NullLogger<AgentSignatureElement>.Instance,
            (pipeline, element) => new AgentSignatureData(
                NullLogger<AgentSignatureData>.Instance, pipeline),
            configuration);
    }
}
