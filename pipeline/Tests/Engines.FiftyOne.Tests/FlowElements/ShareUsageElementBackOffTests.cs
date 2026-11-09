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

using FiftyOne.Common.TestHelpers;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.Engines.TestHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Tests.FlowElements
{
    /// <summary>
    /// Usage sharing pauses sending after repeated failed sends, keeps
    /// queueing evidence while paused, and drains the queue once a send
    /// succeeds. The tests measure time, so they do not run alongside
    /// others.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class ShareUsageElementBackOffTests
    {
        /// <summary>
        /// The longest a test waits for something it expects to happen.
        /// </summary>
        private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Answers every request with the status the test sets, and
        /// records when each request arrived and the client IPs of the
        /// requests it accepted.
        /// </summary>
        private class RecordingHandler : HttpMessageHandler
        {
            private readonly object _lock = new object();
            private readonly Stopwatch _clock = Stopwatch.StartNew();
            private readonly List<TimeSpan> _times = new List<TimeSpan>();
            private readonly List<string> _accepted = new List<string>();
            private volatile int _status = (int)HttpStatusCode.InternalServerError;

            public HttpStatusCode Status
            {
                get => (HttpStatusCode)_status;
                set => _status = (int)value;
            }

            public TimeSpan Now => _clock.Elapsed;

            public int Count { get { lock (_lock) { return _times.Count; } } }

            public TimeSpan[] Times
            {
                get { lock (_lock) { return _times.ToArray(); } }
            }

            public string[] Accepted
            {
                get { lock (_lock) { return _accepted.ToArray(); } }
            }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                var status = Status;
                var body = await request.Content.ReadAsByteArrayAsync()
                    .ConfigureAwait(false);
                lock (_lock)
                {
                    _times.Add(_clock.Elapsed);
                    if (status == HttpStatusCode.OK)
                    {
                        _accepted.AddRange(ClientIps(body));
                    }
                }
                return new HttpResponseMessage(status)
                {
                    Content = new StringContent("")
                };
            }

            private static IEnumerable<string> ClientIps(byte[] body)
            {
                using (var input = new MemoryStream(body))
                using (var gzip = new GZipStream(
                    input, CompressionMode.Decompress))
                using (var reader = new StreamReader(gzip))
                {
                    return System.Text.RegularExpressions.Regex.Matches(
                            reader.ReadToEnd(),
                            "<ClientIP>([^<]*)</ClientIP>")
                        .Cast<System.Text.RegularExpressions.Match>()
                        .Select(m => m.Groups[1].Value)
                        .ToList();
                }
            }
        }

        private RecordingHandler _handler;
        private TestLoggerFactory _loggerFactory;
        private ShareUsageElement _element;
        private int _requests;

        [TestInitialize]
        public void Init()
        {
            _handler = new RecordingHandler();
            _loggerFactory = new TestLoggerFactory();
            _requests = 0;
        }

        [TestCleanup]
        public void Cleanup()
        {
            _element?.Dispose();
        }

        /// <summary>
        /// Builds the element the way a user does, sending each entry on
        /// its own. The failures window is long enough that every failure
        /// in a test counts.
        /// </summary>
        private ShareUsageElement Build(
            int failuresToEnterRecovery,
            double initialDelaySeconds,
            double maxDelaySeconds,
            int maximumQueueSize = 100)
        {
            var pipeline = new Mock<IPipeline>();
            pipeline.Setup(p => p.FlowElements).Returns(
                new ReadOnlyCollection<IFlowElement>(
                    new List<IFlowElement>()));
            var element = new ShareUsageBuilder(
                    _loggerFactory,
                    new HttpClient(_handler))
                .SetMinimumEntriesPerMessage(1)
                .SetMaximumQueueSize(maximumQueueSize)
                .SetFailuresToEnterRecovery(failuresToEnterRecovery)
                .SetFailuresWindowSeconds(100)
                .SetInitialRecoveryDelaySeconds(initialDelaySeconds)
                .SetMaxRecoveryDelaySeconds(maxDelaySeconds)
                .SetRecoveryMultiplier(2)
                .SetShareUsageUrl("http://localhost/usage")
                .Build();
            element.AddPipeline(pipeline.Object);
            return element;
        }

        /// <summary>
        /// Processes a request whose evidence no other request has, so
        /// the element shares it, and returns its client IP.
        /// </summary>
        private string Process()
        {
            var number = _requests++;
            var ip = $"10.0.{number / 250}.{number % 250 + 1}";
            var data = MockFlowData.CreateFromEvidence(
                new Dictionary<string, object>()
                {
                    { Core.Constants.EVIDENCE_CLIENTIP_KEY, ip },
                    {
                        Core.Constants.EVIDENCE_HTTPHEADER_PREFIX +
                            Core.Constants.EVIDENCE_SEPERATOR +
                            "user-agent",
                        $"Test agent {ip}"
                    },
                },
                true);
            _element.Process(data.Object);
            return ip;
        }

        private static void WaitFor(Func<bool> condition, string message)
        {
            var timer = Stopwatch.StartNew();
            while (condition() == false)
            {
                if (timer.Elapsed > WaitLimit)
                {
                    Assert.Fail(message);
                }
                Thread.Sleep(20);
            }
        }

        /// <summary>
        /// Two failed sends pause sending. Nothing is sent during the
        /// pause, the evidence processed during it is queued, and all of
        /// it is sent once the pause ends and the service answers, with
        /// no more requests processed to start the sending.
        /// </summary>
        [TestMethod]
        public void ShareUsageElement_BackOff_PausesThenDrainsQueue()
        {
            const double pauseSeconds = 3;
            _element = Build(2, pauseSeconds, pauseSeconds);

            Process();
            WaitFor(() => _handler.Count == 1 && _element.IsRunning == false,
                "The first send was not made.");
            Process();
            WaitFor(() => _handler.Count == 2 && _element.IsRunning == false,
                "The second send was not made.");
            var pausedAt = _handler.Now;

            var queued = Enumerable.Range(0, 5).Select(i => Process()).ToList();
            Thread.Sleep(TimeSpan.FromSeconds(1));
            Assert.AreEqual(2, _handler.Count,
                "Sends were made during the pause that two failed sends " +
                "should have started.");

            _handler.Status = HttpStatusCode.OK;
            WaitFor(() => queued.All(ip => _handler.Accepted.Contains(ip)),
                "The evidence processed during the pause was not all sent " +
                "once the service answered. Sent: " +
                string.Join(", ", _handler.Accepted));
            var waited = _handler.Times[2] - pausedAt;
            Assert.IsTrue(
                waited >= TimeSpan.FromSeconds(pauseSeconds - 0.5),
                $"Sending resumed {waited.TotalSeconds:0.00} seconds after " +
                $"the pause started, before the {pauseSeconds} second " +
                "pause ended.");
        }

        /// <summary>
        /// Each failed send after a pause multiplies the delay, and the
        /// delay stops growing at the maximum.
        /// </summary>
        [TestMethod]
        public void ShareUsageElement_BackOff_DelayGrowsToMaximum()
        {
            _element = Build(1, 0.5, 1.0);
            for (int i = 0; i < 40; i++)
            {
                Process();
            }

            WaitFor(() => _handler.Count >= 4,
                "Fewer than four sends were made.");
            var times = _handler.Times;
            var gaps = Enumerable.Range(1, 3)
                .Select(i => (times[i] - times[i - 1]).TotalSeconds)
                .ToArray();
            var shown = string.Join(", ", gaps.Select(g => g.ToString("0.00")));
            Assert.IsGreaterThanOrEqualTo(0.4, gaps[0],
                $"The first pause was not the initial delay. Gaps: {shown}");
            Assert.IsGreaterThanOrEqualTo(0.9, gaps[1],
                $"The second pause was not doubled. Gaps: {shown}");
            Assert.IsTrue(gaps[2] >= 0.9 && gaps[2] < 1.9,
                $"The third pause was not held at the maximum. Gaps: {shown}");
        }

        /// <summary>
        /// Closing while paused returns at once rather than waiting for
        /// the pause to end, and sends nothing.
        /// </summary>
        [TestMethod]
        public void ShareUsageElement_BackOff_CloseWhilePausedDoesNotWaitOrSend()
        {
            _element = Build(1, 60, 60);
            Process();
            WaitFor(() => _handler.Count == 1 && _element.IsRunning == false,
                "The first send was not made.");
            Process();
            Process();

            var timer = Stopwatch.StartNew();
            _element.Dispose();
            timer.Stop();

            Assert.IsTrue(timer.Elapsed < TimeSpan.FromSeconds(10),
                $"Closing took {timer.Elapsed.TotalSeconds:0.0} seconds.");
            Assert.AreEqual(1, _handler.Count,
                "A send was made during the pause or when closing.");
        }

        /// <summary>
        /// When the queue fills during a pause, new evidence is dropped
        /// and what was queued first is sent once sending resumes. No
        /// warning is logged for each entry dropped.
        /// </summary>
        [TestMethod]
        public void ShareUsageElement_BackOff_FullQueueDropsNewEvidence()
        {
            _element = Build(1, 2, 2, 4);
            Process();
            WaitFor(() => _handler.Count == 1 && _element.IsRunning == false,
                "The first send was not made.");

            var kept = Enumerable.Range(0, 4).Select(i => Process()).ToList();
            var dropped = Enumerable.Range(0, 6).Select(i => Process()).ToList();
            _handler.Status = HttpStatusCode.OK;

            WaitFor(() => kept.All(ip => _handler.Accepted.Contains(ip)),
                "The evidence queued during the pause was not all sent. " +
                "Sent: " + string.Join(", ", _handler.Accepted));
            Thread.Sleep(500);
            Assert.IsFalse(
                dropped.Any(ip => _handler.Accepted.Contains(ip)),
                "Evidence processed when the queue was full was sent.");
            _loggerFactory.AssertMaxWarnings(3);
        }

        /// <summary>
        /// Evidence discarded because the queue is full is logged, but not
        /// with a message for each entry discarded.
        /// </summary>
        [TestMethod]
        public void ShareUsageElement_FullQueue_LogsWithoutAMessagePerEntry()
        {
            _element = Build(1, 2, 2, 4);
            Process();
            WaitFor(() => _handler.Count == 1 && _element.IsRunning == false,
                "The first send was not made.");

            foreach (var i in Enumerable.Range(0, 14))
            {
                Process();
            }

            var full = _loggerFactory.Loggers
                .SelectMany(l => l.WarningEntries)
                .Where(w => w.Contains("full"))
                .ToList();
            Assert.IsGreaterThanOrEqualTo(1, full.Count,
                "Discarding evidence was not logged.");
            Assert.IsLessThan(6, full.Count,
                "A message was logged for each entry discarded.");
        }
    }
}
