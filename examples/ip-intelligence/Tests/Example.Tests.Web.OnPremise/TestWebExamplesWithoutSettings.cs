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

using FiftyOne.IpIntelligence.Examples;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MixedProgram = FiftyOne.IpIntelligence.Examples.Mixed.OnPremise.GettingStartedWeb.Program;
using WebProgram = FiftyOne.IpIntelligence.Examples.OnPremise.GettingStartedWeb.Program;

// The web examples all bind the same ports and switch the current
// directory of the process, so only one can run at a time.
[assembly: DoNotParallelize]

namespace FiftyOne.IpIntelligence.Example.Tests.Web.OnPremise;

/// <summary>
/// Runs each on-premise web example as someone who has just cloned the
/// repository does, with no arguments and none of the settings the other
/// tests are given, and asks it for its page. So the examples use the free
/// data files that the data repository links, and these tests check that
/// each page is served, shows the properties only the Enterprise data file
/// has as needing it, and links to the contact page.
/// </summary>
[TestClass]
public class TestWebExamplesWithoutSettings
{
    /// <summary>
    /// The environment variables that name a data file, which a fresh clone
    /// does not have.
    /// </summary>
    private static readonly string[] Settings =
        new[]
        {
            Constants.IPI_PATH_ENV_VAR,
            Constants.DD_PATH_ENV_VAR
        }
        .Concat(Constants.OLD_IP_INTELLIGENCE_DATA_FILE_ENV_VARS)
        .Concat(Constants.OLD_DD_PATH_ENV_VARS)
        .ToArray();

    /// <summary>
    /// The start of the link each page gives to ask for a copy of the
    /// Enterprise data file.
    /// </summary>
    private const string ContactUs = "https://51degrees.com/contact-us?";

    /// <summary>
    /// What a page shows in place of a value the data file does not have.
    /// </summary>
    private const string NeedsEnterprise =
        Examples.OnPremise.ExampleUtils.NEEDS_ENTERPRISE;

    /// <summary>
    /// A public address to look up, so that the registration properties
    /// have values.
    /// </summary>
    private const string PublicIp = "82.12.34.23";

    /// <summary>
    /// How long an example is given to start, which includes downloading a
    /// free data file that is missing.
    /// </summary>
    private const int StartTimeoutSeconds = 600;

    /// <summary>
    /// How long one request to an example is given.
    /// </summary>
    private const int RequestTimeoutSeconds = 60;

    /// <summary>
    /// The wait between attempts to reach an example that is starting.
    /// </summary>
    private const int RetryDelayMilliseconds = 250;

    /// <summary>
    /// The plain HTTP address the examples listen on.
    /// </summary>
    private static readonly string BaseUrl =
        $"http://localhost:{Constants.LOCALHOST_HTTP_PORTS[0]}";

    private static readonly string ExamplesPath =
        Path.GetFullPath(
            Path.GetDirectoryName(
                ExampleUtils.FindFile("FiftyOne.IpIntelligence.Examples.sln"))!);

    private string _originalCurrentDirectory;
    private CancellationTokenSource _stop;
    private Task _server;

    public TestContext TestContext { get; set; }

    [TestCleanup]
    public async Task Cleanup()
    {
        _stop?.Cancel();
        if (_server != null)
        {
            // The test body has already reported any fault of the server.
            try { await _server; }
            catch { }
        }
        if (_originalCurrentDirectory != null)
        {
            Directory.SetCurrentDirectory(_originalCurrentDirectory);
        }
        _stop?.Dispose();
    }

    [TestMethod]
    public async Task GettingStartedWeb_WithoutSettings()
    {
        var html = await GetPageWithoutSettings(
            Path.Combine(ExamplesPath, "OnPremise", "GettingStarted-Web"),
            WebProgram.Run);
        Assert.Contains(
            "IP Range Start:</td><td class=\"c-eg-table__cell\">" +
            $"{NeedsEnterprise}</td>",
            html,
            "IpRangeStart should be shown as needing the Enterprise data file.");
        Assert.DoesNotContain(
            "Registered Name:</td><td class=\"c-eg-table__cell\">" +
            $"{NeedsEnterprise}</td>",
            html,
            "The Lite data file has RegisteredName.");
        Assert.Contains(ContactUs, html,
            "The page should link to the contact page.");
        Assert.Contains("Find out more", html,
            "The page should end by saying where to find out more.");
    }

    [TestMethod]
    public async Task MixedGettingStartedWeb_WithoutSettings()
    {
        var html = await GetPageWithoutSettings(
            Path.Combine(
                ExamplesPath, "OnPremise", "Mixed", "GettingStarted-Web"),
            MixedProgram.Run);
        Assert.Contains(
            $"id=\"HardwareVendor\">{NeedsEnterprise}<", html,
            "HardwareVendor should be shown as needing the Enterprise data " +
            "file.");
        Assert.Contains(
            $"id=\"Country\">{NeedsEnterprise}<", html,
            "Country should be shown as needing the Enterprise data file.");
        Assert.Contains("id=\"RegisteredName\">", html,
            "The page should show RegisteredName.");
        Assert.DoesNotContain(
            $"id=\"RegisteredName\">{NeedsEnterprise}<", html,
            "The Lite data file has RegisteredName.");
        Assert.Contains(ContactUs, html,
            "The page should link to the contact page.");
        Assert.Contains("Find out more", html,
            "The page should end by saying where to find out more.");
    }

    /// <summary>
    /// Starts the example from its own folder with no arguments and the
    /// settings removed, and returns its page for a public address.
    /// </summary>
    private async Task<string> GetPageWithoutSettings(
        string exampleFolder,
        Func<string[], CancellationToken, Task> run)
    {
        // The example reads appsettings.json and finds its views from the
        // current directory.
        _originalCurrentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(exampleFolder);
        _stop = new CancellationTokenSource();

        // The example chooses its data files before its server starts, so
        // the settings are only needed out of the way until then.
        var saved = Settings.ToDictionary(
            name => name,
            Environment.GetEnvironmentVariable);
        try
        {
            foreach (var name in Settings)
            {
                Environment.SetEnvironmentVariable(name, null);
            }
            _server = run(Array.Empty<string>(), _stop.Token);
        }
        finally
        {
            foreach (var entry in saved)
            {
                Environment.SetEnvironmentVariable(entry.Key, entry.Value);
            }
        }

        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(RequestTimeoutSeconds)
        };
        await WaitUntilListening(client);
        using var response = await client.GetAsync(
            $"{BaseUrl}/?client-ip={PublicIp}",
            TestContext.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(
            TestContext.CancellationToken);
        TestContext.WriteLine(html);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            "The example should serve its page.");
        return html;
    }

    /// <summary>
    /// Waits until the example answers a request. A server that stops while
    /// it is starting is awaited, so that the test fails with its error.
    /// </summary>
    private async Task WaitUntilListening(HttpClient client)
    {
        var deadline = DateTime.UtcNow.AddSeconds(StartTimeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            if (_server.IsCompleted)
            {
                await _server;
                Assert.Fail("The example stopped before it served a page.");
            }
            try
            {
                using var response = await client.GetAsync(
                    BaseUrl, TestContext.CancellationToken);
                return;
            }
            catch (HttpRequestException)
            {
                await Task.Delay(
                    RetryDelayMilliseconds, TestContext.CancellationToken);
            }
        }
        Assert.Fail(
            $"The example did not answer on {BaseUrl} within " +
            $"{StartTimeoutSeconds} seconds.");
    }
}
