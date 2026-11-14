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

using FiftyOne.Pipeline.Engines;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.IpIntelligence.TestHelpers.Data
{
    public class MetaDataTests
    {
        // The delay in milliseconds between calls to refresh the data
        // used by the engine.
        // The LowMemory and Balanced profiles are significantly 
        // slower so we use a longer delay.
        private static int ReloadDelay(
            PerformanceProfiles profile,
            bool inmemory)
        {
            if (inmemory) return 1600;
            switch (profile)
            {
                case PerformanceProfiles.MaxPerformance:
                case PerformanceProfiles.HighPerformance:
                    return 1600;
                case PerformanceProfiles.LowMemory:
                case PerformanceProfiles.Balanced:
                case PerformanceProfiles.BalancedTemp:
                    return 3600;
                default:
                    return 1600;
            }
        }
        // Each hash task will delay it's start by x times this value 
        // in ms.
        // Where x = the task sequence number (i.e. first task x = 0, 
        // second task x = 1, etc).
        // Therefore the last task will start after 
        // (HashTaskDelay - 1) * HashTaskDelay milliseconds
        private static int HashTaskDelay(bool inmemory)
        {
            if (inmemory) return 1000;
            else return 1000;
        }

        private static int RefreshLimit()
        {
            if (IntPtr.Size == 4)
            {
                return 1;
            }
            // Reduce the number of refreshes as 
            // the memory is being consumed too
            // quickly and result in out of memory
            return 4;
        }

        // The number of simultaneous hashing tasks to start.
        private const int HashTaskCount = 6;
        // Released once by each hashing task as it starts, so the refresh
        // task wakes for every one, however short the hashing task is.
        private readonly SemaphoreSlim _hashTaskStarted = new SemaphoreSlim(0);
        // Set when the first refresh starts. The first hashing task waits
        // for it, so at least one refresh always runs while a hashing task
        // is active, which is what the test is for.
        private readonly ManualResetEventSlim _refreshStarted =
            new ManualResetEventSlim(false);
        // The longest the first hashing task waits for the first refresh.
        private const int FirstRefreshWaitMilliseconds = 60000;



        /// <summary>
        /// Used to return results from 'hashing' tasks
        /// </summary>
        private class HashTaskResult
        {
            public int HashValue { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime FinishTime { get; set; }
        }
        /// <summary>
        /// Used to return results from the refresh task
        /// </summary>
        private class RefreshResult
        {
            public DateTime StartTime { get; set; }
            public DateTime FinishTime { get; set; }
        }


        public void Reload(IWrapper wrapper,
            IMetaDataHasher hasher,
            PerformanceProfiles profile)
        {
            var cancelRefresh = new CancellationTokenSource();
            var reloader = Refresh(wrapper, cancelRefresh.Token, profile);
            Reload(wrapper, hasher, reloader, cancelRefresh, false);
        }

        public void ReloadMemory(IWrapper wrapper,
            IMetaDataHasher hasher,
            PerformanceProfiles profile)
        {
            var masterData = File.ReadAllBytes(
                wrapper.GetEngine().GetDataFileMetaData().DataFilePath);
            CancellationTokenSource cancelRefresh = new CancellationTokenSource();
            var reloader = RefreshInMemory(wrapper, masterData, cancelRefresh.Token, profile);

            Reload(wrapper, hasher, reloader, cancelRefresh, true);
        }

        private void Reload(IWrapper wrapper,
            IMetaDataHasher hasher,
            Task<List<RefreshResult>> reloader,
            CancellationTokenSource cancelRefresh,
            bool inMemory)
        {
            IList<Task<HashTaskResult>> tasks = StartHashingTasks(
                HashTaskCount,
                inMemory,
                wrapper,
                hasher);
            HashTaskResult[] hashes = Task.WhenAll(tasks).Result;

            cancelRefresh.Cancel();
            var refreshResults = reloader.Result;

            Console.WriteLine($"Refreshed the dataset {refreshResults.Count} times.");
            ValidateResults(HashTaskCount, hashes, refreshResults);
        }

        private static void ValidateResults(
            int taskCount,
            HashTaskResult[] hashes,
            List<RefreshResult> refreshResults)
        {

            for (int i = 0; i < taskCount - 1; i++)
            {
                Assert.AreEqual(hashes[i].HashValue,
                    hashes[i + 1].HashValue,
                    "Hashes were not equal");
            }
            Assert.IsTrue(refreshResults.Count > 0,
                "No refresh ran while the hashing tasks were active.");
            var avgHashTime = hashes
                .Average(h => (h.FinishTime - h.StartTime).TotalMilliseconds);
            var avgRefreshTime = refreshResults
                .Average(h => (h.FinishTime - h.StartTime).TotalMilliseconds);

            var refreshesDuringActiveHashTasks = refreshResults.Where(r =>
                hashes.Any(h => (r.StartTime > h.StartTime &&
                    r.StartTime < h.FinishTime) ||
                    (r.FinishTime > h.StartTime &&
                    r.FinishTime < h.FinishTime)));
            Console.WriteLine($"{refreshesDuringActiveHashTasks.Count()} " +
                $"refreshes during active hash tasks");
            Assert.IsGreaterThan(0,
                refreshesDuringActiveHashTasks.Count(),
                "At least 1 refresh needs to occur while hash tasks are " +
                "active in order for this test to be valid. Check the " +
                "ReloadDelay and HashTaskDelay settings. " +
                $"Average hash task time = {avgHashTime} ms. " +
                $"Average refresh task time = {avgRefreshTime} ms.");
        }

        private IList<Task<HashTaskResult>> StartHashingTasks(
            int taskCount,
            bool inMemory,
            IWrapper wrapper,
            IMetaDataHasher hasher)
        {
            var tasks = new List<Task<HashTaskResult>>();
            while (tasks.Count < taskCount)
            {
                tasks.Add(HashData(tasks.Count * HashTaskDelay(inMemory), inMemory, wrapper, hasher));
            }
            return tasks;
        }

        private async Task<HashTaskResult> HashData(
            int delayMs,
            bool inMemory,
            IWrapper wrapper,
            IMetaDataHasher hasher)
        {
            await Task.Delay(delayMs);

            // Taken after the start delay, so a refresh counts as during a
            // hashing task only when it overlaps the hashing itself.
            DateTime start = DateTime.UtcNow;

            int taskId = delayMs / HashTaskDelay(inMemory);
            Console.WriteLine($"Hash task '{taskId}' started.");

            _hashTaskStarted.Release();
            _refreshStarted.Wait(FirstRefreshWaitMilliseconds);
            int hash = 0;

            hash ^= hasher.HashProperties(hash, wrapper);
            hash ^= hasher.HashComponents(hash, wrapper);
            hash ^= hasher.HashProfiles(hash, wrapper);

            Console.WriteLine($"Hash task '{taskId}' finished with hash value '{hash}'.");

            return new HashTaskResult()
            {
                HashValue = hash,
                StartTime = start,
                FinishTime = DateTime.UtcNow
            };
        }

        private async Task<List<RefreshResult>> Refresh(
            IWrapper wrapper,
            CancellationToken cancellationToken,
            PerformanceProfiles profile)
        {
            List<RefreshResult> result = new List<RefreshResult>();
            while (cancellationToken.IsCancellationRequested == false &&
                result.Count < RefreshLimit())
            {
                if (await WaitForHashTask(cancellationToken) == false)
                {
                    break;
                }
                DateTime start = DateTime.UtcNow;
                Console.WriteLine("Refresh started");
                _refreshStarted.Set();
                wrapper.GetEngine().RefreshData(null);
                Console.WriteLine("Refresh completed");
                result.Add(new RefreshResult()
                {
                    StartTime = start,
                    FinishTime = DateTime.UtcNow
                });
                await Task.Delay(ReloadDelay(profile, false));
            }

            return result;
        }

        /// <summary>
        /// Waits for a hashing task to start.
        /// </summary>
        /// <returns>
        /// False if the test cancelled the refreshes first.
        /// </returns>
        private async Task<bool> WaitForHashTask(
            CancellationToken cancellationToken)
        {
            try
            {
                await _hashTaskStarted.WaitAsync(cancellationToken);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private async Task<List<RefreshResult>> RefreshInMemory(
            IWrapper wrapper,
            byte[] masterData,
            CancellationToken cancellationToken,
            PerformanceProfiles profile)
        {
            List<RefreshResult> result = new List<RefreshResult>();

            while (cancellationToken.IsCancellationRequested == false &&
                result.Count < RefreshLimit())
            {
                if (await WaitForHashTask(cancellationToken) == false)
                {
                    break;
                }
                using (var stream = new MemoryStream())
                {
                    stream.Write(masterData, 0, masterData.Length);
                    stream.Seek(0, SeekOrigin.Begin);

                    DateTime start = DateTime.UtcNow;
                    Console.WriteLine("Refresh started");
                    _refreshStarted.Set();
                    wrapper.GetEngine().RefreshData(
                        wrapper.GetEngine().DataFiles[0].Identifier,
                        stream);
                    Console.WriteLine("Refresh completed");
                    result.Add(new RefreshResult()
                    {
                        StartTime = start,
                        FinishTime = DateTime.UtcNow
                    });
                }
                await Task.Delay(ReloadDelay(profile, true));
            }

            return result;
        }
    }
}
