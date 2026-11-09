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
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Data
{
    /// <summary>
    /// Element data instance for <see cref="SequenceElement"/>.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/pipeline-elements/sequence-element.md#element-data">Specification</see>
    /// </summary>
    public class SequenceData : ElementDataBase
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="logger">Used for logging.</param>
        /// <param name="pipeline">
        /// The <see cref="IPipeline"/> this element data belongs to.
        /// </param>
        public SequenceData(
            ILogger<SequenceData> logger,
            IPipeline pipeline)
            : base(logger, pipeline)
        { }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="logger">Used for logging.</param>
        /// <param name="pipeline">
        /// The <see cref="IPipeline"/> this element data belongs to.
        /// </param>
        /// <param name="dictionary">
        /// The dictionary to hold the values.
        /// </param>
        public SequenceData(
            ILogger<ElementDataBase> logger,
            IPipeline pipeline,
            IDictionary<string, object> dictionary)
            : base(logger, pipeline, dictionary)
        { }

        /// <summary>
        /// An identifier for the session.
        /// </summary>
        public string SessionId
        {
            get => this[Constants.EVIDENCE_SESSIONID_SUFFIX] as string;
            set => this[Constants.EVIDENCE_SESSIONID_SUFFIX] = value;
        }

        /// <summary>
        /// A counter that records how many consecutive callbacks have been made
        /// in the session.
        /// </summary>
        public int Sequence
        {
            get => this[Constants.EVIDENCE_SEQUENCE_SUFIX] is int sequence ?
                sequence : 0;
            set => this[Constants.EVIDENCE_SEQUENCE_SUFIX] = value;
        }
    }
}
