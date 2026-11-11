// Temporary timing harness, never committed to a pull request. Writes COST
// lines to the file named by COST_OUT.
using FiftyOne.DeviceDetection.Hash.Engine.OnPremise.Data;
using FiftyOne.DeviceDetection.Hash.Engine.OnPremise.FlowElements;
using FiftyOne.DeviceDetection.Hash.Engine.OnPremise.Wrappers;
using FiftyOne.DeviceDetection.Shared.Data;
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

namespace FiftyOne.DeviceDetection.Hash.Tests.Cost
{
    [TestClass]
    public class IndexCostTimingHash
    {
        private const int EvidenceCount = 5000;
        private const int Rounds = 5;
        private const int Builds = 5;
        private const int Repeats = 20;
        private const int IsMobileIndex = 1;
        private const int BrowserNameIndex = 2;

        private static readonly string[] Names =
        {
            "ScreenPixelsWidth",
            "IsMobile",
            "BrowserName",
            "PlatformName",
            "HardwareVendor",
        };

        private static readonly PropertyInfo ResultsProperty =
            typeof(DeviceDataBaseOnPremise<IResultsSwigWrapper>).GetProperty(
                "Results", BindingFlags.Instance | BindingFlags.NonPublic);

        private static long _sink;

        private enum Mode
        {
            Process,
            Typed,
            ByName,
            RawFastBool,
            RawNameBool,
            RawIndexBool,
            RawFastString,
            RawIndexString,
        }

        [TestMethod]
        public void Measure()
        {
            var dir = Environment.GetEnvironmentVariable("COST_DD_DIR");
            var output = Environment.GetEnvironmentVariable("COST_OUT");
            var label = Environment.GetEnvironmentVariable("COST_LABEL") ?? "";
            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(output))
            {
                Assert.Inconclusive("COST_DD_DIR and COST_OUT are not set.");
                return;
            }
            var userAgents = File.ReadLines(
                    Path.Combine(dir, "20000 User Agents.csv"))
                .Where(l => l.Length > 0)
                .Take(EvidenceCount)
                .ToArray();
            var lines = new StringBuilder();
            foreach (var file in new[] {
                "TAC-HashV41.hash", "51Degrees-LiteV4.1.hash" })
            {
                var path = Path.Combine(dir, file);
                if (File.Exists(path) == false)
                {
                    lines.AppendLine($"COST|{label}|{file}|missing");
                    continue;
                }
                foreach (var profile in new[] {
                    PerformanceProfiles.MaxPerformance,
                    PerformanceProfiles.LowMemory })
                {
                    foreach (var names in new[] { null, Names })
                    {
                        Run(lines, label, file, path, profile, names,
                            userAgents);
                        File.AppendAllText(output, lines.ToString());
                        lines.Clear();
                    }
                }
            }
            File.AppendAllText(output, $"COST|{label}|sink|{_sink}\n");
        }

        private static void Run(
            StringBuilder lines,
            string label,
            string file,
            string path,
            PerformanceProfiles profile,
            string[] names,
            string[] userAgents)
        {
            var tag = string.Join("|", label, file, profile,
                names == null ? "none" : "five");
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
                    (m != Mode.RawIndexBool && m != Mode.RawIndexString))
                .ToArray();
            var engine = Build(path, profile, names);
            var pipeline = new PipelineBuilder(NullLoggerFactory.Instance)
                .AddFlowElement(engine)
                .Build();
            try
            {
                foreach (var mode in modes)
                {
                    Round(pipeline, userAgents, mode);
                }
                var figures = modes.ToDictionary(m => m, m => new List<double>());
                for (var round = 0; round < Rounds; round++)
                {
                    foreach (var mode in modes)
                    {
                        Settle();
                        figures[mode].Add(Round(pipeline, userAgents, mode));
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

        private static DeviceDetectionHashEngine Build(
            string path,
            PerformanceProfiles profile,
            string[] names)
        {
            var builder = new DeviceDetectionHashEngineBuilder(
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
            string[] userAgents,
            Mode mode)
        {
            long rawTicks = 0;
            var start = Stopwatch.GetTimestamp();
            foreach (var userAgent in userAgents)
            {
                using (var flowData = pipeline.CreateFlowData())
                {
                    flowData.AddEvidence("header.user-agent", userAgent);
                    flowData.Process();
                    var device = flowData.Get<IDeviceDataHash>();
                    switch (mode)
                    {
                        case Mode.Process:
                            break;
                        case Mode.Typed:
                            Sink(device.IsMobile);
                            break;
                        case Mode.ByName:
                            Sink(device["IsMobile"] as IAspectPropertyValue<bool>);
                            break;
                        default:
                            rawTicks += Raw(device, mode);
                            break;
                    }
                }
            }
            var total = Stopwatch.GetTimestamp() - start;
            var raw = mode >= Mode.RawFastBool;
            var ticks = raw ? rawTicks : total;
            var count = raw ?
                (double)userAgents.Length * Repeats :
                userAgents.Length;
            return ticks * 1e9 / Stopwatch.Frequency / count;
        }

        private static void Sink(IAspectPropertyValue<bool> value)
        {
            if (value != null && value.HasValue && value.Value)
            {
                _sink++;
            }
        }

        private static long Raw(IDeviceDataHash device, Mode mode)
        {
            var manager = ResultsProperty.GetValue(device);
            var list = (IReadOnlyList<IResultsSwigWrapper>)manager.GetType()
                .GetProperty("ResultsList").GetValue(manager);
            var wrapper = (ResultsSwigWrapper)list[0];
            var native = wrapper.Object;
            var start = Stopwatch.GetTimestamp();
            switch (mode)
            {
                case Mode.RawFastBool:
                    for (var i = 0; i < Repeats; i++)
                    {
                        if (wrapper.TryGetBoolFast("IsMobile", out bool b) && b)
                        {
                            _sink++;
                        }
                    }
                    break;
                case Mode.RawNameBool:
                    for (var i = 0; i < Repeats; i++)
                    {
                        using (var v = native.getValueAsBool("IsMobile"))
                        {
                            if (v.hasValue() && v.getValue())
                            {
                                _sink++;
                            }
                        }
                    }
                    break;
                case Mode.RawIndexBool:
                    for (var i = 0; i < Repeats; i++)
                    {
                        using (var v = native.getValueAsBool(IsMobileIndex))
                        {
                            if (v.hasValue() && v.getValue())
                            {
                                _sink++;
                            }
                        }
                    }
                    break;
                case Mode.RawFastString:
                    for (var i = 0; i < Repeats; i++)
                    {
                        if (wrapper.TryGetStringFast("BrowserName", out string s))
                        {
                            _sink += s.Length;
                        }
                    }
                    break;
                case Mode.RawIndexString:
                    for (var i = 0; i < Repeats; i++)
                    {
                        using (var v = native.getValueAsString(BrowserNameIndex))
                        {
                            if (v.hasValue())
                            {
                                _sink += v.getValue().Length;
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
