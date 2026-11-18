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

using FiftyOne.IpIntelligence.Engine.OnPremise.FlowElements;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using Constants = FiftyOne.IpIntelligence.TestHelpers.Constants;

namespace FiftyOne.IpIntelligence.OnPremise.Tests
{
    /// <summary>
    /// thread-safety.md, Element Data: "Each implementation MUST include a
    /// check, run by its automated tests, that processes every Flow Element
    /// in the repository, then writes to its Element Data and expects the
    /// error." This covers the on-premise IP Intelligence engine. It needs
    /// the data file, and is skipped with a message when it is missing.
    /// </summary>
    [TestClass]
    public class EveryFlowElementFinishedTests : TestsBase
    {
        [TestMethod]
        public void OnPremiseEngine_WriteAfterProcessingFails()
        {
            TestInitialize(PerformanceProfiles.LowMemory);
            using var data = Wrapper.Pipeline.CreateFlowData();
            data.AddEvidence("query.client-ip-51d", "185.28.167.77");
            data.Process();

            var ipData = data.GetFromElement(Wrapper.Engine);
            // Reading a value keeps it, which is not a write.
            _ = ipData.AsDictionary();
            Assert.ThrowsExactly<PipelineException>(
                () => ipData["written.after.processing"] = "value");
        }

        /// <summary>
        /// The on-premise engine is the only public Flow Element in its
        /// package, so a new one is checked too.
        /// </summary>
        [TestMethod]
        public void EveryFlowElement_IsChecked()
        {
            var missing = typeof(IpiOnPremiseEngine).Assembly
                .GetTypes()
                .Where(t => t.IsClass &&
                    t.IsAbstract == false &&
                    t.IsGenericTypeDefinition == false &&
                    t.IsPublic &&
                    IsFlowElement(t) &&
                    t != typeof(IpiOnPremiseEngine))
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