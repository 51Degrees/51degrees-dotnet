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

using FiftyOne.Pipeline.Core.Configuration;

namespace FiftyOne.Pipeline.Web.Shared
{
    /// <summary>
    /// Extends the PipelineOptions class to add web specific options.
    /// </summary>
    public class PipelineWebIntegrationOptions : PipelineOptions
    {       
        /// <summary>
        /// Constructor
        /// </summary>
        public PipelineWebIntegrationOptions()
        {
            ClientSideEvidenceEnabled = true;
            UseAsyncScript = true;
            UseSetHeaderProperties = true;
            SequenceElementEnabled = true;
            SetHeadersElementEnabled = true;
        }

        /// <summary>
        /// The default value of <see cref="JavaScriptPath"/>.
        /// </summary>
        public const string DefaultJavaScriptPath = "/51Degrees.core.js";

        /// <summary>
        /// The default value of <see cref="JsonPath"/>.
        /// </summary>
        public const string DefaultJsonPath = "/51dpipeline/json";

        private string _javaScriptPath = DefaultJavaScriptPath;
        private string _jsonPath = DefaultJsonPath;

        /// <summary>
        /// True if client-side properties should be enabled. If enabled
        /// (and the JavaScriptBundlerElement added to the Pipeline), a
        /// client-side JavaScript file will be served at the path
        /// <see cref="JavaScriptPath"/>.
        /// </summary>
        public bool ClientSideEvidenceEnabled { get; set; }

        /// <summary>
        /// Flag to enable/disable the use of the async attribute for
        /// the client side script.
        /// Defaults to true.
        /// </summary>
        public bool UseAsyncScript { get; set; }

        /// <summary>
        /// Flag to enable/disable a feature that will automatically set
        /// the values of HTTP headers in the response in order to request
        /// additional information.
        /// Defaults to true.
        /// </summary>
        public bool UseSetHeaderProperties { get; set; }

        /// <summary>
        /// True to add the Sequence Element to the Pipeline when it is
        /// not already in the configured elements. If false, the web
        /// integration does not add it. The JSON Builder Element needs the
        /// sequence, so set <see cref="ClientSideEvidenceEnabled"/> to
        /// false as well, or list the Sequence Element in the elements.
        /// Defaults to true.
        /// </summary>
        public bool SequenceElementEnabled { get; set; }

        /// <summary>
        /// True to add the Set Headers Element to the Pipeline when it is
        /// not already in the configured elements. If false, the web
        /// integration does not add it. This differs from
        /// <see cref="UseSetHeaderProperties"/>, which stops the headers
        /// being written to the response.
        /// Defaults to true.
        /// </summary>
        public bool SetHeadersElementEnabled { get; set; }

        /// <summary>
        /// The path of the request that is answered with the client-side
        /// JavaScript. A request whose path ends with this value is
        /// intercepted. A blank value gives the default, which is
        /// /51Degrees.core.js.
        /// </summary>
        public string JavaScriptPath
        {
            get => _javaScriptPath;
            set => _javaScriptPath = string.IsNullOrWhiteSpace(value) ?
                DefaultJavaScriptPath : value;
        }

        /// <summary>
        /// The path of the request that is answered with the client-side
        /// JSON. A request whose path ends with this value is intercepted.
        /// When the web integration adds the JavaScript Builder Element
        /// itself, the script calls back to this path. A JavaScript Builder
        /// Element listed in the configured elements needs its Endpoint
        /// set to the same value. A blank value gives the default, which is
        /// /51dpipeline/json.
        /// </summary>
        public string JsonPath
        {
            get => _jsonPath;
            set => _jsonPath = string.IsNullOrWhiteSpace(value) ?
                DefaultJsonPath : value;
        }
    }
}
