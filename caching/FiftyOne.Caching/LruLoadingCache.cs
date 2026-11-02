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
using System.Threading;

namespace FiftyOne.Caching
{
    internal class LruLoadingCache<K, V> : LruCacheBase<K, V>, ILoadingCache<K, V>
    {
        /// <summary>
        /// Loader used to fetch items not in the cache.
        /// </summary>
        private IValueLoader<K, V> _loader;


        /// <summary>
        /// Constructs a new instance of the cache.
        /// </summary>
        /// <param name="cacheSize">
        /// The number of items to store in the cache
        /// </param>
        internal LruLoadingCache(int cacheSize) : base(cacheSize)
        {
        }

        /// <summary>
        /// Constructs a new instance of the cache.
        /// </summary>
        /// <param name="cacheSize">
        /// The number of items to store in the cache
        /// </param>
        /// <param name="concurrency">
        /// The expected number of concurrent requests to the cache
        /// </param>
        internal LruLoadingCache(int cacheSize, int concurrency) 
            : base(cacheSize, concurrency)
        {
        }

        /// <summary>
        /// Constructs a new instance of the cache.
        /// </summary>
        /// <param name="cacheSize">
        /// The number of items to store in the cache
        /// </param>
        /// <param name="concurrency">
        /// The expected number of concurrent requests to the cache
        /// </param>
        /// <param name="itemLifetime">
        /// The length of time that an item should be returned by the cache
        /// after it is added.
        /// If more than this time has passed then the item should be removed.
        /// Setting this parameter effectively changes the implementation
        /// to a Time-based LRU cache.
        /// https://en.wikipedia.org/wiki/Cache_replacement_policies#Time_aware_least_recently_used_(TLRU)
        /// Passing null disables this functionality.
        /// </param>
        internal LruLoadingCache(int cacheSize, 
            int concurrency,
            TimeSpan? itemLifetime = null)
            : base(cacheSize, concurrency, false, itemLifetime)
        {
        }

        /// <summary>
        /// Constructs a new instance of the cache.
        /// </summary>
        /// <param name="cacheSize">The number of items to store in the cache</param>
        /// <param name="loader">Loader used to fetch items not in the cache</param>
        internal LruLoadingCache(int cacheSize, IValueLoader<K, V> loader)
            : this(cacheSize, loader, Environment.ProcessorCount)
        {
        }

        /// <summary>
        /// Constructs a new instance of the cache.
        /// </summary>
        /// <param name="cacheSize">The number of items to store in the cache</param>
        /// <param name="loader">Loader used to fetch items not in the cache</param>
        /// <param name="concurrency">
        /// The expected number of concurrent requests to the cache
        /// </param>
        /// <param name="itemLifetime">
        /// The length of time that an item should be returned by the cache
        /// after it is added.
        /// If more than this time has passed then the item should be removed.
        /// Setting this parameter effectively changes the implementation
        /// to a Time-based LRU cache.
        /// https://en.wikipedia.org/wiki/Cache_replacement_policies#Time_aware_least_recently_used_(TLRU)
        /// Passing null disables this functionality.
        /// </param>
        internal LruLoadingCache(int cacheSize, 
            IValueLoader<K, V> loader, 
            int concurrency, 
            TimeSpan? itemLifetime = null)
            : this(cacheSize, concurrency, itemLifetime)
        {
            SetValueLoader(loader);
        }

        /// <summary>
        /// Set the value loader that will be used to load items on a cache miss
        /// </summary>
        /// <param name="loader"></param>
        public void SetValueLoader(IValueLoader<K, V> loader)
        {
            _loader = loader;
        }


        /// <summary>
        /// Retrieves the value for key requested. If the key does not exist
        /// in the cache then the loader provided in the constructor is used
        /// to fetch the item.
        /// </summary>
        /// <param name="key">Key for the item required</param>
        /// <returns>An instance of the value associated with the key</returns>
        public override V this[K key]
        {
            get
            {
                return this[key, _loader];
            }
        }

        /// <summary>
        /// Retrieves the value for key requested. If the key does not exist
        /// in the cache then the Fetch method is used to retrieve the value
        /// from another source.
        /// </summary>
        /// <param name="key">Key for the item required</param>
        /// <param name="loader">Loader to fetch the item from if not in the cache</param>
        /// <returns>An instance of the value associated with the key</returns>
        public V this[K key, IValueLoader<K, V> loader]
        {
            get
            {
                var result = base[key];
                if ((result == null || result.Equals(default(V))) &&
                    loader != null)
                {
                    result = loader.Load(key);
                    Add(key, result);
                }
                return result;
            }
        }
    }
}
