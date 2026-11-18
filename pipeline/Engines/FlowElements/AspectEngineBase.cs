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

using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Caching;
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Engines.FlowElements
{
    /// <summary>
    /// Base class for 51Degrees aspect engines. 
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/conceptual-overview.md#aspect-engine">Specification</see>
    /// </summary>
    /// <typeparam name="T">
    /// The type of data that the engine will return. Must implement 
    /// <see cref="IAspectData"/>.
    /// </typeparam>
    /// <typeparam name="TMeta">
    /// The type of meta data that the flow element will supply 
    /// about the properties it populates.
    /// </typeparam>
    public abstract class AspectEngineBase<T, TMeta> : 
        FlowElementBase<T, TMeta>, IAspectEngine<T, TMeta>
        where T : IAspectData
        where TMeta : IAspectPropertyMetaData
    {
        /// <summary>
        /// The results cache to be used to store results against
        /// relevant evidence values.
        /// </summary>
        private IFlowCache _cache;
        private bool _cacheHitOrMiss;

        /// <summary>
        /// For each result whose lazy property is still running, by the
        /// key of its Evidence, a task that finishes once the result has
        /// been put in the results cache or refused. A request with the
        /// same Evidence waits for it and looks in the cache again, rather
        /// than start the work again. This is not a cache. An entry leaves
        /// as soon as its task finishes.
        /// </summary>
        private readonly ConcurrentDictionary<DataKey, Task> _pending =
            new ConcurrentDictionary<DataKey, Task>();

        /// <summary>
        /// <see cref="FlowElementBase{T, TMeta}.CreateElementData"/> as the
        /// delegate that GetOrAdd takes, made once rather than for every
        /// request.
        /// </summary>
        private readonly Func<IPipeline, T> _createElementData;

        /// <summary>
        /// The list <see cref="FlowElementBase{T, TMeta}.Properties"/> last
        /// returned, and the read only projection of it that
        /// <see cref="IAspectEngine.Properties"/> returns. Held as one
        /// object so a reader on another thread never sees a projection
        /// paired with the wrong list.
        /// </summary>
        private Tuple<IList<TMeta>, IList<IAspectPropertyMetaData>>
            _propertiesProjection;

        /// <summary>
        /// The tier to which the current data source belongs.
        /// For 51Degrees this will usually be one of:
        /// Lite
        /// Premium
        /// Enterprise
        /// </summary>
        public abstract string DataSourceTier { get; }

        /// <summary>
        /// The lazy loading configuration to use. Lazy loading is
        /// deprecated and has no effect. The engine always processes on
        /// the thread that processes the Flow Data. Long-running work
        /// belongs in a lazy property of the engine's own data, whose
        /// readers all wait on one task.
        /// </summary>
        public LazyLoadingConfiguration LazyLoadingConfiguration { get; private set; }

        /// <summary>
        /// Provide an implementation for the non-generic,
        /// aspect-specific version of the meta-data property.
        /// </summary>
        /// <remarks>
        /// Every caller gets the same read only list until
        /// <see cref="FlowElementBase{T, TMeta}.Properties"/> returns a
        /// different list, so the projection is not made again for every
        /// request.
        /// </remarks>
        IList<IAspectPropertyMetaData> IAspectEngine.Properties
        {
            get
            {
                var source = Properties;
                var cached = Volatile.Read(ref _propertiesProjection);
                if (cached == null ||
                    ReferenceEquals(cached.Item1, source) == false)
                {
                    cached = Tuple.Create<IList<TMeta>,
                        IList<IAspectPropertyMetaData>>(
                        source,
                        source.Cast<IAspectPropertyMetaData>()
                            .ToList()
                            .AsReadOnly());
                    Volatile.Write(ref _propertiesProjection, cached);
                }
                return cached.Item2;
            }
        }

        /// <summary>
        /// Provide an implementation for the non-generic, 
        /// aspect-specific version of the meta-data property.
        /// </summary>
        public virtual bool HasLoadedProperties { get { return true; } }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">
        /// The logger to use
        /// </param>
        /// <param name="aspectDataFactory">
        /// The factory function to use when the engine creates an
        /// <see cref="AspectDataBase"/> instance.
        /// </param>
        public AspectEngineBase(
            ILogger<AspectEngineBase<T, TMeta>> logger,
            Func<IPipeline, FlowElementBase<T, TMeta>, T> aspectDataFactory)
            : base(logger, aspectDataFactory)
        {
            _createElementData = CreateElementData;
        }

        /// <summary>
        /// Set the results cache.
        /// This is used to store the results of queries against the evidence
        /// that was provided.
        /// If the same evidence is provided again then the cached response
        /// is returned without needing to call the engine itself.
        /// </summary>
        /// <param name="cache">
        /// The cache.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the parameter is null
        /// </exception>
        public virtual void SetCache(IFlowCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _cache.FlowElement = this;
        }

        /// <summary>
        /// Set the engine to flag when a cache hit occurs by setting a field 
        /// on the cached aspect data.
        /// </summary>
        /// <param name="cacheHitOrMiss">
        /// Whether to flag cache hits or not.
        /// </param>
        public virtual void SetCacheHitOrMiss(bool cacheHitOrMiss)
        {
            _cacheHitOrMiss = cacheHitOrMiss;
        }

        /// <summary>
        /// Deprecated, has no effect. Lazy loading is deprecated and the
        /// engine always processes on the thread that processes the Flow
        /// Data. Long-running work belongs in a lazy property of the
        /// engine's own data. Kept so that existing code still builds.
        /// </summary>
        /// <param name="configuration">
        /// The configuration to use.
        /// </param>
        /// <returns>
        /// This engine builder instance.
        /// </returns>
        public virtual void SetLazyLoading(LazyLoadingConfiguration configuration)
        {
            LazyLoadingConfiguration = configuration;
        }

        /// <summary>
        /// Extending classes must implement this method.
        /// It should perform the required processing and update the 
        /// specified aspect data instance.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> instance that provides the evidence.
        /// </param>
        /// <param name="aspectData">
        /// The <see cref="IAspectData"/> instance to populate with the 
        /// results of processing.
        /// </param>
        protected abstract void ProcessEngine(IFlowData data, T aspectData);

        /// <summary>
        /// Implementation of method from the base class 
        /// <see cref="FlowElementBase{T, TMeta}"/>.
        /// This exists to centralize the results caching logic.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> instance that provides the evidence 
        /// and holds the result.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the parameter is null
        /// </exception>
        protected sealed override void ProcessInternal(IFlowData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            ProcessWithCache(data);
        }

        /// <summary>
        /// Whether a result may be stored in the results cache and served
        /// from it. A result that holds a value created for the one request
        /// that asked for it must not be, because a later request with the
        /// same Evidence would receive that value. The default is true.
        /// It runs as soon as processing returns, and must not wait. A
        /// result whose lazy property is still running is not cached then.
        /// See the <see href="https://github.com/51Degrees/specifications/blob/287d773def38f75d3dbb8ea544de197124d45462/pipeline-specification/features/caching.md#values-created-for-one-request">Specification</see>
        /// </summary>
        /// <param name="aspectData">
        /// The result of processing, or a result found in the cache.
        /// </param>
        /// <returns>True if the result may be cached.</returns>
        protected virtual bool CanCache(T aspectData)
        {
            return true;
        }

        /// <summary>
        /// Add the result of processing to the cache, if there is a cache and
        /// the result may be cached.
        /// </summary>
        private void AddToCache(IFlowData data)
        {
            if (_cache != null)
            {
                var result = data.GetFromElement(this);
                if (CanCache(result))
                {
                    _cache.Put(data, result);
                }
            }
        }

        /// <summary>
        /// Prepare to put a result into the results cache once its lazy
        /// property's task has finished, for an engine whose
        /// <see cref="CanCache(T)"/> refuses the result while the task
        /// runs. The Evidence the cache key needs is copied now, on the
        /// thread that processes the Flow Data, so the put never reads the
        /// Flow Data. The task calls the returned action once, before it
        /// finishes, with true to put the result or false not to. Later
        /// calls do nothing. Until then, a request with the same Evidence
        /// waits for the action and then looks in the cache again.
        /// </summary>
        /// <param name="data">The Flow Data being processed.</param>
        /// <param name="aspectData">The result to put.</param>
        /// <returns>
        /// The action, or null when the engine has no results cache.
        /// </returns>
        internal Action<bool> CacheWhenReady(IFlowData data, T aspectData)
        {
            var cache = _cache;
            if (cache == null || data?.Pipeline == null)
            {
                return null;
            }
            var key = data.Pipeline.CreateFlowData();
            foreach (var entry in data.GetEvidence().AsDictionary())
            {
                key.AddEvidence(entry.Key, entry.Value);
            }
            var pendingKey = data.GenerateKey(EvidenceKeyFilter);
            var finished = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = new KeyValuePair<DataKey, Task>(
                pendingKey, finished.Task);
            _pending[pendingKey] = finished.Task;
            var done = 0;
            return put =>
            {
                if (Interlocked.Exchange(ref done, 1) != 0)
                {
                    return;
                }
                try
                {
                    if (put)
                    {
                        cache.Put(key, aspectData);
                    }
                }
#pragma warning disable CA1031 // Do not catch general exception types
                // A result that cannot be cached is still the reader's.
                catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    Logger.LogWarning(ex,
                        $"Engine '{GetType().Name}' could not cache a result.");
                }
                finally
                {
                    // After the put, so that a request that no longer
                    // finds the entry here finds the result in the cache.
                    ((ICollection<KeyValuePair<DataKey, Task>>)_pending)
                        .Remove(pending);
                    finished.TrySetResult(true);
                    key.Dispose();
                }
            };
        }

        /// <summary>
        /// Look in the results cache for a result that may be served.
        /// </summary>
        private T FromCache(IFlowData data)
        {
            T cacheResult = default(T);
            try
            {
                cacheResult = (T)_cache[data];
            }
            catch (InvalidCastException) { }
            // A result that must not be served from the cache is
            // processed again.
            if (cacheResult != null && CanCache(cacheResult) == false)
            {
                cacheResult = default(T);
            }
            return cacheResult;
        }

        /// <summary>
        /// Private method that checks if the result is already in the cache
        /// or not.
        /// If it is then the result is added to 'data', if not then 
        /// <see cref="ProcessEngine(IFlowData, T)"/> is called to do so.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> instance that provides the evidence 
        /// and holds the result.
        /// </param>
        private void ProcessWithCache(IFlowData data)
        {
            T cacheResult = default(T);

            // If there is a cache then check if the result
            // is already in there.
            if (_cache != null)
            {
                cacheResult = FromCache(data);
                // A request with the same Evidence whose lazy property is
                // still running decides soon whether its result is cached,
                // so wait for that rather than start the work again.
                if (cacheResult == null &&
                    _pending.IsEmpty == false &&
                    _pending.TryGetValue(
                        data.GenerateKey(EvidenceKeyFilter),
                        out var pending))
                {
                    pending.Wait();
                    cacheResult = FromCache(data);
                }
            }
            // If we don't have a result from the cache then 
            // run through the normal processing.
            if (cacheResult == null)
            {
                // If the flow data already contains an entry for this
                // element's key then use it. Otherwise, create a new
                // aspect data instance and add it to the flow data.
                T aspectData =
                    data.GetOrAdd(ElementDataKeyTyped, _createElementData);
                if (aspectData.Engines.Contains(this) == false)
                {
                    Internal(aspectData).AddEngine(this);
                }

                // The deprecated lazy loading option has no effect. The
                // engine always processes on the calling thread. Work
                // that takes long belongs in a lazy property of the
                // engine's own data.
                ProcessEngine(data, aspectData);
                AddToCache(data);
            }
            else
            {
                // If this aspect engine is configured to record cache hits,
                // set the cache hit value on the cached aspect data.
                if (_cacheHitOrMiss)
                {
                    Internal(cacheResult).SetCacheHit();
                }

                // We have a result from the cache so add it 
                // into the flow data.
                data.GetOrAdd(ElementDataKeyTyped, (f) =>
                {
                    return cacheResult;
                });
            }
        }

        /// <summary>
        /// The members of the aspect data that this engine writes besides
        /// its values, which every <see cref="AspectDataBase"/> has.
        /// </summary>
        /// <exception cref="PipelineException">
        /// Thrown, naming the data's type, when the data does not derive
        /// from <see cref="AspectDataBase"/>. Going on without them would
        /// let lazy loading read values before processing had finished.
        /// </exception>
        private IAspectDataInternal Internal(T aspectData)
        {
            if (aspectData is IAspectDataInternal result)
            {
                return result;
            }
            throw new PipelineException(string.Format(
                CultureInfo.InvariantCulture,
                "The engine '{0}' cannot process aspect data of type " +
                "'{1}', because the data does not derive from '{2}'.",
                GetType().Name,
                aspectData == null ? "null" : aspectData.GetType().FullName,
                nameof(AspectDataBase)));
        }

        /// <summary>
        /// Called by the base class when this instance is disposed.
        /// </summary>
        protected override void ManagedResourcesCleanup()
        {
            // If there is a cache then ensure it and it's contents are
            // disposed of correctly.
            if (_cache != null)
            {
                _cache.Dispose();
            }
        }
    }
}
