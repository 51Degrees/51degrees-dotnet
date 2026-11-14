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
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FiftyOne.Common.Tests;

/// <summary>
/// Doxygen builds each package family's API documentation from the
/// Doxyfile in the family's docs folder, reading only what its INPUT tag
/// lists. A folder missing from INPUT drops its pages from the published
/// documentation without a warning, and a missing example drops the
/// example page that other documentation pages embed. These tests check
/// that every INPUT entry exists, that every file declaring an example
/// page is read, that every packed project is read or is named in
/// <see cref="NotDocumented"/>, and that no doc comment Doxygen reads holds
/// a numeric character reference.
/// </summary>
[TestClass]
public class DoxyfileInputTests
{
    /// <summary>
    /// Folders of packed projects that no Doxyfile reads, so their
    /// packages have no API documentation pages. A folder added to its
    /// Doxyfile's INPUT comes out of this list.
    /// </summary>
    private static readonly HashSet<string> NotDocumented =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "device-detection/PropertyKeyed",
            "device-detection/RobotsTxt",
            "device-detection/RobotsTxt.Cloud",
            "device-detection/Shared",
            "ip-intelligence/Shared",
            "ip-intelligence/Translation",
            "pipeline/Did",
            "pipeline/Did.Cloud",
            "pipeline/Did.Core",
            "pipeline/Engines.TestHelpers",
            "pipeline/Examples.Shared",
            "pipeline/Web.Framework",
        };

    /// <summary>
    /// Folders whose projects the root Directory.Build.props never packs,
    /// unless a project sets IsPackable itself.
    /// </summary>
    private static readonly string[] UnpackedFolders =
    {
        "/Tests/", "/Examples/", "/examples/", "/performance-tests/",
        "/Tools/",
    };

    /// <summary>
    /// Endings of a project name that the root Directory.Build.props never
    /// packs.
    /// </summary>
    private static readonly string[] UnpackedNameEndings =
    {
        ".Test", ".Tests",
    };

    /// <summary>
    /// The first line of a Doxyfile tag, being its name, = or +=, and the
    /// start of its value.
    /// </summary>
    private static readonly Regex TagLine = new Regex(
        @"^(?<tag>[A-Z_]+)\s*(?<op>\+?=)(?<value>.*)$",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// One word of a tag's value, either in double quotes or up to the
    /// next white space.
    /// </summary>
    private static readonly Regex Word = new Regex(
        @"""(?<quoted>[^""]*)""|(?<bare>\S+)",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// A doc comment line holding Doxygen's example command, which makes
    /// the page for the example it names.
    /// </summary>
    private static readonly Regex ExampleCommand = new Regex(
        @"^\s*(?:///|//!|/\*[*!]|\*)\s*[@\\]example\s+\S",
        RegexOptions.CultureInvariant | RegexOptions.Multiline);

    /// <summary>
    /// A C# doc comment line.
    /// </summary>
    private static readonly Regex DocCommentLine = new Regex(
        @"^\s*///",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// An HTML numeric character reference, such as the one for '@'.
    /// </summary>
    private static readonly Regex NumericReference = new Regex(
        @"&#(?:[0-9]+|[xX][0-9A-Fa-f]+);",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// A project's own IsPackable setting.
    /// </summary>
    private static readonly Regex IsPackable = new Regex(
        @"<IsPackable>\s*(?<value>true|false)\s*</IsPackable>",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// A package family from ci/packages.json that has a Doxyfile.
    /// </summary>
    /// <param name="Name">The family's name.</param>
    /// <param name="Folder">The family's folder, relative to the root.</param>
    /// <param name="Doxyfile">The Doxyfile, relative to the root.</param>
    /// <param name="Inputs">The full path of each INPUT entry.</param>
    /// <param name="Recursive">True when RECURSIVE is YES, so Doxygen
    /// reads the folders inside each INPUT folder.</param>
    private sealed record Family(
        string Name,
        string Folder,
        string Doxyfile,
        List<string> Inputs,
        bool Recursive);

    /// <summary>
    /// The full path of a path relative to the root with '/' between
    /// folders.
    /// </summary>
    private static string FullPath(string root, string relative) =>
        Path.GetFullPath(Path.Combine(
            root, relative.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>
    /// Every tag in the Doxyfile with the words of its value, following
    /// lines that end with a backslash and adding the words of a +=.
    /// </summary>
    private static Dictionary<string, List<string>> Tags(string doxyfile)
    {
        var tags = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        List<string> values = null;
        foreach (var line in File.ReadLines(doxyfile))
        {
            var text = line.TrimEnd();
            if (values == null)
            {
                var match = TagLine.Match(text);
                if (match.Success == false)
                {
                    continue;
                }
                var tag = match.Groups["tag"].Value;
                if (match.Groups["op"].Value == "=" ||
                    tags.TryGetValue(tag, out values) == false)
                {
                    values = new List<string>();
                    tags[tag] = values;
                }
                text = match.Groups["value"].Value;
            }
            var continues = text.EndsWith('\\');
            foreach (Match word in Word.Matches(
                continues ? text[..^1] : text))
            {
                values.Add(word.Groups["quoted"].Success
                    ? word.Groups["quoted"].Value
                    : word.Groups["bare"].Value);
            }
            if (continues == false)
            {
                values = null;
            }
        }
        return tags;
    }

    /// <summary>
    /// The words of a tag's value, or none when the Doxyfile lacks it.
    /// </summary>
    private static List<string> Values(
        Dictionary<string, List<string>> tags,
        string tag) =>
        tags.TryGetValue(tag, out var values) ? values : new List<string>();

    /// <summary>
    /// Each family in ci/packages.json whose folder holds docs/Doxyfile,
    /// which is where the documentation build looks for it.
    /// </summary>
    private static List<Family> Families(string root)
    {
        var families = new List<Family>();
        using var json = JsonDocument.Parse(
            File.ReadAllText(FullPath(root, "ci/packages.json")));
        foreach (var family in json.RootElement.EnumerateArray())
        {
            var folder = family.GetProperty("Path").GetString();
            var doxyfile = $"{folder}/docs/Doxyfile";
            var path = FullPath(root, doxyfile);
            if (File.Exists(path) == false)
            {
                continue;
            }
            var docs = Path.GetDirectoryName(path);
            var tags = Tags(path);
            families.Add(new Family(
                family.GetProperty("Name").GetString(),
                folder,
                doxyfile,
                Values(tags, "INPUT")
                    .Select(i => Path.TrimEndingDirectorySeparator(
                        Path.GetFullPath(Path.Combine(docs, i))))
                    .ToList(),
                Values(tags, "RECURSIVE").SequenceEqual(new[] { "YES" })));
        }
        Assert.AreNotEqual(
            0,
            families.Count,
            "No family in ci/packages.json has a docs/Doxyfile.");
        return families;
    }

    /// <summary>
    /// True when Doxygen reads the file or folder at the full path, because
    /// INPUT lists it or a folder holding it.
    /// </summary>
    private static bool IsRead(Family family, string path)
    {
        foreach (var input in family.Inputs)
        {
            if (path == input)
            {
                return true;
            }
            var folder = input + Path.DirectorySeparatorChar;
            if (path.StartsWith(folder, StringComparison.Ordinal) &&
                (family.Recursive ||
                path.IndexOf(Path.DirectorySeparatorChar, folder.Length) < 0))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// True when the project is packed, by its own IsPackable setting or
    /// else by the rule in the root Directory.Build.props.
    /// </summary>
    private static bool IsPacked(string root, string project)
    {
        var own = IsPackable.Match(File.ReadAllText(FullPath(root, project)));
        if (own.Success)
        {
            return bool.Parse(own.Groups["value"].Value);
        }
        var folder = "/" + project[..(project.LastIndexOf('/') + 1)];
        var name = Path.GetFileNameWithoutExtension(project);
        return UnpackedFolders.Any(
                f => folder.Contains(f, StringComparison.Ordinal)) == false &&
            UnpackedNameEndings.Any(
                e => name.EndsWith(e, StringComparison.Ordinal)) == false;
    }

    /// <summary>
    /// The folder of each packed project in the family, relative to the
    /// root.
    /// </summary>
    private static IEnumerable<string> PackedProjectFolders(
        string root,
        Family family,
        List<string> tracked) =>
        tracked
            .Where(p => p.StartsWith(family.Folder + "/", StringComparison.Ordinal))
            .Where(p => p.EndsWith(".csproj", StringComparison.Ordinal))
            .Where(p => IsPacked(root, p))
            .Select(p => p[..p.LastIndexOf('/')])
            .Distinct();

    /// <summary>
    /// Each C# file in the family's folder or in examples/ under the
    /// family's name that declares an example page.
    /// </summary>
    private static IEnumerable<string> ExampleFiles(
        string root,
        Family family,
        List<string> tracked) =>
        tracked
            .Where(p => p.EndsWith(".cs", StringComparison.Ordinal))
            .Where(p =>
                p.StartsWith(family.Folder + "/", StringComparison.Ordinal) ||
                p.StartsWith(
                    $"examples/{family.Name}/",
                    StringComparison.Ordinal))
            .Where(p => File.Exists(FullPath(root, p)))
            .Where(p => ExampleCommand.IsMatch(
                File.ReadAllText(FullPath(root, p))));

    /// <summary>
    /// Fails with every problem found, one to a line.
    /// </summary>
    private static void AssertNone(List<string> problems, string what) =>
        Assert.AreEqual(
            0,
            problems.Count,
            $"{what}{Environment.NewLine}" +
            string.Join(Environment.NewLine, problems));

    [TestMethod]
    public void Doxyfile_EveryInputExists()
    {
        var root = Repository.Root();
        var problems = new List<string>();
        foreach (var family in Families(root))
        {
            problems.AddRange(family.Inputs
                .Where(i => File.Exists(i) == false &&
                    Directory.Exists(i) == false)
                .Select(i => $"{family.Doxyfile}: {i}"));
        }
        AssertNone(problems, "INPUT lists a path that is not there.");
    }

    [TestMethod]
    public void Doxyfile_ReadsEveryExample()
    {
        var root = Repository.Root();
        var tracked = Repository.TrackedFiles(root);
        var problems = new List<string>();
        foreach (var family in Families(root))
        {
            problems.AddRange(ExampleFiles(root, family, tracked)
                .Where(p => IsRead(family, FullPath(root, p)) == false)
                .Select(p => $"{family.Doxyfile}: {p}"));
        }
        AssertNone(
            problems,
            "These files declare example pages, and their Doxyfile does " +
            "not read them, so the pages are not made.");
    }

    [TestMethod]
    public void Doxyfile_ReadsEveryPackedProject()
    {
        var root = Repository.Root();
        var tracked = Repository.TrackedFiles(root);
        var problems = new List<string>();
        foreach (var family in Families(root))
        {
            problems.AddRange(PackedProjectFolders(root, family, tracked)
                .Where(f => NotDocumented.Contains(f) == false)
                .Where(f => IsRead(family, FullPath(root, f)) == false)
                .Select(f => $"{family.Doxyfile}: {f}"));
        }
        AssertNone(
            problems,
            "These folders hold packed projects, and their Doxyfile does " +
            "not read them, so their packages have no API documentation.");
    }

    /// <summary>
    /// Doxygen writes a numeric character reference in a doc comment out
    /// as the text of the reference, so the published page shows it as
    /// typed rather than as the character it stands for.
    /// </summary>
    [TestMethod]
    public void Doxyfile_ReadsNoNumericCharacterReference()
    {
        var root = Repository.Root();
        var tracked = Repository.TrackedFiles(root);
        var families = Families(root);
        var problems = new List<string>();
        foreach (var file in tracked.Where(
            p => p.EndsWith(".cs", StringComparison.Ordinal)))
        {
            var path = FullPath(root, file);
            if (File.Exists(path) == false ||
                families.Any(f => IsRead(f, path)) == false)
            {
                continue;
            }
            var number = 0;
            foreach (var line in File.ReadLines(path))
            {
                number++;
                if (DocCommentLine.IsMatch(line) &&
                    NumericReference.IsMatch(line))
                {
                    problems.Add($"{file}:{number}: {line.Trim()}");
                }
            }
        }
        AssertNone(
            problems,
            "These doc comments hold a numeric character reference, which " +
            "the published page shows as typed. Write the character, and " +
            "where Doxygen would read it as a command, give it an alias in " +
            "the Doxyfile.");
    }

    [TestMethod]
    public void Doxyfile_NotDocumentedIsCurrent()
    {
        var root = Repository.Root();
        var tracked = Repository.TrackedFiles(root);
        var packed = new Dictionary<string, Family>(StringComparer.Ordinal);
        foreach (var family in Families(root))
        {
            foreach (var folder in PackedProjectFolders(root, family, tracked))
            {
                packed[folder] = family;
            }
        }
        var problems = new List<string>();
        foreach (var folder in NotDocumented.OrderBy(
            f => f,
            StringComparer.Ordinal))
        {
            if (packed.TryGetValue(folder, out var family) == false)
            {
                problems.Add($"{folder} holds no packed project.");
            }
            else if (IsRead(family, FullPath(root, folder)))
            {
                problems.Add($"{family.Doxyfile} reads {folder}.");
            }
        }
        AssertNone(
            problems,
            "Take these folders out of NotDocumented.");
    }
}
