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
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace FiftyOne.Pipeline.Web.Tests;

/// <summary>
/// Checks that the web integration documentation and every web integration
/// example say that a Flow Data cannot be used by two threads at once, and
/// show that work handed to another thread gets values, not the Flow Data.
/// Reads source and documentation only.
/// </summary>
[TestClass]
public class FlowDataThreadsDocumentationTests
{
    private const string Statement =
        "a flow data cannot be used by two threads at once";

    private const string HandOff = "not the flow data";

    // Comment, Markdown and server comment leaders at the start of a line.
    private static readonly Regex LineLeaders =
        new Regex(@"(?m)^\s*(///|//|\*|#|>|<%--|--%>)+");

    private static readonly Regex WebMarker = new Regex(
        @"using\s+(Microsoft\.AspNetCore|System\.Web)|<%@");

    private static readonly Regex FlowDataAccess = new Regex(
        @"GetFlowData\(|CreateFlowData\(|PipelineCapabilities|" +
        @"Request\.Browser");

    // A source that adds the web integration's middleware is a web
    // example, whether or not it names ASP.NET in a using directive.
    private static readonly Regex UsesTheMiddleware = new Regex(
        @"UseFiftyOne\(");

    // The general statement the Flow Data's own documentation makes.
    private const string NotSafe =
        "a flow data is not safe to use from several threads at once";

    private const string CallerLocks =
        "the caller is responsible for any locking";

    private static readonly string[] FlowDataDocuments =
    {
        "pipeline/Core/Data/IFlowData.cs",
    };

    private static readonly string[] Documents =
    {
        "pipeline/README.md",
        "examples/device-detection/README.md",
        "examples/ip-intelligence/README.md",
        "location/README.md",
        "pipeline/Web/FiftyOneMiddleware.cs",
        "pipeline/Web/Services/IFlowDataProvider.cs",
        "pipeline/Web/Services/FlowDataProvider.cs",
        "pipeline/Web.Framework/PipelineModule.cs",
        "pipeline/Web.Framework/Providers/PipelineCapabilities.cs",
    };

    private static readonly string[] KnownExamples =
    {
        "examples/device-detection/Cloud/Framework-Web/Default.aspx.cs",
        "examples/device-detection/Cloud/GettingStarted-Web/Controllers/" +
            "HomeController.cs",
        "examples/device-detection/Cloud/pmp-web/Program.cs",
        "examples/device-detection/Legacy Web/UACH-manual/Controllers/" +
            "HomeController.cs",
        "examples/device-detection/OnPremise/GettingStarted-Web/" +
            "Controllers/HomeController.cs",
        "examples/ip-intelligence/Cloud/GettingStarted-Web/Controllers/" +
            "HomeController.cs",
        "examples/ip-intelligence/OnPremise/Mixed/GettingStarted-API/" +
            "Program.cs",
        "examples/ip-intelligence/OnPremise/GettingStarted-Web/" +
            "Controllers/HomeController.cs",
        "location/Examples/Cloud/AspNetCore/Controllers/HomeController.cs",
        "pipeline/Examples/CustomFlowElement/3. Client-side evidence/" +
            "ClientSideEvidence.MVC/Controllers/HomeController.cs",
        "pipeline/Examples/ExampleFrameworkWebsite/Controllers/" +
            "HomeController.cs",
        "pipeline/Examples/NetCore/Controllers/HomeController.cs",
    };

    [TestMethod]
    public void TheWebIntegrationDocumentationSaysIt()
    {
        var root = Root();
        var missing = Documents
            .Where(d => SaysIt(Path.Combine(root, d)) == false)
            .ToList();
        Assert.AreEqual(
            0,
            missing.Count,
            "These do not say that a Flow Data cannot be used by two " +
            "threads at once, with values, not the Flow Data, handed to " +
            "other threads: " + string.Join(", ", missing));
    }

    [TestMethod]
    public void TheFlowDataDocumentationSaysTheCallerLocks()
    {
        var root = Root();
        var missing = FlowDataDocuments
            .Where(d =>
            {
                var text = Normalized(Path.Combine(root, d));
                return text.Contains(NotSafe) == false ||
                    text.Contains(CallerLocks) == false ||
                    SaysIt(Path.Combine(root, d)) == false;
            })
            .ToList();
        Assert.AreEqual(
            0,
            missing.Count,
            "These do not say that a Flow Data is not safe to use from " +
            "several threads at once, that the caller is responsible for " +
            "any locking, and that a Flow Data cannot be used by two " +
            "threads at once: " + string.Join(", ", missing));
    }

    [TestMethod]
    public void ThePipelineReadmeShowsAHandOff()
    {
        var readme = File.ReadAllText(
            Path.Combine(Root(), "pipeline", "README.md"));
        var section = readme.IndexOf(
            "Flow Data and threads",
            StringComparison.Ordinal);
        Assert.IsTrue(
            section >= 0,
            "pipeline/README.md has no Flow Data and threads section");
        Assert.IsTrue(
            readme.Substring(section).Contains("Task.Run("),
            "The Flow Data and threads section shows no work handed to " +
            "another task");
    }

    [TestMethod]
    public void EveryWebExampleSaysIt()
    {
        var root = Root();
        var found = WebExamples(root);
        foreach (var known in KnownExamples)
        {
            Assert.IsTrue(
                found.Contains(known),
                $"The web example {known} was not found");
        }
        var missing = found
            .Where(e => SaysIt(Path.Combine(root, e)) == false)
            .ToList();
        Assert.AreEqual(
            0,
            missing.Count,
            "These web examples do not say that a Flow Data cannot be " +
            "used by two threads at once, with values, not the Flow Data, " +
            "handed to other threads: " + string.Join(", ", missing));
    }

    /// <summary>
    /// Every C# or ASP.NET page source in an examples folder that uses
    /// ASP.NET and gets, creates or reads through a Flow Data, or that adds
    /// the web integration's middleware.
    /// </summary>
    private static List<string> WebExamples(string root)
    {
        var examples = new List<string>();
        foreach (var file in Sources(root))
        {
            var relative = Path.GetRelativePath(root, file)
                .Replace(Path.DirectorySeparatorChar, '/');
            if (IsExampleSource(relative) == false)
            {
                continue;
            }
            var text = File.ReadAllText(file);
            if ((WebMarker.IsMatch(text) && FlowDataAccess.IsMatch(text)) ||
                UsesTheMiddleware.IsMatch(text))
            {
                examples.Add(relative);
            }
        }
        return examples;
    }

    // Folders that hold no example source, skipped to keep the walk short.
    private static readonly HashSet<string> Skipped =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git", "bin", "obj", "node_modules", "wwwroot", "packages",
            "cxx", "Tests", "Scripts", "Content",
        };

    private static IEnumerable<string> Sources(string folder)
    {
        foreach (var file in Directory.EnumerateFiles(folder))
        {
            yield return file;
        }
        foreach (var child in Directory.EnumerateDirectories(folder))
        {
            if (Skipped.Contains(Path.GetFileName(child)) == false)
            {
                foreach (var file in Sources(child))
                {
                    yield return file;
                }
            }
        }
    }

    private static bool IsExampleSource(string path) =>
        (path.EndsWith(".cs", StringComparison.Ordinal) ||
            path.EndsWith(".aspx", StringComparison.Ordinal)) &&
        (path.StartsWith("examples/", StringComparison.Ordinal) ||
            path.Contains("/Examples/"));

    private static bool SaysIt(string path)
    {
        var text = Normalized(path);
        return text.Contains(Statement) && text.Contains(HandOff);
    }

    /// <summary>
    /// The text with comment and Markdown line leaders removed, white space
    /// collapsed and in lower case, so a sentence wrapped over lines
    /// matches.
    /// </summary>
    private static string Normalized(string path)
    {
        var text = LineLeaders.Replace(File.ReadAllText(path), " ");
        return Regex.Replace(text, @"\s+", " ").ToLowerInvariant();
    }

    /// <summary>
    /// The repository root, being the nearest folder above this source
    /// file, or above the test's own folder, that holds the pipeline,
    /// examples and location folders.
    /// </summary>
    private static string Root()
    {
        foreach (var start in new[] { ThisFile(), AppContext.BaseDirectory })
        {
            var folder = string.IsNullOrEmpty(start) ?
                null :
                new DirectoryInfo(Path.GetDirectoryName(start));
            while (folder != null)
            {
                if (Directory.Exists(Path.Combine(folder.FullName, "pipeline")) &&
                    Directory.Exists(Path.Combine(folder.FullName, "examples")) &&
                    Directory.Exists(Path.Combine(folder.FullName, "location")))
                {
                    return folder.FullName;
                }
                folder = folder.Parent;
            }
        }
        Assert.Fail("The repository root was not found");
        return null;
    }

    private static string ThisFile([CallerFilePath] string path = "") =>
        File.Exists(path) ? path : null;
}
