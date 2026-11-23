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

using System.Collections.Generic;
using System.Linq;

using Base = FiftyOne.DeviceDetection.Example.Tests.Web.Parameters;

namespace FiftyOne.DeviceDetection.Example.Tests.Web.Cloud
{
    /// <summary>
    /// The cases the Accept-CH tests run against the cloud web examples.
    /// The case with every SetHeader property uses the resource key that
    /// <see cref="LiveCloudKey"/> finds. The single header cases use the
    /// licence key in <see cref="LICENSE_KEY_ENV_VAR"/> alone and ask the
    /// cloud for only the properties they need, sent as 'values'. No key
    /// is part of a case, so no key appears in a test's name.
    /// </summary>
    public static class Parameters
    {
        /// <summary>
        /// The licence key the single header cases use.
        /// </summary>
        public const string LICENSE_KEY_ENV_VAR =
            "_51DEGREES_LICENSE_KEY_BESPOKE";

        /// <summary>
        /// The case that uses the resource key, which carries every
        /// SetHeader property.
        /// </summary>
        public const string RESOURCE_KEY_CASE = "resource key";

        /// <summary>
        /// The properties the example page shows. None of them needs a
        /// client hint, so they add nothing to Accept-CH. The page reads
        /// each of them, so every case asks for them.
        /// </summary>
        public static readonly IReadOnlyList<string> PAGE_PROPERTIES =
            Base.BASE_PROPERTIES.Split(',')
                .Select(p => $"device.{p}")
                .ToList();

        public const string BROWSER_PROPERTY =
            "device.SetHeaderBrowserAccept-CH";
        public const string HARDWARE_PROPERTY =
            "device.SetHeaderHardwareAccept-CH";
        public const string PLATFORM_PROPERTY =
            "device.SetHeaderPlatformAccept-CH";

        /// <summary>
        /// The properties each licence key case asks for, by the name of
        /// the case. Each names one SetHeader property at most, with its
        /// component, and 'none' names none.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string>
            LICENSE_KEY_CASES = new Dictionary<string, string>()
        {
            { "browser", Values(BROWSER_PROPERTY) },
            { "hardware", Values(HARDWARE_PROPERTY) },
            { "platform", Values(PLATFORM_PROPERTY) },
            { "none", Values() },
        };

        private static string Values(params string[] setHeader) =>
            string.Join(",", PAGE_PROPERTIES.Concat(setHeader));

        /// <summary>
        /// Used to supply parameters to the VerifyExample tests for
        /// cloud-backed examples. Each row is the case, the properties it
        /// asks for (null for the resource key case), the User-Agent and
        /// the values expected in Accept-CH.
        /// </summary>
        public static IEnumerable<object[]> VerifyCloudExample_DATA()
        {
            var cases = new List<(string Name, string Values)>()
            {
                (RESOURCE_KEY_CASE, null)
            };
            cases.AddRange(LICENSE_KEY_CASES.Select(c => (c.Key, c.Value)));

            List<string> UserAgents = new List<string>()
            {
                Base.CHROME_UA,
                Base.EDGE_UA,
                Base.FIREFOX_UA,
                Base.SAFARI_UA,
                Base.CURL_UA
            };

            foreach (var testCase in cases)
            {
                foreach (var userAgent in UserAgents)
                {
                    yield return new object[]
                    {
                        testCase.Name,
                        testCase.Values,
                        userAgent,
                        ExpectedAcceptCH(testCase.Values, userAgent)
                    };
                }
            }
        }

        /// <summary>
        /// The values expected in Accept-CH. Only Chrome and Edge are sent
        /// any, and only for the SetHeader properties the case has. The
        /// resource key case has all three.
        /// </summary>
        private static List<string> ExpectedAcceptCH(
            string values,
            string userAgent)
        {
            var expected = new List<string>();
            if (userAgent != Base.CHROME_UA && userAgent != Base.EDGE_UA)
            {
                return expected;
            }
            var asked = values?.Split(',');
            if (asked == null || asked.Contains(BROWSER_PROPERTY))
            {
                expected.AddRange(Base.BROWSER_ACCEPT_CH);
            }
            if (asked == null || asked.Contains(HARDWARE_PROPERTY))
            {
                expected.AddRange(Base.HARDWARE_ACCEPT_CH);
            }
            if (asked == null || asked.Contains(PLATFORM_PROPERTY))
            {
                expected.AddRange(Base.PLATFORM_ACCEPT_CH);
            }
            return expected;
        }
    }
}
