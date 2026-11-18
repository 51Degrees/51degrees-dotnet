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
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Core.Data
{
    /// <summary>
    /// This class stores property values that have been determined by a 
    /// specific FlowElement based on the supplied evidence.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/conceptual-overview.md#element-data">Specification</see>
    /// </summary>
    /// <remarks>
    /// An instance is written only by the Flow Element that creates it,
    /// while that Flow Element processes. It is not intended to be written
    /// from several threads at once, nor read while it is being written,
    /// and its dictionary is not safe for either. Once the Flow Element has
    /// finished, the instance is safe to read from several threads at once.
    /// A derived class whose reads change something it holds, such as
    /// native results, holds a lock for each read so that this stays true.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/thread-safety.md#element-data">thread safety specification</see>.
    /// <para>
    /// An instance that opts in with <see cref="IsPoolable"/> goes back to
    /// its Flow Element's pool when the Flow Data that holds it is
    /// disposed, and another request is then given it. Keeping a reference
    /// to Element Data after the Flow Data that holds it is disposed is
    /// not supported, because a pool may give the instance to another
    /// request. Copy the values needed before disposing the Flow Data. An
    /// instance held by a results cache never goes back to a pool.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/resource-cleanup.md#reusing-element-data">resource cleanup specification</see>.
    /// </para>
    /// </remarks>
    public abstract class ElementDataBase : DataBase, IElementData
    {
        /// <summary>
        /// The pool this instance goes back to when its Flow Data is
        /// disposed, or null when it does not go back to one.
        /// </summary>
        private ElementDataPool _pool;

        /// <summary>
        /// True if this instance may be given to another request once the
        /// Flow Data that holds it is disposed. The default is false. An
        /// implementation whose only state is its values, or that resets
        /// the rest in <see cref="ResetForPool"/>, returns true. An
        /// instance that implements <see cref="IDisposable"/> is never
        /// pooled.
        /// </summary>
        protected virtual bool IsPoolable => false;

        /// <summary>
        /// Called before the instance goes back to its pool, once any lazy
        /// task on it has finished. Removes every value, so the next
        /// request starts with none. An override resets any other state
        /// and calls this. If it throws, the instance does not go back.
        /// </summary>
        protected virtual void ResetForPool()
        {
            ClearValues();
        }

        /// <summary>
        /// The lazy task that writes this instance, or null when there is
        /// none. The instance goes back to its pool only once the task has
        /// finished. Disposing the Flow Data does not wait for it. The
        /// default is null.
        /// </summary>
        /// <returns>The task, or null.</returns>
        protected virtual Task GetLazyTask()
        {
            return null;
        }

        /// <summary>
        /// Give this instance a pool to go back to, if it can be pooled.
        /// </summary>
        internal void UsePool(ElementDataPool pool)
        {
            if (IsPoolable && (this is IDisposable) == false)
            {
                Volatile.Write(ref _pool, pool);
            }
        }

        /// <summary>
        /// Keep this instance out of any pool, as when a results cache
        /// holds it and shares it with other requests.
        /// </summary>
        internal void KeepOutOfPool()
        {
            Volatile.Write(ref _pool, null);
        }

        /// <summary>
        /// Return this instance to its pool, once. Called when the Flow
        /// Data that holds it is disposed. An instance whose lazy task is
        /// still running goes back when the task finishes, so disposal
        /// does not wait for it.
        /// </summary>
        internal void ReturnToPool()
        {
            var pool = Interlocked.Exchange(ref _pool, null);
            if (pool == null)
            {
                return;
            }
            var lazyTask = GetLazyTask();
            if (lazyTask == null || lazyTask.IsCompleted)
            {
                Recycle(pool);
                return;
            }
            lazyTask.ContinueWith(
                t =>
                {
                    // The task's failure has reached its readers.
                    _ = t.Exception;
                    Recycle(pool);
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        /// <summary>
        /// Reset this instance and put it in the pool. An instance that
        /// fails to reset is left for the garbage collector.
        /// </summary>
        private void Recycle(ElementDataPool pool)
        {
            try
            {
                ResetForPool();
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // An instance in an unknown state must not be reused.
            catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                return;
            }
            // A pooled instance does not keep the Pipeline alive. It is set
            // again when the instance is next taken.
            Pipeline = null;
            pool.Return(this);
        }

        /// <summary>
        /// The <see cref="IPipeline"/> instance this element data will
        /// be associated with.
        /// </summary>
        public IPipeline Pipeline { get; set; }

        /// <summary>
        /// Constructor
        /// Creates an <see cref="ElementDataBase"/> instance with a 
        /// non-thread-safe, case-insensitive dictionary.
        /// </summary>
        /// <param name="logger">
        /// Used for logging
        /// </param>
        /// <param name="pipeline">
        /// The <see cref="IPipeline"/> instance this element data will
        /// be associated with.
        /// </param>
        public ElementDataBase(ILogger<ElementDataBase> logger, IPipeline pipeline) :
            this(logger, pipeline, new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase))
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">
        /// Used for logging
        /// </param>
        /// <param name="pipeline">
        /// The <see cref="IPipeline"/> instance this element data will
        /// be associated with.
        /// </param>
        /// <param name="dictionary">
        /// The dictionary instance to use internally when storing data values.
        /// </param>
        public ElementDataBase(ILogger<ElementDataBase> logger,
            IPipeline pipeline,
            IDictionary<string, object> dictionary)
            : base(logger, dictionary)
        {
            Pipeline = pipeline;
        }
    }
}
