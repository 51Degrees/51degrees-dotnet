using FiftyOne.Pipeline.Engines.FiftyOne.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Tests.Data
{
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
}
