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

namespace FiftyOne.Pipeline.Engines.FiftyOne.Data
{
    /// <summary>
    /// The evidence the usage sharing element finds, which it shares where
    /// <see cref="EvidenceKeyFilterShareUsage"/> allows. The element accepts
    /// no evidence, so it reads the web request from the
    /// <see cref="Core.Constants.EVIDENCE_HTTPREQUEST_KEY"/> value that a
    /// web integration adds.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/5a250650766c025c42aa9d7943bd6781f4447646/pipeline-specification/pipeline-elements/usage-sharing-element.md#accepted-evidence">Specification</see>
    /// </summary>
    internal static class ShareUsageEvidence
    {
        /// <summary>
        /// Every value found, being the evidence and then the web
        /// request's values for keys the evidence does not hold, so a key
        /// in both is found once with the evidence value. The web request
        /// value itself is left out.
        /// </summary>
        /// <param name="evidence">The flow data's evidence.</param>
        /// <returns>The values, keyed as evidence.</returns>
        internal static IReadOnlyDictionary<string, object> Find(
            IReadOnlyDictionary<string, object> evidence)
        {
            var hasRequest = evidence.TryGetValue(
                Core.Constants.EVIDENCE_HTTPREQUEST_KEY,
                out var requestValue);
            if (hasRequest == false)
            {
                return evidence;
            }

            var found = new Dictionary<string, object>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var entry in evidence)
            {
                if (IsRequestKey(entry.Key) == false)
                {
                    found[entry.Key] = entry.Value;
                }
            }
            if (requestValue is IEnumerable<KeyValuePair<string, object>>
                request)
            {
                foreach (var entry in request)
                {
                    if (IsRequestKey(entry.Key) == false &&
                        found.ContainsKey(entry.Key) == false)
                    {
                        found.Add(entry.Key, entry.Value);
                    }
                }
            }
            return found;
        }

        /// <summary>
        /// The value of a key as <see cref="Find"/> would find it, without
        /// gathering the rest.
        /// </summary>
        /// <param name="evidence">The flow data's evidence.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value, if found.</param>
        /// <returns>True if the key was found.</returns>
        internal static bool TryGetValue(
            IReadOnlyDictionary<string, object> evidence,
            string key,
            out object value)
        {
            if (IsRequestKey(key))
            {
                value = null;
                return false;
            }
            if (evidence.TryGetValue(key, out value))
            {
                return true;
            }
            if (evidence.TryGetValue(
                    Core.Constants.EVIDENCE_HTTPREQUEST_KEY,
                    out var requestValue) &&
                requestValue is IReadOnlyDictionary<string, object> request)
            {
                return request.TryGetValue(key, out value);
            }
            value = null;
            return false;
        }

        /// <summary>
        /// True for the key of the web request value itself.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>True if the key is the web request's.</returns>
        internal static bool IsRequestKey(string key) =>
            string.Equals(
                key,
                Core.Constants.EVIDENCE_HTTPREQUEST_KEY,
                StringComparison.OrdinalIgnoreCase);
    }
}
