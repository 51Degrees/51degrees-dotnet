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

namespace FiftyOne.Common.Tests;

/// <summary>
/// Finds this repository and the files git tracks in it, for the tests that
/// check a rule across every file.
/// </summary>
internal static class Repository
{
    /// <summary>
    /// How long git is given to list the tracked files.
    /// </summary>
    private const int GitTimeoutSeconds = 120;

    /// <summary>
    /// The repository root, being the first folder at or above the test
    /// assembly that holds a .git entry.
    /// </summary>
    public static string Root()
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
    public static List<string> TrackedFiles(string root)
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
}
