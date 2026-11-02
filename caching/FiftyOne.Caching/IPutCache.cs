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

namespace FiftyOne.Caching
{
    /// <summary>
    /// A cache that supports a thread safe put method for inserting to cache.
    /// By contrast, for example, <see cref="LruCacheBase{K, V}"/> is a loading 
    /// cache, it automatically updates itself by being provided with a data 
    /// loader.
    /// </summary>
    /// <typeparam name="K">
    /// The type of the key to use for the data in the cache
    /// </typeparam>
    /// <typeparam name="V">The type of the data in the cache</typeparam>
    public interface IPutCache<K, V> : ICache<K, V>
    {
        /// <summary>
        /// Add an item to the cache
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="value">The data</param>
        void Put(K key, V value);
    }
}
