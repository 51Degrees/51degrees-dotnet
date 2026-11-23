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

using FiftyOne.IpIntelligence.Engine.OnPremise.FlowElements;
using FiftyOne.Pipeline.Engines;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;

namespace FiftyOne.IpIntelligence.OnPremise.Tests.FlowElements;

/// <summary>
/// The real engine and data update service, with the engine disposed after
/// the file watcher has seen a change to the data file and before the
/// refresh. The native engine is freed by then, so the service must leave
/// it alone rather than refresh it.
/// </summary>
[TestClass]
public class WatcherDisposeTests
{
    private const string DataFileName = "51Degrees-IPIV4AsnIpiV41.ipi";

    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

    [TestMethod]
    [DataRow(PerformanceProfiles.LowMemory)]
    [DataRow(PerformanceProfiles.MaxPerformance)]
    public void Watcher_EngineDisposedBeforeRefresh_DoesNotRefresh(
        PerformanceProfiles profile)
    {
        var source = TestHelpers.Utils.GetFilePath(DataFileName);
        var directory = Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(),
            Path.GetRandomFileName())).FullName;
        try
        {
            var master = Path.Combine(directory, DataFileName);
            File.Copy(source.FullName, master);
            using var loggerFactory = new LoggerFactory();
            using var httpClient = new HttpClient();
            var service = new DataUpdateService(
                loggerFactory.CreateLogger<DataUpdateService>(),
                httpClient);
            var started = new ManualResetEventSlim();
            var disposed = new ManualResetEventSlim();
            var reports = new BlockingCollection<DataUpdateCompleteArgs>();
            // The watcher's thread waits here until the engine is disposed.
            service.CheckForUpdateStarted += (sender, e) =>
            {
                started.Set();
                disposed.Wait(Wait);
            };
            service.CheckForUpdateComplete += (sender, e) => reports.Add(e);

            var builder = new IpiOnPremiseEngineBuilder(loggerFactory, service);
            builder.SetPerformanceProfile(profile);
            builder.SetTempDirPath(Directory.CreateDirectory(
                Path.Combine(directory, "temp")).FullName);
            builder.SetDataFileSystemWatcher(true);
            builder.SetDataUpdateOnStartup(false);
            builder.SetAutoUpdate(false);
            var engine = builder.Build(master, true);
            var dataFile = engine.DataFiles.Single();

            File.SetLastWriteTimeUtc(master, DateTime.UtcNow.AddSeconds(1));
            Assert.IsTrue(
                started.Wait(Wait),
                "The watcher did not see the change to the data file.");
            engine.Dispose();
            disposed.Set();

            Assert.AreEqual(
                AutoUpdateStatus.AUTO_UPDATE_NOT_NEEDED,
                TakeFor(reports, dataFile));
        }
        finally
        {
            try
            {
                Directory.Delete(directory, true);
            }
            catch (IOException)
            {
                // The native code can still hold a temporary copy for a
                // moment.
            }
        }
    }

    // The status of the first report for the data file. An event raised
    // once the file is unregistered belongs to no data file, so its report
    // is skipped.
    private static AutoUpdateStatus TakeFor(
        BlockingCollection<DataUpdateCompleteArgs> reports,
        IAspectEngineDataFile dataFile)
    {
        var deadline = DateTime.UtcNow + Wait;
        while (true)
        {
            var remaining = deadline - DateTime.UtcNow;
            DataUpdateCompleteArgs report = null;
            Assert.IsTrue(
                remaining > TimeSpan.Zero &&
                reports.TryTake(out report, remaining),
                "The update service did not finish with the change.");
            if (ReferenceEquals(report.DataFile, dataFile))
            {
                return report.Status;
            }
        }
    }
}
