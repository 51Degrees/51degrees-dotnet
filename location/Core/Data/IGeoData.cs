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
using FiftyOne.Pipeline.Core.Data.Types;
using FiftyOne.Pipeline.Engines.Data;
using System.Threading.Tasks;

namespace FiftyOne.GeoLocation.Core.Data
{
    /// <summary>
    /// Represents an object that contains postal address information
    /// </summary>
    public interface IGeoData : IAspectData
    {
        /// <summary>
        /// A JavaScript snippet that, when run on the client, will 
        /// retrieve latitude and longitude data to be used by a 
        /// reverse geocoding engine in order to determine postal
        /// address information.
        /// </summary>
        AspectPropertyValue<JavaScript> JavaScript { get; set; }

        /// <summary>
        /// The name of the building that is closest to this location.
        /// Part of the postal address.
        /// </summary>
        /// <seealso cref="StreetNumber"/>
        /// <seealso cref="Road"/>
        /// <seealso cref="Town"/>
        /// <seealso cref="Suburb"/>
        /// <seealso cref="County"/>
        /// <seealso cref="Region"/>
        /// <seealso cref="State"/>
        /// <seealso cref="Country"/>
        /// <seealso cref="ZipCode"/>
        AspectPropertyValue<string> Building { get; set; }

        /// <summary>
        /// The number of the building that is closest to this location.
        /// Part of the postal address.
        /// </summary>
        /// <seealso cref="Building"/>
        /// <seealso cref="Road"/>
        /// <seealso cref="Town"/>
        /// <seealso cref="Suburb"/>
        /// <seealso cref="County"/>
        /// <seealso cref="Region"/>
        /// <seealso cref="State"/>
        /// <seealso cref="Country"/>
        /// <seealso cref="ZipCode"/>
        AspectPropertyValue<string> StreetNumber { get; set; }

        /// <summary>
        /// The name of the road that this location is on.
        /// Part of the postal address.
        /// </summary>
        /// <seealso cref="Building"/>
        /// <seealso cref="StreetNumber"/>
        /// <seealso cref="Town"/>
        /// <seealso cref="Suburb"/>
        /// <seealso cref="County"/>
        /// <seealso cref="Region"/>
        /// <seealso cref="State"/>
        /// <seealso cref="Country"/>
        /// <seealso cref="ZipCode"/>
        AspectPropertyValue<string> Road { get; set; }

        /// <summary>
        /// The name of the town that this location is in.
        /// Part of the postal address.
        /// </summary>
        /// <seealso cref="Building"/>
        /// <seealso cref="StreetNumber"/>
        /// <seealso cref="Road"/>
        /// <seealso cref="Suburb"/>
        /// <seealso cref="County"/>
        /// <seealso cref="Region"/>
        /// <seealso cref="State"/>
        /// <seealso cref="Country"/>
        /// <seealso cref="ZipCode"/>
        AspectPropertyValue<string> Town { get; set; }

        /// <summary>
        /// The name of the suburb that this location is in.
        /// Part of the postal address.
        /// </summary>
        /// <seealso cref="Building"/>
        /// <seealso cref="StreetNumber"/>
        /// <seealso cref="Road"/>
        /// <seealso cref="Town"/>
        /// <seealso cref="County"/>
        /// <seealso cref="Region"/>
        /// <seealso cref="State"/>
        /// <seealso cref="Country"/>
        /// <seealso cref="ZipCode"/>
        AspectPropertyValue<string> Suburb { get; set; }

        /// <summary>
        /// The name of the county that this location is in.
        /// Part of the postal address.
        /// </summary>
        /// <seealso cref="Building"/>
        /// <seealso cref="StreetNumber"/>
        /// <seealso cref="Road"/>
        /// <seealso cref="Town"/>
        /// <seealso cref="Suburb"/>
        /// <seealso cref="Region"/>
        /// <seealso cref="State"/>
        /// <seealso cref="Country"/>
        /// <seealso cref="ZipCode"/>
        AspectPropertyValue<string> County { get; set; }

        /// <summary>
        /// The name of the region that this location is in.
        /// Part of the postal address.
        /// </summary>
        /// <seealso cref="Building"/>
        /// <seealso cref="StreetNumber"/>
        /// <seealso cref="Road"/>
        /// <seealso cref="Town"/>
        /// <seealso cref="Suburb"/>
        /// <seealso cref="County"/>
        /// <seealso cref="State"/>
        /// <seealso cref="Country"/>
        /// <seealso cref="ZipCode"/>
        AspectPropertyValue<string> Region { get; set; }

        /// <summary>
        /// The name of the state that this location is in.
        /// Part of the postal address.
        /// </summary>
        /// <seealso cref="Building"/>
        /// <seealso cref="StreetNumber"/>
        /// <seealso cref="Road"/>
        /// <seealso cref="Town"/>
        /// <seealso cref="Suburb"/>
        /// <seealso cref="County"/>
        /// <seealso cref="Region"/>
        /// <seealso cref="Country"/>
        /// <seealso cref="ZipCode"/>
        AspectPropertyValue<string> State { get; set; }

        /// <summary>
        /// The zip code closest to this location.
        /// Part of the postal address.
        /// </summary>
        /// <seealso cref="Building"/>
        /// <seealso cref="StreetNumber"/>
        /// <seealso cref="Road"/>
        /// <seealso cref="Town"/>
        /// <seealso cref="Suburb"/>
        /// <seealso cref="County"/>
        /// <seealso cref="State"/>
        /// <seealso cref="Region"/>
        /// <seealso cref="Country"/>
        AspectPropertyValue<string> ZipCode { get; set; }

        /// <summary>
        /// The name of the country that this location is in.
        /// Part of the postal address.
        /// </summary>
        /// <seealso cref="Building"/>
        /// <seealso cref="StreetNumber"/>
        /// <seealso cref="Road"/>
        /// <seealso cref="Town"/>
        /// <seealso cref="Suburb"/>
        /// <seealso cref="County"/>
        /// <seealso cref="State"/>
        /// <seealso cref="Region"/>
        /// <seealso cref="ZipCode"/>
        AspectPropertyValue<string> Country { get; set; }

        /// <summary>
        /// The country code for the country that this location is in.
        /// </summary>
        /// <remarks>
        /// Different location providers may provide differently formatted
        /// country codes.
        /// </remarks>
        AspectPropertyValue<string> CountryCode { get; set; }

        /// <summary>
        /// The 3 character country code for the country that this location is in.
        /// </summary>
        AspectPropertyValue<string> CountryCode3 { get; set; }

        /// <summary>
        /// The offset from UTC in minutes for the location at the date processed.
        /// </summary>
        AspectPropertyValue<int> TimeZoneOffset { get; set; }

        /// <summary>
        /// The complete address for this location.
        /// </summary>
        AspectPropertyValue<string> Address { get; set; }
    }
}
