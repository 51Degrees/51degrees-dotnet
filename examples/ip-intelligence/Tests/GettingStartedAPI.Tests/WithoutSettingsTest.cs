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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FiftyOne.IpIntelligence.Examples.OnPremise.GettingStartedAPI.Tests;

/// <summary>
/// Runs the example as someone who has just cloned the repository does,
/// from its own folder with no arguments and none of the settings the other
/// tests are given. So it uses the free data files that the data repository
/// links, and these tests check that it starts and answers on them.
/// </summary>
[TestClass]
public class WithoutSettingsTest
{
    /// <summary>
    /// The environment variables that name a data file, which a fresh clone
    /// does not have.
    /// </summary>
    private static readonly string[] Settings =
        new[]
        {
            Examples.Constants.IPI_PATH_ENV_VAR,
            Examples.Constants.DD_PATH_ENV_VAR
        }
        .Concat(Examples.Constants.OLD_IP_INTELLIGENCE_DATA_FILE_ENV_VARS)
        .Concat(Examples.Constants.OLD_DD_PATH_ENV_VARS)
        .ToArray();

    /// <summary>
    /// How long the example is given to stop.
    /// </summary>
    private const int StopTimeoutSeconds = 5;

    private static readonly string ExampleFolder =
        Path.Combine(
            Path.GetFullPath(
                Path.GetDirectoryName(
                    Examples.ExampleUtils.FindFile(
                        "FiftyOne.IpIntelligence.Examples.sln"))!),
            "OnPremise",
            "Mixed",
            "GettingStarted-API");

    private string? _originalCurrentDirectory;
    private Dictionary<string, string?>? _savedSettings;
    private WebApplication? _app;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        // The example reads appsettings.json from the current directory.
        _originalCurrentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(ExampleFolder);
        _savedSettings = Settings.ToDictionary(
            name => name,
            Environment.GetEnvironmentVariable);
        foreach (var name in Settings)
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [TestCleanup]
    public async Task TestCleanup()
    {
        if (_app is not null)
        {
            using var source = new CancellationTokenSource(
                TimeSpan.FromSeconds(StopTimeoutSeconds));
            await _app.StopAsync(source.Token);
            await _app.DisposeAsync();
            _app = null;
        }
        if (_savedSettings is not null)
        {
            foreach (var entry in _savedSettings)
            {
                Environment.SetEnvironmentVariable(entry.Key, entry.Value);
            }
        }
        if (_originalCurrentDirectory is not null)
        {
            Directory.SetCurrentDirectory(_originalCurrentDirectory);
        }
    }

    [TestMethod]
    public async Task Example_WithoutSettings_AnswersForBothEngines()
    {
        _app = new Program().BuildWebApp();
        await _app.StartAsync(TestContext.CancellationToken);

        using var client = new HttpClient();
        client.BaseAddress = new Uri(
            _app.Urls.First().Replace("0.0.0.0", "localhost"));
        var response = await client.GetAsync(
            "/accessibleproperties", TestContext.CancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(
            TestContext.CancellationToken);
        TestContext.WriteLine(json);

        Assert.Contains("\"device\"", json,
            StringComparison.OrdinalIgnoreCase,
            "The answer should list the Device Detection properties.");
        Assert.Contains("\"ip\"", json,
            StringComparison.OrdinalIgnoreCase,
            "The answer should list the IP Intelligence properties.");
    }

    [TestMethod]
    public async Task Example_WithoutSettings_SaysWhatNeedsTheEnterpriseDataFiles()
    {
        var program = new Program();
        _app = program.BuildWebApp();
        await _app.StartAsync(TestContext.CancellationToken);

        var output = new StringWriter();
        program.WriteNotes(
            _app.Services.GetRequiredService<IPipeline>(), output);
        var notes = output.ToString();
        TestContext.WriteLine(notes);

        Assert.Contains("which only the Enterprise data files have", notes,
            "The example should say which properties need the Enterprise " +
            "data files.");
        Assert.Contains("HardwareVendor", notes,
            "HardwareVendor needs the Enterprise Device Detection data file.");
        Assert.Contains("IpRangeStart", notes,
            "IpRangeStart needs the Enterprise IP Intelligence data file.");
        Assert.DoesNotContain("RegisteredName", notes,
            "The Lite data file has RegisteredName.");
        Assert.Contains("https://51degrees.com/contact-us?", notes,
            "The example should link to the contact page.");
        Assert.Contains("Find out more", notes,
            "The example should end by saying where to find out more.");
    }
}
