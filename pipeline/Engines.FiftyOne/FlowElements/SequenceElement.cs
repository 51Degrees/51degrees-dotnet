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
using FiftyOne.Pipeline.Engines.FiftyOne.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace FiftyOne.Pipeline.Engines.FiftyOne.FlowElements
{
    /// <summary>
    /// Sequence element establishes session and sequence evidence in the
    /// pipeline, and holds the same two values as element data.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/pipeline-elements/sequence-element.md">Specification</see>
    /// </summary>
    public class SequenceElement : FlowElementBase<IElementData, IElementPropertyMetaData>
    {
        private IEvidenceKeyFilter _evidenceKeyFilter = new EvidenceKeyFilterWhitelist(
            new List<string>() {
                Constants.EVIDENCE_SESSIONID,
                Constants.EVIDENCE_SEQUENCE
            });

        private IList<IElementPropertyMetaData> _properties;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="logger"></param>
        public SequenceElement(
            ILogger<FlowElementBase<IElementData, IElementPropertyMetaData>> logger)
            : this(logger, null)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="dataLogger">
        /// Used for logging by the element data this element creates.
        /// </param>
        public SequenceElement(
            ILogger<FlowElementBase<IElementData, IElementPropertyMetaData>> logger,
            ILogger<SequenceData> dataLogger)
            : base(logger, (pipeline, element) => new SequenceData(
                dataLogger ?? NullLogger<SequenceData>.Instance,
                pipeline))
        {
            _properties = new List<IElementPropertyMetaData>()
            {
                new ElementPropertyMetaData(this,
                    Constants.EVIDENCE_SESSIONID_SUFFIX, typeof(string), true),
                new ElementPropertyMetaData(this,
                    Constants.EVIDENCE_SEQUENCE_SUFIX, typeof(int), true)
            };
        }

        /// <summary>
        /// The default element data key that will be used for this element. 
        /// </summary>
#pragma warning disable CA1707 // Identifiers should not contain underscores
        public const string DEFAULT_ELEMENT_DATA_KEY = "sequence";
#pragma warning restore CA1707 // Identifiers should not contain underscores

        /// <summary>
        /// The element data key.
        /// </summary>
        public override string ElementDataKey => DEFAULT_ELEMENT_DATA_KEY;

        /// <summary>
        /// The evidence key filter.
        /// </summary>
        public override IEvidenceKeyFilter EvidenceKeyFilter
        {
            get { return _evidenceKeyFilter; }
        }

        /// <summary>
        /// The properties populated by this element.
        /// </summary>
        public override IList<IElementPropertyMetaData> Properties => _properties;

        /// <summary>
        /// Called when the element is disposed.
        /// </summary>
        protected override void ManagedResourcesCleanup()
        {
        }

        /// <summary>
        /// Process method checks for a session id and a sequence number in
        /// the evidence. A value that is absent or not valid is made new, as
        /// a new id or as 1. A valid sequence number is incremented. Both
        /// values are written to the evidence and to the element data.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> instance to process.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied data instance is null
        /// </exception>
        protected override void ProcessInternal(IFlowData data)
        {
            if(data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var evidence = data.GetEvidence().AsDictionary();

            // A session id that is absent, empty or not a string counts as
            // absent, so a new one is made.
            evidence.TryGetValue(
                Constants.EVIDENCE_SESSIONID,
                out object sessionValue);
            var sessionId = sessionValue is string existing &&
                existing.Length > 0 ?
                existing :
                GetNewSessionId();

            // A sequence that is absent or not a number the sequence can
            // be incremented from counts as absent, which gives 1.
            evidence.TryGetValue(
                Constants.EVIDENCE_SEQUENCE,
                out object sequenceValue);
            var sequence = TryGetSequence(sequenceValue, out int current) ?
                current + 1 :
                1;

            // Evidence and Element Data hold the same two values.
            data.AddEvidence(Constants.EVIDENCE_SESSIONID, sessionId);
            data.AddEvidence(Constants.EVIDENCE_SEQUENCE, sequence);
            var elementData = data.GetOrAdd(
                ElementDataKeyTyped,
                CreateElementData);
            elementData[Constants.EVIDENCE_SESSIONID_SUFFIX] = sessionId;
            elementData[Constants.EVIDENCE_SEQUENCE_SUFIX] = sequence;
        }

        /// <summary>
        /// Gets the sequence from a whole number or a string of decimal
        /// digits. The value must be at least 1 and must be one that can
        /// have 1 added without leaving the range of an int.
        /// </summary>
        private static bool TryGetSequence(object value, out int sequence)
        {
            sequence = 0;
            long number;
            switch (value)
            {
                case int i: number = i; break;
                case long l: number = l; break;
                case short sh: number = sh; break;
                case byte by: number = by; break;
                case uint ui: number = ui; break;
                case string text:
                    if (int.TryParse(
                        text,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out int parsed) == false)
                    {
                        return false;
                    }
                    number = parsed;
                    break;
                default:
                    return false;
            }
            if (number < 1 || number >= int.MaxValue)
            {
                return false;
            }
            sequence = (int)number;
            return true;
        }

        /// <summary>
        /// Called as part of object disposal.
        /// This element has no unmanaged resources so this method is empty.
        /// </summary>
        protected override void UnmanagedResourcesCleanup()
        {
        }

        private string GetNewSessionId()
        {
            Guid g = Guid.NewGuid();
            return g.ToString();
        }
    }
}
