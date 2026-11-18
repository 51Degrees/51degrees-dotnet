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
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.TestHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;

namespace FiftyOne.Pipeline.Engines.Tests.FlowElements
{
    /// <summary>
    /// The deprecated lazy loading option has no effect. An Engine
    /// processes on the thread that processes the Flow Data, whether the
    /// option is set or not. See lazy-loading.md and rule 5 of
    /// thread-safety.md in the specification.
    /// </summary>
    [TestClass]
    public class AspectEngineLazyLoadingOptionTests
    {
        private const int TimeoutMs = 10000;

        private const int ProcessCostMs = 200;

        /// <summary>
        /// The engine has finished, on the calling thread, before Process
        /// returns.
        /// </summary>
        [TestMethod]
        public void LazyLoadingSet_EngineFinishesBeforeProcessReturns()
        {
            using var engine = new EmptyEngineBuilder(new TestLoggerFactory())
                .SetLazyLoading(new LazyLoadingConfiguration(TimeoutMs))
                .Build();
            engine.SetProcessCost(
                TimeSpan.TicksPerMillisecond * ProcessCostMs);
            int? processThread = null;
            engine.OnProcessEngineEntered += () =>
                processThread = Environment.CurrentManagedThreadId;
            using var pipeline = new PipelineBuilder(new TestLoggerFactory())
                .AddFlowElement(engine)
                .Build();
            using var data = pipeline.CreateFlowData();

            data.Process();

            var engineData = data.GetFromElement(engine);
            Assert.IsTrue(engineData.ProcessTask.IsCompleted,
                "Processing must have finished when Process returns.");
            Assert.AreEqual(Environment.CurrentManagedThreadId,
                processThread,
                "The engine must process on the calling thread.");
            Assert.AreEqual(2, engineData.ValueTwo);
        }

        /// <summary>
        /// An exception the engine throws while processing reaches the
        /// Pipeline as an error of the engine's processing, as it does
        /// with no lazy loading, and not a later reader.
        /// </summary>
        [TestMethod]
        public void LazyLoadingSet_EngineErrorRecordedWhileProcessing()
        {
            using var engine = new EmptyEngineBuilder(new TestLoggerFactory())
                .SetLazyLoading(new LazyLoadingConfiguration(TimeoutMs))
                .Build();
            engine.SetException(new InvalidOperationException("failed"));
            using var pipeline = new PipelineBuilder(new TestLoggerFactory())
                .SetSuppressProcessExceptions(true)
                .AddFlowElement(engine)
                .Build();
            using var data = pipeline.CreateFlowData();

            data.Process();

            Assert.IsNotNull(data.Errors);
            Assert.HasCount(1, data.Errors);
            Assert.AreSame(engine, data.Errors[0].FlowElement);
            Assert.IsInstanceOfType<InvalidOperationException>(
                data.Errors[0].ExceptionData);
        }
    }
}
