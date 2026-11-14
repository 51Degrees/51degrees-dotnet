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
using System.Text.RegularExpressions;

namespace FiftyOne.Common.Tests;

/// <summary>
/// A project that builds for more than one target framework builds them
/// at the same time, so a documentation file at one fixed path is written
/// by every target at once. That fails a build at random with CS0016, and
/// the package gets whichever target's file was written last for every
/// target. The test reads every project git tracks and fails on one that
/// builds for more than one target and names a documentation file whose
/// path does not depend on a build property.
/// </summary>
[TestClass]
public class DocumentationFileTests
{
    /// <summary>
    /// A TargetFramework or TargetFrameworks element and its value.
    /// </summary>
    private static readonly Regex TargetFrameworks = new Regex(
        @"<TargetFrameworks?\b[^>]*>(?<value>[^<]*)</TargetFrameworks?>",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// A DocumentationFile element and its value.
    /// </summary>
    private static readonly Regex DocumentationFile = new Regex(
        @"<DocumentationFile\b[^>]*>(?<value>[^<]*)</DocumentationFile>",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// Every target framework the project names, under any condition.
    /// </summary>
    private static HashSet<string> Targets(string project)
    {
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in TargetFrameworks.Matches(project))
        {
            foreach (var target in match.Groups["value"].Value.Split(
                ';', StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries))
            {
                targets.Add(target);
            }
        }
        return targets;
    }

    [TestMethod]
    public void Projects_WithMoreThanOneTarget_WriteDocumentationPerTarget()
    {
        var root = Repository.Root();
        var problems = new List<string>();
        foreach (var path in Repository.TrackedFiles(root).Where(p =>
            p.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)))
        {
            var project = File.ReadAllText(Path.Combine(
                root, path.Replace('/', Path.DirectorySeparatorChar)));
            var targets = Targets(project);
            if (targets.Count < 2)
            {
                continue;
            }
            foreach (Match match in DocumentationFile.Matches(project))
            {
                var file = match.Groups["value"].Value.Trim();
                if (file.Contains("$(", StringComparison.Ordinal) == false)
                {
                    problems.Add(
                        $"{path}: '{file}' for " +
                        string.Join(", ", targets.OrderBy(t => t)));
                }
            }
        }
        Assert.AreEqual(
            0,
            problems.Count,
            "These projects build for more than one target framework and " +
            "write every target's XML documentation to the same file. " +
            "Set GenerateDocumentationFile to true in place of " +
            "DocumentationFile, so each target writes its own." +
            Environment.NewLine +
            string.Join(Environment.NewLine, problems));
    }
}
