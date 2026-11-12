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

using FiftyOne.IpIntelligence.Examples;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

#nullable disable

namespace FiftyOne.IpIntelligence.Example.Tests.Cloud;

/// <summary>
/// Tests that the examples still read the resource key, the data file
/// path and the cloud endpoint from the old names of their environment
/// variables when the new name is not set.
/// The tests change the environment of the whole process, so they run
/// apart from the tests that read it.
/// </summary>
[TestClass]
[DoNotParallelize]
public class OldEnvironmentNamesTests
{
    private const string ResourceKeyName = "_51DEGREES_RESOURCE_KEY";
    private static readonly string[] OldResourceKeyNames =
        { "51DEGREES_RESOURCE_KEY", "RESOURCE_KEY", "SUPER_RESOURCE_KEY" };

    private const string DataFileName = "_51DEGREES_IPI_PATH";
    private static readonly string[] OldDataFileNames =
        { "51DEGREES_IPI_PATH", "IPINTELLIGENCEDATAFILE" };
    private const string EndpointName = "_51DEGREES_CLOUD_ENDPOINT";
    private static readonly string[] OldEndpointNames =
        { "51D_CLOUD_ENDPOINT" };

    /// <summary>
    /// A data file name that is not in the repository, so that only the
    /// environment can supply a path for it.
    /// </summary>
    private const string NoSuchFile = "no-such-file-for-this-test.ipi";

    private static IEnumerable<string> AllNames() =>
        new[] { ResourceKeyName, DataFileName, EndpointName }
            .Concat(OldResourceKeyNames)
            .Concat(OldDataFileNames)
            .Concat(OldEndpointNames);

    /// <summary>
    /// The endpoint as the examples read it. The helper is called by name,
    /// so that a helper without it fails the test and not the build.
    /// </summary>
    private static string Endpoint()
    {
        var oldNames = typeof(ExampleUtils)
            .GetField("OLD_CLOUD_END_POINT_ENV_VARS")
            ?.GetValue(null) as string[];
        Assert.IsNotNull(
            oldNames, "ExampleUtils has no OLD_CLOUD_END_POINT_ENV_VARS.");
        CollectionAssert.AreEqual(OldEndpointNames, oldNames);
        var read = typeof(ExampleUtils).GetMethod(
            "FromEnvironment",
            new[] { typeof(string), typeof(string[]) });
        Assert.IsNotNull(read, "ExampleUtils has no FromEnvironment.");
        return (string)read.Invoke(
            null, new object[] { EndpointName, oldNames });
    }

    private readonly Dictionary<string, string> _previous =
        new Dictionary<string, string>();
    private TextWriter _previousError;
    private StringWriter _error;

    private static string Deprecation(string oldName, string newName) =>
        $"The environment variable {oldName} is deprecated and " +
        $"{newName} replaces it. Set {newName} to the same value instead." +
        Environment.NewLine;

    [TestInitialize]
    public void Init()
    {
        foreach (var name in AllNames())
        {
            _previous[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, null);
        }
        // Each old name is reported once in a process, so start every
        // test as a process that has reported none.
        var reported = typeof(ExampleUtils)
            .GetField(
                "_oldNamesReported",
                BindingFlags.NonPublic | BindingFlags.Static)
            ?.GetValue(null) as HashSet<string>;
        reported?.Clear();
        _previousError = Console.Error;
        _error = new StringWriter();
        Console.SetError(_error);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Console.SetError(_previousError);
        foreach (var pair in _previous)
        {
            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }
    }

    [TestMethod]
    public void ResourceKey_OnlyAnOldNameSet_GivesThatKey()
    {
        foreach (var oldName in OldResourceKeyNames)
        {
            Environment.SetEnvironmentVariable(oldName, "key-" + oldName);
            Assert.AreEqual(
                "key-" + oldName,
                ExampleUtils.GetResourceKeyFromEnv(),
                oldName);
            Environment.SetEnvironmentVariable(oldName, null);
        }
    }

    [TestMethod]
    public void ResourceKey_NewNameSet_WinsOverAnOldName()
    {
        Environment.SetEnvironmentVariable(ResourceKeyName, "new-key");
        Environment.SetEnvironmentVariable(OldResourceKeyNames[0], "old-key");

        Assert.AreEqual("new-key", ExampleUtils.GetResourceKeyFromEnv());
        Assert.AreEqual(string.Empty, _error.ToString());
    }

    [TestMethod]
    public void DataFile_OnlyAnOldNameSet_GivesThatPath()
    {
        foreach (var oldName in OldDataFileNames)
        {
            Environment.SetEnvironmentVariable(oldName, "path-" + oldName);
            Assert.AreEqual(
                "path-" + oldName,
                ExampleUtils.FindDataFile(NoSuchFile),
                oldName);
            Environment.SetEnvironmentVariable(oldName, null);
        }
    }

    [TestMethod]
    public void DataFile_NewNameSet_WinsOverAnOldName()
    {
        Environment.SetEnvironmentVariable(DataFileName, "new-path");
        Environment.SetEnvironmentVariable(OldDataFileNames[0], "old-path");

        Assert.AreEqual("new-path", ExampleUtils.FindDataFile(NoSuchFile));
        Assert.AreEqual(string.Empty, _error.ToString());
    }

    [TestMethod]
    public void Endpoint_OnlyTheOldNameSet_GivesThatEndpoint()
    {
        Environment.SetEnvironmentVariable(OldEndpointNames[0], "old-url");

        Assert.AreEqual("old-url", Endpoint());
        Assert.AreEqual(
            Deprecation(OldEndpointNames[0], EndpointName),
            _error.ToString());
    }

    [TestMethod]
    public void Endpoint_NewNameSet_WinsOverTheOldName()
    {
        Environment.SetEnvironmentVariable(EndpointName, "new-url");
        Environment.SetEnvironmentVariable(OldEndpointNames[0], "old-url");

        Assert.AreEqual("new-url", Endpoint());
        Assert.AreEqual(string.Empty, _error.ToString());
    }

    [TestMethod]
    public void OldNameRead_IsReportedAsDeprecatedOnce()
    {
        Environment.SetEnvironmentVariable(OldResourceKeyNames[0], "old-key");

        Assert.AreEqual("old-key", ExampleUtils.GetResourceKeyFromEnv());
        Assert.AreEqual("old-key", ExampleUtils.GetResourceKeyFromEnv());

        Assert.AreEqual(
            Deprecation(OldResourceKeyNames[0], ResourceKeyName),
            _error.ToString());
    }
}
