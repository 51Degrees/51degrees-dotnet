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
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Caching;
using System;

namespace FiftyOne.Pipeline.CloudRequestEngine.Data
{
    /// <summary>
    /// The engine's results cache, which neither serves nor stores the
    /// answer to a request the cache key can't tell apart from another.
    /// <para>
    /// The cache key is made from the evidence keys the engine accepts, but
    /// the engine sends every evidence value it is given. A page iteration
    /// carries the session id and the sequence, and a server may supply the
    /// visitor's JA4 and HTTP/2 settings, none of which the cloud lists.
    /// Two iterations of one page that differ only in those would share a
    /// key, and the later one, which may carry the browser test answers the
    /// cloud waits for before it makes a 51Did, would be answered from the
    /// cache and never reach the cloud. Such a request always goes to the
    /// cloud, and every other request is cached as before.
    /// </para>
    /// </summary>
    internal sealed class PageIterationAwareCache : IFlowCache
    {
        /// <summary>
        /// Evidence names that make a request one the cache must leave
        /// alone, under any prefix. Letter case is ignored.
        /// </summary>
        private static readonly string[] UncachedNames =
        {
            "session-id",
            "sequence",
            "ja4",
            "h2",
        };

        private readonly IFlowCache _inner;

        /// <summary>
        /// Wraps the cache the engine was given.
        /// </summary>
        /// <param name="inner">The cache.</param>
        public PageIterationAwareCache(IFlowCache inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        /// <inheritdoc/>
        public IFlowElement FlowElement
        {
            get => _inner.FlowElement;
            set => _inner.FlowElement = value;
        }

        /// <summary>
        /// The cached answer, or null where the request must go to the
        /// cloud.
        /// </summary>
        /// <param name="key">The request.</param>
        /// <returns>The cached answer, or null.</returns>
        public IElementData this[IFlowData key]
            => IsUncached(key) ? null : _inner[key];

        /// <summary>
        /// Stores the answer, unless the request must go to the cloud.
        /// </summary>
        /// <param name="key">The request.</param>
        /// <param name="value">The answer.</param>
        public void Put(IFlowData key, IElementData value)
        {
            if (IsUncached(key) == false)
            {
                _inner.Put(key, value);
            }
        }

        /// <inheritdoc/>
        public void Dispose() => _inner.Dispose();

        /// <summary>
        /// True when the request carries a value the cache key leaves out
        /// and the cloud reads.
        /// </summary>
        /// <param name="data">The request.</param>
        /// <returns>True if the cache must leave the request alone.</returns>
        internal static bool IsUncached(IFlowData data)
        {
            if (data == null)
            {
                return false;
            }
            foreach (var key in data.GetEvidence().AsDictionary().Keys)
            {
                var separator = key.IndexOf(
                    Core.Constants.EVIDENCE_SEPERATOR,
                    StringComparison.Ordinal);
                var name = separator >= 0 ? key.Substring(separator + 1) : key;
                foreach (var uncached in UncachedNames)
                {
                    if (name.Equals(uncached, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
