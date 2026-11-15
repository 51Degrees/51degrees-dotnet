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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.Pipeline.Web.Tests;

/// <summary>
/// Where the web integration puts the elements it adds to a Pipeline it
/// builds from configuration. The Sequence Element goes first, the JSON
/// Builder immediately before the JavaScript Builder, and the JavaScript
/// Builder and the Set Headers Element after every other element, each
/// only when the configuration does not already have it.
/// </summary>
[TestClass]
public class StartupElementOrderTests
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

    /// <summary>
    /// With none of the four configured, each is added in its place.
    /// </summary>
    [TestMethod]
    public void NoneConfigured_EachAddedInItsPlace()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                Sequence, "EngineA", "EngineB", Json, JavaScript, SetHeaders,
            },
            ElementsBuilt("EngineA", "EngineB"));
    }

    /// <summary>
    /// A JavaScript Builder already configured keeps its place, and the
    /// JSON Builder is added immediately before it.
    /// </summary>
    [TestMethod]
    public void JavaScriptBuilderConfigured_JsonBuilderAddedBeforeIt()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                Sequence, "EngineA", Json, JavaScript, "EngineB", SetHeaders,
            },
            ElementsBuilt("EngineA", JavaScript, "EngineB"));
    }

    /// <summary>
    /// A Sequence Element already configured keeps its place and is not
    /// added again.
    /// </summary>
    [TestMethod]
    public void SequenceElementConfigured_KeepsItsPlace()
    {
        CollectionAssert.AreEqual(
            new[] { "EngineA", Sequence, Json, JavaScript, SetHeaders },
            ElementsBuilt("EngineA", Sequence));
    }
}
