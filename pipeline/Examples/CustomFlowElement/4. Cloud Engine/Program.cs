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
            //! [usage]
            // Construct a cloud request engine using the example endpoint for
            // the example engine to use. This endpoint is the star sign
            // example service built for this tutorial, not the 51Degrees
            // cloud, so it is fixed here. A cloud engine for a 51Degrees
            // product leaves the endpoint unset, and the builder then reads
            // it from _51DEGREES_CLOUD_ENDPOINT, defaulting to
            // https://cloud.51degrees.com/api/v4/. A host other than
            // cloud.51degrees.com would be used to (a) use an on premise
            // web server, or (b) use a privately hosted version of the
            // 51Degrees cloud for performance reasons. This is the private
            // hosting option of the cloud service. Both run the same
            // service, so such an engine works unchanged against either.
            var cloudRequestEngine =
                new CloudRequestEngineBuilder(_loggerFactory, new HttpClient())
                .SetEndPoint("https://51degrees.com/starsign/api/")
                .SetResourceKey("cloudexample")
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
            Console.WriteLine($"With a date of birth of {dob}, " +
                $"your star sign is {flowData.Get<IStarSignData>().StarSign}.");
            //! [usage]
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
