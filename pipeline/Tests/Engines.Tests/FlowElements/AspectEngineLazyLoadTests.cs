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
using FiftyOne.Pipeline.Core.TypedMap;
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.TestHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Engines.Tests.FlowElements
{
    [TestClass]
    // These tests depend on timing, so run them on their own.
    [DoNotParallelize]
    public class AspectEngineLazyLoadTests
    {
        private EmptyEngine _engine;

        private TestLoggerFactory _loggerFactory;
#if DEBUG
        private int _timeoutMS = 3000;
#else
        private int _timeoutMS = 2000;
#endif
        private CancellationTokenSource _cancellationTokenSource = 
            new CancellationTokenSource();

        [TestInitialize]
        public void Init()
        {
            _loggerFactory = new TestLoggerFactory();
            _engine = new EmptyEngineBuilder(_loggerFactory)
                .SetLazyLoading(new LazyLoadingConfiguration(_timeoutMS, 
                    _cancellationTokenSource.Token))
                .Build();
        }

        [TestCleanup]
        public void Cleanup()
        {
            for(int i = 0, n = _loggerFactory.Loggers.Count; i < n; ++i)
            {
                foreach (var entry in _loggerFactory.Loggers[i].ExtendedEntries)
                {
                    Console.WriteLine($"[LOGGER {i} LOGS] [{entry.Timestamp:O}] {entry.LogLevel} > {entry.Message} | {entry.Exception}");
                }
            }
            // Check that no errors or warnings were logged.
            foreach (var logger in _loggerFactory.Loggers)
            {
                logger.AssertMaxErrors(0);
                logger.AssertMaxWarnings(0);
            }
        }

        /// <summary>
        /// Check that the deprecated lazy loading option has no effect:
        /// the process method returns once processing has finished, and
        /// the value is there to read.
        /// </summary>
        [TestMethod]
        public void AspectEngineLazyLoad_Process()
        {
            // Arrange
            // Set the process time to half of the configured timeout
            _engine.SetProcessCost(TimeSpan.TicksPerMillisecond * (_timeoutMS / 2));

            // Act
            var evidence = new Dictionary<string, object>()
            {
                { "user-agent", "1234" }
            };
            var mockData = MockFlowData.CreateFromEvidence(evidence, false);
            // Use the mock flow data to populate this variable with the 
            // engine data from the call to process.
            EmptyEngineData engineData = null;
            mockData.Setup(d => d.GetOrAdd(It.IsAny<ITypedKey<EmptyEngineData>>(),
                It.IsAny<Func<IPipeline, EmptyEngineData>>()))
                .Callback((ITypedKey<EmptyEngineData> k, Func<IPipeline, EmptyEngineData> f) =>
                {
                    engineData = f(mockData.Object.Pipeline);
                })
                .Returns((ITypedKey<EmptyEngineData> k, Func<IPipeline, EmptyEngineData> f) =>
                {
                    return engineData;
                });
            var data = mockData.Object;

            // Process the data.
            _engine.Process(data);
            // Check that the task is complete when process returns.
            bool processReturnedAfterTaskComplete =
                engineData.ProcessTask.IsCompleted == true;

            // Assert
            Assert.AreEqual(2, engineData.ValueTwo);
            // Check that the task is now complete (because the code to get
            // the property value will wait until it is complete)
            bool valueReturnedAfterTaskComplete =
                engineData.ProcessTask.IsCompleted == true;
            Assert.IsTrue(processReturnedAfterTaskComplete);
            Assert.IsTrue(valueReturnedAfterTaskComplete);
        }

        /// <summary>
        /// Check that with the deprecated lazy loading option, which has no
        /// effect, a property whose processing
        /// takes longer than the timeout is read with no timeout.
        /// </summary>
        [TestMethod]
        public void AspectEngineLazyLoad_PropertyTimeout()
        {
            // Arrange
            // Set the process time to double the configured timeout
            _engine.SetProcessCost(TimeSpan.TicksPerMillisecond * (_timeoutMS * 2));

            // Act
            var evidence = new Dictionary<string, object>()
            {
                { "user-agent", "1234" }
            };
            // Use the mock flow data to populate this variable with the
            // engine data from the call to process.
            var mockData = MockFlowData.CreateFromEvidence(evidence, false);
            EmptyEngineData engineData = null;
            mockData.Setup(d => d.GetOrAdd(It.IsAny<ITypedKey<EmptyEngineData>>(),
                It.IsAny<Func<IPipeline, EmptyEngineData>>()))
                .Callback((ITypedKey<EmptyEngineData> k, Func<IPipeline, EmptyEngineData> f) =>
                {
                    engineData = f(mockData.Object.Pipeline);
                })
                .Returns((ITypedKey<EmptyEngineData> k, Func<IPipeline, EmptyEngineData> f) =>
                {
                    return engineData;
                });
            var data = mockData.Object;

            // Process the data
            _engine.Process(data);
            // Lazy loading has no effect, so processing has finished and
            // the value is read with no timeout.
            Assert.AreEqual(2, engineData.ValueTwo);
        }

        /// <summary>
        /// Check that a lazy loaded property that takes longer than the
        /// timeout is read with no timeout when the lazy loading
        /// configuration has no cancellation token.
        /// </summary>
        [TestMethod]
        public void AspectEngineLazyLoad_PropertyTimeoutWithoutToken()
        {
            // Arrange
            // An engine whose lazy loading has a timeout but no
            // cancellation token, with the process time set to double the
            // timeout.
            var engine = new EmptyEngineBuilder(_loggerFactory)
                .SetLazyLoading(new LazyLoadingConfiguration(_timeoutMS))
                .Build();
            engine.SetProcessCost(TimeSpan.TicksPerMillisecond * (_timeoutMS * 2));

            // Act
            var evidence = new Dictionary<string, object>()
            {
                { "user-agent", "1234" }
            };
            // Use the mock flow data to populate this variable with the
            // engine data from the call to process.
            var mockData = MockFlowData.CreateFromEvidence(evidence, false);
            EmptyEngineData engineData = null;
            mockData.Setup(d => d.GetOrAdd(It.IsAny<ITypedKey<EmptyEngineData>>(),
                It.IsAny<Func<IPipeline, EmptyEngineData>>()))
                .Callback((ITypedKey<EmptyEngineData> k, Func<IPipeline, EmptyEngineData> f) =>
                {
                    engineData = f(mockData.Object.Pipeline);
                })
                .Returns((ITypedKey<EmptyEngineData> k, Func<IPipeline, EmptyEngineData> f) =>
                {
                    return engineData;
                });
            var data = mockData.Object;

            // Process the data
            engine.Process(data);
            // Lazy loading has no effect, so processing has finished and
            // the value is read with no timeout, and not fail on the absent
            // cancellation token.
            Assert.AreEqual(2, engineData.ValueTwo);
        }

        /// <summary>
        /// Check that cancelling the token once Process has returned
        /// leaves the value readable, because lazy loading has no effect
        /// and processing has finished.
        /// </summary>
        [TestMethod]
        public void AspectEngineLazyLoad_ProcessCancelled()
        {
            // Arrange
            // Set the process time to the configured timeout
            _engine.SetProcessCost(TimeSpan.TicksPerMillisecond * _timeoutMS);

            // Act
            var evidence = new Dictionary<string, object>()
            {
                { "user-agent", "1234" }
            };
            // Use the mock flow data to populate this variable with the
            // engine data from the call to process.
            var mockData = MockFlowData.CreateFromEvidence(evidence, false);
            EmptyEngineData engineData = null;
            mockData.Setup(d => d.GetOrAdd(It.IsAny<ITypedKey<EmptyEngineData>>(),
                It.IsAny<Func<IPipeline, EmptyEngineData>>()))
                .Callback((ITypedKey<EmptyEngineData> k, Func<IPipeline, EmptyEngineData> f) =>
                {
                    engineData = f(mockData.Object.Pipeline);
                })
                .Returns((ITypedKey<EmptyEngineData> k, Func<IPipeline, EmptyEngineData> f) =>
                {
                    return engineData;
                });
            var data = mockData.Object;

            // Process the data
            _engine.Process(data);

            // Ideally, start a new task that will wait a short time and
            // then trigger the cancellation token.
            // If we've only got one core to work with then this approach
            // can cause the test to fail as the cancellation task may
            // not get run in time.
            // If we only have one core then just trigger cancellation
            // up-front.
            if (Environment.ProcessorCount > 1)
            {
                Task.Run(() =>
                {
                    Task.Delay(_timeoutMS / 10);
                    _cancellationTokenSource.Cancel();
                }).Wait();
            }
            else
            {
                _cancellationTokenSource.Cancel();
            }

            // Lazy loading has no effect, so processing finished before
            // the token was cancelled, and the value is read.
            Assert.AreEqual(2, engineData.ValueTwo);
            // This assert can help work out what is happening if the test fails.
            Assert.IsTrue(_cancellationTokenSource.IsCancellationRequested);
        }

        /// <summary>
        /// Check that throwing an exception from the engine's process method
        /// comes from processing, because lazy loading has no effect.
        /// </summary>
        [TestMethod]
        public void AspectEngineLazyLoad_ProcessErrored()
        {
            // Arrange
            // Set the engine to throw an exception while processing
            var exceptionMessage = "an exception message";
            _engine.SetException(new Exception(exceptionMessage));
            // Act
            var evidence = new Dictionary<string, object>()
            {
                { "user-agent", "1234" }
            };
            // Use the mock flow data to populate this variable with the 
            // engine data from the call to process.
            var mockData = MockFlowData.CreateFromEvidence(evidence, false);
            EmptyEngineData engineData = null;
            mockData.Setup(d => d.GetOrAdd(It.IsAny<ITypedKey<EmptyEngineData>>(),
                It.IsAny<Func<IPipeline, EmptyEngineData>>()))
                .Callback((ITypedKey<EmptyEngineData> k, Func<IPipeline, EmptyEngineData> f) =>
                {
                    engineData = f(mockData.Object.Pipeline);
                })
                .Returns((ITypedKey<EmptyEngineData> k, Func<IPipeline, EmptyEngineData> f) =>
                {
                    return engineData;
                });
            var data = mockData.Object;

            // Process the data. Lazy loading has no effect, so the
            // engine's exception comes from processing.
            var e = Assert.ThrowsExactly<Exception>(
                () => _engine.Process(data));
            Assert.AreEqual(exceptionMessage, e.Message);
        }

        /// <summary>
        /// Check that throwing an exception from more than one engine's
        /// process method comes from each engine's processing, because
        /// lazy loading has no effect.
        /// </summary>
        [TestMethod]
        public void AspectEngineLazyLoad_ProcessMultipleErrored()
        {
            // Arrange
            // Set the engine to throw an exception while processing
            var exceptionMessage = "an exception message";
            var engine2 = new EmptyEngineBuilder(_loggerFactory)
                .SetLazyLoading(new LazyLoadingConfiguration(_timeoutMS,
                    _cancellationTokenSource.Token))
                .Build();
            _engine.SetException(new Exception(exceptionMessage));
            engine2.SetException(new Exception(exceptionMessage));
            // Act
            var evidence = new Dictionary<string, object>()
            {
                { "user-agent", "1234" }
            };
            // Use the mock flow data to populate this variable with the 
            // engine data from the call to process.
            var mockData = MockFlowData.CreateFromEvidence(evidence, false);
            EmptyEngineData engineData = null;
            mockData.Setup(d => d.GetOrAdd(It.IsAny<ITypedKey<EmptyEngineData>>(),
                It.IsAny<Func<IPipeline, EmptyEngineData>>()))
                .Callback((ITypedKey<EmptyEngineData> k, Func<IPipeline, EmptyEngineData> f) =>
                {
                    if (engineData == null)
                    {
                        engineData = f(mockData.Object.Pipeline);
                    }
                })
                .Returns((ITypedKey<EmptyEngineData> k, Func<IPipeline, EmptyEngineData> f) =>
                {
                    return engineData;
                });
            var data = mockData.Object;

            // Process the data twice. Lazy loading has no effect, so each
            // engine's exception comes from its own processing.
            var first = Assert.ThrowsExactly<Exception>(
                () => _engine.Process(data));
            var second = Assert.ThrowsExactly<Exception>(
                () => engine2.Process(data));
            Assert.AreEqual(exceptionMessage, first.Message);
            Assert.AreEqual(exceptionMessage, second.Message);
        }



        /// <summary>
        /// Test that accessing results from an engine with Lazy Loading 
        /// enabled works as expected.
        /// <list type="number">
        /// <item>
        /// Lazy loading is deprecated and has no effect, so the call to
        /// <c>flowData.Process()</c> takes the configured 'process cost'
        /// of the dummy engine.
        /// </item>
        /// <item>
        /// Processing has finished, so 'ValueOne' is accessible almost
        /// immediately after Process returns.
        /// </item>
        /// <item>
        /// As 'ValueTwo' is populated after the dummy engine has
        /// finished waiting, it's value should not be accessible 
        /// until after the configured 'process cost' time has passed.
        /// </item>
        /// </list>
        /// </summary>
        [TestMethod]
        public void AspectEngineLazyLoad_Itegrated()
        {
            // Arrange
            // Set the process time to half of the configured timeout
            var processCostMs = _timeoutMS / 2;
            _engine.SetProcessCost(TimeSpan.TicksPerMillisecond * processCostMs);
            var pipeline = new PipelineBuilder(_loggerFactory)
                .AddFlowElement(_engine)
                .Build();

            // Act
            var stopwatch = new Stopwatch();
            long gotDataTimeMs;
            long processStartedTimeMs = -1;
            long valueOneTimeMs;
            long valueTwoTimeMs;
            using (var flowData = pipeline.CreateFlowData())
            {
                var logsEvents = new List<Action>();
                _engine.OnProcessEngineEntered += () =>
                {
                    processStartedTimeMs = stopwatch.ElapsedMilliseconds;
                    logsEvents.Add(() => Trace.WriteLine(
                        $"{nameof(_engine.OnProcessEngineEntered)} triggerred at {processStartedTimeMs} ms"));
                };
                var didSetValueOne = new ManualResetEventSlim(false);
                _engine.OnWillDelayProcessEngine += didSetValueOne.Set;
                Trace.WriteLine("Process starting");
                stopwatch.Start();
                flowData.Process();
                long processTimeMs = stopwatch.ElapsedMilliseconds;
                try
                {
                    logsEvents.Add(() => Trace.WriteLine($"Process complete in {processTimeMs} ms"));
                    Assert.IsGreaterThanOrEqualTo(processCostMs / 2,
processTimeMs, $"Process should have waited for processing, at " +
                        $"least {processCostMs / 2} ms, but it took " +
                        $"{processTimeMs} ms.");

                    // Assert
                    var data = flowData.Get<EmptyEngineData>();
                    gotDataTimeMs = stopwatch.ElapsedMilliseconds;
                    logsEvents.Add(() => Trace.WriteLine($"Got data at {gotDataTimeMs} ms"));

                    Assert.IsNotNull(data);
                    didSetValueOne.Wait(processCostMs); // wait for ValueOne to actually be set, to prevent delay on access

                    Assert.AreEqual(1, data.ValueOne);
                    valueOneTimeMs = stopwatch.ElapsedMilliseconds;
                    logsEvents.Add(() => Trace.WriteLine($"Value one accessed at {valueOneTimeMs} ms"));
                    Assert.AreEqual(2, data.ValueTwo);
                    valueTwoTimeMs = stopwatch.ElapsedMilliseconds;
                    logsEvents.Add(() => Trace.WriteLine($"Value two accessed at {valueTwoTimeMs} ms"));
                } 
                finally
                {
                    foreach(var logEvent in logsEvents)
                    {
                        logEvent();
                    }
                }

                Assert.IsGreaterThanOrEqualTo(
0,
                    processStartedTimeMs, $"{nameof(processStartedTimeMs)} should be non-negative, got {processStartedTimeMs}");
                Assert.IsLessThan(
processCostMs,
                    processStartedTimeMs, $"{nameof(processStartedTimeMs)} is not within {nameof(processCostMs)}: {processStartedTimeMs} vs {processCostMs}");

                Assert.IsLessThan(processCostMs,
valueOneTimeMs - processTimeMs, $"Accessing value one should have taken less than " +
                    $"{processCostMs} ms from the time the Process method " +
                    $"returned but it took {valueOneTimeMs - processTimeMs} ms.");

                // Note - this should really take at least 'processCostMs'
                // but the accuracy of the timer seems to cause issues
                // if we are being that exact.
                Assert.IsGreaterThanOrEqualTo(processCostMs / 2,
valueTwoTimeMs, $"Accessing value two should have taken at least " +
                    $"{processCostMs / 2} ms from the time the Process method" +
                    $"was called but it only took {valueTwoTimeMs} ms.");
            }
        }

        /// <summary>
        /// Test that accessing results using the 'AsDictionary' method
        /// from an engine with Lazy Loading enabled works as expected.
        /// The call to 'AsDictionary' should wait until processing 
        /// is complete before returning and all expected properties
        /// should be present in the dictionary.
        /// </summary>
        [TestMethod]
        public void AspectEngineLazyLoad_Itegrated_AsDictionary()
        {
            // Arrange
            var processCostMs = _timeoutMS / 2;
            _engine.SetProcessCost(TimeSpan.TicksPerMillisecond * processCostMs);
            var pipeline = new PipelineBuilder(_loggerFactory)
                .AddFlowElement(_engine)
                .Build();

            // Act
            var stopwatch = new Stopwatch();
            using (var flowData = pipeline.CreateFlowData())
            {
                Trace.WriteLine("Process starting");
                stopwatch.Start();
                flowData.Process();
                long processTimeMs = stopwatch.ElapsedMilliseconds;
                Trace.WriteLine($"Process complete in {processTimeMs} ms");

                // Assert
                var data = flowData.Get<EmptyEngineData>();
                Assert.IsNotNull(data);
                var dictionary = data.AsDictionary();
                long dictTimeMs = stopwatch.ElapsedMilliseconds;
                Trace.WriteLine($"Dictionary retrieved after {dictTimeMs} ms");
                Assert.AreEqual(1, dictionary[EmptyEngineData.VALUE_ONE_KEY]);
                Assert.AreEqual(2, dictionary[EmptyEngineData.VALUE_TWO_KEY]);

                // Note - this should really take at least 'processCostMs'
                // but the accuracy of the timer seems to cause issues
                // if we are being that exact.
                Assert.IsGreaterThan(processCostMs / 2,
dictTimeMs, $"Accessing the dictionary should have taken at least " +
                    $"{processCostMs / 2} ms from the time the Process method" +
                    $"was called but it only took {dictTimeMs} ms.");
            }
        }
    }
}
