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

using FiftyOne.Common.TestHelpers;
using FiftyOne.GeoLocation.Cloud;
using FiftyOne.GeoLocation.Cloud.FlowElements;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using System.Collections.Generic;
using System.Net.Http;

namespace FiftyOne.GeoLocation.Tests
{
    /// <summary>
    /// Implementation of the <see cref="IWrapper"/> interface using a 
    /// 51Degrees Geo-Location engine.
    /// </summary>
    public class Wrapper51Degrees: IWrapper
    {
        protected static readonly TestLoggerFactory _logger = new TestLoggerFactory();

        public CloudRequestEngine RequestEngine { get; private set; }
        public GeoLocationCloudEngine Engine { get; private set; }
        public IPipeline Pipeline { get; private set; }

        /// <summary>
        /// Constructor. Create a Cloud Request engine and a Geo-Location engine 
        /// and add these to a pipeline. Gets the resource key for the Cloud 
        /// Request engine from environment variables.
        /// </summary>
        public Wrapper51Degrees()
        {
            var resourceKey = System.Environment.GetEnvironmentVariable(
                Constants.RESOURCE_KEY_ENV_VAR) ?? "AQQNX4o8_wxDhi_U2Eg";

            RequestEngine = new CloudRequestEngineBuilder(_logger, new HttpClient())
                .SetResourceKey(resourceKey)
                .Build();
            Engine = new GeoLocationCloudEngineBuilder(_logger)
                .SetGeoLocationProvider("FiftyOneDegrees")
                .Build();

            Pipeline = new PipelineBuilder(_logger)
                .AddFlowElement(RequestEngine)
                .AddFlowElement(Engine)
                .Build();
        }

        public IEnumerable<IAspectPropertyMetaData> Properties => Engine.Properties;

        public IAspectEngine GetEngine()
        {
            return Engine;
        }
    }
}
