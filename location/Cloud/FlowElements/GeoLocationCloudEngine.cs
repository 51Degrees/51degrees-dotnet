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

using FiftyOne.GeoLocation.Cloud.Data;
using FiftyOne.GeoLocation.Core;
using FiftyOne.GeoLocation.Core.Data;
using FiftyOne.Pipeline.CloudRequestEngine.Data;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Data.Types;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FiftyOne.GeoLocation.Cloud
{
    /// <summary>
    /// Reverse geocoding engine that uses the 51Degrees cloud service.
    /// </summary>
    public class GeoLocationCloudEngine : CloudAspectEngineBase<IGeoData>
    {
        private string _elementDataKey;

        /// <summary>
        /// The key to use for storing this engine's data in a 
        /// <see cref="IFlowData"/> instance.
        /// </summary>
        public override string ElementDataKey => _elementDataKey;

        /// <summary>
        /// The filter that defines the evidence that is used by 
        /// this engine.
        /// This engine needs no evidence as it works from the response
        /// from the <see cref="ICloudRequestEngine"/>.
        /// </summary>
        public override IEvidenceKeyFilter EvidenceKeyFilter =>
            new EvidenceKeyFilterWhitelist(new List<string>());

        /// <summary>
        /// A string identifying the provider of the data that powers 
        /// this engine.
        /// </summary>
        public string DataProviderPrefix { get; private set; }

        private static JsonConverter[] JSON_CONVERTERS = new JsonConverter[]
        {
            new CloudJsonConverter()
        };

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">
        /// The logger used by this instance.
        /// </param>
        /// <param name="aspectDataFactory">
        /// A factory function that is used to create <see cref="IGeoData"/>
        /// instances.
        /// </param>
        /// <param name="provider">
        /// The <see cref="GeoLocationProvider"/> that will be used to
        /// power this engine.
        /// </param>
        public GeoLocationCloudEngine(
            ILogger<GeoLocationCloudEngine> logger,
            Func<IPipeline, FlowElementBase<IGeoData, IAspectPropertyMetaData>, IGeoData> aspectDataFactory,
            GeoLocationProvider provider)
            : base(logger, aspectDataFactory)
        {
            switch (provider)
            {
                case GeoLocationProvider.FiftyOneDegrees:
                    _elementDataKey = "location";
                    DataProviderPrefix = "OSM";
                    break;
                default:
                    throw new PipelineConfigurationException($"provider " +
                        $"'{provider}' not supported by this version of " +
                        $"the geolocation cloud engine. Must be " +
                        $"'DigitalElement' or 'FiftyOneDegrees'");
            }
        }

        /// <summary>
        /// Perform the processing for this engine:
        /// <list type="number">
        /// <item>
        ///     <description>
        ///     Extract properties relevant to this engine from the JSON 
        ///     response.
        ///     </description>
        /// </item>
        /// <item>
        ///     <description>
        ///     Deserialize JSON data to populate an 
        ///     <see cref="IGeoData"/> instance.
        ///     </description>
        /// </item>
        /// </list>
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> instance containing data for the 
        /// current request.
        /// </param>
        /// <param name="aspectData">
        /// The <see cref="IGeoData"/> instance to populate with values.
        /// </param>
        /// <param name="json">
        /// The JSON data from the <see cref="CloudRequestEngine"/> 
        /// response.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if a required parameter is null
        /// </exception>
        /// <exception cref="PipelineConfigurationException">
        /// Thrown if the current pipeline configuration does not allow this
        /// engine to perform processing.
        /// </exception>
        protected override void ProcessCloudEngine(IFlowData data, IGeoData aspectData, string json)
        {
            if (aspectData == null) throw new ArgumentNullException(nameof(aspectData));
            
            // Extract data from JSON to the aspectData instance.
            var dictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            var propertyValues = JsonConvert.DeserializeObject<Dictionary<string, object>>(dictionary[ElementDataKey].ToString(),
                new JsonSerializerSettings()
                {
                    Converters = JSON_CONVERTERS,
                });

            var location = CreateAPVDictionary(propertyValues, Properties.ToList());
            aspectData.PopulateFrom(location);
        }

        /// <summary>
        /// Get the type of property from its name.
        /// </summary>
        /// <param name="propertyMetaData"></param>
        /// <param name="parentObjectType"></param>
        /// <returns></returns>
        protected override Type GetPropertyType(
            PropertyMetaData propertyMetaData,
            Type parentObjectType)
        {
            if (propertyMetaData == null)
            {
                throw new ArgumentNullException(nameof(propertyMetaData));
            }
            if (GeoDataCloud.TryGetPropertyType(
                    propertyMetaData.Name,
                    out var type))
            {
                return type;
            }
            else
            {
                return base.GetPropertyType(propertyMetaData, parentObjectType);
            }
        }
    }
}
