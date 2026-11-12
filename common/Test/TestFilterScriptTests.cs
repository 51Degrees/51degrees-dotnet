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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace FiftyOne.Common.Tests;

/// <summary>
/// Tests that the CI scripts still take the test filter from the old name
/// of its environment variable when the new name is not set. Each test
/// runs ci/test-filter.ps1 in PowerShell with its own environment.
/// </summary>
[TestClass]
public class TestFilterScriptTests
{
    private const string NewName = "_51DEGREES_TEST_FILTER";
    private const string OldName = "FIFTYONE_TEST_FILTER";

    /// <summary>
    /// How long the script is given to finish.
    /// </summary>
    private const int ScriptTimeoutSeconds = 120;

    private static string Script()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder != null)
        {
            if (Directory.Exists(Path.Combine(folder.FullName, ".git")) ||
                File.Exists(Path.Combine(folder.FullName, ".git")))
            {
                return Path.Combine(folder.FullName, "ci", "test-filter.ps1");
            }
            folder = folder.Parent;
        }
        throw new InvalidOperationException(
            $"No folder at or above '{AppContext.BaseDirectory}' holds .git.");
    }

    /// <summary>
    /// The lines the script writes when the two variables hold the values
    /// given, where null leaves a variable unset.
    /// </summary>
    private static string[] Run(string newValue, string oldValue)
    {
        var script = Script();
        Assert.IsTrue(File.Exists(script), $"'{script}' does not exist.");
        var start = new ProcessStartInfo("pwsh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(script);
        start.Environment.Remove(NewName);
        start.Environment.Remove(OldName);
        if (newValue != null)
        {
            start.Environment[NewName] = newValue;
        }
        if (oldValue != null)
        {
            start.Environment[OldName] = oldValue;
        }
        Process process;
        try
        {
            process = Process.Start(start);
        }
        catch (Win32Exception)
        {
            Assert.Inconclusive(
                "PowerShell 7 (pwsh) runs the CI scripts and was not found.");
            throw;
        }
        using (process)
        {
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            Assert.IsTrue(
                process.WaitForExit(ScriptTimeoutSeconds * 1000),
                "The script did not finish in time.");
            Assert.AreEqual(0, process.ExitCode, $"The script failed: {error}");
            return output
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToArray();
        }
    }

    [TestMethod]
    public void OnlyTheOldNameSet_GivesThatFilterWithAWarning()
    {
        var lines = Run(null, "old-filter");

        Assert.AreEqual("old-filter", lines.Last());
        var warning = string.Join(" ", lines.Take(lines.Length - 1));
        StringAssert.Contains(warning, "deprecated");
        StringAssert.Contains(warning, OldName);
    }

    [TestMethod]
    public void NewNameSet_WinsOverTheOldNameWithNoWarning()
    {
        var lines = Run("new-filter", "old-filter");

        CollectionAssert.AreEqual(new[] { "new-filter" }, lines);
    }

    [TestMethod]
    public void NeitherNameSet_GivesNothing()
    {
        var lines = Run(null, null);

        Assert.AreEqual(0, lines.Length, string.Join("\n", lines));
    }
}
