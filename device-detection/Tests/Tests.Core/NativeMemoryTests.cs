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

using FiftyOne.DeviceDetection.TestHelpers;
using FiftyOne.Pipeline.Engines;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using System.Runtime.InteropServices;
using Constants = FiftyOne.DeviceDetection.TestHelpers.Constants;

namespace FiftyOne.DeviceDetection.Tests.Core;

/// <summary>
/// Checks that disposing the flow data and the pipeline frees the native
/// memory of the engine, with no garbage collection while measuring.
/// </summary>
[TestClass]
[DoNotParallelize]
public class NativeMemoryTests
{
    /// <summary>
    /// The memory the process may keep after everything is disposed.
    /// MaxPerformance holds the whole Lite data file in memory,
    /// about 35 MB, so a data set that is not freed is larger than this.
    /// </summary>
    private const long MemoryMarginMegabytes = 10;

    /// <summary>
    /// The managed memory the measured cycle may allocate before a
    /// collection would run. The cycle allocates less than 1 MB.
    /// </summary>
    private const long NoCollectionMegabytes = 8;

    private const int UserAgentCount = 100;

    private static readonly string[] UserAgents = new UserAgentGenerator(
            TestHelpers.Utils.GetFilePath(Constants.UA_FILE_NAME))
        .GetRandomUserAgents(UserAgentCount)
        .ToArray();

    [TestMethod]
    public void NativeMemory_FreedByDispose()
    {
        if (OperatingSystem.IsWindows() == false &&
            OperatingSystem.IsLinux() == false)
        {
            Assert.Inconclusive(
                "Process memory is measured on Windows and Linux only.");
        }

        // Loads the native library and compiles the code, so that neither
        // counts as memory the measured cycle kept.
        RunPipeline();

        // With no collection, no finalizer can free anything while
        // measuring, so any memory returned was returned by Dispose.
        Assert.IsTrue(
            GC.TryStartNoGCRegion(NoCollectionMegabytes << 20),
            "The runtime could not suspend garbage collection.");
        long before, loaded, after;
        bool collected;
        try
        {
            GC.WaitForPendingFinalizers();
            before = ProcessMegabytes();
            loaded = RunPipeline();
            after = ProcessMegabytes();
        }
        finally
        {
            collected = GCSettings.LatencyMode != GCLatencyMode.NoGCRegion;
            if (collected == false)
            {
                GC.EndNoGCRegion();
            }
        }

        Assert.IsLessThan(MemoryMarginMegabytes, after - before,
            $"Disposing kept {after - before} MB of the {loaded - before} " +
            "MB the pipeline used.");
        Assert.IsFalse(collected,
            "A garbage collection ran while measuring, so the result does " +
            "not show that Dispose freed the memory.");
        Assert.IsGreaterThan(2 * MemoryMarginMegabytes, loaded - before,
            $"The pipeline used only {loaded - before} MB, too little to " +
            "show whether it was freed.");
    }

    /// <summary>
    /// Builds a pipeline, processes the User-Agents and disposes
    /// everything.
    /// </summary>
    /// <returns>
    /// The process memory in megabytes before the pipeline was disposed.
    /// </returns>
    private static long RunPipeline()
    {
        var datafile = TestHelpers.Utils.GetFilePath(
            Constants.LITE_HASH_DATA_FILE_NAME);
        using var pipeline = new DeviceDetectionPipelineBuilder(
                new NullLoggerFactory(),
                null,
                new Mock<IDataUpdateService>().Object)
            .UseOnPremise(datafile.FullName, null, false)
            .SetPerformanceProfile(PerformanceProfiles.MaxPerformance)
            .SetShareUsage(false)
            .SetDataFileSystemWatcher(false)
            .Build();
        foreach (var userAgent in UserAgents)
        {
            using var flowData = pipeline.CreateFlowData();
            flowData.AddEvidence("header.user-agent", userAgent);
            flowData.Process();
            _ = flowData.Get<IDeviceData>().IsMobile;
        }
        return ProcessMegabytes();
    }

    /// <summary>
    /// The memory the process holds, in megabytes. On Windows this is the
    /// private memory. On Linux it is the resident memory, read after
    /// malloc_trim returns the memory the C library keeps free for reuse,
    /// which would otherwise stay resident after the data set is freed.
    /// </summary>
    private static long ProcessMegabytes()
    {
        if (OperatingSystem.IsLinux())
        {
            try
            {
                NativeMethods.MallocTrim(UIntPtr.Zero);
            }
            catch (Exception e) when (
                e is DllNotFoundException || e is EntryPointNotFoundException)
            {
                Assert.Inconclusive(
                    "The C library has no malloc_trim to return freed " +
                    "memory to the system, so the memory is not measured.");
            }
        }
        using var process = Process.GetCurrentProcess();
        return (OperatingSystem.IsWindows()
            ? process.PrivateMemorySize64
            : process.WorkingSet64) >> 20;
    }

    private static class NativeMethods
    {
        [DllImport("libc.so.6", EntryPoint = "malloc_trim")]
        internal static extern int MallocTrim(UIntPtr pad);
    }
}
