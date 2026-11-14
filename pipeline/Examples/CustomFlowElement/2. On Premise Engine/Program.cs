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
using Microsoft.Extensions.Logging;
using Examples.OnPremiseEngine.Data;
using Examples.OnPremiseEngine.FlowElements;
using System;

namespace Examples.OnPremiseEngine
{
    public class Program 
    {
        private static ILoggerFactory _loggerFactory = new LoggerFactory();

        public void RunExample()
        {
            //! [usage]
            // Construct the engine using the data file. Let's disable auto
            // updates as we have not implemented it in this example engine.
            var starSignEngine = new SimpleOnPremiseEngineBuilder(_loggerFactory, null)
                .SetAutoUpdate(false)
                .SetDataFileSystemWatcher(false)
                .Build("starsigns.csv", false);
            // Construct the pipeline with the example engine. Disposing the
            // pipeline also disposes the engine.
            using var pipeline = new PipelineBuilder(_loggerFactory)
                .AddFlowElement(starSignEngine)
                .Build();

            var dob = new DateTime(1992, 12, 18);
            // Create a new flow data.
            using (var flowData = pipeline.CreateFlowData())
            {
                // Add the evidence and process the data.
                flowData
                    .AddEvidence("date-of-birth", dob)
                    .Process();
                // Now get the result of the processing.
                Console.WriteLine($"With a date of birth of {dob}, " +
                    $"your star sign is {flowData.Get<IStarSignData>().StarSign}.");
            }
            //! [usage]
        }

        static void Main(string[] args)
        {
            var instance = new Program();
            instance.RunExample();
            WriteFindOutMore();

            Console.WriteLine("==========================================");
            Console.WriteLine("Example complete. Press any key to exit.");
            // Wait for user to press a key.
            Console.ReadKey();
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
                "&utm_content=examples-customflowelement-2.-on-premise-engine-program.cs" +
                "&utm_term=find-out-more");
            Console.WriteLine("  The 51Degrees documentation:");
            Console.WriteLine(
                "    https://51degrees.com/documentation" +
                "?utm_source=code&utm_medium=example" +
                "&utm_campaign=51degrees-dotnet" +
                "&utm_content=examples-customflowelement-2.-on-premise-engine-program.cs" +
                "&utm_term=find-out-more");
            Console.WriteLine(
                "  Talk to 51Degrees about using the pipeline:");
            Console.WriteLine(
                "    https://51degrees.com/contact-us" +
                "?utm_source=code&utm_medium=example" +
                "&utm_campaign=51degrees-dotnet" +
                "&utm_content=examples-customflowelement-2.-on-premise-engine-program.cs" +
                "&utm_term=find-out-more");
            Console.WriteLine(
                "  This repository, where the pipeline and this example " +
                "live:");
            Console.WriteLine(
                "    https://github.com/51Degrees/51degrees-dotnet/tree/main/pipeline");
            Console.WriteLine(
                "  The device detection engine, which is an on-premise " +
                "engine that reads a data file:");
            Console.WriteLine(
                "    https://github.com/51Degrees/51degrees-dotnet/tree/main/device-detection");
        }
    }
}
