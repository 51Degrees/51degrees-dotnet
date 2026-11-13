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

using FiftyOne.DeviceDetection;
using FiftyOne.DeviceDetection.Hash.Engine.OnPremise.FlowElements;
using FiftyOne.IpIntelligence.Engine.OnPremise.FlowElements;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;

/// <summary>
/// @example OnPremise/Mixed/GettingStarted-Console/Program.cs
/// 
/// This example demonstrates using both Device Detection and IP Intelligence 
/// engines in a single pipeline. The pipeline runs both engines on the same
/// evidence, one after the other.
/// 
/// This example is available in full on [GitHub](https://github.com/51Degrees/51degrees-dotnet/blob/main/examples/ip-intelligence/OnPremise/Mixed/GettingStarted-Console/Program.cs). 
/// 
/// Required data files:
/// - Device Detection data file (.hash format)
/// - IP Intelligence data file (.ipi format)
///
/// The paths to the data files can be provided as 2 consecutive command line parameters. By
/// default the example uses the free Lite files, which it downloads when they are missing, and
/// shows each property only the enterprise data files have as needing them.
///
/// Required NuGet Dependencies:
/// - [FiftyOne.DeviceDetection](https://www.nuget.org/packages/FiftyOne.DeviceDetection/)
/// - [FiftyOne.IpIntelligence](https://www.nuget.org/packages/FiftyOne.IpIntelligence/)
/// </summary>
namespace FiftyOne.IpIntelligence.Examples.Mixed.OnPremise.GettingStartedConsole
{
    public class Program
    {
        public class Example
        {
            /// <summary>
            /// Where to ask for a copy of the Enterprise data files, tagged
            /// with this example.
            /// </summary>
            private const string ContactUs = "https://51degrees.com/contact-us?utm_source=code&utm_medium=example&utm_campaign=51degrees-dotnet&utm_content=examples-ip-intelligence-onpremise-mixed-gettingstarted-console-program.cs&utm_term=enterprise-data-file";

            /// <summary>
            /// The engines, which say which properties their data files have.
            /// </summary>
            private DeviceDetectionHashEngine _deviceEngine;
            private IpiOnPremiseEngine _ipEngine;

            /// <summary>
            /// True once the example has shown a property that a data file in
            /// use does not have.
            /// </summary>
            private bool _needsEnterprise;

            public void Run(string deviceDataFile, string ipDataFile, ILoggerFactory loggerFactory, TextWriter output)
            {
                _needsEnterprise = false;
                using (var httpClient = new HttpClient())
                {
                    var dataUpdateService = new DataUpdateService(loggerFactory.CreateLogger<DataUpdateService>(), httpClient);
                    
                    // Create Device Detection engine directly from scratch
                    var deviceEngine = new DeviceDetectionHashEngineBuilder(loggerFactory, dataUpdateService)
                        .SetPerformanceProfile(PerformanceProfiles.MaxPerformance)
                        .SetAutoUpdate(false)
                        .SetDataFileSystemWatcher(false)
                        .Build(deviceDataFile, false);
                    _deviceEngine = deviceEngine;

                    // Create IP Intelligence engine directly from scratch
                    var ipEngine = new IpiOnPremiseEngineBuilder(loggerFactory, dataUpdateService)
                        // LowMemory keeps the large IP Intelligence data file on disk
                        // instead of loading it entirely into memory.
                        .SetPerformanceProfile(PerformanceProfiles.LowMemory)
                        .SetAutoUpdate(false)
                        .SetDataFileSystemWatcher(false)
                        .Build(ipDataFile, false);
                    _ipEngine = ipEngine;

                    // Create ShareUsage element for telemetry and usage analytics
                    var shareUsageElement = new ShareUsageBuilder(loggerFactory, httpClient).Build();

                    // Create a raw pipeline and add all elements to it
                    // The pipeline runs the elements one after the other, in the
                    // order they are added
                    using (var pipeline = new PipelineBuilder(loggerFactory)
                        .AddFlowElement(shareUsageElement)  // Add first for early processing
                        .AddFlowElement(deviceEngine)
                        .AddFlowElement(ipEngine)
                        .Build())
                    {
                        // Process combined evidence (User-Agent for device detection, IP for geolocation)
                        var combinedEvidence = GetCombinedEvidence();
                        
                        foreach (var evidence in combinedEvidence)
                        {
                            AnalyseEvidence(evidence, pipeline, output);
                        }

                        if (_needsEnterprise)
                        {
                            Examples.OnPremise.ExampleUtils.WriteEnterpriseNote(
                                output, ContactUs);
                        }

                        // Check data files
                        CheckDataFiles(pipeline, loggerFactory.CreateLogger<Program>());
                    }
                }
            }

            private List<Dictionary<string, object>> GetCombinedEvidence()
            {
                // Create evidence that includes both User-Agent (for device detection)
                // and IP address (for IP intelligence)
                return new List<Dictionary<string, object>>()
                {
                    // Mobile device from China
                    new Dictionary<string, object>()
                    {
                        { "header.user-agent", "Mozilla/5.0 (iPhone; CPU iPhone OS 14_6 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/14.0.3 Mobile/15E148 Safari/604.1" },
                        { "query.client-ip", "62.61.32.31" }
                    },
                    // Desktop from Chile
                    new Dictionary<string, object>()
                    {
                        { "header.user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36" },
                        { "query.client-ip", "45.236.48.61" }
                    },
                    // Tablet with IPv6
                    new Dictionary<string, object>()
                    {
                        { "header.user-agent", "Mozilla/5.0 (iPad; CPU OS 14_6 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) CriOS/91.0.4472.80 Mobile/15E148 Safari/604.1" },
                        { "query.client-ip", "2001:0db8:085a:0000:0000:8a2e:0370:7334" }
                    },
                    // Android device from USA
                    new Dictionary<string, object>()
                    {
                        { "header.user-agent", "Mozilla/5.0 (Linux; Android 11; SM-G973F) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.120 Mobile Safari/537.36" },
                        { "query.client-ip", "8.8.8.8" }
                    }
                };
            }

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

                    // Process the flow data. The pipeline runs both engines, one
                    // after the other.
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
                OutputValue("Mobile Device", device.IsMobile, message);
                OutputValue("Platform Name", device.PlatformName, message);
                OutputValue("Platform Version", device.PlatformVersion, message);
                OutputValue("Browser Name", device.BrowserName, message);
                OutputValue("Browser Version", device.BrowserVersion, message);
                // Only the Enterprise data file has the hardware and device type
                // properties. The free Lite file has the others.
                var properties = _deviceEngine.Properties;
                OutputEnterpriseValue("Hardware Name", nameof(device.HardwareName),
                    properties, () => device.HardwareName, message);
                OutputEnterpriseValue("Hardware Vendor", nameof(device.HardwareVendor),
                    properties, () => device.HardwareVendor, message);
                OutputEnterpriseValue("Device Type", nameof(device.DeviceType),
                    properties, () => device.DeviceType, message);
                OutputValue("Screen Width", device.ScreenPixelsWidth, message);
                OutputValue("Screen Height", device.ScreenPixelsHeight, message);
            }

            private void OutputIpData(IIpIntelligenceData ipData, StringBuilder message)
            {
                // Only the Enterprise data file has the location and range
                // properties. The free Lite file has the registration ones.
                var properties = _ipEngine.Properties;
                OutputEnterpriseValue("Country", nameof(ipData.Country),
                    properties, () => ipData.Country, message);
                OutputEnterpriseValue("Country Code", nameof(ipData.CountryCode),
                    properties, () => ipData.CountryCode, message);
                OutputEnterpriseValue("Region", nameof(ipData.Region),
                    properties, () => ipData.Region, message);
                OutputEnterpriseValue("State", nameof(ipData.State),
                    properties, () => ipData.State, message);
                OutputEnterpriseValue("Town", nameof(ipData.Town),
                    properties, () => ipData.Town, message);
                OutputEnterpriseValue("Latitude", nameof(ipData.Latitude),
                    properties, () => ipData.Latitude, message);
                OutputEnterpriseValue("Longitude", nameof(ipData.Longitude),
                    properties, () => ipData.Longitude, message);
                OutputEnterpriseValue("Registered Name", nameof(ipData.RegisteredName),
                    properties, () => ipData.RegisteredName, message);
                OutputEnterpriseValue("Registered Owner", nameof(ipData.RegisteredOwner),
                    properties, () => ipData.RegisteredOwner, message);
                OutputEnterpriseValue("Registered Country", nameof(ipData.RegisteredCountry),
                    properties, () => ipData.RegisteredCountry, message);
                OutputEnterpriseValue("IP Range Start", nameof(ipData.IpRangeStart),
                    properties, () => ipData.IpRangeStart, message);
                OutputEnterpriseValue("IP Range End", nameof(ipData.IpRangeEnd),
                    properties, () => ipData.IpRangeEnd, message);
                OutputEnterpriseValue("Accuracy Radius", nameof(ipData.AccuracyRadiusMin),
                    properties, () => ipData.AccuracyRadiusMin, message);
                OutputEnterpriseValue("Time Zone Offset", nameof(ipData.TimeZoneOffset),
                    properties, () => ipData.TimeZoneOffset, message);
            }

            /// <summary>
            /// Output a property, or say that it needs the Enterprise data file
            /// when the engine's data file does not have it. The value is only
            /// read when the data file has the property.
            /// </summary>
            private void OutputEnterpriseValue(string name,
                string property,
                IEnumerable<IElementPropertyMetaData> properties,
                Func<IAspectPropertyValue> value,
                StringBuilder message)
            {
                if (properties.Any(p => string.Equals(
                    p.Name, property, StringComparison.OrdinalIgnoreCase)) == false)
                {
                    message.AppendLine(
                        $"\t{name}: {Examples.OnPremise.ExampleUtils.NEEDS_ENTERPRISE}");
                    _needsEnterprise = true;
                    return;
                }
                var propertyValue = value();
                if (propertyValue is IAspectPropertyValue<IReadOnlyList<string>> list)
                {
                    OutputListValue(name, list, message);
                }
                else
                {
                    OutputValue(name, propertyValue, message);
                }
            }

            private void OutputValue(string name, 
                IAspectPropertyValue value,
                StringBuilder message)
            {
                message.AppendLine(value.HasValue ?
                    $"\t{name}: {value.Value}" :
                    $"\t{name}: {value.NoValueMessage}");
            }

            private void OutputListValue(string name,
                IAspectPropertyValue<IReadOnlyList<string>> property,
                StringBuilder message)
            {
                if (!property.HasValue)
                {
                    message.AppendLine($"\t{name}: {property.NoValueMessage}");
                }
                else
                {
                    var values = string.Join(", ", property.Value.Select(v => $"'{v}'"));
                    message.AppendLine($"\t{name}: {values}");
                }
            }

            private void OutputWeightedStringProperty(string name, 
                IAspectPropertyValue<IReadOnlyList<IWeightedValue<string>>> property,
                StringBuilder message)
            {
                if (!property.HasValue)
                {
                    message.AppendLine($"\t{name}: {property.NoValueMessage}");
                }
                else
                {
                    var values = string.Join(", ", property.Value.Select(x => 
                        x.Weighting() == 1 ? $"'{x.Value}'" : $"('{x.Value}' @ {x.Weighting()})"));
                    message.AppendLine($"\t{name}: {values}");
                }
            }

            private void OutputWeightedIntValues(string name, 
                IAspectPropertyValue<IReadOnlyList<IWeightedValue<int>>> property,
                StringBuilder message)
            {
                if (!property.HasValue)
                {
                    message.AppendLine($"\t{name}: {property.NoValueMessage}");
                }
                else
                {
                    var values = property.Value.Select(x => 
                        Math.Abs(x.Weighting() - 1.0f) < 0.0001f ? x.Value.ToString() : $"({x.Value} @ {x.Weighting():F4})");
                    message.AppendLine($"\t{name}: {string.Join(", ", values)}");
                }
            }

            private void OutputWeightedFloatValues(string name, 
                IAspectPropertyValue<IReadOnlyList<IWeightedValue<float>>> property,
                StringBuilder message)
            {
                if (!property.HasValue)
                {
                    message.AppendLine($"\t{name}: {property.NoValueMessage}");
                }
                else
                {
                    var values = property.Value.Select(x => 
                        Math.Abs(x.Weighting() - 1.0f) < 0.0001f ? x.Value.ToString("F6") : $"({x.Value:F6} @ {x.Weighting():F4})");
                    message.AppendLine($"\t{name}: {string.Join(", ", values)}");
                }
            }

            private void OutputWeightedIPAddressValues(string name, 
                IAspectPropertyValue<IReadOnlyList<IWeightedValue<System.Net.IPAddress>>> property,
                StringBuilder message)
            {
                if (!property.HasValue)
                {
                    message.AppendLine($"\t{name}: {property.NoValueMessage}");
                }
                else
                {
                    var values = property.Value.Select(x => 
                        Math.Abs(x.Weighting() - 1.0f) < 0.0001f ? x.Value.ToString() : $"({x.Value} @ {x.Weighting():F4})");
                    message.AppendLine($"\t{name}: {string.Join(", ", values)}");
                }
            }

            private void CheckDataFiles(IPipeline pipeline, ILogger logger)
            {
                // Check Device Detection data file
                var deviceEngine = pipeline.GetElement<DeviceDetectionHashEngine>();
                if (deviceEngine != null)
                {
                    var dataFile = deviceEngine.DataFiles[0];
                    logger.LogInformation($"Device Detection: Using '{deviceEngine.DataSourceTier}' " +
                        $"data file from '{dataFile.DataFilePath}', " +
                        $"published {dataFile.DataPublishedDateTime}");
                    
                    if (DateTime.UtcNow > dataFile.DataPublishedDateTime.AddDays(30))
                    {
                        logger.LogWarning("Device Detection data file is more than 30 days old. " +
                            "Consider updating for better accuracy.");
                    }
                }

                // Check IP Intelligence data file
                var ipEngine = pipeline.GetElement<IpiOnPremiseEngine>();
                if (ipEngine != null)
                {
                    Examples.OnPremise.ExampleUtils.CheckDataFile(ipEngine, logger);
                }
            }
        }

        public static void Main(string[] args)
        {
            // Use the data files the arguments name, or the free Lite files that the
            // data repository links, downloading them when they are missing. An
            // explicit IP Intelligence data file can also come from the environment
            // variables.
            var deviceDataFile = args.Length >= 1 ? args[0] :
                IpIntelligence.Examples.ExampleUtils.FindFile(Constants.LITE_HASH_FILE_NAME);
            var ipDataFile = args.Length >= 2 ? args[1] :
                IpIntelligence.Examples.ExampleUtils.FindDataFile(Constants.LITE_IPI_FILE_NAME);

            // Configure a logger to output to the console
            var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
            var logger = loggerFactory.CreateLogger<Program>();

            if (deviceDataFile != null && ipDataFile != null)
            {
                logger.LogInformation($"Device Detection data file: {deviceDataFile}");
                logger.LogInformation($"IP Intelligence data file: {ipDataFile}");
                
                new Example().Run(deviceDataFile, ipDataFile, loggerFactory, Console.Out);
            } 
            else
            {
                if (deviceDataFile == null)
                {
                    logger.LogError("Failed to find a Device Detection data file. Make sure the " +
                        "submodules have been fetched by running " +
                        "`git submodule update --init --recursive`, so that the free Lite " +
                        "file can be downloaded.");
                }
                if (ipDataFile == null)
                {
                    logger.LogError("Failed to find an IP Intelligence data file. Make sure the " +
                        "submodules have been fetched by running " +
                        "`git submodule update --init --recursive`, so that the free Lite " +
                        "file can be downloaded.");
                }
            }

            // Dispose the logger to ensure any messages get flushed
            loggerFactory.Dispose();
        }
    }
}