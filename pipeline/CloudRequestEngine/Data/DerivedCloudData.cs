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

using FiftyOne.Pipeline.CloudRequestEngine.Services;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging;

namespace FiftyOne.Pipeline.CloudRequestEngine.Data
{
    /// <summary>
    /// The <see cref="IDerivedCloudData"/> that
    /// <see cref="FlowElements.DerivedCloudEngine"/> gives.
    /// </summary>
    public class DerivedCloudData : AspectDataBase, IDerivedCloudData
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="logger">
        /// The logger for this instance.
        /// </param>
        /// <param name="pipeline">
        /// The Pipeline this instance was created by.
        /// </param>
        /// <param name="engine">
        /// The engine that created this instance.
        /// </param>
        public DerivedCloudData(
            ILogger<DerivedCloudData> logger,
            IPipeline pipeline,
            IAspectEngine engine)
            : base(logger, pipeline, engine, MissingPropertyServiceCloud.Instance)
        {
        }

        /// <inheritdoc/>
        public IAspectPropertyValue<string> HumanConfidence
        {
            get => GetAs<IAspectPropertyValue<string>>(
                Constants.HUMAN_CONFIDENCE);
            set => this[Constants.HUMAN_CONFIDENCE] = value;
        }

        /// <inheritdoc/>
        public IAspectPropertyValue<string> HumanConfidenceSource
        {
            get => GetAs<IAspectPropertyValue<string>>(
                Constants.HUMAN_CONFIDENCE_SOURCE);
            set => this[Constants.HUMAN_CONFIDENCE_SOURCE] = value;
        }
    }
}
