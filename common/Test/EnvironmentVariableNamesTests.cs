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

namespace FiftyOne.Common.Tests;

/// <summary>
/// Every 51Degrees environment variable name in this repository starts with
/// an underscore, because a POSIX shell cannot set a name that starts with a
/// digit. The test reads every file git tracks, which leaves out build
/// output and the submodules, and fails on any name that starts with the
/// digit.
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
    /// underscore.
    /// </summary>
    private static List<string> DigitInitialNames(string line)
    {
        var names = new List<string>();
        var at = 0;
        while (at < line.Length)
        {
            if (at + DigitInitial.Length <= line.Length &&
                string.CompareOrdinal(
                    line, at, DigitInitial, 0, DigitInitial.Length) == 0 &&
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

    [TestMethod]
    public void TheNameMatcherFindsOnlyNamesThatStartWithTheDigit()
    {
        var digitInitial = DigitInitial + "DD_PATH";
        var line =
            $"export {digitInitial}=a _{digitInitial}=b X{digitInitial}";
        CollectionAssert.AreEqual(
            new[] { digitInitial },
            DigitInitialNames(line));
    }

    [TestMethod]
    public void NoFileNamesAVariableThatStartsWithADigit()
    {
        var root = RepositoryRoot();
        var found = new List<string>();
        foreach (var relative in TrackedFiles(root))
        {
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
                foreach (var name in DigitInitialNames(lines[index]))
                {
                    found.Add($"{relative}:{index + 1}: {name}");
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
}
