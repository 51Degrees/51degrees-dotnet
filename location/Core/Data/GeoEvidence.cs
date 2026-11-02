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

namespace FiftyOne.GeoLocation.Core.Data
{
    /// <summary>
    /// Data class containing evidence that can be used when determining 
    /// location data.
    /// </summary>
    public class GeoEvidence
    {
        /// <summary>
        /// The latitude coordinate in floating point format and 
        /// represented as a string.
        /// </summary>
        public string Latitude { get; set; }
        /// <summary>
        /// The longitude coordinate in floating point format and 
        /// represented as a string.
        /// </summary>
        public string Longitude { get; set; }
        /// <summary>
        /// The IP address of the client.
        /// </summary>
        public string IpAddress { get; set; }
        /// <summary>
        /// The level of detail required by the caller.
        /// For example, if only 'country' is needed, the zoom level can
        /// be set to 3, decreasing the amount of time that the query takes.
        /// For more detail on zoom levels, see the 'setZoom' method in
        /// the <a href="https://github.com/osm-search/Nominatim/blob/master/lib-php/ReverseGeocode.php">Nominatim API</a> 
        /// </summary>
        public int Zoom { get; set; } = 18;
    }
}
