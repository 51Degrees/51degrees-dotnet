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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FiftyOne.Pipeline.AgentSignature.Tests
{
    /// <summary>
    /// The specification, agent-signature-element.md, Process: "Nearly
    /// every request carries no signature at all, so the Element MUST test
    /// for the two signature headers before it parses or allocates
    /// anything, and MUST answer that case from values that do not change
    /// between requests."
    /// </summary>
    [TestClass]
    [TestCategory("Unit")]
    public class NoSignatureFirstTests
    {
        /// <summary>
        /// Records each call the element makes on the flow data, and
        /// passes it on to a real flow data.
        /// </summary>
        public class RecordingFlowData : DispatchProxy
        {
            /// <summary>
            /// The flow data the calls are passed to.
            /// </summary>
            public IFlowData Inner { get; set; }

            /// <summary>
            /// Each call in order, as the method name and its first
            /// argument.
            /// </summary>
            public List<(string Method, object Argument)> Calls { get; } =
                new List<(string, object)>();

            /// <inheritdoc/>
            protected override object Invoke(
                MethodInfo targetMethod,
                object[] args)
            {
                Calls.Add((
                    targetMethod.Name,
                    args != null && args.Length > 0 ? args[0] : null));
                try
                {
                    return targetMethod.Invoke(Inner, args);
                }
                catch (TargetInvocationException ex)
                {
                    throw ex.InnerException;
                }
            }
        }

        private static RecordingFlowData RunPlainRequest(
            ElementHarness harness)
        {
            using (var data = harness.Pipeline.CreateFlowData())
            {
                data.AddEvidence("header.host", "example.com");
                data.AddEvidence("header.protocol", "https");
                var proxy = DispatchProxy.Create<
                    IFlowData, RecordingFlowData>();
                var recorder = (RecordingFlowData)(object)proxy;
                recorder.Inner = data;
                harness.Element.Process(proxy);
                Assert.AreEqual(
                    "Absent",
                    data.Get<IAgentSignatureData>().AgentSignature.Value);
                return recorder;
            }
        }

        /// <summary>
        /// The element reads the signature headers before it makes its
        /// element data, so a request with no signature has been answered
        /// "no signature" before anything is allocated for it.
        /// </summary>
        [TestMethod]
        public void NoSignature_TestedBeforeElementDataIsMade()
        {
            using (var harness = ElementHarness.Create())
            {
                var calls = RunPlainRequest(harness).Calls;
                var firstAdd = calls.FindIndex(c => c.Method == "GetOrAdd");
                var firstRead = calls.FindIndex(c =>
                    c.Method.StartsWith("TryGetEvidence"));
                Assert.IsTrue(firstRead >= 0,
                    "The element never read the evidence.");
                Assert.IsTrue(firstAdd < 0 || firstRead < firstAdd,
                    "The element made its element data before it tested " +
                    "for the signature headers. Calls: " +
                    string.Join(", ", calls.Select(c => c.Method)));
                var readsBeforeAdd = calls
                    .Take(firstAdd < 0 ? calls.Count : firstAdd)
                    .Where(c => c.Method.StartsWith("TryGetEvidence"))
                    .Select(c => c.Argument as string)
                    .ToList();
                CollectionAssert.Contains(
                    readsBeforeAdd, Constants.EVIDENCE_SIGNATURE_KEY);
                CollectionAssert.Contains(
                    readsBeforeAdd, Constants.EVIDENCE_SIGNATURE_INPUT_KEY);
            }
        }

        /// <summary>
        /// The keys the element looks the signature headers up with are
        /// the same string instances on every request, so the test for no
        /// signature builds no new string.
        /// </summary>
        [TestMethod]
        public void NoSignature_KeysAreNotBuiltForEachRequest()
        {
            using (var harness = ElementHarness.Create(b =>
                b.SetTrustForwardedEvidence(true)))
            {
                var first = RunPlainRequest(harness).Calls
                    .Where(c => c.Method.StartsWith("TryGetEvidence"))
                    .Select(c => c.Argument as string)
                    .ToList();
                var second = RunPlainRequest(harness).Calls
                    .Where(c => c.Method.StartsWith("TryGetEvidence"))
                    .Select(c => c.Argument as string)
                    .ToList();
                Assert.AreEqual(first.Count, second.Count);
                for (var i = 0; i < first.Count; i++)
                {
                    Assert.IsTrue(
                        ReferenceEquals(first[i], second[i]),
                        $"The key '{second[i]}' was built again for the " +
                        "second request.");
                }
            }
        }
    }
}
