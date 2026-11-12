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
using System.Linq;
using System.Reflection;

#nullable disable

namespace FiftyOne.IpIntelligence.CloudTests;

/// <summary>
/// Tests that the resource key for the live cloud tests is still read from
/// the old names of its environment variable when no new name is set.
/// The tests change the environment of the whole process, so they run
/// apart from the tests that read it.
/// </summary>
[TestClass]
[DoNotParallelize]
public class LiveCloudKeyOldNamesTests
{
    private static readonly string[] OldNames = { "51D_RESOURCE_KEY", "SUPER_RESOURCE_KEY" };

    private readonly Dictionary<string, string> _previous =
        new Dictionary<string, string>();
    private TextWriter _previousError;
    private StringWriter _error;

    private static string Read() => LiveCloudKey.Require("a product", "a property");

    private static string Deprecation(string oldName) =>
        $"The environment variable {oldName} is deprecated and " +
        $"{LiveCloudKey.Names[0]} replaces it. Set " +
        $"{LiveCloudKey.Names[0]} to the same value instead." +
        Environment.NewLine;

    [TestInitialize]
    public void Init()
    {
        foreach (var name in LiveCloudKey.Names.Concat(OldNames))
        {
            _previous[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, null);
        }
        // Each old name is reported once in a process, so start every
        // test as a process that has reported none.
        var reported = typeof(LiveCloudKey)
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
    public void OnlyAnOldNameSet_GivesThatKey()
    {
        foreach (var oldName in OldNames)
        {
            Environment.SetEnvironmentVariable(oldName, "key-" + oldName);
            Assert.AreEqual("key-" + oldName, Read(), oldName);
            Environment.SetEnvironmentVariable(oldName, null);
        }
    }

    [TestMethod]
    public void NewNameSet_WinsOverAnOldName()
    {
        Environment.SetEnvironmentVariable(LiveCloudKey.Names[0], "new-key");
        Environment.SetEnvironmentVariable(OldNames[0], "old-key");

        Assert.AreEqual("new-key", Read());
        Assert.AreEqual(string.Empty, _error.ToString());
    }

    [TestMethod]
    public void OldNameRead_IsReportedAsDeprecatedOnce()
    {
        Environment.SetEnvironmentVariable(OldNames[0], "old-key");

        Assert.AreEqual("old-key", Read());
        Assert.AreEqual("old-key", Read());

        Assert.AreEqual(Deprecation(OldNames[0]), _error.ToString());
    }
}
