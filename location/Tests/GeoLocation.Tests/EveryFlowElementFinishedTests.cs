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

using FiftyOne.GeoLocation.Cloud;
using FiftyOne.GeoLocation.Cloud.FlowElements;
using FiftyOne.GeoLocation.Core;
using FiftyOne.Pipeline.CloudRequestEngine.Data;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.GeoLocation.Tests;

/// <summary>
/// thread-safety.md, Element Data: "Each implementation MUST include a
/// check, run by its automated tests, that processes every Flow Element in
/// the repository, then writes to its Element Data and expects the error."
/// This covers the Flow Elements of the location packages.
/// </summary>
[TestClass]
public class EveryFlowElementFinishedTests
{
    private static IEnumerable<(Type Type, Func<IFlowElement> Build)> Cases()
    {
        foreach (GeoLocationProvider provider in
            Enum.GetValues(typeof(GeoLocationProvider)))
        {
            yield return (typeof(GeoLocationCloudEngine), () =>
                new GeoLocationCloudEngineBuilder(NullLoggerFactory.Instance)
                    .Build(provider));
        }
    }

    [TestMethod]
    public void EveryFlowElement_WriteAfterProcessingFails()
    {
        var failures = new List<string>();
        foreach (var (type, build) in Cases())
        {
            var engine = build();
            using var pipeline = new PipelineBuilder(NullLoggerFactory.Instance)
                .SetSuppressProcessExceptions(true)
                .AddFlowElement(new StubRequestEngine(engine.ElementDataKey))
                .AddFlowElement(engine)
                .Build();
            using var data = pipeline.CreateFlowData();
            data.Process();
            if (data.TryGet(engine.ElementDataKey, out var elementData) ==
                false || elementData == null)
            {
                failures.Add($"{type.Name} added no Element Data");
                continue;
            }
            try
            {
                elementData["written.after.processing"] = "value";
                failures.Add($"{type.Name} {engine.ElementDataKey}: a " +
                    "write after processing succeeded");
            }
            catch (PipelineException)
            {
                // Expected.
            }
        }
        Assert.IsEmpty(failures,
            string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Every Flow Element in the location packages is checked, so a new
    /// one is checked too.
    /// </summary>
    [TestMethod]
    public void EveryFlowElement_IsChecked()
    {
        var checkedTypes = new HashSet<Type>(Cases().Select(c => c.Type));
        var missing = new[]
            {
                typeof(GeoLocationCloudEngine).Assembly,
                typeof(GeoLocationProvider).Assembly,
            }
            .Distinct()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass &&
                t.IsAbstract == false &&
                t.IsGenericTypeDefinition == false &&
                t.IsPublic &&
                IsFlowElement(t) &&
                checkedTypes.Contains(t) == false)
            .Select(t => t.FullName)
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
    /// A cloud request engine whose product list has the engine's product
    /// with one property, and whose response has a value for it.
    /// </summary>
    private class StubRequestEngine
        : AspectEngineBase<CloudRequestData, IAspectPropertyMetaData>,
            ICloudRequestEngine
    {
        private readonly string _json;

        public StubRequestEngine(string product)
            : base(NullLoggerFactory.Instance.CreateLogger<StubRequestEngine>(),
                CreateData)
        {
            _json = "{ \"" + product + "\": { \"country\": \"UK\" } }";
            PublicProperties = new Dictionary<string, ProductMetaData>
            {
                {
                    product,
                    new ProductMetaData
                    {
                        Properties = new List<PropertyMetaData>
                        {
                            new PropertyMetaData
                            {
                                Name = "Country",
                                Type = "String",
                            },
                        },
                    }
                },
            };
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
