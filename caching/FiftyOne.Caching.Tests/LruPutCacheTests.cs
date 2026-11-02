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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Caching.Tests
{
    /// <summary>
    /// Tests targeting the <see cref="LruPutCache{K, V}"/>.
    /// </summary>
    [TestClass]
    public class LruPutCacheTests
    {
        /// <summary>
        /// Test the 'PutCache' functionality of the LruCache
        /// </summary>
        [TestMethod]
        public void LruPutCache_Get()
        {
            LruPutCache<int, string> cache = new LruPutCache<int, string>(2);

            cache.Put(1, "test");
            var result = cache[1];

            Assert.AreEqual("test", result);
        }

        /// <summary>
        /// Test the 'PutCache' functionality of the LruCache
        /// </summary>
        [TestMethod]
        public void LruPutCache_NoValue()
        {
            LruPutCache<int, string> cache = new LruPutCache<int, string>(2);
            
            var result = cache[1];

            Assert.AreEqual(default(string), result);
        }

        /// <summary>
        /// Test the eviction policy of the LruCache will evict the 
        /// least recently used item.
        /// </summary>
        [TestMethod]
        public void LruPutCache_LruPolicyCheck()
        {
            // Set cache size to 2 with only 1 list
            LruPutCache<int, string> cache = new LruPutCache<int, string>(2, 1);

            // Add three items in a row.
            cache.Put(1, "test1");
            cache.Put(2, "test2");
            cache.Put(3, "test3");
            var result1 = cache[1];
            var result2 = cache[2];
            var result3 = cache[3];

            // The oldest item should have been evicted.
            Assert.AreEqual(default(string), result1);
            Assert.AreEqual("test2", result2);
            Assert.AreEqual("test3", result3);
        }

        /// <summary>
        /// Test the eviction policy of the LruCache will evict the 
        /// least recently used item.
        /// </summary>
        [TestMethod]
        public void LruPutCache_LruPolicyCheck2()
        {
            // Set cache size to 2 with only 1 list
            LruPutCache<int, string> cache = new LruPutCache<int, string>(2, 1);

            // Add two items.
            cache.Put(1, "test1");
            cache.Put(2, "test2");
            // Access the first one.
            var temp = cache[1];
            // Add a third item.
            cache.Put(3, "test3");
            var result1 = cache[1];
            var result2 = cache[2];
            var result3 = cache[3];

            // The second item should have been evicted.
            Assert.AreEqual("test1", result1);
            Assert.AreEqual(default(string), result2);
            Assert.AreEqual("test3", result3);
        }

        
        /// <summary>
        /// Test the cache in a high-concurrency scenario
        /// </summary>
        [TestMethod]
        public void LruPutCache_HighConcurrency()
        {
            // Create a cache that can hold 100 items
            LruPutCache<int, string> cache = new LruPutCache<int, string>(100);

            // Create a queue with 1 million random key values from 0 to 199.
            Random rnd = new Random();
            ConcurrentQueue<int> queue = new ConcurrentQueue<int>();
            int totalRequests = 1000000;
            for(int i = 0; i < totalRequests; i++)
            {
                queue.Enqueue(rnd.Next(200));
            }

            // Create 50 tasks that will read from the queue of keys and 
            // query the cache to retrieve the data.
            List<Task> tasks = new List<Task>();
            int hits = 0;
            for(int i = 0; i < 50; i++)
            {
                tasks.Add(new Task(() =>
                {
                    int key = 0;
                    while (queue.TryDequeue(out key))
                    {
                        var result = cache[key];
                        if (result == default(string))
                        {
                            // If the data is not present then add it.
                            cache.Put(key, "test" + key);
                        }
                        else
                        {
                            // If the data is present then make sure
                            // it's correct.
                            Assert.AreEqual("test" + key, result);
                            Interlocked.Increment(ref hits);
                        }
                    }
                }));
            }
            // Start all the tasks as simultaneously as we can.
            foreach(var task in tasks)
            {
                task.Start();
            }
            
            // Wait for all the tasks to finish.
            Task.WaitAll(tasks.ToArray());
            // Check that all the tasks completed successfully.
            foreach(var task in tasks)
            {
                Assert.IsTrue(task.IsCompletedSuccessfully, 
                    "Error in task: " + (task.Exception == null ? 
                    "unknown" : task.Exception.ToString()));
            }
            // Check that there were a reasonable number of hits.
            // It should be approx 50% but is random so we leave a large
            // margin of error and go for 10%. 
            // If it's below this then something is definitely wrong.
            Assert.IsTrue(hits > totalRequests / 10, 
                $"Expected number of cache hits to be at least 10% but was " +
                $"actually {((float)hits / totalRequests) * 100}%");
        }

        /// <summary>
        /// Check that a cache configured to not replace existing items does
        /// not do it if an item with an existing key is added.
        /// </summary>
        [TestMethod]
        public void LruPutCache_DontReplace()
        {
            // Create a cache. Use a size of two to rule out the case where the
            // second add removes the first by the LRU rules
            var cache = new LruPutCacheBuilder()
                .SetUpdateExisting(false)
                .Build<int, string>(2);
            // Add an item to the cache
            cache.Put(1, "test");
            // Add another item to the cache using the same key
            cache.Put(1, "replacement");
            // Get
            var result = cache[1];
            // Check
            Assert.AreEqual(
                "test", result,
                "The existing value was overwritten in the cache");
        }

        /// <summary>
        /// Check that a cache configured to not replace existing items does so
        /// if an item with an existing key is added.
        /// </summary>
        [TestMethod]
        public void LruPutCache_Replace()
        {
            // Create a cache. Use a size of two to rule out the case where the
            // second add removes the first by the LRU rules
            var cache = new LruPutCacheBuilder()
                .SetUpdateExisting(true)
                .Build<int, string>(2);
            // Add an item to the cache
            cache.Put(1, "test");
            // Add another item to the cache using the same key
            cache.Put(1, "replacement");
            // Get
            var result = cache[1];
            // Check
            Assert.AreEqual(
                "replacement", result,
                "The existing value was not overwritten in the cache");
        }

        /// <summary>
        /// Check that a cache configured to not replace existing items 
        /// works correctly when the cache uses multiple internal lists.
        /// (These lists are used to mitigate contention during concurrent access)
        /// </summary>
        [TestMethod]
        public void LruPutCache_Replace_MultipleLists()
        {
            // Create a cache. Use a size of two to rule out the case where the
            // second add removes the first by the LRU rules
            var cache = new LruPutCacheBuilder()
                .SetUpdateExisting(true)
                // Make sure there are at least 4 internal lists.
                .SetConcurrency(4)
                .Build<int, string>(2);
            // Add an item to the cache
            cache.Put(1, "test");
            // Replace the item many times. This is done to ensure that 
            // different lists are accessed.
            for (int i = 0; i <= 100; i++)
            {
                // Add another item to the cache using the same key
                cache.Put(1, $"replacement {i}");
            }
            // Get
            var result = cache[1];
            // Check
            Assert.AreEqual(
                "replacement 100", result,
                "The existing value was not overwritten in the cache");
        }

        /// <summary>
        /// Test the Time-based LRU cache feature.
        /// Check if the item expiring will result in the default value 
        /// being returned.
        /// </summary>
        [TestMethod]
        public void TlruPutCache_Expired()
        {
            // Create the cache. Set item lifetime to 1 tick so that it 
            // expires between adding the item and getting it.
            var cache = new LruPutCacheBuilder()
                .SetItemLifetime(TimeSpan.FromTicks(1))
                .Build<int, string>(2);

            cache.Put(1, "Test Value");
            Thread.Sleep(1);
            var result = cache[1];

            Assert.IsNull(result);
        }

        /// <summary>
        /// Test the Time-based LRU cache feature.
        /// Check if the existing item will be returned if it has not expired.
        /// </summary>
        [TestMethod]
        public void TlruLoadingCache_NotExpired()
        {
            // Create the cache. Set item lifetime to 1 day so that it 
            // does not expire between adding the item and getting it.
            var cache = new LruPutCacheBuilder()
                .SetItemLifetime(TimeSpan.FromDays(1))
                .Build<int, string>(2);

            cache.Put(1, "Test Value");
            Thread.Sleep(1);
            var result = cache[1];

            Assert.AreEqual("Test Value", result);
        }

    }
}
