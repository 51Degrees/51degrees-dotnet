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
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
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
    /// Cloud engine for the <c>browserderived</c> Element Data, which holds
    /// the 51Degrees browser tests the cloud service gives a request that
    /// names HumanConfidence. With it in a Pipeline whose web integration
    /// serves the client script, the page runs the tests and sends their
    /// answers back through the Pipeline, so the page's calls to the cloud
    /// service carry the transaction the <see cref="CloudRequestEngine"/>
    /// keeps for the page.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/human-confidence.md#when-a-pages-results-complete">Specification</see>
    /// </summary>
    public class BrowserDerivedCloudEngine : CloudAspectEngineBase<IBrowserDerivedCloudData>
    {
        /// <summary>
        /// The element data key, and the member of the cloud service's
        /// response that holds the browser tests.
        /// </summary>
        public const string ELEMENT_DATA_KEY = "browserderived";

        private static readonly JsonConverter[] JSON_CONVERTERS =
        {
            new CloudJsonConverter(),
        };

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="logger">
        /// The logger to use.
        /// </param>
        /// <param name="aspectDataFactory">
        /// The factory function to use when the engine creates an
        /// <see cref="IBrowserDerivedCloudData"/> instance.
        /// </param>
        public BrowserDerivedCloudEngine(
            ILogger<AspectEngineBase<IBrowserDerivedCloudData, IAspectPropertyMetaData>> logger,
            Func<IPipeline,
                FlowElementBase<IBrowserDerivedCloudData, IAspectPropertyMetaData>,
                IBrowserDerivedCloudData> aspectDataFactory)
            : base(logger, aspectDataFactory)
        {
        }

        /// <inheritdoc/>
        public override string ElementDataKey => ELEMENT_DATA_KEY;

        /// <inheritdoc/>
        public override IEvidenceKeyFilter EvidenceKeyFilter { get; } =
            new EvidenceKeyFilterWhitelist(new List<string>());

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

        /// <inheritdoc/>
        protected override void ProcessCloudEngine(
            IFlowData data,
            IBrowserDerivedCloudData aspectData,
            string json)
        {
            if (aspectData == null)
            {
                throw new ArgumentNullException(nameof(aspectData));
            }
            var section = JObject.Parse(json).GetValue(
                ELEMENT_DATA_KEY,
                StringComparison.OrdinalIgnoreCase) as JObject;
            if (section == null)
            {
                return;
            }
            var values = JsonConvert.DeserializeObject<Dictionary<string, object>>(
                section.ToString(),
                new JsonSerializerSettings { Converters = JSON_CONVERTERS });
            aspectData.PopulateFrom(CreateAPVDictionary(
                values, PropertiesForResponse.ToList()));
        }

        /// <summary>
        /// Every property is given as an
        /// <see cref="IAspectPropertyValue{T}"/> of the type the cloud
        /// service names, so a value the cloud service gives as null
        /// carries its reason.
        /// </summary>
        /// <param name="propertyMetaData">The property's meta-data.</param>
        /// <param name="parentObjectType">The type the property is on.</param>
        /// <returns>The type of the property's values.</returns>
        protected override Type GetPropertyType(
            PropertyMetaData propertyMetaData,
            Type parentObjectType) =>
            typeof(IAspectPropertyValue<>).MakeGenericType(
                base.GetPropertyType(propertyMetaData, parentObjectType));
    }
}
