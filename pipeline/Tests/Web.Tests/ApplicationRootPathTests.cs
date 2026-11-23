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
using FiftyOne.Pipeline.Engines.TestHelpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FiftyOne.Pipeline.Web.Tests;

[TestClass]
public class ApplicationRootPathTests
{
    private const string ContentRoot = "contentroot";

    private class CapturingBuilder : IPipelineBuilderFromConfiguration
    {
        public PipelineOptions Options { get; private set; }

        public IPipeline BuildFromConfiguration(PipelineOptions options)
        {
            Options = options;
            return new Mock<IPipeline>().Object;
        }
    }

    private class FakeEnvironment : IWebHostEnvironment
    {
        public string ContentRootPath { get; set; } = ContentRoot;
        public string ApplicationName { get; set; } =
            typeof(ApplicationRootPathTests).Assembly.GetName().Name;
        public IFileProvider ContentRootFileProvider { get; set; }
        public string EnvironmentName { get; set; } = "Test";
        public string WebRootPath { get; set; }
        public IFileProvider WebRootFileProvider { get; set; }
    }

    private static string BuildAndGetParameter(string key, string value)
    {
        var testConfig = new Dictionary<string, string>
        {
            ["PipelineOptions:Elements:0:BuilderName"] = "EmptyEngine",
            ["PipelineOptions:Elements:0:BuildParameters:" + key] = value,
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(testConfig)
            .Build();
        var capture = new CapturingBuilder();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<EmptyEngineBuilder>();
        services.AddSingleton<IWebHostEnvironment>(new FakeEnvironment());
        services.AddFiftyOne(configuration);
        services.AddSingleton<IPipelineBuilderFromConfiguration>(capture);

        services.BuildServiceProvider().GetRequiredService<IPipeline>();

        return (string)capture.Options.Elements
            .Single(e => e.BuilderName == "EmptyEngine")
            .BuildParameters[key];
    }

    [TestMethod]
    [DataRow("DataFile", "~/App_Data/data.hash")]
    [DataRow("DataFile", "~\\App_Data\\data.hash")]
    [DataRow("TempDirPath", "~/App_Data/tmp")]
    [DataRow("TempDirPath", "~\\App_Data\\tmp")]
    public void TildePathResolvesUnderContentRoot(string key, string configured)
    {
        var tail = configured.Substring(2)
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        Assert.AreEqual(
            Path.Combine(ContentRoot, tail),
            BuildAndGetParameter(key, configured));
    }

    [TestMethod]
    [DataRow("DataFile", "App_Data/data.hash")]
    [DataRow("DataFile", "data.hash")]
    [DataRow("DataFile", "~data.hash")]
    [DataRow("DataFile", "/var/data/data.hash")]
    [DataRow("TempDirPath", "App_Data/tmp")]
    [DataRow("TempDirPath", "~tmp")]
    public void OtherPathsAreUnchanged(string key, string configured)
    {
        Assert.AreEqual(configured, BuildAndGetParameter(key, configured));
    }
}
