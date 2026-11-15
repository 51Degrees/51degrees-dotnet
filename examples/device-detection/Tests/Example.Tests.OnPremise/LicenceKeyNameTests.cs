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
using Program = FiftyOne.DeviceDetection.Examples.OnPremise.UpdateDataFile.Program;

namespace FiftyOne.DeviceDetection.Example.Tests.OnPremise;

/// <summary>
/// The data file update example reads its licence key from
/// '_51DEGREES_LICENSE_KEY', and from the name it read before only when
/// that is not set. The tests change the environment of the whole process,
/// so they run apart from the tests that read it.
/// </summary>
[TestClass]
[DoNotParallelize]
public class LicenceKeyNameTests
{
    private const string Name = "_51DEGREES_LICENSE_KEY";
    private const string OldName = "DEVICEDETECTIONLICENSEKEY_DOTNET";

    private string _savedName;
    private string _savedOldName;

    [TestInitialize]
    public void Init()
    {
        _savedName = Environment.GetEnvironmentVariable(Name);
        _savedOldName = Environment.GetEnvironmentVariable(OldName);
        Environment.SetEnvironmentVariable(Name, null);
        Environment.SetEnvironmentVariable(OldName, null);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Environment.SetEnvironmentVariable(Name, _savedName);
        Environment.SetEnvironmentVariable(OldName, _savedOldName);
    }

    /// <summary>
    /// The licence key as the example reads it. The method is called by
    /// name, so that an example without it fails the test and not the
    /// build.
    /// </summary>
    private static string LicenseKey()
    {
        var read = typeof(Program).GetMethod(
            "LicenseKeyFromEnvironment", Type.EmptyTypes);
        Assert.IsNotNull(
            read, "The example has no LicenseKeyFromEnvironment.");
        return (string)read.Invoke(null, null);
    }

    [TestMethod]
    public void TheNameIsRead()
    {
        Environment.SetEnvironmentVariable(Name, "new");
        Assert.AreEqual("new", LicenseKey());
    }

    [TestMethod]
    public void TheNameIsReadBeforeTheOldName()
    {
        Environment.SetEnvironmentVariable(Name, "new");
        Environment.SetEnvironmentVariable(OldName, "old");
        Assert.AreEqual("new", LicenseKey());
    }

    [TestMethod]
    public void TheOldNameIsReadWhenTheNameIsNotSet()
    {
        Environment.SetEnvironmentVariable(OldName, "old");
        Assert.AreEqual("old", LicenseKey());
    }

    [TestMethod]
    public void NoKeyIsReadWhenNeitherIsSet()
    {
        Assert.IsNull(LicenseKey());
    }
}
