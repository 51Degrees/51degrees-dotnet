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
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.DeviceDetection.Cloud.Tests;

/// <summary>
/// thread-safety.md, Element Data: "Each implementation MUST include a
/// check, run by its automated tests, that processes every Flow Element in
/// the repository, then writes to its Element Data and expects the error."
/// This covers the Flow Elements of the Device Detection cloud package. A stub cloud request
/// engine gives a response with no section for them, so no cloud service
/// is needed.
/// </summary>
[TestClass]
public class EveryFlowElementFinishedTests
{
    private const string OtherSectionOnly =
        "{ \"other\": { \"value\": true } }";

    /// <summary>
    /// Each Flow Element checked.
    /// </summary>
    private static IEnumerable<(Type Type, Func<IFlowElement> Build)> Cases()
    {
        var logger = NullLoggerFactory.Instance;
        yield return (typeof(DeviceDetectionCloudEngine), () =>
            new DeviceDetectionCloudEngineBuilder(logger).Build());
        yield return (typeof(HardwareProfileCloudEngine), () =>
            new HardwareProfileCloudEngineBuilder(logger).Build());
    }

    /// <summary>
    /// Every Flow Element is processed, then a write to its Element Data
    /// fails.
    /// </summary>
    [TestMethod]
    public void EveryFlowElement_WriteAfterProcessingFails()
    {
        var failures = new List<string>();
        foreach (var (type, build) in Cases())
        {
            var element = build();
            Assert.AreEqual(type, element.GetType());
            var products = new Dictionary<string, ProductMetaData>
            {
                {
                    element.ElementDataKey,
                    new ProductMetaData
                    {
                        Properties = new List<PropertyMetaData>
                        {
                            new PropertyMetaData
                            {
                                Name = "value",
                                Type = "String",
                            },
                        },
                    }
                },
            };
            using var pipeline = new PipelineBuilder(
                    NullLoggerFactory.Instance)
                .AddFlowElement(new StubRequestEngine(products))
                .AddFlowElement(element)
                .Build();
            using var data = pipeline.CreateFlowData();
            data.Process();
            if (data.TryGet(element.ElementDataKey, out var elementData) ==
                false || elementData == null)
            {
                failures.Add($"{type.Name} added no Element Data");
                continue;
            }
            try
            {
                elementData["written.after.processing"] = "value";
                failures.Add($"{type.Name}: a write after processing " +
                    "succeeded");
            }
            catch (PipelineException)
            {
                // Expected.
            }
        }
        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Every public Flow Element in the package is checked, so a new one is
    /// checked too.
    /// </summary>
    [TestMethod]
    public void EveryFlowElement_IsChecked()
    {
        var checkedTypes = new HashSet<Type>(Cases().Select(c => c.Type));
        var missing = typeof(DeviceDetectionCloudEngine).Assembly
            .GetTypes()
            .Where(t => t.IsClass &&
                t.IsAbstract == false &&
                t.IsGenericTypeDefinition == false &&
                t.IsPublic &&
                IsFlowElement(t) &&
                checkedTypes.Contains(t) == false)
            .Select(t => t.FullName)
            .OrderBy(n => n)
            .ToList();
        Assert.IsEmpty(missing,
            "These Flow Elements are not checked: " +
            string.Join(", ", missing));
    }

    private static bool IsFlowElement(Type type)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            if (t.IsGenericType &&
                t.GetGenericTypeDefinition() == typeof(FlowElementBase<,>))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// A cloud request engine with a fixed product list and a response with
    /// no section for the engine under test.
    /// </summary>
    private class StubRequestEngine
        : AspectEngineBase<CloudRequestData, IAspectPropertyMetaData>,
            ICloudRequestEngine
    {
        public StubRequestEngine(
            IReadOnlyDictionary<string, ProductMetaData> publicProperties)
            : base(NullLogger<StubRequestEngine>.Instance, CreateData)
        {
            PublicProperties = publicProperties;
        }

        private static CloudRequestData CreateData(
            IPipeline pipeline,
            FlowElementBase<CloudRequestData, IAspectPropertyMetaData>
                element) =>
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
            aspectData.JsonResponse = OtherSectionOnly;

        protected override void UnmanagedResourcesCleanup() { }
    }
}