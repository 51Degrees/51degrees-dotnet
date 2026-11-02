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
using System.Threading.Tasks;

namespace FiftyOne.Caching
{
    /// <summary>
    /// Read only dictionary which loads values lazily as keys are requested.
    /// The intended implementation of this interface uses Tasks internally to
    /// ensure that concurrent requests for the same key, only result in the
    /// loader being called once for that key. When the value for a given key
    /// cannot be loaded by the loader, a <see cref="KeyNotFoundException"/>
    /// is thrown, with the inner exception being any exception thrown by the
    /// loader.
    /// This interface should always be implemented in a thread-safe manner.
    /// Any implementation should allow for pre-populating the dictionary by
    /// providing an initial collection to the constructor.
    /// </summary>
    /// <typeparam name="TKey">
    /// Type of the key.
    /// </typeparam>
    /// <typeparam name="TValue">
    /// Type of the values stored against the keys.
    /// </typeparam>
    public interface ILoadingDictionary<TKey, TValue>
        where TValue : class
    {
        /// <summary>
        /// Gets the value associated with the key, either from an existing
        /// entry, or by loading it first.
        /// </summary>
        /// <param name="key">
        /// Key in the dictionary.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to cancel the load operation.
        /// </param>
        /// <returns>
        /// Value for the key provided.
        /// </returns>
        /// <exception cref="KeyNotFoundException">
        /// If the key was not already in the dictionary, and could not be loaded
        /// by the loader. The exception from the loader will be the inner
        /// exception.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// If the token cancels the operation.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// If the given key is null.
        /// </exception>
        TValue this[TKey key, CancellationToken cancellationToken] { get; }

        /// <summary>
        /// Enumeration of the keys currently loaded.
        /// </summary>
        ICollection<TKey> Keys { get; }

        /// <summary>
        /// Check whether the key has already been loaded into the dictionary.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        bool ContainsKey(TKey key);

        /// <summary>
        /// Try to get  the value associated with the key, either from an existing
        /// entry, or by loading it first. Rather than throwing an exception, false
        /// is returned to indicate failure.
        /// </summary>
        /// <param name="key">
        /// Key in the dictionary.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to cancel the load operation.
        /// </param>
        /// <param name="value">
        /// Value for the key provided, or null.
        /// </param>
        /// <returns>
        /// True if the get was successful, and value was populated.
        /// </returns>
        /// <exception cref="OperationCanceledException">
        /// If the operation was canceled through the token.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// If the given key is null.
        /// </exception>
        bool TryGet(TKey key, CancellationToken cancellationToken, out TValue value);
    }
}
