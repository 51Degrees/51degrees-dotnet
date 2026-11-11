// Temporary timing harness, never committed to a pull request. Writes COST
// lines to the file named by COST_OUT.
using FiftyOne.IpIntelligence.Engine.OnPremise.Data;
using FiftyOne.IpIntelligence.Engine.OnPremise.FlowElements;
using FiftyOne.IpIntelligence.Engine.OnPremise.Interop;
using FiftyOne.IpIntelligence.Shared.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace FiftyOne.IpIntelligence.OnPremise.Tests.Cost
{
    [TestClass]
    public class IndexCostTimingIpi
    {
        private const int EvidenceCount = 5000;
        private const int Rounds = 5;
        private const int Builds = 5;
        private const int Repeats = 20;

        private static readonly string[] Preferred =
        {
            "RegisteredCountry",
            "RegisteredName",
            "CountryCode",
            "Town",
            "RegisteredOwner",
        };

        private static readonly PropertyInfo ResultsProperty =
            typeof(IpDataBaseOnPremise<ResultsIpiSwig>).GetProperty(
                "Results", BindingFlags.Instance | BindingFlags.NonPublic);

        private static long _sink;

        private enum Mode
        {
            Process,
            Typed,
            ByName,
            RawContains,
            RawNameString,
            RawIndexString,
            RawNameWeighted,
            RawIndexWeighted,
        }

        [TestMethod]
        public void Measure()
        {
            var dir = Environment.GetEnvironmentVariable("COST_IPI_DIR");
            var output = Environment.GetEnvironmentVariable("COST_OUT");
            var label = Environment.GetEnvironmentVariable("COST_LABEL") ?? "";
            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(output))
            {
                Assert.Inconclusive("COST_IPI_DIR and COST_OUT are not set.");
                return;
            }
            var random = new Random(42);
            var ips = new string[EvidenceCount];
            for (var i = 0; i < ips.Length; i++)
            {
                ips[i] = string.Join(".",
                    random.Next(1, 224),
                    random.Next(0, 256),
                    random.Next(0, 256),
                    random.Next(1, 255));
            }
            var lines = new StringBuilder();
            foreach (var file in new[] {
                "51Degrees-LiteV41.ipi", "51Degrees-EnterpriseIpiV41.ipi" })
            {
                var path = Path.Combine(dir, file);
                if (File.Exists(path) == false)
                {
                    lines.AppendLine($"COST|{label}|{file}|missing");
                    continue;
                }
                string[] names;
                using (var probe = Build(
                    path, PerformanceProfiles.LowMemory, null))
                {
                    var available = probe.Properties
                        .Select(p => p.Name)
                        .ToList();
                    names = Preferred
                        .Where(n => available.Contains(
                            n, StringComparer.OrdinalIgnoreCase))
                        .Take(3)
                        .ToArray();
                    if (names.Length == 0)
                    {
                        lines.AppendLine(
                            $"COST|{label}|{file}|no preferred name|" +
                            string.Join(",", available.Take(30)));
                        continue;
                    }
                }
                lines.AppendLine(
                    $"COST|{label}|{file}|names|{string.Join(",", names)}");
                var profiles = file.Contains("Lite") ?
                    new[] {
                        PerformanceProfiles.MaxPerformance,
                        PerformanceProfiles.LowMemory } :
                    new[] { PerformanceProfiles.LowMemory };
                foreach (var profile in profiles)
                {
                    foreach (var named in new[] { false, true })
                    {
                        Run(lines, label, file, path, profile,
                            named ? names : null, names[0], ips);
                        File.AppendAllText(output, lines.ToString());
                        lines.Clear();
                    }
                }
            }
            File.AppendAllText(output, lines.ToString());
            File.AppendAllText(output, $"COST|{label}|sink|{_sink}\n");
        }

        private static void Run(
            StringBuilder lines,
            string label,
            string file,
            string path,
            PerformanceProfiles profile,
            string[] names,
            string read,
            string[] ips)
        {
            var tag = string.Join("|", label, file, profile,
                names == null ? "none" : "named");
            var builds = new List<double>();
            for (var i = 0; i < Builds; i++)
            {
                Settle();
                var watch = Stopwatch.StartNew();
                var built = Build(path, profile, names);
                watch.Stop();
                builds.Add(watch.Elapsed.TotalMilliseconds);
                built.Dispose();
            }
            Report(lines, tag, "build ms", builds);

            var modes = Enum.GetValues(typeof(Mode)).Cast<Mode>()
                .Where(m => names != null ||
                    (m != Mode.RawIndexString && m != Mode.RawIndexWeighted))
                .ToArray();
            var engine = Build(path, profile, names);
            var pipeline = new PipelineBuilder(NullLoggerFactory.Instance)
                .AddFlowElement(engine)
                .Build();
            try
            {
                foreach (var mode in modes)
                {
                    Round(pipeline, ips, mode, read);
                }
                var figures = modes.ToDictionary(m => m, m => new List<double>());
                for (var round = 0; round < Rounds; round++)
                {
                    foreach (var mode in modes)
                    {
                        Settle();
                        figures[mode].Add(Round(pipeline, ips, mode, read));
                    }
                }
                foreach (var mode in modes)
                {
                    Report(lines, tag, mode + " ns", figures[mode]);
                }
            }
            finally
            {
                pipeline.Dispose();
                engine.Dispose();
            }
        }

        private static IpiOnPremiseEngine Build(
            string path,
            PerformanceProfiles profile,
            string[] names)
        {
            var builder = new IpiOnPremiseEngineBuilder(
                    NullLoggerFactory.Instance, null)
                .SetPerformanceProfile(profile)
                .SetAutoUpdate(false)
                .SetDataFileSystemWatcher(false);
            if (names != null)
            {
                builder.SetProperties(new List<string>(names));
            }
            return builder.Build(path, false);
        }

        private static void Settle()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private static double Round(
            IPipeline pipeline,
            string[] ips,
            Mode mode,
            string read)
        {
            long rawTicks = 0;
            var start = Stopwatch.GetTimestamp();
            foreach (var ip in ips)
            {
                using (var flowData = pipeline.CreateFlowData())
                {
                    flowData.AddEvidence("query.client-ip", ip);
                    flowData.Process();
                    var data = flowData.Get<IIpIntelligenceData>();
                    switch (mode)
                    {
                        case Mode.Process:
                            break;
                        case Mode.Typed:
                            Sink(Typed(data, read));
                            break;
                        case Mode.ByName:
                            Sink(data[read] as IAspectPropertyValue<string>);
                            break;
                        default:
                            rawTicks += Raw(data, mode, read);
                            break;
                    }
                }
            }
            var total = Stopwatch.GetTimestamp() - start;
            var raw = mode >= Mode.RawContains;
            var ticks = raw ? rawTicks : total;
            var count = raw ?
                (double)ips.Length * Repeats :
                ips.Length;
            return ticks * 1e9 / Stopwatch.Frequency / count;
        }

        private static IAspectPropertyValue<string> Typed(
            IIpIntelligenceData data,
            string name)
        {
            switch (name)
            {
                case "RegisteredCountry":
                    return data.RegisteredCountry;
                case "RegisteredName":
                    return data.RegisteredName;
                case "CountryCode":
                    return data.CountryCode;
                case "Town":
                    return data.Town;
                default:
                    return data.RegisteredOwner;
            }
        }

        private static void Sink(IAspectPropertyValue<string> value)
        {
            if (value != null && value.HasValue)
            {
                _sink += value.Value.Length;
            }
        }

        private static long Raw(IIpIntelligenceData data, Mode mode, string read)
        {
            var manager = ResultsProperty.GetValue(data);
            var list = (IReadOnlyList<ResultsIpiSwig>)manager.GetType()
                .GetProperty("ResultsList").GetValue(manager);
            var native = list[0];
            var start = Stopwatch.GetTimestamp();
            switch (mode)
            {
                case Mode.RawContains:
                    for (var i = 0; i < Repeats; i++)
                    {
                        if (native.containsProperty(read))
                        {
                            _sink++;
                        }
                    }
                    break;
                case Mode.RawNameString:
                    for (var i = 0; i < Repeats; i++)
                    {
                        using (var v = native.getValueAsUTF8String(read))
                        {
                            if (v.hasValue())
                            {
                                using (var u = v.getValue())
                                {
                                    _sink += u.ToUTF8String().Length;
                                }
                            }
                        }
                    }
                    break;
                case Mode.RawIndexString:
                    for (var i = 0; i < Repeats; i++)
                    {
                        using (var v = native.getValueAsUTF8String(0))
                        {
                            if (v.hasValue())
                            {
                                using (var u = v.getValue())
                                {
                                    _sink += u.ToUTF8String().Length;
                                }
                            }
                        }
                    }
                    break;
                case Mode.RawNameWeighted:
                    for (var i = 0; i < Repeats; i++)
                    {
                        using (var v = native.getValuesAsWeightedUTF8StringList(read))
                        {
                            if (v.hasValue())
                            {
                                _sink++;
                            }
                        }
                    }
                    break;
                case Mode.RawIndexWeighted:
                    for (var i = 0; i < Repeats; i++)
                    {
                        using (var v = native.getValuesAsWeightedUTF8StringList(0))
                        {
                            if (v.hasValue())
                            {
                                _sink++;
                            }
                        }
                    }
                    break;
            }
            return Stopwatch.GetTimestamp() - start;
        }

        private static void Report(
            StringBuilder lines,
            string tag,
            string metric,
            List<double> values)
        {
            var sorted = values.OrderBy(v => v).ToArray();
            lines.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "COST|{0}|{1}|{2:F1}|{3:F1}|{4:F1}",
                tag,
                metric,
                sorted[sorted.Length / 2],
                sorted[0],
                sorted[sorted.Length - 1]));
        }
    }
}
