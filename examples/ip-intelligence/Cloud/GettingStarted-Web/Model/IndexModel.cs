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

using FiftyOne.IpIntelligence.Translation.Data;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.Data;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.IpIntelligence.Examples.Cloud.GettingStartedWeb.Model
{
    public class IndexModel
    {
        public string Name { get; private set; }
        public string RegisteredOwner { get; private set; }
        public string RegisteredCountry { get; private set; }
        public string IpRangeStart { get; private set; }
        public string IpRangeEnd { get; private set; }
        public string Country { get; private set; }
        public string CountryCode { get; private set; }
        public string CountryCode3 { get; private set; }
        public string Region { get; private set; }
        public string State { get; private set; }
        public string Town { get; private set; }
        public string Latitude { get; private set; }
        public string Longitude { get; private set; }
        public string Areas { get; private set; }
        public string AccuracyRadius { get; private set; }
        public string TimeZoneOffset { get; private set; }
        public string InputIpAddress { get; set; }

        public IFlowData FlowData { get; private set; }

        public CloudRequestEngine Engine { get; private set; }

        public List<EvidenceModel> Evidence { get; private set; }

        public IHeaderDictionary ResponseHeaders { get; private set; }
        
        public IReadOnlyList<string> CountryCodesGeographical { get; set; }
        public IReadOnlyList<string> CountryCodesPopulation { get; set; }

        // The full ordered country lists produced by the cloud countries
        // translation engine. CountryCodesGeographicalAll holds the ISO codes
        // (used as the <option> values) and CountryNamesGeographicalAllTranslated
        // holds the matching display names - the two lists are index-aligned.
        public IReadOnlyList<string> CountryCodesGeographicalAll { get; set; }
        public IReadOnlyList<string> CountryNamesGeographicalAllTranslated { get; set; }

        public IndexModel(IFlowData flowData, IHeaderDictionary responseHeaders)
        {
            FlowData = flowData;
            ResponseHeaders = responseHeaders;

            // Get the cloud engine
            Engine = FlowData.Pipeline.GetElement<CloudRequestEngine>();
            // Get the evidence that was used when performing device detection.
            Evidence = FlowData.GetEvidence().AsDictionary()
                .Select(e => new EvidenceModel()
                {
                    Key = e.Key,
                    Value = e.Value.ToString(),
                    Used = Engine.EvidenceKeyFilter.Include(e.Key)
                })
                .ToList();

            // Get the results of IP Intelligence.
            var ipiData = FlowData.Get<IIpIntelligenceData>();
            // Use helper functions to get a human-readable string representation of various
            // property values. These helpers handle situations such as the property missing
            // due to using a lite data file or the property not having a value because
            // IP intelligence didn't find a match.
            Name = ipiData.TryGetValue(d => d.RegisteredName.GetHumanReadable());
            RegisteredOwner = ipiData.TryGetValue(d => d.RegisteredOwner.GetHumanReadable());
            RegisteredCountry = ipiData.TryGetValue(d => d.RegisteredCountry.GetHumanReadable());
            IpRangeStart = ipiData.TryGetValue(d => d.IpRangeStart.GetHumanReadable());
            IpRangeEnd = ipiData.TryGetValue(d => d.IpRangeEnd.GetHumanReadable());
            Country = ipiData.TryGetValue(d => d.Country.GetHumanReadable());
            CountryCode = ipiData.TryGetValue(d => d.CountryCode.GetHumanReadable());
            CountryCode3 = ipiData.TryGetValue(d => d.CountryCode3.GetHumanReadable());
            Region = ipiData.TryGetValue(d => d.Region.GetHumanReadable());
            State = ipiData.TryGetValue(d => d.State.GetHumanReadable());
            Town = ipiData.TryGetValue(d => d.Town.GetHumanReadable());
            Latitude = ipiData.TryGetValue(d => d.Latitude.GetHumanReadable());
            Longitude = ipiData.TryGetValue(d => d.Longitude.GetHumanReadable());
            Areas = ipiData.TryGetValue(d => d.Areas.GetHumanReadable());
            AccuracyRadius = ipiData.TryGetValue(d => d.AccuracyRadiusMin.GetHumanReadable());
            TimeZoneOffset = ipiData.TryGetValue(d => d.TimeZoneOffset.GetHumanReadable());
            CountryCodesGeographical = ipiData.TryGetValue(d => d.CountryCodesGeographical.GetHumanReadableList());
            CountryCodesPopulation = ipiData.TryGetValue(d => d.CountryCodesPopulation.GetHumanReadableList());

            // Get the translated country lists from the cloud countries translation
            // engine. These power the country dropdown in the view.
            var countriesData = FlowData.Get<ICountriesTranslationData>();
            CountryCodesGeographicalAll = countriesData.TryGetValue(
                d => d.CountryCodesGeographicalAll.GetHumanReadableList());
            CountryNamesGeographicalAllTranslated = countriesData.TryGetValue(
                d => d.CountryNamesGeographicalAllTranslated.GetHumanReadableList());
        }
    }
}
