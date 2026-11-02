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

using FiftyOne.GeoLocation.Cloud.FlowElements;
using FiftyOne.GeoLocation.Core;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging;
using System.Net.Http;

namespace FiftyOne.GeoLocation
{
    /// <summary>
    /// Fluent builder used to create a <see cref="IPipeline"/>
    /// that includes a location engine that is backed by
    /// the 51Degrees cloud service.
    /// </summary>
    public class GeoLocationCloudPipelineBuilder:
        CloudPipelineBuilderBase<GeoLocationCloudPipelineBuilder>
    {
        private GeoLocationProvider _provider;

        /// <summary>
        /// Internal Constructor.
        /// This builder should only be created through the 
        /// <see cref="GeoLocationPipelineBuilder"/> 
        /// </summary>
        /// <param name="loggerFactory">
        /// The factory to use when creating logger instances.
        /// </param>
        /// <param name="httpClient">
        /// The <see cref="HttpClient"/> to use when sending HTTP requests.
        /// </param>
        /// <param name="provider">
        /// The location services provider.
        /// </param>
        internal GeoLocationCloudPipelineBuilder(
            ILoggerFactory loggerFactory,
            HttpClient httpClient,
            GeoLocationProvider provider) : base(loggerFactory, httpClient)
        {
            _provider = provider;
        }

        /// <summary>
        /// Build and return a new pipeline instance with the configured 
        /// settings.
        /// </summary>
        /// <returns>
        /// A new <see cref="IPipeline"/>.
        /// </returns>
        public override IPipeline Build()
        {
            IAspectEngine geoLocationEngine = null;
            var geoLocationEngineBuilder =
                new GeoLocationCloudEngineBuilder(LoggerFactory);
            if (LazyLoading)
            {
                geoLocationEngineBuilder.SetLazyLoading(new LazyLoadingConfiguration(
                    (int)LazyLoadingTimeout.TotalMilliseconds,
                    LazyLoadingCancellationToken));
            }
            geoLocationEngine = geoLocationEngineBuilder.Build(_provider);

            FlowElements.Add(geoLocationEngine);

            // Build and return the pipeline
            return base.Build();
        }
    }
}
