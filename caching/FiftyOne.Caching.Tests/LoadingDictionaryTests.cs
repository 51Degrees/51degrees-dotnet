/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2025 51 Degrees Mobile Experts Limited, Davidson House,
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

using FiftyOne.Caching.Tests.Loaders;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Caching.Tests
{
    [TestClass]
    public class LoadingDictionaryTests
    {
        private CancellationTokenSource _token;

        private Mock<ILogger<LoadingDictionary<string, string>>> _logger;

        [TestInitialize]
        public void Init()
        {
            _token = new CancellationTokenSource();
            _logger = new Mock<ILogger<LoadingDictionary<string, string>>>();
        }

        /// <summary>
        /// Test that using the get method on an empty dictionary result in
        /// the load method being called, and the expected value being returned.
        /// </summary>
        [TestMethod]
        public void LoadingDictionary_GetMiss()
        {
            // Arrange

            var value = "teststring";
            var loader = new ReturnKeyLoader<string>();
            var dict = new LoadingDictionary<string, string>(_logger.Object, loader);

            // Act

            var result = dict[value, _token.Token];

            // Assert

            Assert.IsNotNull(result);
            Assert.AreEqual(value, result);
            Assert.AreEqual(1, loader.Calls);
            Assert.AreEqual(1, loader.TaskCalls);
            Assert.AreEqual(1, dict.Keys.Count());
        }

        /// <summary>
        /// Test that using the TryGet method on an empty dictionary result in
        /// the load method being called, and the expected value being returned.
        /// </summary>
        [TestMethod]
        public void LoadingDictionary_TryGetMiss()
        {
            // Arrange

            var value = "teststring";
            var loader = new ReturnKeyLoader<string>();
            var dict = new LoadingDictionary<string, string>(_logger.Object, loader);

            // Act

            var success = dict.TryGet(value, _token.Token, out var result);

            // Assert

            Assert.IsTrue(success);
            Assert.IsNotNull(result);
            Assert.AreEqual(value, result);
            Assert.AreEqual(1, loader.Calls);
            Assert.AreEqual(1, loader.TaskCalls);
            Assert.AreEqual(1, dict.Keys.Count());
        }

        /// <summary>
        /// Test that using the get method on a dictionary that already contains
        /// a value for the key does not result in the load method being called,
        /// and the expected value being returned.
        /// </summary>
        [TestMethod]
        public void LoadingDictionary_GetHit()
        {
            // Arrange

            var value = "teststring";
            var loader = new ReturnKeyLoader<string>();
            var dict = new LoadingDictionary<string, string>(_logger.Object, loader);

            // Act

            var _ = dict[value, _token.Token];
            var result = dict[value, _token.Token];

            // Assert

            Assert.IsNotNull(result);
            Assert.AreEqual(value, result);
            Assert.AreEqual(1, loader.Calls);
            Assert.AreEqual(1, loader.TaskCalls);
            Assert.AreEqual(1, dict.Keys.Count());
        }

        /// <summary>
        /// Test that using the TryGet method on a dictionary that already contains
        /// a value for the key does not result in the load method being called,
        /// and the expected value being returned.
        /// </summary>
        [TestMethod]
        public void LoadingDictionary_TryGetHit()
        {
            // Arrange

            var value = "teststring";
            var loader = new ReturnKeyLoader<string>();
            var dict = new LoadingDictionary<string, string>(_logger.Object, loader);

            // Act

            _ = dict.TryGet(value, _token.Token, out _);
            var success = dict.TryGet(value, _token.Token, out var result);

            // Assert

            Assert.IsTrue(success);
            Assert.IsNotNull(result);
            Assert.AreEqual(value, result);
            Assert.AreEqual(1, loader.Calls);
            Assert.AreEqual(1, loader.TaskCalls);
            Assert.AreEqual(1, dict.Keys.Count());
        }

        /// <summary>
        /// Test that when multiple threads call the get method with the same
        /// key, where the value is not already loaded, that the load method
        /// is only called once. Also check that all the values returned are
        /// correct.
        /// </summary>
        [TestMethod]
        public void LoadingDictionary_GetConcurrentHits()
        {
            LoadingDictionary<string, string> dict = null;
            LoadingDictionary_GetConcurrentHits_Internal(loader => {
                dict = new LoadingDictionary<string, string>(_logger.Object, loader);
                return (k, t) => dict[k, t];
            }, _ => 1);
            Assert.AreEqual(1, dict.Keys.Count());
        }

        /// <summary>
        /// Test that when multiple threads call the get method with the same
        /// key, where the value is not already loaded, that the load method
        /// is the same amount of times. Proof of differences.
        /// </summary>
        [TestMethod]
        public void LoadingDictionary_GetConcurrentHits_PlainDict()
        {
            LoadingDictionary_GetConcurrentHits_Internal(loader => {
                var dict = new ConcurrentDictionary<string, string>();
                return (k, t) => dict.GetOrAdd(k, loader.Load(k, t).Result);
            }, n => n);
        }

        private void LoadingDictionary_GetConcurrentHits_Internal(
            Func<ReturnKeyLoader<string>, Func<string, CancellationToken, string>> getterBuilder,
            Func<int, int> expectedCallsCalculator)
        {
            // Arrange

            var value = "teststring";
            var loader = new ReturnKeyLoader<string>();
            var dictGetter = getterBuilder(loader);
            var results = new ConcurrentDictionary<int, string>();
            var count = 10;

            // Act

            Parallel.For(0, count, (i) =>
            {
                results[i] = dictGetter(value, _token.Token);
            });

            // Assert

            foreach (var result in results)
            {
                Assert.AreEqual(value, result.Value);
            }

            var expectedCalls = expectedCallsCalculator(count);
            Assert.AreEqual(expectedCalls, loader.Calls);
            Assert.AreEqual(expectedCalls, loader.TaskCalls);
        }

        [TestMethod]
        public void LoadingDictionary_GetConcurrentHits_MidLoad()
        {
            // Arrange

            const int loadTimeMS = 10;
            var value = "teststring";
            const int GATE_TIMEOUT_MS = 5000;
            var sourceForLoader = new CancellationTokenSource();
            Func<CancellationToken, CancellationToken> tokenOverride = _ => sourceForLoader.Token;
            var loader = new ReturnKeyLoader<string>(loadTimeMS, tokenOverride, tokenOverride);
            var dict = new LoadingDictionary<string, string>(_logger.Object, loader);
            var results = new ConcurrentDictionary<int, string>();

            // Act

            Func<string, Func<string>> BuildGetValueFunc = s =>
            {
                var t = _token.Token;
                return () =>
                {
                    Console.WriteLine($"[{s}] Trying to get value...");
                    try
                    {
                        var v = dict[value, t];
                        Console.WriteLine($"[{s}] Value is '{v}'");
                        return v;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[{s}] Failed to get value: {ex}");
                        throw;
                    }
                };
            };

            var firstSource = _token;
            Task<string> secondReaderTask = null;
            var firstTokenCancelled = new ManualResetEventSlim(false);
            loader.OnTaskStarted += _ => {
                Console.WriteLine("Loader task started.");
                var secondReaderAwoke = new ManualResetEventSlim(false);
                secondReaderTask = Task.Run(() =>
                {
                    Console.WriteLine("Second reader task started.");
                    secondReaderAwoke.Set();
                    _token = new CancellationTokenSource();
                    return BuildGetValueFunc("B")();
                });

                secondReaderAwoke.Wait(GATE_TIMEOUT_MS);
                Assert.IsTrue(
                    secondReaderAwoke.IsSet,
                    $"{nameof(secondReaderAwoke)} still not set.");

                firstSource.Cancel();
                firstTokenCancelled.Set();
                Console.WriteLine("First reader task cancelled.");
            };

            var firstCallTask = Task.Run(BuildGetValueFunc("A"));
            firstTokenCancelled.Wait(2 * GATE_TIMEOUT_MS);
            Assert.IsTrue(
                firstTokenCancelled.IsSet,
                $"{nameof(firstTokenCancelled)} still not set.");


            // Assert

            Assert.IsNotNull(secondReaderTask);
            Assert.ThrowsException<AggregateException>(() => firstCallTask.Result);
            Assert.AreEqual(value, secondReaderTask.Result);

            Assert.IsTrue(
                firstSource.IsCancellationRequested, 
                $"{nameof(firstSource)} was never cancelled!");

            Assert.AreEqual(1, loader.Calls);
            Assert.AreEqual(1, loader.TaskCalls);
        }

        /// <summary>
        /// Test that when multiple threads call the get method with different
        /// keys, where the values are not already loaded, all the values
        /// returned are correct.
        /// </summary>
        [TestMethod]
        public void LoadingDictionary_GetConcurrentMisses()
        {
            // Arrange

            var loader = new ReturnKeyLoader<string>();
            var dict = new LoadingDictionary<string, string>(_logger.Object, loader);
            var results = new ConcurrentDictionary<int, string>();
            var count = 10;

            // Act

            Parallel.For(0, count, (i) =>
            {
                results[i] = dict[i.ToString(), _token.Token];
            });

            // Assert

            for (int i = 0; i < count; i++)
            {
                Assert.AreEqual(i.ToString(), results[i]);
            }
            Assert.AreEqual(10, loader.Calls);
            Assert.AreEqual(10, loader.TaskCalls);
            Assert.AreEqual(10, dict.Keys.Count());
        }

        /// <summary>
        /// Test that when multiple threads call the TryGet method with the same
        /// key, where the value is not already loaded, that the load method
        /// is only called once. Also check that all the values returned are
        /// correct.
        /// </summary>
        [TestMethod]
        public void LoadingDictionary_TryGetConcurrentHits()
        {
            // Arrange

            var value = "teststring";
            var loader = new ReturnKeyLoader<string>();
            var dict = new LoadingDictionary<string, string>(_logger.Object, loader);
            var successes = new ConcurrentDictionary<int, bool>();
            var results = new ConcurrentDictionary<int, string>();
            var count = 10;

            // Act

            Parallel.For(0, count, (i) =>
            {
                if (dict.TryGet(value, _token.Token, out var result))
                {
                    results[i] = result;
                }
            });

            // Assert

            Assert.AreEqual(count, results.Values.Count);
            foreach (var result in results)
            {
                Assert.AreEqual(value, result.Value);
            }
            Assert.AreEqual(1, loader.Calls);
            Assert.AreEqual(1, loader.TaskCalls);
            Assert.AreEqual(1, dict.Keys.Count());
        }

        /// <summary>
        /// Test that when multiple threads call the TryGet method with different
        /// keys, where the values are not already loaded, all the values
        /// returned are correct.
        /// </summary>
        [TestMethod]
        public void LoadingDictionary_TryGetConcurrentMisses()
        {
            // Arrange

            var loader = new ReturnKeyLoader<string>();
            var dict = new LoadingDictionary<string, string>(_logger.Object, loader);
            var results = new ConcurrentDictionary<int, string>();
            var count = 10;

            // Act

            Parallel.For(0, count, (i) =>
            {
                if (dict.TryGet(i.ToString(), _token.Token, out var value))
                {
                    results[i] = value;

                }
            });

            // Assert

            for (int i = 0; i < count; i++)
            {
                Assert.AreEqual(i.ToString(), results[i]);
            }
            Assert.AreEqual(10, loader.Calls);
            Assert.AreEqual(10, loader.TaskCalls);
            Assert.AreEqual(10, dict.Keys.Count());
        }

        /// <summary>
        /// Test that an exception in the loader is propagated to the caller,
        /// and that the original exception is an inner exception of the
        /// KeyNotFoundException thrown by the get method.
        /// Also check that the result was not added to the dictionary.
        /// </summary>
        [TestMethod]
        public void LoadingDictionary_GetFails()
        {
            // Arrange

            var message = "exceptionmessage";
            var value = "teststring";
            var loader = new ExceptionLoader<string>(message);
            var dict = new LoadingDictionary<string, string>(_logger.Object, loader);

            // Act
            
            Exception exception = null;
            try
            {
                _ = dict[value, _token.Token];
            }
            catch (Exception e)
            {
                exception = e;
            }

            // Assert

            Assert.IsNotNull(exception);
            Assert.IsNotNull(exception.InnerException);
            Assert.AreEqual(message, exception.InnerException.Message);
            Assert.AreEqual(0, dict.Keys.Count());
        }

        /// <summary>
        /// Test that an exception in the loader is swallowed by the TryGet
        /// method, and that false is returned.
        /// Also check that the result was not added to the dictionary.
        /// </summary>
        [TestMethod]
        public void LoadingDictionary_TryGetFails()
        {
            // Arrange

            var message = "exceptionmessage";
            var value = "teststring";
            var loader = new ExceptionLoader<string>(message);
            var dict = new LoadingDictionary<string, string>(_logger.Object, loader);

            // Act
            
            var success = dict.TryGet(value, _token.Token, out _);

            // Assert

            Assert.IsFalse(success);
            Assert.AreEqual(0, dict.Keys.Count());
        }

        /// <summary>
        /// Test that calling the cancellation token results in the get method
        /// returning immediately, and not waiting for the Task to complete.
        /// Also check that the cancellation was passed to the loader properly,
        /// and that the correct exception is thrown.
        /// </summary>
        [TestMethod]
        public void LoadingDictionary_GetCancelled()
        {
            // Arrange

            var millis = 5000;
            var value = "teststring";
            var loader = new ReturnKeyLoader<string>(millis);
            var dict = new LoadingDictionary<string, string>(_logger.Object, loader);
            Exception exception = null;

            // Act

            loader.OnTaskStarted += _ => _token.Cancel();
            var getter = Task.Run(() => dict[value, _token.Token]);
            try
            {
                getter.Wait(millis * 2);
                Assert.Fail(
                    "The prior cancel of the token should prevent getting here");
            }
            catch (AggregateException ex)
            {
                Assert.IsTrue(ex.InnerExceptions.Count == 1);
                Assert.IsTrue(typeof(TaskCanceledException) == ex.InnerException.GetType());
                exception = ex.InnerException;
            }

            // Assert

            Assert.AreEqual(1, loader.Calls);
            Assert.AreEqual(1, loader.TaskCalls);
            Assert.AreEqual(0, loader.CompleteWaits);
            Assert.AreEqual(0, loader.Cancels);
            Assert.IsNotNull(exception);
        }

        /// <summary>
        /// Test that calling the get method with a cancellation token that
        /// is already cancelled results in it returning immediately.
        /// Also check that the correct exception is thrown.
        /// </summary>
        [TestMethod]
        public void LoadingDictionary_GetAlreadyCancelled()
        {
            // Arrange

            var millis = 5000;
            var value = "teststring";
            var loader = new ReturnKeyLoader<string>(millis, true);
            var dict = new LoadingDictionary<string, string>(_logger.Object, loader);
            OperationCanceledException exception = null;

            // Act

            _token.Cancel();
            try
            {
                _ = dict[value, _token.Token];
            }
            catch (OperationCanceledException e)
            {
                exception = e;
            }

            // Assert

            Assert.AreEqual(1, loader.Calls);
            Assert.AreEqual(1, loader.TaskCalls);
            Assert.AreEqual(0, loader.CompleteWaits);
            Assert.AreEqual(0, loader.Cancels);
            Assert.IsNotNull(exception);
        }

        /// <summary>
        /// Test that calling the cancellation token results in the TryGet method
        /// returning immediately, and not waiting for the Task to complete.
        /// Also check that the correct exception is thrown.
        /// </summary>
        [TestMethod]
        public void LoadingDictionary_TryGetCancelled()
        {
            // Arrange

            var millis = 5000;
            var value = "teststring";
            var loader = new ReturnKeyLoader<string>(millis);
            var dict = new LoadingDictionary<string, string>(_logger.Object, loader);
            Exception exception = null;

            // Act

            loader.OnTaskStarted += _ => _token.Cancel();
            var getter = Task.Run(() => dict.TryGet(value, _token.Token, out _));
            try
            {
                getter.Wait(millis * 2);
                Assert.Fail(
                    "The prior cancel of the token should prevent getting here");
            }
            catch (AggregateException ex)
            {
                Assert.IsTrue(ex.InnerExceptions.Count == 1);
                Assert.IsTrue(typeof(TaskCanceledException) == ex.InnerException.GetType());
                exception = ex.InnerException;
            }

            // Assert

            Assert.AreEqual(1, loader.Calls);
            Assert.AreEqual(1, loader.TaskCalls);
            Assert.AreEqual(0, loader.CompleteWaits);
            Assert.AreEqual(0, loader.Cancels);
            Assert.IsNotNull(exception);
        }

        /// <summary>
        /// Test that calling the TryGet method with a cancellation token that
        /// is already cancelled results in it returning immediately.
        /// Also check that the cancellation was passed to the loader properly,
        /// and that the correct exception is thrown.
        /// </summary>
        [TestMethod]
        public void LoadingDictionary_TryGetAlreadyCancelled()
        {
            // Arrange

            var millis = 5000;
            var value = "teststring";
            var loader = new ReturnKeyLoader<string>(millis, true);
            var dict = new LoadingDictionary<string, string>(_logger.Object, loader);
            OperationCanceledException exception = null;

            // Act

            _token.Cancel();
            try
            {
                _ = dict.TryGet(value, _token.Token, out _);
            }
            catch (OperationCanceledException e)
            {
                exception = e;
            }

            // Assert

            Assert.AreEqual(1, loader.Calls);
            Assert.AreEqual(0, loader.CompleteWaits);
            Assert.AreEqual(0, loader.Cancels);
            Assert.IsNotNull(exception);
        }

        /// <summary>
        /// Test that getting values for keys that are pre-loaded in the
        /// constructor, that the load method is never called and the correct
        /// values are returned.
        /// </summary>
        [TestMethod]
        public void LoadingDictionary_GetPreloaded()
        {
            // Arrange

            var values = new Dictionary<string, string>()
            {
                { "one", "one" },
                { "two", "two" },
                { "three", "three" }
            };
            var loader = new ReturnKeyLoader<string>();
            var dict = new LoadingDictionary<string, string>(_logger.Object, loader, values);

            // Act

            foreach (var value in values)
            {
                var result = dict[value.Key, _token.Token];
                Assert.AreEqual(value.Value, result);
            }

            // Assert

            Assert.AreEqual(0, loader.Calls);
            Assert.AreEqual(0, loader.TaskCalls);
            Assert.AreEqual(values.Count, dict.Keys.Count());
        }

        /// <summary>
        /// Test that getting values for keys that are pre-loaded in the
        /// constructor, that the load method is never called and the correct
        /// values are returned.
        /// </summary>
        [TestMethod]
        public void LoadingDictionary_TryGetPreloaded()
        {
            // Arrange

            var values = new Dictionary<string, string>()
            {
                { "one", "one" },
                { "two", "two" },
                { "three", "three" }
            };
            var loader = new ReturnKeyLoader<string>();
            var dict = new LoadingDictionary<string, string>(_logger.Object, loader, values);

            // Act

            foreach (var value in values)
            {
                var success = dict.TryGet(value.Key, _token.Token, out var result);
                Assert.IsTrue(success);
                Assert.AreEqual(value.Value, result);
            }

            // Assert

            Assert.AreEqual(0, loader.Calls);
            Assert.AreEqual(0, loader.TaskCalls);
            Assert.AreEqual(values.Count, dict.Keys.Count());
        }

        /// <summary>
        /// Test that pre-loading values does not affect normal operation of
        /// the loader.
        /// </summary>
        [TestMethod]
        public void LoadingDictionary_GetNotPreloaded()
        {
            // Arrange

            var values = new Dictionary<string, string>()
            {
                { "one", "one" },
                { "two", "two" },
                { "three", "three" }
            };
            var loader = new ReturnKeyLoader<string>();
            var dict = new LoadingDictionary<string, string>(_logger.Object, loader, values);

            // Act

            foreach (var value in values)
            {
                _ = dict[value.Key, _token.Token];
            }
            var result = dict["four", _token.Token];

            // Assert

            Assert.AreEqual(1, loader.Calls);
            Assert.AreEqual(1, loader.TaskCalls);
            Assert.AreEqual("four", result);
            Assert.AreEqual(values.Count + 1, dict.Keys.Count());
        }

        /// <summary>
        /// Test that pre-loading values does not affect normal operation of
        /// the loader.
        /// </summary>
        [TestMethod]
        public void LoadingDictionary_TryGetNotPreloaded()
        {
            // Arrange

            var values = new Dictionary<string, string>()
            {
                { "one", "one" },
                { "two", "two" },
                { "three", "three" }
            };
            var loader = new ReturnKeyLoader<string>();
            var dict = new LoadingDictionary<string, string>(_logger.Object, loader, values);

            // Act

            foreach (var value in values)
            {
                _ = dict.TryGet(value.Key, _token.Token, out _);
            }
            var success = dict.TryGet("four", _token.Token, out var result);

            // Assert

            Assert.AreEqual(1, loader.Calls);
            Assert.AreEqual(1, loader.TaskCalls);
            Assert.IsTrue(success);
            Assert.AreEqual("four", result);
            Assert.AreEqual(values.Count + 1, dict.Keys.Count());
        }

        /// <summary>
        /// Test that if a call to get is canceled, that the result is
        /// not removed from the dictionary on subsequent requests.
        /// </summary>
        [TestMethod]
        public void LoadingDictionary_GetCanceledIsReused()
        {
            // Arrange

            var value = "testvalue";
            var millis = 5000;
            var loader = new ReturnKeyLoader<string>(millis);
            var dict = new LoadingDictionary<string, string>(_logger.Object, loader);
            Exception exception = null;

            // Act

            loader.OnTaskStarted += _ => _token.Cancel();
            var getter = Task.Run(() => _ = dict[value, _token.Token]);
            try
            {
                _ = getter.Result;
            }
            catch (AggregateException e)
            {
                exception = e.InnerException;
            }
            Assert.IsNotNull(exception);

            _token = new CancellationTokenSource();
            Thread.Sleep(100);
            Assert.IsNotNull(dict[value, _token.Token]);
            // Assert

            Assert.AreEqual(1, loader.Calls);
            Assert.AreEqual(1, loader.TaskCalls);
            Assert.AreEqual(0, loader.Cancels);
        }

        /// <summary>
        /// Test that if a call to get fails, that the result is
        /// removed from the dictionary, and subsequent requests try again
        /// instead of returning the previously failed result.
        /// </summary>
        [TestMethod]
        public void LoadingDictionary_GetFailedIsRemoved()
        {
            // Arrange

            var value = "testvalue";
            var loader = new ExceptionLoader<string>("some exception message");
            var dict = new LoadingDictionary<string, string>(_logger.Object, loader);
            var count = 2;

            // Act

            for (int i = 0; i < count; i++)
            {
                try
                {
                    _ = dict[value, _token.Token];
                }
                catch { }
            }

            // Assert

            Assert.AreEqual(count, loader.Calls);
            Assert.AreEqual(0, dict.Keys.Count());
        }

        /// <summary>
        /// Test that if a call to TryGet is canceled, that the result is not
        /// removed from the dictionary, and subsequent requests use the previously
        /// started task.
        /// </summary>
        [TestMethod]
        public void LoadingDictionary_TryGetCanceledIsNotRemoved()
        {
            // Arrange

            var value = "testvalue";
            var millis = 5000;
            var loader = new ReturnKeyLoader<string>(millis);
            var dict = new LoadingDictionary<string, string>(_logger.Object, loader);

            // Act

            loader.OnTaskStarted += _ => _token.Cancel();
            var getter = Task.Run(() => dict.TryGet(value, _token.Token, out _));
            Assert.ThrowsException<AggregateException>(() =>
            {
                _ = getter.Result;
            });

            _token = new CancellationTokenSource();
            Assert.IsTrue(dict.TryGet(value, _token.Token, out _));

            // Assert

            Assert.AreEqual(1, loader.Calls);
            Assert.AreEqual(1, loader.TaskCalls);
            Assert.AreEqual(0, loader.Cancels);
        }

        /// <summary>
        /// Test that if a call to TryGet fails, that the result is
        /// removed from the dictionary, and subsequent requests try again
        /// instead of returning the previously failed result.
        /// </summary>
        [TestMethod]
        public void LoadingDictionary_TryGetFailedIsRemoved()
        {
            // Arrange

            var value = "testvalue";
            var loader = new ExceptionLoader<string>("some exception message");
            var dict = new LoadingDictionary<string, string>(_logger.Object, loader);
            var count = 2;

            // Act

            for (int i = 0; i < count; i++)
            {
                var succes = dict.TryGet(value, _token.Token, out _);
                Assert.IsFalse(succes);
            }

            // Assert

            Assert.AreEqual(count, loader.Calls);
            Assert.AreEqual(0, dict.Keys.Count());
        }

        /// <summary>
        /// Test that if a load task becomes unresponsive and cannot be
        /// canceled, that the get method still returns when canceled.
        /// request.
        /// </summary>
        [TestMethod]
        public void LoadingDictionary_GetUnresponsive()
        {
            var value = "testvalue";
            var loader = new UnresponsiveLoader<string>();
            var dict = new LoadingDictionary<string, string>(_logger.Object, loader);

            // Act

            loader.OnTaskStarted += _ => _token.Cancel();
            var getter = Task.Run(() => dict[value, _token.Token]);
            try
            {
                getter.Wait(1000);
                Assert.IsTrue(
                    _token.IsCancellationRequested,
                    $"{nameof(loader.OnTaskStarted)} has not cancelled the token source.");
                Assert.Fail(
                    "The prior cancel of the token should prevent getting here");
            }
            catch (AggregateException ex)
            {
                Assert.IsTrue(ex.InnerExceptions.Count == 1);
                Assert.IsTrue(typeof(TaskCanceledException) == ex.InnerException.GetType());
            }

            // Assert

            Assert.AreEqual(1, loader.Calls);
            Assert.AreEqual(1, loader.TaskCalls);

            // Cleanup

            loader.Terminate();
        }

        /// <summary>
        /// Test that if a load task becomes unresponsive and cannot be
        /// canceled, that the get method still returns when canceled.
        /// request.
        /// </summary>
        [TestMethod]
        public void LoadingDictionary_TryGetUnresponsive()
        {
            var value = "testvalue";
            var loader = new UnresponsiveLoader<string>();
            var dict = new LoadingDictionary<string, string>(_logger.Object, loader);

            // Act

            loader.OnTaskStarted += _ => _token.Cancel();
            var getter = Task.Run(() => dict.TryGet(value, _token.Token, out _));
            try
            {
                getter.Wait(1000);
                Assert.IsTrue(
                    _token.IsCancellationRequested, 
                    $"{nameof(loader.OnTaskStarted)} has not cancelled the token source.");
                Assert.Fail(
                    "The prior cancel of the token should prevent getting here");
            }
            catch (AggregateException ex)
            {
                Assert.IsTrue(ex.InnerExceptions.Count == 1);
                Assert.IsTrue(typeof(TaskCanceledException) == ex.InnerException.GetType());
            }

            // Assert

            Assert.AreEqual(1, loader.Calls);
            Assert.AreEqual(1, loader.TaskCalls);

            // Cleanup

            loader.Terminate();
        }

        /// <summary>
        /// Test that a null is treated as a valid value.
        /// </summary>
        [TestMethod]
        public void LoadingDictionary_GetNull()
        {
            // Arrange

            var value = "teststring";
            var loader = new NullLoader<string, string>();
            var dict = new LoadingDictionary<string, string>(_logger.Object, loader);

            // Act

            var result = dict[value, _token.Token];

            // Assert

            Assert.IsNull(result);
            Assert.AreEqual(1, loader.Calls);
        }

        /// <summary>
        /// Test that a null is treated as a valid value.
        /// </summary>
        [TestMethod]
        public void LoadingDictionary_TryGetNull()
        {
            // Arrange

            var value = "teststring";
            var loader = new NullLoader<string, string>();
            var dict = new LoadingDictionary<string, string>(_logger.Object, loader);

            // Act

            var success = dict.TryGet(value, _token.Token, out var result);

            // Assert

            Assert.IsTrue(success);
            Assert.IsNull(result);
            Assert.AreEqual(1, loader.Calls);
            Assert.AreEqual(1, loader.TaskCalls);
        }

        /// <summary>
        /// Tests that the error returned when the loader throws an error is 
        /// correctly passed up to the caller.
        /// </summary>
        [TestMethod]
        public async Task LoadingDictionary_LoaderError()
        {
            // Arrange
            var value = "testvalue";
            var loader = new NetworkErrorLoader<string>();
            var dict = new LoadingDictionary<string, string>(_logger.Object, loader);

            // Act
            try
            {
                await dict.GetAsync(value, new CancellationToken());
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex is KeyNotFoundException);
                Assert.IsNotNull(ex.InnerException);
                Assert.IsTrue(ex.InnerException is WebException);
                Assert.IsTrue(ex.InnerException
                    .Message.Contains("Network failure occurred"));
            }
        }
    }
}
