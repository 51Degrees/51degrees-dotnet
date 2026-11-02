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

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Caching.Tests.Loaders
{
    internal abstract class TrackingLoaderBase<TKey, TValue> : IValueTaskLoader<TKey, TValue>, IValueLoader<TKey, TValue>
    {
        private readonly int _delayMillis;

        private int _calls = 0;

        private int _taskCalls = 0;

        private int _cancels = 0;

        private int _completeWaits = 0;

        private readonly object _countersLock = new object();

        public int Calls { get { lock (_countersLock) { return _calls; } } }

        public int TaskCalls { get { lock (_countersLock) { return _taskCalls; } } }

        public int Cancels { get { lock (_countersLock) { return _cancels; } } }

        public int CompleteWaits { get { lock (_countersLock) { return _completeWaits; } } }

        private readonly Func<CancellationToken, CancellationToken> _taskCancellationTokenProvider;

        private readonly Func<CancellationToken, CancellationToken> _loopCancellationTokenProvider;

        public event Action<TKey> OnTaskStarted;

        public TrackingLoaderBase() : this(0)
        {

        }

        public TrackingLoaderBase(int delayMillis) : this(delayMillis, false)
        {
        }

        /// <param name="delayMillis">
        /// Delay before result is returned in ms.
        /// </param>
        /// <param name="runWithToken">
        /// Whether the canellation of the token
        /// should cancel a task (true)
        /// or the internal loop operation (false).
        /// </param>
        public TrackingLoaderBase(int delayMillis, bool runWithToken) :
            this(
                delayMillis,
                runWithToken ? null : static t => new CancellationToken(),
                runWithToken ? static t => new CancellationToken() : null)
        { 
        }

        /// <param name="delayMillis">
        /// Delay before result is returned in ms.
        /// </param>
        /// <param name="taskCancellationTokenProvider">
        /// Transforms a token to be passed for task cancellation.
        /// If is `null` or returns `null`, the token from `Load` is used.
        /// </param>
        /// <param name="loopCancellationTokenProvider">
        /// Transforms a token to be passed for a loop to track.
        /// If is `null` or returns `null`, the token from `Load` is used.
        /// </param>
        public TrackingLoaderBase(
            int delayMillis, 
            Func<CancellationToken, CancellationToken> taskCancellationTokenProvider,
            Func<CancellationToken, CancellationToken> loopCancellationTokenProvider)
        {
            _delayMillis = delayMillis;
            _taskCancellationTokenProvider = taskCancellationTokenProvider;
            _loopCancellationTokenProvider = loopCancellationTokenProvider;
        }

        public Task<TValue> Load(TKey key, CancellationToken token)
        {
            lock (_countersLock) {
                ++_calls;
            }
            var tokenForTask = _taskCancellationTokenProvider?.Invoke(token) ?? token;
            var tokenForLoop = _loopCancellationTokenProvider?.Invoke(token) ?? token;
            return Task.Run(() =>
            {
                lock (_countersLock)
                {
                    ++_taskCalls;
                }
                OnTaskStarted?.Invoke(key);
                if (_delayMillis > 0)
                {
                    var start = DateTime.Now;
                    while (DateTime.Now <= start.AddMilliseconds(_delayMillis) &&
                        tokenForLoop.IsCancellationRequested == false)
                    {
                        Thread.Sleep(1);
                    }
                    lock (_countersLock)
                    {
                        if (tokenForLoop.IsCancellationRequested)
                        {
                            ++_cancels;
                            throw new OperationCanceledException();
                        }
                        if (DateTime.Now >= start.AddMilliseconds(_delayMillis))
                        {
                            ++_completeWaits;
                        }
                    }
                }
                return GetValue(key);
            }, tokenForTask);
        }

        public TValue Load(TKey key)
        {
            lock (_countersLock)
            {
                ++_calls;
            }
            if (_delayMillis > 0)
            {
                var start = DateTime.Now;
                while (DateTime.Now < start.AddMilliseconds(_delayMillis))
                {
                    Thread.Sleep(1);
                }
            }
            return GetValue(key);
        }

        protected abstract TValue GetValue(TKey key);
    }
}
