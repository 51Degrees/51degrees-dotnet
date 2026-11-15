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

using FiftyOne.DeviceDetection.Cloud.FlowElements;
using FiftyOne.Pipeline.CloudRequestEngine.Data;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace FiftyOne.DeviceDetection.Cloud.Tests;

/// <summary>
/// A response with no section for an engine whose product the resource key
/// includes leaves the engine's data without values and raises no error.
/// The hardware profile engine covers the property keyed engines' base
/// class.
/// </summary>
[TestClass]
public class MissingSectionTests
{
    /// <summary>
    /// A response with a section for another engine only.
    /// </summary>
    private const string OtherSectionOnly =
        "{ \"other\": { \"value\": true } }";

    [TestMethod]
    public void DeviceEngine_ResponseWithNoSection_LeavesDataWithoutValues()
    {
        var engine = new DeviceDetectionCloudEngineBuilder(
            new LoggerFactory()).Build();
        using var pipeline = BuildPipeline(engine, "IsMobile", "Boolean");
        using var data = pipeline.CreateFlowData();

        data.Process();

        Assert.IsEmpty(data.Errors ?? new List<IFlowError>(),
            "A response with no section for the engine is not an error.");
        Assert.IsEmpty(data.GetFromElement(engine).AsDictionary(),
            "The engine's data has no values.");
    }

    [TestMethod]
    public void HardwareProfileEngine_ResponseWithNoSection_LeavesDataWithoutValues()
    {
        var engine = new HardwareProfileCloudEngineBuilder(
            new LoggerFactory()).Build();
        using var pipeline = BuildPipeline(engine, "Profiles", "Array");
        using var data = pipeline.CreateFlowData();

        data.Process();

        Assert.IsEmpty(data.Errors ?? new List<IFlowError>(),
            "A response with no section for the engine is not an error.");
        Assert.IsEmpty(data.GetFromElement(engine).AsDictionary(),
            "The engine's data has no values.");
    }

    /// <summary>
    /// A pipeline of a stub cloud request engine, whose product list has the
    /// engine's product with one property and whose response has no section
    /// for it, followed by the engine.
    /// </summary>
    private static IPipeline BuildPipeline(
        IFlowElement engine,
        string property,
        string type)
    {
        var products = new Dictionary<string, ProductMetaData>
        {
            {
                engine.ElementDataKey,
                new ProductMetaData
                {
                    Properties = new List<PropertyMetaData>
                    {
                        new PropertyMetaData { Name = property, Type = type },
                    },
                }
            },
        };
        return new PipelineBuilder(new LoggerFactory())
            .AddFlowElement(new StubRequestEngine(OtherSectionOnly, products))
            .AddFlowElement(engine)
            .Build();
    }

    /// <summary>
    /// A cloud request engine with a fixed product list and a fixed
    /// response, so the engine under test runs without the cloud service.
    /// </summary>
    private class StubRequestEngine
        : AspectEngineBase<CloudRequestData, IAspectPropertyMetaData>,
            ICloudRequestEngine
    {
        private readonly string _json;

        public StubRequestEngine(
            string json,
            IReadOnlyDictionary<string, ProductMetaData> publicProperties)
            : base(new LoggerFactory().CreateLogger<StubRequestEngine>(),
                CreateData)
        {
            _json = json;
            PublicProperties = publicProperties;
        }

        private static CloudRequestData CreateData(
            IPipeline pipeline,
            FlowElementBase<CloudRequestData, IAspectPropertyMetaData> element) =>
            new CloudRequestData(null, pipeline, element as IAspectEngine);

        public IReadOnlyDictionary<string, ProductMetaData> PublicProperties
        { get; }

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
