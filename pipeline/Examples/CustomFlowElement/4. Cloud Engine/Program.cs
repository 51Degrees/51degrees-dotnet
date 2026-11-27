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

using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines;
using Examples.CloudEngine.Data;
using Examples.CloudEngine.FlowElements;
using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace Examples.CloudEngine
{
    public class Program
    {
        private static ILoggerFactory _loggerFactory = new LoggerFactory();

        public void RunExample()
        {
            var resourceKey = GetResourceKey();
            if (resourceKey == null)
            {
                // There is no Resource Key, so the message says where to
                // create one and the example ends.
                Console.WriteLine(
                    "No Resource Key is set in the environment variable " +
                    "'_51DEGREES_RESOURCE_KEY'. The cloud request engine " +
                    "sends its requests to the cloud service with a " +
                    "Resource Key, and one can be created at " +
                    "https://51degrees.com/configure?utm_source=code&utm_medium=example&utm_campaign=51degrees-dotnet&utm_content=examples-customflowelement-4.-cloud-engine-program.cs&utm_term=resource-key-required " +
                    "and set in that variable.");
                return;
            }
            var endpoint = GetCloudEndpoint();

            //! [usage]
            // Construct a cloud request engine for the example engine to
            // use. The cloud service to call is read from
            // _51DEGREES_CLOUD_ENDPOINT and defaults to
            // https://cloud.51degrees.com/api/v4/. A host other than
            // cloud.51degrees.com would be used to (a) use an on premise
            // web server, or (b) use a privately hosted version of the
            // 51Degrees cloud for performance reasons. This is the private
            // hosting option of the cloud service. Both run the same
            // service, so this example works unchanged against either. The
            // Resource Key is read from the _51DEGREES_RESOURCE_KEY
            // environment variable.
            var cloudRequestEngine =
                new CloudRequestEngineBuilder(_loggerFactory, new HttpClient())
                .SetEndPoint(endpoint)
                .SetResourceKey(resourceKey)
                // The default of 2 seconds is short for a first request from
                // a machine that has not yet connected to the endpoint.
                .SetTimeOutSeconds(10)
                .Build();
            // Construct the example engine.
            var starSignEngine =
                new SimpleCloudEngineBuilder(_loggerFactory, cloudRequestEngine)
                .Build();
            // Construct the pipeline with the example engine and the cloud
            // request engine it requires.
            var pipeline = new PipelineBuilder(_loggerFactory)
                .AddFlowElement(cloudRequestEngine)
                .AddFlowElement(starSignEngine)
                .Build();

            var dob = "18/12/1992";
            // Create a new flow data.
            var flowData = pipeline.CreateFlowData();
            // Add the evidence and process the data.
            flowData
                .AddEvidence("date-of-birth", dob)
                .Process();
            // Now get the result of the processing.
            string starSign = null;
            try
            {
                starSign = flowData.Get<IStarSignData>().StarSign;
            }
            catch (PropertyMissingException)
            {
                // The service gave no star sign, which the check below
                // reports.
            }
            if (starSign == null)
            {
                // The service answered but offers no star sign product,
                // which is what the 51Degrees cloud says, because star
                // signs are only served by the example service built for
                // this tutorial. The connection, the property and evidence
                // key negotiation and the request itself all worked.
                Console.WriteLine($"The cloud service at {endpoint} does " +
                    "not offer the star sign product, so no star sign is " +
                    $"available for a date of birth of {dob}.");
            }
            else
            {
                Console.WriteLine($"With a date of birth of {dob}, " +
                    $"your star sign is {starSign}.");
            }
            //! [usage]
        }

        /// <summary>
        /// Get the cloud service to call. The _51DEGREES_CLOUD_ENDPOINT
        /// environment variable names the API base including the /api/v4/
        /// segment, which is the same variable the CloudRequestEngineBuilder
        /// honours when no endpoint is set, and defaults to the public cloud
        /// at https://cloud.51degrees.com/api/v4/. The value is normalised
        /// to end in one slash because the builder appends the endpoint
        /// names to it.
        /// </summary>
        private static string GetCloudEndpoint()
        {
            var endpoint = Environment.GetEnvironmentVariable(
                "_51DEGREES_CLOUD_ENDPOINT");
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                endpoint = "https://cloud.51degrees.com/api/v4/";
            }
            return endpoint.TrimEnd('/') + "/";
        }

        /// <summary>
        /// Get the Resource Key to use for this example, from the
        /// _51DEGREES_RESOURCE_KEY environment variable.
        /// </summary>
        /// <returns>
        /// The key, or null when it is not set.
        /// </returns>
        private static string GetResourceKey()
        {
            var resourceKey =
                Environment.GetEnvironmentVariable("_51DEGREES_RESOURCE_KEY");
            return string.IsNullOrWhiteSpace(resourceKey) ? null : resourceKey;
        }

        static void Main(string[] args)
        {
            var instance = new Program();
            instance.RunExample();
            WriteFindOutMore();

            Console.WriteLine("==========================================");
            Console.WriteLine("Example complete.");
            // Only wait for a key where there is a keyboard to press one
            // on, so the example also runs to completion from a script.
            if (Console.IsInputRedirected == false)
            {
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// Print where to find out more, so that anyone this example is
        /// shown to can follow up from the output itself.
        /// </summary>
        private static void WriteFindOutMore()
        {
            Console.WriteLine("==========================================");
            Console.WriteLine("Find out more");
            Console.WriteLine(
                "  How a pipeline works, and where an engine fits:");
            Console.WriteLine(
                "    https://51degrees.com/documentation/_pipeline_api__concepts__pipeline_basics.html" +
                "?utm_source=code&utm_medium=example" +
                "&utm_campaign=51degrees-dotnet" +
                "&utm_content=examples-customflowelement-4.-cloud-engine-program.cs" +
                "&utm_term=find-out-more");
            Console.WriteLine(
                "  Resource keys, which the cloud request engine sends " +
                "to the 51Degrees cloud service:");
            Console.WriteLine(
                "    https://51degrees.com/documentation/_services__cloud__resource_keys.html" +
                "?utm_source=code&utm_medium=example" +
                "&utm_campaign=51degrees-dotnet" +
                "&utm_content=examples-customflowelement-4.-cloud-engine-program.cs" +
                "&utm_term=find-out-more");
            Console.WriteLine("  The 51Degrees documentation:");
            Console.WriteLine(
                "    https://51degrees.com/documentation" +
                "?utm_source=code&utm_medium=example" +
                "&utm_campaign=51degrees-dotnet" +
                "&utm_content=examples-customflowelement-4.-cloud-engine-program.cs" +
                "&utm_term=find-out-more");
            Console.WriteLine(
                "  Talk to 51Degrees about using the cloud service:");
            Console.WriteLine(
                "    https://51degrees.com/contact-us" +
                "?utm_source=code&utm_medium=example" +
                "&utm_campaign=51degrees-dotnet" +
                "&utm_content=examples-customflowelement-4.-cloud-engine-program.cs" +
                "&utm_term=find-out-more");
            Console.WriteLine(
                "  This repository, where the pipeline, the cloud request " +
                "engine and this example live:");
            Console.WriteLine(
                "    https://github.com/51Degrees/51degrees-dotnet/tree/main/pipeline");
            Console.WriteLine(
                "  The device detection engine, which has a cloud engine " +
                "that works the same way:");
            Console.WriteLine(
                "    https://github.com/51Degrees/51degrees-dotnet/tree/main/device-detection");
        }
    }
}
