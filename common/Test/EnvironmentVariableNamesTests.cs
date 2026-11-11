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
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace FiftyOne.Common.Tests;

/// <summary>
/// Every 51Degrees environment variable name in this repository starts with
/// an underscore, because a POSIX shell cannot set a name that starts with a
/// digit, and each setting has one name with nothing read in its place. The
/// tests read every file git tracks, which leaves out build output and the
/// submodules, and fail on any name that starts with the digit and on any
/// old name that is still read or set.
/// </summary>
[TestClass]
public class EnvironmentVariableNamesTests
{
    /// <summary>
    /// The start of a name that a shell cannot set, joined at run time so
    /// this file does not match itself.
    /// </summary>
    private const string DigitInitial = "5" + "1DEGREES_";

    /// <summary>
    /// How long git is given to list the tracked files.
    /// </summary>
    private const int GitTimeoutSeconds = 120;

    /// <summary>
    /// The repository root, being the first folder at or above the test
    /// assembly that holds a .git entry.
    /// </summary>
    private static string RepositoryRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder != null)
        {
            if (Directory.Exists(Path.Combine(folder.FullName, ".git")) ||
                File.Exists(Path.Combine(folder.FullName, ".git")))
            {
                return folder.FullName;
            }
            folder = folder.Parent;
        }
        throw new InvalidOperationException(
            $"No folder at or above '{AppContext.BaseDirectory}' holds .git.");
    }

    /// <summary>
    /// The files git tracks, as paths relative to the repository root.
    /// </summary>
    private static List<string> TrackedFiles(string root)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("ls-files");
        start.ArgumentList.Add("-z");
        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("git did not start.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        Assert.IsTrue(
            process.WaitForExit(GitTimeoutSeconds * 1000),
            "git ls-files did not finish in time.");
        Assert.AreEqual(0, process.ExitCode, $"git ls-files failed: {error}");
        var paths = new List<string>();
        foreach (var path in output.Split('\0'))
        {
            if (path.Length > 0)
            {
                paths.Add(path);
            }
        }
        return paths;
    }

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
    /// This file, which names every old name on purpose.
    /// </summary>
    private const string ThisFile =
        "common/Test/EnvironmentVariableNamesTests.cs";

    /// <summary>
    /// An old name read or set as an environment variable, or a constant
    /// held to fall back to one. A .NET read takes the name as a string, so
    /// a quoted name is matched, along with the forms the PowerShell scripts
    /// use. A quoted name inside brackets is a dictionary key, such as a
    /// page template's placeholder, and is left alone. The names keep their
    /// case, because the cloud's query string parameter resource_key is not
    /// an environment variable.
    /// </summary>
    private static readonly Regex OldName = new Regex(
        @"(?:(?<!\[)[""']|\$env:|\$\{env:|Env:|@\{\s*|;\s*)" +
        "(?:RESOURCE_KEY|SUPER_RESOURCE_KEY|LICENSE_KEY|" +
        "DEVICEDETECTIONDATAFILE|IPINTELLIGENCEDATAFILE|FOD_CLOUD_API_URL|" +
        "FIFTYONE_CLOUD_ENDPOINT|FIFTYONE_TEST_FILTER)" +
        "(?![A-Za-z0-9_])|" +
        @"\bLEGACY_[A-Z_]*ENV_VARS?\b",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// Each old name the line reads, sets or holds.
    /// </summary>
    private static List<string> OldNames(string line)
    {
        var names = new List<string>();
        foreach (Match match in OldName.Matches(line))
        {
            names.Add(match.Value);
        }
        return names;
    }

    /// <summary>
    /// Every match in the tracked files apart from <paramref name="skip"/>,
    /// as the path, the line number and the text matched.
    /// </summary>
    private static List<string> FindInTrackedFiles(
        Func<string, List<string>> find,
        string skip)
    {
        var root = RepositoryRoot();
        var found = new List<string>();
        foreach (var relative in TrackedFiles(root))
        {
            if (relative == skip)
            {
                continue;
            }
            var path = Path.Combine(
                root, relative.Replace('/', Path.DirectorySeparatorChar));
            // A submodule is tracked as a directory, and its files belong to
            // another repository.
            if (!File.Exists(path))
            {
                continue;
            }
            var text = File.ReadAllText(path, Encoding.UTF8);
            var lines = text.Split('\n');
            for (var index = 0; index < lines.Length; index++)
            {
                foreach (var name in find(lines[index]))
                {
                    found.Add($"{relative}:{index + 1}: {name}");
                }
            }
        }
        return found;
    }

    [TestMethod]
    public void TheNameMatcherFindsOnlyNamesThatStartWithTheDigit()
    {
        var digitInitial = DigitInitial + "DD_PATH";
        var lower = digitInitial.ToLowerInvariant();
        var line =
            $"export {digitInitial}=a _{digitInitial}=b X{digitInitial} " +
            $"{lower}=c";
        CollectionAssert.AreEqual(
            new[] { digitInitial, lower },
            DigitInitialNames(line));
    }

    [TestMethod]
    public void TheOldNameMatcherFindsReadsAndSetsOnly()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                "\"SUPER_RESOURCE_KEY",
                "$env:FIFTYONE_TEST_FILTER",
                "@{ FIFTYONE_CLOUD_ENDPOINT",
                "LEGACY_CLOUD_RESOURCE_KEY_ENV_VAR",
            },
            OldNames(
                "GetEnvironmentVariable(\"SUPER_RESOURCE_KEY\") " +
                "$env:FIFTYONE_TEST_FILTER " +
                "-ExampleEnv @{ FIFTYONE_CLOUD_ENDPOINT = 'x' } " +
                "LEGACY_CLOUD_RESOURCE_KEY_ENV_VAR"));
        CollectionAssert.AreEqual(
            Array.Empty<string>(),
            OldNames(
                "[\"RESOURCE_KEY\"] = ResourceKey {{RESOURCE_KEY}} " +
                "\"_51DEGREES_RESOURCE_KEY\" \"RESOURCE_KEYS\" " +
                "LICENSE_KEY_ENV_VAR resource_key=abc"));
    }

    [TestMethod]
    public void NoFileNamesAVariableThatStartsWithADigit()
    {
        var found = FindInTrackedFiles(DigitInitialNames, null);
        Assert.AreEqual(
            0,
            found.Count,
            $"{found.Count} names start with a digit, which a POSIX shell " +
            "cannot set, so each must start with an underscore:\n" +
            string.Join("\n", found));
    }

    [TestMethod]
    public void NoFileReadsAnOldName()
    {
        var found = FindInTrackedFiles(OldNames, ThisFile);
        Assert.AreEqual(
            0,
            found.Count,
            $"{found.Count} old names are still read, set or held to fall " +
            "back to, so each must use its _51DEGREES_ name alone:\n" +
            string.Join("\n", found));
    }
}
