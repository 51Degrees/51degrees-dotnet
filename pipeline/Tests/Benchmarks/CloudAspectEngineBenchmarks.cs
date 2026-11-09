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

using BenchmarkDotNet.Attributes;
using FiftyOne.Pipeline.CloudRequestEngine.Data;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.Pipeline.Benchmarks;

/// <summary>
/// What one request costs a cloud aspect engine that reads a canned cloud
/// answer the way the device detection cloud engine does, without calling
/// the cloud.
/// </summary>
[MemoryDiagnoser]
public class CloudAspectEngineBenchmarks
{
    private const string ElementKey = "canned";
    private const int PropertyCount = 40;

    private IPipeline _pipeline;
    private CannedEngine _engine;

    [GlobalSetup]
    public void Setup()
    {
        // Strings, booleans and integers in turn, with every fifth value
        // missing and given a reason, as the cloud writes them.
        var products = new Dictionary<string, ProductMetaData>();
        var properties = new List<PropertyMetaData>();
        var values = new Dictionary<string, object>();
        string[] types = { "String", "Boolean", "Int32" };
        for (var i = 0; i < PropertyCount; i++)
        {
            var name = "property" + i;
            var type = types[i % types.Length];
            properties.Add(new PropertyMetaData() { Name = name, Type = type });
            if (i % 5 == 4)
            {
                values[name] = null;
                values[name + "nullreason"] = "Not in this answer.";
            }
            else
            {
                values[name] = type == "String" ? "value" + i :
                    type == "Boolean" ? (object)(i % 2 == 0) : i;
            }
        }
        products[ElementKey] = new ProductMetaData()
        {
            DataTier = "benchmark",
            Properties = properties,
        };
        var json = JsonConvert.SerializeObject(
            new Dictionary<string, object>() { { ElementKey, values } });

        _engine = new CannedEngine();
        _pipeline = new PipelineBuilder(NullLoggerFactory.Instance)
            .AddFlowElement(new CannedRequestEngine(products, json))
            .AddFlowElement(_engine)
            .Build();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _pipeline.Dispose();
    }

    [Benchmark]
    public object Process()
    {
        using (var data = _pipeline.CreateFlowData())
        {
            data.AddEvidence("query.user-agent", "Mozilla/5.0");
            data.Process();
            return data.GetFromElement(_engine)["property0"];
        }
    }

    /// <summary>
    /// Stands in for the cloud request engine, answering every request
    /// with the same JSON.
    /// </summary>
    public class CannedRequestEngine :
        AspectEngineBase<CloudRequestData, IAspectPropertyMetaData>,
        ICloudRequestEngine
    {
        private readonly string _json;
        private readonly IEvidenceKeyFilter _filter =
            new EvidenceKeyFilterWhitelist(
                new List<string>() { "query.user-agent" });

        public CannedRequestEngine(
            IReadOnlyDictionary<string, ProductMetaData> products,
            string json)
            : base(
                NullLogger<AspectEngineBase<CloudRequestData,
                    IAspectPropertyMetaData>>.Instance,
                (pipeline, engine) => new CloudRequestData(
                    NullLogger<AspectDataBase>.Instance,
                    pipeline,
                    (IAspectEngine)engine))
        {
            PublicProperties = products;
            _json = json;
        }

        public IReadOnlyDictionary<string, ProductMetaData>
            PublicProperties { get; }

        public override string ElementDataKey => "cloud";

        public override IEvidenceKeyFilter EvidenceKeyFilter => _filter;

        public override IList<IAspectPropertyMetaData> Properties { get; } =
            new List<IAspectPropertyMetaData>();

        public override string DataSourceTier => "benchmark";

        protected override void ProcessEngine(
            IFlowData data,
            CloudRequestData aspectData)
        {
            aspectData.ProcessStarted = true;
            aspectData.JsonResponse = _json;
        }

        protected override void UnmanagedResourcesCleanup()
        {
        }
    }

    /// <summary>
    /// The data <see cref="CannedEngine"/> writes.
    /// </summary>
    public class CannedData : AspectDataBase
    {
        public CannedData(IPipeline pipeline, IAspectEngine engine)
            : base(NullLogger<AspectDataBase>.Instance, pipeline, engine)
        {
        }
    }

    /// <summary>
    /// Reads its element from the answer as the device detection cloud
    /// engine does, passing a copy of its properties to
    /// <see cref="CloudAspectEngineBase{T}.CreateAPVDictionary"/>.
    /// </summary>
    public class CannedEngine : CloudAspectEngineBase<CannedData>
    {
        private static readonly JsonSerializerSettings Settings =
            new JsonSerializerSettings()
            {
                Converters = new JsonConverter[] { new CloudJsonConverter() },
            };

        private readonly IEvidenceKeyFilter _filter =
            new EvidenceKeyFilterWhitelist(new List<string>());

        public CannedEngine()
            : base(
                NullLogger<AspectEngineBase<CannedData,
                    IAspectPropertyMetaData>>.Instance,
                (pipeline, engine) =>
                    new CannedData(pipeline, (IAspectEngine)engine))
        {
        }

        public override string ElementDataKey => ElementKey;

        public override IEvidenceKeyFilter EvidenceKeyFilter => _filter;

        protected override void ProcessCloudEngine(
            IFlowData data,
            CannedData aspectData,
            string json)
        {
            var answer = JsonConvert
                .DeserializeObject<Dictionary<string, object>>(json);
            var values = JsonConvert
                .DeserializeObject<Dictionary<string, object>>(
                    answer[ElementKey].ToString(), Settings);
            aspectData.PopulateFrom(
                CreateAPVDictionary(values, Properties.ToList()));
        }

        protected override Type GetPropertyType(
            PropertyMetaData propertyMetaData,
            Type parentObjectType)
        {
            return typeof(IAspectPropertyValue<>).MakeGenericType(
                base.GetPropertyType(propertyMetaData, parentObjectType));
        }
    }
}
