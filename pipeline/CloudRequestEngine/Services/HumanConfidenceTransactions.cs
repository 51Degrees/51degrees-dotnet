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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;

namespace FiftyOne.Pipeline.CloudRequestEngine.Services
{
    /// <summary>
    /// The transactions the Cloud Request Engine has given pages, kept on
    /// the server by page id. The server issues a page id when it renders
    /// a page, and the page loads its client script with the page id, so
    /// every call from the page carries it and sends the page's
    /// transaction. The page sends the sealed result back with its page
    /// id, and the transaction is taken, so used once. The page id is only
    /// a handle, and the transaction is never taken from a request.
    /// <para>
    /// A page id is 128 random bits, and only a page id in that form is
    /// looked up. At most <see cref="Capacity"/> pages are kept, each for
    /// the lifetime given, and when every place is taken the oldest gives
    /// way. Everything is done under one lock, in the order pages
    /// were issued. The store is in this process alone.
    /// </para>
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/human-confidence.md#the-transaction">Specification</see>
    /// </summary>
    internal sealed class HumanConfidenceTransactions : IDisposable
    {
        /// <summary>
        /// How long a page's transaction is kept by default.
        /// </summary>
        internal static readonly TimeSpan LIFETIME = TimeSpan.FromSeconds(
            Constants.HUMAN_CONFIDENCE_PAGE_LIFETIME_SECONDS_DEFAULT);

        /// <summary>
        /// The most pages kept at once by default, so pages that never send
        /// a sealed result cannot use up the server's memory.
        /// </summary>
        internal const int CAPACITY =
            Constants.HUMAN_CONFIDENCE_PAGE_CAPACITY_DEFAULT;

        private const string HEX_DIGITS = "0123456789abcdef";

        private sealed class Page
        {
            public Page(string id, string transaction, long expires)
            {
                Id = id;
                Transaction = transaction;
                Expires = expires;
            }

            public string Id { get; }
            public string Transaction { get; }
            public long Expires { get; }
        }

        /// <summary>
        /// The pages in the order they were issued, which is the order
        /// their time runs out in, with an index by page id. Both are only
        /// used under <see cref="_lock"/>.
        /// </summary>
        private readonly LinkedList<Page> _order = new LinkedList<Page>();

        private readonly Dictionary<string, LinkedListNode<Page>> _pages =
            new Dictionary<string, LinkedListNode<Page>>(StringComparer.Ordinal);

        private readonly object _lock = new object();

        private readonly RandomNumberGenerator _random =
            RandomNumberGenerator.Create();

        private readonly Func<long> _now;

        private readonly long _lifetime;

        /// <summary>
        /// Keeps transactions against the time of
        /// <see cref="Stopwatch.GetTimestamp"/>, with the default capacity
        /// and lifetime.
        /// </summary>
        public HumanConfidenceTransactions()
            : this(CAPACITY, LIFETIME)
        {
        }

        /// <summary>
        /// Keeps transactions against the time of
        /// <see cref="Stopwatch.GetTimestamp"/>.
        /// </summary>
        /// <param name="capacity">The most pages kept at once.</param>
        /// <param name="lifetime">How long a page is kept.</param>
        public HumanConfidenceTransactions(int capacity, TimeSpan lifetime)
            : this(Stopwatch.GetTimestamp, Stopwatch.Frequency, capacity, lifetime)
        {
        }

        /// <summary>
        /// Keeps transactions against the clock given, for tests.
        /// </summary>
        /// <param name="now">The time now, in ticks of the clock.</param>
        /// <param name="ticksPerSecond">The ticks of the clock in a second.</param>
        /// <param name="capacity">The most pages kept at once.</param>
        /// <param name="lifetime">How long a page is kept.</param>
        internal HumanConfidenceTransactions(
            Func<long> now,
            long ticksPerSecond,
            int capacity = CAPACITY,
            TimeSpan? lifetime = null)
        {
            _now = now;
            Capacity = capacity;
            _lifetime = (long)((lifetime ?? LIFETIME).TotalSeconds * ticksPerSecond);
        }

        /// <summary>
        /// The most pages kept at once.
        /// </summary>
        internal int Capacity { get; }

        /// <summary>
        /// The number of pages kept now.
        /// </summary>
        internal int Count
        {
            get
            {
                lock (_lock)
                {
                    return _pages.Count;
                }
            }
        }

        /// <summary>
        /// The number of entries in the issue order, which is never more
        /// than <see cref="Count"/>.
        /// </summary>
        internal int OrderCount
        {
            get
            {
                lock (_lock)
                {
                    return _order.Count;
                }
            }
        }

        /// <summary>
        /// True for a page id in the form this store issues.
        /// </summary>
        /// <param name="id">The page id.</param>
        /// <returns>True if the page id can be looked up.</returns>
        internal static bool IsPageId(string id)
        {
            if (id == null || id.Length != 32)
            {
                return false;
            }
            foreach (var c in id)
            {
                if ((c < '0' || c > '9') && (c < 'a' || c > 'f'))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Issues a new page id with a new transaction, and keeps the
        /// transaction for <see cref="LIFETIME"/>.
        /// </summary>
        /// <returns>The page id.</returns>
        public string Issue()
        {
            var id = RandomHex();
            var transaction = RandomHex();
            lock (_lock)
            {
                var now = _now();
                DropExpired(now);
                _pages[id] = _order.AddLast(
                    new Page(id, transaction, now + _lifetime));
                while (_pages.Count > Capacity)
                {
                    Remove(_order.First);
                }
            }
            return id;
        }

        /// <summary>
        /// The transaction kept for the page, which a call from the page
        /// sends so the cloud service binds the sealed result it makes to
        /// it, or null where none is kept.
        /// </summary>
        /// <param name="id">The page id the call carries.</param>
        /// <returns>The transaction, or null.</returns>
        public string Find(string id)
        {
            if (IsPageId(id) == false)
            {
                return null;
            }
            lock (_lock)
            {
                DropExpired(_now());
                return _pages.TryGetValue(id, out var node)
                    ? node.Value.Transaction
                    : null;
            }
        }

        /// <summary>
        /// Takes the transaction kept for the page, so it can be used for
        /// one redemption only.
        /// </summary>
        /// <param name="id">The page id the request carries.</param>
        /// <param name="transaction">The transaction, or null.</param>
        /// <returns>
        /// True when a transaction was kept for the page and had not
        /// expired.
        /// </returns>
        public bool TryTake(string id, out string transaction)
        {
            transaction = null;
            if (IsPageId(id) == false)
            {
                return false;
            }
            lock (_lock)
            {
                DropExpired(_now());
                if (_pages.TryGetValue(id, out var node) == false)
                {
                    return false;
                }
                Remove(node);
                transaction = node.Value.Transaction;
                return true;
            }
        }

        /// <summary>
        /// Drops every page whose time has run out, from the front of the
        /// issue order. Called under the lock.
        /// </summary>
        private void DropExpired(long now)
        {
            while (_order.First != null && _order.First.Value.Expires <= now)
            {
                Remove(_order.First);
            }
        }

        /// <summary>
        /// Removes a page from the order and the index. Called under the
        /// lock.
        /// </summary>
        private void Remove(LinkedListNode<Page> node)
        {
            _order.Remove(node);
            _pages.Remove(node.Value.Id);
        }

        /// <summary>
        /// 128 random bits, written as 32 lower case hexadecimal
        /// characters.
        /// </summary>
        private string RandomHex()
        {
            var bytes = new byte[16];
            lock (_random)
            {
                _random.GetBytes(bytes);
            }
            var text = new char[bytes.Length * 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                text[i * 2] = HEX_DIGITS[bytes[i] >> 4];
                text[i * 2 + 1] = HEX_DIGITS[bytes[i] & 0xF];
            }
            return new string(text);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _random.Dispose();
        }
    }
}
