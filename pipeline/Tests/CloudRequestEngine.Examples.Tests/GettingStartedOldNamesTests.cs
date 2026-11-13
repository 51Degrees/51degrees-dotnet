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
using System.IO;
using System.Reflection;

namespace FiftyOne.Pipeline.CloudRequestEngine.Examples.Tests;

/// <summary>
/// Tests that the getting started example still reads the cloud endpoint
/// and the resource key from the old names of their environment variables
/// when the new name is not set.
/// The tests change the environment of the whole process, so they run
/// apart from the tests that read it.
/// </summary>
[TestClass]
[DoNotParallelize]
public class GettingStartedOldNamesTests
{
    private const string EndpointName = "_51DEGREES_CLOUD_ENDPOINT";
    private const string OldEndpointName = "FOD_CLOUD_API_URL";
    private const string ResourceKeyName = "_51DEGREES_RESOURCE_KEY";
    private const string OldResourceKeyName = "RESOURCE_KEY";

    private static readonly string[] Names =
    {
        EndpointName, OldEndpointName, ResourceKeyName, OldResourceKeyName,
    };

    private readonly Dictionary<string, string> _previous =
        new Dictionary<string, string>();
    private TextWriter _previousError;
    private StringWriter _error;

    private static string Deprecation(string oldName, string newName) =>
        $"The environment variable {oldName} is deprecated and " +
        $"{newName} replaces it. Set {newName} to the same value instead." +
        Environment.NewLine;

    /// <summary>
    /// What one of the example's own readers gives. The example is a
    /// program, so its readers are private and are called by name.
    /// </summary>
    private static string Read(string reader)
    {
        var program = Type.GetType("GettingStarted.Program, GettingStarted");
        Assert.IsNotNull(program, "The getting started example was not found.");
        var method = program.GetMethod(
            reader, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, $"The example has no {reader}.");
        return (string)method.Invoke(null, null);
    }

    [TestInitialize]
    public void Init()
    {
        foreach (var name in Names)
        {
            _previous[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, null);
        }
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
    public void Endpoint_OnlyTheOldNameSet_GivesThatEndpoint()
    {
        Environment.SetEnvironmentVariable(
            OldEndpointName, "https://old.example/api/v4");

        Assert.AreEqual(
            "https://old.example/api/v4/", Read("GetCloudEndpoint"));
        Assert.AreEqual(
            Deprecation(OldEndpointName, EndpointName), _error.ToString());
    }

    [TestMethod]
    public void Endpoint_NewNameSet_WinsOverTheOldName()
    {
        Environment.SetEnvironmentVariable(
            EndpointName, "https://new.example/api/v4/");
        Environment.SetEnvironmentVariable(
            OldEndpointName, "https://old.example/api/v4/");

        Assert.AreEqual(
            "https://new.example/api/v4/", Read("GetCloudEndpoint"));
        Assert.AreEqual(string.Empty, _error.ToString());
    }

    [TestMethod]
    public void ResourceKey_OnlyTheOldNameSet_GivesThatKey()
    {
        Environment.SetEnvironmentVariable(OldResourceKeyName, "old-key");

        Assert.AreEqual("old-key", Read("GetResourceKey"));
        Assert.AreEqual(
            Deprecation(OldResourceKeyName, ResourceKeyName),
            _error.ToString());
    }

    [TestMethod]
    public void ResourceKey_NewNameSet_WinsOverTheOldName()
    {
        Environment.SetEnvironmentVariable(ResourceKeyName, "new-key");
        Environment.SetEnvironmentVariable(OldResourceKeyName, "old-key");

        Assert.AreEqual("new-key", Read("GetResourceKey"));
        Assert.AreEqual(string.Empty, _error.ToString());
    }
}
