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
    /// Provides a method to build caches that implement 
    /// <see cref="ILoadingCache{K, V}"/> 
    /// </summary>
    public interface ILoadingCacheBuilder<TBuilder> : ICacheBuilder
    {
        /// <summary>
        /// Build and return an <see cref="ILoadingCache{K, V}"/>  
        /// </summary>
        /// <typeparam name="K">
        /// The type to use as the key for the cache
        /// </typeparam>
        /// <typeparam name="V">
        /// The type of data that will be stored in the cache
        /// </typeparam>
        /// <param name="cacheSize">
        /// The maximum number of entries that will be stored in the cache.
        /// </param>
        /// <returns>
        /// A cache implementing <see cref="ILoadingCache{K, V}"/> 
        /// </returns>
        new ILoadingCache<K, V> Build<K, V>(int cacheSize);

        /// <summary>
        /// Build and return an <see cref="ILoadingCache{K, V}"/>  
        /// </summary>
        /// <typeparam name="K">
        /// The type to use as the key for the cache
        /// </typeparam>
        /// <typeparam name="V">
        /// The type of data that will be stored in the cache
        /// </typeparam>
        /// <param name="cacheSize">
        /// The maximum number of entries that will be stored in the cache.
        /// </param>
        /// <param name="loader">
        /// The loader to be used when a cache miss occurs.
        /// </param>
        /// <returns>
        /// A cache implementing <see cref="ILoadingCache{K, V}"/> 
        /// </returns>
        ILoadingCache<K, V> Build<K, V>(int cacheSize, IValueLoader<K, V> loader);
    }

    /// <summary>
    /// Interface that combines the loading cache builder and LRU cache 
    /// builder interfaces.
    /// </summary>
    public interface ILruLoadingCacheBuilder :
        ILoadingCacheBuilder<ILruLoadingCacheBuilder>,
        ILruCacheBuilder<ILruLoadingCacheBuilder>
    {
    }
}
