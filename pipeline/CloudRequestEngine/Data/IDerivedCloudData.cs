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

using FiftyOne.Pipeline.Engines.Data;

namespace FiftyOne.Pipeline.CloudRequestEngine.Data
{
    /// <summary>
    /// The <c>derived</c> Element Data of a Pipeline that uses the cloud
    /// service, which gives the two Human Confidence Properties.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/human-confidence.md#what-a-consumer-reads">Specification</see>
    /// </summary>
    public interface IDerivedCloudData : IAspectData
    {
        /// <summary>
        /// How confident the cloud service is that a person is using the
        /// browser: <c>High</c>, <c>Medium</c> or <c>Low</c>. Where the
        /// request carried a sealed result, this is the redeemed Value, or
        /// <c>Medium</c> where it was not redeemed.
        /// </summary>
        IAspectPropertyValue<string> HumanConfidence { get; }

        /// <summary>
        /// How <see cref="HumanConfidence"/> was worked out for the
        /// request: <c>Active</c> where the browser also ran the 51Degrees
        /// browser tests, and <c>Passive</c> where the request alone was
        /// used. Always <c>Active</c> where the request carried a sealed
        /// result.
        /// </summary>
        IAspectPropertyValue<string> HumanConfidenceSource { get; }
    }
}
