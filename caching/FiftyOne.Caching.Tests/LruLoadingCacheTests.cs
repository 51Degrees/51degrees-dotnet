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
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace FiftyOne.Caching.Tests
{
    /// <summary>
    /// Tests targeting the <see cref="LruLoadingCache{K, V}"/>.
    /// </summary>
    [TestClass]
    public class LruLoadingCacheTests
    {
        /// <summary>
        /// Test the 'LoadingCache' functionality of the LruCache when
        /// the loader is passed at construction time.
        /// </summary>
        [TestMethod]
        public void LruLoadingCache_Get()
        {
            Mock<IValueLoader<int, string>> loader =
                new Mock<IValueLoader<int, string>>();
            // Configure the loader to return 'test' for key '1'.
            loader.Setup(l => l.Load(1)).Returns("test");
            // Create the cache, passing in the loader.
            LruLoadingCache<int, string> cache =
                new LruLoadingCache<int, string>(2, loader.Object);

            var result = cache[1];

            Assert.AreEqual("test", result);
        }

        /// <summary>
        /// Test the 'LoadingCache' functionality of the LruCache when
        /// the loader is passed to the indexer.
        /// </summary>
        [TestMethod]
        public void LruLoadingCache_Get2()
        {
            Mock<IValueLoader<int, string>> loader =
                new Mock<IValueLoader<int, string>>();
            // Configure the loader to return 'test' for key '1'.
            loader.Setup(l => l.Load(1)).Returns("test");
            LruLoadingCache<int, string> cache =
                new LruLoadingCache<int, string>(2);

            // Access the cache, passing in the loader.
            var result = cache[1, loader.Object];

            Assert.AreEqual("test", result);
        }

        /// <summary>
        /// Test the Time-based LRU cache feature.
        /// Check if the item expiring will result in a new value being 
        /// requested from the loader.
        /// </summary>
        [TestMethod]
        public void TlruLoadingCache_Expired()
        {
            Mock<IValueLoader<int, string>> loader =
                new Mock<IValueLoader<int, string>>();
            // Configure the loader to return a value corresponding to the 
            // number of times the loader has been accessed.
            int counter = 0;
            loader.Setup(l => l.Load(1)).Returns(() =>
            {
                counter++;
                return counter.ToString();
            });
            // Create the cache. Set item lifetime to 1 tick so that it 
            // expires between the first and second request.
            LruLoadingCache<int, string> cache =
                new LruLoadingCache<int, string>(2, loader.Object, 1, TimeSpan.FromTicks(1));

            var result1 = cache[1];
            Thread.Sleep(1);
            var result2 = cache[1];

            Assert.AreEqual("1", result1);
            Assert.AreEqual("2", result2);
        }

        /// <summary>
        /// Test the Time-based LRU cache feature.
        /// Check if the existing item will be returned if it has not expired.
        /// </summary>
        [TestMethod]
        public void TlruLoadingCache_NotExpired()
        {
            Mock<IValueLoader<int, string>> loader =
                new Mock<IValueLoader<int, string>>();
            // Configure the loader to return a value corresponding to the 
            // number of times the loader has been accessed.
            int counter = 0;
            loader.Setup(l => l.Load(1)).Returns(() =>
            {
                counter++;
                return counter.ToString();
            });
            // Create the cache. Set item lifetime to 1 day so that it does
            // not expire between the first and second request.
            LruLoadingCache<int, string> cache =
                new LruLoadingCache<int, string>(2, loader.Object, 1, TimeSpan.FromDays(1));

            var result1 = cache[1];
            var result2 = cache[1];

            Assert.AreEqual("1", result1);
            Assert.AreEqual("1", result2);
        }
    }
}
