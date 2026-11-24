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
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Data
{
    /// <summary>
    /// 51Degrees specific URL formatter. This formats a data file download URL
    /// using the parameters required by the 51Degrees distributor.
    /// </summary>
    public class FiftyOneUrlFormatter : IDataUpdateUrlFormatter
    {
        /// <summary>
        /// Constructor for a formatter that names no product. When the
        /// license keys hold several products, the data update service
        /// asks for the product of the data file the engine has loaded.
        /// </summary>
        public FiftyOneUrlFormatter()
        {
        }

        /// <summary>
        /// Constructor for a formatter that names the product to request.
        /// Every request then asks for that product, and the product of the
        /// loaded data file is not used. Use it when the license keys hold
        /// several products, for an update on start-up, which runs before
        /// a data file is loaded, or to ask for a product other than the
        /// one of the loaded data file.
        /// </summary>
        /// <param name="product">
        /// The name the distributor gives the product, which is sent as it
        /// is given. For example, 'V4TAC' or 'IPIV4Enterprise'. Null or
        /// empty names no product.
        /// </param>
        public FiftyOneUrlFormatter(string product)
        {
            Product = string.IsNullOrEmpty(product) ? null : product;
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
                if (Product != null)
                {
                    parameters.Add("Product=" + Uri.EscapeDataString(Product));
                }
                return new Uri($"{dataFile.Configuration.DataUpdateUrl}?{string.Join("&", parameters)}");
            }
            else
            {
                return new Uri(dataFile.Configuration.DataUpdateUrl);
            }
        }
    }
}
