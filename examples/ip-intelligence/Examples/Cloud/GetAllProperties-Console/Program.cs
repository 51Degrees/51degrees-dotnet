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

using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using System;
using System.Collections;
using System.Linq;
/// <summary>
/// @example Cloud/GetAllProperties-Console/Program.cs
/// 
/// This example shows how to retrieve all available IP Intelligence properties from the 51Degrees Cloud service.
/// 
/// You will learn:
/// 
/// 1. How to create a Pipeline that uses 51Degrees Cloud IP Intelligence
/// 2. How to process an IP address and retrieve all available properties
/// 3. How to enumerate through all the properties returned by the service
/// 
/// This example is available in full on [GitHub](https://github.com/51Degrees/ip-intelligence-dotnet-examples/blob/main/Examples/Cloud/GetAllProperties-Console/Program.cs).
///
/// To run this example, create a Resource Key for free at https://configure.51degrees.com?utm_source=code&amp;utm_medium=example&amp;utm_campaign=ip-intelligence-dotnet-examples&amp;utm_content=examples-cloud-getallproperties-console-program.cs&amp;utm_term=header and supply it as the first command-line argument or via the 51DEGREES_RESOURCE_KEY environment variable. By default the pipeline talks to cloud.51degrees.com; set 51D_CLOUD_ENDPOINT to point at a self-hosted Cloud service instead.
/// </summary>
namespace FiftyOne.IpIntelligence.Examples.Cloud.GetAllProperties
{
    public class Program
    {
        public class Example
        {
            private const string SomeIpAddress = "8.8.8.8";

            public void Run(string resourceKey, string cloudEndPoint = "")
            {
                var builder = new IpiPipelineBuilder()
                    // Tell it that we want to use cloud and pass our resource key.
                    .UseCloud(resourceKey);

                // If a cloud endpoint has been provided then set the
                // cloud pipeline endpoint. Otherwise the default
                // (cloud.51degrees.com) is used.
                if (string.IsNullOrWhiteSpace(cloudEndPoint) == false)
                {
                    builder.SetEndPoint(cloudEndPoint);
                }

                // Create the pipeline
                using (var pipeline = builder.Build())
                {
                    // Output details for the IP address
                    AnalyseEvidence(SomeIpAddress, pipeline);
                }
            }

            static void AnalyseEvidence(string ipAddress, IPipeline pipeline)
            {
                // Create the FlowData instance.
                using (var data = pipeline.CreateFlowData())
                {
                    // Add Client IP as evidence.
                    data.AddEvidence(FiftyOne.Pipeline.Core.Constants.EVIDENCE_CLIENTIP_KEY, ipAddress);
                    // Process the supplied evidence.
                    data.Process();
                    // Get device data from the flow data.
                    var device = data.Get<IIpIntelligenceData>();
                    Console.WriteLine($"What property values are associated with " +
                        $"the IP '{ipAddress}'?");

                    // Iterate through device data results, displaying all values.
                    foreach (var property in device.AsDictionary()
                        .OrderBy(p => p.Key))
                    {
                        Console.WriteLine($"{property.Key} = {GetValueToOutput(property.Value)}");
                    }
                }
            }

            /// <summary>
            /// Convert the given value into a human-readable string representation 
            /// </summary>
            /// <param name="propertyValue">
            /// Property value object to be converted
            /// </param>
            /// <returns></returns>
            private static string GetValueToOutput(object propertyValue)
            {
                if (propertyValue == null)
                {
                    return "NULL";
                }

                var basePropetyType = propertyValue.GetType();
                var basePropertyValue = propertyValue;

                if (propertyValue is IAspectPropertyValue aspectPropertyValue)
                {
                    if (aspectPropertyValue.HasValue)
                    {
                        // Get the type and value parameters from the 
                        // AspectPropertyValue instance.
                        basePropetyType = basePropetyType.GenericTypeArguments[0];
                        basePropertyValue = aspectPropertyValue.Value;
                    }
                    else
                    {
                        // The property has no value so output the reason.
                        basePropetyType = typeof(string);
                        basePropertyValue = $"NO VALUE ({aspectPropertyValue.NoValueMessage})";
                    }
                }

                if (basePropetyType != typeof(string) &&
                    typeof(IEnumerable).IsAssignableFrom(basePropetyType))
                {
                    // Property is an IEnumerable (that is not a string)
                    // so return a comma-separated list of values.
                    var collection = basePropertyValue as IEnumerable;
                    var output = "";
                    foreach (var entry in collection)
                    {
                        if (output.Length > 0) { output += ","; }
                        output += entry.ToString();
                    }
                    return output;
                }
                else
                {
                    var str = basePropertyValue.ToString();
                    // Truncate any long strings to 200 characters
                    if (str.Length > 200)
                    {
                        str = str.Remove(200);
                        str += "...";
                    }
                    return str;
                }
            }
        }

        static void Main(string[] args)
        {
            // Use the command line args to get the resource key if present.
            // Otherwise, get it from the environment variable.
            string resourceKey = args.Length > 0 ? args[0] :
                ExampleUtils.GetResourceKeyFromEnv();

            // Optional custom cloud endpoint via env var. If unset, the SDK
            // defaults to cloud.51degrees.com.
            string cloudEndPoint = Environment.GetEnvironmentVariable(
                ExampleUtils.CLOUD_END_POINT_ENV_VAR) ?? "";

            if (string.IsNullOrWhiteSpace(resourceKey))
            {
                Console.WriteLine($"No resource key specified on the command line or in the " +
                    $"environment variable '{ExampleUtils.CLOUD_RESOURCE_KEY_ENV_VAR}'. " +
                    $"Obtain a resource key at https://configure.51degrees.com?utm_source=code&utm_medium=example&utm_campaign=ip-intelligence-dotnet-examples&utm_content=examples-cloud-getallproperties-console-program.cs&utm_term=resource-key-required and supply it " +
                    $"as the first argument or via that environment variable.");
            }
            else
            {
                new Example().Run(resourceKey, cloudEndPoint);
            }
#if (DEBUG)
            // Only prompt when running interactively. When stdin is redirected
            // (e.g. tests, CI, piping), Console.ReadKey throws.
            if (Console.IsInputRedirected == false)
            {
                Console.WriteLine("Done. Press any key to exit.");
                Console.ReadKey();
            }
#endif
        }
    }
}
