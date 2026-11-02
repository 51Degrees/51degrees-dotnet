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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WebProgram = FiftyOne.IpIntelligence.Examples.Cloud.GettingStartedWeb.Program;

namespace FiftyOne.IpIntelligence.Example.Tests.Cloud
{
    /// <summary>
    /// Runs the Cloud GettingStarted-Web example in-process (via its
    /// <c>Program.Run</c> entry point, which the example exposes specifically
    /// for tests) against the real 51Degrees Cloud service, then drives it over
    /// HTTP exactly as a browser would and asserts the rendered page contains
    /// the expected IP Intelligence results for a known IP address.
    /// </summary>
    /// <remarks>
    /// Requires the <c>51DEGREES_RESOURCE_KEY</c> (or legacy
    /// <c>SUPER_RESOURCE_KEY</c>) environment variable to be set. It also
    /// requires a trusted HTTPS development certificate, because the example
    /// binds its configured HTTPS endpoints on start-up (run
    /// <c>dotnet dev-certs https --trust</c> once if start-up fails with a
    /// certificate error). The example resolves its configuration and views
    /// relative to the current directory, so the test runs it from the
    /// example's own directory.
    /// </remarks>
    // The web example tests all bind the same localhost port and switch the
    // process-wide current directory, so they cannot run in parallel with
    // each other.
    [TestClass]
    [DoNotParallelize]
    public class TestCloudWebExample
    {
        // A public IPv4 address known to resolve to the United Kingdom - the
        // same address the console Cloud example asserts against.
        private const string KnownUkIpV4 = "82.12.34.23";

        // The example binds Constants.AllUrls; this is its plain-HTTP endpoint,
        // which is the one we query (avoiding the HTTPS ports keeps the request
        // side certificate-free).
        private const string BaseUrl = "http://localhost:5101";

        private static readonly string RepoRootPath =
            Path.GetFullPath(
                Path.GetDirectoryName(
                    ExampleUtils.FindFile("FiftyOne.IpIntelligence.Examples.sln"))!);

        private static readonly string WebExampleDir =
            Path.Combine(RepoRootPath, "Examples", "Cloud", "GettingStarted-Web");

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
        /// The web example, when queried for a known UK IP address, should
        /// render the detection-results page containing that IP's expected
        /// registered name, country and containing IP range.
        /// </summary>
        [TestMethod]
        public async Task Example_Cloud_GettingStartedWeb()
        {
            // The example reads appsettings_51.json and locates its Razor views
            // relative to the current directory, so host it from its own folder.
            _originalCurrentDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(WebExampleDir);

            _cts = new CancellationTokenSource();
            _serverTask = WebProgram.Run(Array.Empty<string>(), _cts.Token);

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            await WaitUntilListeningAsync(client, TimeSpan.FromSeconds(30));

            var html = await client.GetStringAsync($"{BaseUrl}/?ipAddress={KnownUkIpV4}");

            Assert.Contains("Detection results", html,
                "Page should contain the detection results section.");
            Assert.Contains(KnownUkIpV4, html,
                "Page should echo back the queried IP address.");
            Assert.IsTrue(html.Contains("United Kingdom") || html.Contains("GB"),
                $"Page should report the country for {KnownUkIpV4} as United Kingdom / GB.");
            Assert.Contains("VMCBBUK", html,
                "Page should contain the expected registered name for the queried IP.");
            Assert.IsTrue(html.Contains("82.12.34.0") || html.Contains("82.12.34.255"),
                "Page should contain the IP range that bounds the queried IP.");
        }

        /// <summary>
        /// Poll the example's HTTP endpoint until it serves a response, failing
        /// loudly (with the underlying cause) if the server faults on start-up
        /// or does not come up within <paramref name="timeout"/>.
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
                    using var response = await client.GetAsync(BaseUrl);
                    return;
                }
                catch (HttpRequestException)
                {
                    // Server not accepting connections yet - retry.
                    await Task.Delay(250);
                }
            }

            Assert.Fail(
                $"The GettingStarted-Web example did not start listening on " +
                $"{BaseUrl} within {timeout.TotalSeconds:0} seconds.");
        }
    }
}
