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
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace FiftyOne.Caching
{
    /// <summary>
    /// Many of the entities used by the detector are requested repeatably. 
    /// The cache improves memory usage and reduces strain on the garbage collector
    /// by storing previously requested entities for a short period of time to avoid 
    /// the need to refetch them from the underlying storage mechanism.
    /// </summary>
    /// <para>
    /// The Least Recently Used (LRU) cache is used. LRU cache keeps track of what
    /// was used when in order to discard the least recently used items first.
    /// Every time a cache item is used the "age" of the item used is updated.
    /// </para>
    /// <para>
    /// This implementation supports concurrency by using multiple linked lists
    /// in place of a single linked list in the original implementation.
    /// The linked list to use is assigned at random and stored in the cached
    /// item. This will generate an even set of results across the different 
    /// linked lists. The approach reduces the probability of the same linked 
    /// list being locked when used in a environments with a high degree of
    /// concurrency. If the feature is not required then the constructor should be
    /// provided with a concurrency value of 1.
    /// </para>
    /// <para>
    /// Use for User-Agent caching.
    /// For a vast majority of the real life environments a constant stream of unique 
    /// User-Agents is a fairly rare event. Usually the same User-Agent can be
    /// encountered multiple times within a fairly short period of time as the user
    /// is making a subsequent request. Caching frequently occurring User-Agents
    /// improved detection speed considerably.
    /// </para>
    /// <para>
    /// Some devices are also more popular than others and while the User-Agents for
    /// such devices may differ, the combination of components used would be very
    /// similar. Therefore internal caching is also used to take advantage of the 
    /// more frequently occurring entities.
    /// </para>
    /// <typeparam name="K">Key for the cache items</typeparam>
    /// <typeparam name="V">Value for the cache items</typeparam>
    internal abstract class LruCacheBase<K, V> : IDisposable
    {
        #region Classes

        /// <summary>
        /// An item stored in the cache along with references to the next and
        /// previous items.
        /// </summary>
        internal class CachedItem
        {
            /// <summary>
            /// Key associated with the cached item.
            /// </summary>
            internal readonly K Key;

            /// <summary>
            /// Value of the cached item.
            /// </summary>
            internal readonly V Value;

            /// <summary>
            /// The next item in the linked list.
            /// </summary>
            internal CachedItem Next;

            /// <summary>
            /// The previous item in the linked list.
            /// </summary>
            internal CachedItem Previous;

            /// <summary>
            /// The linked list the item is part of.
            /// </summary>
            internal readonly CacheLinkedList List;

            /// <summary>
            /// Indicates that the items is valid and added to the linked list. 
            /// It is not in the process of being manipulated by another thread
            /// either being added to the list or being removed.
            /// </summary>
            internal bool IsValid;

            internal CachedItem(CacheLinkedList list, K key, V value)
            {
                List = list;
                Key = key;
                Value = value;
            }
        }

        /// <summary>
        /// </summary>
        internal class TimeLimitedCachedItem : CachedItem
        {
            internal readonly DateTime ExpiryTimeUTC;

            internal TimeLimitedCachedItem(
                CacheLinkedList list, K key, V value, DateTime expiryTimeUTC) : 
                base (list, key, value)
            {
                ExpiryTimeUTC = expiryTimeUTC;
            }
        }

        /// <summary>
        /// A linked list used in the LruCache implementation in place of the
        /// .NET linked list. This implementation enables items to be moved 
        /// within the linked list.
        /// </summary>
        internal class CacheLinkedList
        {
            /// <summary>
            /// The cache that the list is part of.
            /// </summary>
            private readonly LruCacheBase<K, V> _cache;

            /// <summary>
            /// The first item in the list.
            /// </summary>
            internal CachedItem First { get; private set; }

            /// <summary>
            /// The last item in the list.
            /// </summary>
            internal CachedItem Last { get; private set; }

            /// <summary>
            /// Constructs a new instance of <see cref="CacheLinkedList"/>.
            /// </summary>
            /// <param name="cache">Cache the list is included within</param>
            internal CacheLinkedList(LruCacheBase<K, V> cache)
            {
                _cache = cache;
            }

            /// <summary>
            /// Adds a new cache item to the linked list.
            /// </summary>
            /// <param name="item"></param>
            internal void AddNew(CachedItem item)
            {
                bool added = false;
                if (item != First)
                {
                    lock (this)
                    {
                        if (item != First)
                        {
                            if (First == null)
                            {
                                // First item to be added to the queue.
                                First = item;
                                Last = item;
                            }
                            else
                            {
                                // Add this item to the head of the linked list.
                                item.Next = First;
                                First.Previous = item;
                                First = item;

                                // Set flag to indicate an item was added and if
                                // the cache is full an item should be removed.
                                added = true;
                            }

                            // Indicate the item is now ready for another thread
                            // to manipulate and is fully added to the linked list.
                            item.IsValid = true;
                        }
                    }
                }

                // Check if the linked list needs to be trimmed as the cache
                // size has been exceeded.
                // Note: We don't use the 'Remove' method here because we
                // know that we'll be removing the last item in the list
                // and this allows some slight optimizations.
                if (added && _cache._dictionary.Count > _cache.CacheSize)
                {
                    lock (this)
                    {
                        if (_cache._dictionary.Count > _cache.CacheSize)
                        {                            
                            // Indicate that the last item is being removed from 
                            // the linked list.
                            Last.IsValid = false;

                            // Remove the item from the dictionary before 
                            // removing from the linked list.
                            CachedItem lastItem;
                            var result = _cache._dictionary.TryRemove(
                                Last.Key,
                                out lastItem);
                            Debug.Assert(result,
                                "The last key was not in the dictionary");
                            Debug.Assert(Last == lastItem,
                                "The item removed does not match the last one");
                            Last = Last.Previous;
                            Last.Next = null;
                        }
                    }
                }
            }

            /// <summary>
            /// Set the first item in the linked list to the item provided.
            /// </summary>
            /// <param name="item"></param>
            internal void MoveFirst(CachedItem item)
            {
                if (item != First && item.IsValid == true)
                {
                    lock (this)
                    {
                        if (item != First && item.IsValid == true)
                        {
                            if (item == Last)
                            {
                                // The item is the last one in the list so is 
                                // easy to remove. A new last will need to be
                                // set.
                                Last = item.Previous;
                                Last.Next = null;
                            }
                            else
                            {
                                // The item was not at the end of the list. 
                                // Remove it from it's current position ready
                                // to be added to the top of the list.
                                item.Previous.Next = item.Next;
                                item.Next.Previous = item.Previous;
                            }

                            // Add this item to the head of the linked list.
                            item.Next = First;
                            item.Previous = null;
                            First.Previous = item;
                            First = item;
                        }
                    }
                }
            }

            /// <summary>
            /// Replace an existing item in the cache with a new value. The new
            /// item must have the same key as the existing item.
            /// </summary>
            /// <param name="oldItem">Existing item to replace.</param>
            /// <param name="newItem">New item to replace it with.</param>
            internal void Replace(CachedItem oldItem, CachedItem newItem)
            {
                if (oldItem.Key.Equals(newItem.Key) == false)
                {
                    throw new InvalidOperationException(
                        "A cache item cannot be replace with an item with a " +
                        $"different key. Existing: {{{oldItem.Key} : {oldItem.Value}}}, " +
                        $"new: {{{newItem.Key} : {newItem.Value}}}. '{oldItem.Key}'!='{newItem.Key}'.");
                }
                if (oldItem.IsValid)
                {
                    lock (this)
                    {
                        if (oldItem.IsValid)
                        {
                            var removed = oldItem.List.Remove(oldItem);
                            Debug.Assert(removed, "item not removed from list as expected.");

                            // Insert the new item at the head of the list.
                            newItem.Next = newItem.List.First;
                            if (newItem.List.First != null)
                            {
                                newItem.Next.Previous = newItem;
                            }
                            newItem.List.First = newItem;
                            if (newItem.List.Last == null) { newItem.List.Last = newItem; }

                            // Indicate the item is now ready for another thread
                            // to manipulate and is fully added to the linked list.
                            newItem.IsValid = true;

                            // Replace the value in the dictionary.
                            _cache._dictionary.AddOrUpdate(newItem.Key, newItem, (i, o) => newItem);
                        }
                    }
                }
            }

            /// <summary>
            /// Clears all items from the linked list.
            /// </summary>
            internal void Clear()
            {
                First = null;
                Last = null;
            }

            /// <summary>
            /// Remove the specified item from the cache.
            /// </summary>
            /// <param name="item"></param>
            /// <returns>
            /// True if item is removed from list. False otherwise.
            /// </returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown if the 'item' parameter is null.
            /// </exception>
            internal bool Remove(CachedItem item)
            {
                bool removed = false;

                if(item == null)
                {
                    throw new ArgumentNullException("item");
                }

                if (item.IsValid && item.List == this)
                {
                    lock (this)
                    {
                        if (item.IsValid && item.List == this)
                        {
                            // Indicate that the last item is being removed from 
                            // the linked list.
                            item.IsValid = false;

                            // Link the items to either side of the one we're removing.
                            if (item.Next != null)
                            {
                                item.Next.Previous = item.Previous;
                            }
                            if (item.Previous != null)
                            {
                                item.Previous.Next = item.Next;
                            }
                            // Update First and Last if needed.
                            if (item.Equals(First))
                            {
                                First = item.Next;
                            }
                            if (item.Equals(Last))
                            {
                                Last = item.Previous;
                            }

                            // Remove the item from the dictionary.
                            CachedItem removedItem;
                            var result = _cache._dictionary.TryRemove(
                                item.Key,
                                out removedItem);
                            Debug.Assert(result,
                                "The item specified for removal was not in the dictionary");
                            Debug.Assert(item == removedItem,
                                "The item removed does not match the expected one");
                            removed = true;
                        }
                    }
                }
                else if(item.List != this)
                {
                    throw new Exception("The item to be deleted is not in this list");
                }

                return removed;
            }
        }

        #endregion

        #region Fields

        /// <summary>
        /// Random number generator used to determine which cache list to 
        /// place items into.
        /// </summary>
        private static readonly Random _random = new Random();

        /// <summary>
        /// Hash map of keys to item values.
        /// </summary>
        private readonly ConcurrentDictionary<K, CachedItem> _dictionary;

        /// <summary>
        /// Linked list of items in the cache.
        /// </summary>
        /// <remarks>
        /// Marked internal as checked as part of the unit tests.
        /// </remarks>
        internal readonly CacheLinkedList[] _linkedLists;

        /// <summary>
        /// The number of items the cache lists should have capacity for.
        /// </summary>
        internal readonly int CacheSize;

        internal readonly bool UpdateExisting;

        /// <summary>
        /// The length of time that an item should be returned by the cache
        /// after it is added.
        /// If more than this time has passed then the item should be removed.
        /// If set to null then items should never expire.
        /// </summary>
        internal readonly TimeSpan? ItemLifetime;

        /// <summary>
        /// The number of requests made to the cache.
        /// </summary>
        internal long Requests { get { return _requests; } }
        private long _requests;

        /// <summary>
        /// The number of times an item was not available.
        /// </summary>
        internal long Misses { get { return _misses; } }
        private long _misses;

        #endregion

        #region Properties

        /// <summary>
        /// Percentage of cache misses.
        /// </summary>
        public double PercentageMisses
        {
            get
            {
                return (double)Misses / (double)Requests;
            }
        }

        /// <summary>
        /// Retrieves the value for key requested. If the key does not exist
        /// in the cache then the loader provided in the constructor is used
        /// to fetch the item.
        /// </summary>
        /// <param name="key">Key for the item required</param>
        /// <returns>An instance of the value associated with the key</returns>
        public virtual V this[K key]
        {
            get
            {
                Interlocked.Increment(ref _requests);
                CachedItem node;
                if (_dictionary.TryGetValue(key, out node) == false)
                {
                    // Get the item fresh from the loader before trying
                    // to write the item to the cache.
                    Interlocked.Increment(ref _misses);
                }
                else if (node is TimeLimitedCachedItem timeLimitedItem)
                {
                    // We're using time-limited items so check if this one
                    // has expired.
                    if(timeLimitedItem.ExpiryTimeUTC < DateTime.UtcNow)
                    {
                        // If it has expired then remove it from the cache 
                        // and return the default value.
                        foreach(var list in _linkedLists)
                        {
                            if (list == node.List)
                            {
                                list.Remove(node);
                                break;
                            }
                        }
                        node = null;
                    }
                }

                if (node == null)
                {
                    // no existing node found so return default value.
                    return default(V);
                }
                else
                {
                    // If the item was already in the dictionary then
                    // move it to the head of it's list.
                    node.List.MoveFirst(node);
                    return node.Value;
                }
            }
        }
        

        #endregion

        #region Constructor

        /// <summary>
        /// Constructs a new instance of the cache.
        /// </summary>
        /// <param name="cacheSize">
        /// The number of items to store in the cache
        /// </param>
        internal LruCacheBase(int cacheSize)
            : this(cacheSize, Environment.ProcessorCount) { }

        /// <summary>
        /// Constructs a new instance of the cache.
        /// </summary>
        /// <param name="cacheSize">
        /// The number of items to store in the cache
        /// </param>
        /// <param name="concurrency">
        /// The expected number of concurrent requests to the cache
        /// </param>
        /// <param name="updateExisting">
        /// True if existing items should be replaced
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
        internal LruCacheBase(int cacheSize, int concurrency, 
            bool updateExisting = false, 
            TimeSpan? itemLifetime = null)
        {
            if (concurrency <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    "concurrency",
                    "Concurrency must be a positive integer greater than 0.");
            }
            CacheSize = cacheSize;
            UpdateExisting = updateExisting;
            _dictionary = new ConcurrentDictionary<K, CachedItem>(
                concurrency, cacheSize);
            _linkedLists = new CacheLinkedList[concurrency];
            for (int i = 0; i < _linkedLists.Length; i++)
            {
                _linkedLists[i] = new CacheLinkedList(this);
            }
            ItemLifetime = itemLifetime;
        }

        #endregion

        #region Destructor

        /// <summary>
        /// Ensures that references to the cached data are released.
        /// </summary>
        ~LruCacheBase()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases references to the cached data.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases references to the cached data.
        /// </summary>
        /// <param name="disposing">
        /// True if the calling method is Dispose, false for the finaliser.
        /// </param>
        protected void Dispose(bool disposing)
        {
            // Clear the dictionary and linked lists.
            _dictionary.Clear();
            foreach(var list in _linkedLists)
            {
                list.Clear();
            }
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Public Methods
        /// <summary>
        /// Resets the stats for the cache.
        /// </summary>
        public void ResetCache()
        {
            for (int i = 0; i < _linkedLists.Length; i++)
            {
                _linkedLists[i].Clear();
            }
            _dictionary.Clear();
            _misses = 0;
            _requests = 0;
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Returns a random linked list.
        /// </summary>
        /// <returns>
        /// A random linked list of the cache.
        /// </returns>
        private CacheLinkedList GetRandomLinkedList()
        {
            return _linkedLists[_random.Next(_linkedLists.Length)];
        }

        /// <summary>
        /// Add the specified key and value to the cache.
        /// </summary>
        /// <param name="key">
        /// The key of the item to add
        /// </param>
        /// <param name="value">
        /// The value of the item to add
        /// </param>
        /// <returns>
        /// The <see cref="CachedItem"/> instance representing the item
        /// that was added.
        /// If the key already existed in the cache then this will be the
        /// existing item.
        /// </returns>
        protected CachedItem Add(K key, V value)
        {
            CachedItem newNode;
            if (ItemLifetime.HasValue)
            {
                newNode = new TimeLimitedCachedItem(
                    GetRandomLinkedList(),
                    key,
                    value,
                    DateTime.UtcNow.Add(ItemLifetime.Value));
            }
            else
            {
                newNode = new CachedItem(
                    GetRandomLinkedList(),
                    key,
                    value);
            }

            // If the node has already been added to the dictionary
            // then get it, otherwise add the one just fetched.
            CachedItem node = _dictionary.GetOrAdd(key, newNode);

            // If the node got from the dictionary is the new one
            // just fetched then it needs to be added to the linked
            // list.
            if (node == newNode)
            {
                // Set the node as the first item in the list.
                newNode.List.AddNew(newNode);
            }
            else if (UpdateExisting)
            {
                // Replace the existing node, and move it to the head of its
                // list.
                newNode.List.Replace(node, newNode);
            }
            else
            {
                // If the item was already in the dictionary then
                // move it to the head of it's list.
                node.List.MoveFirst(node);
            }
            return node;
        }
        #endregion
    }
}
