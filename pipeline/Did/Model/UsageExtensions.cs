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

namespace FiftyOne.Did.Model
{
    /// <summary>
    /// The mapping between <see cref="Usage"/> and the <c>id.usage</c>
    /// string the cloud uses, so an application can send back the usage
    /// it read without spelling the values out itself. Every 51Did
    /// package exposes this mapping.
    /// </summary>
    public static class UsageExtensions
    {
        /// <summary>
        /// The <c>id.usage</c> value the cloud uses for this usage:
        /// <c>non-marketing</c>, <c>standard</c> or <c>personalized</c>.
        /// </summary>
        /// <param name="usage">
        /// The usage read from a 51Did.
        /// </param>
        /// <returns>
        /// The <c>id.usage</c> string, or null for a value that is not one
        /// of the three usages the cloud issues.
        /// </returns>
        public static string? IdUsage(this Usage usage)
        {
            switch (usage)
            {
                case Usage.NonMarketing:
                    return "non-marketing";
                case Usage.Standard:
                    return "standard";
                case Usage.Personalized:
                    return "personalized";
                default:
                    return null;
            }
        }
    }
}
