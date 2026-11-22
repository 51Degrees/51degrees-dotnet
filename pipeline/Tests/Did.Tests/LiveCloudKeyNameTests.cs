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
using System.Collections.Generic;
using System.Reflection;

namespace FiftyOne.Did.Tests;

/// <summary>
/// The live 51Did tests read each resource key and the 51Did licence key
/// by their own names, and nothing else. Without the resource key a test
/// names they skip, naming the variable, rather than fail. The tests change
/// the environment of the whole process, so they run apart from the others.
/// </summary>
[TestClass]
[DoNotParallelize]
public class LiveCloudKeyNameTests
{
    private const string ResourceKeyName = "_51DEGREES_RESOURCE_KEY_51DID";
    private const string LicenceKeyName = "_51DEGREES_LICENSE_KEY_51DID";

    /// <summary>
    /// Names the tests must not read for these uses. The general resource
    /// key and the data licence belong to other uses.
    /// </summary>
    private static readonly string[] OtherNames =
    {
        "_51DEGREES_RESOURCE_KEY",
        "_51DEGREES_RESOURCE_KEY_BESPOKE",
        "_51DEGREES_LICENSE_KEY",
    };

    private readonly Dictionary<string, string?> _saved =
        new Dictionary<string, string?>();

    [TestInitialize]
    public void Init()
    {
        foreach (var name in AllNames())
        {
            _saved[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var saved in _saved)
        {
            Environment.SetEnvironmentVariable(saved.Key, saved.Value);
        }
    }

    private static IEnumerable<string> AllNames()
    {
        yield return ResourceKeyName;
        yield return LicenceKeyName;
        foreach (var name in OtherNames)
        {
            yield return name;
        }
    }

    /// <summary>
    /// The 51Did resource key as the live tests read it.
    /// </summary>
    private static string Require() =>
        LiveCloudKey.Require(ResourceKeyName, "FODid", "a 51Did");

    /// <summary>
    /// The licence key as the redeem tests read it. The method is called
    /// by name, so a change to it fails this test and not the build.
    /// </summary>
    private static string? LicenceKey()
    {
        var read = typeof(CloudDidClientTests).GetMethod(
            "LicenceKey", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(read, "CloudDidClientTests has no LicenceKey.");
        return (string?)read.Invoke(null, null);
    }

    [TestMethod]
    public void TheResourceKeyIsReadFromItsName()
    {
        Environment.SetEnvironmentVariable(ResourceKeyName, "51did");
        Assert.AreEqual("51did", Require());
    }

    [TestMethod]
    public void TheBespokeKeyIsReadFromItsName()
    {
        Environment.SetEnvironmentVariable(
            "_51DEGREES_RESOURCE_KEY_BESPOKE", "bespoke");
        Assert.AreEqual("bespoke", LiveCloudKey.Require(
            "_51DEGREES_RESOURCE_KEY_BESPOKE", "FODid", "a 51Did"));
    }

    [TestMethod]
    public void NoOtherResourceKeyIsRead()
    {
        foreach (var name in OtherNames)
        {
            Environment.SetEnvironmentVariable(name, "other");
        }
        var message = Skipped(() => Require());
        Assert.IsNotNull(
            message,
            "A resource key under another name was used.");
    }

    [TestMethod]
    public void WithoutTheResourceKeyTheTestIsSkippedNamingIt()
    {
        var message = Skipped(() => Require());
        Assert.IsNotNull(message, "The test was not skipped.");
        StringAssert.Contains(message, ResourceKeyName);
    }

    [TestMethod]
    public void TheLicenceKeyIsReadFromItsName()
    {
        Environment.SetEnvironmentVariable(LicenceKeyName, "51did");
        Assert.AreEqual("51did", LicenceKey());
    }

    [TestMethod]
    public void TheDataLicenceIsNotReadForTheLicenceKey()
    {
        Environment.SetEnvironmentVariable("_51DEGREES_LICENSE_KEY", "data");
        Assert.IsNull(LicenceKey());
    }

    /// <summary>
    /// The message the action skipped with, or null when it returned.
    /// Any other failure is passed on.
    /// </summary>
    private static string? Skipped(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (AssertInconclusiveException ex)
        {
            return ex.Message;
        }
    }
}
