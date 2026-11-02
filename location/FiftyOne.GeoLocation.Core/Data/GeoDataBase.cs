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
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FiftyOne.GeoLocation.Core.Data
{
    /// <summary>
    /// Data class that contains postal address information
    /// </summary>
    public class GeoData : AspectDataBase, IGeoData
    {
        #region Private Properties
        private ILogger<GeoData> _logger;

        #endregion

        #region Constructor
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">
        /// The logger for this instance to use.
        /// </param>
        /// <param name="pipeline">
        /// The Pipeline that created this instance
        /// </param>
        /// <param name="engine">
        /// The engine that created this instance
        /// </param>
        /// <param name="missingPropertyService">
        /// The <see cref="IMissingPropertyService"/> to use when the
        /// requested property does not exist in the base data store.
        /// </param>
        public GeoData(ILogger<GeoData> logger,
            IPipeline pipeline,
            IAspectEngine engine,
            IMissingPropertyService missingPropertyService)
            : base(logger, pipeline, engine, missingPropertyService)
        {
            _logger = logger;

            JavaScript = new AspectPropertyValue<JavaScript>(new JavaScript(""));
        }

        #endregion

        #region Public Fields
        /// <inheritdoc/>
        public AspectPropertyValue<JavaScript> JavaScript
        {
            get { return GetAs<AspectPropertyValue<JavaScript>>("javascript"); }
            set { this["javascript"] = value; }
        }
        /// <inheritdoc/>
        public AspectPropertyValue<string> Building
        {
            get { return GetAs<AspectPropertyValue<string>>("building"); }
            set { base["building"] = value; }
        }
        /// <inheritdoc/>
        public AspectPropertyValue<string> StreetNumber
        {
            get { return GetAs<AspectPropertyValue<string>>("streetnumber"); }
            set { base["streetnumber"] = value; }
        }
        /// <inheritdoc/>
        public AspectPropertyValue<string> Road
        {
            get { return GetAs<AspectPropertyValue<string>>("road"); }
            set { base["road"] = value; }
        }
        /// <inheritdoc/>
        public AspectPropertyValue<string> Town
        {
            get { return GetAs<AspectPropertyValue<string>>("town"); }
            set { base["town"] = value; }
        }
        /// <inheritdoc/>
        public AspectPropertyValue<string> Suburb
        {
            get { return GetAs<AspectPropertyValue<string>>("suburb"); }
            set { base["suburb"] = value; }
        }
        /// <inheritdoc/>
        public AspectPropertyValue<string> County
        {
            get { return GetAs<AspectPropertyValue<string>>("county"); }
            set { base["county"] = value; }
        }
        /// <inheritdoc/>
        public AspectPropertyValue<string> Region
        {
            get { return GetAs<AspectPropertyValue<string>>("region"); }
            set { base["region"] = value; }
        }
        /// <inheritdoc/>
        public AspectPropertyValue<string> State
        {
            get { return GetAs<AspectPropertyValue<string>>("state"); }
            set { base["state"] = value; }
        }
        /// <inheritdoc/>
        public AspectPropertyValue<string> ZipCode
        {
            get { return GetAs<AspectPropertyValue<string>>("zipcode"); }
            set { base["zipcode"] = value; }
        }
        /// <inheritdoc/>
        public AspectPropertyValue<string> Country
        {
            get { return GetAs<AspectPropertyValue<string>>("country"); }
            set { base["country"] = value; }
        }
        /// <inheritdoc/>
        public AspectPropertyValue<string> CountryCode
        {
            get { return GetAs<AspectPropertyValue<string>>("countrycode"); }
            set { base["countrycode"] = value; }
        }
        /// <inheritdoc/>
        public AspectPropertyValue<string> CountryCode3
        {
            get { return GetAs<AspectPropertyValue<string>>("countrycode3"); }
            set { base["countrycode3"] = value; }
        }
        /// <inheritdoc/>
        public AspectPropertyValue<int> TimeZoneOffset
        {
            get { return GetAs<AspectPropertyValue<int>>("timezoneoffset"); }
            set { base["timezoneoffset"] = value; }
        }
        /// <inheritdoc/>
        public AspectPropertyValue<string> Address
        {
            get { return GetAs<AspectPropertyValue<string>>("address"); }
            set { base["address"] = value; }
        }

        /// <summary>
        /// Dictionary of property types.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, Type> PropertyTypes =
            new Dictionary<string, Type>()
            {
                { "JavaScript", typeof(IAspectPropertyValue<JavaScript>) },
                { "Building", typeof(IAspectPropertyValue<string>) },
                { "StreetNumber", typeof(IAspectPropertyValue<string>) },
                { "Road", typeof(IAspectPropertyValue<string>) },
                { "Town", typeof(IAspectPropertyValue<string>) },
                { "Suburb", typeof(IAspectPropertyValue<string>) },
                { "County", typeof(IAspectPropertyValue<string>) },
                { "Region", typeof(IAspectPropertyValue<string>) },
                { "State", typeof(IAspectPropertyValue<string>) },
                { "ZipCode", typeof(IAspectPropertyValue<string>) },
                { "Country", typeof(IAspectPropertyValue<string>) },
                { "CountryCode", typeof(IAspectPropertyValue<string>) },
                { "CountryCode3", typeof(IAspectPropertyValue<string>) },
                { "TimeZoneOffset", typeof(IAspectPropertyValue<int>) },
                { "Address", typeof(IAspectPropertyValue<string>) },
            };

        #endregion
    }
}
