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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace FiftyOne.DeviceDetection.Example.Tests.Cloud;

/// <summary>
/// Checks which printed lines the live hardware lookup tests count as a
/// device with a vendor and a model, and that they fail when a lookup
/// lists none.
/// </summary>
[TestClass]
public class HardwareLookupOutputTests
{
    private const string Heading =
        "Which devices are associated with the TAC '35925406'?";

    [TestMethod]
    [DataRow("\tApple iPhone 6 (A1586)")]
    [DataRow("\tApple (A1586)")]
    [DataRow("\tApple iPhone 6 (A1586 (Global))")]
    [DataRow("\tApple No value (no name) (A1586)")]
    public void DeviceLine_WithVendorAndModel_Counts(string line)
    {
        Assert.IsTrue(HardwareLookupOutput.HasVendorAndModel(line), line);
    }

    [TestMethod]
    [DataRow("\tNo value (missing) iPhone 6 (A1586)")]
    [DataRow("\tApple iPhone 6 (No value (missing))")]
    [DataRow("\tApple iPhone 6 ()")]
    [DataRow("\tApple iPhone 6 ( )")]
    [DataRow("\t iPhone 6 (A1586)")]
    [DataRow("\tApple iPhone 6")]
    [DataRow("\tNo devices are associated with this TAC.")]
    [DataRow("Apple iPhone 6 (A1586)")]
    [DataRow("\t(A1586)")]
    public void DeviceLine_WithoutVendorOrModel_DoesNotCount(string line)
    {
        Assert.IsFalse(HardwareLookupOutput.HasVendorAndModel(line), line);
    }

    [TestMethod]
    public void AssertListsDevices_DeviceUnderHeading_Passes()
    {
        HardwareLookupOutput.AssertListsDevices(
            Lines(Heading, "\tApple iPhone 6 (A1586)"),
            "_51DEGREES_RESOURCE_KEY_BESPOKE", "TAC lookup", "TAC",
            "35925406");
    }

    [TestMethod]
    [DataRow(Heading + "\n\tNo devices are associated with this TAC.")]
    [DataRow(Heading)]
    [DataRow("\tApple iPhone 6 (A1586)")]
    [DataRow(Heading + "\nApple iPhone 6 (A1586)")]
    [DataRow(Heading + "\n\n\tApple iPhone 6 (A1586)")]
    public void AssertListsDevices_NoDeviceUnderHeading_Fails(string output)
    {
        var failure = Assert.ThrowsException<AssertFailedException>(() =>
            HardwareLookupOutput.AssertListsDevices(
                output, "_51DEGREES_RESOURCE_KEY_BESPOKE",
                "TAC lookup", "TAC", "35925406"));
        StringAssert.Contains(failure.Message,
            "No device with a vendor and a model was listed for the TAC " +
            "'35925406'.");
    }

    [TestMethod]
    public void AssertListsDevices_KeyWithoutTheLookup_FailsNamingIt()
    {
        var failure = Assert.ThrowsException<AssertFailedException>(() =>
            HardwareLookupOutput.AssertListsDevices(
                Lines(Examples.Cloud.TacLookup.Program.Example
                    .NoHardwareLookup),
                "_51DEGREES_RESOURCE_KEY_BESPOKE", "TAC lookup", "TAC",
                "35925406"));
        StringAssert.Contains(failure.Message,
            "The resource key in _51DEGREES_RESOURCE_KEY_BESPOKE does not " +
            "include HardwarePlatform.Profiles, which the TAC lookup needs.");
    }

    private static string Lines(params string[] lines)
    {
        return string.Join(Environment.NewLine, lines);
    }
}
