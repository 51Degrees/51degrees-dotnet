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
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.IO;

namespace Cloud_Client_Hints_Not_Integrated;

public class Program
{
    /// <summary>
    /// The setting in appsettings.json that holds the resource key, the
    /// first element of the pipeline being the Cloud Request Engine.
    /// </summary>
    private const string ResourceKeySetting =
        "PipelineOptions:Elements:0:BuildParameters:ResourceKey";

    public static void Main(string[] args)
    {
        var overrides = CreateKeyOverrides();
        if (overrides == null)
        {
            // There is no resource key, so the message says where to create
            // one and the example ends without a server.
            return;
        }
        CreateHostBuilder(args, overrides).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        CreateHostBuilder(args, new Dictionary<string, string>());

    private static IHostBuilder CreateHostBuilder(
        string[] args,
        IDictionary<string, string> overrides) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(config =>
                config.AddInMemoryCollection(overrides))
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            });

    /// <summary>
    /// Gives the pipeline the resource key from the environment when
    /// appsettings.json has none. The host reads appsettings.json from the
    /// current directory, so this does too.
    /// </summary>
    /// <returns>
    /// The settings to add, empty when appsettings.json has a key, or null
    /// when neither has one, after writing where a key is created.
    /// </returns>
    private static Dictionary<string, string> CreateKeyOverrides()
    {
        var settings = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .Build();
        var fromSettings = settings[ResourceKeySetting];
        if (string.IsNullOrWhiteSpace(fromSettings) == false &&
            fromSettings.StartsWith("!!") == false)
        {
            return new Dictionary<string, string>();
        }
        var fromEnvironment = ExampleUtils.GetResourceKeyFromEnv();
        if (string.IsNullOrEmpty(fromEnvironment) == false)
        {
            return new Dictionary<string, string>
            {
                { ResourceKeySetting, fromEnvironment }
            };
        }
        Console.WriteLine(
            "No resource key is set in appsettings.json or in the " +
            $"environment variable '{ExampleUtils.CLOUD_RESOURCE_KEY_ENV_VAR}'. " +
            "The 51Degrees cloud service is accessed with a resource key. " +
            "A free resource key, which populates the free properties, can be " +
            "created at https://51degrees.com/configure/Wkqxf3Bs?utm_source=code&utm_medium=example&utm_campaign=51degrees-dotnet&utm_content=examples-legacy-web-cloud-uach-manual-program.cs&utm_term=resource-key-required " +
            "and with a paid subscription a key created at " +
            "https://51degrees.com/configure/hYzn3TV3?utm_source=code&utm_medium=example&utm_campaign=51degrees-dotnet&utm_content=examples-legacy-web-cloud-uach-manual-program.cs&utm_term=resource-key-required " +
            "includes every property this example uses. Set the environment " +
            "variable to the key, or put the key in appsettings.json, and run " +
            "the example again.");
        return null;
    }
}
