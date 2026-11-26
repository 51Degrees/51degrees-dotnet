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

using FiftyOne.Pipeline.Core.Data;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.CloudRequestEngine.Data
{
    /// <summary>
    /// The evidence keys the cloud service lists, plus every query and
    /// cookie key whose name starts <c>51D_</c>.
    /// <para>
    /// The page script stores what its snippets work out under names that
    /// start <c>51D_</c>, and posts them back with the session id and the
    /// sequence on the next iteration. Some snippets, such as the browser
    /// tests, pick a new name after the prefix each time they are served,
    /// so no list the cloud gives can hold every name. The cloud service
    /// reads any such name, so the engine accepts them all, and a web
    /// integration then copies them from the request into evidence.
    /// </para>
    /// <para>
    /// <see cref="EvidenceKeyFilterWhitelist.Whitelist"/> stays the list the
    /// cloud gave, so the discovery results are unchanged, and the results
    /// cache is keyed on <see cref="Listed"/>, so a stored value the cloud
    /// doesn't list for the key, such as a location the key has no access
    /// to, shares a cache entry as before.
    /// </para>
    /// </summary>
    internal sealed class CloudEvidenceKeyFilter : EvidenceKeyFilterWhitelist
    {
        /// <summary>
        /// The start of the name of every value the page script stores.
        /// </summary>
        internal const string StoredValuePrefix = "51D_";

        /// <summary>
        /// The prefixes the page script's values arrive under: posted
        /// fields as query, and stored values the browser sends as cookies.
        /// </summary>
        private static readonly string[] StoredValueSources =
        {
            Core.Constants.EVIDENCE_QUERY_PREFIX
                + Core.Constants.EVIDENCE_SEPERATOR,
            Core.Constants.EVIDENCE_COOKIE_PREFIX
                + Core.Constants.EVIDENCE_SEPERATOR,
        };

        /// <summary>
        /// Creates the filter from the keys the cloud service listed.
        /// </summary>
        /// <param name="cloudKeys">The listed keys.</param>
        public CloudEvidenceKeyFilter(List<string> cloudKeys)
            : base(cloudKeys, StringComparer.OrdinalIgnoreCase)
        {
            Listed = new EvidenceKeyFilterWhitelist(
                cloudKeys, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The keys the cloud service listed, and no others.
        /// </summary>
        public EvidenceKeyFilterWhitelist Listed { get; }

        /// <summary>
        /// True for a listed key or a page script's stored value.
        /// </summary>
        /// <param name="key">The evidence key.</param>
        /// <returns>True if the engine accepts the key.</returns>
        public override bool Include(string key)
            => base.Include(key) || IsStoredValue(key);

        /// <summary>
        /// The listed key's order, or for a stored value that is not listed,
        /// one after the last listed key.
        /// </summary>
        /// <param name="key">The evidence key.</param>
        /// <returns>The order, or null if the key is not accepted.</returns>
        public override int? Order(string key)
        {
            var order = base.Order(key);
            if (order.HasValue == false && IsStoredValue(key))
            {
                order = Whitelist.Count;
            }
            return order;
        }

        /// <summary>
        /// True when the key is a query or cookie key whose name starts
        /// <c>51D_</c> and goes on after it. Letter case is ignored.
        /// </summary>
        /// <param name="key">The evidence key.</param>
        /// <returns>True for a page script's stored value.</returns>
        internal static bool IsStoredValue(string key)
        {
            if (key == null)
            {
                return false;
            }
            foreach (var source in StoredValueSources)
            {
                if (key.Length > source.Length + StoredValuePrefix.Length &&
                    key.StartsWith(source, StringComparison.OrdinalIgnoreCase) &&
                    string.Compare(
                        key, source.Length,
                        StoredValuePrefix, 0, StoredValuePrefix.Length,
                        StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
