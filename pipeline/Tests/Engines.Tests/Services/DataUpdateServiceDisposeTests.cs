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

using FiftyOne.Common.Wrappers.IO;
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Engines.Tests.Services;

/// <summary>
/// A data file disposed while the file system watcher is applying a change
/// to it. An engine frees its native data once its data files are
/// disposed, so the service must not refresh the engine after that, and
/// dispose must wait for a refresh in progress.
/// </summary>
[TestClass]
public class DataUpdateServiceDisposeTests
{
    // Every content has the same length, so it can be written in place.
    private const string OldData = "data 0";
    private const string NewData = "data 1";

    private const int WaitMilliseconds = 30000;

    // How long dispose is given to return while a refresh is held, which
    // it must not do.
    private const int EarlyReturnMilliseconds = 1000;

    // Longer than the steps file write times move in.
    private const int WriteTimeStepMilliseconds = 50;

    private string _directory;
    private string _path;
    private DataUpdateService _service;
    private Mock<IOnPremiseAspectEngine> _engine;
    private AspectEngineDataFile _file;
    private int _refreshes;
    private int _entered;
    private int _exited;
    private ConcurrentQueue<DataUpdateCompleteArgs> _reports;
    private CountingHandler _http;

    [TestInitialize]
    public void Init()
    {
        _directory = Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(),
            Path.GetRandomFileName())).FullName;
        _path = Path.Combine(_directory, "data.dat");
        File.WriteAllText(_path, OldData);
        _http = new CountingHandler();
        _service = new DataUpdateService(
            NullLogger<DataUpdateService>.Instance,
            new HttpClient(_http),
            new RealFileSystem(),
            (callback, state, dueTime) => null);
        _service.OnDataFileUpdatedEntered += () =>
            Interlocked.Increment(ref _entered);
        _service.OnDataFileUpdatedWillExit += () =>
            Interlocked.Increment(ref _exited);
        _reports = new ConcurrentQueue<DataUpdateCompleteArgs>();
        _service.CheckForUpdateComplete += (sender, e) =>
            _reports.Enqueue(e);
        _engine = new Mock<IOnPremiseAspectEngine>();
        _file = new AspectEngineDataFile()
        {
            Engine = _engine.Object,
            Configuration = new DataFileConfiguration()
            {
                FileSystemWatcherEnabled = true,
                DataFilePath = _path,
                DataUpdateUrl = "https://update.invalid/data",
            },
            TempDataDirPath = _directory,
        };
        _engine.Setup(e => e.GetDataFileMetaData(It.IsAny<string>()))
            .Returns(_file);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _file.Dispose();
        try
        {
            Directory.Delete(_directory, true);
        }
        catch (IOException)
        {
            // A watcher thread can still hold a file for a moment.
        }
    }

    /// <summary>
    /// The file is disposed after the watcher has seen the change and
    /// before the refresh. The engine is not refreshed.
    /// </summary>
    [TestMethod]
    public void Watcher_DisposedBeforeRefresh_DoesNotRefresh()
    {
        CountRefreshes(() => { });
        var started = new ManualResetEventSlim();
        var disposed = new ManualResetEventSlim();
        // The watcher's thread waits here until the file is disposed.
        _service.CheckForUpdateStarted += (sender, e) =>
        {
            started.Set();
            disposed.Wait(WaitMilliseconds);
        };
        _service.RegisterDataFile(_file);

        Replace(_path, NewData);
        Assert.IsTrue(
            started.Wait(WaitMilliseconds),
            "The watcher did not see the change.");
        _file.Dispose();
        disposed.Set();

        AssertNotRefreshed();
    }

    /// <summary>
    /// The file is disposed while the engine refreshes. Dispose returns
    /// only once the refresh has ended.
    /// </summary>
    [TestMethod]
    public void Watcher_DisposedDuringRefresh_WaitsForRefresh()
    {
        var refreshing = new ManualResetEventSlim();
        var release = new ManualResetEventSlim();
        var ended = new ManualResetEventSlim();
        CountRefreshes(() =>
        {
            refreshing.Set();
            release.Wait(WaitMilliseconds);
            ended.Set();
        });
        _service.RegisterDataFile(_file);

        Replace(_path, NewData);
        Assert.IsTrue(
            refreshing.Wait(WaitMilliseconds),
            "The watcher did not start a refresh.");
        var dispose = Task.Run(() => _file.Dispose());
        var returnedEarly = dispose.Wait(EarlyReturnMilliseconds);
        release.Set();

        Assert.IsFalse(
            returnedEarly,
            "Dispose returned while the engine was refreshing.");
        Assert.IsTrue(
            dispose.Wait(WaitMilliseconds),
            "Dispose did not return once the refresh ended.");
        Assert.IsTrue(ended.IsSet);
        Assert.AreEqual(1, _refreshes);
    }

    /// <summary>
    /// The file is disposed while the watcher waits for the changed file
    /// to be free to read. Dispose stops that wait rather than being held
    /// by it, and the engine is not refreshed.
    /// </summary>
    [TestMethod]
    public void Watcher_DisposedWhileFileHeld_StopsWaitingAndDoesNotRefresh()
    {
        CountRefreshes(() => { });
        var started = new ManualResetEventSlim();
        var first = 0;
        FileStream hold = null;
        // The first event opens the file so that no one else can, before
        // the service tries to. Any other event waits until it has.
        _service.CheckForUpdateStarted += (sender, e) =>
        {
            if (Interlocked.Exchange(ref first, 1) == 0)
            {
                hold = OpenAlone(_path);
                started.Set();
            }
            started.Wait(WaitMilliseconds);
        };
        _service.RegisterDataFile(_file);

        try
        {
            Replace(_path, NewData);
            Assert.IsTrue(
                started.Wait(WaitMilliseconds),
                "The watcher did not see the change.");
            Assert.IsNotNull(hold, "The changed file could not be held.");
            Assert.IsTrue(
                Task.Run(() => _file.Dispose()).Wait(WaitMilliseconds),
                "Dispose waited for a file that cannot be read.");
        }
        finally
        {
            hold?.Dispose();
        }

        AssertNotRefreshed();
    }

    /// <summary>
    /// An update from memory for a disposed file fails, and neither writes
    /// the data file nor refreshes the engine.
    /// </summary>
    [TestMethod]
    public void UpdateFromMemory_Disposed_ThrowsAndChangesNothing()
    {
        CountRefreshes(() => { });
        _file.Dispose();
        using var data = new MemoryStream();
        data.Write(Encoding.ASCII.GetBytes(NewData));

        Assert.ThrowsExactly<ObjectDisposedException>(() =>
            _service.UpdateFromMemory(_file, data));
        Assert.AreEqual(OldData, File.ReadAllText(_path));
        Assert.AreEqual(0, _refreshes);
    }

    /// <summary>
    /// A check for an update to a disposed file reports not needed without
    /// asking the update URL for a new file.
    /// </summary>
    [TestMethod]
    public void CheckForUpdate_Disposed_DoesNotDownload()
    {
        CountRefreshes(() => { });
        _file.Dispose();

        var status = _service.CheckForUpdate(_engine.Object);

        Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_NOT_NEEDED, status);
        Assert.AreEqual(0, _http.Requests);
        Assert.AreEqual(0, _refreshes);
    }

    private void CountRefreshes(Action refresh)
    {
        _engine.Setup(e => e.RefreshData(It.IsAny<string>()))
            .Callback(() =>
            {
                Interlocked.Increment(ref _refreshes);
                refresh();
            });
    }

    // Waits for every watcher event that started to end, then checks none
    // refreshed the engine. An event raised once the file is unregistered
    // belongs to no data file and reports in progress, so only the reports
    // for this file are checked.
    private void AssertNotRefreshed()
    {
        Assert.IsTrue(
            SpinWait.SpinUntil(
                () => Volatile.Read(ref _exited) ==
                    Volatile.Read(ref _entered),
                WaitMilliseconds),
            "The watcher did not finish with the change.");
        Assert.AreEqual(
            0,
            _refreshes,
            "The engine was refreshed after its data file was disposed.");
        var reports = _reports
            .Where(r => ReferenceEquals(r.DataFile, _file))
            .Select(r => r.Status)
            .ToArray();
        Assert.IsTrue(reports.Length > 0, "Nothing was reported.");
        Assert.IsTrue(
            reports.All(r => r == AutoUpdateStatus.AUTO_UPDATE_NOT_NEEDED),
            "Reported " + string.Join(", ", reports) + ".");
    }

    // Opens the file so that it cannot be opened again until it is closed,
    // trying again while the write that changed it still has it open.
    private static FileStream OpenAlone(string path)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(WaitMilliseconds);
        while (true)
        {
            try
            {
                return new FileStream(
                    path, FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(10);
            }
        }
    }

    // Writes over the data file in place with one write. The service only
    // refreshes for a later write time, so the write waits until the clock
    // is clear of the last one.
    private static void Replace(string path, string content)
    {
        var bytes = Encoding.ASCII.GetBytes(content);
        var last = File.GetLastWriteTimeUtc(path);
        while ((DateTime.UtcNow - last).TotalMilliseconds <
            WriteTimeStepMilliseconds)
        {
            Thread.Sleep(10);
        }
        using (var stream = new FileStream(
            path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
        {
            stream.Write(bytes, 0, bytes.Length);
        }
    }

    // Counts the requests the service sends, and answers each with not
    // modified.
    private sealed class CountingHandler : HttpMessageHandler
    {
        private int _requests;

        public int Requests => Volatile.Read(ref _requests);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requests);
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.NotModified));
        }
    }
}
