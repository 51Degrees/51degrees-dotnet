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

namespace FiftyOne.Common
{
    public class Headers
    {
        /// <summary>
        /// The separator character used by pseudo-headers to split 
        /// header names and values.
        /// </summary>
        public const char PSEUDEO_HEADER_SEPARATOR = '\x1f';

        public const string UACH_MODEL = "Sec-CH-UA-Model";
        public const string UACH_ARCH = "Sec-CH-UA-Arch";
        public const string UACH_MOBILE = "Sec-CH-UA-Mobile";
        public const string UACH_PLATFORM = "Sec-CH-UA-Platform";
        public const string UACH_PLATFORM_VERSION = "Sec-CH-UA-Platform-Version";
        public const string UACH_UA = "Sec-CH-UA";
        public const string UACH_UA_FULL_VERSION = "Sec-CH-UA-Full-Version";
        public const string UACH_UA_FULL_VERSION_LIST = "Sec-CH-UA-Full-Version-List";

        public static readonly string PSEUDO_HARDWARE =
            $"{UACH_PLATFORM}{PSEUDEO_HEADER_SEPARATOR}{UACH_MODEL}" +
            $"{PSEUDEO_HEADER_SEPARATOR}{UACH_MOBILE}";
        public static readonly string PSEUDO_BROWSER =
            $"{UACH_UA_FULL_VERSION_LIST}{PSEUDEO_HEADER_SEPARATOR}{UACH_MOBILE}" +
            $"{PSEUDEO_HEADER_SEPARATOR}{UACH_PLATFORM}";
        public static readonly string PSEUDO_BROWSER2 =
            $"{UACH_UA}{PSEUDEO_HEADER_SEPARATOR}{UACH_MOBILE}" +
            $"{PSEUDEO_HEADER_SEPARATOR}{UACH_PLATFORM}";
        public static readonly string PSEUDO_PLATFORM =
            $"{UACH_PLATFORM}{PSEUDEO_HEADER_SEPARATOR}{UACH_PLATFORM_VERSION}";
        public const string OPERA_MINI_UA = "X-OperaMini-Phone-UA";

    }
}
