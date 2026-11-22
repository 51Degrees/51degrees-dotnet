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

namespace FiftyOne.DeviceDetection.Cloud.Tests;

/// <summary>
/// The live cloud tests read their resource key from
/// '_51DEGREES_RESOURCE_KEY_BESPOKE' alone, and skip, naming it, when it is
/// not set. The tests change the environment of the whole process, so they
/// run apart from the others.
/// </summary>
[TestClass]
[DoNotParallelize]
public class LiveCloudKeyNameTests
{
    private const string Name = "_51DEGREES_RESOURCE_KEY_BESPOKE";

    /// <summary>
    /// The name a person running an example sets, which these tests do not
    /// read.
    /// </summary>
    private const string ExampleName = "_51DEGREES_RESOURCE_KEY";

    private string _savedName;
    private string _savedExampleName;

    [TestInitialize]
    public void Init()
    {
        _savedName = Environment.GetEnvironmentVariable(Name);
        _savedExampleName = Environment.GetEnvironmentVariable(ExampleName);
        Environment.SetEnvironmentVariable(Name, null);
        Environment.SetEnvironmentVariable(ExampleName, null);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Environment.SetEnvironmentVariable(Name, _savedName);
        Environment.SetEnvironmentVariable(ExampleName, _savedExampleName);
    }

    /// <summary>
    /// The message the test is skipped with, or null when it is not.
    /// </summary>
    private static string SkipMessage()
    {
        try
        {
            LiveCloudKey.Require("device", "a device");
        }
        catch (AssertInconclusiveException ex)
        {
            return ex.Message;
        }
        return null;
    }

    [TestMethod]
    public void TheNameIsRead()
    {
        Environment.SetEnvironmentVariable(Name, "key");
        Assert.AreEqual("key", LiveCloudKey.Require("device", "a device"));
    }

    [TestMethod]
    public void WithoutTheNameTheTestIsSkippedNamingIt()
    {
        var message = SkipMessage();
        Assert.IsNotNull(message, "The test was not skipped.");
        StringAssert.Contains(message, Name);
    }

    [TestMethod]
    public void TheExampleNameIsNotRead()
    {
        Environment.SetEnvironmentVariable(ExampleName, "key");
        Assert.IsNotNull(SkipMessage(),
            $"The test read its key from {ExampleName}.");
    }
}
