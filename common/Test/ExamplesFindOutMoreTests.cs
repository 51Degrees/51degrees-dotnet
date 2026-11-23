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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FiftyOne.Common.Tests;

/// <summary>
/// Every example ends by saying where to find out more, so that someone
/// who runs one can follow up from its output or its page. A console
/// example writes the section, and a web example has it on its page. The
/// test reads the files git tracks, and fails on any example whose folder
/// has neither the heading nor a call that writes it.
/// </summary>
[TestClass]
public class ExamplesFindOutMoreTests
{
    /// <summary>
    /// The folders whose examples are checked.
    /// </summary>
    private static readonly string[] ExampleRoots =
    {
        "examples/device-detection/",
        "examples/ip-intelligence/",
        "location/Examples/",
        "pipeline/Examples/",
    };

    /// <summary>
    /// The files that make a folder a runnable example, being a program's
    /// entry point or a .NET Framework web application's start.
    /// </summary>
    private static readonly string[] EntryPoints =
    {
        "Program.cs",
        "Global.asax",
    };

    /// <summary>
    /// The kinds of file that can hold the section or the call.
    /// </summary>
    private static readonly string[] SourceExtensions =
    {
        ".cs",
        ".cshtml",
        ".aspx",
        ".html",
    };

    /// <summary>
    /// The section's heading, and the name of the methods that write it.
    /// </summary>
    private static readonly string[] Signs =
    {
        "Find out more",
        "WriteFindOutMore",
    };

    /// <summary>
    /// True when the tracked file is the entry point of an example, which
    /// leaves out the examples' tests.
    /// </summary>
    private static bool IsEntryPoint(string relative) =>
        ExampleRoots.Any(root =>
            relative.StartsWith(root, StringComparison.Ordinal)) &&
        relative.Contains("/Tests/", StringComparison.Ordinal) == false &&
        EntryPoints.Contains(Path.GetFileName(relative));

    /// <summary>
    /// The folder of a tracked file, with forward slashes.
    /// </summary>
    private static string Folder(string relative) =>
        relative.Substring(0, relative.LastIndexOf('/'));

    /// <summary>
    /// True when the text has the section's heading or a call that writes
    /// the section.
    /// </summary>
    private static bool SaysWhereToFindOutMore(string text) =>
        Signs.Any(sign => text.Contains(sign, StringComparison.Ordinal));

    [TestMethod]
    public void TheEntryPointMatcherFindsExamplesAndNotTheirTests()
    {
        Assert.IsTrue(IsEntryPoint(
            "examples/device-detection/OnPremise/GettingStarted-Console/" +
            "Program.cs"));
        Assert.IsTrue(IsEntryPoint(
            "examples/device-detection/OnPremise/Framework-Web/Global.asax"));
        Assert.IsTrue(IsEntryPoint(
            "examples/ip-intelligence/Cloud/Metadata-Console/Program.cs"));
        Assert.IsTrue(IsEntryPoint(
            "pipeline/Examples/ResultCaching/Program.cs"));
        Assert.IsFalse(IsEntryPoint(
            "examples/device-detection/Tests/Example.Tests.Web/Program.cs"));
        Assert.IsFalse(IsEntryPoint(
            "examples/device-detection/ExampleBase/ExampleUtils.cs"));
        Assert.IsFalse(IsEntryPoint("pipeline/Core/Program.cs"));
    }

    [TestMethod]
    public void TheSectionMatcherFindsTheHeadingAndTheCall()
    {
        Assert.IsTrue(SaysWhereToFindOutMore("<h3>Find out more</h3>"));
        Assert.IsTrue(SaysWhereToFindOutMore(
            "ExampleUtils.WriteFindOutMore(Console.Out, ContentSlug);"));
        Assert.IsFalse(SaysWhereToFindOutMore("Console.WriteLine(\"Done\");"));
    }

    [TestMethod]
    public void EveryExampleSaysWhereToFindOutMore()
    {
        var root = Repository.Root();
        var files = Repository.TrackedFiles(root);
        var folders = files
            .Where(IsEntryPoint)
            .Select(Folder)
            .Distinct()
            .OrderBy(folder => folder, StringComparer.Ordinal)
            .ToList();
        Assert.AreNotEqual(0, folders.Count, "No example was found.");

        var missing = new List<string>();
        foreach (var folder in folders)
        {
            var says = files
                .Where(file =>
                    file.StartsWith(folder + "/", StringComparison.Ordinal) &&
                    SourceExtensions.Contains(Path.GetExtension(file)))
                .Select(file => Path.Combine(
                    root, file.Replace('/', Path.DirectorySeparatorChar)))
                .Where(File.Exists)
                .Any(path => SaysWhereToFindOutMore(
                    File.ReadAllText(path, Encoding.UTF8)));
            if (says == false)
            {
                missing.Add(folder);
            }
        }
        Assert.AreEqual(
            0,
            missing.Count,
            $"{missing.Count} of {folders.Count} examples do not end by " +
            "saying where to find out more, so each needs the section on " +
            "its page or a call that writes it:\n" +
            string.Join("\n", missing));
    }
}
