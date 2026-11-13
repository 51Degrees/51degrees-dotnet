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

using FiftyOne.Pipeline.Engines.FiftyOne.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Tests.Data
{
    /// <summary>
    /// Checks that the two ways usage sharing reads the web request, all
    /// its values or one of them, read the same request values.
    /// </summary>
    [TestClass]
    public class ShareUsageEvidenceTests
    {
        private const string HeaderKey = "header.x-test";

        private static Dictionary<string, object> Evidence(object request) =>
            new Dictionary<string, object>()
            {
                { "header.user-agent", "agent" },
                { Core.Constants.EVIDENCE_HTTPREQUEST_KEY, request },
            };

        /// <summary>
        /// A request handed over as a read only dictionary, as the key's
        /// documentation asks, is read by both.
        /// </summary>
        [TestMethod]
        public void ReadOnlyDictionaryRequest_ReadByFindAndTryGetValue()
        {
            var evidence = Evidence(new ReadOnlyDictionary<string, object>(
                new Dictionary<string, object>() { { HeaderKey, "value" } }));

            var found = ShareUsageEvidence.Find(evidence);
            var got = ShareUsageEvidence.TryGetValue(
                evidence, HeaderKey, out var value);

            Assert.AreEqual("value", found[HeaderKey]);
            Assert.IsTrue(got);
            Assert.AreEqual("value", value);
        }

        /// <summary>
        /// A request handed over as a list of pairs, not a read only
        /// dictionary, is read by neither, so the message that is shared
        /// and the check of values to ignore never disagree about it.
        /// </summary>
        [TestMethod]
        public void PairListRequest_ReadByNeither()
        {
            var evidence = Evidence(new List<KeyValuePair<string, object>>()
            {
                new KeyValuePair<string, object>(HeaderKey, "value"),
            });

            var found = ShareUsageEvidence.Find(evidence);
            var got = ShareUsageEvidence.TryGetValue(
                evidence, HeaderKey, out _);

            Assert.IsFalse(found.ContainsKey(HeaderKey),
                "Find read a request that is not a read only dictionary.");
            Assert.IsFalse(got);
            Assert.AreEqual("agent", found["header.user-agent"]);
        }
    }
}
