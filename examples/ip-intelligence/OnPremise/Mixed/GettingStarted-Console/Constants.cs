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

namespace FiftyOne.IpIntelligence.Examples.Mixed.OnPremise.GettingStartedConsole
{
    public static class Constants
    {
        /// <summary>
        /// The free Device Detection data file, which the example uses unless
        /// its first argument names another. The data repository links it, and
        /// the example downloads it when it is missing.
        /// </summary>
        public const string LITE_HASH_FILE_NAME = "51Degrees-LiteV4.1.hash";

        /// <summary>
        /// The free IP Intelligence data file, which the example uses unless
        /// its second argument or an environment variable names another.
        /// </summary>
        public const string LITE_IPI_FILE_NAME =
            Examples.Constants.LITE_IPI_DATA_FILE_NAME;
    }
}