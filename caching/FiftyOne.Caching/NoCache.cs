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
    /// Example implementation that performs no caching.
    /// </summary>
    /// <typeparam name="TKey">
    /// The type of the object to use as the cache key.
    /// </typeparam>
    /// <typeparam name="TVal">
    /// The type of the value object that will be stored against the key in 
    /// the cache.
    /// </typeparam>
    class NoCache<TKey, TVal> :
        IPutCache<TKey, TVal>,
        ILoadingCache<TKey, TVal>
    {
        /// <summary>
        /// The loader to use if the value is not in the cache.
        /// </summary>
        private IValueLoader<TKey, TVal> _loader;

        /// <summary>
        /// Create the cache without a loader.
        /// </summary>
        public NoCache() { }
        /// <summary>
        /// Create the cache with the specified loader.
        /// </summary>
        /// <param name="loader">
        /// The loader to use if values are not in the cache.
        /// </param>
        public NoCache(IValueLoader<TKey, TVal> loader)
        {
            _loader = loader;
        }

        /// <summary>
        /// Get the <see cref="TVal"/> associated with the specified 
        /// <see cref="TKey"/>.
        /// </summary>
        /// <param name="key">
        /// The key to use when retrieving data.
        /// </param>
        /// <returns>
        /// The <see cref="TVal"/> associated with the specified 
        /// <see cref="TKey"/>.
        /// </returns>
        public TVal this[TKey key]
        {
           get { return this[key, _loader]; }
        }

        /// <summary>
        /// Get the <see cref="TVal"/> associated with the specified 
        /// <see cref="TKey"/>.
        /// </summary>
        /// <param name="key">
        /// The key to use when retrieving data.
        /// </param>
        /// <param name="loader">
        /// The loader to use if the key is not present in the cache.
        /// </param>
        /// <returns>
        /// The <see cref="TVal"/> associated with the specified 
        /// <see cref="TKey"/>.
        /// </returns>
        public TVal this[TKey key, IValueLoader<TKey, TVal> loader]
        {
            get
            {
                if (loader != null)
                {
                    return loader.Load(key);
                }
                return default(TVal);
            }
        }

        /// <summary>
        /// Add the specified key value pair to the cache.
        /// </summary>
        /// <param name="key">
        /// The key to store in the cache.
        /// </param>
        /// <param name="value">
        /// The value to store with the key.
        /// </param>
        public void Put(TKey key, TVal value)
        {
        }

        /// <summary>
        /// Caches must implement IDisposable
        /// </summary>
        public void Dispose()
        {
            // Nothing to dispose of
        }
    }
}
