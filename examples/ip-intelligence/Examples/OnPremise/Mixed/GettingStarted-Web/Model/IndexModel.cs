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

using FiftyOne.DeviceDetection;
using FiftyOne.IpIntelligence.Translation.Data;
using System.Collections.Generic;

namespace FiftyOne.IpIntelligence.Examples.Mixed.OnPremise.GettingStartedWeb.Model
{
    public class IndexModel
    {
        // Device Detection properties
        public IDeviceData Device { get; set; }
        
        // IP Intelligence properties
        public IIpIntelligenceData IpData { get; set; }
        
        // IP Countries Translation properties
        public ICountriesTranslationData TranslationData { get; set; }

        // IP Countries Codes Translation properties
        public ICountryCodeTranslationData CodeTranslationData { get; set; }
        
        // Evidence used for detection
        public List<EvidenceModel> Evidence { get; set; }
        
        // User input for IP address lookup
        public string UserInputIp { get; set; }
        
        // Messages for display
        public string DeviceMessage { get; set; }
        public string IpMessage { get; set; }

        // True when the IP Intelligence engine is running against the free 'Lite'
        // data file. Used to surface the contact-us message in the view.
        public bool IsLiteDataFile { get; set; }
    }
}