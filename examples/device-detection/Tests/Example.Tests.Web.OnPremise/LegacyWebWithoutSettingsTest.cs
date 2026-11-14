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
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using IntegratedProgram = Client_Hints.Program;
using NotIntegratedProgram = Client_Hints_Not_Integrated.Program;

namespace FiftyOne.DeviceDetection.Example.Tests.Web.OnPremise;

/// <summary>
/// Runs each Legacy Web example as someone who has just cloned the
/// repository does, from its own folder with none of the settings the other
/// tests are given, and asks it for its page. So the examples use the free
/// Lite data file that the data repository links, and these tests check
/// that each page is served, shows the properties only the Enterprise data
/// file has as needing it, links to the contact page and ends by saying
/// where to find out more.
/// </summary>
[TestClass]
[DoNotParallelize]
public class LegacyWebWithoutSettingsTest
{
    /// <summary>
    /// The start of the link each page gives to ask about the paid data
    /// file.
    /// </summary>
    private const string ContactUs = "https://51degrees.com/contact-us?";

    /// <summary>
    /// How long one request to an example is given, which includes building
    /// its pipeline.
    /// </summary>
    private const int RequestTimeoutSeconds = 120;

    /// <summary>
    /// The address the examples are told to listen on.
    /// </summary>
    private static readonly string Url =
        $"http://localhost:{Constants.LOCALHOST_HTTP_PORTS[0]}";

    private static readonly string ExamplesPath =
        Path.GetFullPath(
            Path.GetDirectoryName(
                ExampleUtils.FindFile("FiftyOne.DeviceDetection.Examples.sln")));

    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task ClientHints_WithoutSettings()
    {
        AssertPage(await GetPageWithoutSettings(
            "UACH", IntegratedProgram.CreateHostBuilder));
    }

    [TestMethod]
    public async Task ClientHintsNotIntegrated_WithoutSettings()
    {
        AssertPage(await GetPageWithoutSettings(
            "UACH-manual", NotIntegratedProgram.CreateHostBuilder));
    }

    private static void AssertPage(string html)
    {
        StringAssert.Contains(html,
            $"id=\"HardwareVendor\">{ExampleUtils.NEEDS_ENTERPRISE}<",
            "HardwareVendor should be shown as needing the Enterprise data " +
            "file.");
        StringAssert.Contains(html, "id=\"BrowserName\">",
            "The page should show BrowserName.");
        Assert.IsFalse(
            html.Contains(
                $"id=\"BrowserName\">{ExampleUtils.NEEDS_ENTERPRISE}<"),
            "The Lite data file has BrowserName.");
        StringAssert.Contains(html, ContactUs,
            "The page should link to the contact page.");
        StringAssert.Contains(html, "Find out more",
            "The page should end by saying where to find out more.");
    }

    /// <summary>
    /// Starts the example from its own folder with the data file settings
    /// removed, and returns its page.
    /// </summary>
    private async Task<string> GetPageWithoutSettings(
        string folder,
        Func<string[], IHostBuilder> createHostBuilder)
    {
        // The example reads appsettings.json and finds its views from the
        // current directory. It builds its pipeline for the first request,
        // so the settings stay out of the way until the page has come back.
        var currentDirectory = Directory.GetCurrentDirectory();
        var saved = Constants.DATA_FILE_ENV_VARS.ToDictionary(
            name => name,
            Environment.GetEnvironmentVariable);
        Directory.SetCurrentDirectory(
            Path.Combine(ExamplesPath, "Legacy Web", folder));
        try
        {
            foreach (var name in Constants.DATA_FILE_ENV_VARS)
            {
                Environment.SetEnvironmentVariable(name, null);
            }
            using var host = createHostBuilder(
                new[] { "--urls", Url }).Build();
            await host.StartAsync();
            try
            {
                using var client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(RequestTimeoutSeconds)
                };
                using var response = await client.GetAsync(Url);
                var html = await response.Content.ReadAsStringAsync();
                TestContext.WriteLine(html);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
                    "The example should serve its page.");
                return html;
            }
            finally
            {
                await host.StopAsync();
            }
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
            foreach (var entry in saved)
            {
                Environment.SetEnvironmentVariable(entry.Key, entry.Value);
            }
        }
    }
}
