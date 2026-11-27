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

using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.Engines.TestHelpers;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Tests.FlowElements;

/// <summary>
/// Each usage sharing record names every 51Degrees package in use with its
/// version, being the package that holds the Pipeline, the package that
/// holds each flow element, and every 51Degrees package those depend on.
/// The Pipeline here holds the Sequence Element and the usage sharing
/// element, from one package, and an engine from another.
/// </summary>
[TestClass]
public class ShareUsagePackagesTests
{
    private static readonly Regex PackagePattern = new Regex(
        "<Package Name=\"([^\"]+)\">([^<]*)</Package>");

    private readonly LoggerFactory _loggerFactory = new LoggerFactory();

    [TestMethod]
    public void TheRecord_NamesEachPackageInUseOnce()
    {
        var names = SharePackages(out _).Select(p => p.Name).ToList();

        foreach (var expected in new[]
        {
            // The package that holds the Pipeline.
            "FiftyOne.Pipeline.Core",
            // The packages that hold the flow elements.
            "FiftyOne.Pipeline.Engines.FiftyOne",
            "FiftyOne.Pipeline.Engines.TestHelpers",
            // Packages those depend on.
            "FiftyOne.Pipeline.Engines",
            "FiftyOne.Caching",
        })
        {
            CollectionAssert.Contains(names, expected);
        }
        CollectionAssert.AllItemsAreUnique(names);
    }

    /// <summary>
    /// Every 51Degrees package a named package depends on is named too.
    /// </summary>
    [TestMethod]
    public void TheRecord_NamesThePackagesEachPackageDependsOn()
    {
        var names = SharePackages(out _).Select(p => p.Name).ToList();

        foreach (var name in names)
        {
            foreach (var reference in Loaded(name).GetReferencedAssemblies()
                .Where(r => r.Name.StartsWith(
                    "FiftyOne.", StringComparison.Ordinal)))
            {
                CollectionAssert.Contains(names, reference.Name,
                    $"{name} depends on {reference.Name}.");
            }
        }
    }

    /// <summary>
    /// Each package's version is the version its assembly was built as,
    /// without the source revision, and the Pipeline's package has the
    /// version the record gives as the Pipeline's.
    /// </summary>
    [TestMethod]
    public void TheRecord_GivesEachPackageItsVersion()
    {
        var packages = SharePackages(out var xml);

        foreach (var (name, version) in packages)
        {
            var built = Loaded(name)
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                .InformationalVersion.Split('+')[0];
            Assert.AreEqual(built, version, name);
        }
        var pipeline = Regex.Match(xml, "<Version>([^<]*)</Version>")
            .Groups[1].Value;
        Assert.AreEqual(
            pipeline,
            packages.Single(p => p.Name == "FiftyOne.Pipeline.Core").Version);
    }

    /// <summary>
    /// The packages follow the flow elements, as the record's layout in the
    /// specification shows.
    /// </summary>
    [TestMethod]
    public void ThePackages_FollowTheFlowElements()
    {
        SharePackages(out var xml);

        var lastFlowElement = xml.LastIndexOf(
            "<FlowElement>", StringComparison.Ordinal);
        var firstPackage = xml.IndexOf("<Package ", StringComparison.Ordinal);
        var language = xml.IndexOf("<Language>", StringComparison.Ordinal);
        var ordered = lastFlowElement >= 0 &&
            lastFlowElement < firstPackage &&
            firstPackage < language;
        Assert.IsTrue(ordered, xml);
    }

    /// <summary>
    /// The loaded assembly of the package.
    /// </summary>
    private static Assembly Loaded(string name)
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == name);
        Assert.IsNotNull(assembly, $"No assembly {name} is loaded.");
        return assembly;
    }

    /// <summary>
    /// Shares one call and returns the packages its record names, in the
    /// order it names them, checking that it names at least one.
    /// </summary>
    private List<(string Name, string Version)> SharePackages(out string xml)
    {
        var capture = new UsageMessageCapture();
        using var http = new HttpClient(capture);
        var pipeline = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(new SequenceElementBuilder(_loggerFactory).Build())
            .AddFlowElement(new EmptyEngineBuilder(_loggerFactory).Build())
            .AddFlowElement(new ShareUsageBuilder(_loggerFactory, http)
                .SetSharePercentage(1)
                .SetMinimumEntriesPerMessage(1)
                .Build())
            .Build();
        try
        {
            using var data = pipeline.CreateFlowData();
            data.AddEvidence("header.user-agent", "Mozilla/5.0 (packages)");
            data.Process();
        }
        finally
        {
            // Disposing sends every record still queued.
            pipeline.Dispose();
        }

        xml = string.Concat(capture.Xml);
        var records = Regex.Matches(xml, "<Device>").Count;
        Assert.AreEqual(1, records, "One record should be sent.");
        var packages = PackagePattern.Matches(xml)
            .Select(m => (m.Groups[1].Value, m.Groups[2].Value))
            .ToList();
        var named = packages.Count > 0;
        Assert.IsTrue(named, "The record names no package. " + xml);
        return packages;
    }
}
