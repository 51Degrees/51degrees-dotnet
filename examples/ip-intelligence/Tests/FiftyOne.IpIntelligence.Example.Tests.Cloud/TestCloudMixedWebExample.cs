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


// Ignore Spelling: ip

using FiftyOne.IpIntelligence.Examples;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WebProgram = FiftyOne.IpIntelligence.Examples.Mixed.Cloud.GettingStartedWeb.Program;

namespace FiftyOne.IpIntelligence.Example.Tests.Cloud;

/// <summary>
/// Runs the combined (device detection + IP intelligence) Cloud
/// GettingStarted-Web example in-process against the real 51Degrees Cloud
/// service, then drives it over HTTP
///exactly as a browser would and asserts the rendered page contains the
/// combined-results layout.
/// </summary>
/// <remarks>
/// Requires the <c>51DEGREES_RESOURCE_KEY</c> environment variable to be set. 
/// As with the other web example tests it binds the example's configured 
/// HTTP/HTTPS endpoints on start-up, so a trusted HTTPS development 
/// certificate must be present. The page is queried over plain HTTP, because
/// the example reads the visitor's IP from the connection rather than a query
/// string, this test asserts the page renders successfully with the expected 
/// structure rather than a specific geographic result.
/// </remarks>
// The web example tests all bind the same localhost port and switch the
// process-wide current directory, so they cannot run in parallel with
// each other.
[TestClass]
[DoNotParallelize]
public class TestCloudMixedWebExample
{
    // The example binds Constants.AllUrls; this is its plain-HTTP endpoint,
    // which is the one we query (avoiding the HTTPS ports keeps the request
    // side certificate-free).
    private const string BaseUrl = "http://localhost:5101";

    private static readonly string RepoRootPath =
        Path.GetFullPath(
            Path.GetDirectoryName(
                ExampleUtils.FindFile("FiftyOne.IpIntelligence.Examples.sln"))!);

    private static readonly string WebExampleDir =
        Path.Combine(RepoRootPath, "Examples", "Cloud", "Mixed", "GettingStarted-Web");

    private string _originalCurrentDirectory;
    private CancellationTokenSource _cts;
    private Task _serverTask;

    [TestInitialize]
    public void Init()
    {
        var resourceKey = ExampleUtils.GetResourceKeyFromEnv();

        Assert.IsFalse(
            string.IsNullOrWhiteSpace(resourceKey),
            $"Environment variable '{ExampleUtils.CLOUD_RESOURCE_KEY_ENV_VAR}' " +
            $"is not set. Obtain a resource key at " +
            $"https://configure.51degrees.com and export it to run this test.");
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        _cts?.Cancel();
        if (_serverTask != null)
        {
            // Swallow the expected cancellation; any other fault has already
            // been surfaced by the test body.
            try { await _serverTask; }
            catch (OperationCanceledException) { }
            catch { /* surfaced in the test */ }
        }
        if (_originalCurrentDirectory != null)
        {
            Directory.SetCurrentDirectory(_originalCurrentDirectory);
        }
        _cts?.Dispose();
    }

    /// <summary>
    /// The combined web example should serve its results page with both the
    /// device detection and IP intelligence sections rendered.
    /// </summary>
    [TestMethod]
    public async Task Example_Cloud_MixedGettingStartedWeb()
    {
        // The example reads appsettings_51.json and locates its Razor views
        // relative to the current directory, so host it from its own folder.
        _originalCurrentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(WebExampleDir);

        _cts = new CancellationTokenSource();
        _serverTask = WebProgram.Run([], _cts.Token);

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        await WaitUntilListeningAsync(client, TimeSpan.FromSeconds(30));

        using var response = await client.GetAsync(BaseUrl, TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            "The combined web example should return a 200 response.");

        var html = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);

        Assert.Contains("Combined device detection and IP intelligence example", html,
            "Page should render the combined example heading.");
        Assert.Contains("IP intelligence results", html,
            "Page should contain the IP intelligence results section.");
        Assert.Contains("Device detection results", html,
            "Page should contain the device detection results section.");
    }

    /// <summary>
    /// Poll the example's HTTP endpoint until it serves a response, failing
    /// loudly if the server faults on start-up or does not come up within 
    /// <paramref name="timeout"/>.
    /// </summary>
    private async Task WaitUntilListeningAsync(HttpClient client, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            // If the host failed to start (e.g. a missing HTTPS dev
            // certificate), await it so the real exception is thrown rather
            // than letting the test time out with a generic message.
            if (_serverTask.IsCompleted)
            {
                await _serverTask;
            }

            try
            {
                using var response = await client.GetAsync(BaseUrl, TestContext.CancellationToken);
                return;
            }
            catch (HttpRequestException)
            {
                // Server not accepting connections yet - retry.
                await Task.Delay(250, TestContext.CancellationToken);
            }
        }

        Assert.Fail(
            $"The Mixed GettingStarted-Web example did not start listening on " +
            $"{BaseUrl} within {timeout.TotalSeconds:0} seconds.");
    }

    public TestContext TestContext { get; set; }
}
