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
using FiftyOne.DeviceDetection.Hash.Engine.OnPremise.FlowElements;
using FiftyOne.IpIntelligence.Engine.OnPremise.FlowElements;
using FiftyOne.IpIntelligence.Translation.Data;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Engines.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FiftyOne.IpIntelligence.Examples.Mixed.OnPremise.GettingStartedWeb.Model
{
    /// <summary>
    /// The values the page shows. A property that a data file in use does
    /// not have is shown as needing the Enterprise data file, and its value
    /// is never read, because reading it throws.
    /// </summary>
    public class IndexModel
    {
        /// <summary>
        /// Shown for a property that the data file has but that has no
        /// value for this request.
        /// </summary>
        private const string Unknown = "Unknown";

        /// <summary>
        /// The number format for the latitude and longitude.
        /// </summary>
        private const string CoordinateFormat = "F4";

        private readonly DeviceDetectionHashEngine _deviceEngine;
        private readonly IpiOnPremiseEngine _ipEngine;

        // Device Detection values
        public string HardwareVendor { get; private set; }
        public string HardwareName { get; private set; }
        public string DeviceType { get; private set; }
        public string PlatformVendor { get; private set; }
        public string PlatformName { get; private set; }
        public string PlatformVersion { get; private set; }
        public string BrowserVendor { get; private set; }
        public string BrowserName { get; private set; }
        public string BrowserVersion { get; private set; }
        public string ScreenWidth { get; private set; }
        public string ScreenHeight { get; private set; }
        public string DeviceId { get; private set; }

        // IP Intelligence values
        public string RegisteredName { get; private set; }
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
        public IReadOnlyList<string> CountryCodesGeographical { get; private set; }
        public IReadOnlyList<string> CountryCodesPopulation { get; private set; }

        // Values from the translation engines
        public IReadOnlyList<string> CountryNamesGeographicalAllTranslated { get; private set; }
        public IReadOnlyList<string> CountryNamesPopulationAllTranslated { get; private set; }
        public IReadOnlyList<string> CountryCodesGeographicalAll { get; private set; }
        public IReadOnlyList<string> CountryCodesPopulationAll { get; private set; }

        /// <summary>
        /// The area, latitude and longitude for the map, each empty when the
        /// data file does not have it or it has no value.
        /// </summary>
        public string MapAreas { get; private set; }
        public string MapLatitude { get; private set; }
        public string MapLongitude { get; private set; }

        /// <summary>
        /// True when the page shows a property that a data file in use does
        /// not have, so the page says where to ask for the Enterprise data
        /// files.
        /// </summary>
        public bool NeedsEnterprise { get; private set; }

        // Evidence used for detection
        public List<EvidenceModel> Evidence { get; private set; }

        // User input for IP address lookup
        public string UserInputIp { get; set; }

        // Messages for display
        public string DeviceMessage { get; private set; }
        public string IpMessage { get; set; }

        public IndexModel(IFlowData flowData)
        {
            // The engines say which properties their data files have.
            _deviceEngine = flowData.Pipeline.GetElement<DeviceDetectionHashEngine>();
            _ipEngine = flowData.Pipeline.GetElement<IpiOnPremiseEngine>();

            // Only the Enterprise data file has the hardware and device type
            // properties. The free Lite data file has the others.
            var device = flowData.Get<IDeviceData>();
            HardwareVendor = DeviceValue(nameof(device.HardwareVendor),
                () => device.HardwareVendor);
            HardwareName = DeviceValue(nameof(device.HardwareName),
                () => device.HardwareName);
            DeviceType = DeviceValue(nameof(device.DeviceType),
                () => device.DeviceType);
            PlatformVendor = DeviceValue(nameof(device.PlatformVendor),
                () => device.PlatformVendor);
            PlatformName = DeviceValue(nameof(device.PlatformName),
                () => device.PlatformName);
            PlatformVersion = DeviceValue(nameof(device.PlatformVersion),
                () => device.PlatformVersion);
            BrowserVendor = DeviceValue(nameof(device.BrowserVendor),
                () => device.BrowserVendor);
            BrowserName = DeviceValue(nameof(device.BrowserName),
                () => device.BrowserName);
            BrowserVersion = DeviceValue(nameof(device.BrowserVersion),
                () => device.BrowserVersion);
            ScreenWidth = DeviceValue(nameof(device.ScreenPixelsWidth),
                () => device.ScreenPixelsWidth);
            ScreenHeight = DeviceValue(nameof(device.ScreenPixelsHeight),
                () => device.ScreenPixelsHeight);
            DeviceId = DeviceValue(nameof(device.DeviceId),
                () => device.DeviceId);
            DeviceMessage = $"You are using {BrowserName} on {PlatformName}";

            // Only the Enterprise data file has the range and location
            // properties. The free Lite data file has the registration ones.
            var ipData = flowData.Get<IIpIntelligenceData>();
            RegisteredName = IpValue(nameof(ipData.RegisteredName),
                () => ipData.RegisteredName);
            RegisteredOwner = IpValue(nameof(ipData.RegisteredOwner),
                () => ipData.RegisteredOwner);
            RegisteredCountry = IpValue(nameof(ipData.RegisteredCountry),
                () => ipData.RegisteredCountry);
            IpRangeStart = IpValue(nameof(ipData.IpRangeStart),
                () => ipData.IpRangeStart);
            IpRangeEnd = IpValue(nameof(ipData.IpRangeEnd),
                () => ipData.IpRangeEnd);
            Country = IpValue(nameof(ipData.Country),
                () => ipData.Country);
            CountryCode = IpValue(nameof(ipData.CountryCode),
                () => ipData.CountryCode);
            CountryCode3 = IpValue(nameof(ipData.CountryCode3),
                () => ipData.CountryCode3);
            Region = IpValue(nameof(ipData.Region),
                () => ipData.Region);
            State = IpValue(nameof(ipData.State),
                () => ipData.State);
            Town = IpValue(nameof(ipData.Town),
                () => ipData.Town);
            Latitude = IpValue(nameof(ipData.Latitude),
                () => ipData.Latitude, CoordinateFormat);
            Longitude = IpValue(nameof(ipData.Longitude),
                () => ipData.Longitude, CoordinateFormat);
            Areas = IpValue(nameof(ipData.Areas),
                () => ipData.Areas);
            AccuracyRadius = IpValue(nameof(ipData.AccuracyRadiusMin),
                () => ipData.AccuracyRadiusMin);
            TimeZoneOffset = IpValue(nameof(ipData.TimeZoneOffset),
                () => ipData.TimeZoneOffset);
            CountryCodesGeographical = IpValues(
                nameof(ipData.CountryCodesGeographical),
                () => ipData.CountryCodesGeographical.GetHumanReadableList());
            CountryCodesPopulation = IpValues(
                nameof(ipData.CountryCodesPopulation),
                () => ipData.CountryCodesPopulation.GetHumanReadableList());

            // The translation engines translate the country codes, so their
            // values also need the Enterprise data file.
            var translation = flowData.Get<ICountriesTranslationData>();
            CountryNamesGeographicalAllTranslated = IpValues(
                nameof(ipData.CountryCodesGeographical),
                () => translation.TryGetValue(d =>
                    d.CountryNamesGeographicalAllTranslated.GetHumanReadableList()));
            CountryCodesGeographicalAll = IpValues(
                nameof(ipData.CountryCodesGeographical),
                () => translation.TryGetValue(d =>
                    d.CountryCodesGeographicalAll.GetHumanReadableList()));
            CountryNamesPopulationAllTranslated = IpValues(
                nameof(ipData.CountryCodesPopulation),
                () => translation.TryGetValue(d =>
                    d.CountryNamesPopulationAllTranslated.GetHumanReadableList()));
            CountryCodesPopulationAll = IpValues(
                nameof(ipData.CountryCodesPopulation),
                () => translation.TryGetValue(d =>
                    d.CountryCodesPopulationAll.GetHumanReadableList()));

            MapAreas = MapValue(nameof(ipData.Areas),
                () => ipData.Areas, null);
            MapLatitude = MapValue(nameof(ipData.Latitude),
                () => ipData.Latitude, CoordinateFormat);
            MapLongitude = MapValue(nameof(ipData.Longitude),
                () => ipData.Longitude, CoordinateFormat);

            Evidence = flowData.GetEvidence().AsDictionary()
                .Select(e => new EvidenceModel
                {
                    Key = e.Key,
                    Value = e.Value?.ToString() ?? "null"
                })
                .ToList();
        }

        /// <summary>
        /// The value of a Device Detection property, or that it needs the
        /// Enterprise data file when the data file in use does not have it.
        /// </summary>
        private string DeviceValue(
            string property,
            Func<IAspectPropertyValue> value)
        {
            if (HasProperty(_deviceEngine.Properties, property) == false)
            {
                NeedsEnterprise = true;
                return Examples.OnPremise.ExampleUtils.NEEDS_ENTERPRISE;
            }
            return Text(value(), null) ?? Unknown;
        }

        /// <summary>
        /// The value of an IP Intelligence property, or that it needs the
        /// Enterprise data file when the data file in use does not have it.
        /// </summary>
        private string IpValue(
            string property,
            Func<IAspectPropertyValue> value,
            string format = null)
        {
            if (HasProperty(_ipEngine.Properties, property) == false)
            {
                NeedsEnterprise = true;
                return Examples.OnPremise.ExampleUtils.NEEDS_ENTERPRISE;
            }
            return Text(value(), format) ?? Unknown;
        }

        /// <summary>
        /// The values of an IP Intelligence property, or that it needs the
        /// Enterprise data file when the data file in use does not have it.
        /// </summary>
        private IReadOnlyList<string> IpValues(
            string property,
            Func<IReadOnlyList<string>> values)
        {
            if (HasProperty(_ipEngine.Properties, property) == false)
            {
                NeedsEnterprise = true;
                return new[] { Examples.OnPremise.ExampleUtils.NEEDS_ENTERPRISE };
            }
            return values();
        }

        /// <summary>
        /// The value of an IP Intelligence property for the map, or an empty
        /// string when the data file does not have it or it has no value.
        /// </summary>
        private string MapValue(
            string property,
            Func<IAspectPropertyValue> value,
            string format)
        {
            return HasProperty(_ipEngine.Properties, property) ?
                Text(value(), format) ?? "" :
                "";
        }

        /// <summary>
        /// True when the data file the properties came from has the one
        /// named.
        /// </summary>
        private static bool HasProperty(
            IEnumerable<IElementPropertyMetaData> properties,
            string name)
        {
            return properties.Any(p => string.Equals(
                p.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// The value as text, or null when there is no value. A list is
        /// joined with commas, and a number is written with the format when
        /// one is given.
        /// </summary>
        private static string Text(IAspectPropertyValue value, string format)
        {
            if (value == null || value.HasValue == false)
            {
                return null;
            }
            if (value.Value is string text)
            {
                return text;
            }
            if (value.Value is IEnumerable<string> list)
            {
                return string.Join(", ", list);
            }
            if (format != null && value.Value is IFormattable number)
            {
                return number.ToString(format, CultureInfo.InvariantCulture);
            }
            return Convert.ToString(value.Value, CultureInfo.InvariantCulture);
        }
    }
}
