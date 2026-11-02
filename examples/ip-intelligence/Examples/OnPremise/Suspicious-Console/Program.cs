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

using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FiftyOne.IpIntelligence.Examples.OnPremise.Suspicious;

/// <summary>
/// @example OnPremise/Suspicious-Console/Program.cs
/// 
/// This example shows how to combine 51Degrees On-premise IP Intelligence
/// properties to determine whether an IP is likely to be the source
/// suspicious requests.
/// 
/// You will learn:
/// 
/// 1. How to get diversity properties from the IP Intelligence engine.
/// 2. How to combine diversity values with other network related values to
/// assess the likelihood of an IP address being something suspicious.
/// 
/// This example is available in full on [GitHub](https://github.com/51Degrees/ip-intelligence-dotnet-examples/blob/master/Examples/OnPremise/Suspicious-Console/Program.cs). 
/// 
/// This example requires an enterprise IP Intelligence data file (.ipi). 
/// To obtain an enterprise data file for testing, please [contact us](https://51degrees.com/contact-us?utm_source=code&amp;utm_medium=example&amp;utm_campaign=ip-intelligence-dotnet-examples&amp;utm_content=examples-onpremise-suspicious-console-program.cs&amp;utm_term=program).
/// 
/// Required NuGet Dependencies:
/// - [FiftyOne.IpIntelligence](https://www.nuget.org/packages/FiftyOne.IpIntelligence/)
/// </summary>
public class Program
{
    public class Example : ExampleBase
    {
        public void Run(string dataFile, ILoggerFactory loggerFactory, TextWriter output)
        {
            // In this example, we use the IpiPipelineBuilder and configure it
            // in code. For more information about builders in general see the documentation at
            // https://51degrees.com/documentation/_concepts__configuration__builders__index.html?utm_source=code&utm_medium=example&utm_campaign=ip-intelligence-dotnet-examples&utm_content=examples-onpremise-suspicious-console-program.cs&utm_term=run

            // Note that we wrap the creation of a pipeline in a using to control its life cycle
            using (var pipeline = new IpiPipelineBuilder()
                .UseOnPremise(dataFile, null, false)
                // Use the LowMemory profile so the (multi-gigabyte) IP Intelligence
                // data file is paged from disk rather than loaded entirely into RAM.
                // See the documentation for more detail on this and other
                // configuration options.
                // https://51degrees.com/documentation/_features__automatic_datafile_updates.html?utm_source=code&utm_medium=example&utm_campaign=ip-intelligence-dotnet-examples&utm_content=examples-onpremise-suspicious-console-program.cs&utm_term=run
                // https://51degrees.com/documentation/_features__usage_sharing.html?utm_source=code&utm_medium=example&utm_campaign=ip-intelligence-dotnet-examples&utm_content=examples-onpremise-suspicious-console-program.cs&utm_term=run
                .SetPerformanceProfile(PerformanceProfiles.LowMemory)
                // inhibit sharing usage for this example, usually this should be set to "true"
                .SetShareUsage(false)
                // inhibit auto-update of the data file for this test
                .SetAutoUpdate(false)
                .SetDataUpdateOnStartUp(false)
                .SetDataFileSystemWatcher(false)
                .Build())
            {
                ExampleUtils.CheckDataFile(pipeline, loggerFactory.CreateLogger<Program>());
                foreach (var evidence in Examples.ExampleUtils.EvidenceValues)
                {
                    AnalyseEvidence(evidence, pipeline, output);
                }
            }
        }

        private void AnalyseEvidence(
            Dictionary<string, object> evidence,
            IPipeline pipeline,
            TextWriter output)
        {
            // Process the evidence
            using var flowData = pipeline.CreateFlowData();
            flowData.AddEvidence(evidence);
            flowData.Process();
            var ipData = flowData.Get<IIpIntelligenceData>();

            // In this example we use the following properties to make some
            // basic assumptions about the likelihood of an IP being a source of
            // suspicious activity.
            var isCellular = ipData.IsCellular.HasValue && ipData.IsCellular.Value;
            var hardwareDiversity = ipData.HardwareDiversity.HasValue ? ipData.HardwareDiversity.Value : 0;
            var browserDiversity = ipData.BrowserDiversity.HasValue ? ipData.BrowserDiversity.Value : 0;
            var locationConfidence = ipData.LocationConfidence.HasValue ? ipData.LocationConfidence.Value : null;
            var hosted = ipData.IsHosted.HasValue ? ipData.IsHosted.Value : false;
            var country = ipData.CountryCode.HasValue ? ipData.CountryCode.Value : null;
            var registeredCountry = ipData.RegisteredCountry.HasValue ? ipData.RegisteredCountry.Value : null;
            var human = ipData.HumanProbability.HasValue ? ipData.HumanProbability.Value : 0;

            // Calculating a simple "suspicious" score based on the properties
            // above.
            // Many other properties can be used to draw conclusions about the
            // likelihood of suspicious activity, and this is a basic example
            // that should not be used in production without further testing
            // and tuning.
            var isSuspicious =
                // Here we can say that if there is a high diversity of hardware
                // profiles in the IP, then is could be either VPN, cellular,
                // proxy, or other hosting.
                // We can then rule out cellular with the IsCellular property.
                // A low location confidence is further evidence of VPN or
                // proxy use, rather than other hosting, but this is not a
                // strong deteminer on its own.
                (hardwareDiversity >= 7 &&
                isCellular == false &&
                locationConfidence == "Low") ||
                // Then we can also consider the observed country, and the
                // coutnry the IP range is registered to. If these are not the
                // same, then this can be an indication of VPN or proxy use.
                (hosted == true &&
                country != null &&
                country != "Unknown" &&
                country != registeredCountry) ||
                // If the browser versions are significantly more diverse than
                // the hardware, this may indicate that some devices are using
                // multiple browsers, which can be a sign of suspicious
                // activity.
                browserDiversity - hardwareDiversity > 2;

            var message = new StringBuilder();
            message.AppendLine("Input values:");
            foreach (var entry in evidence)
            {
                message.AppendLine($"\t{entry.Key}: {entry.Value}");
            }

            message.AppendLine("Results:");
            message.AppendLine($"\tIsCellular: {isCellular}");
            message.AppendLine($"\tHardwareDiversity: {hardwareDiversity}");
            message.AppendLine($"\tBrowserDiversity: {browserDiversity}");
            message.AppendLine($"\tLocationConfidence: {locationConfidence}");
            message.AppendLine($"\tIsHosted: {hosted}");
            message.AppendLine($"\tCountry: {country}");
            message.AppendLine($"\tRegisteredCountry: {registeredCountry}");
            message.AppendLine($"\tHumanProbability: {human}");
            message.AppendLine($"\tIsSuspicious: {isSuspicious}");
            output.WriteLine(message.ToString());
        }
    }

    static void Main(string[] args)
    {
        // Use the supplied path for the data file or find the lite file that is included
        // in the repository.
        var dataFile = args.Length > 0 ? args[0] :
        // In this example, by default, the 51degrees IP Intelligence data file needs to be somewhere in the
        // project space, or you may specify another file as a command line parameter.
        //
        // For testing, contact us to obtain an enterprise data file: https://51degrees.com/contact-us?utm_source=code&utm_medium=example&utm_campaign=ip-intelligence-dotnet-examples&utm_content=examples-onpremise-suspicious-console-program.cs&utm_term=main
        Examples.ExampleUtils.FindDataFile(Constants.ENTERPRISE_IPI_DATA_FILE_NAME);

        File.WriteAllText("Metadata_DataFileName.txt", dataFile);

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
                "ip-intelligence-data submodule has been updated by running " +
                "`git submodule update --recursive`. By default, the 'lite' file included " +
                "with this code will be used. A different file can be specified " +
                "by supplying the full path as a command line argument");
        }

        // Dispose the logger to ensure any messages get flushed
        loggerFactory.Dispose();
    }
}
