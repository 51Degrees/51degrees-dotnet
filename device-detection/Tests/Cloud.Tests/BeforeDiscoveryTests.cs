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

using FiftyOne.Pipeline.CloudRequestEngine;
using FiftyOne.Pipeline.CloudRequestEngine.Data;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.DeviceDetection.Cloud.Tests;

/// <summary>
/// A response that arrives while the property list cannot be read, as before
/// the cloud request engine's discovery has completed, is still read, each
/// value keeping the type it has in the response, as the cloud aspect engine
/// specification says.
/// </summary>
[TestClass]
public class BeforeDiscoveryTests
{
    private static readonly ILoggerFactory Loggers =
        new LoggerFactory();

    [TestMethod]
    public void DeviceEngine_ResponseBeforeDiscovery_KeepsItsValues()
    {
        var engine = new DeviceDetectionCloudEngineBuilder(
            Loggers).Build();
        using var pipeline = BuildPipeline(engine, "{ \"device\": { \"hardwarevendor\": \"Apple\" } }");
        using var data = pipeline.CreateFlowData();

        data.Process();

        Assert.IsEmpty(data.Errors ?? new List<IFlowError>(),
            "A response read before discovery is not an error.");
        Assert.AreEqual("Apple",
            Value(data.GetFromElement(engine).AsDictionary(), "hardwarevendor"),
            "The engine's data holds the value from the response.");
    }

    [TestMethod]
    public void HardwareProfileEngine_ResponseBeforeDiscovery_KeepsItsValues()
    {
        var engine = new HardwareProfileCloudEngineBuilder(
            Loggers).Build();
        using var pipeline = BuildPipeline(engine, "{ \"hardware\": { \"profiles\": [ { \"hardwarevendor\": \"Apple\" } ] } }");
        using var data = pipeline.CreateFlowData();

        data.Process();

        Assert.IsEmpty(data.Errors ?? new List<IFlowError>(),
            "A response read before discovery is not an error.");
        var profiles = data.GetFromElement(engine).Profiles;
        Assert.HasCount(1, profiles,
            "The engine's data holds the profile from the response.");
        Assert.AreEqual("Apple",
            Value(profiles[0].AsDictionary(), "hardwarevendor"),
            "The profile holds the value from the response.");
    }

    /// <summary>
    /// A pipeline of a stub cloud request engine that cannot give its
    /// property list, followed by the engine.
    /// </summary>
    private static IPipeline BuildPipeline(IFlowElement engine, string json)
    {
        return new PipelineBuilder(Loggers)
            .AddFlowElement(new StubRequestEngine(json))
            .AddFlowElement(engine)
            .Build();
    }

    /// <summary>
    /// The value the data holds under a name, whatever its case, as text.
    /// </summary>
    private static string Value(
        IReadOnlyDictionary<string, object> values,
        string name)
    {
        return values
            .Single(v => string.Equals(
                v.Key, name, StringComparison.OrdinalIgnoreCase))
            .Value?.ToString();
    }

    /// <summary>
    /// A cloud request engine whose property list cannot be read, as before
    /// its discovery has completed, and which gives a fixed response, so the
    /// engine under test runs without the cloud service.
    /// </summary>
    private class StubRequestEngine
        : AspectEngineBase<CloudRequestData, IAspectPropertyMetaData>,
            ICloudRequestEngine
    {
        private readonly string _json;

        public StubRequestEngine(string json)
            : base(Loggers.CreateLogger<StubRequestEngine>(), CreateData)
        {
            _json = json;
        }

        private static CloudRequestData CreateData(
            IPipeline pipeline,
            FlowElementBase<CloudRequestData, IAspectPropertyMetaData> element) =>
            new CloudRequestData(null, pipeline, element as IAspectEngine);

        public IReadOnlyDictionary<string, ProductMetaData> PublicProperties =>
            throw new CloudRequestException(
                "The property list has not been read yet.");

        public override string DataSourceTier => "";
        public override string ElementDataKey => "cloud";
        public override IEvidenceKeyFilter EvidenceKeyFilter =>
            new EvidenceKeyFilterWhitelist(new List<string>());
        public override IList<IAspectPropertyMetaData> Properties =>
            new List<IAspectPropertyMetaData>();

        protected override void ProcessEngine(
            IFlowData data, CloudRequestData aspectData) =>
            aspectData.JsonResponse = _json;

        protected override void UnmanagedResourcesCleanup() { }
    }
}
