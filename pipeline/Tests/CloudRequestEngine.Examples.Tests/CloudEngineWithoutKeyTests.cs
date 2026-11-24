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
using System.Text.RegularExpressions;

namespace FiftyOne.Pipeline.CloudRequestEngine.Examples.Tests;

/// <summary>
/// The cloud engine example, run with no Resource Key set, has no key of
/// its own to fall back on. It ends without an error, names the variable to
/// set and links to where a Resource Key is created. The test changes the
/// environment of the whole process, so it runs apart from the tests that
/// read it.
/// </summary>
[TestClass]
[DoNotParallelize]
public class CloudEngineWithoutKeyTests
{
    private const string ResourceKeyName = "_51DEGREES_RESOURCE_KEY";

    private static readonly Regex CreateKeyLink = new Regex(
        @"https://51degrees\.com/configure(/[A-Za-z0-9]+)?\?" +
        "utm_source=code&utm_medium=example&utm_campaign=51degrees-dotnet");

    private string _previous;

    private TextWriter _previousOut;

    private StringWriter _out;

    [TestInitialize]
    public void Init()
    {
        _previous = Environment.GetEnvironmentVariable(ResourceKeyName);
        Environment.SetEnvironmentVariable(ResourceKeyName, null);
        _previousOut = Console.Out;
        _out = new StringWriter();
        Console.SetOut(_out);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Console.SetOut(_previousOut);
        Environment.SetEnvironmentVariable(ResourceKeyName, _previous);
    }

    [TestMethod]
    public void RunExample_NoKeySet_SaysWhereToCreateOne()
    {
        new global::Examples.CloudEngine.Program().RunExample();

        var output = _out.ToString();
        StringAssert.Contains(output, ResourceKeyName,
            $"The example should name the variable '{ResourceKeyName}'.");
        Assert.IsTrue(CreateKeyLink.IsMatch(output),
            "The example should link to where a Resource Key is created. " +
            $"It wrote: {output}");
    }
}
