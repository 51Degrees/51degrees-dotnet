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

// Ignore Spelling: Ip Wkt

using CsvHelper;
using CsvHelper.Configuration;
using Examples.OnPremise.Areas;
using FiftyOne.IpIntelligence.Engine.OnPremise.FlowElements;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Reflection;
using System.Threading;

/// <summary>
/// @example OnPremise/LocationAnalysis-Console/Program.cs
///
/// This example produces an independent analysis of the location data
/// contained in a given 51Degrees IP Intelligence data file (.ipi). It walks
/// the IP address space range by range using the IpRangeStart and IpRangeEnd
/// properties returned for each lookup, so every unique issued IP address is
/// included in the analysis without needing any input list of addresses.
///
/// The results are grouped by a configurable set of key fields. By default
/// these are ContinentName, Country, LocationConfidence, and ConnectionType.
/// Any property of `IIpIntelligenceData` can be used by passing a comma
/// separated list of property names as a command line argument. For each
/// group the example records the number of IP addresses in the group and the
/// average geographic area in square kilometers of the areas returned. The
/// results are written to a CSV file for further analysis.
///
/// IPv4 groups count individual IP addresses. IPv6 groups count /64 subnets,
/// as a /64 is the standard end site allocation and counting individual IPv6
/// addresses would produce impractically large numbers.
///
/// For very large data files a random sample percentage can be supplied so
/// the example completes in a reasonable time. After each observed range the
/// walk skips ahead by a random number of addresses in proportion to the
/// sample percentage, and the counts are scaled to remain estimates of the
/// whole address space. A sample percentage of 1 (the default) walks every
/// range and produces exact counts.
///
/// This example is available in full on [GitHub](https://github.com/51Degrees/ip-intelligence-dotnet-examples/blob/master/Examples/OnPremise/LocationAnalysis-Console/Program.cs).
///
/// This example can be run against the free 'Lite' IP Intelligence data file
/// obtained via the [ip-intelligence-data repository](https://github.com/51Degrees/ip-intelligence-data),
/// although the Lite file contains fewer properties and less precise data
/// than the enterprise data file. To obtain an enterprise data file for
/// testing, please [contact us](https://51degrees.com/contact-us?utm_source=code&amp;utm_medium=example&amp;utm_campaign=ip-intelligence-dotnet-examples&amp;utm_content=examples-onpremise-locationanalysis-console-program.cs&amp;utm_term=header).
///
/// Required NuGet Dependencies:
/// - [FiftyOne.IpIntelligence](https://www.nuget.org/packages/FiftyOne.IpIntelligence/)
/// - [Microsoft.Extensions.Logging.Console](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Console/)
/// - [CsvHelper](https://www.nuget.org/packages/CsvHelper/)
/// </summary>
namespace FiftyOne.IpIntelligence.Examples.OnPremise.LocationAnalysis;

public class Program
{
    /// <summary>
    /// Time interval to wait between logging progress.
    /// </summary>
    private static readonly TimeSpan _logInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The percentage of the IP address space to include in the analysis,
    /// expressed as a fraction where 1 is 100%. The default walks the whole
    /// address space and produces exact counts. Use a lower value, for
    /// example 0.01, when analyzing a large enterprise data file so the
    /// example completes in a reasonable time.
    /// </summary>
    private const double DEFAULT_SAMPLE_PERCENTAGE = 1;

    /// <summary>
    /// The metrics recorded for a single combination of the group key field
    /// values.
    /// </summary>
    public class Group
    {
        /// <summary>
        /// The values of the key fields for this group. The first entry is
        /// the address family followed by one entry for each group property.
        /// </summary>
        public string[] Values { get; }

        /// <summary>
        /// The number of IP addresses (IPv4) or /64 subnets (IPv6) that
        /// belong to this group. When a sample percentage below 1 is used
        /// this is an estimate scaled to the whole address space.
        /// </summary>
        public decimal IpCount { get; set; }

        /// <summary>
        /// The number of IP ranges that were observed for this group.
        /// </summary>
        public long SampledRanges { get; set; }

        /// <summary>
        /// Sum of the geographic area in square kilometers weighted by the
        /// number of IP addresses each observation represents.
        /// </summary>
        public double TotalAreaSqKm { get; set; }

        /// <summary>
        /// The number of IP addresses which contributed a geographic area to
        /// <see cref="TotalAreaSqKm"/>.
        /// </summary>
        public decimal AreaIpCount { get; set; }

        /// <summary>
        /// The average geographic area in square kilometers returned for the
        /// IP addresses in this group, or 0 if no areas were available.
        /// </summary>
        public double AverageAreaSqKm => AreaIpCount > 0 ?
            TotalAreaSqKm / (double)AreaIpCount : 0;

        public Group(string[] values)
        {
            Values = values;
        }
    }

    /// <summary>
    /// A group key field together with a flag indicating whether the
    /// property is present in the data file being analyzed. Fields that are
    /// not present still appear in the output so the CSV shape does not
    /// depend on the data file tier, but they are never read from results.
    /// </summary>
    public class GroupField
    {
        public PropertyInfo Property { get; }

        public bool Available { get; }

        public GroupField(PropertyInfo property, bool available)
        {
            Property = property;
            Available = available;
        }
    }

    /// <summary>
    /// Implementation of the example that can be called from the Program's
    /// main method or any other consuming service such as a test.
    /// </summary>
    public class Example : ExampleBase
    {
        /// <summary>
        /// The key fields used to produce groups when none are specified.
        /// </summary>
        public static readonly IReadOnlyList<string> DefaultGroupProperties =
            new[]
            {
                "ContinentName",
                "Country",
                "LocationConfidence",
                "ConnectionType"
            };

        /// <summary>
        /// Runs the location analysis example.
        /// </summary>
        /// <param name="dataFile">
        /// IP Intelligence data file to analyze.
        /// </param>
        /// <param name="output">
        /// Output writer that receives the resulting CSV.
        /// </param>
        /// <param name="loggerFactory">
        /// Factory to use when creating loggers.
        /// </param>
        /// <param name="samplePercentage">
        /// Fraction of the IP address space to include, where 1 is 100%.
        /// </param>
        /// <param name="groupPropertyNames">
        /// Names of the `IIpIntelligenceData` properties used to produce
        /// groups, or null to use <see cref="DefaultGroupProperties"/>.
        /// </param>
        /// <param name="stoppingToken">
        /// Cancellation token that when fired stops the walk early and
        /// writes the results collected so far.
        /// </param>
        /// <param name="logger">
        /// Allow passing in of an external logger for automated testing.
        /// </param>
        public static void Run(
            string dataFile,
            TextWriter output,
            ILoggerFactory loggerFactory,
            double samplePercentage = DEFAULT_SAMPLE_PERCENTAGE,
            IReadOnlyList<string> groupPropertyNames = null,
            CancellationToken stoppingToken = default,
            ILogger logger = null)
        {
            logger ??= loggerFactory.CreateLogger<Example>();

            if (samplePercentage <= 0 || samplePercentage > 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(samplePercentage),
                    "The sample percentage must be greater than 0 and no " +
                    "more than 1, where 1 includes the entire address " +
                    "space.");
            }

            // Build a new on-premise IP Intelligence engine with the
            // LowMemory profile so the large data file is paged from disk,
            // not loaded into RAM. See the documentation for more detail on
            // this and other configuration options.
            // https://51degrees.com/documentation/_features__automatic_datafile_updates.html?utm_source=code&utm_medium=example&utm_campaign=ip-intelligence-dotnet-examples&utm_content=examples-onpremise-locationanalysis-console-program.cs&utm_term=run
            using var ipiEngine = new IpiOnPremiseEngineBuilder(loggerFactory)
                .SetPerformanceProfile(PerformanceProfiles.LowMemory)
                // Inhibit auto-update of the data file for this example.
                .SetAutoUpdate(false)
                .SetDataFileSystemWatcher(false)
                .SetDataUpdateOnStartup(false)
                .Build(dataFile, false);

            // Build a pipeline to consume the IP Intelligence engine. Needed
            // so that flow data can be used to pass evidence in and get
            // results.
            using var pipeline = new PipelineBuilder(loggerFactory)
                .AddFlowElement(ipiEngine)
                .SetAutoDisposeElements(false)
                .Build();

            // The properties present in the data file being analyzed. Lower
            // data file tiers contain fewer properties, so availability is
            // checked once here rather than producing a warning for every
            // lookup.
            var availableProperties = new HashSet<string>(
                ipiEngine.Properties.Select(i => i.Name),
                StringComparer.OrdinalIgnoreCase);

            // Resolve the requested group property names against the
            // properties available on IIpIntelligenceData and record
            // whether each is present in the data file.
            var groupFields = GetGroupFields(
                groupPropertyNames ?? DefaultGroupProperties,
                availableProperties,
                logger);

            // Check whether the walk can use range ends and areas.
            var hasRangeEnd = availableProperties.Contains(
                nameof(IIpIntelligenceData.IpRangeEnd));
            if (hasRangeEnd == false)
            {
                logger.LogWarning(
                    "The data file does not contain the IpRangeEnd " +
                    "property so the walk advances in fixed strides of " +
                    "1/65536th of the address space and the counts are " +
                    "approximate at that granularity.");
            }
            var hasAreas = availableProperties.Contains(
                nameof(IIpIntelligenceData.Areas));
            if (hasAreas == false)
            {
                logger.LogWarning(
                    "The data file does not contain the Areas property so " +
                    "the average area column will be 0.");
            }

            logger.LogInformation(
                "Analyzing data file '{0}' (tier '{1}', published " +
                "'{2:yyyy-MM-dd}') sampling '{3:P2}' of the address space " +
                "grouped by '{4}'",
                dataFile,
                ipiEngine.DataSourceTier,
                ipiEngine.DataFiles[0].DataPublishedDateTime,
                samplePercentage,
                string.Join(
                    ", ",
                    groupFields.Select(i => i.Property.Name)));

            var groups = new Dictionary<string, Group>();
            // Cache of area calculation results for each distinct WKT string
            // so the relatively expensive geometric calculation only happens
            // once for each distinct area in the data file.
            var areaCache = new Dictionary<string, int>();
            var random = new Random();

            // Walk the IPv4 address space, then the IPv6 address space.
            foreach (var family in new[]
            {
                AddressFamily.InterNetwork,
                AddressFamily.InterNetworkV6
            })
            {
                Analyze(
                    pipeline,
                    family,
                    groupFields,
                    hasRangeEnd,
                    hasAreas,
                    groups,
                    areaCache,
                    samplePercentage,
                    random,
                    logger,
                    stoppingToken);
            }

            WriteCsv(output, groupFields, groups.Values);

            logger.LogInformation(
                "Analysis produced '{0}' groups from '{1}' observed ranges " +
                "and '{2}' distinct areas",
                groups.Count,
                groups.Values.Sum(i => i.SampledRanges),
                areaCache.Count);

            // Finally check the data file used for consistency with the
            // other examples.
            ExampleUtils.CheckDataFile(
                ipiEngine,
                loggerFactory.CreateLogger<Program>());
        }

        /// <summary>
        /// Walks the address space for the family provided passing the start
        /// of each IP range to the pipeline and recording the results
        /// against the group the range belongs to. The IpRangeEnd property
        /// of each result is used to jump directly to the next range so the
        /// number of lookups is proportional to the number of ranges in the
        /// data file rather than the number of possible addresses.
        /// </summary>
        private static void Analyze(
            IPipeline pipeline,
            AddressFamily family,
            GroupField[] groupFields,
            bool hasRangeEnd,
            bool hasAreas,
            Dictionary<string, Group> groups,
            Dictionary<string, int> areaCache,
            double samplePercentage,
            Random random,
            ILogger logger,
            CancellationToken stoppingToken)
        {
            var familyName = family == AddressFamily.InterNetwork ?
                "IPv4" : "IPv6";
            var bits = family == AddressFamily.InterNetwork ? 32 : 128;
            var maxAddress = (BigInteger.One << bits) - 1;
            // When a result does not include a range end the walk advances
            // by this many addresses so that it always makes progress. The
            // step is 1/65536th of the address space for the family.
            var fallbackStep = BigInteger.One << (bits - 16);
            var current = BigInteger.Zero;
            long ranges = 0;
            long missingEnds = 0;
            var nextLog = DateTime.UtcNow.Add(_logInterval);

            while (current <= maxAddress &&
                stoppingToken.IsCancellationRequested == false)
            {
                var address = ToAddress(current, family);
                BigInteger end;
                var values = new string[groupFields.Length + 1];
                values[0] = familyName;
                int? areaSqKm;

                // FlowData is wrapped in a using block in order to ensure
                // that the unmanaged resources allocated by the native IP
                // Intelligence library are freed.
                using (var flowData = pipeline.CreateFlowData())
                {
                    flowData.AddEvidence(
                        "query.client-ip",
                        address.ToString());
                    flowData.Process();
                    var data = flowData.Get<IIpIntelligenceData>();

                    // Get the end of the range that the current address
                    // belongs to.
                    end = GetRangeEnd(
                        data,
                        family,
                        hasRangeEnd,
                        current,
                        maxAddress,
                        fallbackStep,
                        ref missingEnds);

                    // Get the value of each group key field.
                    for (var i = 0; i < groupFields.Length; i++)
                    {
                        values[i + 1] = GetGroupValue(
                            data,
                            groupFields[i]);
                    }

                    // Get the geographic area for the result if available.
                    areaSqKm = hasAreas ?
                        GetAreaSqKm(data, areaCache) : null;
                }

                // Work out the number of IP addresses (IPv4) or /64 subnets
                // (IPv6) the observed range represents, scaled up when only
                // a sample of the address space is being observed.
                var size = end - current + 1;
                var weight = (decimal)ToUnits(size, family) /
                    (decimal)samplePercentage;

                // Add the observation to the group.
                var key = string.Join("\u0001", values);
                if (groups.TryGetValue(key, out var group) == false)
                {
                    group = new Group(values);
                    groups.Add(key, group);
                }
                group.IpCount += weight;
                group.SampledRanges++;
                if (areaSqKm.HasValue)
                {
                    group.TotalAreaSqKm += (double)weight * areaSqKm.Value;
                    group.AreaIpCount += weight;
                }
                ranges++;

                // Move to the address that follows the observed range. When
                // sampling, also skip ahead by a random number of addresses
                // so that on average the requested percentage of the address
                // space is observed.
                current = end + 1;
                if (samplePercentage < 1)
                {
                    current += RandomSkip(size, samplePercentage, random);
                }

                if (DateTime.UtcNow >= nextLog)
                {
                    logger.LogInformation(
                        "Processed '{0}' {1} ranges, current address " +
                        "'{2}', '{3}' groups so far",
                        ranges,
                        familyName,
                        address,
                        groups.Count);
                    nextLog = DateTime.UtcNow.Add(_logInterval);
                }
            }

            logger.LogInformation(
                "Finished {0} after '{1}' ranges of which '{2}' had no " +
                "range end available",
                familyName,
                ranges,
                missingEnds);
        }

        /// <summary>
        /// Resolves the requested group property names to properties of
        /// <see cref="IIpIntelligenceData"/>. Unknown names are reported and
        /// skipped so a typo does not silently change the analysis.
        /// Properties which exist but are not present in the data file are
        /// kept, and reported once, so the CSV shape stays the same across
        /// data file tiers.
        /// </summary>
        private static GroupField[] GetGroupFields(
            IEnumerable<string> names,
            HashSet<string> availableProperties,
            ILogger logger)
        {
            var fields = new List<GroupField>();
            foreach (var name in names)
            {
                var property = typeof(IIpIntelligenceData).GetProperty(
                    name.Trim(),
                    BindingFlags.Public |
                    BindingFlags.Instance |
                    BindingFlags.IgnoreCase);
                if (property == null)
                {
                    logger.LogWarning(
                        "The group property '{0}' is not a property of " +
                        "{1} and will be ignored",
                        name.Trim(),
                        nameof(IIpIntelligenceData));
                }
                else
                {
                    var available = availableProperties.Contains(
                        property.Name);
                    if (available == false)
                    {
                        logger.LogWarning(
                            "The group property '{0}' is not present in " +
                            "the data file so its value will be reported " +
                            "as 'NotAvailable'",
                            property.Name);
                    }
                    fields.Add(new GroupField(property, available));
                }
            }
            if (fields.Count == 0)
            {
                throw new ArgumentException(
                    "None of the requested group properties are " +
                    $"properties of {nameof(IIpIntelligenceData)}.");
            }
            return fields.ToArray();
        }

        /// <summary>
        /// Gets the value of the property provided as a string suitable for
        /// use as part of a group key. Properties which are not available in
        /// the data file being analyzed return 'NotAvailable' and properties
        /// with no value for the range return 'Unknown'.
        /// </summary>
        private static string GetGroupValue(
            IIpIntelligenceData data,
            GroupField field)
        {
            if (field.Available == false)
            {
                // The property is not present in the data file tier being
                // analyzed. For example the Lite file contains fewer
                // properties than the enterprise file.
                return "NotAvailable";
            }
            object value;
            try
            {
                value = field.Property.GetValue(data);
            }
            catch (TargetInvocationException ex)
                when (ex.InnerException is PropertyMissingException)
            {
                return "NotAvailable";
            }
            if (value is IAspectPropertyValue aspectValue)
            {
                if (aspectValue.HasValue == false)
                {
                    return "Unknown";
                }
                return FormatValue(aspectValue.Value);
            }
            return value == null ? "Unknown" : FormatValue(value);
        }

        /// <summary>
        /// Formats a property value as an invariant culture string. Weighted
        /// list values, for example the CountryCodesGeographical property,
        /// are joined with '|' so they remain a single CSV field.
        /// </summary>
        private static string FormatValue(object value)
        {
            switch (value)
            {
                case null:
                    return "Unknown";
                case string text:
                    return text;
                case IFormattable formattable:
                    return formattable.ToString(
                        null,
                        CultureInfo.InvariantCulture);
                case System.Collections.IEnumerable items:
                    return string.Join("|", items
                        .Cast<object>()
                        .Select(UnwrapWeightedValue));
                default:
                    return value.ToString();
            }
        }

        /// <summary>
        /// Returns the value contained in an IWeightedValue instance, or the
        /// string form of the instance if it is not a weighted value.
        /// </summary>
        private static string UnwrapWeightedValue(object item)
        {
            if (item == null)
            {
                return string.Empty;
            }
            var valueProperty = item.GetType().GetProperty("Value");
            if (valueProperty != null)
            {
                return FormatValue(valueProperty.GetValue(item));
            }
            return item.ToString();
        }

        /// <summary>
        /// Gets the end of the range the result relates to, or a fallback
        /// position when no usable range end is available so the walk always
        /// makes progress.
        /// </summary>
        private static BigInteger GetRangeEnd(
            IIpIntelligenceData data,
            AddressFamily family,
            bool hasRangeEnd,
            BigInteger current,
            BigInteger maxAddress,
            BigInteger fallbackStep,
            ref long missingEnds)
        {
            try
            {
                if (hasRangeEnd &&
                    data.IpRangeEnd.HasValue &&
                    data.IpRangeEnd.Value != null &&
                    data.IpRangeEnd.Value.AddressFamily == family)
                {
                    var end = ToNumber(data.IpRangeEnd.Value);
                    if (end >= current)
                    {
                        return BigInteger.Min(end, maxAddress);
                    }
                }
            }
            catch (PropertyMissingException)
            {
                // Fall through to the fallback step below.
            }
            missingEnds++;
            return BigInteger.Min(current + fallbackStep - 1, maxAddress);
        }

        /// <summary>
        /// Gets the geographic area in square kilometers for the result, or
        /// null when no area is available. Results are cached by WKT string
        /// as many ranges share the same area.
        /// </summary>
        private static int? GetAreaSqKm(
            IIpIntelligenceData data,
            Dictionary<string, int> areaCache)
        {
            string wkt;
            try
            {
                if (data.Areas.HasValue == false ||
                    data.Areas.Value == null)
                {
                    return null;
                }
                wkt = data.Areas.Value.Value;
            }
            catch (PropertyMissingException)
            {
                // The Areas property is not present in the data file tier
                // being analyzed.
                return null;
            }
            if (string.IsNullOrEmpty(wkt))
            {
                return null;
            }
            if (areaCache.TryGetValue(wkt, out var squareKms) == false)
            {
                try
                {
                    squareKms = Calculations.GetAreas(wkt, 0, 0).SquareKms;
                }
                catch (Exception)
                {
                    // In rare cases the WKT cannot be turned into an area.
                    // Record -1 so the same WKT is not retried.
                    squareKms = -1;
                }
                areaCache.Add(wkt, squareKms);
            }
            return squareKms >= 0 ? squareKms : (int?)null;
        }

        /// <summary>
        /// Works out the number of units the range size represents. IPv4
        /// counts individual addresses. IPv6 counts /64 subnets rounding up
        /// so a range smaller than a /64 still counts as one unit.
        /// </summary>
        private static BigInteger ToUnits(
            BigInteger size,
            AddressFamily family)
        {
            if (family == AddressFamily.InterNetwork)
            {
                return size;
            }
            var subnetSize = BigInteger.One << 64;
            return BigInteger.Max(
                BigInteger.One,
                (size + subnetSize - 1) / subnetSize);
        }

        /// <summary>
        /// Works out a random number of addresses to skip so that on average
        /// the requested percentage of the address space is observed. The
        /// expected skip after a range of the size provided is
        /// size * (1 - p) / p, with a random factor between 0 and 2 so the
        /// observed ranges are spread across the space rather than falling
        /// on a fixed stride.
        /// </summary>
        private static BigInteger RandomSkip(
            BigInteger rangeSize,
            double samplePercentage,
            Random random)
        {
            var factor = (1.0 - samplePercentage) / samplePercentage *
                random.NextDouble() * 2.0;
            return new BigInteger((double)rangeSize * factor);
        }

        /// <summary>
        /// Converts an IP address to its numeric position in the address
        /// space.
        /// </summary>
        private static BigInteger ToNumber(IPAddress address)
        {
            return new BigInteger(
                address.GetAddressBytes(),
                isUnsigned: true,
                isBigEndian: true);
        }

        /// <summary>
        /// Converts a numeric position in the address space back to an IP
        /// address of the family provided.
        /// </summary>
        private static IPAddress ToAddress(
            BigInteger value,
            AddressFamily family)
        {
            var length = family == AddressFamily.InterNetwork ? 4 : 16;
            var raw = value.ToByteArray(
                isUnsigned: true,
                isBigEndian: true);
            var bytes = new byte[length];
            raw.CopyTo(bytes, length - raw.Length);
            return new IPAddress(bytes);
        }

        /// <summary>
        /// Writes the groups to the output in CSV format ordered with the
        /// largest groups first.
        /// </summary>
        private static void WriteCsv(
            TextWriter output,
            GroupField[] groupFields,
            IEnumerable<Group> groups)
        {
            using var writer = new CsvWriter(
                output,
                new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    Delimiter = ","
                });

            writer.WriteField("AddressFamily");
            foreach (var field in groupFields)
            {
                writer.WriteField(field.Property.Name);
            }
            writer.WriteField("IpCount");
            writer.WriteField("SampledRanges");
            writer.WriteField("AverageAreaSqKm");
            writer.NextRecord();

            foreach (var group in groups.OrderByDescending(i => i.IpCount))
            {
                foreach (var value in group.Values)
                {
                    writer.WriteField(value);
                }
                writer.WriteField(Math.Round(group.IpCount)
                    .ToString("0", CultureInfo.InvariantCulture));
                writer.WriteField(group.SampledRanges);
                writer.WriteField(group.AverageAreaSqKm
                    .ToString("0", CultureInfo.InvariantCulture));
                writer.NextRecord();
            }

            writer.Flush();
        }
    }

    static void Main(string[] args)
    {
        // Use the supplied path for the data file or find one in the
        // project space.
        var dataFile = args.Length > 0 ? args[0] :
            // In this example, by default, the 51Degrees IP Intelligence
            // data file needs to be somewhere in the project space, or you
            // may specify another file as a command line parameter.
            //
            // For testing, contact us to obtain an enterprise data file:
            // https://51degrees.com/contact-us?utm_source=code&utm_medium=example&utm_campaign=ip-intelligence-dotnet-examples&utm_content=examples-onpremise-locationanalysis-console-program.cs&utm_term=main
            Examples.ExampleUtils.FindDataFile(
                Constants.ENTERPRISE_IPI_DATA_FILE_NAME);

        // Get the location for the output CSV file.
        var outputFile = args.Length > 1 ?
            args[1] : "location-analysis-output.csv";

        // Get the sample percentage or use the default.
        var samplePercentage = args.Length > 2 ?
            double.Parse(args[2], CultureInfo.InvariantCulture) :
            DEFAULT_SAMPLE_PERCENTAGE;

        // Get the group key fields or use the defaults.
        var groupPropertyNames = args.Length > 3 ?
            args[3].Split(',') : null;

        // Configure a logger to output to the console with timestamps.
        var loggerFactory = LoggerFactory.Create(b =>
            b.AddSimpleConsole(options =>
            {
                options.IncludeScopes = false;
                options.SingleLine = true;
                options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
            }));
        var logger = loggerFactory.CreateLogger<Program>();

        if (dataFile != null)
        {
            // Allow Ctrl+C to stop the walk early. The results collected so
            // far are still written to the output file.
            using var cancellationSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellationSource.Cancel();
            };

            using (var output = File.CreateText(outputFile))
            {
                Example.Run(
                    dataFile,
                    output,
                    loggerFactory,
                    samplePercentage,
                    groupPropertyNames,
                    cancellationSource.Token);
            }
            logger.LogInformation(
                "Analysis complete. See results in: '{0}'",
                outputFile);
        }
        else
        {
            logger.LogError("Failed to find a IP Intelligence data file. " +
                "Make sure the ip-intelligence-data submodule has been " +
                "updated by running `git submodule update --recursive`. A " +
                "different file can be specified by supplying the full " +
                "path as a command line argument");
        }

        // Dispose the logger to ensure any messages get flushed
        loggerFactory.Dispose();
    }
}
