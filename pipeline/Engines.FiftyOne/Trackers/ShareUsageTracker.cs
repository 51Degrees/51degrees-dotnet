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

using FiftyOne.Caching;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.FiftyOne.Data;
using FiftyOne.Pipeline.Engines.Trackers;
using System;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Trackers
{
    /// <summary>
    /// A tracker used by share usage to attempt to avoid repeatedly 
    /// sending data relating to the same user session.
    /// Evidence that matches an entry is shared again once the interval
    /// has passed since it was last shared, so a repeat is shared at most
    /// once in each interval.
    /// </summary>
    public class ShareUsageTracker : TrackerBase<DateTime?>
    {
        /// <summary>
        /// When evidence was last shared. The cache keeps the first value
        /// it is given for a key, so the time is held here and changed in
        /// place when a repeat is shared.
        /// </summary>
        private sealed class SharedTime
        {
            /// <summary>
            /// A boxed time or null, so that reads and writes are atomic.
            /// </summary>
            private volatile object _time;

            internal SharedTime(DateTime? time)
            {
                _time = time;
            }

            internal DateTime? Time
            {
                get => (DateTime?)_time;
                set => _time = value;
            }
        }

        /// <summary>
        /// The time each entry was last shared.
        /// </summary>
        private readonly IPutCache<DataKey, SharedTime> _sharedTimes;

        /// <summary>
        /// The filter that defines which evidence keys to use in the 
        /// tracker.
        /// </summary>
        private IEvidenceKeyFilter _filter;

		/// <summary>
		/// How long after evidence is shared a repeat is not shared.
		/// </summary>
		private TimeSpan _interval;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="configuration">
        /// The cache configuration to use when building the internal cache
        /// used by this tracker
        /// </param>
        /// <param name="filter">
        /// The <see cref="IEvidenceKeyFilter"/> that defines the evidence
        /// values to use when creating a key from an <see cref="IFlowData"/>.
        /// </param>
        /// <param name="interval">
        /// How long after evidence is shared a repeat of it is not shared.
        /// A repeat after this interval is shared, and starts a new one.
        /// </param>
        public ShareUsageTracker(
            CacheConfiguration configuration,
			TimeSpan interval,
            IEvidenceKeyFilter filter) 
            : base(Smallest(configuration))
        {
			_interval = interval;
            _filter = filter;
            _sharedTimes = configuration.Builder.Build<DataKey, SharedTime>(
                configuration.Size) as IPutCache<DataKey, SharedTime>;
        }

        /// <summary>
        /// The base class builds a cache that this tracker never reads, as
        /// the times are held in the shared times field, so it is given
        /// the smallest cache.
        /// </summary>
        private static CacheConfiguration Smallest(
            CacheConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }
            return new CacheConfiguration()
            {
                Builder = configuration.Builder,
                Size = 1
            };
        }

        /// <summary>
        /// Keeps the check of a repeat and the storing of the time it is
        /// shared as one step, so that evidence tracked by several threads
        /// at once is shared once.
        /// </summary>
        private readonly object _trackLock = new object();

        /// <summary>
        /// Track the specified <see cref="IFlowData"/> instance.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> to track.
        /// </param>
        /// <returns>
        /// True if the evidence is new or was last shared at least the
        /// interval ago, false otherwise.
        /// </returns>
        public override bool Track(IFlowData data)
        {
            lock (_trackLock)
            {
                return base.Track(data);
            }
        }

        /// <summary>
        /// Get the time the evidence in the supplied data was last shared.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> to use as a key.
        /// </param>
        /// <returns>
        /// The time, or null if the evidence is not in the tracker.
        /// </returns>
#pragma warning disable CA1043 // Use Integral Or String Argument For Indexers
        public override DateTime? this[IFlowData data]
#pragma warning restore CA1043 // Use Integral Or String Argument For Indexers
        {
            get
            {
                if (data == null)
                {
                    throw new ArgumentNullException(nameof(data));
                }
                return _sharedTimes[GenerateKey(data)]?.Time;
            }
        }

        /// <summary>
        /// Set the time the evidence in the supplied data was last shared.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> to use as a key.
        /// </param>
        /// <param name="value">
        /// The time to store.
        /// </param>
        public override void Put(IFlowData data, DateTime? value)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            var key = GenerateKey(data);
            var existing = _sharedTimes[key];
            if (existing == null)
            {
                _sharedTimes.Put(key, new SharedTime(value));
            }
            else
            {
                existing.Time = value;
            }
        }

        /// <summary>
        /// Get the evidence filter used to create keys for this tracker. 
        /// </summary>
        /// <returns>
        /// The <see cref="IEvidenceKeyFilter"/> to use.
        /// </returns>
        protected override IEvidenceKeyFilter GetFilter()
        {
            return _filter;
        }

        /// <summary>
        /// The key for the supplied <see cref="IFlowData"/>, made from the
        /// values usage sharing would share, which include the values of
        /// the web request where the evidence holds it.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> to make the key from.
        /// </param>
        /// <returns>
        /// The key.
        /// </returns>
        protected override DataKey GenerateKey(IFlowData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            var evidence = data.GetEvidence()?.AsDictionary();
            if (evidence == null ||
                ShareUsageEvidence.HasRequest(evidence) == false)
            {
                return base.GenerateKey(data);
            }
            var builder = new DataKeyBuilder();
            foreach (var entry in ShareUsageEvidence.Find(evidence))
            {
                if (_filter.Include(entry.Key))
                {
                    builder.Add(
                        _filter.Order(entry.Key) ?? int.MinValue,
                        entry.Key,
                        entry.Value);
                }
            }
            return builder.Build();
        }

        /// <summary>
        /// Called when the flow data matches an existing entry in the tracker
        /// </summary>
        /// <param name="data">
        /// The flow data
        /// </param>
        /// <param name="value">
        /// The time the evidence was last shared.
        /// </param>
        /// <returns>
        /// True if the interval has passed since the evidence was last
        /// shared, in which case the time it is shared now is stored.
        /// False otherwise.
        /// </returns>
        protected override bool Match(IFlowData data, DateTime? value)
        {
			var now = DateTime.UtcNow;
			if (value <= (now - _interval))
			{
				Put(data, now);
				return true;
			}
			else
				return false;
        }

        /// <summary>
        /// Called when a new instance of this tracker's meta-data is needed
        /// to add to the tracker.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> that is being added to the tracker.
        /// </param>
        /// <returns>
        /// The time now, as the evidence is about to be shared.
        /// </returns>
        protected override DateTime? NewValue(IFlowData data)
        {
            return DateTime.UtcNow;
        }

        /// <summary>
        /// Dispose of the caches holding the entries.
        /// </summary>
        /// <param name="disposing">
        /// True if this is called from the Dispose method.
        /// False if it is called from the finalizer.
        /// </param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _sharedTimes.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
