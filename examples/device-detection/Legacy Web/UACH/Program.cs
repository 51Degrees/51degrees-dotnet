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
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System.IO;

namespace Client_Hints
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureAppConfiguration(UseDataFile);
                    webBuilder.UseStartup<Startup>();
                });

        /// <summary>
        /// The setting that holds the data file of the device detection
        /// engine, which is the first element in appsettings.json.
        /// </summary>
        private const string DataFileSetting =
            "PipelineOptions:Elements:0:BuildParameters:DataFile";

        /// <summary>
        /// The engine looks for a data file named without a path in the
        /// current directory only, and the examples share one copy of each
        /// data file. So a name without a path is found here, in the
        /// '_51DEGREES_DD_PATH' environment variable or the data folder of
        /// the repository, where the free Lite file is downloaded when it is
        /// missing, and the engine is given the full path. An application
        /// of your own can name the full path in its configuration.
        /// </summary>
        private static void UseDataFile(IConfigurationBuilder config)
        {
            var configured = config.Build()[DataFileSetting];
            if (string.IsNullOrEmpty(configured) ||
                Path.IsPathRooted(configured))
            {
                return;
            }
            var found = ExampleUtils.FindDataFile(configured);
            if (found != null)
            {
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    [DataFileSetting] = found
                });
            }
        }
    }
}
