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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Tests.FlowElements;

/// <summary>
/// Every Pipeline configuration file in the repository that names the
/// usage sharing element names it last, because a builder refuses any
/// other place and people copy these files.
/// See the <see href="https://github.com/51Degrees/specifications/blob/d79bbe3b2aef86273570742f6b660f29bfe72816/pipeline-specification/pipeline-elements/usage-sharing-element.md?plain=1#L127-L133">Specification</see>
/// </summary>
[TestClass]
public class ShareUsageConfigurationFilesTests
{
    private const string SampleConfiguration =
        "examples/device-detection/sample-configuration.json";

    private static readonly string[] SkippedFolders = { "bin", "obj", ".git" };

    [TestMethod]
    public void EveryConfigurationFile_NamesUsageSharingLast()
    {
        var root = FindRoot();
        var checkedFiles = new List<string>();
        var wrong = new List<string>();
        foreach (var file in JsonFiles(root))
        {
            var names = BuilderNames(file);
            if (names == null ||
                names.Any(IsUsageSharing) == false)
            {
                continue;
            }
            var relative = Path.GetRelativePath(root, file)
                .Replace('\\', '/');
            checkedFiles.Add(relative);
            if (IsUsageSharing(names.Last()) == false)
            {
                wrong.Add($"{relative} ({string.Join(", ", names)})");
            }
        }

        CollectionAssert.Contains(
            checkedFiles,
            SampleConfiguration,
            "The sample configuration names no usage sharing element.");
        Assert.IsEmpty(
            wrong,
            "Configuration files that put usage sharing before another " +
            "element: " + string.Join("; ", wrong));
    }

    /// <summary>
    /// The same match the web integration uses to find usage sharing in a
    /// configuration.
    /// </summary>
    private static bool IsUsageSharing(string builderName) =>
        builderName != null &&
        builderName.Contains("ShareUsage", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The builder names of PipelineOptions.Elements in order, or null when
    /// the file is not a Pipeline configuration.
    /// </summary>
    private static List<string> BuilderNames(string file)
    {
        var text = File.ReadAllText(file);
        if (text.Contains("BuilderName", StringComparison.Ordinal) == false ||
            text.Contains(
                "ShareUsage", StringComparison.OrdinalIgnoreCase) == false)
        {
            return null;
        }
        using var document = JsonDocument.Parse(
            text,
            new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            document.RootElement.TryGetProperty(
                "PipelineOptions", out var options) == false ||
            options.TryGetProperty("Elements", out var elements) == false)
        {
            return null;
        }
        return elements.EnumerateArray()
            .Select(e => e.TryGetProperty("BuilderName", out var name) ?
                name.GetString() : null)
            .ToList();
    }

    private static IEnumerable<string> JsonFiles(string root)
    {
        var folders = new Stack<string>();
        folders.Push(root);
        while (folders.Count > 0)
        {
            var folder = folders.Pop();
            foreach (var child in Directory.EnumerateDirectories(folder))
            {
                var name = Path.GetFileName(child);
                if (SkippedFolders.Contains(name) == false &&
                    name != "cxx" &&
                    name != "node_modules")
                {
                    folders.Push(child);
                }
            }
            foreach (var file in Directory.EnumerateFiles(folder, "*.json"))
            {
                yield return file;
            }
        }
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(
                directory.FullName, SampleConfiguration)))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        Assert.Fail("The repository root holding " + SampleConfiguration +
            " was not found above " + AppContext.BaseDirectory);
        return null;
    }
}
