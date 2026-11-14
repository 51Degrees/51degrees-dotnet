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

using FiftyOne.DeviceDetection.Hash.Engine.OnPremise.FlowElements;
using FiftyOne.DeviceDetection.TestHelpers;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;

namespace FiftyOne.DeviceDetection.Hash.Tests.FlowElements
{
    /// <summary>
    /// The real engine, given a data file it cannot use. A refresh must fail
    /// with an error and leave the engine answering from the data it had, and
    /// the data update service must report the failure and apply a later good
    /// file.
    /// </summary>
    /// <remarks>
    /// The broken file is the Lite data file with a version the native code
    /// does not support in its first 16 bytes. Each change is one write of those
    /// bytes in place, so the file watcher sees each change once and never a
    /// partial file.
    /// </remarks>
    [TestClass]
    public class BrokenDataFileTests
    {
        // The version is four 32 bit numbers at the start of the file.
        private const int VersionEnd = 4 * sizeof(int);
        private static readonly int[] UnsupportedVersion = { 1, 2, 3, 4 };

        private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

        // Longer than the steps file write times move in.
        private static readonly TimeSpan WriteTimeStep =
            TimeSpan.FromMilliseconds(50);

        private string _directory;
        private string _master;
        private byte[] _good;
        private byte[] _broken;

        [TestInitialize]
        public void Init()
        {
            var source = Utils.GetFilePath(Constants.LITE_HASH_DATA_FILE_NAME);
            _directory = Directory.CreateDirectory(Path.Combine(
                Path.GetTempPath(),
                Path.GetRandomFileName())).FullName;
            _master = Path.Combine(_directory, source.Name);
            _good = File.ReadAllBytes(source.FullName);
            File.WriteAllBytes(_master, _good);
            _broken = (byte[])_good.Clone();
            for (var i = 0; i < UnsupportedVersion.Length; i++)
            {
                BitConverter.GetBytes(UnsupportedVersion[i])
                    .CopyTo(_broken, i * sizeof(int));
            }
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                Directory.Delete(_directory, true);
            }
            catch (IOException)
            {
                // The native code can still hold a temporary copy for a moment.
            }
        }

        /// <summary>
        /// A refresh from a data file the engine cannot use fails with an
        /// error, and the engine goes on answering from the data it had.
        /// </summary>
        [TestMethod]
        [DataRow(PerformanceProfiles.LowMemory)]
        [DataRow(PerformanceProfiles.MaxPerformance)]
        [DataRow(PerformanceProfiles.HighPerformance)]
        [DataRow(PerformanceProfiles.Balanced)]
        [DataRow(PerformanceProfiles.BalancedTemp)]
        public void Refresh_BrokenFile_FailsAndKeepsAnswering(
            PerformanceProfiles profile)
        {
            var loggerFactory = new LoggerFactory();
            var engine = new DeviceDetectionHashEngineBuilder(loggerFactory)
                .SetPerformanceProfile(profile)
                .SetTempDirPath(Directory.CreateDirectory(
                    Path.Combine(_directory, "temp")).FullName)
                .SetAutoUpdate(false)
                .SetDataFileSystemWatcher(false)
                .SetDataUpdateOnStartup(false)
                .Build(_master, true);
            // Disposing the pipeline also disposes the engine.
            using (var pipeline = new PipelineBuilder(loggerFactory)
                .AddFlowElement(engine)
                .Build())
            {
                var before = Lookup(pipeline, engine);

                WriteVersion(_master, _broken);
                Exception thrown = null;
                try
                {
                    engine.RefreshData(engine.DataFiles.Single().Identifier);
                }
                catch (Exception e)
                {
                    thrown = e;
                }

                Assert.IsNotNull(
                    thrown,
                    "The refresh from a file the engine cannot use did not " +
                    "fail with an error.");
                Assert.AreEqual(
                    before,
                    Lookup(pipeline, engine),
                    "The engine does not answer from the data it had.");
            }
        }

        /// <summary>
        /// The data update service reports a refresh from a broken file as a
        /// failure, the engine keeps answering, and a later good file is still
        /// applied.
        /// </summary>
        [TestMethod]
        [DataRow(PerformanceProfiles.LowMemory)]
        [DataRow(PerformanceProfiles.MaxPerformance)]
        [DataRow(PerformanceProfiles.HighPerformance)]
        [DataRow(PerformanceProfiles.Balanced)]
        [DataRow(PerformanceProfiles.BalancedTemp)]
        public void Watcher_BrokenFile_ReportsFailureAndAppliesLaterFile(
            PerformanceProfiles profile)
        {
            var loggerFactory = new LoggerFactory();
            var service = new DataUpdateService(
                loggerFactory.CreateLogger<DataUpdateService>(),
                new HttpClient());
            var reports = new BlockingCollection<AutoUpdateStatus>();
            service.CheckForUpdateComplete +=
                (sender, e) => reports.Add(e.Status);
            var builder = new DeviceDetectionHashEngineBuilder(
                loggerFactory, service);
            builder.SetPerformanceProfile(profile);
            builder.SetTempDirPath(Directory.CreateDirectory(
                Path.Combine(_directory, "temp")).FullName);
            builder.SetDataFileSystemWatcher(true);
            builder.SetDataUpdateOnStartup(false);
            // Automatic updates must be on for the watcher. The polling timer
            // is left for a day so the test never downloads anything.
            builder.SetAutoUpdate(true);
            builder.SetDataUpdateLicenseKey("not-used");
            builder.SetUpdatePollingInterval(TimeSpan.FromDays(1));
            builder.SetUpdateRandomisationMax(TimeSpan.Zero);

            var engine = builder.Build(_master, true);
            // Disposing the pipeline also disposes the engine.
            using (var pipeline = new PipelineBuilder(loggerFactory)
                .AddFlowElement(engine)
                .Build())
            {
                var before = Lookup(pipeline, engine);

                WriteVersion(_master, _broken);
                Assert.IsTrue(
                    WaitFor(reports, AutoUpdateStatus.AUTO_UPDATE_REFRESH_FAILED),
                    "The update service did not report the failed refresh.");
                Assert.AreEqual(
                    before,
                    Lookup(pipeline, engine),
                    "The engine does not answer from the data it had.");

                WriteVersion(_master, _good);
                Assert.IsTrue(
                    WaitFor(reports, AutoUpdateStatus.AUTO_UPDATE_SUCCESS),
                    "The update service did not apply the later good file.");
                Assert.AreEqual(before, Lookup(pipeline, engine));
            }
        }

        // The device and whether it is mobile, for a mobile User-Agent.
        private static string Lookup(
            IPipeline pipeline,
            DeviceDetectionHashEngine engine)
        {
            using (var data = pipeline.CreateFlowData())
            {
                data.AddEvidence("header.user-agent", Constants.MobileUserAgent);
                data.Process();
                var device = data.GetFromElement(engine);
                return $"{device.DeviceId.Value};{device.IsMobile.Value}";
            }
        }

        // Writes the version bytes from the data in place with one write. The
        // service only refreshes for a later write time, so the write waits
        // until the clock is clear of the last one, and it tries again while
        // the service or the native code has the file open.
        private static void WriteVersion(string path, byte[] data)
        {
            var last = File.GetLastWriteTimeUtc(path);
            while (DateTime.UtcNow - last < WriteTimeStep)
            {
                Thread.Sleep(10);
            }
            var deadline = DateTime.UtcNow + Wait;
            while (true)
            {
                try
                {
                    using (var stream = new FileStream(
                        path,
                        FileMode.Open,
                        FileAccess.Write,
                        FileShare.ReadWrite))
                    {
                        stream.Write(data, 0, VersionEnd);
                    }
                    return;
                }
                catch (IOException) when (DateTime.UtcNow < deadline)
                {
                    Thread.Sleep(10);
                }
            }
        }

        // Takes reports until one has the status, ignoring the others that the
        // watcher raises for the same change.
        private static bool WaitFor(
            BlockingCollection<AutoUpdateStatus> reports,
            AutoUpdateStatus status)
        {
            var deadline = DateTime.UtcNow + Wait;
            while (true)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining < TimeSpan.Zero ||
                    reports.TryTake(out var report, remaining) == false)
                {
                    return false;
                }
                if (report == status)
                {
                    return true;
                }
            }
        }
    }
}
