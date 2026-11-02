# FiftyOne.Caching

This package contains interfaces and classes relating to caching. There are two major types of cache represented, 'put' caches and 'loading' caches.
Both types have an indexer to access the data stored in the cache.

Put caches will return the default value if the item is not present in the cache and include a 'Put' method to add data to the cache.
In contrast, loading caches can be passed a loader object that will be used to load a value if it is not present in the cache.
 
# Implementations

This package currently contains 2 cache implementations:

## No Cache

An implementation that performs no caching at all, if using the loading cache then values will always be loaded, if using the put cache, the default value will always be returned.

## LRU Cache

LRU stands for Least Recently Used and is a commonly used cache eviction policy. LRU means that when the cache is full and a new item is added, the item that was accessed the furthest in the past will be removed from the cache to make room for the new item.

LRU based caches often suffer from poor multi-threaded performance so this implementation uses multiple LRU lists internally in order to mitigate this.