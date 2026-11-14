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

using FiftyOne.Common.TestHelpers;
using FiftyOne.Pipeline.CloudRequestEngine.Data;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.CloudRequestEngine.Services;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.Pipeline.CloudRequestEngine.Tests;

/// <summary>
/// A valid resource key can carry no product for a cloud aspect engine.
/// The Pipeline still builds, the engine has no properties and logs that
/// once, and nothing is read from the response for it. See the start-up
/// activity section of the cloud aspect engine specification.
/// </summary>
[TestClass]
public class CloudAspectEngineMissingProductTests
{
    /// <summary>
    /// The product, and element data key, of the engine under test.
    /// </summary>
    private const string Product = "test";

    /// <summary>
    /// The text of the warning logged when the product is missing.
    /// </summary>
    private const string MissingProductLogText = "could not be loaded";

    /// <summary>
    /// How many requests the tests that process send through a Pipeline.
    /// </summary>
    private const int RequestCount = 3;

    /// <summary>
    /// A response that holds data for a product other than the engine's,
    /// which is what the cloud returns for a key without the product.
    /// </summary>
    private const string OtherProductResponse =
        "{ \"other\": { \"ismobile\": true } }";

    /// <summary>
    /// A response that holds data for the engine's product.
    /// </summary>
    private const string ProductResponse =
        "{ \"test\": { \"ismobile\": true } }";

    /// <summary>
    /// Aspect data that reports missing properties through the cloud
    /// missing property service, as the cloud engines' data does.
    /// </summary>
    private sealed class RecordingData : AspectDataBase
    {
        public RecordingData(IPipeline pipeline, IAspectEngine engine)
            : base(null, pipeline, engine, MissingPropertyServiceCloud.Instance)
        {
        }
    }

    /// <summary>
    /// A cloud aspect engine that logs to the factory it is given and
    /// counts the responses it reads.
    /// </summary>
    private sealed class RecordingEngine : CloudAspectEngineBase<RecordingData>
    {
        public RecordingEngine(ILoggerFactory loggerFactory)
            : base(loggerFactory.CreateLogger<RecordingEngine>(), CreateData)
        {
        }

        public int ResponsesRead { get; private set; }

        public override string ElementDataKey => Product;

        public override IEvidenceKeyFilter EvidenceKeyFilter { get; } =
            new EvidenceKeyFilterWhitelist(new List<string>());

        private static RecordingData CreateData(
            IPipeline pipeline,
            FlowElementBase<RecordingData, IAspectPropertyMetaData> engine)
            => new RecordingData(pipeline, (IAspectEngine)engine);

        protected override void ProcessCloudEngine(
            IFlowData data, RecordingData aspectData, string json)
        {
            ResponsesRead++;
            var values = JObject.Parse(json)[Product]
                .ToObject<Dictionary<string, object>>();
            aspectData.PopulateFrom(
                CreateAPVDictionary(values, Properties.ToList()));
        }
    }

    /// <summary>
    /// Stands in for the cloud request engine, so a test can say exactly
    /// what the resource key carries and what the cloud returns.
    /// </summary>
    private sealed class StubRequestEngine
        : AspectEngineBase<CloudRequestData, IAspectPropertyMetaData>,
            ICloudRequestEngine
    {
        private readonly string _json;
        private readonly IReadOnlyDictionary<string, ProductMetaData>
            _publicProperties;
        private readonly Func<bool> _propertyRequestFails;

        public StubRequestEngine(
            IReadOnlyDictionary<string, ProductMetaData> publicProperties,
            string json,
            Func<bool> propertyRequestFails)
            : base(NullLogger<StubRequestEngine>.Instance, CreateData)
        {
            _publicProperties = publicProperties;
            _json = json;
            _propertyRequestFails = propertyRequestFails;
        }

        private static CloudRequestData CreateData(
            IPipeline pipeline,
            FlowElementBase<CloudRequestData, IAspectPropertyMetaData> engine)
            => new CloudRequestData(null, pipeline, (IAspectEngine)engine);

        /// <summary>
        /// Fails as a request to the cloud does while the test says so,
        /// and otherwise gives the accessible properties.
        /// </summary>
        public IReadOnlyDictionary<string, ProductMetaData> PublicProperties
        {
            get
            {
                if (_propertyRequestFails?.Invoke() == true)
                {
                    throw new CloudRequestException(
                        "The cloud could not be reached.");
                }
                return _publicProperties;
            }
        }

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
            aspectData.JsonResponse = _json;
        }

        protected override void UnmanagedResourcesCleanup()
        {
        }
    }

    private static ProductMetaData ProductWith(params string[] names) =>
        new ProductMetaData()
        {
            DataTier = "tier",
            Properties = names
                .Select(n => new PropertyMetaData() { Name = n, Type = "String" })
                .ToList(),
        };

    /// <summary>
    /// The accessible properties for each way a key can lack the product.
    /// </summary>
    private static IReadOnlyDictionary<string, ProductMetaData> Missing(
        string how) => how switch
        {
            "other product only" => new Dictionary<string, ProductMetaData>()
            {
                ["other"] = ProductWith("ismobile"),
            },
            "no products" => new Dictionary<string, ProductMetaData>(),
            "product with no properties" =>
                new Dictionary<string, ProductMetaData>()
                {
                    [Product] = ProductWith(),
                },
            "nothing returned" => null,
            _ => throw new AssertFailedException($"Unknown case '{how}'"),
        };

    private static IReadOnlyDictionary<string, ProductMetaData> Granted() =>
        new Dictionary<string, ProductMetaData>()
        {
            [Product] = ProductWith("ismobile"),
        };

    private static IPipeline Build(
        ILoggerFactory loggerFactory,
        IReadOnlyDictionary<string, ProductMetaData> publicProperties,
        string json,
        out RecordingEngine engine,
        Func<bool> propertyRequestFails = null)
    {
        engine = new RecordingEngine(loggerFactory);
        return new PipelineBuilder(loggerFactory)
            .AddFlowElement(new StubRequestEngine(
                publicProperties, json, propertyRequestFails))
            .AddFlowElement(engine)
            .Build();
    }

    private static IEnumerable<string> Logged(
        TestLoggerFactory loggerFactory, LogLevel level, string text) =>
        loggerFactory.Loggers
            .SelectMany(l => l.ExtendedEntries)
            .Where(e => e.LogLevel == level && e.Message.Contains(text))
            .Select(e => e.Message);

    /// <summary>
    /// Whichever way the product is missing, the Pipeline builds and the
    /// engine has loaded an empty list of properties, so the Pipeline
    /// offers nothing for it.
    /// </summary>
    [TestMethod]
    [DataRow("other product only")]
    [DataRow("no products")]
    [DataRow("product with no properties")]
    [DataRow("nothing returned")]
    public void MissingProduct_PipelineBuilds(string how)
    {
        using var pipeline = Build(
            new TestLoggerFactory(),
            Missing(how),
            OtherProductResponse,
            out var engine);

        Assert.IsTrue(engine.HasLoadedProperties,
            "the accessible properties were read, so the engine has " +
            "loaded its properties");
        Assert.AreEqual(0, engine.Properties.Count,
            "the key carries no properties for the engine");
        Assert.IsTrue(
            pipeline.ElementAvailableProperties.TryGetValue(
                Product, out var available),
            "the Pipeline lists the engine");
        Assert.AreEqual(0, available.Count,
            "the Pipeline offers nothing for the engine");
    }

    /// <summary>
    /// The missing product is logged once, when the Pipeline is built,
    /// however often the properties are read and however many requests
    /// are processed.
    /// </summary>
    [TestMethod]
    public void MissingProduct_LoggedOnceAtBuild()
    {
        var loggerFactory = new TestLoggerFactory();
        using var pipeline = Build(
            loggerFactory,
            Missing("other product only"),
            OtherProductResponse,
            out var engine);

        var atBuild = Logged(
            loggerFactory, LogLevel.Warning, MissingProductLogText).ToList();
        Assert.AreEqual(1, atBuild.Count,
            "the missing product is logged when the Pipeline is built");
        Assert.Contains($"'{Product}'", atBuild[0],
            "the log names the product");

        for (var i = 0; i < RequestCount; i++)
        {
            _ = engine.Properties;
            _ = pipeline.ElementAvailableProperties;
            using var data = pipeline.CreateFlowData();
            data.Process();
        }

        Assert.AreEqual(1,
            Logged(loggerFactory, LogLevel.Warning, MissingProductLogText)
                .Count(),
            "the missing product is logged only once");
    }

    /// <summary>
    /// The request for the property list fails while the Pipeline is
    /// built, so the engine has no property list then. The first response
    /// makes it read the list, which shows the product is missing, so it
    /// does not read a response that holds nothing for it and records no
    /// error.
    /// </summary>
    [TestMethod]
    public void PropertyRequestFailedAtBuild_MissingProduct_ResponseNotRead()
    {
        var loggerFactory = new TestLoggerFactory();
        var cloudUnreachable = true;
        using var pipeline = Build(
            loggerFactory,
            Missing("other product only"),
            OtherProductResponse,
            out var engine,
            () => cloudUnreachable);
        Assert.IsFalse(engine.HasLoadedProperties,
            "the property list was not read while the Pipeline was built");
        cloudUnreachable = false;

        for (var i = 0; i < RequestCount; i++)
        {
            using var data = pipeline.CreateFlowData();
            data.Process();
            Assert.IsFalse(data.Errors?.Any() == true,
                "a response with nothing for the engine is not an error");
        }

        Assert.AreEqual(0, engine.ResponsesRead,
            "there is nothing in the response for the engine to read");
        Assert.IsTrue(engine.HasLoadedProperties,
            "the first response made the engine read the property list");
        Assert.AreEqual(1,
            Logged(loggerFactory, LogLevel.Warning, MissingProductLogText)
                .Count(),
            "the missing product is logged once");
    }

    /// <summary>
    /// The response holds nothing for a product the key does not carry,
    /// so the engine does not read it, records no error, and a property
    /// read from its data says the resource key does not include the
    /// product.
    /// </summary>
    [TestMethod]
    public void MissingProduct_ResponseNotRead()
    {
        using var pipeline = Build(
            new TestLoggerFactory(),
            Missing("other product only"),
            OtherProductResponse,
            out var engine);

        using var data = pipeline.CreateFlowData();
        data.Process();

        Assert.AreEqual(0, engine.ResponsesRead,
            "there is nothing in the response for the engine to read");
        Assert.IsFalse(data.Errors?.Any() == true,
            "a key without the product is not an error on each request");
        var missing = Assert.ThrowsExactly<PropertyMissingException>(
            () => _ = data.GetFromElement(engine)["ismobile"]);
        Assert.AreEqual(
            MissingPropertyReason.ProductNotAccessibleWithResourceKey,
            missing.Reason);
    }

    /// <summary>
    /// The control for the tests above. With the product granted, every
    /// response is read and nothing is logged as an error.
    /// </summary>
    [TestMethod]
    public void GrantedProduct_ResponseRead()
    {
        var loggerFactory = new TestLoggerFactory();
        using var pipeline = Build(
            loggerFactory, Granted(), ProductResponse, out var engine);

        for (var i = 0; i < RequestCount; i++)
        {
            using var data = pipeline.CreateFlowData();
            data.Process();
        }

        Assert.AreEqual(RequestCount, engine.ResponsesRead);
        Assert.AreEqual(1, engine.Properties.Count);
        Assert.IsFalse(
            Logged(loggerFactory, LogLevel.Error, string.Empty).Any(),
            "nothing is wrong, so nothing is logged as an error");
    }
}
