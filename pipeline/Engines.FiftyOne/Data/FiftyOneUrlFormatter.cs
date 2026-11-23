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

using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Data
{
    /// <summary>
    /// 51Degrees specific URL formatter. This formats a data file download URL
    /// using the parameters required by the 51Degrees distributor.
    /// </summary>
    public class FiftyOneUrlFormatter : IDataUpdateProductFormatter
    {
        /// <summary>
        /// Constructor for a formatter that names no product. The product
        /// is taken from the data file the engine has loaded when the
        /// distributor needs one.
        /// </summary>
        public FiftyOneUrlFormatter()
        {
        }

        /// <summary>
        /// Constructor for a formatter that names the product to request.
        /// Use it for an update on start-up, which runs before a data file
        /// is loaded, when the license keys hold more than one product.
        /// </summary>
        /// <param name="product">
        /// The name the distributor gives the product. For example,
        /// 'V4TAC' or 'IPIV4Enterprise'.
        /// </param>
        public FiftyOneUrlFormatter(string product)
        {
            Product = product;
        }

        /// <summary>
        /// The name of the product that every request asks for, or null if
        /// none was given.
        /// </summary>
        public string Product { get; }

        /// <summary>
        /// Get the URL to call to request an updated version of the 
        /// supplied data file.
        /// </summary>
        /// <param name="dataFile">
        /// The data file to build an update URL for.
        /// </param>
        /// <returns>
        /// The URL to call in order to check for and download an update.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied data file is null
        /// </exception>
        [Obsolete("Use the GetFormattedDataUpdateUri method instead." +
            "This method may be removed in future versions.")]
#pragma warning disable CA1055 // Uri return values should not be strings
        public string GetFormattedDataUpdateUrl(IAspectEngineDataFile dataFile)
#pragma warning restore CA1055 // Uri return values should not be strings
        {
            return GetFormattedDataUpdateUri(dataFile).AbsoluteUri;
        }

        /// <summary>
        /// Get the URL to call to request an updated version of the 
        /// supplied data file.
        /// </summary>
        /// <param name="dataFile">
        /// The data file to build an update URL for.
        /// </param>
        /// <returns>
        /// The URL to call in order to check for and download an update.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied data file is null
        /// </exception>
        public Uri GetFormattedDataUpdateUri(IAspectEngineDataFile dataFile)
        {
            return GetFormattedDataUpdateUri(dataFile, Product);
        }

        /// <summary>
        /// Get the URL to call to request an updated version of the
        /// supplied data file for the product named. The URL is the one
        /// <see cref="GetFormattedDataUpdateUri(IAspectEngineDataFile)"/>
        /// gives, with the Product parameter added.
        /// </summary>
        /// <param name="dataFile">
        /// The data file to build an update URL for.
        /// </param>
        /// <param name="product">
        /// The product name to request, or null to name no product.
        /// </param>
        /// <returns>
        /// The URL to call in order to check for and download an update.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied data file is null
        /// </exception>
        public Uri GetFormattedDataUpdateUri(
            IAspectEngineDataFile dataFile, string product)
        {
            if (dataFile == null)
            {
                throw new ArgumentNullException(nameof(dataFile));
            }

            var fiftyOneDataFile = dataFile as IFiftyOneDataFile;
            if (fiftyOneDataFile != null)
            {
                var parameters = new List<string>()
                {
                "LicenseKeys=" + String.Join("|", dataFile.Configuration.DataUpdateLicenseKeys),
                "Download=True",
                "Type=" + fiftyOneDataFile.DataDownloadType,
                };
                if (string.IsNullOrEmpty(product) == false)
                {
                    parameters.Add("Product=" + Uri.EscapeDataString(product));
                }
                return new Uri($"{dataFile.Configuration.DataUpdateUrl}?{string.Join("&", parameters)}");
            }
            else
            {
                return new Uri(dataFile.Configuration.DataUpdateUrl);
            }
        }

        /// <summary>
        /// Get the name the distributor gives the product of the supplied
        /// data file. It is the <see cref="Product"/> if one was given.
        /// Otherwise the name is a prefix for the kind of data file, 'V4'
        /// for Device Detection and 'IPIV4' for IP Intelligence, followed
        /// by the tier of the data the engine has loaded. A tier that
        /// already starts with the prefix is the product name as it is.
        /// For example, 'V4TAC' or 'IPIV4Enterprise'.
        /// </summary>
        /// <param name="dataFile">
        /// The data file to name the product of.
        /// </param>
        /// <returns>
        /// The product name, or null if no data file is loaded.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied data file is null
        /// </exception>
        public string GetDataUpdateProduct(IAspectEngineDataFile dataFile)
        {
            if (dataFile == null)
            {
                throw new ArgumentNullException(nameof(dataFile));
            }

            if (string.IsNullOrEmpty(Product) == false)
            {
                return Product;
            }

            // An engine that gives the product value of its data file apart
            // from its tier is asked for that value.
            var tier =
                (dataFile.Engine as IDataFileProductEngine)
                    ?.GetDataFileProduct(dataFile.Identifier) ??
                dataFile.Engine?.DataSourceTier;
            if (string.IsNullOrEmpty(tier))
            {
                return null;
            }
            var type = (dataFile as IFiftyOneDataFile)?.DataDownloadType;
            var prefix = type != null && type.StartsWith(
                IpIntelligenceType, StringComparison.OrdinalIgnoreCase) ?
                IpIntelligencePrefix : DeviceDetectionPrefix;
            return tier.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ?
                tier : prefix + tier;
        }

        /// <summary>
        /// The start of the 'Type' of an IP Intelligence data file.
        /// </summary>
        private const string IpIntelligenceType = "IPI";

        /// <summary>
        /// The start of the product name of an IP Intelligence data file.
        /// </summary>
        private const string IpIntelligencePrefix = "IPIV4";

        /// <summary>
        /// The start of the product name of a Device Detection data file.
        /// </summary>
        private const string DeviceDetectionPrefix = "V4";
    }
}
