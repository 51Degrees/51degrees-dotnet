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

using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.Engines.Services;

namespace FiftyOne.IpIntelligence.Shared.FlowElements
{
    /// <summary>
    /// Fluent builder that is used to create an on-premise IP intelligence 
    /// aspect engine.
    /// </summary>
    /// <typeparam name="TBuilder">
    /// The type of engine builder for fluent methods to return.
    /// </typeparam>
    /// <typeparam name="TEngine">
    /// The type of engine that will be built. 
    /// </typeparam>
    public abstract class OnPremiseIpiEngineBuilderBase<TBuilder, TEngine>
        : FiftyOneOnPremiseAspectEngineBuilderBase<TBuilder, TEngine>
        where TBuilder : OnPremiseIpiEngineBuilderBase<TBuilder, TEngine>
        where TEngine : IFiftyOneAspectEngine
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="dataUpdateService">
        /// The <see cref="IDataUpdateService"/> to use when registering
        /// data files to check for for automatic updates.
        /// </param>
        public OnPremiseIpiEngineBuilderBase(
            IDataUpdateService dataUpdateService)
            : base(dataUpdateService)
        {
        }

        /// <summary>
        /// Set the expected number of operations that use the engine at the same
        /// time. The native code uses it for every collection read from the data
        /// file.
        /// First, it sets the number of file handles in the pool, which is the
        /// concurrency up to a fixed maximum of 64, because a process can only have
        /// so many files open. An operation that has to read from the file when
        /// every handle is in use waits until another operation releases one.
        /// Second, it decides how each cache is split into parts, each with its own
        /// lock, which avoids excessive locking. An item in use cannot be removed
        /// from a cache, and any operation can need any part, so every part holds
        /// at least the concurrency in items. The number of parts is the
        /// concurrency, or fewer where needed so that every part holds that many
        /// items, and a cache that holds fewer items than the concurrency in total
        /// is made large enough to hold that many. No value stops the engine
        /// building.
        /// The concurrency has no effect when every collection is held in memory,
        /// for example with the MaxPerformance profile.
        /// </summary>
        /// <param name="concurrency">
        /// The expected number of operations that use the engine at
        /// the same time.
        /// </param>
        /// <returns>This builder</returns>
        public abstract TBuilder SetConcurrency(ushort concurrency);
    }
}
