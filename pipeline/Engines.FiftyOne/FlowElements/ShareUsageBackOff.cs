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

namespace FiftyOne.Pipeline.Engines.FiftyOne.FlowElements
{
    /// <summary>
    /// Decides when usage sharing may send. Sending pauses for the initial
    /// delay once the set number of sends fail within the window. A failed
    /// send after a pause multiplies the delay, up to the maximum, and a
    /// successful send ends the back-off. Only sending pauses, so evidence
    /// is still queued while paused.
    /// Each method takes the time from the caller.
    /// </summary>
    internal sealed class ShareUsageBackOff
    {
        private readonly object _lock = new object();
        private readonly int _failuresToEnterRecovery;
        private readonly TimeSpan _failuresWindow;
        private readonly double _initialDelaySeconds;
        private readonly double _maxDelaySeconds;
        private readonly double _multiplier;

        /// <summary>
        /// When each recent failed send happened, oldest first. Empty
        /// while backing off.
        /// </summary>
        private readonly Queue<DateTime> _failures = new Queue<DateTime>();

        /// <summary>
        /// The length of the current pause, or zero when not backing off.
        /// </summary>
        private TimeSpan _delay = TimeSpan.Zero;

        /// <summary>
        /// When sending may start again.
        /// </summary>
        private DateTime _resumeAt = DateTime.MinValue;

        internal ShareUsageBackOff(
            int failuresToEnterRecovery,
            int failuresWindowSeconds,
            double initialDelaySeconds,
            double maxDelaySeconds,
            double multiplier)
        {
            if (failuresToEnterRecovery < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(failuresToEnterRecovery),
                    failuresToEnterRecovery,
                    "Must be 1 or more.");
            }
            if (failuresWindowSeconds < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(failuresWindowSeconds),
                    failuresWindowSeconds,
                    "Must be 1 or more.");
            }
            if ((initialDelaySeconds > 0) == false)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(initialDelaySeconds),
                    initialDelaySeconds,
                    "Must be more than 0.");
            }
            if ((maxDelaySeconds > 0) == false)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxDelaySeconds),
                    maxDelaySeconds,
                    "Must be more than 0.");
            }
            if ((multiplier >= 1) == false)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(multiplier),
                    multiplier,
                    "Must be 1 or more.");
            }
            _failuresToEnterRecovery = failuresToEnterRecovery;
            _failuresWindow = TimeSpan.FromSeconds(failuresWindowSeconds);
            _initialDelaySeconds = initialDelaySeconds;
            _maxDelaySeconds = maxDelaySeconds;
            _multiplier = multiplier;
        }

        /// <summary>
        /// True from the start of a pause until a send succeeds.
        /// </summary>
        internal bool BackingOff
        {
            get { lock (_lock) { return _delay > TimeSpan.Zero; } }
        }

        /// <summary>
        /// The time left before sending may start again, or zero if it may
        /// start now.
        /// </summary>
        internal TimeSpan Remaining(DateTime now)
        {
            lock (_lock)
            {
                return _resumeAt > now ? _resumeAt - now : TimeSpan.Zero;
            }
        }

        /// <summary>
        /// Records a failed send.
        /// </summary>
        /// <returns>
        /// The new pause if this failure started or lengthened one,
        /// otherwise zero.
        /// </returns>
        internal TimeSpan Failed(DateTime now)
        {
            lock (_lock)
            {
                if (_delay > TimeSpan.Zero)
                {
                    // The send tried when the last pause ended failed.
                    _delay = TimeSpan.FromSeconds(Math.Min(
                        _delay.TotalSeconds * _multiplier,
                        _maxDelaySeconds));
                }
                else
                {
                    _failures.Enqueue(now);
                    while (_failures.Peek() < now - _failuresWindow)
                    {
                        _failures.Dequeue();
                    }
                    if (_failures.Count < _failuresToEnterRecovery)
                    {
                        return TimeSpan.Zero;
                    }
                    _failures.Clear();
                    _delay = TimeSpan.FromSeconds(Math.Min(
                        _initialDelaySeconds,
                        _maxDelaySeconds));
                }
                _resumeAt = now + _delay;
                return _delay;
            }
        }

        /// <summary>
        /// Records a successful send.
        /// </summary>
        /// <returns>
        /// True if this ended a back-off.
        /// </returns>
        internal bool Succeeded()
        {
            lock (_lock)
            {
                if (_delay == TimeSpan.Zero)
                {
                    return false;
                }
                _delay = TimeSpan.Zero;
                _resumeAt = DateTime.MinValue;
                return true;
            }
        }
    }
}
