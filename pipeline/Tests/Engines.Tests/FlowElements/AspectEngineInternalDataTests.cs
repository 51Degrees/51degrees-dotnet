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

using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Engines.Tests.FlowElements;

/// <summary>
/// What an aspect engine does with data that does not derive from
/// <see cref="AspectDataBase"/>, and the list
/// <see cref="IAspectEngine.Properties"/> returns.
/// </summary>
[TestClass]
public class AspectEngineInternalDataTests
{
    private class Engine :
        AspectEngineBase<IAspectData, IAspectPropertyMetaData>
    {
        public Engine(Func<IPipeline, IAspectData> factory)
            : base(
                NullLogger<AspectEngineBase<IAspectData,
                    IAspectPropertyMetaData>>.Instance,
                (pipeline, engine) => factory(pipeline))
        {
        }

        public IList<IAspectPropertyMetaData> Current { get; set; } =
            new List<IAspectPropertyMetaData>();

        public int Processed { get; private set; }

        public override string DataSourceTier => "test";

        public override string ElementDataKey => "internal";

        public override IEvidenceKeyFilter EvidenceKeyFilter =>
            new EvidenceKeyFilterWhitelist(new List<string>());

        public override IList<IAspectPropertyMetaData> Properties => Current;

        protected override void ProcessEngine(
            IFlowData data,
            IAspectData aspectData)
        {
            Processed++;
        }

        protected override void UnmanagedResourcesCleanup() { }
    }

    /// <summary>
    /// Data that implements <see cref="IAspectData"/> directly, which the
    /// interface allows.
    /// </summary>
    private static IAspectData PlainData(params IAspectEngine[] engines)
    {
        var data = new Mock<IAspectData>();
        data.SetupGet(d => d.Engines).Returns(engines);
        return data.Object;
    }

    private static IFlowData FlowDataFor(Engine engine)
    {
        var pipeline = new PipelineBuilder(NullLoggerFactory.Instance)
            .AddFlowElement(engine)
            .Build();
        return pipeline.CreateFlowData();
    }

    /// <summary>
    /// Lazy loading needs the data to hold the process task, so data that
    /// cannot fails naming its type rather than going on without it.
    /// </summary>
    [TestMethod]
    public void AspectEngine_LazyLoadingPlainData_FailsNamingTheType()
    {
        IAspectData data = null;
        Engine engine = null;
        engine = new Engine(p => data = PlainData(engine));
        engine.SetLazyLoading(new LazyLoadingConfiguration(1000));
        using var flowData = FlowDataFor(engine);

        var ex = Assert.ThrowsExactly<PipelineException>(() =>
            engine.Process(flowData));

        StringAssert.Contains(ex.Message, data.GetType().FullName);
        Assert.AreEqual(0, engine.Processed,
            "No processing should start for data that cannot hold it.");
    }

    /// <summary>
    /// Data the engine did not create, and so does not list it, fails
    /// naming its type when the engine adds itself.
    /// </summary>
    [TestMethod]
    public void AspectEngine_PlainDataWithoutTheEngine_FailsNamingTheType()
    {
        IAspectData data = null;
        var engine = new Engine(p => data = PlainData());
        using var flowData = FlowDataFor(engine);

        var ex = Assert.ThrowsExactly<PipelineException>(() =>
            engine.Process(flowData));

        StringAssert.Contains(ex.Message, data.GetType().FullName);
    }

    /// <summary>
    /// Data that lists the engine, with no lazy loading or cache, needs
    /// nothing else from it, so it is processed as before.
    /// </summary>
    [TestMethod]
    public void AspectEngine_PlainDataListingTheEngine_IsProcessed()
    {
        Engine engine = null;
        engine = new Engine(p => PlainData(engine));
        using var flowData = FlowDataFor(engine);

        engine.Process(flowData);

        Assert.AreEqual(1, engine.Processed);
    }

    [TestMethod]
    public void AspectEngine_Properties_SameListReturnsSameInstance()
    {
        var engine = new Engine(p => PlainData());
        IAspectEngine aspectEngine = engine;

        Assert.AreSame(aspectEngine.Properties, aspectEngine.Properties);
    }

    [TestMethod]
    public void AspectEngine_Properties_NewListReturnsNewInstance()
    {
        var engine = new Engine(p => PlainData());
        IAspectEngine aspectEngine = engine;
        var first = aspectEngine.Properties;

        engine.Current = new List<IAspectPropertyMetaData>()
        {
            new AspectPropertyMetaData(
                engine, "one", typeof(string), "", new List<string>(), true)
        };
        var second = aspectEngine.Properties;

        Assert.AreNotSame(first, second);
        Assert.AreEqual(0, first.Count);
        Assert.AreEqual(1, second.Count);
    }

    [TestMethod]
    public void AspectEngine_Properties_IsReadOnly()
    {
        var engine = new Engine(p => PlainData());
        IAspectEngine aspectEngine = engine;

        Assert.ThrowsExactly<NotSupportedException>(() =>
            aspectEngine.Properties.Add(null));
    }
}
