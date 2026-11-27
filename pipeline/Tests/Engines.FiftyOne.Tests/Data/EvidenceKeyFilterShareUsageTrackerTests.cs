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

namespace FiftyOne.Pipeline.Engines.FiftyOne.Tests.Data;

[TestClass]
public class EvidenceKeyFilterShareUsageTrackerTests
{
    private static readonly string[] SessionAndSequenceKeys =
    {
        "query.session-id", "query.sequence",
        "query.Session-Id", "query.Sequence",
        "QUERY.SESSION-ID", "QUERY.SEQUENCE"
    };

    /// <summary>
    /// The session id and sequence are never part of the repeat
    /// comparison, whatever the case of their keys, as the Evidence and
    /// the share filter both ignore case.
    /// </summary>
    [TestMethod]
    public void Include_SessionIdAndSequence_NeverWhateverTheirCase()
    {
        var filters = new[]
        {
            new EvidenceKeyFilterShareUsageTracker(
                new List<string>(),
                new List<string>() { "session-id", "sequence" },
                false,
                "asp.net_sessionid"),
            new EvidenceKeyFilterShareUsageTracker()
        };
        foreach (var filter in filters)
        {
            foreach (var key in SessionAndSequenceKeys)
            {
                Assert.IsFalse(filter.Include(key), key);
            }
        }
    }
}
