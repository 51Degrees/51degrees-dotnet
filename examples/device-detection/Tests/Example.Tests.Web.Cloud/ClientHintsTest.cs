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

using FiftyOne.DeviceDetection.Examples;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace FiftyOne.DeviceDetection.Example.Tests.Web.Cloud
{
    public class ClientHintsTest<T> : ClientHintsExampleTestBase<T> where T : class
    {
        private string CloudEndPoint { get; set; }

        private string ResourceKey { get; set; }

        private string LicenseKey { get; set; }

        private string RequestedProperties { get; set; }

        /// <summary>
        /// Path to the app settings to use for the test.
        /// </summary>
        private string AppSettingsPath { get; set; }

        [TestInitialize]
        public void Init()
        {
            var endPoint = Environment.GetEnvironmentVariable(
                ExampleUtils.CLOUD_ENDPOINT_ENV_VAR);
            CloudEndPoint = string.IsNullOrWhiteSpace(endPoint)
                ? string.Empty
                : endPoint;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Zero based index of the element in the array of the original
            // appsettings file.
            var elementIndex = 0;
            var requestEnginePrefix = $"PipelineOptions:Elements:" +
                $"{elementIndex}:BuildParameters:";
            var resourceKeyConfigKey = $"{requestEnginePrefix}ResourceKey";
            var endPointConfigKey = $"{requestEnginePrefix}EndPoint";
            var testConfigOverrides =
                new Dictionary<string, string>()
                {
                { resourceKeyConfigKey, ResourceKey },
                };
            if (string.IsNullOrEmpty(LicenseKey) == false)
            {
                // The licence key alone, with the properties the case asks
                // for. The example's resource key is replaced with an
                // empty one, which the engine treats as absent.
                testConfigOverrides.Add(
                    $"{requestEnginePrefix}LicenseKey", LicenseKey);
                testConfigOverrides.Add(
                    $"{requestEnginePrefix}RequestedProperties",
                    RequestedProperties);
            }
            if (string.IsNullOrEmpty(CloudEndPoint) == false)
            {
                testConfigOverrides.Add(endPointConfigKey, CloudEndPoint);
            }

            builder.ConfigureAppConfiguration((context, builder) =>
            {
                builder
                    // First add the appsettings.json file for
                    // this example.
                    .AddJsonFile(AppSettingsPath)
                    // Now override the configuration with any
                    // specific values we want to use for the test.
                    .AddInMemoryCollection(testConfigOverrides);
            });

            base.ConfigureWebHost(builder);
        }

        /// <summary>
        /// Runs the example for one case of
        /// <see cref="Parameters.VerifyCloudExample_DATA"/> and checks
        /// Accept-CH. A case whose key is not set is skipped, naming the
        /// variable it needs. Every case is skipped on a build entry that
        /// leaves out the live cloud tests, licence key cases included.
        /// </summary>
        /// <param name="caseName">The name of the case.</param>
        /// <param name="requestedProperties">
        /// The properties the licence key case asks for, or null for the
        /// resource key case.
        /// </param>
        /// <param name="userAgent">The User-Agent to send.</param>
        /// <param name="expectedAcceptCH">
        /// The values expected in Accept-CH.
        /// </param>
        /// <param name="exampleDirName">The example to run.</param>
        protected async Task VerifyExample(
            string caseName,
            string requestedProperties,
            string userAgent,
            List<string> expectedAcceptCH,
            string exampleDirName)
        {
            if (ExampleResourceKey.RunHere == false)
            {
                Assert.Inconclusive(ExampleResourceKey.NotRunHere);
            }
            if (requestedProperties == null)
            {
                var resourceKey = LiveCloudKey.Find();
                if (resourceKey == null)
                {
                    Assert.Inconclusive(LiveCloudKey.NoKey(
                        "device",
                        "the SetHeaderBrowserAccept-CH, " +
                        "SetHeaderHardwareAccept-CH and " +
                        "SetHeaderPlatformAccept-CH properties"));
                }
                ResourceKey = resourceKey;
                LicenseKey = null;
                RequestedProperties = null;
            }
            else
            {
                var licenseKey = Environment.GetEnvironmentVariable(
                    Parameters.LICENSE_KEY_ENV_VAR);
                if (string.IsNullOrWhiteSpace(licenseKey))
                {
                    Assert.Inconclusive(
                        $"The '{caseName}' case calls the live 51Degrees " +
                        "cloud service with a licence key, and " +
                        $"{Parameters.LICENSE_KEY_ENV_VAR} is not set. The " +
                        "licence has to carry the device product, because " +
                        $"the case asks for {requestedProperties}.");
                }
                ResourceKey = string.Empty;
                LicenseKey = licenseKey;
                RequestedProperties = requestedProperties;
            }

            // Determine the path to the app settings file we need to used
            string appSettingsPath = Environment.CurrentDirectory;
            var pos = appSettingsPath.IndexOf("Tests");
            appSettingsPath = appSettingsPath.Remove(pos);
            AppSettingsPath = Path.Combine(
                appSettingsPath,
                "Legacy Web",
                exampleDirName,
                "appsettings.json");

            // Has the side effect of starting the web server and calling
            // ConfigureWebHost.
            using (var http = CreateClient())
            using (var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get
            })
            { 
                request.Headers.Add("User-Agent", userAgent);
                using (var response = await http.SendAsync(request))
                {
                    // Verify the request was successful.
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                    VerifyAcceptCH(response.Headers, expectedAcceptCH);
                }
            }
        }
    }
}