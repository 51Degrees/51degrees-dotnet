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

using FiftyOne.Pipeline.Engines;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace FiftyOne.DeviceDetection.Hash.Tests.FlowElements
{
    /// <summary>
    /// The match metric Properties belong to the Metrics component, and the
    /// engine lists that component with the components the data file gives.
    /// </summary>
    [TestClass]
    public class MetricsComponentTests : TestsBase
    {
        private const string MetricsComponent = "Metrics";

        private const string MetricsCategory = "Device Metrics";

        [TestMethod]
        public void MatchMetrics_InTheListedMetricsComponent()
        {
            InitWrapperAndUserAgents(PerformanceProfiles.MaxPerformance);
            var engine = Wrapper.Engine;
            var metrics = engine.Properties
                .Where(p => p.Category == MetricsCategory)
                .ToList();
            var anyMetrics = metrics.Count > 0;
            Assert.IsTrue(anyMetrics,
                "The engine lists no match metric Properties.");
            foreach (var property in metrics)
            {
                Assert.AreEqual(MetricsComponent, property.Component.Name,
                    $"'{property.Name}' is not in the Metrics component.");
            }
            var listed = engine.Components.Any(
                c => c.Name == MetricsComponent);
            Assert.IsTrue(listed,
                "The engine's components do not include Metrics.");
        }
    }
}
