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

using FiftyOne.Pipeline.Core.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace FiftyOne.GeoLocation
{
    /// <summary>
    /// Static class containing various constants that are used by this 
    /// package and/or are helpful to callers. 
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming",
        "CA1707:Identifiers should not contain underscores",
        Justification = "51Degrees coding style is for constant names " +
            "to be all-caps with an underscore to separate words.")]
    public static class Constants
    {
        /// <summary>
        /// Complete evidence key for latitude that is supplied
        /// from off-line location information.
        /// </summary>
        public const string EVIDENCE_GEO_LAT_KEY = "location" +
            Pipeline.Core.Constants.EVIDENCE_SEPERATOR + "latitude";
        /// <summary>
        /// Complete evidence key for longitude that is supplied
        /// from off-line location information.
        /// </summary>
        public const string EVIDENCE_GEO_LON_KEY = "location" + 
            Pipeline.Core.Constants.EVIDENCE_SEPERATOR + "longitude";

        /// <summary>
        /// The name for the cookie / form parameter used to pass latitude
        /// from the client.
        /// </summary>
        public const string EVIDENCE_GEO_LAT = 
            Pipeline.Engines.Constants.FIFTYONE_COOKIE_PREFIX + "pos_latitude";
        /// <summary>
        /// The name for the cookie / form parameter used to pass longitude
        /// from the client.
        /// </summary>
        public const string EVIDENCE_GEO_LON = 
            Pipeline.Engines.Constants.FIFTYONE_COOKIE_PREFIX + "pos_longitude";

        /// <summary>
        /// The complete evidence key for latitude obtained from a
        /// cookie passed in the request.
        /// </summary>
        public const string EVIDENCE_GEO_LAT_COOKIE_KEY = 
            Pipeline.Core.Constants.EVIDENCE_COOKIE_PREFIX + 
            Pipeline.Core.Constants.EVIDENCE_SEPERATOR + EVIDENCE_GEO_LAT;
        /// <summary>
        /// The complete evidence key for longitude obtained from a
        /// cookie passed in the request.
        /// </summary>
        public const string EVIDENCE_GEO_LON_COOKIE_KEY = 
            Pipeline.Core.Constants.EVIDENCE_COOKIE_PREFIX + 
            Pipeline.Core.Constants.EVIDENCE_SEPERATOR + EVIDENCE_GEO_LON;
        /// <summary>
        /// The complete evidence key for latitude passed in the query
        /// string, as a form parameter or from some other mechanism.
        /// </summary>
        public const string EVIDENCE_GEO_LAT_PARAM_KEY = 
            Pipeline.Core.Constants.EVIDENCE_QUERY_PREFIX + 
            Pipeline.Core.Constants.EVIDENCE_SEPERATOR + EVIDENCE_GEO_LAT;
        /// <summary>
        /// The complete evidence key for longitude passed in the query
        /// string, as a form parameter or from some other mechanism.
        /// </summary>
        public const string EVIDENCE_GEO_LON_PARAM_KEY = 
            Pipeline.Core.Constants.EVIDENCE_QUERY_PREFIX + 
            Pipeline.Core.Constants.EVIDENCE_SEPERATOR + EVIDENCE_GEO_LON;

        /// <summary>
        /// The default evidence filter for geolocation engines.
        /// </summary>
        public static readonly IEvidenceKeyFilter DefaultGeoEvidenceKeyFilter =
            new EvidenceKeyFilterWhitelist(new List<string>()
            {
                Pipeline.Core.Constants.EVIDENCE_CLIENTIP_KEY,
                Constants.EVIDENCE_GEO_LAT_KEY,
                Constants.EVIDENCE_GEO_LON_KEY,
                Constants.EVIDENCE_GEO_LAT_COOKIE_KEY,
                Constants.EVIDENCE_GEO_LON_COOKIE_KEY,
                Constants.EVIDENCE_GEO_LAT_PARAM_KEY,
                Constants.EVIDENCE_GEO_LON_PARAM_KEY
            });

        /// <summary>
        /// The message to be sent to the user when the JavaScript that
        /// gathers lat/lon data has not yet been run.
        /// </summary>
        public const string NO_EVIDENCE_MESSAGE = "This property requires evidence" +
                " values from JavaScript running on the client. It cannot be" +
                " populated until a future request is made that contains this" +
                " additional data.";

        /// <summary>
        /// The message to be sent to the user when the JavaScript that
        /// gathers lat/lon data is not required. This is the case if location
        /// evidence has been provided.
        /// </summary>
        public const string NO_JAVASCRIPT_MESSAGE = "Location evidence has been " +
            "provided so the client-side JavaScript is not required.";
    }
}
