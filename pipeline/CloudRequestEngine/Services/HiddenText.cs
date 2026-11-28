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
using System.Net;

namespace FiftyOne.Pipeline.CloudRequestEngine.Services
{
    /// <summary>
    /// Replaces values that no log entry or error message may hold, in
    /// whole or in part, being the License Key, a sealed Human Confidence
    /// result, a transaction and a page id.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/human-confidence.md#what-a-package-never-exposes">Specification</see>
    /// </summary>
    internal static class HiddenText
    {
        /// <summary>
        /// What stands in for a value that is hidden.
        /// </summary>
        internal const string HIDDEN = "(hidden)";

        /// <summary>
        /// The text with each value replaced whole by
        /// <see cref="HIDDEN"/>, whether plain or encoded for a form or a
        /// URL. The longest value goes first, so a shorter value that is
        /// part of a longer one cannot leave the rest of the longer one in
        /// the text.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="values">
        /// The values to hide. Null and empty values are ignored.
        /// </param>
        /// <returns>The text with the values replaced.</returns>
        public static string Hide(string text, IEnumerable<string> values)
        {
            if (string.IsNullOrEmpty(text) || values == null)
            {
                return text;
            }
            var forms = values
                .Where(v => string.IsNullOrEmpty(v) == false)
                .SelectMany(v => new[]
                {
                    v,
                    Uri.EscapeDataString(v),
                    WebUtility.UrlEncode(v),
                })
                .Distinct()
                .OrderByDescending(f => f.Length);
            var result = text;
            foreach (var form in forms)
            {
                result = result.Replace(form, HIDDEN);
            }
            return result;
        }
    }
}
