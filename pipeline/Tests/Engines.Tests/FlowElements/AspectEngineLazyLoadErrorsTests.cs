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

using FiftyOne.Common.TestHelpers;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.TestHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;

namespace FiftyOne.Pipeline.Engines.Tests.FlowElements
{
    /// <summary>
    /// The deprecated lazy loading option processes an engine in a
    /// background task while the Pipeline goes on, so errors can be added
    /// from two threads at once until that task ends.
    /// </summary>
    [TestClass]
    public class AspectEngineLazyLoadErrorsTests
    {
        private const int ErrorsPerThread = 100000;

        private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

        [TestMethod]
        public void LazyTaskAndPipelineThread_AddErrorsAtOnce_NoneLost()
        {
            var loggerFactory = new TestLoggerFactory();
            using (var engine = new EmptyEngineBuilder(loggerFactory)
                .SetLazyLoading(new LazyLoadingConfiguration(60000))
                .Build())
            using (var pipeline = new PipelineBuilder(loggerFactory)
                .AddFlowElement(engine)
                .Build())
            using (var start = new Barrier(2))
            using (var flowData = pipeline.CreateFlowData())
            {
                // The background task adds its errors as soon as the
                // Pipeline thread is ready to add its own.
                engine.OnProcessEngineEntered += () =>
                {
                    Assert.IsTrue(start.SignalAndWait(Wait),
                        "The Pipeline thread did not start.");
                    AddErrors(flowData);
                };

                flowData.Process();
                Assert.IsTrue(start.SignalAndWait(Wait),
                    "The lazy loading task did not start.");
                AddErrors(flowData);
                Assert.IsTrue(flowData.Get<EmptyEngineData>()
                    .ProcessTask.Wait(Wait),
                    "The lazy loading task did not finish.");

                Assert.AreEqual(ErrorsPerThread * 2, flowData.Errors.Count,
                    "Errors were lost when added from two threads at once.");
            }
        }

        private static void AddErrors(IFlowData flowData)
        {
            var error = new Exception("Test error");
            for (int i = 0; i < ErrorsPerThread; i++)
            {
                flowData.AddError(error, null, false, false);
            }
        }
    }
}
