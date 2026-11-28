/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2026 51 Degrees Mobile Experts Limited, Davidson House,
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

using FiftyOne.Pipeline.CloudRequestEngine.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.CloudRequestEngine.Tests
{
    /// <summary>
    /// The store of pages the Cloud Request Engine has issued, against a
    /// clock the tests move, so nothing here waits.
    /// </summary>
    [TestClass]
    public class HumanConfidenceTransactionsTests
    {
        /// <summary>
        /// Ticks of the test clock in a second.
        /// </summary>
        private const long Second = 1000;

        private static readonly long Lifetime =
            (long)HumanConfidenceTransactions.LIFETIME.TotalSeconds * Second;

        private long _now = 1;

        private HumanConfidenceTransactions NewStore() =>
            new HumanConfidenceTransactions(() => Interlocked.Read(ref _now), Second);

        [TestMethod]
        public void Defaults_MatchTheOtherLanguages()
        {
            Assert.AreEqual(TimeSpan.FromSeconds(600),
                HumanConfidenceTransactions.LIFETIME);
            var capacity = typeof(HumanConfidenceTransactions)
                .GetField(nameof(HumanConfidenceTransactions.CAPACITY),
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Static)
                .GetValue(null);
            Assert.AreEqual(10000, capacity);
        }

        [TestMethod]
        public void Issue_EachPageItsOwnIdAndTransaction()
        {
            using var store = NewStore();

            var first = store.Issue();
            var second = store.Issue();

            Assert.AreNotEqual(first, second);
            Assert.IsTrue(HumanConfidenceTransactions.IsPageId(first));
            var transaction = store.Find(first);
            Assert.AreEqual(32, transaction.Length);
            Assert.AreNotEqual(first, transaction,
                "The page id is not the transaction.");
            Assert.AreNotEqual(transaction, store.Find(second));
            Assert.AreEqual(transaction, store.Find(first),
                "Every call from the page carries the same transaction.");
            Assert.AreEqual(2, store.Count);
        }

        /// <summary>
        /// Only a page id in the form the store issues is looked up.
        /// </summary>
        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("0123456789ABCDEF0123456789ABCDEF")]
        [DataRow("0123456789abcdef0123456789abcde")]
        [DataRow("0123456789abcdef0123456789abcdef0")]
        [DataRow("0123456789abcdef0123456789abcdeg")]
        public void IsPageId_RefusesAnyOtherForm(string id)
        {
            using var store = NewStore();

            Assert.IsFalse(HumanConfidenceTransactions.IsPageId(id));
            Assert.IsNull(store.Find(id));
            Assert.IsFalse(store.TryTake(id, out _));
        }

        [TestMethod]
        public void TryTake_GivesTheTransactionOnce()
        {
            using var store = NewStore();
            var page = store.Issue();
            var kept = store.Find(page);

            Assert.IsTrue(store.TryTake(page, out var taken));
            Assert.AreEqual(kept, taken);
            Assert.IsFalse(store.TryTake(page, out var again));
            Assert.IsNull(again);
            Assert.IsNull(store.Find(page));
            Assert.AreEqual(0, store.Count);
        }

        [TestMethod]
        public void Expiry_APageRunsOutAfterItsLifetime()
        {
            using var store = NewStore();
            var page = store.Issue();

            _now += Lifetime - Second;
            Assert.IsNotNull(store.Find(page));
            _now += Second;

            Assert.IsNull(store.Find(page));
            Assert.IsFalse(store.TryTake(page, out _));
            Assert.AreEqual(0, store.Count);
        }

        /// <summary>
        /// Pages whose time has run out are dropped when the store is next
        /// used, and a page still in its time is not.
        /// </summary>
        [TestMethod]
        public void Sweep_DropsExpiredPagesOnly()
        {
            using var store = NewStore();
            for (var i = 0; i < 100; i++)
            {
                store.Issue();
            }
            _now += Lifetime / 2;
            var later = store.Issue();
            _now += Lifetime / 2;

            store.Issue();

            Assert.AreEqual(2, store.Count);
            Assert.AreEqual(2, store.OrderCount);
            Assert.IsTrue(store.TryTake(later, out _));
        }

        /// <summary>
        /// When every place is taken, the oldest page gives way, so a new
        /// page is always issued and the store never grows past its
        /// capacity.
        /// </summary>
        [TestMethod]
        public void Capacity_TheOldestGivesWay()
        {
            using var store = NewStore();
            var pages = Enumerable.Range(
                0, HumanConfidenceTransactions.CAPACITY + 10)
                .Select(_ => store.Issue())
                .ToList();

            Assert.AreEqual(HumanConfidenceTransactions.CAPACITY, store.Count);
            Assert.AreEqual(HumanConfidenceTransactions.CAPACITY, store.OrderCount);
            foreach (var oldest in pages.Take(10))
            {
                Assert.IsFalse(store.TryTake(oldest, out _));
            }
            Assert.IsTrue(store.TryTake(pages[10], out _));
            Assert.IsTrue(store.TryTake(pages.Last(), out _));
        }

        /// <summary>
        /// A page taken leaves the issue order at once, so issuing and
        /// redeeming page after page never grows the store.
        /// </summary>
        [TestMethod]
        public void IssueAndTake_NeverGrowsTheOrder()
        {
            using var store = NewStore();

            for (var i = 0; i < 3 * HumanConfidenceTransactions.CAPACITY; i++)
            {
                Assert.IsTrue(store.TryTake(store.Issue(), out _));
            }

            Assert.AreEqual(0, store.Count);
            Assert.AreEqual(0, store.OrderCount);
        }

        /// <summary>
        /// Issue, Find and TryTake from many threads at once, with the
        /// clock moving, never give a live page's transaction away twice,
        /// never lose a live page, and leave the order and the index the
        /// same size.
        /// </summary>
        [TestMethod]
        public void Concurrent_IssueFindAndTake_StayConsistent()
        {
            using var store = NewStore();
            var taken = new ConcurrentBag<string>();
            var lost = 0;

            Parallel.For(0, 20000,
                new ParallelOptions { MaxDegreeOfParallelism = 8 },
                i =>
                {
                    var page = store.Issue();
                    if (store.Find(page) == null)
                    {
                        Interlocked.Increment(ref lost);
                    }
                    if (i % 2 == 0 && store.TryTake(page, out var transaction))
                    {
                        taken.Add(transaction);
                    }
                    if (i % 500 == 0)
                    {
                        Interlocked.Add(ref _now, Lifetime / 50);
                    }
                });

            Assert.AreEqual(0, lost, "A page just issued was dropped.");
            Assert.AreEqual(taken.Count, taken.Distinct().Count());
            Assert.AreEqual(store.Count, store.OrderCount);
            Assert.IsLessThanOrEqualTo(HumanConfidenceTransactions.CAPACITY, store.Count);
        }
    }
}
