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

using FiftyOne.Pipeline.Core.Configuration;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.Engines.TestHelpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.Pipeline.Web.Tests;

/// <summary>
/// The web integration adds the elements it needs before a usage sharing
/// element, which is always last.
/// See the <see href="https://github.com/51Degrees/specifications/blob/a90ba516a3bfadc58dd7a51535d83af6bdaed291/pipeline-specification/features/web-integration.md#pipeline-configuration">Specification</see>
/// </summary>
[TestClass]
public class UsageSharingLastTests
{
    private const string Sequence = "SequenceElement";
    private const string Json = "JsonBuilderElement";
    private const string JavaScript = "JavaScriptBuilderElement";
    private const string SetHeaders = "SetHeadersElement";

    private class CapturingBuilder : IPipelineBuilderFromConfiguration
    {
        public PipelineOptions Options { get; private set; }

        public IPipeline BuildFromConfiguration(PipelineOptions options)
        {
            Options = options;
            return new Mock<IPipeline>().Object;
        }
    }

    private static string[] ElementsBuilt(params string[] configured)
    {
        var testConfig = new Dictionary<string, string>();
        for (var i = 0; i < configured.Length; i++)
        {
            testConfig[$"PipelineOptions:Elements:{i}:BuilderName"] =
                configured[i];
        }
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(testConfig)
            .Build();
        var capture = new CapturingBuilder();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFiftyOne(configuration);
        services.AddSingleton<IPipelineBuilderFromConfiguration>(capture);

        services.BuildServiceProvider().GetRequiredService<IPipeline>();

        return capture.Options.Elements.Select(e => e.BuilderName).ToArray();
    }

    private static ServiceProvider CreateProvider(
        Dictionary<string, string> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<EmptyEngineBuilder>();
        services.AddSingleton<ShareUsageBuilder>();
        services.AddFiftyOne(configuration);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// A usage sharing element in the configuration stays last, with the
    /// elements the web integration adds placed before it.
    /// </summary>
    [TestMethod]
    [DataRow("ShareUsageBuilder")]
    [DataRow("ShareUsage")]
    [DataRow("shareusagebuilder")]
    public void UsageSharingConfigured_ElementsAddedBeforeIt(string name)
    {
        Assert.AreEqual(
            string.Join(", ",
                Sequence, "EngineA", Json, JavaScript, SetHeaders, name),
            string.Join(", ", ElementsBuilt("EngineA", name)));
    }

    /// <summary>
    /// The Pipeline the web integration builds ends with the usage sharing
    /// element that the configuration names.
    /// </summary>
    [TestMethod]
    public void UsageSharingConfigured_PipelineEndsWithIt()
    {
        using var provider = CreateProvider(new Dictionary<string, string>
        {
            ["PipelineOptions:Elements:0:BuilderName"] = "EmptyEngine",
            ["PipelineOptions:Elements:1:BuilderName"] = "ShareUsageBuilder",
        });

        var pipeline = provider.GetRequiredService<IPipeline>();

        Assert.IsInstanceOfType<ShareUsageElement>(pipeline.FlowElements.Last());
        Assert.AreEqual(
            1,
            pipeline.FlowElements.Count(e => e is ShareUsageElement));
    }

    /// <summary>
    /// The Pipeline the web integration builds ends with the usage sharing
    /// element that the Pipeline builder adds.
    /// </summary>
    [TestMethod]
    public void UsageSharingAddedByBuilder_PipelineEndsWithIt()
    {
        using var provider = CreateProvider(new Dictionary<string, string>
        {
            ["PipelineOptions:Elements:0:BuilderName"] = "EmptyEngine",
            ["PipelineOptions:BuildParameters:ShareUsage"] = "True",
        });

        var pipeline = provider.GetRequiredService<IPipeline>();

        Assert.IsInstanceOfType<ShareUsageElement>(pipeline.FlowElements.Last());
        Assert.AreEqual(
            1,
            pipeline.FlowElements.Count(e => e is ShareUsageElement));
    }
}
