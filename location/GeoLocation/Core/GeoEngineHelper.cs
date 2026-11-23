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

using FiftyOne.GeoLocation.Core.Data;
using FiftyOne.GeoLocation.Core.Templates;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Data.Types;
using FiftyOne.Pipeline.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace FiftyOne.GeoLocation.Core
{
    /// <summary>
    /// Helper class that contains shared functionality required
    /// by geolocation engines.
    /// </summary>
    /// <remarks>
    /// This functionality is here rather than a geo engine base 
    /// class because different geolocation engines require
    /// different engine base class functionality.
    /// </remarks>
    public static class GeoEngineHelper
    {
        /// <summary>
        /// Try getting the specified coordinate keys from evidence 
        /// and adding them to a <see cref="GeoEvidence"/> instance.
        /// </summary>
        /// <param name="evidence">
        /// evidence collection to get the coordinates from
        /// </param>
        /// <param name="geoEvidence">
        /// <see cref="GeoEvidence"/> to add the coordinates to
        /// </param>
        /// <param name="latitudeKey">
        /// The name of the key for the latitude value in the evidence
        /// </param>
        /// <param name="longitudeKey">
        /// The name of the key for the longitude value in the evidence
        /// </param>
        /// <returns>
        /// True if both values were found in the evidence. False if not.
        /// </returns>
        private static bool TryAddCoordinates(
            IReadOnlyDictionary<string, object> evidence,
            GeoEvidence geoEvidence,
            string latitudeKey,
            string longitudeKey)
        {
            bool found = false;
            if (evidence.ContainsKey(latitudeKey) &&
                evidence.ContainsKey(longitudeKey))
            {
                geoEvidence.Latitude = evidence[latitudeKey].ToString();
                geoEvidence.Longitude = evidence[longitudeKey].ToString();
                found = true;
            }
            return found;
        }

        /// <summary>
        /// Use evidence from the specified <see cref="IFlowData"/> 
        /// instance to construct a query 
        /// </summary>
        /// <param name="data">
        /// The flow data to process
        /// </param>
        /// <param name="webClient">
        /// The <see cref="HttpClient"/> to use when making a request
        /// to the geolocation service.
        /// </param>
        /// <param name="addExtraParams">
        /// A function that will set any additional fields in 
        /// <see cref="GeoEvidence"/> that may be unique to the calling engine.
        /// </param>
        /// <param name="formatRequest">
        /// A function that will create a URL that can be used to
        /// query a web service based on the supplied 
        /// <see cref="GeoEvidence"/>.
        /// </param>
        /// <returns>
        /// A new <see cref="GeoLookupResult"/> instance.
        /// If the supplied <see cref="IFlowData"/> contains the required 
        /// evidence then this will contain the raw response from the web 
        /// service.
        /// If not, it will contain the JavaScript to run on the client
        /// device in order to supply the required evidence.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if a required parameter is null.
        /// </exception>
        public static GeoLookupResult PerformGeoLookup(
            IFlowData data,
            HttpClient webClient,
            Action<IReadOnlyDictionary<string, object>, GeoEvidence> addExtraParams, 
            Func<GeoEvidence, string> formatRequest)
        {
            if(data == null) { throw new ArgumentNullException(nameof(data)); }

            GeoLookupResult result = new GeoLookupResult();
            // Get the evidence as a dictionary.
            var allEvidence = data.GetEvidence().AsDictionary();
            GeoEvidence geoEvidence = new GeoEvidence();

            // Try to get any coordinates from the location key, the cookie,
            // then overrides. If any of these find latitude or longitude
            // values, then getServerSide will be set to true.
            bool getServerSide = TryAddCoordinates(
                    allEvidence,
                    geoEvidence,
                    Constants.EVIDENCE_GEO_LAT_KEY,
                    Constants.EVIDENCE_GEO_LON_KEY);
            getServerSide = getServerSide ||
                TryAddCoordinates(
                    allEvidence,
                    geoEvidence,
                    Constants.EVIDENCE_GEO_LAT_COOKIE_KEY,
                    Constants.EVIDENCE_GEO_LON_COOKIE_KEY);
            getServerSide = getServerSide ||
                TryAddCoordinates(
                    allEvidence,
                    geoEvidence,
                    Constants.EVIDENCE_GEO_LAT_PARAM_KEY,
                    Constants.EVIDENCE_GEO_LON_PARAM_KEY);

            if (getServerSide == true)
            {
                // There are coordinates in the evidence.
                // Add the IP address.
                if (allEvidence.ContainsKey(Pipeline.Core.Constants.EVIDENCE_CLIENTIP_KEY))
                {
                    geoEvidence.IpAddress =
                        (string)allEvidence[Pipeline.Core.Constants.EVIDENCE_CLIENTIP_KEY];
                }

                // Add any extra items to the geo evidence.
                addExtraParams?.Invoke(allEvidence, geoEvidence);

                // Format the URL correctly for whatever service
                // we're querying
                var uri = formatRequest(geoEvidence);
                // Send the request and store the response in
                // the result.
                result.GeoServiceResponse = webClient.GetStringAsync(uri).Result;
            }
            else
            {
                // No location data found in evidence so set the 
                // JavaScript value in the geo data.
                // If this JavaScript is executed on the client device 
                // then we'll have location data on the next request
                // the device makes.
                result.ClientsideEvidenceJS = new JavaScript(
                    new JavaScriptResource(
                        Constants.EVIDENCE_GEO_LAT,
                        Constants.EVIDENCE_GEO_LON)
                    .TransformText());
            }

            return result;
        }
    }
}
