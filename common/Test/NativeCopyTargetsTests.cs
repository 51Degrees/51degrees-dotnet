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
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace FiftyOne.Common.Tests;

/// <summary>
/// A .NET Framework project does not read the runtimes folder of a
/// package, so each on-premise engine package carries a .targets file that
/// copies its native library to the project's output. Each test lays out a
/// package in a temporary folder as NuGet unpacks one, with the engine's
/// .targets file and a stand-in library under each runtimes folder the
/// real package has, and a project that imports the .targets file after
/// the SDK, where NuGet imports it. MSBuild is then asked, without
/// building, which library the project copies to its output.
/// </summary>
[TestClass]
public class NativeCopyTargetsTests
{
    /// <summary>
    /// How long MSBuild is given to evaluate the project.
    /// </summary>
    private const int MsBuildTimeoutSeconds = 120;

    private const string DeviceDetectionTargets =
        "device-detection/Hash.Engine.OnPremise/" +
        "FiftyOne.DeviceDetection.Hash.Engine.OnPremise.targets";

    private const string DeviceDetectionLibrary =
        "FiftyOne.DeviceDetection.Hash.Engine.OnPremise.Native.dll";

    private const string IpIntelligenceTargets =
        "ip-intelligence/Engine.OnPremise/" +
        "FiftyOne.IpIntelligence.Engine.OnPremise.targets";

    private const string IpIntelligenceLibrary =
        "FiftyOne.IpIntelligence.Engine.OnPremise.Native.dll";

    /// <summary>
    /// A .NET Framework project copies the library for its platform, and
    /// AnyCPU, which is the default, and no platform at all run as x64 on
    /// a 64-bit Windows. The Device Detection package has a Windows x86
    /// library and the IP Intelligence package does not, so an x86 project
    /// copies nothing from the IP Intelligence package.
    /// </summary>
    [DataTestMethod]
    [DataRow(DeviceDetectionTargets, DeviceDetectionLibrary, true, "", "win-x64")]
    [DataRow(DeviceDetectionTargets, DeviceDetectionLibrary, true, "AnyCPU", "win-x64")]
    [DataRow(DeviceDetectionTargets, DeviceDetectionLibrary, true, "x64", "win-x64")]
    [DataRow(DeviceDetectionTargets, DeviceDetectionLibrary, true, "x86", "win-x86")]
    [DataRow(IpIntelligenceTargets, IpIntelligenceLibrary, false, "", "win-x64")]
    [DataRow(IpIntelligenceTargets, IpIntelligenceLibrary, false, "AnyCPU", "win-x64")]
    [DataRow(IpIntelligenceTargets, IpIntelligenceLibrary, false, "x64", "win-x64")]
    [DataRow(IpIntelligenceTargets, IpIntelligenceLibrary, false, "x86", null)]
    public void FrameworkProject_CopiesTheLibraryForItsPlatform(
        string targets,
        string library,
        bool hasX86,
        string platform,
        string expectedRuntime)
    {
        var copied = CopiedLibrary(
            targets, library, hasX86, "net462", platform);
        if (expectedRuntime == null)
        {
            Assert.IsNull(copied, $"Nothing should be copied, not {copied}.");
            return;
        }
        Assert.IsNotNull(
            copied,
            $"A .NET Framework project on platform '{platform}' copies no " +
            $"{library}, so it cannot load the engine.");
        StringAssert.EndsWith(
            copied.Replace('\\', '/'),
            $"/runtimes/{expectedRuntime}/native/{library}");
    }

    /// <summary>
    /// A .NET project reads the runtimes folder of the package itself, so
    /// the .targets file copies nothing, which would otherwise overwrite
    /// the library for the platform a publish is for.
    /// </summary>
    [DataTestMethod]
    [DataRow(DeviceDetectionTargets, DeviceDetectionLibrary, true)]
    [DataRow(IpIntelligenceTargets, IpIntelligenceLibrary, false)]
    public void DotNetProject_CopiesNothing(
        string targets,
        string library,
        bool hasX86)
    {
        var copied = CopiedLibrary(
            targets, library, hasX86, "net8.0", "AnyCPU");
        Assert.IsNull(copied, $"Nothing should be copied, not {copied}.");
    }

    /// <summary>
    /// The full path of the library the project copies to its output, or
    /// null if it copies none.
    /// </summary>
    private static string CopiedLibrary(
        string targets,
        string library,
        bool hasX86,
        string targetFramework,
        string platform)
    {
        var root = Repository.Root();
        var folder = Path.Combine(
            Path.GetTempPath(), "native-copy-" + Guid.NewGuid().ToString("N"));
        try
        {
            var package = Path.Combine(folder, "package");
            var build = Path.Combine(package, "build");
            Directory.CreateDirectory(build);
            var targetsName = Path.GetFileName(targets);
            File.Copy(
                Path.Combine(root, targets.Replace(
                    '/', Path.DirectorySeparatorChar)),
                Path.Combine(build, targetsName));
            var runtimes = new List<string> { "win-x64" };
            if (hasX86)
            {
                runtimes.Add("win-x86");
            }
            foreach (var runtime in runtimes)
            {
                var native = Path.Combine(
                    package, "runtimes", runtime, "native");
                Directory.CreateDirectory(native);
                File.WriteAllText(Path.Combine(native, library), runtime);
            }
            var project = Path.Combine(folder, "consumer", "Consumer.csproj");
            Directory.CreateDirectory(Path.GetDirectoryName(project));
            File.WriteAllText(project,
                "<Project>\n" +
                "  <Import Project=\"Sdk.props\" Sdk=\"Microsoft.NET.Sdk\" />\n" +
                "  <PropertyGroup>\n" +
                $"    <TargetFramework>{targetFramework}</TargetFramework>\n" +
                "  </PropertyGroup>\n" +
                "  <Import Project=\"Sdk.targets\" Sdk=\"Microsoft.NET.Sdk\" />\n" +
                $"  <Import Project=\"..\\package\\build\\{targetsName}\" />\n" +
                "</Project>\n");
            return Evaluate(project, platform, library);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>
    /// Ask MSBuild for the project's None and Content items, without
    /// building it, and return the full path of the one linked as the
    /// library, or null.
    /// </summary>
    private static string Evaluate(
        string project,
        string platform,
        string library)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(project),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        // No first run banner or telemetry notice in front of the JSON.
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.ArgumentList.Add("msbuild");
        start.ArgumentList.Add(project);
        start.ArgumentList.Add("-getItem:None");
        start.ArgumentList.Add("-getItem:Content");
        if (string.IsNullOrEmpty(platform) == false)
        {
            start.ArgumentList.Add($"-p:Platform={platform}");
        }
        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("dotnet did not start.");
        var errorRead = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        Assert.IsTrue(
            process.WaitForExit(MsBuildTimeoutSeconds * 1000),
            "MSBuild did not finish evaluating the project in time.");
        Assert.AreEqual(
            0,
            process.ExitCode,
            $"MSBuild failed: {output} {errorRead.Result}");
        using var json = JsonDocument.Parse(output);
        foreach (var kind in json.RootElement.GetProperty("Items")
            .EnumerateObject())
        {
            foreach (var item in kind.Value.EnumerateArray())
            {
                if (item.TryGetProperty("Link", out var link) &&
                    string.Equals(
                        link.GetString(),
                        library,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return item.GetProperty("FullPath").GetString();
                }
            }
        }
        return null;
    }
}
