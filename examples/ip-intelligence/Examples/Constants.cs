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
using System.Globalization;
using System.Linq;

namespace FiftyOne.IpIntelligence.Examples
{
    public class Constants
    {
        /// <summary>
        /// The default name of the paid-for enterprise IP Intelligence data file.
        /// </summary>
        public const string ENTERPRISE_IPI_DATA_FILE_NAME = "51Degrees-EnterpriseIpiV41.ipi";

        /// <summary>
        /// The free IP Intelligence data file with the registration
        /// properties, which the examples that show location or
        /// registration properties use by default. The data repository
        /// links it, and the examples download it when it is missing.
        /// </summary>
        public const string LITE_IPI_DATA_FILE_NAME = "51Degrees-LiteV41.ipi";

        /// <summary>
        /// The free IP Intelligence data file with Asn and AsnName, which the
        /// performance and update examples use by default because it is far
        /// smaller to download than the Lite file.
        /// </summary>
        public const string ASN_IPI_DATA_FILE_NAME = "51Degrees-IPIV4AsnIpiV41.ipi";

        /// <summary>
        /// Name of the file to use for the test evidence.
        /// </summary>
        public const string YAML_EVIDENCE_FILE_NAME = "evidence.yml";

        /// <summary>
        /// Name of the file to use for the test evidence.
        /// </summary>
        public const string GEOIP_COMPARISON_EVIDENCE_FILE_NAME = "geoip_comparison_evidence.csv";

        /// <summary>
        /// Environment variable key for the license key file to use for the 
        /// tests.
        /// </summary>
        public const string LICENSE_KEY_ENV_VAR = "IPINTELLIGENCELICENSEKEY_DOTNET";

        /// <summary>
        /// Environment variable key used to supply an explicit path to the
        /// IP Intelligence data file.
        /// </summary>
        public const string IPI_PATH_ENV_VAR = "_51DEGREES_IPI_PATH";

        /// <summary>
        /// An old environment variable for the path to the IP Intelligence
        /// data file, read only when <see cref="IPI_PATH_ENV_VAR"/> is not
        /// set.
        /// </summary>
        [System.Obsolete("Use IPI_PATH_ENV_VAR, which names the environment " +
            "variable that replaces this one.")]
        public const string IP_INTELLIGENCE_DATA_FILE_ENV_VAR = "51DEGREES_IPI_PATH";

        /// <summary>
        /// An old environment variable for the path to the IP Intelligence
        /// data file, read only when <see cref="IPI_PATH_ENV_VAR"/> is not
        /// set.
        /// </summary>
        [System.Obsolete("Use IPI_PATH_ENV_VAR, which names the environment " +
            "variable that replaces this one.")]
        public const string LEGACY_IP_INTELLIGENCE_DATA_FILE_ENV_VAR = "IPINTELLIGENCEDATAFILE";

#pragma warning disable CS0618 // The old names are listed to be read.
        /// <summary>
        /// The old names of
        /// <see cref="IPI_PATH_ENV_VAR"/>,
        /// read in this order only when that variable is not set.
        /// </summary>
        public static readonly string[] OLD_IP_INTELLIGENCE_DATA_FILE_ENV_VARS = new string[]
        {
            IP_INTELLIGENCE_DATA_FILE_ENV_VAR,
            LEGACY_IP_INTELLIGENCE_DATA_FILE_ENV_VAR,
        };
#pragma warning restore CS0618

        /// <summary>
        /// The free Device Detection data file, which the mixed examples
        /// use by default. The data repository links it, and the examples
        /// download it when it is missing.
        /// </summary>
        public const string LITE_HASH_DATA_FILE_NAME = "51Degrees-LiteV4.1.hash";

        /// <summary>
        /// Environment variable key used to supply an explicit path to the
        /// Device Detection data file that the mixed examples use.
        /// </summary>
        public const string DD_PATH_ENV_VAR = "_51DEGREES_DD_PATH";

        /// <summary>
        /// The old names of <see cref="DD_PATH_ENV_VAR"/>, read in this
        /// order only when that variable is not set.
        /// </summary>
        public static readonly string[] OLD_DD_PATH_ENV_VARS = new string[]
        {
            "51DEGREES_DD_PATH",
            "DEVICEDETECTIONDATAFILE",
        };

        /// <summary>
        /// Environment variable key for the evidence file to use for the tests.
        /// </summary>
        public const string EVIDENCE_FILE_ENV_VAR = "EVIDENCEFILE";

        /// <summary>
        /// Environment variable key for the geo ip truth 
        /// evidence file to use for the tests.
        /// </summary>
        public const string GEOIP_TRUTH_EVIDENCE_FILE_ENV_VAR = "GEOIPEVIDENCEFILE";

        /// <summary>
        /// Ports used for running web examples and their tests. When these
        /// are taken on a machine, set _51DEGREES_EXAMPLE_HTTP_PORTS and
        /// _51DEGREES_EXAMPLE_HTTPS_PORTS to other ports, comma separated.
        /// </summary>
        public static readonly int[] LOCALHOST_HTTP_PORTS =
            PortsFromEnvironment("_51DEGREES_EXAMPLE_HTTP_PORTS", new int[] { 5101 });
        public static readonly int[] LOCALHOST_HTTPS_PORTS =
            PortsFromEnvironment("_51DEGREES_EXAMPLE_HTTPS_PORTS", new int[] { 5001, 5002 });

        private static int[] PortsFromEnvironment(string name, int[] defaults)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaults;
            }
            return value.Split(',')
                .Select(p => int.Parse(p.Trim(), CultureInfo.InvariantCulture))
                .ToArray();
        }

        /// <summary>
        /// The URLs that the web server should listen on.
        /// </summary>
        public static string[] AllUrls =>
            Constants.LOCALHOST_HTTP_PORTS.Select(i =>
                $"http://localhost:{i}").Concat(
                Constants.LOCALHOST_HTTPS_PORTS.Select(i =>
                    $"https://localhost:{i}")).ToArray();
    }
}
