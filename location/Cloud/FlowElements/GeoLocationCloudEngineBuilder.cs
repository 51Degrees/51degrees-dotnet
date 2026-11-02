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

using System;
using System.Collections.Generic;
using System.Text;
using FiftyOne.GeoLocation.Cloud.Data;
using FiftyOne.GeoLocation.Core;
using FiftyOne.GeoLocation.Core.Data;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.CloudRequestEngine.Services;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging;

namespace FiftyOne.GeoLocation.Cloud.FlowElements
{
    /// <summary>
    /// Fluent builder used to build a reverse geocoding engine that uses 
    /// the 51Degrees cloud service.
    /// </summary>
    public class GeoLocationCloudEngineBuilder :
        AspectEngineBuilderBase<GeoLocationCloudEngineBuilder, GeoLocationCloudEngine>
    {
        private ILoggerFactory _loggerFactory;
        private GeoLocationProvider _provider;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="loggerFactory">
        /// The factory to use when creating loggers
        /// </param>
        public GeoLocationCloudEngineBuilder(
            ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        /// <summary>
        /// Set the provider of the data that will be used by the engine.
        /// </summary>
        /// <remarks>
        /// This method exists in order to allow configuration from a file,
        /// as strings are not automatically parsed to enums.
        /// If calling from code, use the 
        /// <see cref="Build(GeoLocationProvider)"/> method instead.
        /// </remarks>
        /// <param name="provider">
        /// The provider name. This should be one of the entries in
        /// <see cref="GeoLocationProvider"/>.
        /// </param>
        /// <returns>
        /// This builder.
        /// </returns>
        public GeoLocationCloudEngineBuilder SetGeoLocationProvider(string provider)
        {
            if (Enum.TryParse(provider, out _provider) == false)
            {
                throw new PipelineConfigurationException($"{provider} is not a valid " +
                        $"GeoLocationProvider, please select from: " +
                        $"{string.Join(", ", Enum.GetNames(typeof(GeoLocationProvider)))}");
            }
            return this;
        }

        /// <summary>
        /// Build and return a new <see cref="GeoLocationCloudEngine"/>.
        /// </summary>
        /// <param name="provider">
        /// The provider of the data that will be used by the engine.
        /// </param>
        /// <returns>
        /// A new <see cref="GeoLocationCloudEngine"/>.
        /// </returns>
        public GeoLocationCloudEngine Build(GeoLocationProvider provider)
        {
            _provider = provider;
            return BuildEngine();
        }

        /// <summary>
        /// Build and return a new <see cref="GeoLocationCloudEngine"/>.
        /// </summary>
        /// <returns>
        /// A new <see cref="GeoLocationCloudEngine"/>.
        /// </returns>
        public GeoLocationCloudEngine Build()
        {
            return BuildEngine();
        }

        /// <summary>
        /// Called by the base class to create a new engine.
        /// </summary>
        /// <param name="properties">
        /// The list of properties that this engine should populate.
        /// </param>
        /// <returns>
        /// A new <see cref="GeoLocationCloudEngine"/> instance.
        /// </returns>
        protected override GeoLocationCloudEngine NewEngine(List<string> properties)
        {
            return new GeoLocationCloudEngine(
               _loggerFactory.CreateLogger<GeoLocationCloudEngine>(),
               CreateAspectData,
               _provider);
        }

        /// <summary>
        /// Factory method used to create <see cref="GeoDataCloud"/> instances.
        /// </summary>
        /// <param name="pipeline">
        /// The <see cref="IPipeline"/> that is creating the new data 
        /// instance.
        /// </param>
        /// <param name="engine">
        /// The <see cref="IAspectEngine"/> that is creating the new data 
        /// instance.
        /// </param>
        /// <returns>
        /// A new <see cref="GeoDataCloud"/> instance, ready to be populated.
        /// </returns>
        private IGeoData CreateAspectData(IPipeline pipeline, 
            FlowElementBase<IGeoData, IAspectPropertyMetaData> engine)
        {
            return new GeoDataCloud(
                _loggerFactory.CreateLogger<GeoData>(),
                pipeline,
                (IAspectEngine)engine,
                MissingPropertyServiceCloud.Instance);
        }
    }
}
