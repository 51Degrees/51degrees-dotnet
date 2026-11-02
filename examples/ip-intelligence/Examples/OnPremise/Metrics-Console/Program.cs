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

// Ignore Spelling: Wkt Ip

using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Examples.OnPremise.Areas;
using FiftyOne.IpIntelligence.Engine.OnPremise.FlowElements;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FiftyOne.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

/// <summary>
/// @example OnPremise/Metrics-Console/Program.cs
///
/// This example takes all public IP addresses and passes them to the IP
/// Intelligence service, recording the average geographic area in square
/// kilometers, the number of polygons that form the area, and the equivalent
/// radius if the area can be represented as a circle.
/// 
/// Depending on the available processor cores the example can take a long time
/// to complete.
///
/// The sample used in the metrics can be adjusted with the sample percentage,
/// the cap on the addresses taken from any one range, and the percentage of
/// ranges to include, all supplied as command line parameters. The last of
/// these is the most effective way to shorten a run.
///
/// This example is primarily designed for those who are interested in 
/// verifying the published metrics associated with 51Degrees' 
/// IP intelligence service.
///
/// This example is available in full on [GitHub](https://github.com/51Degrees/ip-intelligence-dotnet-examples/blob/master/Examples/OnPremise/Metrics-Console/Program.cs). 
/// 
/// This example requires an enterprise IP Intelligence data file (.ipi). 
/// To obtain an enterprise data file for testing, please [contact us](https://51degrees.com/contact-us?utm_source=code&amp;utm_medium=example&amp;utm_campaign=ip-intelligence-dotnet-examples&amp;utm_content=examples-onpremise-metrics-console-program.cs&amp;utm_term=header).
/// 
/// Required NuGet Dependencies:
/// - [FiftyOne.IpIntelligence](https://www.nuget.org/packages/FiftyOne.IpIntelligence/)
/// - [Microsoft.Extensions.Logging.Console](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Console/)
/// </summary>
namespace FiftyOne.IpIntelligence.Examples.OnPremise.Metrics;

public static class Extensions
{
    /// <summary>
    /// Time interval to wait between logging profile scan progress.
    /// </summary>
    private static readonly TimeSpan _logScan = TimeSpan.FromSeconds(30);

    /// <param name="engine"></param>
    /// <param name="rangePercentage">
    /// Proportion of the ranges in the data file to return, where 1 is 100%
    /// and returns every range.
    /// </param>
    /// <param name="maxProfilesWithoutRange">
    /// Give up once this many profiles in a row have produced no range. Zero
    /// scans every profile in the data file.
    ///
    /// The ranges sit together near the start of the file and are followed by
    /// a long run of profiles that carry none - measured on an enterprise file
    /// as 601,443 ranges within the first 603,531 profiles, then 993,628 more
    /// profiles yielding nothing. Scanning that run costs the majority of a
    /// complete pass and cannot add anything to the analysis.
    ///
    /// Only 2,088 profiles before the last range produced nothing, so any
    /// limit well above that cannot cut the scan short while ranges remain.
    /// </param>
    /// <param name="logger">
    /// Reports how far through the profiles the scan has got. Long runs of
    /// profiles carry no usable range, so without this the example looks like
    /// it has stopped whenever it is working through one of them.
    /// </param>
    public static IEnumerable<(string, string)>
        ValidRanges(
            this IpiOnPremiseEngine engine,
            double rangePercentage = 1,
            int maxProfilesWithoutRange = 0,
            ILogger logger = null)
    {
        var network = engine.Components.Single(i =>
            "Network".Equals(
                i.Name,
                StringComparison.InvariantCultureIgnoreCase));
        var random = new Random();
        var scanned = 0;
        var found = 0;
        var sinceFound = 0;
        var nextLog = DateTime.UtcNow.Add(_logScan);
        foreach (var profile in engine.Profiles)
        {
            scanned++;
            sinceFound++;
            if (DateTime.UtcNow >= nextLog)
            {
                logger?.LogInformation(
                    "Scanned '{0}' profiles finding '{1}' ranges",
                    scanned,
                    found);
                nextLog = DateTime.UtcNow.Add(_logScan);
            }

            // Stop once the ranges have clearly run out. Only applied after a
            // range has been found so that a file which opens with a long run
            // of profiles carrying none is still scanned.
            if (maxProfilesWithoutRange > 0 &&
                found > 0 &&
                sinceFound > maxProfilesWithoutRange)
            {
                profile.Dispose();
                logger?.LogInformation(
                    "Stopped scanning after '{0}' profiles in a row produced " +
                    "no range, having found '{1}' in '{2}' profiles",
                    sinceFound,
                    found,
                    scanned);
                yield break;
            }

            // Decide whether to keep the profile before reading any of its
            // property values. Reading them costs around a hundred times more
            // than stepping over the profile (measured at 2,300 profiles a
            // second against 246,000 to enumerate them), so skipping here is
            // what makes a smaller sample of ranges actually run faster.
            if (rangePercentage < 1 && random.NextDouble() > rangePercentage)
            {
                profile.Dispose();
                continue;
            }

            // The enumeration yields more entries than the data file holds
            // profiles, and reading the values of one of the surplus entries
            // fails. Values are found by indexing the profile id into a
            // collection, and on the file this was written against that
            // collection holds 35,007,545 while the enumeration yields the
            // same number of entries - so every entry whose id is past it
            // fails, starting at index 1,602,768 with id 35,007,548 where the
            // one before it, id 35,007,525, reads cleanly.
            //
            // The last entry that reads is therefore the last real profile,
            // and the ranges found up to it are the whole data file rather
            // than part of it. Stop there instead of letting the failure end
            // the run.
            (string, string) range = default;
            Exception unreadable = null;
            try
            {
                range = GetRange(network, profile);
            }
            catch (Exception ex)
            {
                unreadable = ex;
            }
            profile.Dispose();

            if (unreadable != null)
            {
                logger?.LogWarning(
                    unreadable,
                    "Stopped scanning at profile '{0}', whose values could " +
                    "not be read. Found '{1}' ranges before it",
                    scanned,
                    found);
                yield break;
            }

            if (range.Item1 != null && range.Item2 != null)
            {
                found++;
                sinceFound = 0;
                yield return range;
            }
        }
    }

    private static (string, string) GetRange(
        IComponentMetaData network,
        IProfileMetaData profile)
    {
        if (network.Equals(profile.Component) &&
            IsRegisteredCountryValid(profile))
        {
            return GetRange(profile);
        }
        return default;
    }

    private static bool IsRegisteredCountryValid(IProfileMetaData profile)
    {
        var value = profile.GetValue("RegisteredCountry", "Unknown");
        if (value == null) return true;
        value.Dispose();
        return false;
    }

    private static (string, string) GetRange(IProfileMetaData profile)
    {
        return new(
            GetValue(profile, "IpRangeStart"), 
            GetValue(profile, "IpRangeEnd"));
    }

    private static string GetValue(IProfileMetaData profile, string name)
    {
        foreach(var value in profile.GetValues(name))
        {
            var result = value.Name;
            value.Dispose();
            return result;
        }
        return null;
    }

    /// <summary>
    /// Returns the IP addresses to include in the metrics for the inclusive
    /// range provided.
    ///
    /// The addresses are drawn at random rather than by walking the range so
    /// that the work is proportional to the number of addresses wanted. An
    /// IPv6 range holds far too many addresses to visit each one, so a range
    /// walk would never finish however small the sample percentage was.
    /// </summary>
    /// <param name="startIp">First address in the range.</param>
    /// <param name="endIp">Last address in the range.</param>
    /// <param name="samplePercentage">
    /// Proportion of the range to sample where 1 is 100%. At 1 or above every
    /// address in the range is returned.
    /// </param>
    /// <param name="maxSamplesPerRange">
    /// Most addresses to return for the range.
    /// </param>
    /// <param name="random"></param>
    /// <returns></returns>
    public static IEnumerable<IPAddress> Sample(
        IPAddress startIp,
        IPAddress endIp,
        double samplePercentage,
        int maxSamplesPerRange,
        Random random)
    {
        var ipv6 = startIp.AddressFamily != AddressFamily.InterNetwork;
        var start = ToUInt128(startIp);
        var end = ToUInt128(endIp);
        if (end < start)
        {
            yield break;
        }

        // The last offset from the start of the range. The range is inclusive
        // so it contains span + 1 addresses.
        var span = end - start;

        // Return every address when a complete analysis is wanted.
        if (samplePercentage >= 1)
        {
            for (UInt128 offset = 0; offset <= span; offset++)
            {
                yield return ToIpAddress(start + offset, ipv6);
            }
            yield break;
        }

        var count = GetSampleCount(span, samplePercentage, maxSamplesPerRange);
        for (var i = 0; i < count; i++)
        {
            yield return ToIpAddress(start + NextOffset(random, span), ipv6);
        }
    }

    /// <summary>
    /// The number of addresses to sample from a range containing
    /// <paramref name="span"/> + 1 addresses. Always at least one so that
    /// every range is represented in the metrics.
    /// </summary>
    private static int GetSampleCount(
        UInt128 span,
        double samplePercentage,
        int maxSamplesPerRange)
    {
        var count = ((double)span + 1) * samplePercentage;
        if (count >= maxSamplesPerRange)
        {
            return maxSamplesPerRange;
        }
        return Math.Max(1, (int)Math.Ceiling(count));
    }

    /// <summary>
    /// A random offset between 0 and <paramref name="span"/> inclusive.
    /// </summary>
    private static UInt128 NextOffset(Random random, UInt128 span)
    {
        if (span == 0)
        {
            return 0;
        }
        Span<byte> bytes = stackalloc byte[16];
        random.NextBytes(bytes);
        var value = new UInt128(
            BitConverter.ToUInt64(bytes[..8]),
            BitConverter.ToUInt64(bytes[8..]));
        return span == UInt128.MaxValue ? value : value % (span + 1);
    }

    private static UInt128 ToUInt128(IPAddress ip)
    {
        Span<byte> bytes = stackalloc byte[16];
        ip.TryWriteBytes(bytes, out var written);
        UInt128 value = 0;
        for (var i = 0; i < written; i++)
        {
            value = (value << 8) | bytes[i];
        }
        return value;
    }

    private static IPAddress ToIpAddress(UInt128 value, bool ipv6)
    {
        Span<byte> bytes = stackalloc byte[ipv6 ? 16 : 4];
        for (var i = bytes.Length - 1; i >= 0; i--)
        {
            bytes[i] = (byte)value;
            value >>= 8;
        }
        return new IPAddress(bytes);
    }

    /// <summary>
    /// Get the next address after this one.
    /// </summary>
    /// <param name="ip"></param>
    /// <returns></returns>
    public static IPAddress GetNextAddress(this IPAddress ip)
    {
        return ip.GetNextAddress(0);
    }

    /// <summary>
    /// Get the next address after this one.
    /// </summary>
    /// <param name="ip"></param>
    /// <param name="ignoreBytes">
    /// The number of bytes to ignore, starting with the least 
    /// significant.
    /// e.g. 
    /// With ip 0.0.0.0 and ignore bytes 0, the result would be 0.0.0.1
    /// With ip 0.0.0.0 and ignore bytes 1, the result would be 0.0.1.0
    /// </param>
    /// <returns>
    /// The provided IPAddress with 1 added to the least significant 
    /// possible byte
    /// </returns>
    public static IPAddress GetNextAddress(
        this IPAddress ip,
        int ignoreBytes)
    {
        IPAddress result = null;

        var bytes = ip.GetAddressBytes();
        var newBytes = new byte[bytes.Length];

        var added = false;

        // Work through the IP address bytes from least significant 
        // to most significant.
        for (var i = newBytes.Length - 1; i >= 0; i--)
        {
            // If we've already added to a byte or if this byte
            // is being ignored, just copy the existing byte value 
            // to the new array.
            if (added || newBytes.Length - i <= ignoreBytes)
            {
                newBytes[i] = bytes[i];
            }
            // Otherwise, either add one to the byte value or, if it's 
            // already 255, set it to 0.
            else
            {
                if (bytes[i] == byte.MaxValue)
                {
                    newBytes[i] = 0;
                }
                else
                {
                    newBytes[i] = (byte)(bytes[i] + 1);
                    added = true;
                }
            }
        }
        // As long as we've managed to add to one of the bytes, set the
        // result from the new byte array.
        // Otherwise, return null. 
        // (e.g. if the provided IP was 255.255.255.255)
        if (added != false)
        {
            result = new IPAddress(newBytes);
        }

        return result;
    }
}

public class Program
{
    /// <summary>
    /// Time interval to wait between logging progress.
    /// </summary>
    private static readonly TimeSpan _logBuild = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The percentage of registered IP addresses to randomly include in the
    /// metrics sample. 1 is 100%. Higher values will result in longer elapsed
    /// processing.
    /// </summary>
    private const double DEFAULT_SAMPLE_PERCENTAGE = 0.1;

    /// <summary>
    /// The most IP addresses that will be sampled from any single range.
    ///
    /// A single IPv6 range in the data file can contain more addresses than
    /// could be enumerated in any practical amount of time (a /48 holds over
    /// 10^24), so the sample percentage on its own is not enough to bound the
    /// work. Capping the sample per range is what keeps the elapsed time
    /// proportional to the number of ranges rather than the size of the
    /// address space.
    /// </summary>
    private const int DEFAULT_MAX_SAMPLES_PER_RANGE = 8;

    /// <summary>
    /// The percentage of the ranges in the data file to include, where 1 is
    /// 100%.
    ///
    /// This is the most effective control over how long the example takes.
    /// Reading the property values that make up a range costs around a
    /// hundred times more than stepping over the profile that holds it, so
    /// discarding a range before those values are read takes almost all of
    /// its cost with it.
    /// </summary>
    private const double DEFAULT_RANGE_PERCENTAGE = 1;

    /// <summary>
    /// The performance profile the engine is built with.
    ///
    /// This example walks the profile metadata of the whole data file, which
    /// is a very different workload to detection. Reading the property values
    /// of a profile measured 166 a second under LowMemory against 2,013 a
    /// second under MaxPerformance on the same file - paging the data back
    /// from disk dominates everything else the example does. MaxPerformance
    /// holds the data file in memory, so it needs as much free RAM as the
    /// file is large; drop to Balanced or LowMemory where that is not
    /// available.
    /// </summary>
    private const PerformanceProfiles DEFAULT_PERFORMANCE_PROFILE =
        PerformanceProfiles.MaxPerformance;

    /// <summary>
    /// Give up scanning once this many profiles in a row have produced no
    /// range. Zero scans every profile in the data file.
    ///
    /// See <see cref="Extensions.ValidRanges"/> for the measurements behind
    /// this. The margin is wide: on the file it was measured against, the
    /// longest run of profiles without a range before the last range was
    /// 2,088.
    /// </summary>
    private const int DEFAULT_MAX_PROFILES_WITHOUT_RANGE = 100_000;

    public class Counter
    {
        public int Value = 1;
    }

    public class Metric : Key
    {
        /// <summary>
        /// Number of IP addresses that relate to this metric.
        /// </summary>
        [Index(9)]
        public int IpCount { get; set; } = 0;

        /// <summary>
        /// The total area in km squared of all IPs.
        /// </summary>
        [Ignore]
        public long TotalAreaKm { get; set; } = 0;

        /// <summary>
        /// Number of areas included.
        /// </summary>
        [Index(10)]
        public int AreaCount { get; set; } = 0;

        /// <summary>
        /// Average area in km squared for the IPs, or 0 if there is no area
        /// available.
        /// </summary>
        [Index(11)]
        public long AverageAreaKm =>
            AreaCount > 0 ? TotalAreaKm / AreaCount : 0;

        /// <summary>
        /// The radius that a circle would need to so that it covered the same
        /// area as the <see cref="AverageAreaKm"/>.
        /// </summary>
        [Index(12)]
        public int EquivalentRadiusKm =>
            (int)Math.Sqrt(AverageAreaKm / Math.PI);

        /// <summary>
        /// Average number of polygons for the metric.
        /// </summary>
        [Index(14)]
        public double AveragePolygons
        {
            get
            {
                if (_polygons.Count == 0)
                {
                    return 0;
                }
                var weightedSum = _polygons.Sum(i => i.Key * i.Value.Value);
                var total = _polygons.Sum(i => i.Value.Value);
                return (double)weightedSum / total;
            }
        }

        /// <summary>
        /// Key is the number of areas, and the value the number of IPs that
        /// contain the areas.
        /// </summary>
        private readonly Dictionary<int, Counter> _polygons = [];

        public Metric(Key key) : base(
            key.ContinentName,
            key.Country,
            key.LocationConfidence,
            key.ConnectionType,
            key.IsVPN,
            key.IsProxy,
            key.IsTor,
            key.IsPublicRouter)
        { }

        public override string ToString() =>
            $"{base.ToString()}{IpCount},{AverageAreaKm}," +
            $"{EquivalentRadiusKm},{AveragePolygons.ToString("0.##")}";

        /// <summary>
        /// Increase the count of IP addresses with the number of polygons
        /// provided.
        /// </summary>
        /// <param name="polygons"></param>
        public void IncrementPolygons(int polygons)
        {
            if (_polygons.TryGetValue(polygons, out var counter))
            {
                counter.Value++;
            }
            else
            {
                _polygons.Add(polygons, new Counter());
            }
        }

        /// <summary>
        /// Merge the other metric instance with this one.
        /// </summary>
        /// <param name="other"></param>
        public void Merge(Metric other)
        {
            IpCount += other.IpCount;
            AreaCount += other.AreaCount;
            TotalAreaKm += other.TotalAreaKm;
            foreach (var pair in other._polygons)
            {
                if (_polygons.TryGetValue(pair.Key, out var counter))
                {
                    counter.Value += pair.Value.Value;
                }
                else
                {
                    _polygons.Add(pair.Key, pair.Value);
                }
            }
        }
    }

    /// <summary>
    /// Key used for each metric.
    /// </summary>
    public class Key : IEquatable<Key>, IComparable<Key>
    {
        public Key(
            string continentName,
            string country, 
            string locationConfidence,
            string connectionType,
            string isVPN,
            string isProxy,
            string isTor,
            string isPublicRouter)
        {
            ContinentName = continentName;
            Country = country;
            LocationConfidence = locationConfidence;
            ConnectionType = connectionType;
            IsVPN = isVPN;
            IsProxy = isProxy;
            IsTor = isTor;
            IsPublicRouter = isPublicRouter;
            _hashCode = HashCode.Combine(
                continentName,
                country,
                locationConfidence,
                connectionType,
                isVPN,
                isProxy,
                isTor,
                isPublicRouter);
        }

        [Index(1)]
        public string ContinentName { get; set; }

        [Index(2)]
        public string Country { get; set; }

        [Index(3)]
        public string LocationConfidence { get; set; }

        [Index(4)]
        public string ConnectionType { get; set; }

        [Index(5)]
        public string IsVPN { get; set; }

        [Index(6)]
        public string IsProxy { get; set; }

        [Index(7)]
        public string IsTor { get; set; }

        [Index(8)]
        public string IsPublicRouter { get; set; }

        private readonly int _hashCode;

        public bool Equals(Key other)
        {
            return ContinentName.Equals(other.ContinentName) &&
                Country.Equals(other.Country) &&
                LocationConfidence.Equals(other.LocationConfidence) &&
                ConnectionType.Equals(other.ConnectionType) &&
                IsVPN.Equals(other.IsVPN) &&
                IsProxy.Equals(other.IsProxy) &&
                IsTor.Equals(other.IsTor) &&
                IsPublicRouter.Equals(other.IsPublicRouter);
        }

        public int CompareTo(Key other)
        {
            var difference = ContinentName.CompareTo(other.ContinentName);
            if (difference != 0) return difference;
            difference = Country.CompareTo(other.Country);
            if (difference != 0) return difference;
            difference = LocationConfidence.CompareTo(other.LocationConfidence);
            if (difference != 0) return difference;
            difference = ConnectionType.CompareTo(other.ConnectionType);
            if (difference != 0) return difference;
            difference = IsVPN.CompareTo(other.IsVPN);
            if (difference != 0) return difference;
            difference = IsProxy.CompareTo(other.IsProxy);
            if (difference != 0) return difference;
            difference = IsTor.CompareTo(other.IsTor);
            if (difference != 0) return difference;
            difference = IsPublicRouter.CompareTo(other.IsPublicRouter);
            if (difference != 0) return difference;
            return 0;
        }

        public override int GetHashCode() => _hashCode;
    }

    /// <summary>
    /// Factory used to create <see cref="Key"/>.
    /// </summary>
    public class KeyFactory
    {
        /// <summary>
        /// Returns the key for the data instance provided.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public Key Create(IIpIntelligenceData data)
        {
            return new Key(
                GetValue(data.ContinentName),
                GetValue(data.Country),
                GetValue(data.LocationConfidence),
                GetValue(data.ConnectionType),
                GetValue(data.IsVPN),
                GetValue(data.IsProxy),
                GetValue(data.IsTor),
                GetValue(data.IsPublicRouter));
        }

        /// <summary>
        /// Returns the string value or the no value message.
        /// <param name="value"></param>
        /// <returns></returns>
        private static string GetValue(IAspectPropertyValue<string> value)
        {
            if (value.HasValue)
                return value.Value;
            return value.NoValueMessage;
        }

        /// <summary>
        /// Returns the string value or the no value message.
        /// <param name="value"></param>
        /// <returns></returns>
        private static string GetValue(IAspectPropertyValue<bool> value)
        {
            if (value.HasValue)
                return value.Value.ToString();
            return value.NoValueMessage;
        }
    }

    public class Consumer
    {
        /// <summary>
        /// The task associated with the consumer.
        /// </summary>
        public Task<IReadOnlyDictionary<Key, Metric>> Task;

        /// <summary>
        /// The number of IP pipeline processes that have been completed in
        /// a given period of time. Used for logging during processing.
        /// </summary>
        public int Count;
    }

    public class AreaIndex
    {
        /// <summary>
        /// Properties used by the Area index.
        /// </summary>
        public static readonly string[] Properties = ["Areas"];

        /// <summary>
        /// Dictionary that takes WKT value and returns the geographic area
        /// in square kms and the number polygons that form the area.
        /// </summary>
        public IReadOnlyDictionary<string, Result> WktAreas;

        public AreaIndex(
            IFiftyOneAspectPropertyMetaData property,
            ILogger logger,
            CancellationToken stoppingToken)
        {
            WktAreas = BuildWktAreas(property, logger, stoppingToken);
        }

        private static IReadOnlyDictionary<string, Result> BuildWktAreas(
            IFiftyOneAspectPropertyMetaData property,
            ILogger logger,
            CancellationToken stoppingToken)
        {
            var wktAreas = new Dictionary<string, Result>();
            var nextLog = DateTime.UtcNow.Add(_logBuild);
            Parallel.ForEach(
                property.GetValues(),
                new ParallelOptions() { CancellationToken = stoppingToken },
                () => new Dictionary<string, Result>(),
                (wkt, state, local) =>
                {
                    local.Add(wkt.Name, Calculations.GetAreas(wkt.Name, 0, 0));
                    wkt.Dispose();
                    return local;
                },
                local =>
                {
                    lock (wktAreas)
                    {
                        foreach (var pair in local)
                        {
                            wktAreas.Add(pair.Key, pair.Value);
                        }
                        if (DateTime.UtcNow >= nextLog)
                        {
                            logger.LogInformation(
                                "Mapped '{0}' areas",
                                wktAreas.Count);
                            nextLog = DateTime.UtcNow.Add(_logBuild);
                        }
                    }
                });
            return wktAreas;
        }
    }

    public class Configuration
    {
        /// <summary>
        /// IPI data file.
        /// </summary>
        public string DataFile;

        /// <summary>
        /// Stream to write the output to.
        /// </summary>
        public StreamWriter Output;

        /// <summary>
        /// Sample of IP addresses to sample for the metrics.
        /// </summary>
        public double SamplePercentage;

        /// <summary>
        /// Maximum number of IP addresses sampled from a single range.
        /// </summary>
        public int MaxSamplesPerRange;

        /// <summary>
        /// Percentage of the ranges in the data file to include.
        /// </summary>
        public double RangePercentage;

        /// <summary>
        /// Give up scanning after this many profiles in a row with no range.
        /// </summary>
        public int MaxProfilesWithoutRange;

        /// <summary>
        /// Performance profile the engine is built with.
        /// </summary>
        public PerformanceProfiles PerformanceProfile;

        /// <summary>
        /// Logger factory for reporting progress.
        /// </summary>
        public ILoggerFactory LoggerFactory;

        /// <summary>
        /// Function used to determine if an IP address range should be
        /// included.
        /// </summary>
        public Func<(string, string), bool> Condition;
    }

    public sealed class Worker(
        IHostApplicationLifetime hostApplicationLifetime,
        Configuration configuration)
        : BackgroundService
    {
        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            try
            {
                await Example.Run(
                    configuration.DataFile,
                    configuration.LoggerFactory,
                    configuration.Output,
                    configuration.SamplePercentage,
                    configuration.MaxSamplesPerRange,
                    configuration.RangePercentage,
                    configuration.MaxProfilesWithoutRange,
                    configuration.PerformanceProfile,
                    configuration.Condition,
                    stoppingToken);
            }
            catch (Exception ex)
            {
                // The metrics have already been written by this point. Report
                // the failure rather than leaving it unhandled, which would
                // end the process without the summary below.
                configuration.LoggerFactory
                    .CreateLogger<Worker>()
                    .LogError(ex, "The metrics run did not complete");
            }
            finally
            {
                hostApplicationLifetime.StopApplication();
            }
        }
    }

    public class Example : ExampleBase
    {
        /// <summary>
        /// Runs the example as a task.
        /// </summary>
        /// <param name="dataFile">
        /// IP Intelligence data file to compile metrics for.
        /// </param>
        /// <param name="loggerFactory"></param>
        /// <param name="output">
        /// Output CSV writer for the metrics.
        /// </param>
        /// <param name="samplePercentage">
        /// Percentage of possible IP addresses to include in the metric.
        /// </param>
        /// <param name="maxSamplesPerRange">
        /// Most IP addresses to sample from any single range. Needed to bound
        /// the work for the very large IPv6 ranges.
        /// </param>
        /// <param name="rangePercentage">
        /// Percentage of the ranges in the data file to include. The cheapest
        /// way to shorten a run - see <see cref="DEFAULT_RANGE_PERCENTAGE"/>.
        /// </param>
        /// <param name="maxProfilesWithoutRange">
        /// Give up scanning once this many profiles in a row have produced no
        /// range. Zero scans every profile - see
        /// <see cref="DEFAULT_MAX_PROFILES_WITHOUT_RANGE"/>.
        /// </param>
        /// <param name="performanceProfile">
        /// Profile the engine is built with. Dominates how long the scan of
        /// the data file takes - see
        /// <see cref="DEFAULT_PERFORMANCE_PROFILE"/>.
        /// </param>
        /// <param name="condition">
        /// Function used to determine if an IP address range should be
        /// included.
        /// </param>
        /// <param name="stoppingToken">
        /// Cancellation token that when fired will gracefully stop the example
        /// and write the output metrics from the data processed to date.
        /// </param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static async Task Run(
            string dataFile,
            ILoggerFactory loggerFactory,
            TextWriter output,
            double samplePercentage,
            int maxSamplesPerRange,
            double rangePercentage,
            int maxProfilesWithoutRange,
            PerformanceProfiles performanceProfile,
            Func<(string, string), bool> condition,
            CancellationToken stoppingToken,
            ILogger logger = null)
        {
            logger ??= loggerFactory.CreateLogger<Example>();

            // Ensure that batch latency mode is always enabled.
            GCSettings.LatencyMode = GCLatencyMode.Batch;

            // Build a new on-premise IP Intelligence engine. See the
            // documentation for more detail on this and other configuration options.
            // https://51degrees.com/documentation/_features__automatic_datafile_updates.html?utm_source=code&utm_medium=example&utm_campaign=ip-intelligence-dotnet-examples&utm_content=examples-onpremise-metrics-console-program.cs&utm_term=run
            using var ipiEngine = new IpiOnPremiseEngineBuilder(loggerFactory)
                .SetPerformanceProfile(performanceProfile)
                // inhibit auto-update of the data file for this test
                .SetAutoUpdate(false)
                .SetDataFileSystemWatcher(false)
                .SetDataUpdateOnStartup(false)
                // Set to only return from processing the properties needed.
                .SetProperties(
                    typeof(Key).GetProperties().Select(i => i.Name).Concat(
                        AreaIndex.Properties).ToList())
                // Optimize for the expected parallel workload. One handle per
                // consumer, plus one for the producer, which reads profile
                // metadata from the same pool while the consumers are
                // querying. Sizing the pool to the consumers alone exhausts it
                // and fails the run with "Insufficient handles available".
                .SetConcurrency((ushort)(Environment.ProcessorCount + 1))
                .Build(dataFile, false);
            using var pipeline = new PipelineBuilder(loggerFactory)
                .AddFlowElement(ipiEngine)
                .SetAutoDisposeElements(false)
                .Build();

            // Log data file information
            var dataFilePublishDate = ipiEngine.DataFiles[0].DataPublishedDateTime;
            var dataFileTier = ipiEngine.DataSourceTier;
            logger.LogInformation(
                "Using data file: {0} (tier: {1}, published: {2:yyyy-MM-dd})",
                dataFile,
                dataFileTier,
                dataFilePublishDate);

            logger.LogInformation(
                "Server GC: '{0}', performance profile: '{1}'",
                GCSettings.IsServerGC,
                performanceProfile);

            // Create the data set structure to rapidly retrieve the data
            // needed to calculate the metrics and to determine the valid
            // IP ranges.
            logger.LogInformation("Creating area index");
            var areaIndex = new AreaIndex(
                ipiEngine.Properties.Single(i => "Areas".Equals(
                    i.Name,
                    StringComparison.InvariantCultureIgnoreCase)),
                logger,
                stoppingToken);
            logger.LogInformation(
                "Created index for '{0}' areas",
                areaIndex.WktAreas.Count);

            // Perform IP lookup for all the IP addresses in the ranges
            // and record the results.
            var factory = new KeyFactory();

            // Create a collection of ranges to ensure that there are always
            // items available for the consumers.
            var ranges =
                Channel.CreateBounded<(string, string)>(
                new BoundedChannelOptions(Environment.ProcessorCount) 
                {  
                    SingleWriter = true
                });
            var consumers = CreateConsumers(
                pipeline,
                areaIndex,
                factory,
                ranges,
                samplePercentage,
                maxSamplesPerRange,
                stoppingToken);
            logger.LogInformation(
                "Created '{0}' consumer processors sampling '{1:P2}' of IPs " +
                "up to '{2}' per range",
                consumers.Length,
                samplePercentage,
                maxSamplesPerRange);

            // Use the main thread as the producer adding ranges for the
            // consumers to process.
            var scanFailure = await AddRanges(
                ipiEngine,
                logger, 
                ranges,
                rangePercentage,
                maxProfilesWithoutRange,
                condition,
                consumers,
                stoppingToken);

            // Combine all the consumer groups into the main groups.
            var groups = new Dictionary<Key, Metric>();
            foreach (var consumer in consumers)
            {
                await consumer.Task;
                logger.LogInformation(
                    "Finished consumer '{0}'",
                    consumer.Task.Id);
                foreach (var group in consumer.Task.Result)
                {
                    if (groups.TryGetValue(group.Key, out var metric))
                    {
                        metric.Merge(group.Value);
                    }
                    else
                    {
                        groups.Add(group.Key, group.Value);
                    }
                }
            }

            WriteToCsv(output, groups.Values);

            ExampleUtils.CheckDataFile(
                ipiEngine,
                loggerFactory.CreateLogger<Program>());

            // Report a scan that stopped early only now that the metrics it
            // did gather have been written out. The caller needs to know the
            // analysis does not cover the whole data file.
            if (scanFailure != null)
            {
                ExceptionDispatchInfo.Capture(scanFailure).Throw();
            }
        }

        /// <summary>
        /// Write the metrics to the provided output in CSV format.
        /// </summary>
        /// <param name="output"></param>
        /// <param name="metrics"></param>
        private static void WriteToCsv(
            TextWriter output,
            IEnumerable<Metric> metrics)
        {
            using var writer = new CsvWriter(
                output,
                new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    Delimiter = ",",
                });

            // Write Records
            writer.WriteRecords(metrics.OrderBy(i => i));

            writer.Flush();
        }

        /// <summary>
        /// Create and start the consumers which will be waiting on the
        /// producer to start.
        /// </summary>
        /// <param name="samplePercentage"></param>
        /// <param name="pipeline"></param>
        /// <param name="areaIndex"></param>
        /// <param name="factory"></param>
        /// <param name="ranges"></param>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        private static Consumer[]
            CreateConsumers(
                IPipeline pipeline,
                AreaIndex areaIndex,
                KeyFactory factory,
                Channel<(string, string)> ranges,
                double samplePercentage,
                int maxSamplesPerRange,
                CancellationToken stoppingToken)
        {
            return [.. Enumerable.Range(
                0,
                Environment.ProcessorCount).Select(_ =>
                {
                    var consumer = new Consumer();
                    consumer.Task = ProcessRange(
                        pipeline,
                        areaIndex,
                        factory,
                        ranges,
                        consumer,
                        samplePercentage,
                        maxSamplesPerRange,
                        stoppingToken);
                    return consumer;
                })];
        }

        /// <returns>
        /// The failure that stopped the scan early, or null if it ran to the
        /// end. Returned rather than thrown so that the metrics gathered
        /// before the failure can still be written out.
        /// </returns>
        private async static Task<Exception> AddRanges(
            IpiOnPremiseEngine ipiEngine,
            ILogger logger,
            Channel<(string, string)> ranges,
            double rangePercentage,
            int maxProfilesWithoutRange,
            Func<(string, string), bool> condition,
            Consumer[] consumers,
            CancellationToken stoppingToken)
        {
            var process = Process.GetCurrentProcess();
            var added = 0;
            var lastLog = DateTime.UtcNow;
            var nextLog = lastLog.Add(_logBuild);
            var lastProcessorTime = process.TotalProcessorTime;
            var source = condition == null ?
                ipiEngine.ValidRanges(
                    rangePercentage, maxProfilesWithoutRange, logger) :
                ipiEngine.ValidRanges(
                    rangePercentage, maxProfilesWithoutRange, logger)
                    .Where(i => condition(i));
            try
            {
                foreach (var range in source.TakeWhile(_ => 
                    stoppingToken.IsCancellationRequested == false))
                {
                    try
                    {
                        await ranges.Writer.WaitToWriteAsync(stoppingToken);
                        await ranges.Writer.WriteAsync(range, stoppingToken);
                        added++;
                        if (DateTime.UtcNow >= nextLog)
                        {
                            nextLog = Log(
                                logger, 
                                ranges, 
                                consumers, 
                                process, 
                                added, 
                                ref lastLog,
                                ref lastProcessorTime, 
                                range);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Do nothing and exit as if the ranges had been fully
                        // consumed.
                    }
                }
            }
            catch (Exception ex)
            {
                // The engine can fail part way through enumerating profiles.
                // Report it and finish with the ranges found so far - the
                // metrics gathered up to this point are still worth writing,
                // and this used to take the whole process down with it.
                logger.LogError(
                    ex,
                    "Stopped looking for ranges after '{0}'",
                    added);
                return ex;
            }
            finally
            {
                ranges.Writer.Complete();
            }
            logger.LogInformation("Finished adding '{0}' ranges", added);
            return null;
        }

        private static DateTime Log(
            ILogger logger,
            Channel<(string, string)> ranges,
            Consumer[] consumers,
            Process process,
            int added,
            ref DateTime lastLog,
            ref TimeSpan lastProcessorTime,
            (string, string) range)
        {
            // Log the ranges and other telemetry.
            logger.LogInformation(
                "Processed '{0}' ranges with '{1}' in " +
                "queue, most recent '{2}', and '{3}' " +
                "consumers",
                added,
                ranges.Reader.Count,
                range.Item1,
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
                process.WorkingSet64 / 1_000_000);

            // Reset the count of queries to IPI.
            foreach (var consumer in consumers)
            {
                consumer.Count = 0;
            }

            // Reset the other logging parameters.
            lastLog = DateTime.UtcNow;
            lastProcessorTime = process.TotalProcessorTime;
            return DateTime.UtcNow.Add(_logBuild);
        }

        private async static Task<IReadOnlyDictionary<Key, Metric>> 
            ProcessRange(
                IPipeline pipeline,
                AreaIndex dataSet,
                KeyFactory factory,
                Channel<(string, string)> ranges,
                Consumer consumer,
                double samplePercentage,
                int maxSamplesPerRange,
                CancellationToken stoppingToken)
        {
            var random = new Random();
            var groups = new Dictionary<Key, Metric>();
            try
            {
                await foreach (var range in ranges.Reader.ReadAllAsync(
                    stoppingToken))
                {
                    // Get the next range.
                    var startIp = IPAddress.Parse(range.Item1);
                    var endIp = IPAddress.Parse(range.Item2);

                    foreach (var ip in Extensions.Sample(
                        startIp,
                        endIp,
                        samplePercentage,
                        maxSamplesPerRange,
                        random))
                    {
                        if (stoppingToken.IsCancellationRequested)
                        {
                            break;
                        }
                        ProcessIp(
                            pipeline,
                            dataSet,
                            groups,
                            factory,
                            ip);
                        consumer.Count++;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Do nothing.
            }
            return groups;
        }

        private static void ProcessIp(
            IPipeline pipeline,
            AreaIndex dataSet,
            Dictionary<Key, Metric> groups,
            KeyFactory factory,
            IPAddress ipAddress)
        {
            // Get the data for the IP address.
            using var flowData = pipeline.CreateFlowData();
            flowData.AddEvidence("query.client-ip", ipAddress.ToString());
            flowData.Process();
            var data = flowData.Get<IIpIntelligenceData>();

            // Get the metric instance for the group key.
            var key = factory.Create(data);
            if (groups.TryGetValue(key, out var metric) == false)
            {
                metric = new Metric(key);
                groups.Add(key, metric);
            }

            // Increase the number of IP addresses that relate to this key.
            metric.IpCount++;

            // Increase the total area and number of areas for the metric only
            // where a non zero area is available.
            if (data.Areas.HasValue && 
                dataSet.WktAreas.TryGetValue(
                    data.Areas.Value.Value,
                    out var value) && 
                value != null)
            {
                metric.TotalAreaKm += value.SquareKms;
                metric.IncrementPolygons(value.Geometries);
                metric.AreaCount++;
            }
        }
    }

    static void Main(string[] args)
    {
        var configuration = new Configuration
        {
            // Use the supplied path for the data file or find the lite file that is included
            // in the repository.
            DataFile = args.Length > 0 ? args[0] :
            // In this example, by default, the 51degrees IP Intelligence data file needs to be somewhere in the
            // project space, or you may specify another file as a command line parameter.
            //
            // For testing, contact us to obtain an enterprise data file: https://51degrees.com/contact-us?utm_source=code&utm_medium=example&utm_campaign=ip-intelligence-dotnet-examples&utm_content=examples-onpremise-metrics-console-program.cs&utm_term=main
            Examples.ExampleUtils.FindDataFile(
                Constants.ENTERPRISE_IPI_DATA_FILE_NAME)
        };

        // Get the location for the output file. Use the same location as the
        // evidence if a path is not supplied on the command line.
        var outputFile = args.Length > 1 ? args[1] : "metrics-output.csv";

        // Get the sample percentage or use the default.
        configuration.SamplePercentage = args.Length > 2 ?
            double.Parse(args[2]) :
            DEFAULT_SAMPLE_PERCENTAGE;

        // Get the cap on the addresses sampled from a single range, or use
        // the default.
        configuration.MaxSamplesPerRange = args.Length > 3 ?
            int.Parse(args[3]) :
            DEFAULT_MAX_SAMPLES_PER_RANGE;

        // Get the percentage of ranges to include, or use the default.
        configuration.RangePercentage = args.Length > 4 ?
            double.Parse(args[4]) :
            DEFAULT_RANGE_PERCENTAGE;

        // Get the point at which to give up scanning, or use the default.
        configuration.MaxProfilesWithoutRange = args.Length > 6 ?
            int.Parse(args[6]) :
            DEFAULT_MAX_PROFILES_WITHOUT_RANGE;

        // Get the performance profile, or use the default.
        configuration.PerformanceProfile = args.Length > 5 ?
            Enum.Parse<PerformanceProfiles>(args[5], true) :
            DEFAULT_PERFORMANCE_PROFILE;

        // Only include IP addresses with periods in them. i.e. IPv4. There are
        // too many IPv6 addresses for the metrics example to complete in a 
        // short time frame.
        // configuration.Condition = (i) => i.Item1.Contains(".");

        File.WriteAllText("Metrics_DataFileName.txt", configuration.DataFile);

        // Configure a logger to output to the console with timestamps.
        configuration.LoggerFactory = LoggerFactory.Create(b =>
            b.AddSimpleConsole(options =>
            {
                options.IncludeScopes = false;
                options.SingleLine = true;
                options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
            }));
        var logger = configuration.LoggerFactory.CreateLogger<Program>();

        if (configuration.DataFile != null)
        {
            logger.LogInformation($"Found IPI data file: {configuration.DataFile}");
            using var output = File.CreateText(outputFile);
            configuration.Output = output;
            var builder = Host.CreateApplicationBuilder([]);
            builder.Services.AddSingleton(configuration);
            builder.Services.AddHostedService<Worker>();
            // Stopping the example is meant to write out the metrics gathered
            // so far. The default shutdown timeout is far too short for that,
            // and when it expires the host disposes the engine while the
            // worker is still using it, which loses the output and takes the
            // process down with it.
            builder.Services.Configure<HostOptions>(o =>
                o.ShutdownTimeout = TimeSpan.FromMinutes(5));
            using var host = builder.Build();
            host.Run();
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
        configuration.LoggerFactory.Dispose();
    }
}