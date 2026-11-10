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
using FiftyOne.Pipeline.Engines.FiftyOne.Trackers;
using FiftyOne.Pipeline.Engines.TestHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Tests.Trackers
{
	[TestClass]
    public class ShareUsageTrackerTests
    {
		private TimeSpan _interval;

		/// <summary>
		/// An interval longer than any test takes, for the tests that expect a
		/// repeat to fall inside it however slowly the runner goes.
		/// </summary>
		private const int RepeatIntervalMinutes = 10;

		private IEvidenceKeyFilter _evidenceKeyFilter;

		private ShareUsageTracker _shareUsageTracker;
		private string _aspSessionCookieName;
        private List<string> _blockedHttpHeaders;
        private List<string> _includedQueryStringParameters;
        private Dictionary<string, object> _evidenceData;
		private Mock<IFlowData> _data;

		[TestInitialize]
		public void Init()
		{
			_blockedHttpHeaders = new List<string>() { };
            _includedQueryStringParameters = new List<string>();
			_aspSessionCookieName = Engines.Constants.DEFAULT_ASP_COOKIE_NAME;

			_interval = new TimeSpan(0, 0, 0, 0, 50);
			_evidenceKeyFilter = new EvidenceKeyFilterShareUsageTracker(
				_blockedHttpHeaders, _includedQueryStringParameters, true, _aspSessionCookieName);
			_shareUsageTracker = new ShareUsageTracker(new CacheConfiguration()
				{
					Builder = new LruPutCacheBuilder(),
					Size = 1000
				},
				TimeSpan.FromMinutes(RepeatIntervalMinutes),
				_evidenceKeyFilter);

			_evidenceData = new Dictionary<string, object>()
			{
				{ Core.Constants.EVIDENCE_HTTPHEADER_PREFIX + Core.Constants.EVIDENCE_SEPERATOR + "user-agent", "iPhone" }
			};

            var dataKeyBuilder = new DataKeyBuilder();
            foreach(var evidence in _evidenceData)
            {
                dataKeyBuilder.Add(100, evidence.Key, evidence.Value);
            }

			_data = MockFlowData.CreateFromEvidence(_evidenceData, false);
			_data.Setup(d => d.GenerateKey(It.IsAny<IEvidenceKeyFilter>())).Returns(dataKeyBuilder.Build());
		}

		/// <summary>
		/// Test that repeat evidence is not tracked when 2 requests are made 
		/// within the tracking interval or session timeout.
		/// </summary>
		[TestMethod]
		public void ShareUsageTracker_RepeatEvidence_BeforeSessionTimeout()
		{
			int trackedEvents = 0;
			for(var i = 0; i <2; i++)
			{
				if (_shareUsageTracker.Track(_data.Object))
					trackedEvents++;
			}
			Assert.AreEqual(1, trackedEvents);
		}

		/// <summary>
		/// Check that after a tracker interval or session timeout has elapsed, 
		/// repeat evidence is treated as a new session and is tracked.
		/// </summary>
		[TestMethod]
		public void ShareUsageTracker_RepeatEvidence_AfterSessionTimeout()
		{
			var shareUsageTracker = new ShareUsageTracker(new CacheConfiguration()
				{
					Builder = new LruPutCacheBuilder(),
					Size = 1000
				},
				_interval,
				_evidenceKeyFilter);

			int trackedEvents = 0;
			for (var i = 0; i < 2; i++)
			{
				if (shareUsageTracker.Track(_data.Object))
					trackedEvents++;

                // Wait some time equal to the interval to elapse.
				Thread.Sleep(_interval);
			}
			Assert.AreEqual(2, trackedEvents);
		}

		/// <summary>
		/// A repeat is shared at most once in each interval. Sharing a
		/// repeat after the interval starts a new interval, so a repeat
		/// straight after it is not shared.
		/// </summary>
		[TestMethod]
		public void ShareUsageTracker_RepeatEvidence_SharedOncePerInterval()
		{
			var interval = TimeSpan.FromMilliseconds(500);
			var shareUsageTracker = new ShareUsageTracker(new CacheConfiguration()
				{
					Builder = new LruPutCacheBuilder(),
					Size = 1000
				},
				interval,
				_evidenceKeyFilter);

			Assert.IsTrue(shareUsageTracker.Track(_data.Object),
				"New evidence was not shared.");
			Thread.Sleep(interval + TimeSpan.FromMilliseconds(250));
			Assert.IsTrue(shareUsageTracker.Track(_data.Object),
				"A repeat after the interval was not shared.");
			Assert.IsFalse(shareUsageTracker.Track(_data.Object),
				"A repeat straight after a repeat was shared was also " +
				"shared, so the time it was shared was not stored.");
		}

		/// <summary>
		/// The time stored is UTC, so a change of the local time zone offset
		/// cannot make a repeat look early or late.
		/// </summary>
		[TestMethod]
		public void ShareUsageTracker_RepeatEvidence_TimeIsUtc()
		{
			_shareUsageTracker.Track(_data.Object);

			Assert.AreEqual(
				DateTimeKind.Utc,
				_shareUsageTracker[_data.Object].Value.Kind);
		}

		/// <summary>
		/// A repeat inside the interval is not shared and leaves the time
		/// it was last shared as it was.
		/// </summary>
		[TestMethod]
		public void ShareUsageTracker_RepeatEvidence_InsideIntervalKeepsTime()
		{
			_shareUsageTracker.Track(_data.Object);
			var shared = _shareUsageTracker[_data.Object];
			Thread.Sleep(20);

			Assert.IsFalse(_shareUsageTracker.Track(_data.Object));

			Assert.AreEqual(shared, _shareUsageTracker[_data.Object]);
		}

		/// <summary>
		/// Evidence tracked by several threads at once is shared by one of
		/// them only. Each thread waits for the others after it starts to
		/// track, so that all of them look for the evidence together.
		/// </summary>
		[TestMethod]
		public void ShareUsageTracker_RepeatEvidence_SharedOnceAcrossThreads()
		{
			const int rounds = 3;
			const int threads = 8;
			var waitForOthers = TimeSpan.FromMilliseconds(100);
			for (var round = 0; round < rounds; round++)
			{
				var key = new DataKeyBuilder();
				key.Add(100, "header.user-agent", "agent" + round);
				var built = key.Build();
				var data = MockFlowData.CreateFromEvidence(_evidenceData, false);
				using (var arrived = new ThreadLocal<bool>())
				using (var together = new Barrier(threads))
				{
					data.Setup(d => d.GenerateKey(It.IsAny<IEvidenceKeyFilter>()))
						.Returns(() =>
						{
							if (arrived.Value == false)
							{
								arrived.Value = true;
								together.SignalAndWait(waitForOthers);
							}
							return built;
						});
					var tasks = Enumerable.Range(0, threads)
						.Select(_ => Task.Factory.StartNew(
							() => _shareUsageTracker.Track(data.Object),
							TaskCreationOptions.LongRunning))
						.ToArray();
					Task.WaitAll(tasks);

					Assert.AreEqual(
						1,
						tasks.Count(t => t.Result),
						$"Round {round} shared the same evidence more than once.");
				}
			}
		}

		/// <summary>
		/// Test that the session ID is included in the evidence when session
		/// tracking is enabled. This means that for a request containing the 
		/// same evidence but a different session ID, both will be tracked.
		/// </summary>
		[TestMethod]
		public void ShareUsageTracker_Session_Track()
		{
			var shareUsageTracker = new ShareUsageTracker(new CacheConfiguration()
			{
				Builder = new LruPutCacheBuilder(),
				Size = 1000
			},
			_interval,
			_evidenceKeyFilter);

			int trackedEvents = 0;
			for (var i = 0; i < 2; i++)
			{
				var evidenceData = new Dictionary<string, object>()
				{
					{ Core.Constants.EVIDENCE_HTTPHEADER_PREFIX + Core.Constants.EVIDENCE_SEPERATOR + "user-agent", "iPhone" },
					{ Core.Constants.EVIDENCE_COOKIE_PREFIX + Core.Constants.EVIDENCE_SEPERATOR + Engines.Constants.DEFAULT_ASP_COOKIE_NAME, i }
				};
				var data = MockFlowData.CreateFromEvidence(_evidenceData, true);
				data.Setup(d => d.GenerateKey(It.IsAny<IEvidenceKeyFilter>())).Returns(
                    (IEvidenceKeyFilter filter) =>
                    {
                        return GenerateKey(filter, evidenceData);
                    });
                if (shareUsageTracker.Track(data.Object))
					trackedEvents++;

			}

			Assert.AreEqual(2, trackedEvents);
		}

		/// <summary>
		/// Test that when session tracking is disabled, repeat evidence is 
		/// not tracked even if the session id is different.
		/// </summary>
		[TestMethod]
		public void ShareUsageTracker_Session_DoNotTrack()
		{
			var evidenceKeyFilter = new EvidenceKeyFilterShareUsageTracker(
				_blockedHttpHeaders, _includedQueryStringParameters, false, _aspSessionCookieName);
			var shareUsageTracker = new ShareUsageTracker(new CacheConfiguration()
			{
				Builder = new LruPutCacheBuilder(),
				Size = 1000
			},
			TimeSpan.FromMinutes(RepeatIntervalMinutes),
			evidenceKeyFilter);
			
			int trackedEvents = 0;
			for (var i = 0; i < 2; i++)
			{
				var evidenceData = new Dictionary<string, object>()
				{
					{ Core.Constants.EVIDENCE_HTTPHEADER_PREFIX + Core.Constants.EVIDENCE_SEPERATOR + "user-agent", "iPhone" },
                    { Core.Constants.EVIDENCE_COOKIE_PREFIX + Core.Constants.EVIDENCE_SEPERATOR + Engines.Constants.DEFAULT_ASP_COOKIE_NAME, i }
				};
				var data = MockFlowData.CreateFromEvidence(_evidenceData, true);
				data.Setup(d => d.GenerateKey(It.IsAny<IEvidenceKeyFilter>())).Returns(
                    (IEvidenceKeyFilter filter) => 
                    {
                        return GenerateKey(filter, evidenceData);
                    });
				if (shareUsageTracker.Track(data.Object))
					trackedEvents++;

			}
			Assert.AreEqual(1, trackedEvents);
		}

        public DataKey GenerateKey(IEvidenceKeyFilter filter, Dictionary<string, object> evidence)
		{
			DataKeyBuilder result = new DataKeyBuilder();
			foreach (var entry in evidence)
			{
				if (filter.Include(entry.Key))
				{
					result.Add(100, entry.Key, entry.Value);
				}
			}
			return result.Build();
		}
	}
}
