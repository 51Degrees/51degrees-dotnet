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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace FiftyOne.IpIntelligence.TestHelpers.FlowElements
{
    public class EvidenceKeyTests
    {
        public static void ContainsIpAddress(IWrapper wrapper)
        {
            Assert.IsTrue(wrapper.GetEngine().EvidenceKeyFilter.Include("query.client-ip-51d"));
            Assert.IsTrue(wrapper.GetEngine().EvidenceKeyFilter.Include("query.true-client-ip-51d"));
            Assert.IsTrue(wrapper.GetEngine().EvidenceKeyFilter.Include("server.client-ip-51d"));
            Assert.IsTrue(wrapper.GetEngine().EvidenceKeyFilter.Include("server.true-client-ip-51d"));
        }

        /// <summary>
        /// The accepted evidence comes from the data file, so it is read
        /// again when the data is refreshed. After a refresh the engine
        /// still accepts the client IP keys and still refuses a key that
        /// is not evidence it uses.
        /// </summary>
        public static void SameAfterRefresh(IWrapper wrapper)
        {
            var keys = new[]
            {
                "query.client-ip-51d",
                "query.true-client-ip-51d",
                "server.client-ip-51d",
                "server.true-client-ip-51d",
                "header.x-forwarded-for",
                "header.user-agent",
                "query.not-evidence-the-engine-uses"
            };
            var before = keys.ToDictionary(
                k => k,
                k => wrapper.GetEngine().EvidenceKeyFilter.Include(k));
            Assert.IsTrue(before["query.client-ip-51d"]);
            Assert.IsFalse(before["query.not-evidence-the-engine-uses"]);

            wrapper.GetEngine().RefreshData(null);

            foreach (var key in keys)
            {
                Assert.AreEqual(before[key],
                    wrapper.GetEngine().EvidenceKeyFilter.Include(key),
                    $"The engine's answer for evidence key '{key}' changed after a refresh.");
            }
        }

        public static void CaseInsensitiveKeys(IWrapper wrapper)
        {
            Assert.IsTrue(wrapper.GetEngine().EvidenceKeyFilter.Include("query.client-ip-51d"));
            Assert.IsTrue(wrapper.GetEngine().EvidenceKeyFilter.Include("QUERY.client-ip-51d"));
            Assert.IsTrue(wrapper.GetEngine().EvidenceKeyFilter.Include("query.CLIENT-IP-51D"));
            Assert.IsTrue(wrapper.GetEngine().EvidenceKeyFilter.Include("QUERY.CLIENT-IP-51D"));
        }
    }
}
