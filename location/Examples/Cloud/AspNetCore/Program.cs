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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AspNetCore
{
    public class Program
    {
        /// <summary>
        /// The environment variable the resource key is read from when
        /// appsettings.json does not give one.
        /// </summary>
        private const string ResourceKeyVariable = "_51DEGREES_RESOURCE_KEY";

        public static void Main(string[] args)
        {
            var keys = ResourceKeys();
            if (keys == null)
            {
                // There is no resource key, so the message says where to
                // create one and the example ends without a server.
                Console.WriteLine(
                    "No resource key was given in appsettings.json or in the " +
                    $"environment variable '{ResourceKeyVariable}'. A resource " +
                    "key can be created at " +
                    "https://51degrees.com/configure?utm_source=code&utm_medium=example&utm_campaign=51degrees-dotnet&utm_content=examples-cloud-aspnetcore-program.cs&utm_term=resource-key-required. " +
                    "Make sure to include the 'Country' property as it is " +
                    "used by this example.");
                return;
            }
            CreateHostBuilder(args)
                .ConfigureAppConfiguration(c => c.AddInMemoryCollection(keys))
                .Build()
                .Run();
        }

        /// <summary>
        /// The key from the environment for each cloud request engine
        /// that appsettings.json gives no key, or null when one has no key
        /// from either.
        /// </summary>
        private static Dictionary<string, string> ResourceKeys()
        {
            var settings = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .Build();
            var key = Environment.GetEnvironmentVariable(ResourceKeyVariable);
            var keys = new Dictionary<string, string>();
            foreach (var element in settings
                .GetSection("PipelineOptions:Elements").GetChildren())
            {
                var builder = element["BuilderName"] ?? "";
                if (builder.Contains("CloudRequestEngine",
                    StringComparison.OrdinalIgnoreCase) == false)
                {
                    continue;
                }
                var given = element["BuildParameters:ResourceKey"];
                if (string.IsNullOrWhiteSpace(given) == false &&
                    given.StartsWith("!!") == false)
                {
                    continue;
                }
                if (string.IsNullOrWhiteSpace(key))
                {
                    return null;
                }
                keys[$"{element.Path}:BuildParameters:ResourceKey"] = key;
            }
            return keys;
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
