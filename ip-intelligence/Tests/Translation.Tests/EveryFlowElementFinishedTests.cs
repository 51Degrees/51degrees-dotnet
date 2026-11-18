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

using FiftyOne.IpIntelligence.Translation.FlowElements;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.IpIntelligence.Translation.Tests
{
    /// <summary>
    /// thread-safety.md, Element Data: "Each implementation MUST include a
    /// check, run by its automated tests, that processes every Flow Element
    /// in the repository, then writes to its Element Data and expects the
    /// error." This covers the Flow Elements of the IP Intelligence
    /// translation package.
    /// </summary>
    [TestClass]
    public class EveryFlowElementFinishedTests
    {
        /// <summary>
        /// Stands in for the IP Intelligence engine, giving two country
        /// codes.
        /// </summary>
        private static IFlowElement IpElement()
        {
            IAspectPropertyValue<IReadOnlyList<IWeightedValue<string>>>
                codes = new AspectPropertyValue<
                    IReadOnlyList<IWeightedValue<string>>>(
                    new List<IWeightedValue<string>>
                    {
                        new WeightedValue<string>(40000, "GB"),
                        new WeightedValue<string>(25535, "FR"),
                    });
            var ipData = new Mock<IIpIntelligenceData>();
            ipData.Setup(i => i["CountryCodesGeographical"]).Returns(codes);
            ipData.Setup(i => i["CountryCodesPopulation"]).Returns(codes);
            ipData.Setup(i => i.CountryCodesGeographical).Returns(codes);
            ipData.Setup(i => i.CountryCodesPopulation).Returns(codes);
            var element = new Mock<IFlowElement>();
            element.SetupGet(i => i.Properties)
                .Returns(new List<IElementPropertyMetaData>());
            element.SetupGet(i => i.ElementDataKey).Returns("ip");
            element.Setup(i => i.Process(It.IsAny<IFlowData>()))
                .Callback<IFlowData>(data =>
                    data.GetOrAdd("ip", p => ipData.Object));
            return element.Object;
        }

        /// <summary>
        /// Each Flow Element checked, with the elements a Pipeline needs
        /// before it, the last being the one checked.
        /// </summary>
        private static IEnumerable<(Type Type, Func<IFlowElement[]> Build)>
            Cases()
        {
            var logger = NullLoggerFactory.Instance;
            yield return (typeof(CountryCodeTranslationEngine), () =>
                new[]
                {
                    IpElement(),
                    new CountryCodeTranslationEngineBuilder(logger).Build(),
                });
            yield return (typeof(CountriesTranslationEngine), () =>
                new[]
                {
                    IpElement(),
                    new CountryCodeTranslationEngineBuilder(logger).Build(),
                    new CountriesTranslationEngineBuilder(logger).Build(),
                });
        }

        /// <summary>
        /// Every Flow Element is processed, then a write to its Element
        /// Data fails.
        /// </summary>
        [TestMethod]
        public void EveryFlowElement_WriteAfterProcessingFails()
        {
            var failures = new List<string>();
            foreach (var (type, build) in Cases())
            {
                var elements = build();
                var element = elements.Last();
                Assert.AreEqual(type, element.GetType());
                using var pipeline = elements
                    .Aggregate(
                        new PipelineBuilder(NullLoggerFactory.Instance),
                        (b, e) => b.AddFlowElement(e))
                    .Build();
                using var data = pipeline.CreateFlowData();
                data.AddEvidence("header.Accept-Language", "fr_FR");
                data.Process();
                if (data.TryGet(element.ElementDataKey, out var elementData)
                    == false || elementData == null)
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
            Assert.IsEmpty(failures,
                string.Join(Environment.NewLine, failures));
        }

        /// <summary>
        /// Every public Flow Element in the translation package is checked,
        /// so a new one is checked too.
        /// </summary>
        [TestMethod]
        public void EveryFlowElement_IsChecked()
        {
            var checkedTypes = new HashSet<Type>(Cases().Select(c => c.Type));
            var missing = typeof(CountryCodeTranslationEngine).Assembly
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
                    t.GetGenericTypeDefinition() ==
                        typeof(FlowElementBase<,>))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
