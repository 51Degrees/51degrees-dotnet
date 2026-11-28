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

using FiftyOne.Pipeline.Core.Data.Types;
using FiftyOne.Pipeline.Engines.Data;

namespace FiftyOne.Pipeline.CloudRequestEngine.Data
{
    /// <summary>
    /// The <c>browserderived</c> Element Data of a Pipeline that uses the
    /// cloud service, which holds the 51Degrees browser tests.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/human-confidence.md#when-a-pages-results-complete">Specification</see>
    /// </summary>
    public interface IBrowserDerivedCloudData : IAspectData
    {
        /// <summary>
        /// The browser tests, which the cloud service gives a request that
        /// names HumanConfidence. The client script runs them like any
        /// other JavaScript Property, and sends their answers with its
        /// next request.
        /// </summary>
        IAspectPropertyValue<JavaScript> BrowserTestsJavaScript { get; }
    }
}
