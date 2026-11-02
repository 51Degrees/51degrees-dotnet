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
using FiftyOne.GeoLocation.Core;
using FiftyOne.GeoLocation.Core.Data;
using FiftyOne.Pipeline.Engines.TestHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;
using System.Net.Http;

namespace FiftyOne.GeoLocation.Tests
{

    /// <summary>
    /// Tests the GeoEngineHelper methods
    /// </summary>
    [TestClass]
    public class GeoEngineHelperTests
    {
        private Mock<MockHttpMessageHandler> _httpHandler;
        private HttpClient _testHttpClient;

        /// <summary>
        /// Test initialization sets up the httpclient used in the lookup with 
        /// a mock handler so that a real geo-location api is not needed.
        /// </summary>
        [TestInitialize]
        public void Init()
        {
            _httpHandler = new Mock<MockHttpMessageHandler>() { CallBase = true };
            _httpHandler
                .Setup(h => h.Send(It.IsAny<HttpRequestMessage>()))
                .Returns(
                    new HttpResponseMessage(System.Net.HttpStatusCode.OK) 
                    { 
                        Content = new StringContent(Constants.JSON_RESPONSE) 
                    });

            _testHttpClient = new HttpClient(_httpHandler.Object);
        }

        /// <summary>
        /// Test with no evidence, confirm that no http calls are made
        /// </summary>
        [TestMethod]
        public void PerformGeoLookup_NoEvidence()
        {
            var flowData = MockFlowData.CreateFromEvidence(new Dictionary<string, object>(), true);

            GeoEngineHelper.PerformGeoLookup(flowData.Object, _testHttpClient, null, FormatUrl);

            _httpHandler.Verify(h => h.Send(It.IsAny<HttpRequestMessage>()), Times.Never);
        }

        /// <summary>
        /// Test with evidence from query params, check that one http call
        /// has been made to the geo-location api.
        /// </summary>
        [TestMethod]
        public void PerformGeoLookup_QueryEvidence()
        {
            var flowData = MockFlowData.CreateFromEvidence(new Dictionary<string, object>() {
                { GeoLocation.Constants.EVIDENCE_GEO_LAT_PARAM_KEY, Constants.TEST_LAT },
                { GeoLocation.Constants.EVIDENCE_GEO_LON_PARAM_KEY, Constants.TEST_LON }
            }, true);

            GeoEngineHelper.PerformGeoLookup(flowData.Object, _testHttpClient, null, FormatUrl);

            _httpHandler.Verify(h => h.Send(It.IsAny<HttpRequestMessage>()), Times.Once);
        }

        /// <summary>
        /// Test with evidence from cookies, check that one http call
        /// has been made to the geo-location api.
        /// </summary>
        [TestMethod]
        public void PerformGeoLookup_CookieEvidence()
        {
            var flowData = MockFlowData.CreateFromEvidence(new Dictionary<string, object>() {
                { GeoLocation.Constants.EVIDENCE_GEO_LAT_COOKIE_KEY, Constants.TEST_LAT },
                { GeoLocation.Constants.EVIDENCE_GEO_LON_COOKIE_KEY, Constants.TEST_LON }
            }, true);

            GeoEngineHelper.PerformGeoLookup(flowData.Object, _testHttpClient, null, FormatUrl);

            _httpHandler.Verify(h => h.Send(It.IsAny<HttpRequestMessage>()), Times.Once);
        }

        /// <summary>
        /// Test with evidence from all sources, check that one http call
        /// has been made to the geo-location api.
        /// </summary>
        [TestMethod]
        public void PerformGeoLookup_AllEvidence()
        {
            var flowData = MockFlowData.CreateFromEvidence(new Dictionary<string, object>() {
                { GeoLocation.Constants.EVIDENCE_GEO_LAT_KEY, Constants.TEST_LAT },
                { GeoLocation.Constants.EVIDENCE_GEO_LON_KEY, Constants.TEST_LON  },
                { GeoLocation.Constants.EVIDENCE_GEO_LAT_PARAM_KEY, Constants.TEST_LAT },
                { GeoLocation.Constants.EVIDENCE_GEO_LON_PARAM_KEY, Constants.TEST_LON  },
                { GeoLocation.Constants.EVIDENCE_GEO_LAT_COOKIE_KEY, Constants.TEST_LAT },
                { GeoLocation.Constants.EVIDENCE_GEO_LON_COOKIE_KEY, Constants.TEST_LON  }
            }, true);

            GeoEngineHelper.PerformGeoLookup(flowData.Object, _testHttpClient, null, FormatUrl);

            _httpHandler.Verify(h => h.Send(It.IsAny<HttpRequestMessage>()), Times.Once);
        }

        private string FormatUrl(GeoEvidence evidence)
        {
            return $"https://localhost" +
                $"?lat={evidence.Latitude}" +
                $"&lon={evidence.Longitude}" +
                $"&zoom={evidence.Zoom}" +
                $"&format=json";
        }
    }
}
