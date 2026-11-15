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

using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace FiftyOne.Pipeline.CloudRequestEngine.FlowElements
{
    /// <summary>
    /// Base class for cloud pipeline builders. Sets the options required to
    /// access the cloud service including endpoints and resource keys.
    /// </summary>
    /// <typeparam name="TBuilder"></typeparam>
    public abstract class CloudPipelineBuilderBase<TBuilder> : PrePackagedPipelineBuilderBase<TBuilder>
        where TBuilder : CloudPipelineBuilderBase<TBuilder>
    {
        /// <summary>
        /// The HTTP client to be used when making web requests
        /// </summary>
        protected HttpClient HttpClient { get; private set; }
        
        /// <summary>
        /// The base URL on the cloud service.
        /// </summary>
        protected Uri Url { get; set; }
        /// <summary>
        /// The URL to the JSON resource on the cloud service.
        /// </summary>
        protected Uri JsonEndpoint { get; set; }
        /// <summary>
        /// The URL for the AccessileProperties endpoint on the cloud service.
        /// </summary>
        protected string PropertiesEndpoint { get; set; } = string.Empty;
        /// <summary>
        /// The URL for the EvidenceKeys endpoint on the cloud service.
        /// </summary>
        protected string EvidenceKeysEndpoint { get; set; } = string.Empty;
        /// <summary>
        /// The resource key used to access the cloud service. Required
        /// unless a license key and requested properties are set.
        /// </summary>
        protected string ResourceKey { get; set; } = string.Empty;
        /// <summary>
        /// The license key, used alone or to add products to a resource key.
        /// </summary>
        protected string LicenceKey { get; set; } = string.Empty;

        /// <summary>
        /// The value to set for the Origin header when making requests
        /// to the cloud service.
        /// </summary>
        protected string CloudRequestOrigin { get; set; } = string.Empty;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="loggerFactory">
        /// The logger factory to use.
        /// </param>
        /// <param name="httpClient">
        /// </param>
        public CloudPipelineBuilderBase(
            ILoggerFactory loggerFactory,
            HttpClient httpClient) : base(loggerFactory)
        {
            HttpClient = httpClient;
        }

        /// <summary>
        /// Set the base path for the cloud service. This will update the URL 
        /// for the JSON endpoint and the AccessileProperties endpoint.
        /// </summary>
        /// <param name="url">
        /// The base URL to use.
        /// </param>
        /// <returns>This builder</returns>
        public TBuilder SetEndPoint(string url)
        {
            Url = new Uri(url);
            return this as TBuilder;
        }

        /// <summary>
        /// Set the base path for the cloud service. This will update the URL 
        /// for the JSON endpoint and the AccessileProperties endpoint.
        /// </summary>
        /// <param name="url">
        /// The base URL to use.
        /// </param>
        /// <returns>This builder</returns>
        public TBuilder SetEndPoint(Uri url)
        {
            Url = url;
            return this as TBuilder;
        }

        /// <summary>
        /// Set the URL to the JSON endpoint on the cloud service.
        /// </summary>
        /// <param name="url">
        /// The URL which points to the JSON endpoint.
        /// </param>
        /// <returns>This builder</returns>
        public TBuilder SetJSONEndpoint(string url)
        {
            JsonEndpoint = new Uri(url);
            return this as TBuilder;
        }

        /// <summary>
        /// Set the URL to the JSON endpoint on the cloud service.
        /// </summary>
        /// <param name="url">
        /// The URL which points to the JSON endpoint.
        /// </param>
        /// <returns>This builder</returns>
        public TBuilder SetJSONEndpoint(Uri url)
        {
            JsonEndpoint = url;
            return this as TBuilder;
        }

        /// <summary>
        /// Set the URL to the AccessibleProperties endpoint on the cloud 
        /// service.
        /// </summary>
        /// <param name="propertiesEndpoint">
        /// The URL to use
        /// </param>
        /// <returns>This builder</returns>
        public TBuilder SetPropertiesEndpoint(string propertiesEndpoint)
        {
            PropertiesEndpoint = propertiesEndpoint;
            return this as TBuilder;
        }

        /// <summary>
        /// Set the URL to the EvidenceKeys endpoint on the cloud 
        /// service.
        /// </summary>
        /// <param name="evidenceKeysEndpoint">
        /// The URL to use
        /// </param>
        /// <returns>This builder</returns>
        public TBuilder SetEvidenceKeysEndpoint(string evidenceKeysEndpoint)
        {
            EvidenceKeysEndpoint = evidenceKeysEndpoint;
            return this as TBuilder;
        }

        /// <summary>
        /// Set the resource key which will be used to query the cloud service.
        /// Obtain a resource key @ https://51degrees.com/configure?utm_source=code&amp;utm_medium=comment&amp;utm_campaign=51degrees-dotnet&amp;utm_content=fiftyone.pipeline.cloudrequestengine-flowelements-cloudpipelinebuilderbase.cs&amp;utm_term=setresourcekey
        /// </summary>
        /// <param name="key">The resource key to use</param>
        /// <returns>This builder</returns>
        public TBuilder SetResourceKey(string key)
        {
            ResourceKey = key;
            return this as TBuilder;
        }

        /// <summary>
        /// Set the license key to authenticate with. A license key alone
        /// needs <see cref="SetRequestedProperties"/> to name the
        /// properties wanted. Beside a resource key it adds the requested
        /// properties the license grants to what the resource key carries.
        /// </summary>
        /// <param name="key"></param>
        /// <returns>This builder</returns>
        public TBuilder SetLicenseKey(string key)
        {
            LicenceKey = key;
            return this as TBuilder;
        }

        /// <summary>
        /// The fully qualified properties to ask the cloud service for.
        /// </summary>
        protected List<string> RequestedProperties { get; } =
            new List<string>();

        /// <summary>
        /// Name the properties the cloud service should return, as fully
        /// qualified names such as 'device.ismobile'. Required with a
        /// license key alone. Beside a resource key the cloud service adds
        /// them to the properties the resource key carries.
        /// </summary>
        /// <param name="properties">The property names.</param>
        /// <returns>This builder</returns>
        public TBuilder SetRequestedProperties(IEnumerable<string> properties)
        {
            if (properties == null)
            {
                throw new ArgumentNullException(nameof(properties));
            }
            RequestedProperties.AddRange(properties);
            return this as TBuilder;
        }

        /// <summary>
        /// The value to set for the Origin header when making requests
        /// to the cloud service.
        /// This is used by the cloud service to check that the request
        /// is being made from a origin matching those allowed by the 
        /// resource key.
        /// For more detail, see the 'Request Headers' section in the 
        /// <a href="https://cloud.51degrees.com/api-docs/index.html">cloud documentation</a>.
        /// </summary>
        /// <param name="cloudRequestOrigin">
        /// The value to use for the Origin header.
        /// </param>
        /// <returns>
        /// This builder
        /// </returns>
        public TBuilder SetCloudRequestOrigin(string cloudRequestOrigin)
        {
            CloudRequestOrigin = cloudRequestOrigin;
            return this as TBuilder;
        }

        /// <summary>
        /// The seconds a request to the cloud service may take before it
        /// is given up. The same setting the
        /// <see cref="CloudRequestEngineBuilder.SetTimeOutSeconds(int)"/>
        /// method sets on the engine this builder creates.
        /// </summary>
        protected int CloudRequestTimeoutSeconds { get; set; }
            = Constants.CLOUD_REQUEST_TIMEOUT_DEFAULT_SECONDS;

        /// <summary>
        /// Set the seconds a request to the cloud service may take before
        /// it is given up. The default of
        /// <see cref="Constants.CLOUD_REQUEST_TIMEOUT_DEFAULT_SECONDS"/>
        /// covers the first request to the service, including the DNS
        /// lookup and the TLS handshake, so a cold machine may need more.
        /// </summary>
        /// <param name="timeoutSeconds">
        /// The timeout in seconds. A value below one means no timeout.
        /// </param>
        /// <returns>
        /// This builder
        /// </returns>
        public TBuilder SetCloudRequestTimeOutSeconds(int timeoutSeconds)
        {
            CloudRequestTimeoutSeconds = timeoutSeconds;
            return this as TBuilder;
        }

        /// <summary>
        /// Create and return a new pipeline based on the configuration
        /// so far.
        /// </summary>
        /// <returns>
        /// A new <see cref="IPipeline"/>.
        /// </returns>
        public override IPipeline Build()
        {
            // Configure and build the cloud request engine
            var cloudRequestEngineBuilder = new CloudRequestEngineBuilder(LoggerFactory, HttpClient);
            if (LazyLoading)
            {
                cloudRequestEngineBuilder.SetLazyLoading(new LazyLoadingConfiguration(
                    (int)LazyLoadingTimeout.TotalMilliseconds,
                    LazyLoadingCancellationToken));
            }
            if (ResultsCache)
            {
                cloudRequestEngineBuilder.SetCache(new CacheConfiguration() { Size = ResultsCacheSize });
            }
            if (Url != null && 
                Url.AbsoluteUri.Length != 0)
            {
                cloudRequestEngineBuilder.SetEndPoint(Url.AbsoluteUri);
            }
            if (JsonEndpoint != null &&
                JsonEndpoint.AbsoluteUri.Length != 0)
            {
                cloudRequestEngineBuilder.SetDataEndpoint(JsonEndpoint.AbsoluteUri);
            }
            if (string.IsNullOrEmpty(EvidenceKeysEndpoint) == false)
            {
                cloudRequestEngineBuilder.SetEvidenceKeysEndpoint(EvidenceKeysEndpoint);
            }
            if (string.IsNullOrEmpty(PropertiesEndpoint) == false)
            {
                cloudRequestEngineBuilder.SetPropertiesEndpoint(PropertiesEndpoint);
            }
            if (string.IsNullOrEmpty(ResourceKey) == false)
            {
                cloudRequestEngineBuilder.SetResourceKey(ResourceKey);
            }
            if (string.IsNullOrEmpty(LicenceKey) == false)
            {
                cloudRequestEngineBuilder.SetLicenseKey(LicenceKey);
            }
            cloudRequestEngineBuilder.SetRequestedProperties(
                RequestedProperties);
            if (string.IsNullOrEmpty(CloudRequestOrigin) == false)
            {
                cloudRequestEngineBuilder.SetCloudRequestOrigin(CloudRequestOrigin);
            }
            cloudRequestEngineBuilder.SetTimeOutSeconds(CloudRequestTimeoutSeconds);
            var cloudRequestEngine = cloudRequestEngineBuilder.Build();

            // Add the cloud request engine as the first element.
            FlowElements.Insert(0, cloudRequestEngine);

            // Build and return the pipeline
            return base.Build();
        }
    }
}
