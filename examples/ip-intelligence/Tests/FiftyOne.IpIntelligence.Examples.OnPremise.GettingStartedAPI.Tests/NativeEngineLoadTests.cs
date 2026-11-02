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

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FiftyOne.IpIntelligence.Examples.OnPremise.GettingStartedAPI.Tests;

/// <summary>
/// Focused pre-flight checks for the native IP Intelligence engine that this
/// project loads through its ASP.NET Core reference to GettingStarted-API.
///
/// These exist because a native-load failure normally surfaces only as an
/// opaque wrapper — <c>PipelineConfigurationException</c> -> "The type
/// initializer for '...IpIntelligenceEngineModulePINVOKE' threw an exception."
/// — which hides the real Win32 reason. When one of these tests fails it names
/// the actual cause directly, so the next regression is diagnosed from the
/// assertion message alone instead of another round of ad-hoc logging.
///
/// History: the nightly Windows integration run failed here with
/// <c>0x800700CE</c> (ERROR_FILENAME_EXCED_RANGE) because the native DLL's
/// dependency path exceeded MAX_PATH (260). This project's long directory name
/// pushed the <c>runtimes\win-x64\native\</c> path to 255 chars on its own; the
/// dependency the loader resolved next to it tipped over 260. The checkout
/// directory is now shortened in CI, and
/// <see cref="NativeEngineDllPath_IsWithinWindowsMaxPath"/> guards against the
/// margin silently eroding again.
/// </summary>
[TestClass]
public class NativeEngineLoadTests
{
    private const string IpiNativeDll =
        "FiftyOne.IpIntelligence.Engine.OnPremise.Native.dll";

    private const string DdNativeDll =
        "FiftyOne.DeviceDetection.Hash.Engine.OnPremise.Native.dll";

    private const string PInvokeTypeName =
        "FiftyOne.IpIntelligence.Engine.OnPremise.Interop.IpIntelligenceEngineModulePINVOKE, "
        + "FiftyOne.IpIntelligence.Engine.OnPremise";

    /// <summary>
    /// The Windows loader (and its dependency resolution) still enforces
    /// MAX_PATH even with LongPathsEnabled set, so the deployed native DLL path
    /// must stay clear of 260. We reserve a margin because it is not the native
    /// DLL itself but a sibling dependency it pulls from the same folder that
    /// overflows first.
    /// </summary>
    [TestMethod]
    public void NativeEngineDllPath_IsWithinWindowsMaxPath()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Inconclusive("MAX_PATH only constrains the Windows loader.");
        }

        const int windowsMaxPath = 260;

        // The overflow is not the engine DLL's own path but the path the loader
        // constructs for a sibling dependency it resolves from the same folder:
        // <native-dir>\<dependency-file>. Model that worst case directly rather
        // than the engine path, whose filename length differs from the
        // dependency's.
        var nativePath = GetNativeDllPath(IpiNativeDll);
        var nativeDir = Path.GetDirectoryName(nativePath)!;
        var worstDependencyPath =
            Path.Combine(nativeDir, DdNativeDll);

        Assert.IsTrue(
            worstDependencyPath.Length < windowsMaxPath,
            $"Native dependency path would be {worstDependencyPath.Length} chars, "
            + $"crossing the Windows MAX_PATH limit of {windowsMaxPath}; native "
            + $"load then fails with 0x800700CE (ERROR_FILENAME_EXCED_RANGE). "
            + $"Shorten the checkout / output directory. Worst-case dependency "
            + $"path: {worstDependencyPath}");
    }

    /// <summary>
    /// The native engine DLL must be deployed to the RID-specific output folder
    /// the loader probes. A missing file here means the build/publish did not
    /// carry the native asset, not that the load failed.
    /// </summary>
    [TestMethod]
    public void NativeEngineDll_IsDeployed()
    {
        var nativePath = GetNativeDllPath(IpiNativeDll);
        Assert.IsTrue(
            File.Exists(nativePath),
            $"Native engine DLL was not deployed to the expected RID path. "
            + $"Expected: {nativePath}");
    }

    /// <summary>
    /// Loads the native engine DLL directly so a failure reports the real leaf
    /// exception (e.g. <see cref="DllNotFoundException"/> for a missing
    /// dependency / path-too-long, or <see cref="BadImageFormatException"/> for
    /// an architecture mismatch) instead of the flattened wrapper the pipeline
    /// produces.
    /// </summary>
    [TestMethod]
    public void NativeEngineDll_LoadsDirectly()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Inconclusive(
                "This check targets the win-x64 native asset layout.");
        }

        var nativePath = GetNativeDllPath(IpiNativeDll);

        IntPtr handle = IntPtr.Zero;
        try
        {
            handle = NativeLibrary.Load(nativePath);
        }
        catch (DllNotFoundException ex)
        {
            Assert.Fail(
                $"Native engine DLL (or one of its dependencies) failed to load "
                + $"[{ex.GetType().Name}]: {ex.Message}. On Windows the common "
                + $"cause is 0x800700CE (path exceeds MAX_PATH) or a missing "
                + $"runtime dependency. Path: {nativePath}");
        }
        catch (BadImageFormatException ex)
        {
            Assert.Fail(
                $"Native engine DLL is the wrong architecture for this process "
                + $"({RuntimeInformation.ProcessArchitecture}) "
                + $"[{ex.GetType().Name}]: {ex.Message}. Path: {nativePath}");
        }
        finally
        {
            if (handle != IntPtr.Zero)
            {
                NativeLibrary.Free(handle);
            }
        }
    }

    /// <summary>
    /// Runs the SWIG-generated P/Invoke type initializer in isolation. This is
    /// the exact static constructor whose failure the production stack reports
    /// only as "The type initializer for '...ModulePINVOKE' threw an
    /// exception."; here the inner exception chain is surfaced verbatim.
    /// </summary>
    [TestMethod]
    public void NativeEnginePInvoke_TypeInitializerSucceeds()
    {
        var pinvokeType = Type.GetType(PInvokeTypeName);
        Assert.IsNotNull(
            pinvokeType,
            $"Could not resolve the native P/Invoke type '{PInvokeTypeName}'. "
            + "Has the engine package or its namespace changed?");

        try
        {
            RuntimeHelpers.RunClassConstructor(pinvokeType.TypeHandle);
        }
        catch (TypeInitializationException ex)
        {
            Assert.Fail(
                "Native P/Invoke type initializer threw. Real cause: "
                + DescribeChain(ex));
        }
    }

    private static string GetNativeDllPath(string fileName) =>
        Path.Combine(
            AppContext.BaseDirectory, "runtimes", "win-x64", "native", fileName);

    private static string DescribeChain(Exception ex)
    {
        var parts = new List<string>();
        for (var cur = ex; cur is not null; cur = cur.InnerException)
        {
            parts.Add($"[{cur.GetType().Name}] {cur.Message}");
        }
        return string.Join(" -> ", parts);
    }
}
