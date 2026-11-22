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
using System.Text.RegularExpressions;

namespace FiftyOne.HumanConfidence.Examples.Tests;

/// <summary>
/// The rules every Human Confidence web example keeps, read from the
/// example's own files, so that a change to the page, the server or the
/// README that breaks one is caught without a browser or a key. The server
/// listens on port 5151 unless told otherwise, reads its resource key and
/// its endpoint from the agreed variables, ends its page with a Find out
/// more section whose links are tagged, links to contact us for a key that
/// does not carry the property, and never says how the cloud works the
/// level out, because a cloud property is read, not re-derived.
/// </summary>
[TestClass]
public class ExampleRulesTests
{
    /// <summary>
    /// The example's folder, relative to the repository root.
    /// </summary>
    private const string ExampleFolder = "pipeline/Examples/HumanConfidence";

    private const string Program = "HumanConfidenceWeb/Program.cs";
    private const string Page = "HumanConfidenceWeb/page.html";
    private const string ReadMe = "README.md";

    /// <summary>
    /// The port every web example listens on by default.
    /// </summary>
    private const string Port = "5151";

    /// <summary>
    /// The port the example listened on before the rule.
    /// </summary>
    private const string OldPort = "5110";

    private const string KeyVariable = "_51DEGREES_RESOURCE_KEY";
    private const string EndpointVariable = "_51DEGREES_CLOUD_ENDPOINT";

    /// <summary>
    /// The files that can hold the rules, with the build output left out.
    /// </summary>
    private static readonly string[] SourceExtensions =
    {
        ".cs",
        ".html",
        ".md",
    };

    /// <summary>
    /// A link to 51degrees.com, as a page or a program writes it.
    /// </summary>
    private static readonly Regex AnyLink = new Regex(
        @"https://51degrees\.com/[^""'\s<)]*");

    /// <summary>
    /// A link to 51degrees.com tagged as example output, with the content
    /// and term that say which file and which section it came from. A page
    /// writes the ampersands as entities.
    /// </summary>
    private static readonly Regex TaggedLink = new Regex(
        @"^https://51degrees\.com/[^?]*\?" +
        @"utm_source=code&(amp;)?utm_medium=example&(amp;)?" +
        @"utm_campaign=51degrees-dotnet&(amp;)?" +
        @"utm_content=[a-z0-9.-]+&(amp;)?utm_term=[a-z0-9-]+$");

    /// <summary>
    /// A tagged link to where someone asks 51Degrees for the property.
    /// </summary>
    private static readonly Regex ContactUsLink = new Regex(
        @"https://51degrees\.com/contact-us\?utm_source=code&amp;");

    /// <summary>
    /// Phrases that describe how the cloud reaches the level. An example
    /// shows the level the cloud gives and leaves the working to the cloud.
    /// </summary>
    private static readonly string[] HowTheLevelIsWorkedOut =
    {
        "worked out",
        "TLS inspection",
        "headless",
        "genuine device",
        "genuine computer",
        "Firefox",
    };

    private static string Root() => Path.Combine(
        ExampleWithoutKey.RepositoryRoot(),
        ExampleFolder.Replace('/', Path.DirectorySeparatorChar));

    private static string Text(string relative) => File.ReadAllText(
        Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>
    /// The example's source files, as paths relative to its folder with
    /// forward slashes, leaving out what a build writes.
    /// </summary>
    private static List<string> SourceFiles()
    {
        var root = Root();
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(f => SourceExtensions.Contains(Path.GetExtension(f)))
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Where(f => f.Split('/').Any(part =>
                part == "bin" || part == "obj") == false)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
    }

    [TestMethod]
    public void TheServerListensOn5151ByDefault()
    {
        Assert.IsTrue(
            Text(Program).Contains($"?? \"{Port}\"", StringComparison.Ordinal),
            $"{Program} should listen on port {Port} when PORT is not set.");
        Assert.IsTrue(
            Text(ReadMe).Contains($"localhost:{Port}", StringComparison.Ordinal),
            $"{ReadMe} should say the page is at localhost:{Port}.");
        var stale = SourceFiles()
            .Where(f => Text(f).Contains(OldPort, StringComparison.Ordinal))
            .ToList();
        Assert.AreEqual(0, stale.Count,
            $"These files still say port {OldPort}:\n" +
            string.Join("\n", stale));
    }

    [TestMethod]
    public void TheKeyAndTheEndpointComeFromTheAgreedVariables()
    {
        var program = Text(Program);
        Assert.IsTrue(
            program.Contains($"GetEnvironmentVariable(\"{KeyVariable}\")",
                StringComparison.Ordinal),
            $"{Program} should read the resource key from {KeyVariable}.");
        Assert.IsTrue(
            program.Contains($"GetEnvironmentVariable(\"{EndpointVariable}\")",
                StringComparison.Ordinal),
            $"{Program} should read the endpoint from {EndpointVariable}.");
        var readMe = Text(ReadMe);
        Assert.IsTrue(
            readMe.Contains(KeyVariable, StringComparison.Ordinal) &&
            readMe.Contains(EndpointVariable, StringComparison.Ordinal),
            $"{ReadMe} should document {KeyVariable} and {EndpointVariable}.");
    }

    [TestMethod]
    public void ThePageEndsWithFindOutMoreAndEveryLinkIsTagged()
    {
        var page = Text(Page);
        Assert.IsTrue(
            page.Contains("Find out more", StringComparison.Ordinal),
            $"{Page} should end with a Find out more section.");
        var links = AnyLink.Matches(page).Select(m => m.Value).ToList();
        Assert.AreNotEqual(0, links.Count,
            $"{Page} should link to 51degrees.com in its Find out more section.");
        var untagged = links.Where(l => TaggedLink.IsMatch(l) == false).ToList();
        Assert.AreEqual(0, untagged.Count,
            $"These links in {Page} are not tagged as example output:\n" +
            string.Join("\n", untagged));
    }

    [TestMethod]
    public void ThePageLinksToContactUsWhereTheKeyLacksTheProperty()
    {
        Assert.IsTrue(
            ContactUsLink.IsMatch(Text(Page)),
            $"{Page} should hold a tagged link to contact us, for a key " +
            "that does not carry HumanConfidence.");
        Assert.IsTrue(
            Text(Program).Contains("accessibleproperties", StringComparison.Ordinal),
            $"{Program} should ask accessibleproperties whether the key " +
            "carries HumanConfidence, so the page can show the link.");
    }

    [TestMethod]
    public void NothingSaysHowTheLevelIsWorkedOut()
    {
        var says = new List<string>();
        foreach (var file in SourceFiles())
        {
            var text = Text(file);
            foreach (var phrase in HowTheLevelIsWorkedOut)
            {
                if (text.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                {
                    says.Add($"{file}: \"{phrase}\"");
                }
            }
        }
        Assert.AreEqual(0, says.Count,
            "The cloud works the level out, and an example only reads it, " +
            "so these should go:\n" + string.Join("\n", says));
    }
}
