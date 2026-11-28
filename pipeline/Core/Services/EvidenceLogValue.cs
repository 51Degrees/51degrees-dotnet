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
using System.Linq;

namespace FiftyOne.Pipeline.Core.Services
{
    /// <summary>
    /// Gives the value of a piece of Evidence as a log entry or an error
    /// message may show it. A sealed Human Confidence result, the id of
    /// the page it came from, and the transaction it is bound to, under its
    /// current and older names, are never shown.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/human-confidence.md#what-a-package-never-exposes">Specification</see>
    /// </summary>
    public static class EvidenceLogValue
    {
        /// <summary>
        /// What a log entry shows in place of a value it must not hold.
        /// </summary>
        public const string NotLogged = "(not logged)";

        /// <summary>
        /// The names, after the prefix, of Evidence whose value is never
        /// written to a log entry or an error message.
        /// </summary>
        private static readonly string[] UnloggedNames =
        {
            "humanconfidence",
            "humanconfidencepage",
            "transaction",
            "challenge",
        };

        /// <summary>
        /// The value of a piece of Evidence as a log entry may show it.
        /// </summary>
        /// <param name="key">The Evidence key, with its prefix.</param>
        /// <param name="value">The Evidence value.</param>
        /// <returns>
        /// The value as text, or <see cref="NotLogged"/>.
        /// </returns>
        public static string Of(string key, object value)
        {
            if (key == null)
            {
                return value?.ToString();
            }
            var separator = key.IndexOf(
                Constants.EVIDENCE_SEPERATOR, StringComparison.Ordinal);
            var name = separator >= 0 ? key.Substring(separator + 1) : key;
            return UnloggedNames.Any(n =>
                n.Equals(name, StringComparison.OrdinalIgnoreCase))
                ? NotLogged
                : value?.ToString();
        }
    }
}
