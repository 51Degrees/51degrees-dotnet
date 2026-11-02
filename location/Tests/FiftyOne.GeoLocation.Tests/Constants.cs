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

namespace FiftyOne.GeoLocation.Tests
{
    /// <summary>
    /// GeoLocation test constants
    /// </summary>
    internal class Constants
    {
        /// <summary>
        /// Sample response returned by a geo location api server. 
        /// In this case, nominatim.
        /// </summary>
        internal const string JSON_RESPONSE = @"
{
    'place_id': 85642204,
    'licence': 'Data © OpenStreetMap contributors, ODbL 1.0. https://osm.org/copyright',
    'osm_type': 'way',
    'osm_id': 96177745,
    'lat': '51.4578261',
    'lon': '-0.975922996290084',
    'display_name': 'Spaces, 9, Greyfriars Road, Coley, Reading, South East, England, RG1 1NU, United Kingdom',
    'address': {
        'place': 'Spaces',
        'house_number': '9',
        'road': 'Greyfriars Road',
        'suburb': 'Coley',
        'town': 'Reading',
        'county': 'Reading',
        'state_district': 'South East',
        'state': 'England',
        'postcode': 'RG1 1NU',
        'country': 'United Kingdom',
        'country_code': 'gb'
    },
    'boundingbox': ['51.4575761', '51.4580634', '-0.9764213', '-0.9757286']
}";

        internal const string TEST_LAT = "51.4578261";
        internal const string TEST_LON = "-0.975922996290084";

        internal const string RESOURCE_KEY_ENV_VAR = "SUPER_RESOURCE_KEY";
    }
}
