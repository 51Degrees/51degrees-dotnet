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

namespace FiftyOne.Did.Client
{
    /// <summary>
    /// The Human Confidence Value a 51Did carries, mapped from the
    /// <c>humanConfidence</c> string the cloud's verify and redeem
    /// endpoints return. The cloud returns it only to a call that presents
    /// a License Key valid for Human Confidence, and gives a reason in
    /// <c>humanConfidenceNullReason</c> to any other call.
    /// </summary>
    public enum HumanConfidenceValue
    {
        /// <summary>
        /// The 51Did carries no Human Confidence Value. Every 51Did made
        /// before the cloud added the Value reads as this, as does one made
        /// when no Value was available.
        /// </summary>
        Absent = 0,

        /// <summary>Low confidence that a human made the request.</summary>
        Low = 1,

        /// <summary>Medium confidence that a human made the request.</summary>
        Medium = 2,

        /// <summary>High confidence that a human made the request.</summary>
        High = 3,
    }
}
