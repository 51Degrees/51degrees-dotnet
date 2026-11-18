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

namespace FiftyOne.Pipeline.Core.Data
{
    /// <summary>
    /// This class stores property values that have been determined by a 
    /// specific FlowElement based on the supplied evidence.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/conceptual-overview.md#element-data">Specification</see>
    /// </summary>
    /// <remarks>
    /// An instance is written only by the Flow Element that provides it, or
    /// the Flow Elements that share it, and only while they process. Once a
    /// Flow Element that writes it has finished, a write through the indexer
    /// or <see cref="DataBase.PopulateFrom"/>, or a setter built on them,
    /// throws a <see cref="Exceptions.PipelineException"/>, until a Flow
    /// Element that shares the instance starts to process it again. Values
    /// a derived class keeps once it has read them, such as values read from
    /// native results, are not writes, and are kept with
    /// <see cref="KeepValues"/>. It is not intended to be written
    /// from several threads at once, nor read while it is being written,
    /// and its dictionary is not safe for either. Once the Flow Element has
    /// finished, the instance is safe to read from several threads at once.
    /// A derived class whose reads change something it holds, such as
    /// native results, holds a lock for each read so that this stays true.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/thread-safety.md#element-data">thread safety specification</see>.
    /// </remarks>
    public abstract class ElementDataBase : DataBase, IElementData
    {
        // Flow Elements processing this instance now. A field read, never
        // a lock.
        private int _writers;

        // Set once a Flow Element that writes this instance has finished.
        private volatile bool _finished;

        /// <summary>
        /// Get or set a data value. Setting fails once a Flow Element that
        /// writes this instance has finished, as the remarks on
        /// <see cref="ElementDataBase"/> describe.
        /// </summary>
        /// <param name="key">
        /// The name of the property
        /// </param>
        /// <returns>
        /// The property value
        /// </returns>
        /// <exception cref="Exceptions.PipelineException">
        /// Thrown when setting a value once a Flow Element that writes this
        /// instance has finished.
        /// </exception>
        public override object this[string key]
        {
            get
            {
                return base[key];
            }
            set
            {
                CheckWrite(key);
                base[key] = value;
            }
        }

        /// <summary>
        /// Check that this instance can be written now. A derived class
        /// calls this before a write that does not go through the indexer,
        /// such as adding to a list it holds.
        /// </summary>
        /// <param name="key">
        /// The name of the value about to be written, for the message.
        /// </param>
        /// <exception cref="Exceptions.PipelineException">
        /// Thrown once a Flow Element that writes this instance has
        /// finished, and no Flow Element that shares it is writing it.
        /// </exception>
        protected void CheckWrite(string key)
        {
            if (_finished && Volatile.Read(ref _writers) == 0)
            {
                throw new Exceptions.PipelineException(
                    $"Element Data '{GetType().Name}' cannot be " +
                    $"written once the Flow Element that provides it " +
                    $"has finished. The value for '{key}' was not " +
                    $"written.");
            }
        }

        /// <summary>
        /// Keep values that this instance has read, such as values read from
        /// native results, so that they are not read again. These are not
        /// writes, so they are kept whether or not the Flow Element that
        /// provides this instance has finished.
        /// </summary>
        /// <param name="values">
        /// The values to keep.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied values are null
        /// </exception>
        protected void KeepValues(
            IEnumerable<KeyValuePair<string, object>> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }
            foreach (var value in values)
            {
                base[value.Key] = value.Value;
            }
        }

        /// <summary>
        /// Record that a Flow Element that writes this instance has started
        /// to process it, which opens it for writing again.
        /// </summary>
        internal void StartWriting()
        {
            Interlocked.Increment(ref _writers);
        }

        /// <summary>
        /// Record that a Flow Element that writes this instance has
        /// finished.
        /// </summary>
        /// <param name="started">
        /// True if <see cref="StartWriting"/> was called for this Flow
        /// Element.
        /// </param>
        internal void FinishWriting(bool started)
        {
            _finished = true;
            if (started)
            {
                Interlocked.Decrement(ref _writers);
            }
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
