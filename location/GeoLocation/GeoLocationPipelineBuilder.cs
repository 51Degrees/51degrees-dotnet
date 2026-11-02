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

using FiftyOne.GeoLocation.Core;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;

namespace FiftyOne.GeoLocation
{
    /// <summary>
    /// Fluent builder used to create a <see cref="IPipeline"/> that 
    /// includes a reverse geocoding engine.
    /// </summary>
    public class GeoLocationPipelineBuilder
    {
        /// <summary>
        /// The factory to use when building loggers.
        /// </summary>
        public ILoggerFactory LoggerFactory { get; private set; }
        /// <summary>
        /// The HttpClient to use when making HTTP requests.
        /// </summary>
        public HttpClient HttpClient { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public GeoLocationPipelineBuilder() : this(new LoggerFactory())
        { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="loggerFactory">
        /// The factory to use for creating loggers within the pipeline.
        /// </param>
        public GeoLocationPipelineBuilder(
            ILoggerFactory loggerFactory) :
            this(loggerFactory, new HttpClient())
        { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="loggerFactory">
        /// The factory to use for creating loggers within the pipeline.
        /// </param>
        /// <param name="httpClient">
        /// The HTTP client to use within the pipeline.
        /// </param>
        public GeoLocationPipelineBuilder(
            ILoggerFactory loggerFactory,
            HttpClient httpClient)
        {
            LoggerFactory = loggerFactory;
            HttpClient = httpClient;
        }

        /// <summary>
        /// Use the 51Degrees cloud service to perform Geo Location.
        /// </summary>
        /// <param name="resourceKey">
        /// The resource key to use when querying the cloud service. 
        /// Obtain one from https://configure.51degrees.com?utm_source=code&amp;utm_medium=comment&amp;utm_campaign=location-dotnet&amp;utm_content=fiftyone.geolocation-geolocationpipelinebuilder.cs&amp;utm_term=usecloud
        /// </param>
        /// <param name="provider">
        /// The Geo Location provider to use.
        /// </param>
        /// <returns>
        /// A builder that can be used to configure and build a pipeline
        /// that will use the cloud Geo Location engine.
        /// </returns>
        public GeoLocationCloudPipelineBuilder UseCloud(string resourceKey, GeoLocationProvider provider)
        {
            return new GeoLocationCloudPipelineBuilder(LoggerFactory, HttpClient, provider)
                .SetResourceKey(resourceKey);
        }

        /// <summary>
        /// Use the 51Degrees cloud service to perform Geo Location.
        /// </summary>
        /// <param name="resourceKey">
        /// The resource key to use when querying the cloud service. 
        /// Obtain one from https://configure.51degrees.com?utm_source=code&amp;utm_medium=comment&amp;utm_campaign=location-dotnet&amp;utm_content=fiftyone.geolocation-geolocationpipelinebuilder.cs&amp;utm_term=usecloud-2
        /// </param>
        /// <param name="endpoint">
        /// The 51Degrees cloud URL.
        /// </param>
        /// <param name="provider">
        /// The Geo Location provider to use.
        /// </param>
        /// <returns>
        /// A builder that can be used to configure and build a pipeline
        /// that will use the cloud Geo Location engine.
        /// </returns>
        public GeoLocationCloudPipelineBuilder UseCloud(string resourceKey, 
            string endpoint, 
            GeoLocationProvider provider)
        {
            return UseCloud(resourceKey, provider)
                .SetEndPoint(new Uri(endpoint));
        }
    }
}
