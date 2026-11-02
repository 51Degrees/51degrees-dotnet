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

using FiftyOne.GeoLocation;
using FiftyOne.GeoLocation.Core.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// @example Cloud/GettingStarted/Program.cs
/// 
/// Getting started example of using 51Degrees reverse geocoding.
/// 
/// This example is available in full on [GitHub](https://github.com/51Degrees/location-dotnet/blob/main/Examples/Cloud/GettingStarted/Program.cs). 
/// 
/// To run this example, you will need to create a **resource key**. 
/// The resource key is used as short-hand to store the particular set of 
/// properties you are interested in as well as any associated license keys 
/// that entitle you to increased request limits and/or paid-for properties.
/// 
/// You can create a resource key using the 51Degrees [Configurator](https://configure.51degrees.com?utm_source=code&amp;utm_medium=example&amp;utm_campaign=location-dotnet&amp;utm_content=examples-cloud-gettingstarted-program.cs&amp;utm_term=gettingstarted).
/// 
/// Required NuGet Dependencies:
/// - [FiftyOne.GeoLocation](https://www.nuget.org/packages/FiftyOne.GeoLocation/)
/// 
/// The example shows how to:
/// 
/// 1. Build a new Pipeline with a cloud-based geolocation engine, with lazy
/// loading configured to allow up to a second for a response from the cloud
/// service.
/// 2. Create a new FlowData instance ready to be populated with evidence for
/// the Pipeline.
/// 3. Process a longitude and latitude to retrieve the values associated with
/// with the location for the selected properties.
/// 4. Extract the value of a property as a string from the results.
/// </summary>
namespace GettingStarted
{
    public class Program
    {
        public class Example
        {
            private static ILoggerFactory _loggerFactory = new LoggerFactory();

            private static string latitude = "51.458048";
            private static string longitude = "-0.9822207999999999";

            public void Run(string resourceKey, string cloudEndPoint = "")
            {
                // Create a new pipeline builder.
                var pipelineBuilder = new GeoLocationPipelineBuilder(_loggerFactory)
                    // Obtain a resource key from https://configure.51degrees.com?utm_source=code&utm_medium=example&utm_campaign=location-dotnet&utm_content=examples-cloud-gettingstarted-program.cs&utm_term=run 
                    // and select your Geo Location provider.
                    .UseCloud(resourceKey, FiftyOne.GeoLocation.Core.GeoLocationProvider.FiftyOneDegrees)
                    .UseLazyLoading(TimeSpan.FromSeconds(10));

                // If a cloud endpoint has been provided then set the
                // cloud pipeline endpoint. 
                if (string.IsNullOrWhiteSpace(cloudEndPoint) == false)
                {
                    pipelineBuilder.SetEndPoint(cloudEndPoint);
                }

                using (var pipeline = pipelineBuilder.Build())
                {
                    using (var data = pipeline.CreateFlowData())
                    {
                        data.AddEvidence("query.51D_Pos_latitude", latitude);
                        data.AddEvidence("query.51D_Pos_longitude", longitude);

                        data.Process();

                        var country = data.Get<IGeoData>().Country;

                        Console.Write($"Awaiting response");
                        CancellationTokenSource cancellationSource = new CancellationTokenSource();
                        Task.Run(() => { OutputUntilCancelled(".", 1000, cancellationSource.Token); });
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
                    }
                }
            }

            private void OutputUntilCancelled(string text, int intervalMs, CancellationToken token)
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
            // Obtain a resource key for free at https://configure.51degrees.com?utm_source=code&utm_medium=example&utm_campaign=location-dotnet&utm_content=examples-cloud-gettingstarted-program.cs&utm_term=main
            // Make sure to include the 'Country' property as it is used by this example.
            string resourceKey = args.Length > 0 ? args[0] : "!!YOUR_RESOURCE_KEY!!";

            if (resourceKey.StartsWith("!!"))
            {
                Console.WriteLine("You need to create a resource key at " +
                    "https://configure.51degrees.com?utm_source=code&utm_medium=example&utm_campaign=location-dotnet&utm_content=examples-cloud-gettingstarted-program.cs&utm_term=resource-key-required and paste it into this example.");
                Console.WriteLine("Make sure to include the 'Country' " +
                    "property as it is used by this example.");
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
