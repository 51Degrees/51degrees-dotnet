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

namespace FiftyOne.Did.Cloud.Tests;

/// <summary>
/// Each row of the live cloud test reads its resource key from the one
/// variable it names, and skips, naming that variable, when it is not set.
/// '_51DEGREES_RESOURCE_KEY', the name a person running an example sets,
/// is never read. The tests change the environment of the whole process,
/// so they run apart from the others.
/// </summary>
[TestClass]
[DoNotParallelize]
public class LiveCloudKeyNameTests
{
    private const string ExampleName = "_51DEGREES_RESOURCE_KEY";

    private string? _savedName;
    private string? _savedExampleName;
    private string _name = null!;

    private void Clear(string name)
    {
        _name = name;
        _savedName = Environment.GetEnvironmentVariable(name);
        _savedExampleName = Environment.GetEnvironmentVariable(ExampleName);
        Environment.SetEnvironmentVariable(name, null);
        Environment.SetEnvironmentVariable(ExampleName, null);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (_name != null)
        {
            Environment.SetEnvironmentVariable(_name, _savedName);
            Environment.SetEnvironmentVariable(ExampleName, _savedExampleName);
        }
    }

    /// <summary>
    /// The message the read skipped with, or null when it returned.
    /// </summary>
    private static string? Skipped(string name)
    {
        try
        {
            LiveCloudKey.Require(name, "a 51Did");
            return null;
        }
        catch (AssertInconclusiveException ex)
        {
            return ex.Message;
        }
    }

    [TestMethod]
    [DataRow("_51DEGREES_RESOURCE_KEY_BASIC")]
    [DataRow("_51DEGREES_RESOURCE_KEY_BESPOKE")]
    [DataRow("_51DEGREES_RESOURCE_KEY_51DID")]
    public void TheKeyIsReadFromItsName(string name)
    {
        Clear(name);
        Environment.SetEnvironmentVariable(name, "key");
        Assert.AreEqual("key", LiveCloudKey.Require(name, "a 51Did"));
    }

    [TestMethod]
    [DataRow("_51DEGREES_RESOURCE_KEY_BASIC")]
    [DataRow("_51DEGREES_RESOURCE_KEY_BESPOKE")]
    [DataRow("_51DEGREES_RESOURCE_KEY_51DID")]
    public void WithoutItsNameTheRowIsSkippedNamingIt(string name)
    {
        Clear(name);
        var message = Skipped(name);
        Assert.IsNotNull(message, "The test was not skipped.");
        StringAssert.Contains(message, name);
    }

    [TestMethod]
    [DataRow("_51DEGREES_RESOURCE_KEY_BASIC")]
    [DataRow("_51DEGREES_RESOURCE_KEY_BESPOKE")]
    [DataRow("_51DEGREES_RESOURCE_KEY_51DID")]
    public void TheExampleNameIsNotRead(string name)
    {
        Clear(name);
        Environment.SetEnvironmentVariable(ExampleName, "key");
        Assert.IsNotNull(Skipped(name),
            $"The row read its key from {ExampleName}.");
    }
}
