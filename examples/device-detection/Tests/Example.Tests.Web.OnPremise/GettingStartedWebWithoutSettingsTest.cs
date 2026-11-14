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

using FiftyOne.DeviceDetection.Examples;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WebProgram = FiftyOne.DeviceDetection.Examples.OnPremise.GettingStartedWeb.Program;

namespace FiftyOne.DeviceDetection.Example.Tests.Web.OnPremise;

/// <summary>
/// Runs the GettingStarted-Web example as someone who has just cloned the
/// repository does, from its own folder with no arguments and none of the
/// settings the other tests are given, and asks it for its page. So the
/// example uses the free Lite data file that the data repository links, and
/// the test checks that the page is served, shows the properties only the
/// Enterprise data file has as needing it, links to the contact page and
/// ends by saying where to find out more.
/// </summary>
[TestClass]
[DoNotParallelize]
public class GettingStartedWebWithoutSettingsTest
{
    /// <summary>
    /// The start of the link the page gives to ask about the paid data file.
    /// </summary>
    private const string ContactUs = "https://51degrees.com/contact-us?";

    /// <summary>
    /// How long the example is given to start, which includes downloading
    /// the free data file when it is missing.
    /// </summary>
    private const int StartTimeoutSeconds = 300;

    /// <summary>
    /// How long one request to the example is given.
    /// </summary>
    private const int RequestTimeoutSeconds = 60;

    /// <summary>
    /// The wait between attempts to reach the example while it starts.
    /// </summary>
    private const int RetryDelayMilliseconds = 250;

    /// <summary>
    /// The plain HTTP address the example listens on.
    /// </summary>
    private static readonly string Url =
        $"http://localhost:{Constants.LOCALHOST_HTTP_PORTS[0]}";

    private static readonly string ExampleFolder =
        Path.Combine(
            Path.GetFullPath(
                Path.GetDirectoryName(
                    ExampleUtils.FindFile(
                        "FiftyOne.DeviceDetection.Examples.sln"))),
            "OnPremise",
            "GettingStarted-Web");

    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task GettingStartedWeb_WithoutSettings()
    {
        // The example reads appsettings.json and finds its views from the
        // current directory, and chooses its data file before its server
        // starts.
        var currentDirectory = Directory.GetCurrentDirectory();
        var names = Constants.DATA_FILE_ENV_VARS
            .Append("ASPNETCORE_URLS")
            .ToArray();
        var saved = names.ToDictionary(
            name => name,
            Environment.GetEnvironmentVariable);
        using var stop = new CancellationTokenSource();
        Task server = null;
        Directory.SetCurrentDirectory(ExampleFolder);
        try
        {
            foreach (var name in names)
            {
                Environment.SetEnvironmentVariable(name, null);
            }
            server = WebProgram.Run(Array.Empty<string>(), stop.Token);

            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(RequestTimeoutSeconds)
            };
            var html = await GetPage(client, server);
            TestContext.WriteLine(html);

            StringAssert.Contains(html,
                "Hardware Vendor:</td><td class=\"c-eg-table__cell\">" +
                $"{ExampleUtils.NEEDS_ENTERPRISE}</td>",
                "HardwareVendor should be shown as needing the Enterprise " +
                "data file.");
            Assert.IsFalse(
                html.Contains(
                    "Browser Name:</td><td class=\"c-eg-table__cell\">" +
                    $"{ExampleUtils.NEEDS_ENTERPRISE}</td>"),
                "The Lite data file has BrowserName.");
            StringAssert.Contains(html, ContactUs,
                "The page should link to the contact page.");
            StringAssert.Contains(html, "Find out more",
                "The page should end by saying where to find out more.");
        }
        finally
        {
            stop.Cancel();
            if (server != null)
            {
                // The test body has already reported any fault of the
                // server.
                try { await server; }
                catch { }
            }
            Directory.SetCurrentDirectory(currentDirectory);
            foreach (var entry in saved)
            {
                Environment.SetEnvironmentVariable(entry.Key, entry.Value);
            }
        }
    }

    /// <summary>
    /// Asks the example for its page until it answers. A server that stops
    /// while it is starting is awaited, so that the test fails with its
    /// error.
    /// </summary>
    private static async Task<string> GetPage(HttpClient client, Task server)
    {
        var deadline = DateTime.UtcNow.AddSeconds(StartTimeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            if (server.IsCompleted)
            {
                await server;
                Assert.Fail("The example stopped before it served a page.");
            }
            try
            {
                using var response = await client.GetAsync(Url);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
                    "The example should serve its page.");
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException)
            {
                await Task.Delay(RetryDelayMilliseconds);
            }
        }
        Assert.Fail(
            $"The example did not answer on {Url} within " +
            $"{StartTimeoutSeconds} seconds.");
        return null;
    }
}
