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

using FiftyOne.Pipeline.AgentSignature.Data;
using FiftyOne.Pipeline.AgentSignature.Tests.Helpers;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Core.TypedMap;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.AgentSignature.Tests
{
    /// <summary>
    /// The specification's Process section: "Nearly every request carries
    /// no signature at all, so the Element MUST test for the two signature
    /// headers before it parses or allocates anything, and MUST answer that
    /// case from values that do not change between requests."
    /// </summary>
    [TestClass]
    public class NoSignatureAllocationTests
    {
        /// <summary>
        /// The number of requests measured together, so that the count is
        /// of the work each request does rather than of anything done once.
        /// </summary>
        private const int Requests = 200;

        /// <summary>
        /// Processing a request with no signature allocates nothing beyond
        /// what the flow data spends to hold an answer that already exists.
        /// The cost of holding it is measured the same way on the same
        /// kind of flow data, so the test counts only the element's own
        /// allocations.
        /// </summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void NoSignature_AllocatesNothing(bool trustForwarded)
        {
            using (var harness = ElementHarness.Create(
                b => b.SetTrustForwardedEvidence(trustForwarded)))
            {
                var existing = harness.ProcessPlainRequest();
                Func<IPipeline, IAgentSignatureData> holdExisting =
                    p => existing;
                var key = harness.Element.ElementDataKeyTyped;

                // Run both paths enough times first that nothing compiled
                // or cached on first use is counted.
                for (var i = 0; i < 3; i++)
                {
                    MeasureElement(harness);
                    MeasureHolding(harness, key, holdExisting);
                }

                var element = MeasureElement(harness);
                var holding = MeasureHolding(harness, key, holdExisting);
                Assert.IsTrue(
                    element <= holding,
                    $"Processing {Requests} requests with no signature " +
                    $"allocated {element} bytes, where holding an answer " +
                    $"that already exists costs the flow data {holding}.");
            }
        }

        /// <summary>
        /// Every request with no signature is answered with the same
        /// values, which do not change between requests.
        /// </summary>
        [TestMethod]
        public void NoSignature_AnsweredFromValuesThatDoNotChange()
        {
            using (var harness = ElementHarness.Create())
            {
                var first = harness.ProcessPlainRequest();
                var second = harness.ProcessPlainRequest();
                Assert.AreEqual(
                    Constants.STATUS_ABSENT, second.AgentSignature.Value);
                Assert.AreSame(first.AgentSignature, second.AgentSignature);
                Assert.AreSame(
                    first.AgentSignatureReason,
                    second.AgentSignatureReason);
                Assert.AreSame(
                    first.AgentSignatureCardUrl,
                    second.AgentSignatureCardUrl);
            }
        }

        private static List<IFlowData> PlainRequests(ElementHarness harness)
        {
            var list = new List<IFlowData>(Requests);
            for (var i = 0; i < Requests; i++)
            {
                var data = harness.Pipeline.CreateFlowData();
                data.AddEvidence("header.host", "example.com");
                data.AddEvidence("header.protocol", "https");
                data.AddEvidence("header.user-agent", "Mozilla/5.0");
                list.Add(data);
            }
            return list;
        }

        private static long MeasureElement(ElementHarness harness)
        {
            var requests = PlainRequests(harness);
            var before = GC.GetAllocatedBytesForCurrentThread();
            foreach (var data in requests)
            {
                harness.Element.Process(data);
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            foreach (var data in requests)
            {
                data.Dispose();
            }
            return allocated;
        }

        private static long MeasureHolding(
            ElementHarness harness,
            ITypedKey<IAgentSignatureData> key,
            Func<IPipeline, IAgentSignatureData> holdExisting)
        {
            var requests = PlainRequests(harness);
            var before = GC.GetAllocatedBytesForCurrentThread();
            foreach (var data in requests)
            {
                data.GetOrAdd(key, holdExisting);
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            foreach (var data in requests)
            {
                data.Dispose();
            }
            return allocated;
        }
    }
}
