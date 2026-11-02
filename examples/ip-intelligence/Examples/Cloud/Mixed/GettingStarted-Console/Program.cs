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

using FiftyOne.IpIntelligence.Cloud.FlowElements;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.Configuration;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using FiftyOne.DeviceDetection;
using FiftyOne.DeviceDetection.Cloud.FlowElements;

/// <summary>
/// @example Cloud/Mixed/GettingStarted-Console/Program.cs
///
/// This example demonstrates using both Device Detection and IP Intelligence from the 51Degrees Cloud service.
/// 
/// You will learn:
/// 
/// 1. How to create a Pipeline that uses both 51Degrees Cloud Device Detection and IP Intelligence
/// 2. How to pass input data (User-Agent and IP address) to the Pipeline
/// 3. How to retrieve device and IP intelligence results from a single pipeline
/// 
/// This example is available in full on [GitHub](https://github.com/51Degrees/ip-intelligence-dotnet-examples/blob/main/Examples/Cloud/Mixed/GettingStarted-Console/Program.cs).
///
/// To run this example, create a Resource Key for free at https://configure.51degrees.com?utm_source=code&amp;utm_medium=example&amp;utm_campaign=ip-intelligence-dotnet-examples&amp;utm_content=examples-cloud-mixed-gettingstarted-console-program.cs&amp;utm_term=header and supply it via the appsettings.json file or the 51DEGREES_RESOURCE_KEY environment variable. By default the pipeline talks to cloud.51degrees.com; set 51D_CLOUD_ENDPOINT to point at a self-hosted Cloud service instead.
///
/// Required NuGet Dependencies:
/// - [FiftyOne.IpIntelligence](https://www.nuget.org/packages/FiftyOne.IpIntelligence/)
/// - [Microsoft.Extensions.Configuration.Json](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.Json/)
/// - [Microsoft.Extensions.DependencyInjection](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection/)
/// - [Microsoft.Extensions.Logging.Console](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Console/)
/// </summary>
namespace FiftyOne.IpIntelligence.Examples.Cloud.Mixed.GettingStartedConsole
{
    public class Program
    {
        public class Example
        {
            public void Run(IServiceProvider serviceProvider, TextWriter output)
            {
                var pipelineOptions = serviceProvider.GetRequiredService<PipelineOptions>();
                var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

                // In this example, we use the FiftyOnePipelineBuilder and configure it from a file.
                // For more information about builders in general see the documentation at
                // https://51degrees.com/documentation/_concepts__configuration__builders__index.html?utm_source=code&utm_medium=example&utm_campaign=ip-intelligence-dotnet-examples&utm_content=examples-cloud-mixed-gettingstarted-console-program.cs&utm_term=run

                // Create the pipeline using the service provider and the configured options.
                using (var pipeline = new FiftyOnePipelineBuilder(loggerFactory, serviceProvider)
                    .BuildFromConfiguration(pipelineOptions))
                {
                    // Carry out some sample detections
                    foreach (var values in CombinedEvidence)
                    {
                        AnalyseEvidence(values, pipeline, output);
                    }
                }
            }

            private static readonly List<Dictionary<string, object>> CombinedEvidence =
            [
                // Desktop from UK
                new()
                {
                    {
                        "header.user-agent",
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36"
                    },
                    { "query.client-ip", "82.12.34.23" }
                },
                // Mobile device from China
                new()
                {
                    {
                        "header.user-agent",
                        "Mozilla/5.0 (iPhone; CPU iPhone OS 14_6 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/14.0.3 Mobile/15E148 Safari/604.1"
                    },
                    { "query.client-ip", "1.3.32.31" }
                },
                // Desktop from Chile
                new()
                {
                    {
                        "header.user-agent",
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36"
                    },
                    { "query.client-ip", "45.236.48.61" }
                },
                // Tablet with IPv6
                new()
                {
                    {
                        "header.user-agent",
                        "Mozilla/5.0 (iPad; CPU OS 14_6 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) CriOS/91.0.4472.80 Mobile/15E148 Safari/604.1"
                    },
                    { "query.client-ip", "2001:0db8:085a:0000:0000:8a2e:0370:7334" }
                },
                // Android device from USA
                new()
                {
                    {
                        "header.user-agent",
                        "Mozilla/5.0 (Linux; Android 11; SM-G973F) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.120 Mobile Safari/537.36"
                    },
                    { "query.client-ip", "8.8.8.8" }
                },
            ];

            private void AnalyseEvidence(
                Dictionary<string, object> evidence,
                IPipeline pipeline, 
                TextWriter output)
            {
                // FlowData is used to convey information through the pipeline
                using (var data = pipeline.CreateFlowData())
                {
                    StringBuilder message = new StringBuilder();

                    // List the evidence
                    message.AppendLine("=" + new string('=', 78));
                    message.AppendLine("Input values:");
                    foreach (var entry in evidence)
                    {
                        message.AppendLine($"\t{entry.Key}: {entry.Value}");
                    }
                    output.WriteLine(message.ToString());

                    // Add the evidence values to the flow data
                    data.AddEvidence(evidence);

                    // Process the flow data - both engines will run in parallel
                    data.Process();

                    message = new StringBuilder();
                    
                    // Get Device Detection results
                    message.AppendLine("\nDevice Detection Results:");
                    message.AppendLine("-" + new string('-', 24));
                    var device = data.Get<IDeviceData>();
                    OutputDeviceData(device, message);
                    
                    // Get IP Intelligence results
                    message.AppendLine("\nIP Intelligence Results:");
                    message.AppendLine("-" + new string('-', 23));
                    var ipData = data.Get<IIpIntelligenceData>();
                    OutputIpData(ipData, message);
                    
                    output.WriteLine(message.ToString());
                }
            }

            private void OutputDeviceData(IDeviceData device, StringBuilder message)
            {
                OutputProperty("Mobile Device", device.IsMobile, message);
                OutputProperty("Platform Name", device.PlatformName, message);
                OutputProperty("Platform Version", device.PlatformVersion, message);
                OutputProperty("Browser Name", device.BrowserName, message);
                OutputProperty("Browser Version", device.BrowserVersion, message);
                OutputProperty("Hardware Name", device.HardwareName, message);
                OutputProperty("Hardware Vendor", device.HardwareVendor, message);
                OutputProperty("Device Type", device.DeviceType, message);
                OutputProperty("Screen Width", device.ScreenPixelsWidth, message);
                OutputProperty("Screen Height", device.ScreenPixelsHeight, message);
            }

            private void OutputIpData(IIpIntelligenceData ipData, StringBuilder message)
            {
                OutputProperty("Country", ipData.Country, message);
                OutputProperty("Country Code", ipData.CountryCode, message);
                OutputProperty("Region", ipData.Region, message);
                OutputProperty("State", ipData.State, message);
                OutputProperty("Town", ipData.Town, message);
                OutputProperty("Latitude", ipData.Latitude, message);
                OutputProperty("Longitude", ipData.Longitude, message);
                OutputProperty("Registered Name", ipData.RegisteredName, message);
                OutputProperty("Registered Owner", ipData.RegisteredOwner, message);
                OutputProperty("Registered Country", ipData.RegisteredCountry, message);
                OutputProperty("IP Range Start", ipData.IpRangeStart, message);
                OutputProperty("IP Range End", ipData.IpRangeEnd, message);
                OutputProperty("Accuracy Radius", ipData.AccuracyRadiusMin, message);
                OutputProperty("Time Zone Offset", ipData.TimeZoneOffset, message);
            }

            private void OutputProperty<T>(string name,
                IAspectPropertyValue<T> property,
                StringBuilder message)
            {
                if (property == null)
                {
                    message.AppendLine($"\t{name}: (not available)");
                }
                else if (!property.HasValue)
                {
                    message.AppendLine($"\t{name}: {property.NoValueMessage}");
                }
                else
                {
                    message.AppendLine($"\t{name}: {property.Value}");
                }
            }

            /// <summary>
            /// This Run method is called by the example test to avoid the need to duplicate the 
            /// service provider setup logic.
            /// </summary>
            /// <param name="options"></param>
            public void Run(PipelineOptions options, TextWriter output)
            {
                // Initialize a service collection which will be used to create the services
                // required by the Pipeline and manage their lifetimes.
                using (var serviceProvider = new ServiceCollection()
                    // Add the configuration to the services collection.
                    .AddSingleton(options)
                    // Make sure we're logging to the console.
                    .AddLogging(l => l.AddConsole())
                    // Add an HttpClient instance. This is used for making requests to the
                    // Cloud service.
                    .AddSingleton<HttpClient>()
                    // Add the builders that will be needed to create the engines specified in the 
                    // configuration file.
                    .AddSingleton<CloudRequestEngineBuilder>()
                    .AddSingleton<IpiCloudEngineBuilder>()
                    .AddSingleton<DeviceDetectionCloudEngineBuilder>()
                    .BuildServiceProvider())
                {
                    // Get the resource key setting from the config file. 
                    var resourceKey = options.GetResourceKey();

                    // If we don't have a resource key then log an error.
                    if (string.IsNullOrWhiteSpace(resourceKey))
                    {
                        serviceProvider.GetRequiredService<ILogger<Program>>().LogError(
                            $"No resource key specified in the configuration file " +
                            $"'appsettings.json' or the environment variable " +
                            $"'{ExampleUtils.CLOUD_RESOURCE_KEY_ENV_VAR}'. The 51Degrees Cloud " +
                            $"service is accessed using a 'ResourceKey'. For more information " +
                            $"see " +
                            $"https://51degrees.com/documentation/_info__resource_keys.html?utm_source=code&utm_medium=example&utm_campaign=ip-intelligence-dotnet-examples&utm_content=examples-cloud-mixed-gettingstarted-console-program.cs&utm_term=resource-key-required. " +
                            $"A resource key with the properties required by this example can be " +
                            $"created for free at https://configure.51degrees.com/1QWJwHxl?utm_source=code&utm_medium=example&utm_campaign=ip-intelligence-dotnet-examples&utm_content=examples-cloud-mixed-gettingstarted-console-program.cs&utm_term=resource-key-required. " +
                            $"Once complete, populate the config file or environment variable " +
                            $"mentioned at the start of this message with the key.");
                    }
                    else
                    {
                        new Example().Run(serviceProvider, output);
                    }
                }
            }
        }

        static void Main(string[] args)
        {
            // Use the command line args to get the resource key if present.
            // Otherwise, get it from the environment variable.
            string resourceKey = args.Length > 0 ? args[0] :
                ExampleUtils.GetResourceKeyFromEnv();

            // Load the configuration file
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            // Bind the configuration to a pipeline options instance
            PipelineOptions options = new PipelineOptions();
			var section = config.GetRequiredSection("PipelineOptions");
            // Use the 'ErrorOnUnknownConfiguration' option to warn us if we've got any
            // misnamed configuration keys.
            section.Bind(options, (o) => { o.ErrorOnUnknownConfiguration = true; });

            // Get the resource key setting from the config file. 
            var resourceKeyFromConfig = options.GetResourceKey();
            var configHasKey = string.IsNullOrWhiteSpace(resourceKeyFromConfig) == false &&
                    resourceKeyFromConfig.StartsWith("!!") == false;

            // If no resource key is specified in the config file then override it with the key
            // from the environment variable / command line. 
            if (configHasKey == false)
            {
                options.SetResourceKey(resourceKey);
            }

            new Example().Run(options, Console.Out);
        }
    }
}
