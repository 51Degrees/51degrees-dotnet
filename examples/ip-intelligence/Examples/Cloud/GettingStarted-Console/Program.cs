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

/// <summary>
/// @example Cloud/GettingStarted-Console/Program.cs
///
/// This example shows how to use 51Degrees Cloud IP Intelligence to determine location and network details from IP addresses.
/// 
/// You will learn:
/// 
/// 1. How to create a Pipeline that uses 51Degrees Cloud IP Intelligence
/// 2. How to pass input data (evidence) to the Pipeline
/// 3. How to retrieve the results
/// 
/// This example is available in full on [GitHub](https://github.com/51Degrees/ip-intelligence-dotnet-examples/blob/main/Examples/Cloud/GettingStarted-Console/Program.cs).
///
/// To run this example, create a Resource Key for free at https://configure.51degrees.com?utm_source=code&amp;utm_medium=example&amp;utm_campaign=ip-intelligence-dotnet-examples&amp;utm_content=examples-cloud-gettingstarted-console-program.cs&amp;utm_term=header and supply it via the appsettings.json file or the 51DEGREES_RESOURCE_KEY environment variable. By default the pipeline talks to cloud.51degrees.com; set 51D_CLOUD_ENDPOINT to point at a self-hosted Cloud service instead.
///
/// Required NuGet Dependencies:
/// - [FiftyOne.IpIntelligence](https://www.nuget.org/packages/FiftyOne.IpIntelligence/)
/// - [Microsoft.Extensions.Configuration.Json](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.Json/)
/// - [Microsoft.Extensions.DependencyInjection](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection/)
/// - [Microsoft.Extensions.Logging.Console](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Console/)
/// </summary>
namespace FiftyOne.IpIntelligence.Examples.Cloud.GettingStartedConsole
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
                // https://51degrees.com/documentation/_concepts__configuration__builders__index.html?utm_source=code&utm_medium=example&utm_campaign=ip-intelligence-dotnet-examples&utm_content=examples-cloud-gettingstarted-console-program.cs&utm_term=run

                // Create the pipeline using the service provider and the configured options.
                using (var pipeline = new FiftyOnePipelineBuilder(loggerFactory, serviceProvider)
                    .BuildFromConfiguration(pipelineOptions))
                {
                    // Carry out some sample detections
                    foreach (var values in ExampleUtils.EvidenceValues)
                    {
                        AnalyseEvidence(values, pipeline, output);
                    }
                }
            }

            private void AnalyseEvidence(
                Dictionary<string, object> evidence,
                IPipeline pipeline,
                TextWriter output)
            {
                // FlowData is a data structure that is used to convey information required for
                // detection and the results of the detection through the pipeline.
                // Information required for detection is called "evidence" and usually consists
                // of a number of HTTP Header field values, in this case represented by a
                // Dictionary<string, object> of header name/value entries.
                //
                // FlowData is wrapped in a using block in order to ensure that the resources
                // are freed in a timely manner.
                using (var data = pipeline.CreateFlowData())
                {
                    StringBuilder message = new StringBuilder();

                    // list the evidence
                    message.AppendLine("Input values:");
                    foreach (var entry in evidence)
                    {
                        message.AppendLine($"\t{entry.Key}: {entry.Value}");
                    }
                    output.WriteLine(message.ToString());

                    // Add the evidence values to the flow data
                    data.AddEvidence(evidence);

                    // Process the flow data.
                    data.Process();

                    message = new StringBuilder();
                    message.AppendLine("Results:");

                    // Now that it's been processed, the flow data will have been populated with
                    // the result. In this case, we want information about the IP address, which we
                    // can get by asking for a result matching the `IIpIntelligenceData` interface.
                    var ipData = data.Get<IIpIntelligenceData>();

                    // Output all the properties
                    OutputProperty(nameof(ipData.RegisteredName), ipData.RegisteredName, message);
                    OutputProperty(nameof(ipData.RegisteredOwner), ipData.RegisteredOwner, message);
                    OutputProperty(nameof(ipData.RegisteredCountry), ipData.RegisteredCountry, message);
                    OutputProperty(nameof(ipData.IpRangeStart), ipData.IpRangeStart, message);
                    OutputProperty(nameof(ipData.IpRangeEnd), ipData.IpRangeEnd, message);
                    OutputProperty(nameof(ipData.Country), ipData.Country, message);
                    OutputProperty(nameof(ipData.CountryCode), ipData.CountryCode, message);
                    OutputProperty(nameof(ipData.CountryCode3), ipData.CountryCode3, message);
                    OutputProperty(nameof(ipData.Region), ipData.Region, message);
                    OutputProperty(nameof(ipData.State), ipData.State, message);
                    OutputProperty(nameof(ipData.Town), ipData.Town, message);
                    OutputProperty(nameof(ipData.Latitude), ipData.Latitude, message);
                    OutputProperty(nameof(ipData.Longitude), ipData.Longitude, message);
                    OutputProperty(nameof(ipData.Areas), ipData.Areas, message);
                    OutputProperty(nameof(ipData.AccuracyRadiusMin), ipData.AccuracyRadiusMin, message);
                    OutputProperty(nameof(ipData.TimeZoneOffset), ipData.TimeZoneOffset, message);
                    output.WriteLine(message.ToString());
                }
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
                            $"https://51degrees.com/documentation/_info__resource_keys.html?utm_source=code&utm_medium=example&utm_campaign=ip-intelligence-dotnet-examples&utm_content=examples-cloud-gettingstarted-console-program.cs&utm_term=resource-key-required. " +
                            $"A resource key with the properties required by this example can be " +
                            $"created for free at https://configure.51degrees.com/1QWJwHxl?utm_source=code&utm_medium=example&utm_campaign=ip-intelligence-dotnet-examples&utm_content=examples-cloud-gettingstarted-console-program.cs&utm_term=resource-key-required. " +
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
