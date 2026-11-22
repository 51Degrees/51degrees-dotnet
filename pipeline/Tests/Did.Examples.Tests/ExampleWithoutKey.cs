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

#nullable disable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FiftyOne.Did.Examples.Tests;

/// <summary>
/// Runs an example's own build with no resource key in its environment, as
/// someone who has just cloned the repository runs it, and checks that it
/// ends without an error, names the variable to set and links to where a
/// resource key is created.
/// </summary>
internal static class ExampleWithoutKey
{
    /// <summary>
    /// How long an example may take to say it needs a resource key and end.
    /// It loads no data file and calls no service, so it ends in seconds.
    /// </summary>
    private const int TimeoutMilliseconds = 60000;

    /// <summary>
    /// The variable every cloud example reads its resource key from.
    /// </summary>
    private const string KeyVariable = "_51DEGREES_RESOURCE_KEY";


    /// <summary>
    /// A link to where a resource key is created, tagged as example output.
    /// </summary>
    private static readonly Regex CreateKeyLink = new Regex(
        @"https://51degrees\.com/configure(/[A-Za-z0-9]+)?\?" +
        "utm_source=code&utm_medium=example&utm_campaign=51degrees-dotnet");

    /// <summary>
    /// Runs the example built from the project in the folder, relative to
    /// the repository root, and checks what it says with no resource key.
    /// </summary>
    public static void AssertSaysWhereToCreateAKey(
        string projectFolder,
        string assemblyName)
    {
        var (exitCode, output) = Run(projectFolder, assemblyName);
        Console.WriteLine(output);
        Assert.AreEqual(0, exitCode,
            $"{assemblyName} should end successfully with no resource key.");
        var threw = output.Contains(
            "Unhandled exception", StringComparison.OrdinalIgnoreCase);
        Assert.IsFalse(threw,
            $"{assemblyName} should end without an error.");
        var namesVariable = output.Contains(
            KeyVariable, StringComparison.Ordinal);
        Assert.IsTrue(namesVariable,
            $"{assemblyName} should name the variable '{KeyVariable}'.");
        var linksToCreate = CreateKeyLink.IsMatch(output);
        Assert.IsTrue(linksToCreate,
            $"{assemblyName} should link to where a resource key is created.");
    }

    private static (int ExitCode, string Output) Run(
        string projectFolder,
        string assemblyName)
    {
        var dll = FindBuild(projectFolder, assemblyName);
        var folder = Path.GetDirectoryName(dll);
        // The app host matches the architecture the example was built for,
        // which the dotnet on the path might not.
        var host = Path.Combine(folder,
            assemblyName + (OperatingSystem.IsWindows() ? ".exe" : ""));
        var start = new ProcessStartInfo(File.Exists(host) ? host : "dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            WorkingDirectory = folder,
            CreateNoWindow = true,
        };
        if (File.Exists(host) == false)
        {
            start.ArgumentList.Add(dll);
        }
        start.Environment.Remove(KeyVariable);

        var output = new StringBuilder();
        using var process = new Process { StartInfo = start };
        process.OutputDataReceived += (_, e) => Append(output, e.Data);
        process.ErrorDataReceived += (_, e) => Append(output, e.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        // Closed input means an example never waits for a key press.
        process.StandardInput.Close();
        if (process.WaitForExit(TimeoutMilliseconds) == false)
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail($"{assemblyName} was still running after " +
                $"{TimeoutMilliseconds} ms with no resource key. It wrote:" +
                Environment.NewLine + output);
        }
        // Waits for the output handlers to finish.
        process.WaitForExit();
        lock (output)
        {
            return (process.ExitCode, output.ToString());
        }
    }

    private static void Append(StringBuilder output, string line)
    {
        if (line != null)
        {
            lock (output)
            {
                output.AppendLine(line);
            }
        }
    }

    /// <summary>
    /// The newest build of the example's assembly that can be run, being one
    /// with its runtime configuration beside it.
    /// </summary>
    private static string FindBuild(string projectFolder, string assemblyName)
    {
        var bin = Path.Combine(RepositoryRoot(),
            projectFolder.Replace('/', Path.DirectorySeparatorChar), "bin");
        var dll = Directory.Exists(bin) ?
            Directory.EnumerateFiles(bin, assemblyName + ".dll",
                    SearchOption.AllDirectories)
                .Where(f => File.Exists(
                    Path.ChangeExtension(f, ".runtimeconfig.json")))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault() :
            null;
        Assert.IsNotNull(dll, $"No build of {assemblyName} under {bin}. " +
            "The example is built with its solution before the tests run.");
        return dll;
    }

    private static string RepositoryRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory);
            folder != null;
            folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "Directory.Build.targets")) &&
                Directory.Exists(Path.Combine(folder.FullName, "ci")))
            {
                return folder.FullName;
            }
        }
        Assert.Fail("The repository root was not found above " +
            AppContext.BaseDirectory);
        return null;
    }
}
