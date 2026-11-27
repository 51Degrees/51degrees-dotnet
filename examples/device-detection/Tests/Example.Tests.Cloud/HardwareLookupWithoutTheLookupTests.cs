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

using FiftyOne.DeviceDetection.Examples;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NativeModelLookup =
    FiftyOne.DeviceDetection.Examples.Cloud.NativeModelLookup.Program;
using TacLookup =
    FiftyOne.DeviceDetection.Examples.Cloud.TacLookup.Program;

namespace FiftyOne.DeviceDetection.Example.Tests.Cloud;

/// <summary>
/// Runs each hardware lookup example against a stand-in for the cloud
/// service that answers as it does for a resource key without the hardware
/// lookup. The example should say so on one line, with links to create a
/// key that includes it and to contact 51Degrees, end without an error and
/// look nothing up. No real key or network is needed.
/// </summary>
[TestClass]
public class HardwareLookupWithoutTheLookupTests
{
    /// <summary>
    /// Any value, because the stand-in accepts every key.
    /// </summary>
    private const string StandInKey = "stand-in-key";

    [TestMethod]
    public void TacLookup_KeyWithoutTheLookup_SaysSoOnOneLine()
    {
        using var standIn = new CloudStandIn();
        var output = new StringWriter();
        var options = TestExamples.CreateConfiguration(
            typeof(TacLookup.Example), StandInKey, standIn.EndPoint);

        new TacLookup.Example().Run(options, output);

        HardwareLookupOutput.AssertSaysTheKeyLacksTheLookup(
            output.ToString(), TacLookup.Example.NoHardwareLookup);
        AssertLookedNothingUp(standIn);
    }

    [TestMethod]
    public void NativeModelLookup_KeyWithoutTheLookup_SaysSoOnOneLine()
    {
        using var standIn = new CloudStandIn();
        using var loggerFactory = new LoggerFactory();
        var output = new StringWriter();

        new NativeModelLookup.Example().Run(
            StandInKey, loggerFactory, output, standIn.EndPoint);

        HardwareLookupOutput.AssertSaysTheKeyLacksTheLookup(
            output.ToString(), NativeModelLookup.Example.NoHardwareLookup);
        AssertLookedNothingUp(standIn);
    }

    /// <summary>
    /// Runs each example's own build as a process, as a person runs it,
    /// and checks it also ends with exit code 0 and the "Find out more"
    /// section after the line.
    /// </summary>
    [TestMethod]
    [DataRow("examples/device-detection/Cloud/TAC-Console",
        "FiftyOne.DeviceDetection.Examples.Cloud.TacLookup",
        TacLookup.Example.NoHardwareLookup)]
    [DataRow("examples/device-detection/Cloud/NativeModel-Console",
        "FiftyOne.DeviceDetection.Examples.Cloud.NativeModelLookup",
        NativeModelLookup.Example.NoHardwareLookup)]
    public void Example_KeyWithoutTheLookup_EndsWithExitCodeZero(
        string projectFolder,
        string assemblyName,
        string expectedLine)
    {
        using var standIn = new CloudStandIn();

        var (exitCode, output) = ExampleWithoutKey.Run(
            projectFolder,
            assemblyName,
            new Dictionary<string, string>
            {
                [ExampleUtils.CLOUD_RESOURCE_KEY_ENV_VAR] = StandInKey,
                [ExampleUtils.CLOUD_ENDPOINT_ENV_VAR] = standIn.EndPoint,
            });

        Console.WriteLine(output);
        Assert.AreEqual(0, exitCode,
            $"{assemblyName} should end with exit code 0.");
        HardwareLookupOutput.AssertSaysTheKeyLacksTheLookup(
            output, expectedLine);
        Assert.IsTrue(
            output.IndexOf("Find out more", StringComparison.Ordinal) >
                output.IndexOf(expectedLine, StringComparison.Ordinal),
            $"{assemblyName} should end with the Find out more section.");
        AssertNoError(output, assemblyName);
        AssertLookedNothingUp(standIn);
    }

    /// <summary>
    /// Runs each example's own build as a process against a stand-in that
    /// fails the first request for the key's properties and then answers
    /// as for a key without the lookup. The example should ask again and
    /// still say on one line that the key lacks the lookup.
    /// </summary>
    [TestMethod]
    [DataRow("examples/device-detection/Cloud/TAC-Console",
        "FiftyOne.DeviceDetection.Examples.Cloud.TacLookup",
        TacLookup.Example.NoHardwareLookup)]
    [DataRow("examples/device-detection/Cloud/NativeModel-Console",
        "FiftyOne.DeviceDetection.Examples.Cloud.NativeModelLookup",
        NativeModelLookup.Example.NoHardwareLookup)]
    public void Example_PropertiesFailOnce_StillSaysTheKeyLacksTheLookup(
        string projectFolder,
        string assemblyName,
        string expectedLine)
    {
        using var standIn = new CloudStandIn(
            StandInAnswer.FailsOnceThenNoHardware);

        var (exitCode, output) = ExampleWithoutKey.Run(
            projectFolder,
            assemblyName,
            new Dictionary<string, string>
            {
                [ExampleUtils.CLOUD_RESOURCE_KEY_ENV_VAR] = StandInKey,
                [ExampleUtils.CLOUD_ENDPOINT_ENV_VAR] = standIn.EndPoint,
            });

        Console.WriteLine(output);
        Assert.AreEqual(0, exitCode,
            $"{assemblyName} should end with exit code 0.");
        HardwareLookupOutput.AssertSaysTheKeyLacksTheLookup(
            output, expectedLine);
        Assert.IsTrue(
            standIn.Paths.Count(p => p.EndsWith("/accessibleproperties")) >= 2,
            $"{assemblyName} should ask again for the key's properties.");
        AssertLookedNothingUp(standIn);
    }

    /// <summary>
    /// Runs each example's own build as a process against a stand-in that
    /// fails a request: every request with status 500, the request for the
    /// key's properties with 403 as for a refused key, or every lookup
    /// with 500. The example should say the request failed, with its
    /// status code, not that the key lacks the lookup, and end with the
    /// Find out more section and a non-zero exit code rather than on an
    /// unhandled exception.
    /// </summary>
    [TestMethod]
    [DataRow("examples/device-detection/Cloud/TAC-Console",
        "FiftyOne.DeviceDetection.Examples.Cloud.TacLookup",
        TacLookup.Example.NoHardwareLookup, StandInAnswer.Failing, 500)]
    [DataRow("examples/device-detection/Cloud/NativeModel-Console",
        "FiftyOne.DeviceDetection.Examples.Cloud.NativeModelLookup",
        NativeModelLookup.Example.NoHardwareLookup,
        StandInAnswer.Failing, 500)]
    [DataRow("examples/device-detection/Cloud/NativeModel-Console",
        "FiftyOne.DeviceDetection.Examples.Cloud.NativeModelLookup",
        NativeModelLookup.Example.NoHardwareLookup,
        StandInAnswer.RefusesTheKey, 403)]
    [DataRow("examples/device-detection/Cloud/TAC-Console",
        "FiftyOne.DeviceDetection.Examples.Cloud.TacLookup",
        TacLookup.Example.NoHardwareLookup, StandInAnswer.LookupFails, 500)]
    [DataRow("examples/device-detection/Cloud/NativeModel-Console",
        "FiftyOne.DeviceDetection.Examples.Cloud.NativeModelLookup",
        NativeModelLookup.Example.NoHardwareLookup,
        StandInAnswer.LookupFails, 500)]
    public void Example_CloudRequestFails_SaysTheRequestFailed(
        string projectFolder,
        string assemblyName,
        string noLookupLine,
        StandInAnswer answer,
        int statusCode)
    {
        using var standIn = new CloudStandIn(answer);

        var (exitCode, output) = RunAgainst(
            standIn, projectFolder, assemblyName);

        Assert.AreNotEqual(0, exitCode,
            $"{assemblyName} should end with a non-zero exit code.");
        var lines = Lines(output);
        var failure = lines.FindIndex(l => l.StartsWith(
            "The example stopped because the request to the cloud " +
            "service failed. ", StringComparison.Ordinal) &&
            l.Contains($"(status code {statusCode})",
                StringComparison.Ordinal));
        Assert.IsTrue(failure >= 0,
            $"{assemblyName} should say the request failed with status " +
            $"code {statusCode}.");
        Assert.IsTrue(
            lines.FindIndex(l => l == "Find out more") > failure,
            $"{assemblyName} should end with the Find out more section.");
        Assert.IsFalse(lines.Contains(noLookupLine),
            $"{assemblyName} should not say the key lacks the lookup.");
        Assert.IsFalse(
            output.Contains("Unhandled exception",
                StringComparison.OrdinalIgnoreCase),
            $"{assemblyName} should not end on an unhandled exception.");
        if (answer != StandInAnswer.LookupFails)
        {
            Assert.IsFalse(standIn.Paths.Any(p => p.EndsWith("/json")),
                $"{assemblyName} should make no lookup request.");
        }
    }

    /// <summary>
    /// Runs the TAC example's own build as a process against a stand-in
    /// that refuses the key. The example builds its pipeline from a
    /// configuration file, and that build reports the refusal in the
    /// message of its own exception, so the example should say the build
    /// failed with the service's message, not that the key lacks the
    /// lookup, and end with the Find out more section and a non-zero exit
    /// code rather than on an unhandled exception.
    /// </summary>
    [TestMethod]
    public void TacLookup_CloudRefusesTheKey_SaysThePipelineWasNotBuilt()
    {
        using var standIn = new CloudStandIn(StandInAnswer.RefusesTheKey);

        var (exitCode, output) = RunAgainst(
            standIn,
            "examples/device-detection/Cloud/TAC-Console",
            "FiftyOne.DeviceDetection.Examples.Cloud.TacLookup");

        Assert.AreNotEqual(0, exitCode,
            "The example should end with a non-zero exit code.");
        var lines = Lines(output);
        var failure = lines.IndexOf("The example stopped because the " +
            "pipeline could not be built from its configuration.");
        Assert.IsTrue(failure >= 0,
            "The example should say its pipeline could not be built.");
        StringAssert.Contains(output,
            "Error returned from 51Degrees cloud service: The stand-in " +
            "service is failing.");
        Assert.IsTrue(
            lines.FindIndex(l => l == "Find out more") > failure,
            "The example should end with the Find out more section.");
        Assert.IsFalse(lines.Contains(TacLookup.Example.NoHardwareLookup),
            "The example should not say the key lacks the lookup.");
        Assert.IsFalse(
            output.Contains("Unhandled exception",
                StringComparison.OrdinalIgnoreCase),
            "The example should not end on an unhandled exception.");
    }

    /// <summary>
    /// Runs each example's own build as a process against a stand-in whose
    /// key includes the lookup with a profile property the SDK cannot
    /// read. That is a fault, so the example should end with a non-zero
    /// exit code and never say the key lacks the lookup.
    /// </summary>
    [TestMethod]
    [DataRow("examples/device-detection/Cloud/TAC-Console",
        "FiftyOne.DeviceDetection.Examples.Cloud.TacLookup",
        TacLookup.Example.NoHardwareLookup)]
    [DataRow("examples/device-detection/Cloud/NativeModel-Console",
        "FiftyOne.DeviceDetection.Examples.Cloud.NativeModelLookup",
        NativeModelLookup.Example.NoHardwareLookup)]
    public void Example_UnreadableProfileProperty_DoesNotBlameTheKey(
        string projectFolder,
        string assemblyName,
        string noLookupLine)
    {
        using var standIn = new CloudStandIn(
            StandInAnswer.UnknownProfileProperty);

        var (exitCode, output) = RunAgainst(
            standIn, projectFolder, assemblyName);

        Assert.AreNotEqual(0, exitCode,
            $"{assemblyName} should end with a non-zero exit code.");
        Assert.IsFalse(
            Lines(output).Contains(noLookupLine),
            $"{assemblyName} should not say the key lacks the lookup.");
    }

    /// <summary>
    /// The lines the example printed, without their line endings.
    /// </summary>
    private static List<string> Lines(string output)
    {
        return output.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
    }

    /// <summary>
    /// Runs the example's own build as a process with the stand-in key and
    /// endpoint in its environment, and writes what it printed to the test
    /// output.
    /// </summary>
    private static (int ExitCode, string Output) RunAgainst(
        CloudStandIn standIn,
        string projectFolder,
        string assemblyName)
    {
        var result = ExampleWithoutKey.Run(
            projectFolder,
            assemblyName,
            new Dictionary<string, string>
            {
                [ExampleUtils.CLOUD_RESOURCE_KEY_ENV_VAR] = StandInKey,
                [ExampleUtils.CLOUD_ENDPOINT_ENV_VAR] = standIn.EndPoint,
            });
        Console.WriteLine(result.Output);
        return result;
    }

    /// <summary>
    /// Fails if the example logged an error or ended on an unhandled
    /// exception.
    /// </summary>
    private static void AssertNoError(string output, string assemblyName)
    {
        Assert.IsFalse(
            output.Contains("Unhandled exception",
                StringComparison.OrdinalIgnoreCase),
            $"{assemblyName} should end without a stack trace.");
        Assert.IsFalse(
            output.Split('\n').Any(l => l.StartsWith("fail:",
                StringComparison.Ordinal)),
            $"{assemblyName} should log no error.");
    }

    /// <summary>
    /// Fails unless the example asked the stand-in for the key's
    /// properties and made no lookup request.
    /// </summary>
    private static void AssertLookedNothingUp(CloudStandIn standIn)
    {
        var paths = standIn.Paths;
        Assert.IsTrue(
            paths.Any(p => p.EndsWith("/accessibleproperties")),
            "The example should ask the stand-in for the key's properties.");
        Assert.IsFalse(
            paths.Any(p => p.EndsWith("/json")),
            "The example should make no lookup request. It asked for: " +
            string.Join(", ", paths));
    }
}
