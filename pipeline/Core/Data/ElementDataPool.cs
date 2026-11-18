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
using System.Collections.Concurrent;
using System.Threading;

namespace FiftyOne.Pipeline.Core.Data
{
    /// <summary>
    /// A bounded pool of Element Data instances for one Flow Element. A
    /// request takes an instance from the pool when there is one, and the
    /// instance goes back when the Flow Data that held it is disposed.
    /// When the pool is full, a returned instance is left for the garbage
    /// collector.
    /// </summary>
    internal sealed class ElementDataPool
    {
        /// <summary>
        /// The most instances the pool holds: at least 64, or eight for
        /// each processor.
        /// </summary>
        internal static readonly int Capacity =
            Math.Max(64, 8 * Environment.ProcessorCount);

        private readonly ConcurrentBag<ElementDataBase> _items =
            new ConcurrentBag<ElementDataBase>();

        private int _count;

        /// <summary>
        /// Take an instance from the pool, if there is one.
        /// </summary>
        public bool TryTake(out ElementDataBase item)
        {
            if (_items.TryTake(out item))
            {
                Interlocked.Decrement(ref _count);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Put an instance back, unless the pool is full.
        /// </summary>
        public void Return(ElementDataBase item)
        {
            if (Interlocked.Increment(ref _count) > Capacity)
            {
                Interlocked.Decrement(ref _count);
                return;
            }
            _items.Add(item);
        }
    }
}
