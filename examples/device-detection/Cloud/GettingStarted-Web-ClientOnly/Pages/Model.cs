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

using Microsoft.AspNetCore.Mvc.RazorPages;
using System;

namespace FiftyOne.DeviceDetection.Examples.Cloud.GettingStartedWeb.ClientOnly.Pages
{
    public class Model : PageModel
    {
        /// <summary>
        /// Resource key provided by the environment variable with the key 
        /// <see cref="ExampleUtils.CLOUD_RESOURCE_KEY_ENV_VAR"/>.
        /// </summary>
        public string? ResourceKey { get; private set; }

        /// <summary>
        /// The cloud's address without the API path or a trailing slash,
        /// which the pages add /api/v4/ and the key to. It comes from the
        /// environment variable <see cref="ExampleUtils.CLOUD_ENDPOINT_ENV_VAR"/>,
        /// or is <see cref="DEFAULT_END_POINT"/> when that is not set.
        /// </summary>
        public string? CloudEndPoint { get; private set; }

        /// <summary>
        /// The default cloud end point if the environment variable is not
        /// configured.
        /// </summary>
        private const string DEFAULT_END_POINT = "https://cloud.51degrees.com";

        /// <summary>
        /// The path the pages put between the cloud's address and the key.
        /// </summary>
        private const string API_PATH = "/api/v4";

        public Model()
        {
            ResourceKey = ExampleUtils.GetResourceKeyFromEnv();
            CloudEndPoint = CloudBase(System.Environment.GetEnvironmentVariable(
                ExampleUtils.CLOUD_ENDPOINT_ENV_VAR));
        }

        /// <summary>
        /// The cloud's address from the value of
        /// <see cref="ExampleUtils.CLOUD_ENDPOINT_ENV_VAR"/>. The variable
        /// names the address the cloud request engine calls, which ends
        /// /api/v4/, so that part is taken off here. Left on, the pages
        /// asked for /api/v4//api/v4/{key}.js, which the cloud answers with
        /// a 404, and the page never got its results.
        /// </summary>
        /// <param name="value">The variable's value, or null.</param>
        /// <returns>The address without the API path.</returns>
        public static string CloudBase(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return DEFAULT_END_POINT;
            }
            var endPoint = value.Trim().TrimEnd('/');
            if (endPoint.EndsWith(API_PATH, StringComparison.OrdinalIgnoreCase))
            {
                endPoint = endPoint
                    .Substring(0, endPoint.Length - API_PATH.Length)
                    .TrimEnd('/');
            }
            return endPoint;
        }

        public void OnGet()
        {
        }
    }
}
