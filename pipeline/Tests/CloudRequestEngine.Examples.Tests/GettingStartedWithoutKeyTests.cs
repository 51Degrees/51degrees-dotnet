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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace FiftyOne.Pipeline.CloudRequestEngine.Examples.Tests;

/// <summary>
/// The getting started example, run with no resource key set, has no key of
/// its own to fall back on. It ends without an error, names the variable to
/// set and links to where a resource key is created. The tests change the
/// environment of the whole process, so they run apart from the tests that
/// read it.
/// </summary>
[TestClass]
[DoNotParallelize]
public class GettingStartedWithoutKeyTests
{
    private const string ResourceKeyName = "_51DEGREES_RESOURCE_KEY";

    /// <summary>
    /// Every variable whose name, with an underscore put in front, holds
    /// this is cleared, so that no resource key of any tier, and no older
    /// name for one, reaches the example.
    /// </summary>
    private const string KeyNamePart = "_RESOURCE_KEY";

    private static readonly Regex CreateKeyLink = new Regex(
        @"https://51degrees\.com/configure(/[A-Za-z0-9]+)?\?" +
        "utm_source=code&utm_medium=example&utm_campaign=51degrees-dotnet");

    private readonly Dictionary<string, string> _previous =
        new Dictionary<string, string>();

    private TextWriter _previousOut;

    private StringWriter _out;

    /// <summary>
    /// One of the example's own methods. The example is a program, so its
    /// methods are private and are found by name.
    /// </summary>
    private static MethodInfo Method(string name)
    {
        var program = Type.GetType("GettingStarted.Program, GettingStarted");
        Assert.IsNotNull(program, "The getting started example was not found.");
        var method = program.GetMethod(
            name, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, $"The example has no {name}.");
        return method;
    }

    [TestInitialize]
    public void Init()
    {
        foreach (DictionaryEntry entry in
            Environment.GetEnvironmentVariables())
        {
            var name = (string)entry.Key;
            if (("_" + name).Contains(
                KeyNamePart, StringComparison.OrdinalIgnoreCase))
            {
                _previous[name] = (string)entry.Value;
                Environment.SetEnvironmentVariable(name, null);
            }
        }
        _previousOut = Console.Out;
        _out = new StringWriter();
        Console.SetOut(_out);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Console.SetOut(_previousOut);
        foreach (var pair in _previous)
        {
            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }
    }

    [TestMethod]
    public void ResourceKey_NoNameSet_GivesNoKey()
    {
        Assert.IsNull(Method("GetResourceKey").Invoke(null, null),
            "With no resource key set, the example has no key to use.");
    }

    [TestMethod]
    public void Main_NoKeySet_SaysWhereToCreateOne()
    {
        Method("Main").Invoke(null, new object[] { Array.Empty<string>() });

        var output = _out.ToString();
        StringAssert.Contains(output, ResourceKeyName,
            $"The example should name the variable '{ResourceKeyName}'.");
        Assert.IsTrue(CreateKeyLink.IsMatch(output),
            "The example should link to where a resource key is created. " +
            $"It wrote: {output}");
    }
}
