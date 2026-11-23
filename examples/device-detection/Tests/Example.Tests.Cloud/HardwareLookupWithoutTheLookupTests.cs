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
        AssertLookedNothingUp(standIn);
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
