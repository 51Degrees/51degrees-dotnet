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

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using NativeModelLookup =
    FiftyOne.DeviceDetection.Examples.Cloud.NativeModelLookup.Program;
using TacLookup =
    FiftyOne.DeviceDetection.Examples.Cloud.TacLookup.Program;

namespace FiftyOne.DeviceDetection.Example.Tests.Cloud;

/// <summary>
/// Runs each hardware lookup example against a stand-in for the cloud
/// service that answers as it does for a resource key with the hardware
/// lookup, where every lookup matches no device. The example should say so
/// under each heading rather than print nothing. No real key or network is
/// needed.
/// </summary>
[TestClass]
public class HardwareLookupMatchingNothingTests
{
    /// <summary>
    /// Any value, because the stand-in accepts every key.
    /// </summary>
    private const string StandInKey = "stand-in-key";

    [TestMethod]
    public void TacLookup_NoMatchingDevice_SaysSoUnderEachHeading()
    {
        using var standIn = new CloudStandIn(StandInAnswer.NoDevices);
        var output = new StringWriter();
        var options = TestExamples.CreateConfiguration(
            typeof(TacLookup.Example), StandInKey, standIn.EndPoint);

        new TacLookup.Example().Run(options, output);

        AssertSaysNoDevices(output.ToString(), standIn, "TAC",
            TacLookup.Example.NoHardwareLookup,
            TacLookup.Example.Tac1, TacLookup.Example.Tac2);
    }

    [TestMethod]
    public void NativeModelLookup_NoMatchingDevice_SaysSoUnderEachHeading()
    {
        using var standIn = new CloudStandIn(StandInAnswer.NoDevices);
        using var loggerFactory = new LoggerFactory();
        var output = new StringWriter();

        new NativeModelLookup.Example().Run(
            StandInKey, loggerFactory, output, standIn.EndPoint);

        AssertSaysNoDevices(output.ToString(), standIn, "native model name",
            NativeModelLookup.Example.NoHardwareLookup,
            NativeModelLookup.Example.NativeModel1,
            NativeModelLookup.Example.NativeModel2);
    }

    /// <summary>
    /// The first request for the key's properties fails and the next
    /// answers with the hardware lookup. The example should ask again and
    /// go on to look each value up, not decide the key lacks the lookup.
    /// </summary>
    [TestMethod]
    public void TacLookup_PropertiesFailOnce_StillLooksUp()
    {
        using var standIn = new CloudStandIn(
            StandInAnswer.FailsOnceThenNoDevices);
        var output = new StringWriter();
        var options = TestExamples.CreateConfiguration(
            typeof(TacLookup.Example), StandInKey, standIn.EndPoint);

        new TacLookup.Example().Run(options, output);

        AssertSaysNoDevices(output.ToString(), standIn, "TAC",
            TacLookup.Example.NoHardwareLookup,
            TacLookup.Example.Tac1, TacLookup.Example.Tac2);
    }

    /// <summary>
    /// As <see cref="TacLookup_PropertiesFailOnce_StillLooksUp"/> for the
    /// native model example.
    /// </summary>
    [TestMethod]
    public void NativeModelLookup_PropertiesFailOnce_StillLooksUp()
    {
        using var standIn = new CloudStandIn(
            StandInAnswer.FailsOnceThenNoDevices);
        using var loggerFactory = new LoggerFactory();
        var output = new StringWriter();

        new NativeModelLookup.Example().Run(
            StandInKey, loggerFactory, output, standIn.EndPoint);

        AssertSaysNoDevices(output.ToString(), standIn, "native model name",
            NativeModelLookup.Example.NoHardwareLookup,
            NativeModelLookup.Example.NativeModel1,
            NativeModelLookup.Example.NativeModel2);
    }

    /// <summary>
    /// Fails unless each value's heading is followed by the line saying no
    /// device is associated with it, the example did not say the key lacks
    /// the lookup, and it made a lookup request for each value.
    /// </summary>
    private static void AssertSaysNoDevices(
        string output,
        CloudStandIn standIn,
        string subject,
        string noLookupLine,
        params string[] values)
    {
        Console.WriteLine(output);
        var lines = output.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        Assert.IsFalse(lines.Contains(noLookupLine),
            "The example should not say the key lacks the lookup.");
        foreach (var value in values)
        {
            var heading =
                $"Which devices are associated with the {subject} '{value}'?";
            var at = lines.IndexOf(heading);
            Assert.IsTrue(at >= 0 && at + 1 < lines.Count,
                $"The example should print the heading for '{value}'." +
                Environment.NewLine + output);
            Assert.AreEqual(
                $"\tNo devices are associated with this {subject}.",
                lines[at + 1],
                $"The example should say no device matches '{value}'.");
        }
        Assert.AreEqual(values.Length,
            standIn.Paths.Count(p => p.EndsWith("/json")),
            "The example should make one lookup request for each value.");
    }
}
