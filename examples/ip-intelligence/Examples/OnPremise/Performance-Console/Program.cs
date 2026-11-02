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

using FiftyOne.IpIntelligence.Engine.OnPremise.FlowElements;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// @example Performance-Console/Program.cs
///
/// This example shows how to benchmark the performance of 51Degrees On-premise IP Intelligence.
/// 
/// You will learn:
/// 
/// 1. How to create a Pipeline that uses 51Degrees On-premise IP Intelligence with different performance configurations
/// 2. How to run benchmarks to measure detection speed and throughput
/// 3. How to analyze performance trade-offs between speed, memory usage, and accuracy
///
/// The example illustrates a "clock-time" benchmark for assessing detection speed.
///
/// Using a YAML formatted evidence file - "20000 Evidence Records.yml" - supplied with the
/// distribution or can be obtained from the [data repository on Github](https://github.com/51Degrees/ip-intelligence-data).
///
/// It's important to understand the trade-offs between performance, memory usage and accuracy, that
/// the 51Degrees pipeline configuration makes available, and this example shows a range of
/// different configurations to illustrate the difference in performance.
///
/// Requesting properties from a single component reduces detection time compared with requesting 
/// properties from multiple components. If you don't specify any properties to detect, then all 
/// properties are detected.
///
/// 
/// This example is available in full on [GitHub](https://github.com/51Degrees/ip-intelligence-dotnet-examples/blob/master/Examples/OnPremise/Performance-Console/Program.cs).
/// 
/// This example requires an enterprise IP Intelligence data file (.ipi). 
/// To obtain an enterprise data file for testing, please [contact us](https://51degrees.com/contact-us?utm_source=code&amp;utm_medium=example&amp;utm_campaign=ip-intelligence-dotnet-examples&amp;utm_content=examples-onpremise-performance-console-program.cs&amp;utm_term=header).
/// 
/// Required NuGet Dependencies:
/// - [FiftyOne.IpIntelligence](https://www.nuget.org/packages/FiftyOne.IpIntelligence/)
/// </summary>
namespace FiftyOne.IpIntelligence.Examples.OnPremise.Performance
{
    public class Program
    {
        private static long _startupTimeMs = 0;
        
        public class BenchmarkResult
        {
            public long Count { get; set; }
            public Stopwatch Timer { get; } = new Stopwatch();
            public long HashSum { get; set; } = 0;
            public bool GCEnabled { get; set; } = true;
            public long GCCollections0 { get; set; }
            public long GCCollections1 { get; set; }
            public long GCCollections2 { get; set; }
        }

        // MaxPerformance is the fastest configuration, and the nightly performance
        // graphs are drawn from its result. It loads the whole data file into
        // memory, so the machine needs several gigabytes of free memory.
        private static readonly PerformanceConfiguration[] _configs = new PerformanceConfiguration[]
        {
            new PerformanceConfiguration(PerformanceProfiles.MaxPerformance, false),
        };

        private const ushort DEFAULT_THREAD_COUNT = 4;

        private static float GetMsPerDetection(
            IList<BenchmarkResult> results,
            int threadCount)
        {
            var detections = results.Sum(r => r.Count);
            var milliseconds = results.Sum(r => r.Timer.ElapsedMilliseconds);
            // Calculate approx. real-time ms per detection. 
            return milliseconds > 0 ? (float)(milliseconds) / (detections * threadCount) : 1f;
        }

        public class Example : ExampleBase
        {
            private IPipeline _pipeline;

            public Example(IPipeline pipeline)
            {
                _pipeline = pipeline;
            }

            private List<BenchmarkResult> Run(
                TextReader evidenceReader,
                TextWriter output,
                int threadCount)
            {
                var evidence = GetEvidence(evidenceReader).ToList();

                // Make an initial run to warm up the system
                output.WriteLine("Warming up");
                var warmup = Benchmark(evidence, threadCount, false);
                var warmupTime = warmup.Sum(r => r.Timer.ElapsedMilliseconds);
                GC.Collect();
                Task.Delay(500).Wait();

                output.WriteLine("Running");
                var execution = Benchmark(evidence, threadCount, false);
                var executionTime = execution.Sum(r => r.Timer.ElapsedMilliseconds);
                output.WriteLine($"Finished - Execution time was {executionTime} ms, " +
                    $"adjustment from warm-up {executionTime - warmupTime} ms");

                Report(execution, threadCount, output);

                return execution;
            }

            /// <summary>
            /// Output some results from the benchmarking
            /// </summary>
            /// <param name="results"></param>
            /// <param name="threadCount"></param>
            /// <param name="output"></param>
            /// <param name="mode"></param>
            private void Report(List<BenchmarkResult> results,
                int threadCount,
                TextWriter output)
            {
                // Calculate approx. real-time ms per detection. 
                var msPerDetection = GetMsPerDetection(results, threadCount);
                var detectionsPerSecond = 1000 / msPerDetection;
                var totalGC0 = results.Sum(r => r.GCCollections0);
                var totalGC1 = results.Sum(r => r.GCCollections1);
                var totalGC2 = results.Sum(r => r.GCCollections2);
                
                output.WriteLine($"Results:");
                output.WriteLine($"  Detections: {results.Sum(i => i.Count)}");
                output.WriteLine($"  Average ms per detection: {msPerDetection:F4}");
                output.WriteLine($"  Detections per second: {detectionsPerSecond:F0}");
                output.WriteLine($"  GC Collections - Gen0: {totalGC0}, Gen1: {totalGC1}, Gen2: {totalGC2}");
                output.WriteLine($"  Threads: {threadCount}");
            }

            /// <summary>
            /// Run the benchmark for the supplied evidence.
            /// </summary>
            /// <param name="allEvidence"></param>
            /// <param name="threadCount"></param>
            /// <param name="gcEnabled"></param>
            /// <returns></returns>
            private List<BenchmarkResult> Benchmark(
                List<Dictionary<string, object>> allEvidence, 
                int threadCount,
                bool gcEnabled = false)
            {
                List<BenchmarkResult> results = new List<BenchmarkResult>();
                bool gcRegionStarted = false;

                try
                {
                    if (!gcEnabled)
                    {
                        // Try to start a no-GC region with progressively smaller sizes
                        long[] sizes = { 64 * 1024 * 1024, 32 * 1024 * 1024, 16 * 1024 * 1024, 8 * 1024 * 1024 };
                        foreach (var size in sizes)
                        {
                            gcRegionStarted = GC.TryStartNoGCRegion(size);
                            if (gcRegionStarted)
                            {
                                break;
                            }
                        }
                        if (!gcRegionStarted)
                        {
                            Console.WriteLine("Warning: Could not start no-GC region, running with GC enabled");
                        }
                    }

                    // Record initial GC stats
                    var initialGC0 = GC.CollectionCount(0);
                    var initialGC1 = GC.CollectionCount(1);
                    var initialGC2 = GC.CollectionCount(2);

                    // Start multiple threads to process a set of evidence.
                    var processing = Parallel.ForEach(allEvidence,
                        new ParallelOptions()
                        {
                            MaxDegreeOfParallelism = threadCount,
                        },
                        // Create a benchmark result instance per parallel unit
                        () => new BenchmarkResult() { GCEnabled = false },
                        (evidence, loopState, result) =>
                        {
                            result.Timer.Start();
                            // A using block MUST be used for the FlowData instance. This ensures that
                            // native resources created by the IP Intelligence engine are freed in
                            // good time.
                            using (var data = _pipeline.CreateFlowData())
                            {
                                // Add the evidence to the flow data.
                                data.AddEvidence(evidence).Process();

                                // Get the data from the engine.
                                var ipData = data.Get<IIpIntelligenceData>();

                                result.Count++;

                                // Access a property to ensure compiler optimizer doesn't optimize
                                // out the very method that the benchmark is testing.
                                if (ipData.RegisteredName.HasValue)
                                {
                                    result.HashSum += ipData.RegisteredName.Value.GetHashCode();
                                }
                            }

                        result.Timer.Stop();
                        return result;
                    },
                    // Add the results from this run to the overall results.
                    (result) => 
                    {
                        // Record GC stats for this task
                        result.GCCollections0 = GC.CollectionCount(0) - initialGC0;
                        result.GCCollections1 = GC.CollectionCount(1) - initialGC1;
                        result.GCCollections2 = GC.CollectionCount(2) - initialGC2;
                        
                        lock (results)
                        {
                            results.Add(result);
                        }
                    });

                    return results;
                }
                finally
                {
                    if (!gcEnabled && gcRegionStarted)
                    {
                        try
                        {
                            GC.EndNoGCRegion();
                        }
                        catch (InvalidOperationException)
                        {
                            // GC region may have already ended due to memory pressure
                        }
                    }
                }
            }

            /// <summary>
            /// This Run method is called by the example test to avoid the need to duplicate the 
            /// service provider setup logic.
            /// </summary>
            /// <param name="options"></param>
            public static List<BenchmarkResult> Run(string dataFile, string evidenceFile, 
                PerformanceConfiguration config, TextWriter output, ushort threadCount)
            {
                // Output benchmarking configuration first
                output.WriteLine($"Benchmarking with profile '{config.Profile}', " +
                    $"AllProperties {config.AllProperties}");
                
                // Initialize a service collection which will be used to create the services
                // required by the Pipeline and manage their lifetimes.
                using (var serviceProvider = new ServiceCollection()
                    // Make sure we're logging to the console.
                    .AddLogging(l => l.AddConsole())
                    .AddTransient<PipelineBuilder>()
                    .AddTransient<IpiOnPremiseEngineBuilder>()
                    // Add a factory to create the singleton IpiOnPremiseEngineBuilder instance.
                    .AddSingleton((x) =>
                    {
                        var builder = x.GetRequiredService<IpiOnPremiseEngineBuilder>()
                            // Disable any data file updates
                            .SetDataFileSystemWatcher(false)
                            .SetAutoUpdate(false)
                            .SetDataUpdateOnStartup(false)
                            // Set performance profile
                            .SetPerformanceProfile(config.Profile)
                            // Hint for cache concurrency
                            .SetConcurrency(threadCount);

                        // Performance is improved by selecting only the properties you intend to
                        // use. Requesting properties from a single component reduces detection
                        // time compared with requesting properties from multiple components.
                        // If you don't specify any properties to detect, then all properties are
                        // detected, here we choose "all properties" by specifying none, or just
                        // "isMobile".
                        // Specify "BrowserName" for just the browser component, "PlatformName"
                        // for just platform or "IsCrawler" for the crawler component.
                        if (config.AllProperties == false)
                        {
                            builder.SetProperty("RegisteredName");
                        }

                        // The data file can be loaded directly from disk or from a byte array
                        // in memory.
                        // This latter option is useful for cloud-based environments with little 
                        // or no hard drive space available. In this scenario, the 'LowMemory' 
                        // performance profile is recommended, as the data is actually already
                        // in memory. Using MaxPerformance would just cause the native code to 
                        // make another copy of the data in memory for little benefit.
                        IpiOnPremiseEngine engine = null;
                        var startupTimer = Stopwatch.StartNew();
                        engine = builder.Build(dataFile, false);
                        startupTimer.Stop();
                        _startupTimeMs = startupTimer.ElapsedMilliseconds;

                        return engine;
                    })
                    // Add a factory to create the singleton IPipeline instance
                    .AddSingleton((x) => {
                        return x.GetRequiredService<PipelineBuilder>()
                            .AddFlowElement(x.GetRequiredService<IpiOnPremiseEngine>())
                            .Build();
                    })
                    .AddTransient<Example>()
                    .BuildServiceProvider())
                using (var evidenceReader = new StreamReader(File.OpenRead(evidenceFile)))
                {
                    // If we don't have a resource key then log an error.
                    if (string.IsNullOrWhiteSpace(dataFile))
                    {
                        serviceProvider.GetRequiredService<ILogger<Program>>().LogError(
                            "Failed to find a IP Intelligence data file. Make sure the " +
                            "ip-intelligence-data submodule has been updated by running " +
                            "`git submodule update --recursive`.");
                        return null;
                    }
                    else
                    {
                        ExampleUtils.CheckDataFile(
                            serviceProvider.GetRequiredService<IPipeline>(), 
                            serviceProvider.GetRequiredService<ILogger<Program>>());
                        
                        output.WriteLine($"Engine startup time: {_startupTimeMs} ms");
                        output.WriteLine($"Processing evidence from '{evidenceFile}'");

                        return serviceProvider.GetRequiredService<Example>().Run(evidenceReader, output, threadCount);
                    }
                }
            }
        }

        static void Main(string[] args)
        {
            // Use the supplied path for the data file or find the lite file that is included
            // in the repository.
            var options = Examples.ExampleUtils.ParseOptions(args);
            if (options != null) {
                var dataFile = options.DataFilePath != null ? options.DataFilePath :
                    // In this example, by default, the 51degrees "Lite" file needs to be somewhere in the
                    // project space, or you may specify another file as a command line parameter.
                    //
                    // Note that the Lite data file is only used for illustration, and has limited accuracy
                    // and capabilities. Find out about the Enterprise data file on our pricing page:
                    // https://51degrees.com/pricing?utm_source=code&utm_medium=example&utm_campaign=ip-intelligence-dotnet-examples&utm_content=examples-onpremise-performance-console-program.cs&utm_term=main
                    Examples.ExampleUtils.FindDataFile(Constants.ENTERPRISE_IPI_DATA_FILE_NAME);
                // Do the same for the yaml evidence file.
                var evidenceFile = options.EvidenceFile != null ? options.EvidenceFile :
                    // This file contains the 20,000 most commonly seen combinations of header values 
                    // that are relevant to IP Intelligence.
                    Examples.ExampleUtils.FindFile(Constants.YAML_EVIDENCE_FILE_NAME);

                var results = new Dictionary<PerformanceConfiguration, IList<BenchmarkResult>>();
                for (int i = 0; i < _configs.Length; i++)
                {
                    if (i > 0)
                    {
                        Console.WriteLine(); // Add blank line between configurations
                    }
                    var result = Example.Run(dataFile, evidenceFile, _configs[i], Console.Out, DEFAULT_THREAD_COUNT);
                    results[_configs[i]] = result;
                }

                if (string.IsNullOrEmpty(options.JsonOutput) == false)
                {
                    using (var jsonOutput = File.CreateText(options.JsonOutput))
                    {
                        var jsonResults = results.ToDictionary(
                            k => $"{Enum.GetName(k.Key.Profile)}{(k.Key.AllProperties ? "_All" : "")}",
                            v => new Dictionary<string, float>()
                            {
                            {"DetectionsPerSecond", 1000 / GetMsPerDetection(v.Value, DEFAULT_THREAD_COUNT) },
                            {"DetectionsPerSecondPerThread", 1000 / (GetMsPerDetection(v.Value, DEFAULT_THREAD_COUNT) * DEFAULT_THREAD_COUNT) },
                            {"MsPerDetection", GetMsPerDetection(v.Value, DEFAULT_THREAD_COUNT) }
                            });
                        jsonOutput.Write(JsonSerializer.Serialize(jsonResults));
                    }
                }
            }
        }

    }
}