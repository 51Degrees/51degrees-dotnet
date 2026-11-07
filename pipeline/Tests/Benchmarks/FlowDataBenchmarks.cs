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
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Benchmarks;

/// <summary>
/// What one request costs a pipeline of three aspect engines, being create
/// a flow data, add evidence, process, read a result and dispose. Also the
/// cache key a results cache makes for every request.
/// </summary>
[MemoryDiagnoser]
public class FlowDataBenchmarks
{
    private const string EvidenceKey = "query.value";

    private IPipeline _pipeline;
    private ValueEngine _first;
    private IFlowData _keyData;
    private IEvidenceKeyFilter _keyFilter;

    [GlobalSetup]
    public void Setup()
    {
        _first = new ValueEngine("first");
        _pipeline = new PipelineBuilder(NullLoggerFactory.Instance)
            .AddFlowElement(_first)
            .AddFlowElement(new ValueEngine("second"))
            .AddFlowElement(new ValueEngine("third"))
            .Build();

        // Evidence as a web request carries it, of which the filter keeps
        // the headers and the query, in an order of its own.
        _keyFilter = new EvidenceKeyFilterWhitelist(
            new Dictionary<string, int>()
            {
                { "header.user-agent", 0 },
                { "header.sec-ch-ua", 1 },
                { "header.sec-ch-ua-platform", 1 },
                { "header.sec-ch-ua-mobile", 1 },
                { "query.client-ip", 2 },
            },
            StringComparer.OrdinalIgnoreCase);
        _keyData = _pipeline.CreateFlowData();
        _keyData.AddEvidence("header.user-agent", "Mozilla/5.0");
        _keyData.AddEvidence("header.sec-ch-ua", "\"Chromium\";v=\"142\"");
        _keyData.AddEvidence("header.sec-ch-ua-platform", "\"Windows\"");
        _keyData.AddEvidence("header.sec-ch-ua-mobile", "?0");
        _keyData.AddEvidence("header.accept", "text/html");
        _keyData.AddEvidence("cookie.session", "abc");
        _keyData.AddEvidence("query.client-ip", "185.28.167.77");
        _keyData.AddEvidence("server.client-ip", "185.28.167.77");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _keyData.Dispose();
        _pipeline.Dispose();
    }

    [Benchmark]
    public object Cycle()
    {
        using (var data = _pipeline.CreateFlowData())
        {
            data.AddEvidence(EvidenceKey, "1");
            data.Process();
            return data.GetFromElement(_first)[ValueEngine.Property];
        }
    }

    [Benchmark]
    public DataKey GenerateKey()
    {
        return _keyData.GenerateKey(_keyFilter);
    }

    /// <summary>
    /// The data <see cref="ValueEngine"/> writes.
    /// </summary>
    public class ValueData : AspectDataBase
    {
        public ValueData(IPipeline pipeline, IAspectEngine engine)
            : base(
                NullLogger<AspectDataBase>.Instance,
                pipeline,
                engine,
                MissingPropertyService.Instance)
        {
        }
    }

    /// <summary>
    /// An aspect engine that writes one value for the evidence, so the
    /// cost measured is the pipeline's rather than the engine's.
    /// </summary>
    public class ValueEngine :
        AspectEngineBase<ValueData, IAspectPropertyMetaData>
    {
        public const string Property = "value";

        private readonly string _key;
        private readonly IList<IAspectPropertyMetaData> _properties;
        private readonly IEvidenceKeyFilter _filter =
            new EvidenceKeyFilterWhitelist(new List<string>() { EvidenceKey });

        public ValueEngine(string key)
            : base(
                NullLogger<AspectEngineBase<ValueData,
                    IAspectPropertyMetaData>>.Instance,
                (pipeline, engine) =>
                    new ValueData(pipeline, (IAspectEngine)engine))
        {
            _key = key;
            _properties = new List<IAspectPropertyMetaData>()
            {
                new AspectPropertyMetaData(
                    this,
                    Property,
                    typeof(string),
                    "",
                    new List<string>(),
                    true)
            };
        }

        public override string ElementDataKey => _key;

        public override IEvidenceKeyFilter EvidenceKeyFilter => _filter;

        public override IList<IAspectPropertyMetaData> Properties =>
            _properties;

        public override string DataSourceTier => "benchmark";

        protected override void ProcessEngine(
            IFlowData data,
            ValueData aspectData)
        {
            data.TryGetEvidence(EvidenceKey, out string value);
            aspectData[Property] = value;
        }

        protected override void UnmanagedResourcesCleanup()
        {
        }
    }
}
