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

using FiftyOne.Pipeline.CloudRequestEngine.Data;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.CloudRequestEngine.Services;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines;
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading;

namespace FiftyOne.Pipeline.CloudRequestEngine.Tests;

/// <summary>
/// With the deprecated lazy loading option, a cloud aspect engine can
/// find that the cloud request failed after the Flow Data has finished
/// processing. The reader of a property still learns that the cloud
/// request failed, and not that the Flow Data is read only.
/// </summary>
[TestClass]
public class CloudAspectEngineLazyFailureTests
{
    private const string Product = "test";

    private sealed class Data : AspectDataBase
    {
        public Data(IPipeline pipeline, IAspectEngine engine)
            : base(null, pipeline, engine, MissingPropertyServiceCloud.Instance)
        {
        }
    }

    /// <summary>
    /// A cloud aspect engine whose processing waits until the test lets
    /// it go, so that it runs after the Flow Data has finished.
    /// </summary>
    private sealed class LateEngine : CloudAspectEngineBase<Data>
    {
        public LateEngine()
            : base(NullLogger<LateEngine>.Instance, CreateData)
        {
        }

        public ManualResetEventSlim Gate { get; } =
            new ManualResetEventSlim(false);

        public override string ElementDataKey => Product;

        public override IEvidenceKeyFilter EvidenceKeyFilter { get; } =
            new EvidenceKeyFilterWhitelist(new List<string>());

        private static Data CreateData(
            IPipeline pipeline,
            FlowElementBase<Data, IAspectPropertyMetaData> engine)
            => new Data(pipeline, (IAspectEngine)engine);

        protected override void ProcessEngine(IFlowData data, Data aspectData)
        {
            Gate.Wait();
            base.ProcessEngine(data, aspectData);
        }
    }

    /// <summary>
    /// Stands in for a cloud request engine whose request failed, so it
    /// gives no response.
    /// </summary>
    private sealed class FailedRequestEngine
        : AspectEngineBase<CloudRequestData, IAspectPropertyMetaData>,
            ICloudRequestEngine
    {
        public FailedRequestEngine()
            : base(NullLogger<FailedRequestEngine>.Instance, CreateData)
        {
        }

        private static CloudRequestData CreateData(
            IPipeline pipeline,
            FlowElementBase<CloudRequestData, IAspectPropertyMetaData> engine)
            => new CloudRequestData(null, pipeline, (IAspectEngine)engine);

        public IReadOnlyDictionary<string, ProductMetaData> PublicProperties
        { get; } = new Dictionary<string, ProductMetaData>()
        {
            [Product] = new ProductMetaData()
            {
                DataTier = "tier",
                Properties = new List<PropertyMetaData>()
                {
                    new PropertyMetaData() { Name = "ismobile", Type = "Boolean" },
                },
            },
        };

        public override string DataSourceTier => string.Empty;

        public override string ElementDataKey => "cloud";

        public override IEvidenceKeyFilter EvidenceKeyFilter { get; } =
            new EvidenceKeyFilterWhitelist(new List<string>());

        public override IList<IAspectPropertyMetaData> Properties { get; } =
            new List<IAspectPropertyMetaData>();

        protected override void ProcessEngine(
            IFlowData data, CloudRequestData aspectData)
        {
            aspectData.ProcessStarted = true;
            aspectData.JsonResponse = null;
        }

        protected override void UnmanagedResourcesCleanup()
        {
        }
    }

    /// <summary>
    /// The reader of a property gets the cloud request failure, even
    /// though the engine found it after processing had finished.
    /// </summary>
    [TestMethod]
    public void LazyLoading_FailureFoundAfterProcessing_ReachesTheReader()
    {
        var engine = new LateEngine();
        engine.SetLazyLoading(new LazyLoadingConfiguration(10000));
        using var pipeline = new PipelineBuilder(NullLoggerFactory.Instance)
            .AddFlowElement(new FailedRequestEngine())
            .AddFlowElement(engine)
            .Build();
        using var flowData = pipeline.CreateFlowData();

        flowData.AddEvidence("query.user-agent", "iPhone").Process();
        engine.Gate.Set();

        var thrown = Assert.Throws<Exception>(
            () => flowData.Get(Product)["ismobile"]);
        Assert.IsInstanceOfType<PropertyMissingException>(
            thrown, thrown.ToString());
        Assert.AreEqual(
            MissingPropertyReason.CloudRequestFailed,
            ((PropertyMissingException)thrown).Reason);
    }
}
