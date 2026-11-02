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

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Caching
{
    /// <summary>
    /// Implementation of <see cref="ILoadingDictionary{TKey, TValue}"/>.
    /// This implementation fulfils the expectations described in the
    /// interface description.
    /// As per the source (https://referencesource.microsoft.com/#mscorlib/system/Collections/Concurrent/ConcurrentDictionary.cs,2f8bcdfbad10304f)
    /// <see cref="ConcurrentDictionary{TKey, TValue}.GetOrAdd(TKey, Func{TKey, TValue})"/>
    /// is not guaranteed to only call the factory once. Hence, we wrap the
    /// Tasks in a <see cref="Lazy{T}"/>, meaning the value doesn't get
    /// evaluated until it is read.
    /// It is the responsibility of the loader to handle cancellation of
    /// its Task when instructed to by the token. If a thread is left in
    /// an unresponsive state, it will result in failed gets for that key
    /// until the Task responds and can be removed from the dictionary.
    /// 
    /// Details of the use of <see cref="Lazy"/>:
    /// Consider the scenario where two get requests are made at the same
    /// instant, with the same key. Inside the
    /// <see cref="ConcurrentDictionary{TKey, TValue}.GetOrAdd(TKey, Func{TKey, TValue})"/>
    /// method, it will first call TryGet. Both will fail to get, as the key
    /// is not yet present. Both threads will then call ConcurrentDictionary.TryAddInternal(key, factory(key), ...).
    /// Only one will succeed in adding, and that result will be returned to
    /// both threads. However, both threads will have called the factory with
    /// the same key. If the factory directly starts a Task, it will run twice.
    /// If we instead wrap the Task in a Lazy, the actual Task will not be
    /// initialized until it is accessed. We know that a single result is
    /// returned to both threads, so only one Lazy will be resolved, the other
    /// will be lost and the Task never started.
    /// 
    /// This is a requirement when the load method called in the factory is
    /// an expensive operation.
    /// </summary>
    /// <typeparam name="TKey">
    /// Type of the key.
    /// </typeparam>
    /// <typeparam name="TValue">
    /// Type of the values stored against the keys.
    /// </typeparam>
    public class LoadingDictionary<TKey, TValue> : IAsyncLoadingDictionary<TKey, TValue>
        where TValue : class
    {
        /// <summary>
        /// Internal dictionary of loaded values.
        /// </summary>
        private readonly ConcurrentDictionary<TKey, Lazy<Task<TValue>>> _dictionary;

        /// <summary>
        /// The loader to use when a key is not already present.
        /// </summary>
        private readonly IValueTaskLoader<TKey, TValue> _loader;

        /// <summary>
        /// Logger to use.
        /// </summary>
        private readonly ILogger<LoadingDictionary<TKey, TValue>> _logger;

        /// <summary>
        /// Timeout to use for internal load tasks.
        /// </summary>
        private readonly TimeSpan _taskTimeout;

        #region constructors
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="logger">
        /// The logger to use for messages.
        /// </param>
        /// <param name="loader">
        /// The loader to use when loading values.
        /// </param>
        /// <param name="initial">
        /// Collection of initial values to pre-populate the dictionary with.
        /// </param>
        /// <param name="concurrencyLevel">
        /// The estimated number of threads that will fetch from the dictionary
        /// concurrently.
        /// </param>
        /// <param name="capacity">
        /// The initial number of elements that the can contain.
        /// </param>
        /// <param name="taskTimeoutDuration">
        /// Duration to let internal tasks run for before cancelling completely.
        /// This is separate from the cancellation of a get.
        /// </param>
        public LoadingDictionary(
            ILogger<LoadingDictionary<TKey, TValue>> logger,
            IValueTaskLoader<TKey, TValue> loader,
            ICollection<KeyValuePair<TKey, TValue>> initial,
            int concurrencyLevel,
            int capacity,
            TimeSpan? taskTimeoutDuration = null)
            : this(
                  logger,
                  loader,
                  new ConcurrentDictionary<TKey, Lazy<Task<TValue>>>(concurrencyLevel, capacity),
                  initial,
                  taskTimeoutDuration)
        { }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="logger">
        /// The logger to use for messages.
        /// </param>
        /// <param name="loader">
        /// The loader to use when loading values.
        /// </param>
        /// <param name="initial">
        /// Collection of initial values to pre-populate the dictionary with.
        /// </param>
        /// <param name="taskTimeoutDuration">
        /// Duration to let internal tasks run for before cancelling completely.
        /// This is separate from the cancellation of a get.
        /// </param>
        public LoadingDictionary(
            ILogger<LoadingDictionary<TKey, TValue>> logger,
            IValueTaskLoader<TKey, TValue> loader,
            ICollection<KeyValuePair<TKey, TValue>> initial,
            TimeSpan? taskTimeoutDuration = null)
            : this(
                  logger,
                  loader,
                  new ConcurrentDictionary<TKey, Lazy<Task<TValue>>>(),
                  initial,
                  taskTimeoutDuration)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="logger">
        /// The logger to use for messages.
        /// </param>
        /// <param name="loader">
        /// The loader to use when loading values.
        /// </param>
        /// <param name="concurrencyLevel">
        /// The estimated number of threads that will fetch from the dictionary
        /// concurrently.
        /// </param>
        /// <param name="capacity">
        /// The initial number of elements that the can contain.
        /// </param>
        public LoadingDictionary(
            ILogger<LoadingDictionary<TKey, TValue>> logger,
            IValueTaskLoader<TKey, TValue> loader,
            int concurrencyLevel,
            int capacity)
            : this(
                  logger,
                  loader,
                  new ConcurrentDictionary<TKey, Lazy<Task<TValue>>>(concurrencyLevel, capacity))
        { }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="logger">
        /// The logger to use for messages.
        /// </param>
        /// <param name="loader">
        /// The loader to use when loading values.
        /// </param>
        public LoadingDictionary(
            ILogger<LoadingDictionary<TKey, TValue>> logger,
            IValueTaskLoader<TKey, TValue> loader)
            : this(
                  logger,
                  loader,
                  new ConcurrentDictionary<TKey, Lazy<Task<TValue>>>())
        { }

        private LoadingDictionary(
            ILogger<LoadingDictionary<TKey, TValue>> logger,
            IValueTaskLoader<TKey, TValue> loader,
            ConcurrentDictionary<TKey, Lazy<Task<TValue>>> dictionary,
            ICollection<KeyValuePair<TKey, TValue>> initial = null,
            TimeSpan? taskTimeoutDuration = null)
        {
            _logger = logger;
            _dictionary = dictionary;
            _loader = loader;
            // set default timeout to 30 seconds
            _taskTimeout = taskTimeoutDuration ?? TimeSpan.FromSeconds(30);

            if (initial != null)
            {
                foreach (var pair in initial)
                {
                    _dictionary[pair.Key] = new Lazy<Task<TValue>>(() => Task.FromResult(pair.Value));
                }
            }
        }
        #endregion

        /// <summary>
        /// Gets the value associated with the key, either from an existing
        /// entry, or by loading it first.
        /// </summary>
        /// <param name="key">
        /// Key in the dictionary.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to cancel the load operation.
        /// </param>
        /// <returns>
        /// Value for the key provided.
        /// </returns>
        /// <exception cref="KeyNotFoundException">
        /// If the key was not already in the dictionary, and could not be loaded
        /// by the loader. The exception from the loader will be the inner
        /// exception.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// If the token cancels the operation.
        /// </exception>
        public TValue this[TKey key, CancellationToken cancellationToken] => Get(key, cancellationToken);

        /// <summary>
        /// Collection of the keys currently loaded.
        /// </summary>
        public ICollection<TKey> Keys => _dictionary.Keys;

        /// <summary>
        /// Check whether the key has already been loaded into the dictionary.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool ContainsKey(TKey key) => _dictionary.ContainsKey(key);

        /// <summary>
        /// Try to get  the value associated with the key, either from an existing
        /// entry, or by loading it first. Rather than throwing an exception, false
        /// is returned to indicate failure.
        /// </summary>
        /// <param name="key">
        /// Key in the dictionary.
        /// </param>
        /// <param name="callerStopToken">
        /// Token used to cancel the load operation.
        /// </param>
        /// <param name="value">
        /// Value for the key provided, or null.
        /// </param>
        /// <returns>
        /// True if the get was successful, and value was populated.
        /// </returns>
        /// <exception cref="OperationCanceledException">
        /// Thrown if the operation was cancelled by the caller.
        /// </exception>
        public bool TryGet(
            TKey key, 
            CancellationToken callerStopToken, 
            out TValue value)
        {
            try
            {
                value = Get(key, callerStopToken);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                value = null;
                return false;
            }
        }

        /// <summary>
        /// Asynchronously retrieves the value associated with 
        /// the specified key.If the value is not already loaded, 
        /// it triggers the loading process.This method will respect 
        /// the provided cancellation token to cancel the load operation.
        public Task<TValue> GetAsync(TKey key, CancellationToken callerToken)
        {
            return GetAndWait(key, callerToken);
        }

        /// <summary>
        /// Synchronously retrieves the value associated with the specified key.
        /// This method blocks the calling thread until the value is available.
        /// If the operation fails or is cancelled, the exception is propagated.
        /// </summary>
        private TValue Get(TKey key, CancellationToken callerToken)
        {
            try
            {
                return GetAndWait(key, callerToken).Result;
            }
            catch (AggregateException ex)
            {
                throw ex.InnerException;
            }
        }

        /// <summary>
        /// Retrieves the value for the specified key, waiting for the 
        /// load operation to complete if necessary.
        /// </summary>
        /// <param name="key">The key to retrieve the value for.</param>
        /// <param name="callerToken">Cancellation token to cancel 
        /// the operation.</param>
        /// <returns>The value associated with the key.</returns>
        /// <remarks>
        /// If the caller token cancels the operation while the load is still 
        /// in progress, an <see cref="OperationCanceledException"/> is thrown.
        /// If the load operation fails, the key is removed from the 
        /// dictionary, and a <see cref="KeyNotFoundException"/> is thrown 
        /// with the original exception as the inner exception.
        /// </remarks>
        private async Task<TValue> GetAndWait(
            TKey key,
            CancellationToken callerToken)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            try
            {
                var result = _dictionary.GetOrAdd(
                    key,
                    k => Load(k, new CancellationTokenSource(_taskTimeout).Token))
                    .Value;
                    result.Wait(callerToken);
                return result.Result;
            }
            catch (OperationCanceledException)
            {
                // Caller cancelled the operation.
                throw;
            }
            catch (Exception ex)
            {
                // Loader operation failed 
                // Remove invalid key.
                Remove(key);
                // throw exception.
                ThrowKeyNotFoundException(key, ex);
                throw;
            }
        }

        /// <summary>
        /// Get whether or not the task completed successfully.
        /// </summary>
        /// <param name="task">
        /// The Task to check.
        /// </param>
        /// <returns>
        /// True if the task completed, and did not throw any
        /// exceptions.
        /// </returns>
        private static bool GetIsCompleteSuccess(Task<TValue> task) => 
                task.Status == TaskStatus.RanToCompletion &&
                task.IsFaulted == false;

        /// <summary>
        /// Throws a KeyNotFoundException, including any exceptions thrown
        /// within the failed Task.
        /// </summary>
        /// <param name="key">
        /// Key which could not be loaded.
        /// </param>
        /// <param name="task">
        /// Task to get any exceptions from.
        /// </param>
        private void ThrowKeyNotFoundException(TKey key, Exception ex)
        {
            // If exception is aggregate and only 1 inner exception
            // then set outer to inner exception
            // else set exception
            var innerException = ex is AggregateException aggregateException && 
                aggregateException.InnerExceptions.Count == 1
                ? aggregateException.InnerException
                : ex.InnerException ?? ex;

            throw new KeyNotFoundException(
                $"An exception occurred in '{_loader.GetType().Name}' while " +
                $"trying to load the value for key '{key}'", 
                innerException);
        }

        /// <summary>
        /// Factory method which gets a new lazy load task to pass to the
        /// GetOrAdd method. The task does not begin running until the
        /// Lazy value is retrieved. This prevents duplicate Tasks from
        /// being started when the
        /// <see cref="ConcurrentDictionary{TKey, TValue}.GetOrAdd(TKey, Func{TKey, TValue})"/>
        /// method calls the
        /// <see cref="ConcurrentDictionary{TKey, TValue}.TryAdd(TKey, TValue)"/>
        /// method with the result of this factory.
        /// </summary>
        /// <param name="key">
        /// Key to load the value for.
        /// </param>
        /// <param name="cancellationToken">
        /// Cancellation passed to the Task.
        /// </param>
        /// <returns>
        /// A new lazily loaded Task to load the value.
        /// </returns>
        private Lazy<Task<TValue>> Load(
            TKey key,
            CancellationToken cancellationToken) =>
            new Lazy<Task<TValue>>(
                () => _loader.Load(key, cancellationToken), true);

        /// <summary>
        /// Try to remove the key from the dictionary.
        /// In the case that removal fails, an error is logged.
        /// </summary>
        /// <param name="key">
        /// Key to remove from the internal dictionary.
        /// </param>
        private void Remove(TKey key)
        {
            if (_dictionary.TryRemove(key, out _) == false)
                _logger.LogInformation($"Failed to remove entry for key '{key}'.");
        }
    }
}
