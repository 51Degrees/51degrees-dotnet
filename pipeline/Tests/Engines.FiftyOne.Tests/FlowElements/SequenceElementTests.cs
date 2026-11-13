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
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Tests.FlowElements
{
    /// <summary>
    /// The sequence element gives each flow data a session id and a
    /// sequence number, which the client side JavaScript sends back so the
    /// next request carries on from the last.
    /// </summary>
    [TestClass]
    public class SequenceElementTests
    {
        private TestLoggerFactory _loggerFactory;
        private IPipeline _pipeline;

        [TestInitialize]
        public void Init()
        {
            _loggerFactory = new TestLoggerFactory();
            _pipeline = new PipelineBuilder(_loggerFactory)
                .AddFlowElement(new SequenceElementBuilder(_loggerFactory).Build())
                .Build();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _pipeline.Dispose();
        }

        /// <summary>
        /// A request with no session evidence gets a new session id and
        /// sequence number 1.
        /// </summary>
        [TestMethod]
        public void SequenceElement_NoEvidence_StartsSessionAtOne()
        {
            using (var data = _pipeline.CreateFlowData())
            {
                data.Process();

                var evidence = data.GetEvidence().AsDictionary();
                Assert.IsTrue(Guid.TryParse(
                    evidence[Constants.EVIDENCE_SESSIONID].ToString(),
                    out _),
                    "The session id should be a new GUID.");
                Assert.AreEqual(1, evidence[Constants.EVIDENCE_SEQUENCE]);
            }
        }

        /// <summary>
        /// Two requests without session evidence get different session ids.
        /// </summary>
        [TestMethod]
        public void SequenceElement_NoEvidence_EachRequestGetsItsOwnSession()
        {
            string first;
            using (var data = _pipeline.CreateFlowData())
            {
                data.Process();
                first = data.GetEvidence().AsDictionary()[Constants.EVIDENCE_SESSIONID].ToString();
            }
            using (var data = _pipeline.CreateFlowData())
            {
                data.Process();
                var second = data.GetEvidence().AsDictionary()[Constants.EVIDENCE_SESSIONID].ToString();
                Assert.AreNotEqual(first, second);
            }
        }

        /// <summary>
        /// A request that carries a session id and a sequence number keeps
        /// the session id and increments the sequence. The number arrives
        /// as a string from a query string, or as an int.
        /// </summary>
        [DataTestMethod]
        [DataRow("1", 2)]
        [DataRow("7", 8)]
        [DataRow(3, 4)]
        public void SequenceElement_SessionEvidence_IncrementsSequence(
            object sequence, int expected)
        {
            var session = Guid.NewGuid().ToString();
            using (var data = _pipeline.CreateFlowData())
            {
                data.AddEvidence(Constants.EVIDENCE_SESSIONID, session);
                data.AddEvidence(Constants.EVIDENCE_SEQUENCE, sequence);
                data.Process();

                var evidence = data.GetEvidence().AsDictionary();
                Assert.AreEqual(session, evidence[Constants.EVIDENCE_SESSIONID]);
                Assert.AreEqual(expected, evidence[Constants.EVIDENCE_SEQUENCE]);
                Assert.IsEmpty(data.Errors ?? Enumerable.Empty<IFlowError>().ToList());
            }
        }

        /// <summary>
        /// A sequence that is not a whole number, or is a string with
        /// anything but decimal digits, counts as absent. So the sequence
        /// is 1, no error is recorded, and the evidence and the element
        /// data hold the same two values.
        /// </summary>
        [TestMethod]
        [DataRow("not a number")]
        [DataRow("")]
        [DataRow(" 3")]
        [DataRow("-1")]
        [DataRow("1.5")]
        [DataRow("\u0663")]
        [DataRow(1.5)]
        [DataRow(-4)]
        public void SequenceElement_InvalidSequence_TreatedAsAbsent(
            object sequence)
        {
            var session = Guid.NewGuid().ToString();
            using (var data = _pipeline.CreateFlowData())
            {
                data.AddEvidence(Constants.EVIDENCE_SESSIONID, session);
                data.AddEvidence(Constants.EVIDENCE_SEQUENCE, sequence);
                data.Process();

                AssertOutputs(data, session, 1);
                Assert.IsEmpty(data.Errors ?? Enumerable.Empty<IFlowError>().ToList());
            }
        }

        /// <summary>
        /// A string with NUL characters after its digits is not made only
        /// of the digits 0 to 9, so it counts as absent and the sequence is
        /// 1. The NUL characters are added here, because a test name that
        /// holds one cannot be written to every test results format.
        /// </summary>
        [TestMethod]
        [DataRow("5", 1)]
        [DataRow("5", 3)]
        [DataRow("2147483646", 1)]
        public void SequenceElement_DigitsThenNul_TreatedAsAbsent(
            string digits, int nuls)
        {
            var session = Guid.NewGuid().ToString();
            using (var data = _pipeline.CreateFlowData())
            {
                data.AddEvidence(Constants.EVIDENCE_SESSIONID, session);
                data.AddEvidence(
                    Constants.EVIDENCE_SEQUENCE,
                    digits + new string('\0', nuls));
                data.Process();

                AssertOutputs(data, session, 1);
                Assert.IsEmpty(data.Errors ?? Enumerable.Empty<IFlowError>().ToList());
            }
        }

        /// <summary>
        /// A sequence that one more would not fit in an int counts as
        /// absent, and the largest that does fit is incremented.
        /// </summary>
        [TestMethod]
        [DataRow(int.MaxValue, 1)]
        [DataRow("2147483647", 1)]
        [DataRow("2147483648", 1)]
        [DataRow("99999999999999999999", 1)]
        [DataRow(2147483648L, 1)]
        [DataRow(int.MaxValue - 1, int.MaxValue)]
        [DataRow("2147483646", int.MaxValue)]
        [DataRow("0", 1)]
        [DataRow("007", 8)]
        [DataRow(5L, 6)]
        public void SequenceElement_SequenceAtLimit_IncrementsOrStartsAgain(
            object sequence, int expected)
        {
            var session = Guid.NewGuid().ToString();
            using (var data = _pipeline.CreateFlowData())
            {
                data.AddEvidence(Constants.EVIDENCE_SESSIONID, session);
                data.AddEvidence(Constants.EVIDENCE_SEQUENCE, sequence);
                data.Process();

                AssertOutputs(data, session, expected);
            }
        }

        /// <summary>
        /// A session id that is not a string, or is empty, counts as
        /// absent, so a new one is made.
        /// </summary>
        [TestMethod]
        [DataRow(5)]
        [DataRow("")]
        public void SequenceElement_InvalidSessionId_NewSessionMade(
            object sessionId)
        {
            using (var data = _pipeline.CreateFlowData())
            {
                data.AddEvidence(Constants.EVIDENCE_SESSIONID, sessionId);
                data.AddEvidence(Constants.EVIDENCE_SEQUENCE, "3");
                data.Process();

                var evidence = data.GetEvidence().AsDictionary();
                var made = (string)evidence[Constants.EVIDENCE_SESSIONID];
                Assert.IsTrue(Guid.TryParse(made, out _));
                AssertOutputs(data, made, 4);
            }
        }

        /// <summary>
        /// A sequence with no session id gets a new session id and is
        /// incremented.
        /// </summary>
        [TestMethod]
        public void SequenceElement_SequenceWithoutSessionId_NewSessionMade()
        {
            using (var data = _pipeline.CreateFlowData())
            {
                data.AddEvidence(Constants.EVIDENCE_SEQUENCE, 6);
                data.Process();

                var evidence = data.GetEvidence().AsDictionary();
                var made = (string)evidence[Constants.EVIDENCE_SESSIONID];
                Assert.IsTrue(Guid.TryParse(made, out _));
                AssertOutputs(data, made, 7);
            }
        }

        /// <summary>
        /// A session id with no sequence is kept and the sequence is 1.
        /// </summary>
        [TestMethod]
        public void SequenceElement_SessionIdWithoutSequence_SequenceIsOne()
        {
            var session = Guid.NewGuid().ToString();
            using (var data = _pipeline.CreateFlowData())
            {
                data.AddEvidence(Constants.EVIDENCE_SESSIONID, session);
                data.Process();

                AssertOutputs(data, session, 1);
            }
        }

        /// <summary>
        /// A session id of "0" is a string, so it is kept.
        /// </summary>
        [TestMethod]
        public void SequenceElement_SessionIdZero_Kept()
        {
            using (var data = _pipeline.CreateFlowData())
            {
                data.AddEvidence(Constants.EVIDENCE_SESSIONID, "0");
                data.AddEvidence(Constants.EVIDENCE_SEQUENCE, "1");
                data.Process();

                AssertOutputs(data, "0", 2);
            }
        }

        /// <summary>
        /// The evidence and the element data hold the same session id and
        /// sequence.
        /// </summary>
        private static void AssertOutputs(
            IFlowData data, string sessionId, int sequence)
        {
            var evidence = data.GetEvidence().AsDictionary();
            var elementData = data.Get(SequenceElement.DEFAULT_ELEMENT_DATA_KEY);
            Assert.AreEqual(sessionId, evidence[Constants.EVIDENCE_SESSIONID]);
            Assert.AreEqual(sequence, evidence[Constants.EVIDENCE_SEQUENCE]);
            Assert.AreEqual(sessionId, elementData["session-id"]);
            Assert.AreEqual(sequence, elementData["sequence"]);
        }

        /// <summary>
        /// The session id and sequence are in the element data as well as
        /// the evidence, being the outputs the specification lists.
        /// </summary>
        [TestMethod]
        public void SequenceElement_NoEvidence_WritesElementData()
        {
            using (var data = _pipeline.CreateFlowData())
            {
                data.Process();

                var elementData = data.Get(SequenceElement.DEFAULT_ELEMENT_DATA_KEY);
                var evidence = data.GetEvidence().AsDictionary();
                Assert.AreEqual(
                    evidence[Constants.EVIDENCE_SESSIONID],
                    elementData["session-id"]);
                Assert.AreEqual(1, elementData["sequence"]);
            }
        }

        /// <summary>
        /// A request that carries a session keeps its session id in the
        /// element data and has the incremented sequence there.
        /// </summary>
        [TestMethod]
        public void SequenceElement_SessionEvidence_WritesElementData()
        {
            var session = Guid.NewGuid().ToString();
            using (var data = _pipeline.CreateFlowData())
            {
                data.AddEvidence(Constants.EVIDENCE_SESSIONID, session);
                data.AddEvidence(Constants.EVIDENCE_SEQUENCE, "4");
                data.Process();

                var elementData = data.Get(SequenceElement.DEFAULT_ELEMENT_DATA_KEY);
                Assert.AreEqual(session, elementData["session-id"]);
                Assert.AreEqual(5, elementData["sequence"]);
            }
        }

        /// <summary>
        /// The element declares the two properties it writes.
        /// </summary>
        [TestMethod]
        public void SequenceElement_Properties_AreSessionIdAndSequence()
        {
            var element = new SequenceElementBuilder(_loggerFactory).Build();
            CollectionAssert.AreEquivalent(
                new[] { "session-id", "sequence" },
                element.Properties.Select(p => p.Name).ToArray());
        }

        /// <summary>
        /// The element accepts only the session id and sequence evidence.
        /// </summary>
        [TestMethod]
        public void SequenceElement_EvidenceKeyFilter_AcceptsOnlySessionEvidence()
        {
            var element = new SequenceElementBuilder(_loggerFactory).Build();
            Assert.IsTrue(element.EvidenceKeyFilter.Include(Constants.EVIDENCE_SESSIONID));
            Assert.IsTrue(element.EvidenceKeyFilter.Include(Constants.EVIDENCE_SEQUENCE));
            Assert.IsFalse(element.EvidenceKeyFilter.Include("header.user-agent"));
        }
    }
}
