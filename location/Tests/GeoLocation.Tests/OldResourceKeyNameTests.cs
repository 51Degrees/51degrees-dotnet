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
using System.IO;
using System.Reflection;

#nullable disable

namespace FiftyOne.GeoLocation.Tests;

/// <summary>
/// Tests that the resource key for these tests is still read from the old
/// name of its environment variable when the new name is not set.
/// The tests change the environment of the whole process, so they run
/// apart from the tests that read it.
/// </summary>
[TestClass]
[DoNotParallelize]
public class OldResourceKeyNameTests
{
    private const string NewName = "_51DEGREES_RESOURCE_KEY";
    private const string OldName = "SUPER_RESOURCE_KEY";

    private string _previousNew;
    private string _previousOld;
    private TextWriter _previousError;
    private StringWriter _error;

    private static string Deprecation(string oldName, string newName) =>
        $"The environment variable {oldName} is deprecated and " +
        $"{newName} replaces it. Set {newName} to the same value instead." +
        Environment.NewLine;

    [TestInitialize]
    public void Init()
    {
        _previousNew = Environment.GetEnvironmentVariable(NewName);
        _previousOld = Environment.GetEnvironmentVariable(OldName);
        Environment.SetEnvironmentVariable(NewName, null);
        Environment.SetEnvironmentVariable(OldName, null);
        // The old name is reported once in a process, so start every test
        // as a process that has not reported it.
        typeof(Constants)
            .GetField(
                "_oldNameReported",
                BindingFlags.NonPublic | BindingFlags.Static)
            ?.SetValue(null, false);
        _previousError = Console.Error;
        _error = new StringWriter();
        Console.SetError(_error);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Console.SetError(_previousError);
        Environment.SetEnvironmentVariable(NewName, _previousNew);
        Environment.SetEnvironmentVariable(OldName, _previousOld);
    }

    /// <summary>
    /// The key as the tests read it. The method is called by name, so that
    /// a class without it fails the test and not the build.
    /// </summary>
    private static string Read()
    {
        var read = typeof(Constants).GetMethod(
            "ResourceKeyFromEnvironment",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(
            read, "Constants has no ResourceKeyFromEnvironment.");
        return (string)read.Invoke(null, null);
    }

    [TestMethod]
    public void OnlyTheOldNameSet_GivesThatValue()
    {
        Environment.SetEnvironmentVariable(OldName, "old-value");

        Assert.AreEqual("old-value", Read());
    }

    [TestMethod]
    public void NewNameSet_WinsOverTheOldName()
    {
        Environment.SetEnvironmentVariable(NewName, "new-value");
        Environment.SetEnvironmentVariable(OldName, "old-value");

        Assert.AreEqual("new-value", Read());
        Assert.AreEqual(string.Empty, _error.ToString());
    }

    [TestMethod]
    public void OldNameRead_IsReportedAsDeprecatedOnce()
    {
        Environment.SetEnvironmentVariable(OldName, "old-value");

        Assert.AreEqual("old-value", Read());
        Assert.AreEqual("old-value", Read());

        Assert.AreEqual(Deprecation(OldName, NewName), _error.ToString());
    }
}
