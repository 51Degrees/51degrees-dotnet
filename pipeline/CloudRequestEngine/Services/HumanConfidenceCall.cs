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

using System;
using System.Collections.Generic;
using System.Linq;
using FiftyOne.Pipeline.Core.Data;

namespace FiftyOne.Pipeline.CloudRequestEngine.Services
{
    /// <summary>
    /// What one JSON call of the Cloud Request Engine carries for Human
    /// Confidence, and the values no log entry or error message made from
    /// the call may hold.
    /// <para>
    /// The server issues a page id when it renders a page, through
    /// <see cref="FlowElements.CloudRequestEngine.IssueHumanConfidencePage"/>,
    /// and the page loads its client script with the page id as
    /// humanconfidencepage. The script then sends it on every call, as the
    /// Evidence <see cref="EVIDENCE_PAGE"/>, and each such call carries the
    /// page's transaction, so the cloud service binds the sealed result it
    /// makes to it.
    /// </para>
    /// <para>
    /// A request whose Evidence holds a sealed result, and the page id,
    /// redeems it where the engine has a License Key and still keeps a
    /// transaction for that page. The call then carries the sealed result
    /// and that transaction. Otherwise it carries neither, and nothing is
    /// redeemed.
    /// </para>
    /// <para>
    /// How the page id is issued and sent back is a choice the
    /// specification leaves open, so it is kept to this class,
    /// <see cref="HumanConfidenceTransactions"/> and the engine's issuing
    /// method.
    /// </para>
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/human-confidence.md#redeeming-through-the-json-call">Specification</see>
    /// </summary>
    internal sealed class HumanConfidenceCall
    {
        /// <summary>
        /// The field the sealed result is sent in.
        /// </summary>
        internal const string SEALED_RESULT_FIELD = "humanconfidence";

        /// <summary>
        /// The field the transaction is sent in.
        /// </summary>
        internal const string TRANSACTION_FIELD = "transaction";

        /// <summary>
        /// The older name of the transaction, which Evidence never sends.
        /// </summary>
        internal const string CHALLENGE_FIELD = "challenge";

        /// <summary>
        /// The Evidence key the page sends its page id back under, beside
        /// the sealed result.
        /// </summary>
        internal const string EVIDENCE_PAGE =
            Constants.EVIDENCE_HUMAN_CONFIDENCE_PAGE;

        /// <summary>
        /// The field name of <see cref="EVIDENCE_PAGE"/>, which Evidence
        /// never sends to the cloud service.
        /// </summary>
        internal const string PAGE_FIELD = "humanconfidencepage";

        private static readonly HumanConfidenceCall None =
            new HumanConfidenceCall(null, null, Array.Empty<string>());

        private readonly IReadOnlyList<string> _hidden;

        private HumanConfidenceCall(
            string sealedResult,
            string transaction,
            IEnumerable<string> hidden)
        {
            SealedResult = sealedResult;
            Transaction = transaction;
            _hidden = hidden
                .Where(h => string.IsNullOrEmpty(h) == false)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// The sealed result to redeem, exactly as it arrived, or null
        /// where the call redeems nothing.
        /// </summary>
        public string SealedResult { get; }

        /// <summary>
        /// The transaction kept for the page, or null where the call
        /// carries none.
        /// </summary>
        public string Transaction { get; }

        /// <summary>
        /// True where the response to the request is for that request
        /// alone, so it is neither stored in nor served from a results
        /// cache: the request carries a page id, whose call may carry the
        /// page's transaction, or a value named humanconfidence under any
        /// prefix or none. Only query.humanconfidence is a sealed result
        /// that is redeemed, and none of the others is sent, but a request
        /// that carries one is still kept out of the cache.
        /// </summary>
        /// <param name="data">The Flow Data of the request.</param>
        public static bool IsForOneRequest(IFlowData data) =>
            Text(data, EVIDENCE_PAGE) != null ||
            data.GetEvidence().AsDictionary().Any(e =>
                NameOf(e.Key).Equals(
                    SEALED_RESULT_FIELD, StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrEmpty(e.Value?.ToString()) == false);

        /// <summary>
        /// The name of an Evidence key after its prefix, or the whole key
        /// where it has none.
        /// </summary>
        private static string NameOf(string key)
        {
            var separator = key.IndexOf(
                Core.Constants.EVIDENCE_SEPERATOR, StringComparison.Ordinal);
            return separator >= 0 ? key.Substring(separator + 1) : key;
        }

        /// <summary>
        /// Works out what the call for a request carries.
        /// </summary>
        /// <param name="data">The Flow Data of the request.</param>
        /// <param name="licenseKey">
        /// The engine's License Key, from its configuration. Redemption
        /// needs it, so without one nothing is redeemed and no transaction
        /// is given.
        /// </param>
        /// <param name="transactions">The transactions kept for pages.</param>
        /// <returns>What the call carries.</returns>
        public static HumanConfidenceCall For(
            IFlowData data,
            string licenseKey,
            HumanConfidenceTransactions transactions)
        {
            var sealedResult = SealedResultFlowCache.SealedResult(data);
            var hasLicenseKey = string.IsNullOrWhiteSpace(licenseKey) == false;
            var pageId = Text(data, EVIDENCE_PAGE);
            if (sealedResult != null)
            {
                // Taken whether or not the call succeeds, so a transaction
                // redeems at most once. Without one kept for the page,
                // nothing is redeemed.
                if (hasLicenseKey &&
                    transactions.TryTake(pageId, out var transaction))
                {
                    return new HumanConfidenceCall(
                        sealedResult,
                        transaction,
                        new[] { sealedResult, transaction, licenseKey, pageId });
                }
                return new HumanConfidenceCall(
                    null,
                    null,
                    new[] { sealedResult, licenseKey, pageId });
            }
            var kept = hasLicenseKey ? transactions.Find(pageId) : null;
            if (kept != null)
            {
                return new HumanConfidenceCall(
                    null, kept, new[] { kept, pageId });
            }
            return None;
        }

        /// <summary>
        /// The values of the call that no log entry or error message may
        /// hold.
        /// </summary>
        public IReadOnlyList<string> Hidden => _hidden;

        private static string Text(IFlowData data, string key)
        {
            if (data.TryGetEvidence(key, out object value))
            {
                var text = value?.ToString();
                return string.IsNullOrEmpty(text) ? null : text;
            }
            return null;
        }
    }
}
