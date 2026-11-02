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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FiftyOne.IpIntelligence.Examples;
using FiftyOne.IpIntelligence.Examples.Cloud;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.Configuration;
using FiftyOne.Pipeline.Web.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace FiftyOne.IpIntelligence.Examples.Mixed.Cloud.GettingStartedWeb
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Start the server and then wait for the task to finish.
            Run(args).Wait();
        }

        /// <summary>
        /// Used by unit tests to run the example in an almost identical manner
        /// to a developer using the example. Returns the task that the web
        /// server is running in so that the test can trigger the cancellation
        /// token and then wait for the server to shutdown before finishing.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="stopToken"></param>
        /// <returns></returns>
        public static Task Run(
            string[] args,
            CancellationToken stopToken = default)
        {
            var config = CreateConfiguration();
            var configOverrides = CreateConfigOverrides(config);
            return CreateHostBuilder(config, configOverrides, args).Build().RunAsync(
                stopToken);
        }

        public static IHostBuilder CreateHostBuilder(
            IConfiguration baseConfig,
            IDictionary<string, string> overrides,
            string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .ConfigureAppConfiguration(config =>
                        {
                            config
                                .AddConfiguration(baseConfig)
                                .AddInMemoryCollection(overrides);
                        })
                        .UseStartup<Startup>()
                        .UseUrls(Constants.AllUrls);
                });

        private static IConfiguration CreateConfiguration()
            => new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

        /// <summary>
        /// If the resource key in the config file is the placeholder
        /// (begins with '!!'), substitute the value of the
        /// <see cref="ExampleUtils.CLOUD_RESOURCE_KEY_ENV_VAR"/> environment
        /// variable instead. Mirrors the standalone GettingStarted-Web example.
        /// </summary>
        private static Dictionary<string, string> CreateConfigOverrides(IConfiguration config)
        {
            var result = new Dictionary<string, string>();

            PipelineOptions options = new PipelineWebIntegrationOptions();
            var section = config.GetRequiredSection("PipelineOptions");
            section.Bind(options, (o) => { o.ErrorOnUnknownConfiguration = true; });

            var resourceKeyFromConfig = options.GetResourceKey();
            var configHasKey = string.IsNullOrWhiteSpace(resourceKeyFromConfig) == false &&
                    resourceKeyFromConfig.StartsWith("!!") == false;

            if (configHasKey == false)
            {
                var cloudEngineOptions = options.GetElementConfig(nameof(CloudRequestEngine));
                var cloudEngineIndex = options.Elements.IndexOf(cloudEngineOptions);
                var resourceKeyConfigKey = $"PipelineOptions:Elements:{cloudEngineIndex}" +
                    $":BuildParameters:ResourceKey";

                string resourceKey = ExampleUtils.GetResourceKeyFromEnv();

                if (string.IsNullOrEmpty(resourceKey) == false)
                {
                    result.Add(resourceKeyConfigKey, resourceKey);
                }
                else
                {
                    throw new Exception($"No resource key specified in the configuration file " +
                        $"'appsettings.json' or the environment variable " +
                        $"'{ExampleUtils.CLOUD_RESOURCE_KEY_ENV_VAR}'. The 51Degrees Cloud " +
                        $"service is accessed using a 'ResourceKey'. For more information " +
                        $"see https://51degrees.com/documentation/_info__resource_keys.html?utm_source=code&utm_medium=example&utm_campaign=ip-intelligence-dotnet-examples&utm_content=examples-cloud-mixed-gettingstarted-web-program.cs&utm_term=resource-key-required. " +
                        $"A resource key with the properties required by this example can be " +
                        $"created for free at https://configure.51degrees.com/1QWJwHxl?utm_source=code&utm_medium=example&utm_campaign=ip-intelligence-dotnet-examples&utm_content=examples-cloud-mixed-gettingstarted-web-program.cs&utm_term=resource-key-required. " +
                        $"Once complete, populate the config file or environment variable " +
                        $"mentioned at the start of this message with the key.");
                }
            }

            return result;
        }
    }
}