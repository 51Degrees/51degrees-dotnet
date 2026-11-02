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
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;

/// <summary>
/// @example OnPremise/OfflineProcessing-Console/Program.cs
/// 
/// This example shows how to process a YAML file containing IP address evidence for IP Intelligence analysis.
/// 
/// You will learn:
/// 
/// 1. How to create a Pipeline that uses 51Degrees On-premise IP Intelligence
/// 2. How to process evidence from YAML files in batch
/// 3. How to output IP Intelligence results to YAML format
/// 
/// Provides an example of processing a YAML file containing evidence for IP Intelligence. 
/// There are 20,000 examples in the supplied file of evidence representing HTTP Headers.
/// For example:
/// 
/// ```
/// query.client-ip: '8.8.8.8'
/// server.client-ip: '1.2.3.4'
/// ```
/// 
/// We create a IP Intelligence pipeline to read the data and find out about the associated IP address,
/// we write this data to a YAML formatted output stream.
/// 
/// As well as explaining the basic operation of off line processing using the defaults, for
/// advanced operation this example can be used to experiment with tuning IP Intelligence for
/// performance and predictive power using Performance Profile, Graph and Difference and Drift 
/// settings.
/// 
/// Evidence files can be obtained from the [ip-intelligence-data repository](https://github.com/51Degrees/ip-intelligence-data).
/// 
/// This example is available in full on [GitHub](https://github.com/51Degrees/ip-intelligence-dotnet-examples/blob/master/Examples/OnPremise/OfflineProcessing-Console/Program.cs). 
/// 
/// This example requires an enterprise IP Intelligence data file (.ipi). 
/// To obtain an enterprise data file for testing, please [contact us](https://51degrees.com/contact-us?utm_source=code&amp;utm_medium=example&amp;utm_campaign=ip-intelligence-dotnet-examples&amp;utm_content=examples-onpremise-offlineprocessing-console-program.cs&amp;utm_term=header).
/// 
/// Required NuGet Dependencies:
/// - [FiftyOne.IpIntelligence](https://www.nuget.org/packages/FiftyOne.IpIntelligence/)
/// - [Microsoft.Extensions.Logging.Console](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Console/)
/// - [YamlDotNet](https://www.nuget.org/packages/YamlDotNet/)
/// </summary>
namespace FiftyOne.IpIntelligence.Examples.OnPremise.OfflineProcessing
{
    public class Program
    {
        public class Example : ExampleBase
        {
            /// <summary>
            /// Process a YAML representation of evidence - and create a YAML output containing 
            /// the processed evidence.
            /// </summary>
            /// <param name="dataFile">
            /// The path to the IP Intelligence data file
            /// </param>
            /// <param name="evidenceYaml">
            /// Reader containing the yaml representation of the evidence to process
            /// </param>
            /// <param name="loggerFactory">
            /// Factory to use when creating loggers
            /// </param>
            /// <param name="output">
            /// Output writer to use when writing results
            /// </param>
            public void Run(
                string dataFile,
                TextReader evidenceYaml,
                ILoggerFactory loggerFactory,
                TextWriter output)
            {
                var logger = loggerFactory.CreateLogger<Program>();
                // In this example, we use the IpiPipelineBuilder and configure it
                // in code. For more information about builders in general see the documentation at
                // https://51degrees.com/documentation/_concepts__configuration__builders__index.html?utm_source=code&utm_medium=example&utm_campaign=ip-intelligence-dotnet-examples&utm_content=examples-onpremise-offlineprocessing-console-program.cs&utm_term=run

                // Note that we wrap the creation of a pipeline in a using to control its life cycle
                using (var pipeline = new IpiPipelineBuilder(loggerFactory)
                    .UseOnPremise(dataFile, null, false)
                    // Use the LowMemory profile so the (multi-gigabyte) IP Intelligence
                    // data file is paged from disk rather than loaded entirely into RAM.
                    // See the documentation for more detail on this and other
                    // configuration options.
                    // https://51degrees.com/documentation/_features__automatic_datafile_updates.html?utm_source=code&utm_medium=example&utm_campaign=ip-intelligence-dotnet-examples&utm_content=examples-onpremise-offlineprocessing-console-program.cs&utm_term=run
                    // https://51degrees.com/documentation/_features__usage_sharing.html?utm_source=code&utm_medium=example&utm_campaign=ip-intelligence-dotnet-examples&utm_content=examples-onpremise-offlineprocessing-console-program.cs&utm_term=run
                    .SetPerformanceProfile(PerformanceProfiles.LowMemory)
                    // Inhibit sharing usage for this example.
                    // In general, off line processing usage should NOT be shared back to 51Degrees.
                    // This is because it will not contain the full set of information that is 
                    // required by our data processing back-end and will be discarded.
                    // If you specifically want to share data that is being processed off line
                    // in order to help us improve the detection, then
                    // this additional data will need to be collected and included as evidence
                    // to the Pipeline. See
                    // https://51degrees.com/documentation/_features__usage_sharing.html?utm_source=code&utm_medium=example&utm_campaign=ip-intelligence-dotnet-examples&utm_content=examples-onpremise-offlineprocessing-console-program.cs&utm_term=run#Low_Level_Usage_Sharing
                    // for more details on this.
                    .SetShareUsage(false)
                    // Inhibit auto-update of the data file for this example
                    .SetAutoUpdate(false)
                    .SetDataUpdateOnStartUp(false)
                    .SetDataFileSystemWatcher(false)
                    .SetProperty("RegisteredName")
                    .Build())
                {
                    var serializer = new Serializer();
                    foreach (var evidence in GetEvidence(evidenceYaml, logger))
                    {
                        // write the yaml document separator
                        output.WriteLine("---");
                    
                        // Pass the record to the pipeline as evidence so that it can be analyzed
                        AnalyseEvidence(evidence, pipeline, output, serializer);
                    }
                    // write the yaml document end marker
                    output.WriteLine("...");

                    ExampleUtils.CheckDataFile(pipeline, loggerFactory.CreateLogger<Program>());
                }
            }

            private void AnalyseEvidence(
                Dictionary<string, object> evidence,
                IPipeline pipeline,
                TextWriter writer,
                Serializer serializer)
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
                    // Add the evidence values to the flow data
                    data.AddEvidence(evidence);
                    // Process the flow data.
                    data.Process();

                    var ipData = data.Get<IIpIntelligenceData>();
                    Dictionary<string, string> output = new Dictionary<string, string>();
                    // Add the evidence values to the output
                    foreach(var entry in evidence)
                    {
                        output.Add(entry.Key, entry.Value.ToString());
                    }
                    // Now add the values that we want to store against the record.
                    // TODO: Read other properties
                    {
                        var name = ipData.RegisteredName;
                        if (!name.HasValue)
                        {
                            output.Add(nameof(ipData.RegisteredName),
                                $"\t{nameof(ipData.RegisteredName)}: {name.NoValueMessage} - {name.NoValueMessage}");
                        }
                        else
                        {
                            output.Add(nameof(ipData.RegisteredName), 
                                $"\t{nameof(ipData.RegisteredName)}  : {name.Value}");
                        }
                    }
                    // Our IP Intelligence solution uses machine learning to find the optimal
                    // way to perform identification based on the real-world evidence values that we
                    // observe each day.
                    // As this changes over time, the result of detection can potentially change
                    // as well.
                    serializer.Serialize(writer, output);
                }
            }
        }

        static void Main(string[] args)
        {
            // Use the supplied path for the data file or find the lite file that is included
            // in the repository.
            var dataFile = args.Length > 0 ? args[0] :
                // In this example, by default, the 51degrees "Lite" file needs to be somewhere in the
                // project space, or you may specify another file as a command line parameter.
                //
                // Note that the Lite data file is only used for illustration, and has limited accuracy
                // and capabilities. Find out about the Enterprise data file on our pricing page:
                // https://51degrees.com/pricing?utm_source=code&utm_medium=example&utm_campaign=ip-intelligence-dotnet-examples&utm_content=examples-onpremise-offlineprocessing-console-program.cs&utm_term=main
                Examples.ExampleUtils.FindDataFile(Constants.ENTERPRISE_IPI_DATA_FILE_NAME);

            File.WriteAllText("OfflineProcessing_DataFileName.txt", dataFile);

            // Do the same for the yaml evidence file.
            var evidenceFile = args.Length > 1 ? args[1] :
                // This file contains the 20,000 most commonly seen combinations of header values 
                // that are relevant to IP Intelligence.
                Examples.ExampleUtils.FindFile(Constants.YAML_EVIDENCE_FILE_NAME);
            // Finally, get the location for the output file. Use the same location as the
            // evidence if a path is not supplied on the command line.
            var outputFile = args.Length > 2 ? args[2] :
                Path.Combine(Path.GetDirectoryName(evidenceFile), "offline-processing-output.yml");

            // Configure a logger to output to the console.
            var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
            var logger = loggerFactory.CreateLogger<Program>();

            if (dataFile != null)
            {
                using (var writer = new StreamWriter(File.OpenWrite(outputFile)))
                using (var reader = new StreamReader(File.OpenRead(evidenceFile)))
                {
                    new Example().Run(dataFile, reader, loggerFactory, writer);
                }
                logger.LogInformation($"Processing complete. See results in: '{outputFile}'");
            }
            else
            {
                logger.LogError("Failed to find a IP Intelligence data file. Make sure the " +
                    "ip-intelligence-data submodule has been updated by running " +
                    "`git submodule update --recursive`.");
            }

            // Dispose the logger to ensure any messages get flushed
            loggerFactory.Dispose();
        }
    }
}