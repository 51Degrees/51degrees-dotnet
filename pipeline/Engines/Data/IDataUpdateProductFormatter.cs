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

namespace FiftyOne.Pipeline.Engines.Data
{
    /// <summary>
    /// A URL formatter that can name the product of a data file. The data
    /// update service uses it when the update service answers that the
    /// license keys hold several products.
    /// </summary>
    public interface IDataUpdateProductFormatter : IDataUpdateUrlFormatter
    {
        /// <summary>
        /// Get the name the update service gives the product of the
        /// supplied data file.
        /// </summary>
        /// <param name="dataFile">
        /// The data file to name the product of.
        /// </param>
        /// <returns>
        /// The product name, or null if it is not known, as when no data
        /// file is loaded.
        /// </returns>
        string GetDataUpdateProduct(IAspectEngineDataFile dataFile);

        /// <summary>
        /// Get the URL to call to request an updated version of the
        /// supplied data file for the product named.
        /// </summary>
        /// <param name="dataFile">
        /// The data file to build an update URL for.
        /// </param>
        /// <param name="product">
        /// The product name to request.
        /// </param>
        /// <returns>
        /// The URL to call in order to check for and download an update.
        /// </returns>
        Uri GetFormattedDataUpdateUri(
            IAspectEngineDataFile dataFile, string product);
    }
}
