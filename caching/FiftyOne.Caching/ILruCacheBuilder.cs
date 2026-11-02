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

using System;
using System.Collections.Generic;
using System.Text;

namespace FiftyOne.Caching
{
    public interface ILruCacheBuilder<TBuilder>
    {
        /// <summary>
        /// Set the expected number of concurrent requests to the cache. This
        /// will determine the number linked lists used in the cache structure.
        /// For details see description of multiple linked lists in
        /// <see cref="LruCacheBase{K, V}"/>.
        /// </summary>
        /// <param name="concurrency">
        /// The expected number of concurrent requests to the cache.
        /// </param>
        /// <returns>
        /// This builder
        /// </returns>
        TBuilder SetConcurrency(int concurrency);

        /// <summary>
        /// Set the length of time that an item should be returned by the cache
        /// after it is added.
        /// If more than this time has passed then the item should be removed.
        /// Setting this parameter effectively changes the implementation
        /// to a Time-based LRU cache.
        /// https://en.wikipedia.org/wiki/Cache_replacement_policies#Time_aware_least_recently_used_(TLRU)
        /// </summary>
        /// <param name="itemLifetime">
        /// The lifetime of a cache item.
        /// </param>
        /// <returns>
        /// This builder
        /// </returns>
        TBuilder SetItemLifetime(TimeSpan itemLifetime);
    }
}
