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


// Ignore Spelling: Ip

using FiftyOne.IpIntelligence.Translation.Data;
using FiftyOne.IpIntelligence.Translation.FlowElements;
using FiftyOne.IpIntelligence.Engine.OnPremise.FlowElements;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
/// @example OnPremise/GettingStarted-Console/Program.cs
///
/// This example shows how to use 51Degrees On-premise IP Intelligence to determine location and network details from IP addresses.
/// It also demonstrates the CountriesTranslationEngine which produces translated country
/// name lists and complete ordered country code lists.
///
/// You will learn:
///
/// 1. How to manually build a Pipeline with the IP Intelligence engine and translation engines
/// 2. How to pass input data (evidence) to the Pipeline
/// 3. How to retrieve the results
///
/// This example is available in full on [GitHub](https://github.com/51Degrees/51degrees-dotnet/blob/main/examples/ip-intelligence/OnPremise/GettingStarted-Console/Program.cs).
///
/// By default the example uses the free Lite IP Intelligence data file, which it downloads when
/// it is missing. The Lite file has the registration properties, and the example shows each of
/// the others as needing the Enterprise data file, which adds the location, range and
/// connection properties and the country translations. To obtain an enterprise data file for
/// testing, please [contact us](https://51degrees.com/contact-us?utm_source=code&amp;utm_medium=example&amp;utm_campaign=51degrees-dotnet&amp;utm_content=examples-onpremise-gettingstarted-console-program.cs&amp;utm_term=header).
///
/// Required NuGet Dependencies:
/// - [FiftyOne.IpIntelligence](https://www.nuget.org/packages/FiftyOne.IpIntelligence/)
/// </summary>
namespace FiftyOne.IpIntelligence.Examples.OnPremise.GettingStartedConsole
{
    public class Program
    {
        public class Example : ExampleBase
        {
            /// <summary>
            /// Where to ask for a copy of the Enterprise data file, tagged
            /// with this example.
            /// </summary>
            private const string ContactUs = "https://51degrees.com/contact-us?utm_source=code&utm_medium=example&utm_campaign=51degrees-dotnet&utm_content=examples-ip-intelligence-onpremise-gettingstarted-console-program.cs&utm_term=enterprise-data-file";

            /// <summary>
            /// The engine, which says which properties the data file has.
            /// </summary>
            private IpiOnPremiseEngine _engine;

            /// <summary>
            /// True when the pipeline has the translation engines.
            /// </summary>
            private bool _translates;

            /// <summary>
            /// True once the example has shown a property that the data file
            /// in use does not have.
            /// </summary>
            private bool _needsEnterprise;

            public void Run(string dataFile, ILoggerFactory loggerFactory, TextWriter output)
            {
                _needsEnterprise = false;

                // Create IP Intelligence engine
                _engine = new IpiOnPremiseEngineBuilder(loggerFactory)
                    // LowMemory keeps the large IP Intelligence data file on disk
                    // instead of loading it entirely into memory.
                    .SetPerformanceProfile(PerformanceProfiles.LowMemory)
                    .SetAutoUpdate(false)
                    .SetDataFileSystemWatcher(false)
                    .Build(dataFile, false);

                // Build pipeline - translation engines must come after the IP
                // Intelligence engine. They translate the country codes, which
                // only the Enterprise data file has, so they are only added
                // when the data file has them.
                var builder = new PipelineBuilder(loggerFactory)
                    .AddFlowElement(_engine);
                _translates = ExampleUtils.HasProperty(
                    _engine, "CountryCodesGeographical");
                if (_translates)
                {
                    builder
                        .AddFlowElement(new CountryCodeTranslationEngineBuilder(loggerFactory)
                            .Build())
                        .AddFlowElement(new CountriesTranslationEngineBuilder(loggerFactory)
                            .Build());
                }
                using (var pipeline = builder.Build())
                {
                    // IPs near country borders that produce multi-country weighted results
                    var borderIps = new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object> { { "query.client-ip", "194.209.0.1" } },
                        new Dictionary<string, object> { { "query.client-ip", "91.183.0.1" } },
                        new Dictionary<string, object> { { "query.client-ip", "77.119.0.1" } },
                        new Dictionary<string, object> { { "query.client-ip", "5.1.0.1" } },
                        new Dictionary<string, object> { { "query.client-ip", "1.46.0.1" } },
                    };

                    foreach (var evidence in borderIps)
                    {
                        AnalyseEvidence(evidence, pipeline, output);
                    }

                    if (_needsEnterprise)
                    {
                        ExampleUtils.WriteEnterpriseNote(output, ContactUs);
                    }
                    ExampleUtils.CheckDataFile(pipeline, loggerFactory.CreateLogger<Program>());
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
                // FlowData is wrapped in a using block in order to ensure that the unmanaged
                // resources allocated by the native IP Intelligence library are freed
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

                    // Output all the properties. The free Lite data file has the
                    // first three, and only the Enterprise data file has the rest.
                    OutputProperty(nameof(ipData.RegisteredName), () => ipData.RegisteredName, message);
                    OutputProperty(nameof(ipData.RegisteredOwner), () => ipData.RegisteredOwner, message);
                    OutputProperty(nameof(ipData.RegisteredCountry), () => ipData.RegisteredCountry, message);
                    OutputProperty(nameof(ipData.IpRangeStart), () => ipData.IpRangeStart, message);
                    OutputProperty(nameof(ipData.IpRangeEnd), () => ipData.IpRangeEnd, message);
                    OutputProperty(nameof(ipData.Country), () => ipData.Country, message);
                    OutputProperty(nameof(ipData.CountryCode), () => ipData.CountryCode, message);
                    OutputProperty(nameof(ipData.CountryCode3), () => ipData.CountryCode3, message);
                    OutputProperty(nameof(ipData.Region), () => ipData.Region, message);
                    OutputProperty(nameof(ipData.State), () => ipData.State, message);
                    OutputProperty(nameof(ipData.Town), () => ipData.Town, message);
                    OutputProperty(nameof(ipData.Latitude), () => ipData.Latitude, message);
                    OutputProperty(nameof(ipData.Longitude), () => ipData.Longitude, message);
                    OutputProperty(nameof(ipData.Areas), () => ipData.Areas, message);
                    OutputProperty(nameof(ipData.AccuracyRadiusMin), () => ipData.AccuracyRadiusMin, message);
                    OutputProperty(nameof(ipData.TimeZoneOffset), () => ipData.TimeZoneOffset, message);

                    // Output weighted country code properties
                    OutputWeightedList(
                        nameof(ipData.CountryCodesGeographical),
                        () => ipData.CountryCodesGeographical,
                        message);
                    OutputWeightedList(
                        nameof(ipData.CountryCodesPopulation),
                        () => ipData.CountryCodesPopulation,
                        message);

                    // Output complete country code lists from the CountriesTranslationEngine
                    if (_translates)
                    {
                        var translationData = data.Get<ICountriesTranslationData>();
                        OutputList(
                            nameof(translationData.CountryCodesGeographicalAll),
                            translationData.CountryCodesGeographicalAll,
                            message);
                        OutputList(
                            nameof(translationData.CountryCodesPopulationAll),
                            translationData.CountryCodesPopulationAll,
                            message);
                    }
                    else
                    {
                        message.AppendLine(
                            $"\t{nameof(ICountriesTranslationData.CountryCodesGeographicalAll)}: " +
                            ExampleUtils.NEEDS_ENTERPRISE);
                        message.AppendLine(
                            $"\t{nameof(ICountriesTranslationData.CountryCodesPopulationAll)}: " +
                            ExampleUtils.NEEDS_ENTERPRISE);
                    }

                    output.WriteLine(message.ToString());
                }
            }

            /// <summary>
            /// Reports whether the data file has the property, and when it does
            /// not, writes that the property needs the Enterprise data file.
            /// </summary>
            private bool HasProperty(string name, StringBuilder message)
            {
                if (ExampleUtils.HasProperty(_engine, name))
                {
                    return true;
                }
                message.AppendLine($"\t{name}: {ExampleUtils.NEEDS_ENTERPRISE}");
                _needsEnterprise = true;
                return false;
            }

            private void OutputProperty<T>(string name,
                Func<IAspectPropertyValue<T>> value,
                StringBuilder message)
            {
                if (HasProperty(name, message) == false)
                {
                    return;
                }
                var property = value();
                if (!property.HasValue)
                {
                    message.AppendLine($"\t{name}: {property.NoValueMessage}");
                }
                else
                {
                    message.AppendLine($"\t{name}: {property.Value}");
                }
            }

            private void OutputWeightedList(string name,
                Func<IAspectPropertyValue<IReadOnlyList<IWeightedValue<string>>>> value,
                StringBuilder message)
            {
                if (HasProperty(name, message) == false)
                {
                    return;
                }
                var property = value();
                if (!property.HasValue)
                {
                    message.AppendLine($"\t{name}: {property.NoValueMessage}");
                }
                else
                {
                    var items = property.Value;
                    var formatted = string.Join(", ",
                        items.Select(w => $"{w.Value}({w.Weighting():F2})"));
                    message.AppendLine($"\t{name}: [{formatted}]");
                }
            }

            private void OutputList(string name,
                IAspectPropertyValue<IReadOnlyList<string>> property,
                StringBuilder message)
            {
                if (!property.HasValue)
                {
                    message.AppendLine($"\t{name}: {property.NoValueMessage}");
                }
                else
                {
                    var codes = property.Value;
                    var all = string.Join(", ", codes);
                    message.AppendLine($"\t{name} ({codes.Count} total): {all}");
                }
            }
        }

        public static void Main(string[] args)
        {
            // Use the supplied path for the data file or find the free Lite file that the
            // data repository links, downloading it when it is missing.
            var dataFile = args.Length > 0 ? args[0] :
                Examples.ExampleUtils.FindDataFile(Constants.LITE_IPI_DATA_FILE_NAME);

            // Configure a logger to output to the console.
            var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
            var logger = loggerFactory.CreateLogger<Program>();

            if (dataFile != null)
            {
                new Example().Run(dataFile, loggerFactory, Console.Out);
            }
            else
            {
                logger.LogError("Failed to find a IP Intelligence data file. Make sure the " +
                    "submodules have been fetched by running " +
                    "`git submodule update --init --recursive`, so that the free Lite " +
                    "file can be downloaded, or supply the path to a data file as a " +
                    "command line argument.");
            }

            // Dispose the logger to ensure any messages get flushed
            loggerFactory.Dispose();

            Examples.ExampleUtils.WriteFindOutMore(Console.Out, ContentSlug);
        }

        /// <summary>
        /// This file as a lower case slug, which tags the links in the
        /// closing section.
        /// </summary>
        private const string ContentSlug =
            "examples-ip-intelligence-onpremise-gettingstarted-console-program.cs";
    }
}
