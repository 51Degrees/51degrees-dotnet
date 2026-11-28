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

using FiftyOne.Pipeline.CloudRequestEngine.Data;
using FiftyOne.Pipeline.CloudRequestEngine.Services;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines;
using FiftyOne.Pipeline.Engines.Caching;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.Pipeline.CloudRequestEngine.FlowElements
{
    /// <summary>
    /// Cloud engine for the <c>derived</c> Element Data, which gives the
    /// Human Confidence Properties HumanConfidence and
    /// HumanConfidenceSource.
    /// <para>
    /// On a request that carries no sealed result, both are read from the
    /// <c>derived</c> member of the cloud service's response. On a request
    /// that carries one, the <see cref="CloudRequestEngine"/> redeems it
    /// through its own call, HumanConfidenceSource is <c>Active</c>, and
    /// HumanConfidence is the redeemed Value, or <c>Medium</c> where
    /// nothing was redeemed. A consumer only reads the two Properties.
    /// </para>
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/human-confidence.md">Specification</see>
    /// </summary>
    public class DerivedCloudEngine : CloudAspectEngineBase<IDerivedCloudData>
    {
        /// <summary>
        /// The Values a redeemed sealed result can have. Any other value,
        /// <c>Unknown</c> included, means it was not redeemed.
        /// </summary>
        private static readonly string[] REDEEMED_VALUES =
        {
            "High",
            "Medium",
            "Low",
        };

        /// <summary>
        /// The two Properties described locally, so the values in a cloud
        /// response are given their types before the cloud's property
        /// list has been fetched.
        /// </summary>
        private readonly List<IElementPropertyMetaData> _propertyTypes;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="logger">
        /// The logger to use.
        /// </param>
        /// <param name="aspectDataFactory">
        /// The factory function to use when the engine creates an
        /// <see cref="IDerivedCloudData"/> instance.
        /// </param>
        public DerivedCloudEngine(
            ILogger<AspectEngineBase<IDerivedCloudData, IAspectPropertyMetaData>> logger,
            Func<IPipeline,
                FlowElementBase<IDerivedCloudData, IAspectPropertyMetaData>,
                IDerivedCloudData> aspectDataFactory)
            : base(logger, aspectDataFactory)
        {
            _propertyTypes = new[]
            {
                Constants.HUMAN_CONFIDENCE,
                Constants.HUMAN_CONFIDENCE_SOURCE,
            }
            .Select(name => (IElementPropertyMetaData)new AspectPropertyMetaData(
                this,
                name,
                typeof(IAspectPropertyValue<string>),
                "Bot Detection",
                new List<string>(),
                true))
            .ToList();
        }

        /// <inheritdoc/>
        public override string ElementDataKey =>
            Constants.DERIVED_ELEMENT_DATA_KEY;

        /// <summary>
        /// The sealed result and the page id beside it, so a web
        /// integration adds them to the Evidence. The sealed result is
        /// never written to the Element Data.
        /// </summary>
        public override IEvidenceKeyFilter EvidenceKeyFilter { get; } =
            new EvidenceKeyFilterWhitelist(new List<string>
            {
                Constants.EVIDENCE_HUMAN_CONFIDENCE,
                Constants.EVIDENCE_HUMAN_CONFIDENCE_PAGE,
            });

        /// <summary>
        /// On a request that carries a sealed result, gives the two
        /// Properties as
        /// <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/human-confidence.md#when-humanconfidence-arrives-sealed">the specification</see>
        /// sets out, without throwing and without adding an error of its
        /// own. Any failure of the request is the
        /// <see cref="CloudRequestEngine"/>'s error alone. Every other
        /// request is processed as any cloud engine processes it.
        /// </summary>
        /// <param name="data">The Flow Data to process.</param>
        /// <param name="aspectData">The data to populate.</param>
        protected override void ProcessEngine(
            IFlowData data,
            IDerivedCloudData aspectData)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (aspectData == null)
            {
                throw new ArgumentNullException(nameof(aspectData));
            }
            if (SealedResultFlowCache.HasSealedResult(data) == false)
            {
                base.ProcessEngine(data, aspectData);
                return;
            }
            aspectData.PopulateFrom(new Dictionary<string, object>
            {
                [Constants.HUMAN_CONFIDENCE] = new AspectPropertyValue<string>(
                    RedeemedValue(data) ??
                        Constants.HUMAN_CONFIDENCE_NOT_REDEEMED),
                [Constants.HUMAN_CONFIDENCE_SOURCE] =
                    new AspectPropertyValue<string>(
                        Constants.HUMAN_CONFIDENCE_SOURCE_ACTIVE),
            });
        }

        /// <inheritdoc/>
        protected override void ProcessCloudEngine(
            IFlowData data,
            IDerivedCloudData aspectData,
            string json)
        {
            if (aspectData == null)
            {
                throw new ArgumentNullException(nameof(aspectData));
            }
            var derived = Derived(JObject.Parse(json));
            if (derived == null)
            {
                return;
            }
            // Only the two Properties, with the reason beside each where
            // the cloud service gives it as null.
            var values = derived.Properties()
                .Where(p => _propertyTypes.Any(t =>
                    p.Name.StartsWith(t.Name, StringComparison.OrdinalIgnoreCase) &&
                    (p.Name.Length == t.Name.Length ||
                    p.Name.Substring(t.Name.Length).Equals(
                        "nullreason", StringComparison.OrdinalIgnoreCase))))
                .ToDictionary(
                    p => p.Name,
                    p => p.Value.Type == JTokenType.Null
                        ? null
                        : (object)p.Value.ToString(),
                    StringComparer.OrdinalIgnoreCase);
            aspectData.PopulateFrom(CreateAPVDictionary(values, _propertyTypes));
        }

        /// <summary>
        /// A results cache is refused. This engine reads the response the
        /// <see cref="CloudRequestEngine"/> gives for the request, and its
        /// Evidence filter does not say what that response depends on, so
        /// a cache here would give one request's result to another. Cache
        /// results in the Cloud Request Engine instead.
        /// </summary>
        /// <param name="cache">The cache, which is disposed of.</param>
        /// <exception cref="PipelineConfigurationException">
        /// Always.
        /// </exception>
        public override void SetCache(IFlowCache cache)
        {
            cache?.Dispose();
            throw new PipelineConfigurationException(
                $"The '{GetType().Name}' takes no results cache. Set one " +
                "on the CloudRequestEngine instead.");
        }

        /// <summary>
        /// The Value the <see cref="CloudRequestEngine"/> redeemed for the
        /// request, or null where it redeemed none: where the Pipeline has
        /// no Cloud Request Engine, where the call carried no sealed
        /// result, where the request failed, and where the response holds
        /// no Value that a sealed result redeems to.
        /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/human-confidence.md#where-the-sealed-result-is-not-redeemed">Specification</see>
        /// </summary>
        private string RedeemedValue(IFlowData data)
        {
            if (TryGetResponse(data, out var requestData, out var json) == false ||
                requestData.SealedResultSent == false ||
                string.IsNullOrEmpty(json))
            {
                return null;
            }
            JObject response;
            try
            {
                response = JObject.Parse(json);
            }
            catch (JsonReaderException)
            {
                return null;
            }
            var value = Derived(response)?.GetValue(
                Constants.HUMAN_CONFIDENCE,
                StringComparison.OrdinalIgnoreCase);
            var text = value?.Type == JTokenType.String
                ? value.Value<string>()
                : null;
            return REDEEMED_VALUES.Contains(text, StringComparer.Ordinal)
                ? text
                : null;
        }

        /// <summary>
        /// The Cloud Request Engine's data and response for the request.
        /// False where there is none to read. A failed request is already
        /// recorded as the Cloud Request Engine's error, so nothing is
        /// recorded or thrown here.
        /// </summary>
        private bool TryGetResponse(
            IFlowData data,
            out CloudRequestData requestData,
            out string json)
        {
            requestData = null;
            json = null;
            try
            {
                requestData = data.GetFromElement(RequestEngine.GetInstance());
                json = requestData?.JsonResponse;
            }
            catch (PipelineException)
            {
                // No Cloud Request Engine, or a failure met while waiting
                // for its lazy loaded result.
                return false;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
            catch (PropertyMissingException)
            {
                return false;
            }
            catch (AggregateException)
            {
                return false;
            }
            catch (TimeoutException)
            {
                return false;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            return requestData != null;
        }

        /// <summary>
        /// The <c>derived</c> member of a response, in any letter case, or
        /// null where there is none.
        /// </summary>
        private static JObject Derived(JObject response) =>
            response.GetValue(
                Constants.DERIVED_ELEMENT_DATA_KEY,
                StringComparison.OrdinalIgnoreCase) as JObject;
    }
}
