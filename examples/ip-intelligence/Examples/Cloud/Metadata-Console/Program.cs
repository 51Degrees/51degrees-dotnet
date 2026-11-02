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

using FiftyOne.IpIntelligence.Cloud.FlowElements;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.Data;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;
using FiftyOne.Pipeline.Engines.Data;

/// <summary>
/// @example Cloud/Metadata-Console/Program.cs
///
/// The Cloud service exposes metadata that can provide additional information about the various 
/// properties that might be returned.
/// This example shows how to access this data and display the values available.
/// 
/// A list of the properties will be displayed, along with some additional information about each
/// property. Note that this is the list of properties used by the supplied resource key, rather
/// than all properties that can be returned by the Cloud service.
/// 
/// In addition, the evidence keys that are accepted by the service are listed. These are the 
/// keys that, when added to the evidence collection in flow data, could have some impact on the
/// result that is returned.
/// 
/// Bear in mind that this is a list of ALL evidence keys accepted by all products offered by the 
/// cloud. If you are only using a single product (for example - device detection) then not all
/// of these keys will be relevant.
/// 
/// This example is available in full on [GitHub](https://github.com/51Degrees/ip-intelligence-dotnet-examples/blob/main/Examples/Cloud/Metadata-Console/Program.cs).
///
/// To run this example, create a Resource Key for free at https://configure.51degrees.com?utm_source=code&amp;utm_medium=example&amp;utm_campaign=ip-intelligence-dotnet-examples&amp;utm_content=examples-cloud-metadata-console-program.cs&amp;utm_term=header and supply it as the first command-line argument or via the 51DEGREES_RESOURCE_KEY environment variable. By default the pipeline talks to cloud.51degrees.com; set 51D_CLOUD_ENDPOINT to point at a self-hosted Cloud service instead.
/// 
/// Required NuGet Dependencies:
/// - [FiftyOne.IpIntelligence](https://www.nuget.org/packages/FiftyOne.IpIntelligence/)
/// - [Microsoft.Extensions.Logging.Console](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Console/)
/// </summary>
namespace FiftyOne.IpIntelligence.Examples.Cloud.Metadata
{
    public class Program
    {
        public class Example : ExampleBase
        {
            public void Run(string resourceKey, ILoggerFactory loggerFactory, TextWriter output)
            {
                var builder = new IpiPipelineBuilder(loggerFactory)
                    .UseCloud(resourceKey);

                // Optional custom cloud endpoint via env var. If unset, the SDK
                // defaults to cloud.51degrees.com.
                var cloudEndPoint = Environment.GetEnvironmentVariable(
                    ExampleUtils.CLOUD_END_POINT_ENV_VAR);
                if (string.IsNullOrWhiteSpace(cloudEndPoint) == false)
                {
                    builder.SetEndPoint(cloudEndPoint);
                }

                using (var pipeline = builder.Build())
                {
                    OutputProperties(pipeline.GetElement<IpiCloudEngine>(), output);
                    // We use the CloudRequestEngine to get evidence key details, rather than the
                    // IpiCloudEngine.
                    // This is because the IpiCloudEngine doesn't actually make use
                    // of any evidence values. It simply processes the JSON that is returned
                    // by the call to the Cloud service that is made by the CloudRequestEngine.
                    // The CloudRequestEngine is actually taking the evidence values and passing
                    // them to the cloud, so that's the engine we want the keys from.
                    OutputEvidenceKeyDetails(pipeline.GetElement<CloudRequestEngine>(), output);
                }
            }

            private void OutputEvidenceKeyDetails(CloudRequestEngine engine, TextWriter output)
            {
                output.WriteLine();
                if (typeof(EvidenceKeyFilterWhitelist).IsAssignableFrom(engine.EvidenceKeyFilter.GetType()))
                {
                    // If the evidence key filter extends EvidenceKeyFilterWhitelist then we can
                    // display a list of accepted keys.
                    var filter = engine.EvidenceKeyFilter as EvidenceKeyFilterWhitelist;
                    output.WriteLine($"Accepted evidence keys:");
                    foreach (var entry in filter.Whitelist)
                    {
                        output.WriteLine($"\t{entry.Key}");
                    }
                }
                else
                {
                    output.WriteLine($"The evidence key filter has type " +
                        $"{engine.EvidenceKeyFilter.GetType().Name}. As this does not extend " +
                        $"EvidenceKeyFilterWhitelist, a list of accepted values cannot be " +
                        $"displayed. As an alternative, you can pass evidence keys to " +
                        $"filter.Include(string) to see if a particular key will be included " +
                        $"or not.");
                    output.WriteLine($"For example, query.client-ip is " +
                        (engine.EvidenceKeyFilter.Include("query.client-ip") ? "" : "not ") +
                        "accepted.");
                }

            }

            private void OutputProperties(IpiCloudEngine engine, TextWriter output)
            {
                foreach (var property in engine.Properties)
                {
                    // Output some details about the property.
                    // If we're outputting to console then we also add some formatting to make it 
                    // more readable.
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.BackgroundColor = ConsoleColor.DarkRed;
                    output.Write($"Property - {property.Name}");
                    Console.ResetColor();
                    var typeName = GetPrettyTypeName(
                        typeof(IAspectPropertyValue).IsAssignableFrom(property.Type)
                            ? property.Type.GenericTypeArguments[0] 
                            : property.Type);
                    output.WriteLine($"[Category: {property.Category}] ({typeName})");
                }
            }
        }


        static void Main(string[] args)
        {
            // Use the command line args to get the resource key if present.
            // Otherwise, get it from the environment variable.
            string resourceKey = args.Length > 0 ? args[0] :
                ExampleUtils.GetResourceKeyFromEnv();

            // Configure a logger to output to the console.
            var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
            var logger = loggerFactory.CreateLogger<Program>();

            if (string.IsNullOrEmpty(resourceKey))
            {
                logger.LogError($"No resource key specified in the configuration file " +
                    $"'appsettings.json' or the environment variable " +
                    $"'{ExampleUtils.CLOUD_RESOURCE_KEY_ENV_VAR}'. The 51Degrees Cloud service is " +
                    $"accessed using a 'ResourceKey'. For more information see " +
                    $"https://51degrees.com/documentation/_info__resource_keys.html?utm_source=code&utm_medium=example&utm_campaign=ip-intelligence-dotnet-examples&utm_content=examples-cloud-metadata-console-program.cs&utm_term=resource-key-required. " +
                    $"A resource key with the properties required by this example can be " +
                    $"created for free at https://configure.51degrees.com/1QWJwHxl?utm_source=code&utm_medium=example&utm_campaign=ip-intelligence-dotnet-examples&utm_content=examples-cloud-metadata-console-program.cs&utm_term=resource-key-required. " +
                    $"Once complete, supply the resource key as a command line argument or via " +
                    $"the environment variable mentioned at the start of this message.");
            }
            else
            {
                new Example().Run(resourceKey, loggerFactory, Console.Out);
            }

            // Dispose the logger to ensure any messages get flushed
            loggerFactory.Dispose();
        }

        private static void AppendPrettyTypeName(Type type, StringBuilder typeNameBuilder)
        {
            if (!type.IsGenericType)
            {
                typeNameBuilder.Append(type.Name);
                return;
            }

            var typeName = type.Name;
            var genericTypeName = typeName.Substring(0, typeName.IndexOf('`'));
            typeNameBuilder.Append(genericTypeName);
            var genericArguments = type.GetGenericArguments();
            typeNameBuilder.Append('<');
            for (int i = 0; i < genericArguments.Length; i++)
            {
                if (i > 0)
                    typeNameBuilder.Append(", ");
                AppendPrettyTypeName(genericArguments[i], typeNameBuilder);
            }
            typeNameBuilder.Append('>');
        }

        private static string GetPrettyTypeName(Type type)
        {
            var typeNameBuilder = new StringBuilder();
            AppendPrettyTypeName(type, typeNameBuilder);
            return typeNameBuilder.ToString();
        }
    }
}