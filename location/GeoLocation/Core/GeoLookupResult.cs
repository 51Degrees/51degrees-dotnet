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
using System;
using System.Collections.Generic;
using System.Text;

namespace FiftyOne.GeoLocation.Core
{

    /// <summary>
    /// Data class that contains responses from geolocation 
    /// lookup services.
    /// </summary>
    public class GeoLookupResult
    {
        /// <summary>
        /// The raw string response from the geolocation web service.
        /// If the <see cref="IFlowData"/> does not contain the required
        /// evidence then the ClientsideEvidenceJS property will be 
        /// populated instead.
        /// </summary>
        public string GeoServiceResponse { get; set; }
        /// <summary>
        /// A <see cref="JavaScript"/> instance containing the JavaScript
        /// to run on the client device in order to supply the evidence
        /// required by the geolocation service.
        /// This is only populated if the required evidence is not 
        /// available in the <see cref="IFlowData"/>.
        /// </summary>
        public JavaScript ClientsideEvidenceJS { get; set; }
    }
}
