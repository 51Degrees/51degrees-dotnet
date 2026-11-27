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
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FiftyOne.Common.Tests;

/// <summary>
/// Every environment variable name this repository reads or sets is in
/// environment-variables.md at the root, apart from the version property of
/// each package, which ci/packages.json names. A name a customer uses starts
/// with an underscore and _51DEGREES_, because a POSIX shell cannot set a
/// name that starts with a digit. A secret or variable that only CI uses has
/// no prefix and is listed in the table of secrets and variables only CI
/// reads. Any other name is one that another tool defines, listed in the last
/// table of that file. A workflow reads only _51DEGREES_ secrets and
/// variables and the secrets and variables only CI reads, and no _51DEGREES_
/// name is filled from another. The tests read
/// every file git tracks, which leaves out build output and the submodules.
/// </summary>
[TestClass]
public class EnvironmentVariableNamesTests
{
    /// <summary>
    /// The start of every name 51Degrees defines.
    /// </summary>
    private const string Prefix = "_51DEGREES_";

    /// <summary>
    /// The start of a name that a shell cannot set, joined at run time so
    /// this file does not match itself.
    /// </summary>
    private const string DigitInitial = "5" + "1DEGREES_";

    /// <summary>
    /// The file that lists the names, relative to the repository root.
    /// </summary>
    private const string NamesFile = "environment-variables.md";

    /// <summary>
    /// The file that names each package's version property, relative to the
    /// repository root.
    /// </summary>
    private const string PackagesFile = "ci/packages.json";

    /// <summary>
    /// The heading in <see cref="NamesFile"/> of the table of names other
    /// tools define.
    /// </summary>
    private const string OtherToolsHeading = "## Names other tools define";

    /// <summary>
    /// The heading in <see cref="NamesFile"/> of the table of secrets and
    /// variables that only CI uses, which have no prefix.
    /// </summary>
    private const string CiSecretsHeading =
        "## Secrets and variables only CI reads";

    /// <summary>
    /// This file, whose tests use made up names.
    /// </summary>
    private const string ThisFile =
        "common/Test/EnvironmentVariableNamesTests.cs";

    /// <summary>
    /// The secret GitHub gives every workflow, which it may name.
    /// </summary>
    private const string GitHubToken = "GITHUB_TOKEN";

    /// <summary>
    /// The extensions of the files that hold code or build settings, where
    /// a name is read or set. Documentation may name anything.
    /// </summary>
    private static readonly HashSet<string> CodeExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".cshtml", ".aspx", ".asax", ".config", ".ps1", ".psm1",
            ".sh", ".yml", ".yaml", ".json", ".props", ".targets",
            ".csproj", ".cmake", ".nuspec",
        };

    /// <summary>
    /// The ways code reads or sets a variable by a name written in it, with
    /// the name in the group 'name'.
    /// </summary>
    private static readonly Regex[] NameReads =
    {
        // C# and PowerShell calls of the .NET methods.
        new Regex(
            @"(?:Get|Set)EnvironmentVariable\(\s*[""'](?<name>[A-Za-z_][A-Za-z0-9_]*)[""']",
            RegexOptions.CultureInvariant),
        // A child process's environment in C#.
        new Regex(
            @"Environment(?:Variables)?\s*(?:\[\s*|\.Remove\(\s*|\.Add\(\s*)""(?<name>[A-Za-z_][A-Za-z0-9_]*)""",
            RegexOptions.CultureInvariant),
        // A C# constant whose name says it holds a variable's name.
        new Regex(
            @"const\s+string\s+\w*(?:ENV_VAR|EnvVar|Variable|VARIABLE)\w*\s*=\s*""(?<name>[A-Za-z_][A-Za-z0-9_]*)""",
            RegexOptions.CultureInvariant),
        // PowerShell, as $env:NAME and ${env:NAME}.
        new Regex(
            @"(?<![A-Za-z0-9_])\$\{?env:(?<name>[A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase),
        // PowerShell's environment drive, as in Remove-Item Env:NAME.
        new Regex(
            @"(?<![A-Za-z0-9_${])Env:(?<name>[A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase),
        // A workflow expression.
        new Regex(
            @"\$\{\{[^}]*?(?<![A-Za-z0-9_.])env\.(?<name>[A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.CultureInvariant),
        // CMake.
        new Regex(
            @"\$ENV\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\}",
            RegexOptions.CultureInvariant),
    };

    /// <summary>
    /// A _51DEGREES_ name anywhere in a line.
    /// </summary>
    private static readonly Regex PrefixedName = new Regex(
        @"(?<![A-Za-z0-9_])_51DEGREES_[A-Za-z0-9_]*[A-Za-z0-9]",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// A secret or variable a workflow reads.
    /// </summary>
    private static readonly Regex WorkflowName = new Regex(
        @"(?<![A-Za-z0-9_])(?:secrets|vars)\.(?<name>[A-Za-z0-9_]+)",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// A resource key a workflow reads as an organization variable. Every
    /// resource key is an organization secret.
    /// </summary>
    private static readonly Regex ResourceKeyVariable = new Regex(
        @"(?<![A-Za-z0-9_])vars\.(?<name>_51DEGREES_RESOURCE_KEY[A-Za-z0-9_]*)",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// A _51DEGREES_ name set from a workflow secret or variable, or from
    /// another variable in PowerShell, with the source in 'from'.
    /// </summary>
    private static readonly Regex[] Fills =
    {
        new Regex(
            @"(?<![A-Za-z0-9_])(?<name>_51DEGREES_[A-Za-z0-9_]+)\s*:\s*\$\{\{\s*(?:secrets|vars)\.(?<from>[A-Za-z0-9_]+)\s*\}\}",
            RegexOptions.CultureInvariant),
        new Regex(
            @"\$env:(?<name>_51DEGREES_[A-Za-z0-9_]+)\s*=\s*\$env:(?<from>[A-Za-z0-9_]+)",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase),
    };

    /// <summary>
    /// The key of a line in a workflow's env block.
    /// </summary>
    private static readonly Regex EnvKey = new Regex(
        @"^(?<indent>\s*)(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// A line that opens a workflow's env block.
    /// </summary>
    private static readonly Regex EnvBlock = new Regex(
        @"^(?<indent>\s*)env:\s*$",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// A name in the first column of a table row of
    /// <see cref="NamesFile"/>.
    /// </summary>
    private static readonly Regex ListedName = new Regex(
        @"^\|\s*`(?<name>[A-Za-z_][A-Za-z0-9_]*)`\s*\|",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// True when the character can be part of an environment variable name.
    /// </summary>
    private static bool IsNameCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || character == '_';

    /// <summary>
    /// Each name in the line that starts with <see cref="DigitInitial"/> and
    /// is not the end of a longer name, such as one with a leading
    /// underscore. The name is matched in any case, because Windows reads an
    /// environment variable name in any case.
    /// </summary>
    private static List<string> DigitInitialNames(string line)
    {
        var names = new List<string>();
        var at = 0;
        while (at < line.Length)
        {
            if (at + DigitInitial.Length <= line.Length &&
                string.Compare(
                    line,
                    at,
                    DigitInitial,
                    0,
                    DigitInitial.Length,
                    StringComparison.OrdinalIgnoreCase) == 0 &&
                (at == 0 || !IsNameCharacter(line[at - 1])))
            {
                var end = at;
                while (end < line.Length && IsNameCharacter(line[end]))
                {
                    end++;
                }
                names.Add(line.Substring(at, end - at));
                at = end;
            }
            else
            {
                at++;
            }
        }
        return names;
    }

    /// <summary>
    /// The names read or set by name in the line.
    /// </summary>
    private static List<string> NamesRead(string line)
    {
        var names = new List<string>();
        foreach (var read in NameReads)
        {
            foreach (Match match in read.Matches(line))
            {
                names.Add(match.Groups["name"].Value);
            }
        }
        return names;
    }

    /// <summary>
    /// The keys of the env blocks of a workflow, one entry for each line
    /// that holds one, with its index.
    /// </summary>
    private static List<(int Index, string Name)> EnvBlockKeys(
        IReadOnlyList<string> lines)
    {
        var keys = new List<(int, string)>();
        int? blockIndent = null;
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index].TrimEnd('\r');
            if (blockIndent != null)
            {
                var key = EnvKey.Match(line);
                if (key.Success &&
                    key.Groups["indent"].Value.Length > blockIndent)
                {
                    keys.Add((index, key.Groups["name"].Value));
                    continue;
                }
                if (line.Trim().Length > 0 &&
                    line.Length - line.TrimStart().Length <= blockIndent)
                {
                    blockIndent = null;
                }
            }
            var block = EnvBlock.Match(line);
            if (block.Success)
            {
                blockIndent = block.Groups["indent"].Value.Length;
            }
        }
        return keys;
    }

    /// <summary>
    /// The secrets and variables a workflow line reads that it may not, being
    /// any without the prefix, apart from GitHub's own token and the secrets
    /// and variables only CI reads.
    /// </summary>
    private static List<string> OtherWorkflowNames(
        string line, ISet<string> ciSecrets)
    {
        var names = new List<string>();
        foreach (Match match in WorkflowName.Matches(line))
        {
            var name = match.Groups["name"].Value;
            if (!name.StartsWith(Prefix, StringComparison.Ordinal) &&
                name != GitHubToken && !ciSecrets.Contains(name))
            {
                names.Add(match.Value);
            }
        }
        return names;
    }

    /// <summary>
    /// Each place in the line where a _51DEGREES_ name is set from a source
    /// of another name.
    /// </summary>
    private static List<string> FillsFromAnotherName(string line)
    {
        var fills = new List<string>();
        foreach (var fill in Fills)
        {
            foreach (Match match in fill.Matches(line))
            {
                if (!string.Equals(
                    match.Groups["name"].Value,
                    match.Groups["from"].Value,
                    StringComparison.OrdinalIgnoreCase))
                {
                    fills.Add(match.Value);
                }
            }
        }
        return fills;
    }

    /// <summary>
    /// The names <see cref="NamesFile"/> lists, as the names customers use,
    /// the secrets and variables only CI reads and the names other tools
    /// define.
    /// </summary>
    private static (HashSet<string> Ours, HashSet<string> Ci, HashSet<string> OtherTools)
        ListedNames(IEnumerable<string> lines)
    {
        var ours = new HashSet<string>(StringComparer.Ordinal);
        var ci = new HashSet<string>(StringComparer.Ordinal);
        var other = new HashSet<string>(StringComparer.Ordinal);
        var table = ours;
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                table = line == OtherToolsHeading ? other
                    : line == CiSecretsHeading ? ci
                    : ours;
            }
            var listed = ListedName.Match(line);
            if (listed.Success)
            {
                table.Add(listed.Groups["name"].Value);
            }
        }
        return (ours, ci, other);
    }

    /// <summary>
    /// A tracked file, as its path relative to the root and its lines.
    /// </summary>
    private sealed record TrackedFile(string Relative, string[] Lines)
    {
        public bool IsCode =>
            CodeExtensions.Contains(Path.GetExtension(Relative)) ||
            Path.GetFileName(Relative) == "CMakeLists.txt";

        public bool IsWorkflow =>
            Relative.StartsWith(".github/", StringComparison.Ordinal) &&
            (Relative.EndsWith(".yml", StringComparison.Ordinal) ||
             Relative.EndsWith(".yaml", StringComparison.Ordinal));
    }

    /// <summary>
    /// Every file git tracks that is a file of this repository. A submodule
    /// is tracked as a folder, and its files belong to another repository.
    /// </summary>
    private static List<TrackedFile> TrackedFiles()
    {
        var root = Repository.Root();
        var files = new List<TrackedFile>();
        foreach (var relative in Repository.TrackedFiles(root))
        {
            var path = Path.Combine(
                root, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
            {
                files.Add(new TrackedFile(
                    relative,
                    File.ReadAllText(path, Encoding.UTF8).Split('\n')));
            }
        }
        return files;
    }

    private static readonly Lazy<List<TrackedFile>> Files =
        new Lazy<List<TrackedFile>>(TrackedFiles);

    private static readonly Lazy<(HashSet<string> Ours, HashSet<string> Ci, HashSet<string> OtherTools)>
        Listed = new Lazy<(HashSet<string>, HashSet<string>, HashSet<string>)>(() =>
            ListedNames(File.ReadAllLines(
                Path.Combine(Repository.Root(), NamesFile))));

    /// <summary>
    /// The version property of each package in <see cref="PackagesFile"/>.
    /// </summary>
    private static readonly Lazy<HashSet<string>> VersionProperties =
        new Lazy<HashSet<string>>(() =>
        {
            using var packages = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(Repository.Root(), PackagesFile)));
            return new HashSet<string>(
                packages.RootElement.EnumerateArray()
                    .Select(p => p.GetProperty("Property").GetString()),
                StringComparer.Ordinal);
        });

    /// <summary>
    /// Each name the code reads or sets, with where.
    /// </summary>
    private static List<(string Where, string Name)> CodeNames()
    {
        var names = new List<(string, string)>();
        foreach (var file in Files.Value.Where(f =>
            f.IsCode && f.Relative != ThisFile))
        {
            for (var index = 0; index < file.Lines.Length; index++)
            {
                foreach (var name in NamesRead(file.Lines[index]))
                {
                    names.Add(($"{file.Relative}:{index + 1}", name));
                }
                foreach (Match match in PrefixedName.Matches(file.Lines[index]))
                {
                    names.Add(($"{file.Relative}:{index + 1}", match.Value));
                }
            }
            if (file.IsWorkflow)
            {
                foreach (var (index, name) in EnvBlockKeys(file.Lines))
                {
                    names.Add(($"{file.Relative}:{index + 1}", name));
                }
            }
        }
        return names;
    }

    [TestMethod]
    public void TheReadMatcherFindsANameInEachLanguage()
    {
        CollectionAssert.AreEquivalent(
            new[]
            {
                "MADE_UP_ONE", "MADE_UP_TWO", "MADE_UP_THREE",
                "MADE_UP_FOUR", "MADE_UP_FIVE", "MADE_UP_SIX",
                "MADE_UP_SEVEN", "MADE_UP_EIGHT", "MADE_UP_NINE",
            },
            NamesRead(
                "Environment.GetEnvironmentVariable(\"MADE_UP_ONE\") " +
                "[Environment]::SetEnvironmentVariable('MADE_UP_TWO', 1) " +
                "start.Environment[\"MADE_UP_THREE\"] = key; " +
                "const string KeyVariable = \"MADE_UP_FOUR\"; " +
                "$env:MADE_UP_FIVE = 1; ${env:MADE_UP_SIX} " +
                "Remove-Item Env:MADE_UP_SEVEN " +
                "${{ env.MADE_UP_EIGHT }} $ENV{MADE_UP_NINE}"));
        CollectionAssert.AreEqual(
            Array.Empty<string>(),
            NamesRead(
                "const string Header = \"MADE_UP\"; env: " +
                "\"Env:$variable\" GetEnvironmentVariable(name)"));
    }

    [TestMethod]
    public void TheEnvBlockKeysOfAWorkflowAreFound()
    {
        var keys = EnvBlockKeys(new[]
        {
            "    steps:",
            "      - name: A step",
            "        env:",
            "          MADE_UP_ONE: ${{ secrets.MADE_UP_ONE }}",
            "          MADE_UP_TWO: value",
            "        run: |",
            "          echo done",
            "        with:",
            "          not-a-name: value",
        });
        CollectionAssert.AreEqual(
            new[] { "MADE_UP_ONE", "MADE_UP_TWO" },
            keys.Select(k => k.Name).ToArray());
    }

    [TestMethod]
    public void TheFillMatcherFindsOnlyANameSetFromAnother()
    {
        var made = Prefix + "MADE_UP";
        var other = Prefix + "OTHER";
        CollectionAssert.AreEqual(
            new[]
            {
                $"{made}: ${{{{ secrets.{other} }}}}",
                $"{made}: ${{{{ vars.MADE_UP }}}}",
                $"$env:{made} = $env:{other}",
            },
            FillsFromAnotherName(
                $"{made}: ${{{{ secrets.{other} }}}} " +
                $"{made}: ${{{{ vars.MADE_UP }}}} " +
                $"$env:{made} = $env:{other}"));
        CollectionAssert.AreEqual(
            Array.Empty<string>(),
            FillsFromAnotherName(
                $"{made}: ${{{{ secrets.{made} }}}} " +
                $"{made}: ${{{{ vars.{made} }}}} " +
                $"{made}: ${{{{ steps.login.outputs.KEY }}}} " +
                $"TOOL_TOKEN: ${{{{ secrets.{made} }}}}"));
    }

    [TestMethod]
    public void TheWorkflowNameMatcherFindsOnlyNamesWithoutThePrefix()
    {
        CollectionAssert.AreEqual(
            new[] { "secrets.MADE_UP", "vars.MADE_UP" },
            OtherWorkflowNames(
                "${{ secrets.MADE_UP }} ${{ vars.MADE_UP }} " +
                $"${{{{ secrets.{Prefix}MADE_UP }}}} " +
                $"${{{{ vars.{Prefix}MADE_UP }}}} " +
                "${{ secrets.MADE_UP_CI_SECRET }} " +
                "${{ secrets.GITHUB_TOKEN }} secrets: inherit",
                new HashSet<string> { "MADE_UP_CI_SECRET" }));
    }

    /// <summary>
    /// The staging cloud and its key are names only CI uses. No customer
    /// sets them, so they have no prefix, and a workflow may read them. A
    /// name that only looks like one of them is still refused.
    /// </summary>
    [TestMethod]
    public void TheStagingNamesAreOnlyCiNamesAndLookAlikesAreRefused()
    {
        var ci = Listed.Value.Ci;
        var staging = new[]
        {
            "CLOUD_STAGING_ENDPOINT", "RESOURCE_KEY_BESPOKE_STAGING",
        };
        foreach (var name in staging)
        {
            Assert.Contains(name, ci,
                $"{name} should be listed under {CiSecretsHeading}.");
        }
        CollectionAssert.AreEqual(
            Array.Empty<string>(),
            OtherWorkflowNames(
                "${{ vars.CLOUD_STAGING_ENDPOINT }} " +
                "${{ secrets.RESOURCE_KEY_BESPOKE_STAGING }}",
                ci));
        CollectionAssert.AreEqual(
            new[]
            {
                "vars.CLOUD_STAGING_ENDPOINTS",
                "secrets.RESOURCE_KEY_BESPOKE_STAGING_2",
                "vars.STAGING_ENDPOINT",
            },
            OtherWorkflowNames(
                "${{ vars.CLOUD_STAGING_ENDPOINTS }} " +
                "${{ secrets.RESOURCE_KEY_BESPOKE_STAGING_2 }} " +
                "${{ vars.STAGING_ENDPOINT }}",
                ci));
    }

    [TestMethod]
    public void TheNamesFileListsEachKind()
    {
        var (ours, ci, other) = ListedNames(new[]
        {
            "## Keys",
            $"| `{Prefix}MADE_UP` | A key | Tests |",
            "| Name | What it holds | Read by |",
            CiSecretsHeading,
            "| `MADE_UP_CI_SECRET` | A token | CI |",
            OtherToolsHeading,
            "| `MADE_UP` | A tool |",
        });
        CollectionAssert.AreEqual(new[] { Prefix + "MADE_UP" }, ours.ToArray());
        CollectionAssert.AreEqual(new[] { "MADE_UP_CI_SECRET" }, ci.ToArray());
        CollectionAssert.AreEqual(new[] { "MADE_UP" }, other.ToArray());

        var (listedOurs, listedCi, listedOther) = Listed.Value;
        Assert.IsTrue(listedOurs.Count > 0, "No 51Degrees name is listed.");
        Assert.IsTrue(listedCi.Count > 0,
            "No secret or variable only CI reads is listed.");
        Assert.IsTrue(listedOther.Count > 0, "No other tool's name is listed.");
        var wrongTable = listedOurs
            .Where(n => !n.StartsWith(Prefix, StringComparison.Ordinal))
            .Concat(listedCi.Concat(listedOther)
                .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal)))
            .ToList();
        Assert.AreEqual(
            0,
            wrongTable.Count,
            $"These are in the wrong table of {NamesFile}: " +
            string.Join(", ", wrongTable));
    }

    [TestMethod]
    public void NoFileNamesAVariableThatStartsWithADigit()
    {
        var found = new List<string>();
        foreach (var file in Files.Value)
        {
            for (var index = 0; index < file.Lines.Length; index++)
            {
                foreach (var name in DigitInitialNames(file.Lines[index]))
                {
                    found.Add($"{file.Relative}:{index + 1}: {name}");
                }
            }
        }
        Assert.AreEqual(
            0,
            found.Count,
            $"{found.Count} names start with a digit, which a POSIX shell " +
            "cannot set, so each must start with an underscore:\n" +
            string.Join("\n", found));
    }

    [TestMethod]
    public void EveryNameTheCodeReadsIsListed()
    {
        var (ours, ci, other) = Listed.Value;
        var versions = VersionProperties.Value;
        var found = CodeNames()
            .Where(n => n.Name.StartsWith(Prefix, StringComparison.Ordinal)
                ? !ours.Contains(n.Name) && !versions.Contains(n.Name)
                : !ci.Contains(n.Name) && !other.Contains(n.Name))
            .Select(n => $"{n.Where}: {n.Name}")
            .ToList();
        Assert.AreEqual(
            0,
            found.Count,
            $"{found.Count} names are read or set that {NamesFile} does " +
            $"not list. A name 51Degrees defines starts {Prefix} and is " +
            "listed with what it holds, a secret or variable only CI reads " +
            "is listed without the prefix, and another tool's name is " +
            "listed in its " +
            "last table:\n" + string.Join("\n", found));
    }

    [TestMethod]
    public void EveryListedNameIsUsed()
    {
        var (ours, ci, other) = Listed.Value;
        var used = new HashSet<string>(
            CodeNames().Select(n => n.Name), StringComparer.Ordinal);
        var unused = ours.Concat(ci).Concat(other)
            .Where(n => !used.Contains(n)).ToList();
        Assert.AreEqual(
            0,
            unused.Count,
            $"{NamesFile} lists names nothing reads or sets, so each must " +
            "go from the list: " + string.Join(", ", unused));
    }

    [TestMethod]
    public void EveryWorkflowReadsOnlyPrefixedSecretsAndVariablesOrCiSecrets()
    {
        var ci = Listed.Value.Ci;
        var found = new List<string>();
        foreach (var file in Files.Value.Where(f => f.IsWorkflow))
        {
            for (var index = 0; index < file.Lines.Length; index++)
            {
                foreach (var name in OtherWorkflowNames(file.Lines[index], ci))
                {
                    found.Add($"{file.Relative}:{index + 1}: {name}");
                }
            }
        }
        Assert.AreEqual(
            0,
            found.Count,
            $"{found.Count} secrets or variables named in the workflows " +
            $"neither start {Prefix} nor are secrets or variables only " +
            "CI reads:\n" +
            string.Join("\n", found));
    }

    [TestMethod]
    public void TheResourceKeyVariableMatcherFindsOnlyAResourceKeyVariable()
    {
        var key = Prefix + "RESOURCE_KEY_BASIC";
        Assert.IsTrue(ResourceKeyVariable.IsMatch(
            $"{key}: ${{{{ vars.{key} }}}}"));
        Assert.IsTrue(ResourceKeyVariable.IsMatch(
            $"{Prefix}RESOURCE_KEY: ${{{{ vars.{Prefix}RESOURCE_KEY }}}}"));
        Assert.IsFalse(ResourceKeyVariable.IsMatch(
            $"{key}: ${{{{ secrets.{key} }}}}"));
        Assert.IsFalse(ResourceKeyVariable.IsMatch(
            $"{Prefix}CLOUD_ENDPOINT: ${{{{ vars.{Prefix}CLOUD_ENDPOINT }}}}"));
    }

    [TestMethod]
    public void EveryWorkflowReadsEachResourceKeyAsASecret()
    {
        var found = new List<string>();
        foreach (var file in Files.Value.Where(f => f.IsWorkflow))
        {
            for (var index = 0; index < file.Lines.Length; index++)
            {
                foreach (Match match in
                    ResourceKeyVariable.Matches(file.Lines[index]))
                {
                    found.Add($"{file.Relative}:{index + 1}: {match.Value}");
                }
            }
        }
        Assert.AreEqual(
            0,
            found.Count,
            $"{found.Count} resource keys are read as organization " +
            "variables, where each is an organization secret and must be " +
            "read as secrets.<name>:\n" + string.Join("\n", found));
    }

    [TestMethod]
    public void NoNameIsFilledFromAnother()
    {
        var found = new List<string>();
        foreach (var file in Files.Value.Where(f =>
            f.IsCode && f.Relative != ThisFile))
        {
            for (var index = 0; index < file.Lines.Length; index++)
            {
                foreach (var fill in FillsFromAnotherName(file.Lines[index]))
                {
                    found.Add($"{file.Relative}:{index + 1}: {fill}");
                }
            }
        }
        Assert.AreEqual(
            0,
            found.Count,
            $"{found.Count} {Prefix} names are filled from a secret, " +
            "variable or environment variable of another name, where each " +
            "must come from its own:\n" + string.Join("\n", found));
    }
}
