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

using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Examples.OnPremise.Areas;
using FiftyOne.IpIntelligence.Engine.OnPremise.FlowElements;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines;
using GeoCoordinatePortable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// @example OnPremise/Compare-Console/Program.cs
/// 
/// This example takes in a CSV file containing 'known and true' IP-Location 
/// records (IP addresses with associated latitude and longitudes).
/// In this context, 'known and true' refers to real-world data the user 
/// has collected that is considered accurate and trustworthy.
/// 
/// These records are then used for comparison against an IP Intelligence 
/// service that can return latitude, longitude and area information 
/// for a given IP address.
/// This can be useful for understanding how the results of an IP to
/// location service compare to real-world information, especially when 
/// evaluating different solutions.
///
/// The example will ingest the following fields from a CSV file.
/// 
/// Date Time
/// IP address
/// Address Family (optional)
/// Latitude
/// Longitude
/// Continent (optional)
/// Country (optional)
/// 
/// See the `Truth` class for all fields and descriptions.
/// 
/// The IP Intelligence service is used to obtain the latitude and longitude
/// from the IP address. The distance in kilometers is then calculate along
/// with the confidence if available, the total geographic area covered by
/// the area returned, and an indicator as to if the provided latitude and
/// longitude is within the area returned.
/// 
/// Output fields are defined in the `Result` class and include.
/// 
/// Latitude
/// Longitude
/// Found
/// Geometries
/// SquareKms
/// DistanceKms
/// 
/// The output CSV file contains the input truth and the result fields for easy
/// evaluation.
/// 
/// This example is available in full on [GitHub](https://github.com/51Degrees/ip-intelligence-dotnet-examples/blob/master/Examples/OnPremise/Compare-Console/Program.cs). 
/// 
/// This example requires an enterprise IP Intelligence data file (.ipi). 
/// To obtain an enterprise data file for testing, please [contact us](https://51degrees.com/contact-us?utm_source=code&amp;utm_medium=example&amp;utm_campaign=ip-intelligence-dotnet-examples&amp;utm_content=examples-onpremise-compare-console-program.cs&amp;utm_term=header).
/// 
/// Required NuGet Dependencies:
/// - [FiftyOne.IpIntelligence](https://www.nuget.org/packages/FiftyOne.IpIntelligence/)
/// - [Microsoft.Extensions.Logging.Console](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Console/)
/// </summary>
namespace FiftyOne.IpIntelligence.Examples.OnPremise.Compare;

public class Program
{
    /// <summary>
    /// Time interval to wait between logging progress.
    /// </summary>
    private static readonly TimeSpan _logBuild = TimeSpan.FromSeconds(30);

    /// <summary>
    /// A record of latitude, longitude, IP address, and a date time that is
    /// considered truthful for the purposes of comparing with an IP to
    /// location solution result.
    /// </summary>
    public class Truth
    {
        /// <summary>
        /// The date and time in UTC of the observed truth.
        /// </summary>
        public DateTime DateTimeUtc { get; set; }

        /// <summary>
        /// The latitude of the device used.
        /// </summary>
        public double Latitude { get; set; }

        /// <summary>
        /// The longitude of the device used.
        /// </summary>
        public double Longitude { get; set; }

        /// <summary>
        /// The public IP address associated with the device.
        /// </summary>
        public string Ip { get; set; }

        /// <summary>
        /// The version of the IP address.
        /// </summary>
        public string AddressFamily { get; set; }

        /// <summary>
        /// The continent associated with the latitude and longitude.
        /// </summary>
        public string Continent { get; set; }

        /// <summary>
        /// The country associated with the latitude and longitude.
        /// </summary>
        public string Country { get; set; }

        /// <summary>
        /// The line in the source CSV file the truth was read from. Used to
        /// identify the record when it can not be processed.
        /// </summary>
        [Ignore]
        public long LineNumber { get; set; }
    }

    /// <summary>
    /// Records the truth records that were skipped because they could not be
    /// read or compared. Each skipped record is logged, up to a limit, so that
    /// it can be found in the source file, and a summary is logged once all
    /// the records have been processed. Safe to use from many consumers.
    /// </summary>
    /// <param name="logger">
    /// Used to report each skipped record and the summary.
    /// </param>
    /// <param name="maxLogged">
    /// The number of skipped records to log individually. Records after this
    /// are only counted so that a file with many bad records does not flood
    /// the log.
    /// </param>
    /// <param name="maxShare">
    /// The share of the records read that may be skipped before the run is
    /// failed. A few bad records are expected, but most records failing
    /// means the source or the comparison is broken and the run should not
    /// complete as if it had worked.
    /// </param>
    /// <param name="minimumSkipped">
    /// The number of records that must have been skipped before the share is
    /// checked, so that the first bad record in a file does not fail the run.
    /// </param>
    public class SkippedTruths(
        ILogger logger,
        int maxLogged = 100,
        double maxShare = 0.1,
        int minimumSkipped = 100)
    {
        /// <summary>
        /// The longest detail logged for a skipped record.
        /// </summary>
        private const int MaxDetailLength = 500;

        /// <summary>
        /// Number of records skipped for each reason.
        /// </summary>
        private readonly ConcurrentDictionary<string, int> _reasons = new();

        /// <summary>
        /// Total number of records read from the source.
        /// </summary>
        private int _read;

        /// <summary>
        /// Total number of records skipped.
        /// </summary>
        private int _count;

        /// <summary>
        /// Total number of records skipped.
        /// </summary>
        public int Count => _count;

        /// <summary>
        /// Counts a record read from the source, whether or not it is then
        /// skipped.
        /// </summary>
        public void Read() => Interlocked.Increment(ref _read);

        /// <summary>
        /// Number of records skipped for each reason.
        /// </summary>
        public IReadOnlyDictionary<string, int> Reasons => _reasons;

        /// <summary>
        /// Records and logs a truth record that has been skipped.
        /// </summary>
        /// <param name="lineNumber">
        /// The line in the source file the record was read from.
        /// </param>
        /// <param name="ip">
        /// The IP address of the record, if known.
        /// </param>
        /// <param name="reason">
        /// A short reason used to group skipped records in the summary.
        /// </param>
        /// <param name="detail">
        /// Further detail about why the record was skipped.
        /// </param>
        /// <exception cref="InvalidDataException">
        /// More than the allowed share of the records read have been skipped.
        /// </exception>
        public void Add(long lineNumber, string ip, string reason, string detail)
        {
            var count = Interlocked.Increment(ref _count);
            _reasons.AddOrUpdate(reason, 1, (_, i) => i + 1);
            var read = Volatile.Read(ref _read);
            if (count >= minimumSkipped && count > read * maxShare)
            {
                throw new InvalidDataException(
                    $"Skipped '{count}' of '{read}' truth records read, " +
                    $"more than the '{maxShare:P0}' allowed. Last skipped " +
                    $"at line '{lineNumber}' with IP '{ip}'. {reason}. " +
                    detail);
            }
            if (count <= maxLogged)
            {
                if (detail?.Length > MaxDetailLength)
                {
                    detail = detail[..MaxDetailLength] + "...";
                }
                logger.LogWarning(
                    "Skipped truth record at line '{0}' with IP '{1}'. " +
                    "{2}. {3}",
                    lineNumber,
                    ip,
                    reason,
                    detail);
                if (count == maxLogged)
                {
                    logger.LogWarning(
                        "Further skipped truth records will be counted but " +
                        "not logged individually");
                }
            }
        }

        /// <summary>
        /// Logs the number of records skipped for each reason.
        /// </summary>
        /// <param name="source">
        /// The source file the records were read from.
        /// </param>
        public void LogSummary(string source)
        {
            if (_count == 0)
            {
                logger.LogInformation(
                    "No truth records skipped from '{0}'",
                    source);
                return;
            }
            logger.LogWarning(
                "Skipped '{0}' truth records from '{1}'. {2}",
                _count,
                source,
                String.Join(
                    ", ",
                    _reasons.OrderByDescending(i => i.Value).Select(i =>
                        $"'{i.Value}' {i.Key}")));
        }
    }

    public class Result
    {
        /// <summary>
        /// The latitude returned for the IP address from the service being
        /// compared to the truth.
        /// </summary>
        [Name("LatitudeResult")]
        public double? Latitude { get; set; }

        /// <summary>
        /// The longitude returned for the IP address from the service being
        /// compared to the truth.
        /// </summary>
        [Name("LongitudeResult")]
        public double? Longitude { get; set; }

        /// <summary>
        /// An indicator concerning how confident the service is in the result.
        /// </summary>
        public string Confidence { get; set; }

        /// <summary>
        /// The distance in kilometres between the true latitude and longitude
        /// of the device and the latitude and longitude from the service.
        /// </summary>
        public double DistanceKms { get; set; }

        /// <summary>
        /// The area in square kilometers that the IP address is likely to be
        /// found within.
        /// </summary>
        public int SquareKms { get; set; }

        /// <summary>
        /// The number of non overlapping areas that the IP address might be
        /// located in.
        /// </summary>
        public int Geometries { get; set; }

        /// <summary>
        /// True if the latitude and longitude provided in the truth was found
        /// in the area returned.
        /// </summary>
        public bool Contains { get; set; }
    }

    /// <summary>
    /// Classes used when writing out the CSV file both the truth provided and
    /// the result from the IP Intelligence service.
    /// </summary>
    /// <param name="truth"></param>
    /// <param name="result"></param>
    public class Output(Truth truth, Result result)
    {
        public Truth Truth => truth;
        public Result Result => result;
    }

    /// <summary>
    /// Wrapper for a consumer task that returns a list of records.
    /// </summary>
    public class Consumer
    {
        /// <summary>
        /// The task associated with the consumer.
        /// </summary>
        public Task<IReadOnlyList<Output>> Task;

        /// <summary>
        /// The number of IP pipeline processes that have been completed in
        /// a given period of time. Used for logging during processing.
        /// </summary>
        public int Count;
    }
    
    /// <summary>
    /// Configuration passed to the worker host service via dependency
    /// injection.
    /// </summary>
    public class Configuration
    {
        /// <summary>
        /// IPI data file.
        /// </summary>
        public string DataFile;

        /// <summary>
        /// The source truth CSV file.
        /// </summary>
        public string CsvTruthFile;

        /// <summary>
        /// Stream to write the output to.
        /// </summary>
        public StreamWriter Output;
                
        /// <summary>
        /// Logger factory for reporting progress.
        /// </summary>
        public ILoggerFactory LoggerFactory;
    }

    /// <summary>
    /// BackgroundService for running the <see cref="Example"/>.
    /// </summary>
    /// <param name="hostApplicationLifetime"></param>
    /// <param name="configuration"></param>
    public sealed class Worker(
        IHostApplicationLifetime hostApplicationLifetime,
        Configuration configuration) 
        : BackgroundService
    {
        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            await Example.Run(
                configuration.DataFile,
                configuration.CsvTruthFile,
                configuration.Output,
                configuration.LoggerFactory,
                stoppingToken);
            hostApplicationLifetime.StopApplication();
        }
    }

    /// <summary>
    /// Implementation of the example that can be called from the Program's
    /// main method or any other consuming service.
    /// </summary>
    public class Example : ExampleBase
    {
        /// <summary>
        /// Runs the Compare console example
        /// </summary>
        /// <param name="dataFile"></param>
        /// <param name="csvTruthFile"></param>
        /// <param name="output"></param>
        /// <param name="loggerFactory"></param>
        /// <param name="stoppingToken"></param>
        /// <param name="logger"></param>
        /// <returns>Allow passing in of an external logger
        /// for automated comparison</returns>
        public static async Task Run(
            string dataFile,
            string csvTruthFile,
            TextWriter output,
            ILoggerFactory loggerFactory,
            CancellationToken stoppingToken,
            ILogger logger = null)
        {
            using var ipiEngine = BuildEngine(dataFile, loggerFactory);
            await Run(
                ipiEngine,
                csvTruthFile,
                output,
                loggerFactory,
                stoppingToken,
                logger);
        }

        /// <summary>
        /// Builds the on-premise IP Intelligence engine used by the example.
        /// Exposed so callers comparing many truth files against the same
        /// data file can build the engine once and pass it to
        /// <see cref="Run(IpiOnPremiseEngine, string, TextWriter,
        /// ILoggerFactory, CancellationToken, ILogger)"/> for each file.
        /// The caller is responsible for disposing the returned engine.
        /// </summary>
        /// <param name="dataFile"></param>
        /// <param name="loggerFactory"></param>
        /// <returns></returns>
        public static IpiOnPremiseEngine BuildEngine(
            string dataFile,
            ILoggerFactory loggerFactory)
        {
            // Ensure that batch latency mode is always enabled.
            GCSettings.LatencyMode = GCLatencyMode.Batch;

            // Build a new on-premise IP Intelligence engine with the LowMemory
            // profile so the large data file is paged from disk, not loaded into RAM.
            return new IpiOnPremiseEngineBuilder(loggerFactory)
                // LowMemory keeps the (multi-gigabyte) IP Intelligence data file on
                // disk rather than loading it entirely into memory. See the
                // documentation for more detail on this and other configuration options.
                // https://51degrees.com/documentation/_features__automatic_datafile_updates.html?utm_source=code&utm_medium=example&utm_campaign=ip-intelligence-dotnet-examples&utm_content=examples-onpremise-compare-console-program.cs&utm_term=run
                .SetPerformanceProfile(PerformanceProfiles.LowMemory)
                // inhibit auto-update of the data file for this test
                .SetAutoUpdate(false)
                .SetDataFileSystemWatcher(false)
                .SetDataUpdateOnStartup(false)
                // Set to only return from processing the properties needed.
                .SetProperty("Latitude")
                .SetProperty("Longitude")
                .SetProperty("LocationConfidence")
                .SetProperty("Areas")
                // Optimize for the expected parallel workload.
                .SetConcurrency((ushort)Environment.ProcessorCount)
                .Build(dataFile, false);
        }

        /// <summary>
        /// Runs the Compare console example against an already built engine.
        /// The engine is not disposed, the caller retains ownership.
        /// </summary>
        /// <param name="ipiEngine"></param>
        /// <param name="csvTruthFile"></param>
        /// <param name="output"></param>
        /// <param name="loggerFactory"></param>
        /// <param name="stoppingToken"></param>
        /// <param name="logger"></param>
        /// <returns>Allow passing in of an external logger
        /// for automated comparison</returns>
        public static async Task Run(
            IpiOnPremiseEngine ipiEngine,
            string csvTruthFile,
            TextWriter output,
            ILoggerFactory loggerFactory,
            CancellationToken stoppingToken,
            ILogger logger = null)
        {
            logger ??= loggerFactory.CreateLogger<Example>();

            // Ensure that batch latency mode is always enabled.
            GCSettings.LatencyMode = GCLatencyMode.Batch;

            // Build a pipeline to consumer the IP intelligence engine. Needed
            // so that flowdata can be used to pass evidence in and get
            // results.
            using var pipeline = new PipelineBuilder(loggerFactory)
                .AddFlowElement(ipiEngine)
                .SetAutoDisposeElements(false)
                .Build();

            // Create a reader for the source of truth.
            using var reader = File.OpenText(csvTruthFile);
            var config = CsvConfiguration.FromAttributes<Truth>(
                CultureInfo.InvariantCulture);
            config.MissingFieldFound = null;
            using var source = new CsvReader(reader, config);

            // Create a collection of truths to ensure that there are records
            // always available to the consumer.
            var truth = new BlockingCollection<Truth>(
                Environment.ProcessorCount);

            // Records that can not be read or compared are logged and
            // skipped so that one bad record does not stop the comparison.
            var skipped = new SkippedTruths(logger);

            // Stopped by the caller, or by a consumer that failed.
            using var stopping = CancellationTokenSource
                .CreateLinkedTokenSource(stoppingToken);

            // Create consumers that are used to add the result to the truth.
            // These run in parallel to ensure best performance as the IPI and
            // area calculations can be time consuming compared to reading new
            // truth records.
            var consumers = CreateConsumers(
                pipeline,
                truth,
                skipped,
                stopping);
            logger.LogInformation(
                "Created '{0}' consumer processors",
                consumers.Length);

            // Use the main thread as the producer adding truths for the
            // consumers to process. Every consumer has stopped by the time
            // this returns, whether the producer finished or failed.
            try
            {
                await ProduceAndConsume(
                    () => AddTruth(
                        logger,
                        source,
                        truth,
                        consumers,
                        skipped,
                        stopping.Token),
                    truth,
                    consumers,
                    stopping);
            }
            finally
            {
                skipped.LogSummary(csvTruthFile);
            }

            // Create the write for the destination output.
            using var writer = new CsvWriter(
                output,
                new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    Delimiter = ","
                });

            // Write out the records each consumer generated.
            foreach (var consumer in consumers)
            {
                logger.LogInformation(
                    "Finished consumer '{0}'",
                    consumer.Task.Id);
                writer.WriteRecords(consumer.Task.Result);
            }
            skipped.LogSummary(csvTruthFile);

            // Finally check the data file used for consistency with the other
            // examples.
            ExampleUtils.CheckDataFile(
                ipiEngine,
                loggerFactory.CreateLogger<Program>());
        }

        /// <summary>
        /// Create and start the consumers which will be waiting on the
        /// producer to start. The number of consumers matches the number of
        /// processor cores.
        /// </summary>
        /// <param name="pipeline"></param>
        /// <param name="truth"></param>
        /// <param name="skipped">
        /// Records the truths that could not be compared.
        /// </param>
        /// <param name="stopping">
        /// Cancelled when a consumer fails so that the producer stops too.
        /// </param>
        /// <returns></returns>
        private static Consumer[] CreateConsumers(
            IPipeline pipeline,
            BlockingCollection<Truth> truth,
            SkippedTruths skipped,
            CancellationTokenSource stopping)
        {
            return CreateConsumers(
                i => ProcessTruth(pipeline, i),
                truth,
                skipped,
                stopping);
        }

        /// <summary>
        /// Create and start the consumers which will be waiting on the
        /// producer to start. The number of consumers matches the number of
        /// processor cores.
        /// </summary>
        /// <param name="process">
        /// Compares a single truth returning the result, or null if there is
        /// no result for the truth.
        /// </param>
        /// <param name="truth"></param>
        /// <param name="skipped">
        /// Records the truths that could not be compared.
        /// </param>
        /// <param name="stopping">
        /// Cancelled when a consumer fails so that the producer stops too.
        /// </param>
        /// <returns></returns>
        public static Consumer[] CreateConsumers(
            Func<Truth, Result> process,
            BlockingCollection<Truth> truth,
            SkippedTruths skipped,
            CancellationTokenSource stopping)
        {
            return Enumerable.Range(
                0,
                Environment.ProcessorCount).Select(_ =>
                {
                    var consumer = new Consumer();
                    consumer.Task = Task.Factory.StartNew(() =>
                        {
                            try
                            {
                                return ProcessTruth(
                                    process,
                                    truth,
                                    skipped,
                                    stopping.Token);
                            }
                            catch
                            {
                                stopping.Cancel();
                                throw;
                            }
                        },
                        TaskCreationOptions.LongRunning);
                    return consumer;
                }).ToArray();
        }

        /// <summary>
        /// Runs the producer, then waits for every consumer to stop before
        /// returning, whether the producer finished, failed or was stopped.
        /// Returning while a consumer is still running would let the caller
        /// dispose the pipeline and the engine under it.
        /// </summary>
        /// <param name="produce">
        /// Adds the truths to the collection. The collection is completed
        /// when it returns or throws.
        /// </param>
        /// <param name="truth"></param>
        /// <param name="consumers"></param>
        /// <param name="stopping">
        /// Cancelled if the producer fails so that the consumers stop.
        /// </param>
        /// <exception cref="Exception">
        /// The producer's failure once every consumer has stopped, otherwise
        /// the first consumer's failure.
        /// </exception>
        public static async Task ProduceAndConsume(
            Action produce,
            BlockingCollection<Truth> truth,
            Consumer[] consumers,
            CancellationTokenSource stopping)
        {
            ExceptionDispatchInfo failure = null;
            try
            {
                produce();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
                stopping.Cancel();
            }
            finally
            {
                truth.CompleteAdding();
            }

            // Wait for every consumer to stop before going further, even when
            // one has failed. Awaiting them one at a time would return on the
            // first failure while the others are still using the pipeline and
            // the engine.
            try
            {
                await Task.WhenAll(consumers.Select(i => i.Task));
            }
            catch when (failure != null)
            {
                // The consumers were stopped because the producer failed, so
                // the producer's failure is the one to report.
            }
            failure?.Throw();
        }

        private static void AddTruth(
            ILogger logger,
            CsvReader source,
            BlockingCollection<Truth> truth,
            Consumer[] consumers,
            SkippedTruths skipped,
            CancellationToken stoppingToken)
        {
            var process = Process.GetCurrentProcess();
            var lastLog = DateTime.UtcNow;
            var nextLog = lastLog.Add(_logBuild);
            var lastProcessorTime = process.TotalProcessorTime;
            var ips = new HashSet<string>();
            foreach (var item in ReadTruths(source, skipped).TakeWhile(
                _ => stoppingToken.IsCancellationRequested == false))
            {
                try
                {
                    if (ips.Contains(item.Ip) == false)
                    {
                        truth.TryAdd(item, -1, stoppingToken);
                        ips.Add(item.Ip);
                    }
                    LogProgress(
                        logger, 
                        truth, 
                        consumers, 
                        process,
                        ips.Count, 
                        ref lastLog, 
                        ref nextLog, 
                        ref lastProcessorTime, 
                        item);
                }
                catch (OperationCanceledException)
                {
                    // Do nothing and exit as if the truths had been fully
                    // consumed.
                }
            }
            truth.CompleteAdding();
            logger.LogInformation("Finished adding '{0}' sources", ips.Count);
        }

        /// <summary>
        /// Reads the truth records from the CSV source. A record that can not
        /// be read, or is missing the values needed to compare it, is added
        /// to the skipped records and reading continues with the next line.
        /// </summary>
        /// <param name="source">
        /// CSV reader positioned before the header record.
        /// </param>
        /// <param name="skipped">
        /// Records the truths that could not be read.
        /// </param>
        /// <returns>
        /// The truth records that can be compared.
        /// </returns>
        public static IEnumerable<Truth> ReadTruths(
            CsvReader source,
            SkippedTruths skipped)
        {
            if (source.Read() == false)
            {
                yield break;
            }
            source.ReadHeader();

            // A missing or misnamed column would otherwise read as a default
            // value for every record and the comparison would run on it.
            source.ValidateHeader<Truth>();
            var columns = source.HeaderRecord.Length;
            while (source.Read())
            {
                skipped.Read();
                var lineNumber = source.Parser.RawRow;
                Truth item = null;
                string reason = null;
                string detail = null;
                try
                {
                    item = source.GetRecord<Truth>();
                    item.LineNumber = lineNumber;
                    if (source.Parser.Count < columns)
                    {
                        reason = "Malformed record";
                        detail = $"Found '{source.Parser.Count}' of " +
                            $"'{columns}' columns";
                    }
                    else
                    {
                        (reason, detail) = Validate(item);
                    }
                }
                catch (CsvHelperException ex)
                {
                    reason = "Malformed record";
                    detail = ex.InnerException?.Message ?? ex.Message;
                }
                if (reason == null)
                {
                    yield return item;
                }
                else
                {
                    skipped.Add(lineNumber, item?.Ip, reason, detail);
                }
            }
        }

        /// <summary>
        /// Checks the truth has the values needed to compare it.
        /// </summary>
        /// <param name="truth"></param>
        /// <returns>
        /// Null values if the truth is valid, otherwise the reason and detail.
        /// </returns>
        private static (string, string) Validate(Truth truth)
        {
            if (IPAddress.TryParse(truth.Ip, out _) == false)
            {
                return ("Invalid IP address", $"'{truth.Ip}'");
            }
            if (Double.IsFinite(truth.Latitude) == false ||
                Math.Abs(truth.Latitude) > 90 ||
                Double.IsFinite(truth.Longitude) == false ||
                Math.Abs(truth.Longitude) > 180)
            {
                return (
                    "Invalid latitude or longitude",
                    $"'{truth.Latitude},{truth.Longitude}'");
            }
            return (null, null);
        }

        /// <summary>
        /// Logs progress from the producer.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="truth"></param>
        /// <param name="consumers"></param>
        /// <param name="process"></param>
        /// <param name="added"></param>
        /// <param name="lastLog"></param>
        /// <param name="nextLog"></param>
        /// <param name="lastProcessorTime"></param>
        /// <param name="item"></param>
        private static void LogProgress(
            ILogger logger,
            BlockingCollection<Truth> truth, 
            Consumer[] consumers,
            Process process, 
            int added, 
            ref DateTime lastLog, 
            ref DateTime nextLog,
            ref TimeSpan lastProcessorTime,
            Truth item)
        {
            if (DateTime.UtcNow >= nextLog)
            {
                // Log the ranges and other telemetry.
                logger.LogInformation(
                    "Processed '{0}' source records with '{1}' in " +
                    "queue, most recent '{2:N2},{3:N2}', and '{4}' " +
                    "consumers",
                    added,
                    truth.Count,
                    item.Latitude,
                    item.Longitude,
                    consumers.Count(i => i.Task.IsCompleted == false));

                // Get the elapsed time since last logged.
                var elapsed = DateTime.UtcNow - lastLog;

                // The amount of CPU used is the processor time
                // difference divided by the wall clock time
                // difference.
                var cpu =
                    (process.TotalProcessorTime - lastProcessorTime) /
                    elapsed;

                // Work out the number of queries per second.
                var total = consumers.Sum(i => i.Count);
                var qps = total / elapsed.TotalSeconds;

                // Log the resource usage.
                logger.LogInformation(
                    "'{0:F2}' processors used, '{1:N0} qps, " +
                    "'{2}' threads, '{3}' handles, and '{4:N0}MB' " +
                    "memory used",
                    cpu,
                    qps,
                    process.Threads.Count,
                    process.HandleCount,
                    process.WorkingSet64 / 1000);

                // Reset the count of queries to IPI.
                foreach (var consumer in consumers)
                {
                    consumer.Count = 0;
                }

                // Reset the other logging parameters.
                lastLog = DateTime.UtcNow;
                nextLog = DateTime.UtcNow.Add(_logBuild);
                lastProcessorTime = process.TotalProcessorTime;
            }
        }

        /// <summary>
        /// Processes the truths in the blocking collection until completed or
        /// stopped.
        /// </summary>
        /// <param name="process">
        /// Compares a single truth returning the result, or null if there is
        /// no result for the truth.
        /// </param>
        /// <param name="source"></param>
        /// <param name="skipped">
        /// Records the truths that could not be compared.
        /// </param>
        /// <param name="stoppingToken"></param>
        /// <returns>
        /// A list of the output results.
        /// </returns>
        public static IReadOnlyList<Output> ProcessTruth(
            Func<Truth, Result> process,
            BlockingCollection<Truth> source,
            SkippedTruths skipped,
            CancellationToken stoppingToken)
        {
            var output = new List<Output>();
            while (source.IsCompleted == false &&
                stoppingToken.IsCancellationRequested == false)
            {
                try
                {
                    if (source.TryTake(out var truth, -1, stoppingToken))
                    {
                        Result result = null;
                        try
                        {
                            result = process(truth);
                        }
                        catch (InvalidDataException ex)
                        {
                            // Log and skip the record, then carry on with
                            // the next one.
                            skipped.Add(
                                truth.LineNumber,
                                truth.Ip,
                                ex.Message,
                                ex.InnerException?.Message);
                        }
                        if (result != null)
                        {
                            output.Add(new Output(truth, result));
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Do nothing and exit the consumer.
                }
            }
            return output;
        }

        /// <summary>
        /// Process the specific truth returning the result.
        /// </summary>
        /// <param name="pipeline"></param>
        /// <param name="truth"></param>
        /// <returns></returns>
        private static Result ProcessTruth(IPipeline pipeline, Truth truth)
        {
            // Get the data for the IP address.
            using var flowData = pipeline.CreateFlowData();
            flowData.AddEvidence("query.client-ip", truth.Ip);
            flowData.Process();
            var data = flowData.Get<IIpIntelligenceData>();

            // Check if the required properties have values. If not, skip this
            // record as the IP address was not found in the database.
            if (data.Latitude.HasValue == false ||
                data.Longitude.HasValue == false ||
                data.Areas.HasValue == false)
            {
                return null;
            }

            return Compare(
                truth,
                data.Latitude.Value,
                data.Longitude.Value,
                data.Areas.Value.Value,
                data.LocationConfidence.HasValue
                    ? data.LocationConfidence.Value
                    : null);
        }

        /// <summary>
        /// Compares the truth with the location and area returned for its IP
        /// address.
        /// </summary>
        /// <param name="truth"></param>
        /// <param name="latitude">
        /// Returned for the IP address.
        /// </param>
        /// <param name="longitude">
        /// Returned for the IP address.
        /// </param>
        /// <param name="wkt">
        /// The area returned for the IP address in WKT format.
        /// </param>
        /// <param name="confidence">
        /// Returned for the IP address, if any.
        /// </param>
        /// <returns></returns>
        /// <exception cref="InvalidDataException">
        /// The truth can not be compared, for example because the area is
        /// not a valid geometry.
        /// </exception>
        public static Result Compare(
            Truth truth,
            double latitude,
            double longitude,
            string wkt,
            string confidence)
        {
            // Set the address family of the source truth does not provide it.
            if (String.IsNullOrEmpty(truth.AddressFamily))
            {
                if (IPAddress.TryParse(truth.Ip, out var address) == false)
                {
                    throw new InvalidDataException("Invalid IP address");
                }
                truth.AddressFamily = address.AddressFamily.ToString();
            }

            // Get the truth and result as points.
            var truthPoint = new GeoCoordinate(
                truth.Latitude,
                truth.Longitude);
            var resultPoint = new GeoCoordinate(
                latitude,
                longitude);

            // Get the area result for the returned data and the true latitude
            // and longitude.
            global::Examples.OnPremise.Areas.Result area;
            try
            {
                area = Calculations.GetAreas(
                    wkt,
                    truth.Latitude,
                    truth.Longitude);
            }
            catch (Exception ex)
            {
                // Any failure working out the area, including a bug in the
                // area calculation, only affects this record.
                throw new InvalidDataException(
                    "Area could not be compared",
                    ex);
            }

            // Return the result including the latitude, longitude, and
            // distance in kilometers between the result and the truth.
            return new Result()
            {
                Latitude = resultPoint.Latitude,
                Longitude = resultPoint.Longitude,
                Confidence = confidence,
                DistanceKms = truthPoint.GetDistanceTo(resultPoint) / 1000,
                SquareKms = area.SquareKms,
                Geometries = area.Geometries,
                Contains = area.Contains
            };
        }
    }

    static void Main(string[] args)
    {
        var configuration = new Configuration
        {
            // Use the supplied path for the data file or find the lite file that
            // is included in the repository.
            DataFile = args.Length > 0 ? args[0] :
            // In this example, by default, the 51Degrees IP Intelligence data
            // file needs to be somewhere in the project space, or you may
            // specify another file as a command line parameter.
            //
            // For testing, contact us to obtain an enterprise data file:
            // https://51degrees.com/contact-us?utm_source=code&utm_medium=example&utm_campaign=ip-intelligence-dotnet-examples&utm_content=examples-onpremise-compare-console-program.cs&utm_term=main
                Examples.ExampleUtils.FindDataFile(
                    Constants.ENTERPRISE_IPI_DATA_FILE_NAME),

            // Get the of the CSV file containing source truth.
            CsvTruthFile = args.Length > 1 
            ? args[1] 
            : Examples.ExampleUtils.FindFile(
                    Constants.GEOIP_COMPARISON_EVIDENCE_FILE_NAME),
        };

        // Get the location for the output file. Use the same location as the
        // evidence if a path is not supplied on the command line.
        var outputFile = args.Length > 2 ? args[2] : "compare-output.csv";

        File.WriteAllText("Metrics_DataFileName.txt", configuration.DataFile);

        // Configure a logger to output to the console.
        configuration.LoggerFactory = LoggerFactory.Create(b => b.AddConsole());
        
        if (configuration.DataFile != null)
        {
            using var output = File.CreateText(outputFile);
            configuration.Output = output;
            var builder = Host.CreateApplicationBuilder([]);
            builder.Services.AddSingleton(configuration);
            builder.Services.AddHostedService<Worker>();
            using var host = builder.Build();
            host.Run();
        }
        else
        {
            var logger = configuration.LoggerFactory.CreateLogger<Program>();
            logger.LogError("Failed to find a IP Intelligence data file. " +
                "Make sure the ip-intelligence-data submodule has been " +
                "updated by running `git submodule update --recursive`. By " +
                "default, the 'lite' file included with this code will be " +
                "used. A different file can be specified by supplying the " +
                "full path as a command line argument");
        }

        // Dispose the logger to ensure any messages get flushed
        configuration.LoggerFactory.Dispose();
    }
}