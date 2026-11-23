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
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.IO;
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
        var master = Path.Combine(directory, DataFileName);
        File.Copy(source.FullName, master);

        var loggerFactory = new LoggerFactory();
        var service = new DataUpdateService(
            loggerFactory.CreateLogger<DataUpdateService>(),
            new HttpClient());
        var started = new ManualResetEventSlim();
        var disposed = new ManualResetEventSlim();
        var reports = new BlockingCollection<AutoUpdateStatus>();
        // The watcher's thread waits here until the engine is disposed.
        service.CheckForUpdateStarted += (sender, e) =>
        {
            started.Set();
            disposed.Wait(Wait);
        };
        service.CheckForUpdateComplete += (sender, e) =>
            reports.Add(e.Status);

        var builder = new IpiOnPremiseEngineBuilder(loggerFactory, service);
        builder.SetPerformanceProfile(profile);
        builder.SetTempDirPath(Directory.CreateDirectory(
            Path.Combine(directory, "temp")).FullName);
        builder.SetDataFileSystemWatcher(true);
        builder.SetDataUpdateOnStartup(false);
        builder.SetAutoUpdate(false);
        var engine = builder.Build(master, true);

        File.SetLastWriteTimeUtc(master, DateTime.UtcNow.AddSeconds(1));
        Assert.IsTrue(
            started.Wait(Wait),
            "The watcher did not see the change to the data file.");
        engine.Dispose();
        disposed.Set();

        Assert.IsTrue(
            reports.TryTake(out var status, Wait),
            "The update service did not finish with the change.");
        Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_NOT_NEEDED, status);

        try
        {
            Directory.Delete(directory, true);
        }
        catch (IOException)
        {
            // The native code can still hold a temporary copy for a moment.
        }
    }
}
