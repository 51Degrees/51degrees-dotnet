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
using System.Threading;
using System.Threading.Tasks;
using FiftyOne.DeviceDetection.Hash.Engine.OnPremise.FlowElements;
using FiftyOne.IpIntelligence.Engine.OnPremise.FlowElements;
using FiftyOne.IpIntelligence.Examples.OnPremise;
using FiftyOne.Pipeline.Core.Configuration;
using FiftyOne.Pipeline.Web.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

/// <summary>
/// @example OnPremise/Mixed/GettingStarted-Web/Program.cs
/// 
/// This example demonstrates using both Device Detection and IP Intelligence 
/// engines in a single web application pipeline. The engines process evidence 
/// in parallel for optimal performance.
/// 
/// This example is available in full on [GitHub](https://github.com/51Degrees/ip-intelligence-dotnet-examples/blob/main/Examples/OnPremise/Mixed/GettingStarted-Web/Program.cs). 
/// 
/// Required data files:
/// - Device Detection data file (.hash format)
/// - IP Intelligence data file (.ipi format)
/// 
/// The paths to the data files should be provided as command line parameters
/// 
/// Required NuGet Dependencies:
/// - [FiftyOne.DeviceDetection](https://www.nuget.org/packages/FiftyOne.DeviceDetection/)
/// - [FiftyOne.IpIntelligence](https://www.nuget.org/packages/FiftyOne.IpIntelligence/)
/// - [Microsoft.AspNetCore.App](https://www.nuget.org/packages/Microsoft.AspNetCore.App/)
/// </summary>
namespace FiftyOne.IpIntelligence.Examples.Mixed.OnPremise.GettingStartedWeb
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Start the server and then wait for the task to finish.
            Run(args).Wait();
        }

        /// <summary>
        /// Used by unit tests to run the example in an almost identical manner
        /// to a developer using the example. Returns the task that the web 
        /// server is running in so that the test can trigger the cancellation
        /// token and then wait for the server to shutdown before finishing.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="stopToken"></param>
        /// <returns></returns>
        public static Task Run(
            string[] args,
            CancellationToken stopToken = default)
        {
            var ddDataFileOverride = args.Length > 0 ? args[0] : null;
            var ipiDataFileOverride = args.Length > 1 ? args[1] : null;
            var configOverrides = CreateConfigOverrides(ddDataFileOverride, ipiDataFileOverride);
            return CreateHostBuilder(configOverrides, args).Build().RunAsync(
                stopToken);
        }

        public static IHostBuilder CreateHostBuilder(
            IDictionary<string, string> overrides, string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .ConfigureAppConfiguration(config =>
                        {
                            config.AddJsonFile("appsettings.json")
                                .AddInMemoryCollection(overrides);
                        })
                        .UseStartup<Startup>()
                        .UseUrls(Constants.AllUrls);
                });

        /// <summary>
        /// Typically, something like this will not be necessary.
        /// The IP Intelligence API will accept an absolute or relative path for the data file.
        /// However, if a relative path is specified, it will only look in the current working 
        /// directory.
        /// In our examples, we have many different projects and we don't want to have a copy of 
        /// the data file for every single one.
        /// In order to handle this, we dynamically search the project directories for the data 
        /// file location and then override the configured setting with the absolute path if 
        /// necessary.
        /// In a real-world scenario, you can just put the data file in your working directory
        /// or use an absolute path in the configuration file.
        /// </summary>
        private static Dictionary<string, string> CreateConfigOverrides(
            string ddDataFileOverride = null,
            string ipiDataFileOverride = null)
        {
            var overrides = new Dictionary<string, string>();

            // Load the configuration file
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();
            // Bind the configuration to a pipeline options instance
            PipelineOptions options = new PipelineWebIntegrationOptions();
            var section = config.GetRequiredSection("PipelineOptions");
            // Use the 'ErrorOnUnknownConfiguration' option to warn us if we've got any
            // misnamed configuration keys.
            section.Bind(options, (o) => { o.ErrorOnUnknownConfiguration = true; });

            AddOverrides_IPI(options, overrides, ipiDataFileOverride);
            AddOverrides_DD(options, overrides, ddDataFileOverride);

            return overrides;
        }

        private static void AddOverrides_IPI(PipelineOptions options, Dictionary<string, string> overrides,
            string dataFileOverride = null)
        {
            // Get the index of the IP Intelligence engine element in the config file so that
            // we can create an override key for it.
            var ipiEngineOptions = options.GetElementConfig(nameof(IpiOnPremiseEngine));
            var ipiEngineIndex = options.Elements.IndexOf(ipiEngineOptions);
            var dataFileConfigKey = $"PipelineOptions:Elements:{ipiEngineIndex}" +
                                    $":BuildParameters:DataFile";

            // Use the command line argument if provided. Otherwise, an explicit data file
            // path supplied in an environment variable takes precedence over the value in
            // the configuration file. The aligned '51DEGREES_IPI_PATH' variable is checked
            // first, followed by the legacy 'IPINTELLIGENCEDATAFILE' variable.
            var dataFile = dataFileOverride;
            if (string.IsNullOrWhiteSpace(dataFile))
            {
                dataFile = Environment.GetEnvironmentVariable(
                    Examples.Constants.IP_INTELLIGENCE_DATA_FILE_ENV_VAR);
            }
            if (string.IsNullOrWhiteSpace(dataFile))
            {
                dataFile = Environment.GetEnvironmentVariable(
                    Examples.Constants.LEGACY_IP_INTELLIGENCE_DATA_FILE_ENV_VAR);
            }
            if (string.IsNullOrWhiteSpace(dataFile))
            {
                dataFile = options.GetIpiDataFile();
            }
            var foundDataFile = false;
            if (string.IsNullOrEmpty(dataFile))
            {
                throw new Exception($"A data file must be specified as a command line " +
                    $"argument, in the appsettings.json file, or in the " +
                    $"'{Examples.Constants.IP_INTELLIGENCE_DATA_FILE_ENV_VAR}' environment " +
                    $"variable.");
            }
            // The data file location provided in the configuration may be using an absolute or
            // relative path. If it is relative then search for a matching file using the 
            // ExampleUtils.FindFile function.
            if (Path.IsPathRooted(dataFile) == false)
            {
                var newPath = Examples.ExampleUtils.FindFile(dataFile);
                if (newPath != null)
                {
                    // Add an override for the absolute path to the data file.
                    overrides.Add(dataFileConfigKey, newPath);
                    foundDataFile = true;
                }
            }
            else
            {
                if (File.Exists(dataFile))
                {
                    overrides[dataFileConfigKey] = dataFile;
                    foundDataFile = true;
                }
            }

            if (foundDataFile == false)
            {
                throw new Exception($"Failed to find a IP Intelligence data file matching " +
                                    $"'{dataFile}'. If using the lite file, then make sure the " +
                                    $"ip-intelligence-data submodule has been updated by running " +
                                    "`git submodule update --recursive`. Otherwise, ensure that the filename " +
                                    "is correct in appsettings.json.");
            }
        }

        private static void AddOverrides_DD(PipelineOptions options, Dictionary<string, string> overrides,
            string dataFileOverride = null)
        {
            // Get the index of the device detection engine element in the config file so that
            // we can create an override key for it.
            var hashEngineOptions = options.GetElementConfig(nameof(DeviceDetectionHashEngine));
            var hashEngineIndex = options.Elements.IndexOf(hashEngineOptions);
            var dataFileConfigKey = $"PipelineOptions:Elements:{hashEngineIndex}" +
                                    $":BuildParameters:DataFile";

            // Use the command line argument if provided, otherwise use the appsettings.json value.
            var dataFile = dataFileOverride ?? options.GetHashDataFile();
            var foundDataFile = false;
            if (string.IsNullOrEmpty(dataFile))
            {
                throw new Exception($"A data file must be specified as a command line argument " +
                    $"or in the appsettings.json file.");
            }
            // The data file location provided in the configuration may be using an absolute or
            // relative path. If it is relative then search for a matching file using the
            // ExampleUtils.FindFile function.
            if (Path.IsPathRooted(dataFile) == false)
            {
                var newPath = ExampleUtils.FindFile(dataFile);
                if (newPath != null)
                {
                    // Add an override for the absolute path to the data file.
                    overrides.Add(dataFileConfigKey, newPath);
                    foundDataFile = true;
                }
            }
            else
            {
                if (File.Exists(dataFile))
                {
                    overrides[dataFileConfigKey] = dataFile;
                    foundDataFile = true;
                }
            }

            if (foundDataFile == false)
            {
                throw new Exception($"Failed to find a device detection data file matching " +
                                    $"'{dataFile}'. If using the lite file, then make sure the " +
                                    $"device-detection-data submodule has been updated by running " +
                                    "`git submodule update --recursive`. Otherwise, ensure that the filename " +
                                    "is correct in appsettings.json.");
            }
        }
    }
}