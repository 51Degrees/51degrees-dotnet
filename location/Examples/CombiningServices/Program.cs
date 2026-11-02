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

using FiftyOne.DeviceDetection;
using FiftyOne.DeviceDetection.Cloud.FlowElements;
using FiftyOne.GeoLocation;
using FiftyOne.GeoLocation.Cloud.FlowElements;
using FiftyOne.GeoLocation.Core;
using FiftyOne.GeoLocation.Core.Data;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// @example CombiningServices/Program.cs
/// 
/// using 51Degrees reverse geocoding alongside 
/// 51Degrees device detection.
/// 
/// This example is available in full on [GitHub](https://github.com/51Degrees/location-dotnet/blob/master/Examples/CombiningServices/Program.cs). 
/// 
/// To run this example, you will need to create a **resource key**. 
/// The resource key is used as short-hand to store the particular set of 
/// properties you are interested in as well as any associated license keys 
/// that entitle you to increased request limits and/or paid-for properties.
/// 
/// You can create a resource key using the 51Degrees [Configurator](https://configure.51degrees.com?utm_source=code&amp;utm_medium=example&amp;utm_campaign=location-dotnet&amp;utm_content=examples-combiningservices-program.cs&amp;utm_term=combiningservices).
/// 
/// Required NuGet Dependencies:
/// - [FiftyOne.GeoLocation](https://www.nuget.org/packages/FiftyOne.GeoLocation/)
/// - [FiftyOne.DeviceDetection](https://www.nuget.org/packages/FiftyOne.DeviceDetection/)
/// 
/// The example shows how to:
/// 
/// 1. Build new cloud-based geolocation, and device detection engines, along
/// with the cloud request engine on which they rely.
/// 2. Build a new Pipeline containing the engines which were just created.
/// 3. Create a new FlowData instance ready to be populated with evidence for
/// the Pipeline.
/// 4. Process a longitude and latitude and User-Agent to retrieve the values
/// associated with the location and device for the selected properties.
/// 5. Extract the value of a property as a string from the results.

/// </summary>
namespace CombiningServices
{
    public class Program
    {
        public class Example
        {
            private static ILoggerFactory _loggerFactory = new LoggerFactory();

            private static HttpClient _httpClient = new HttpClient();

            private static string mobileUserAgent =
                "Mozilla/5.0 (Linux; Android 9; SAMSUNG SM-G960U) " +
                "AppleWebKit/537.36 (KHTML, like Gecko) SamsungBrowser/10.1 " +
                "Chrome/71.0.3578.99 Mobile Safari/537.36";

            private static string latitude = "51.458048";
            private static string longitude = "-0.9822207999999999";

            public void Run(string resourceKey, string cloudEndPoint = "") 
            {
                Console.WriteLine($"Using resource key {resourceKey}");

                var cloudRequestEngineBuilder =
                    new CloudRequestEngineBuilder(_loggerFactory, _httpClient)
                    .SetResourceKey(resourceKey);

                if (string.IsNullOrWhiteSpace(cloudEndPoint) == false)
                {
                    cloudRequestEngineBuilder.SetEndPoint(cloudEndPoint);
                }

                var cloudRequestEngine = cloudRequestEngineBuilder
                    .Build();
                var deviceDetectionEngine =
                    new DeviceDetectionCloudEngineBuilder(_loggerFactory)
                    .Build();
                var locationEngine =
                    new GeoLocationCloudEngineBuilder(
                        _loggerFactory)
                    .Build(GeoLocationProvider.FiftyOneDegrees);

                using (var pipeline =
                    new PipelineBuilder(_loggerFactory)
                    .AddFlowElement(cloudRequestEngine)
                    .AddFlowElement(deviceDetectionEngine)
                    .AddFlowElement(locationEngine)
                    .Build())
                {
                    using (var data = pipeline.CreateFlowData())
                    {
                        data.AddEvidence("header.user-agent", mobileUserAgent);
                        data.AddEvidence("query.51D_Pos_latitude", latitude);
                        data.AddEvidence("query.51D_Pos_longitude", longitude);

                        data.Process();

                        Console.Write($"Awaiting response");
                        CancellationTokenSource cancellationSource = new CancellationTokenSource();
                        Task.Run(() => { OutputUntilCancelled(".", 1000, cancellationSource.Token); });

                        var country = data.Get<IGeoData>().Country;
                        var isMobile = data.Get<IDeviceData>().IsMobile;

                        cancellationSource.Cancel();
                        Console.WriteLine();

                        if (country.HasValue)
                        {
                            Console.WriteLine($"Country: {country.Value}");
                        }
                        else
                        {
                            Console.WriteLine($"Country: {country.NoValueMessage}");
                        }

                        if (isMobile.HasValue)
                        {
                            Console.WriteLine($"IsMobile: {isMobile.Value}");
                        }
                        else
                        {
                            Console.WriteLine($"IsMobile: {isMobile.NoValueMessage}");
                        }
                    }
                }
            }

            private static void OutputUntilCancelled(string text, int intervalMs, CancellationToken token)
            {
                while (token.IsCancellationRequested == false)
                {
                    Console.Write(text);
                    Task.Delay(intervalMs).Wait();
                }
            }
        }

        static void Main(string[] args)
        {
            // Obtain a resource key for free at https://configure.51degrees.com?utm_source=code&utm_medium=example&utm_campaign=location-dotnet&utm_content=examples-combiningservices-program.cs&utm_term=main
            // Make sure to include the 'Country' and 'IsMobile' properties as
            // they are used by this example.
            string resourceKey = args.Length > 0 ? args[0] : "!!YOUR_RESOURCE_KEY!!";

            if (resourceKey.StartsWith("!!"))
            {
                Console.WriteLine("You need to create a resource key at " +
                    "https://configure.51degrees.com?utm_source=code&utm_medium=example&utm_campaign=location-dotnet&utm_content=examples-combiningservices-program.cs&utm_term=resource-key-required and paste it into this example.");
                Console.WriteLine("Make sure to include the 'Country' and " +
                    "'IsMobile' properties as they are used by this example.");
            }
            else
            {
                new Example().Run(resourceKey);
            }
#if (DEBUG)
            Console.WriteLine("Complete. Press key to exit.");
            Console.ReadKey();
#endif
        }
    }
}
