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
using FiftyOne.Common.Wrappers.IO;
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;

namespace FiftyOne.Pipeline.Engines.Tests.Services;

/// <summary>
/// A watched, polled or downloaded file is loaded only when the published
/// date in its header is later than that of the data the engine has
/// loaded. Otherwise the check is reported as not needed, whatever the
/// file's modification time says.
/// </summary>
[TestClass]
public class DataUpdateServicePublishedDateTests
{
    /// <summary>
    /// How long a test waits for the service to report a check.
    /// </summary>
    private const int CheckTimeoutMilliseconds = 30000;

    /// <summary>
    /// How long a watcher test waits after the first report for any
    /// further events the watcher raises for the same write.
    /// </summary>
    private const int WatcherSettleMilliseconds = 1000;

    /// <summary>
    /// The MD5 hash of Resources/file.gz, which the download tests serve.
    /// </summary>
    private const string DownloadMd5 = "08527dcbdd437e7fa6c084423d06dba6";

    private static readonly DateTime LoadedPublished =
        new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);

    private TestLogger<DataUpdateService> _logger;
    private Mock<MockHttpMessageHandler> _httpHandler;
    private HttpClient _httpClient;
    private Mock<Func<TimerCallback, object, TimeSpan, Timer>> _timerFactory;
    private Mock<IFileWrapper> _fileWrapper;
    private Mock<IFileSystem> _fileSystem;
    private string _folder;
    private readonly List<AspectEngineDataFile> _files =
        new List<AspectEngineDataFile>();
    private readonly List<DataUpdateCompleteArgs> _reports =
        new List<DataUpdateCompleteArgs>();
    private readonly ManualResetEventSlim _reported =
        new ManualResetEventSlim(false);

    [TestInitialize]
    public void Init()
    {
        _logger = new TestLogger<DataUpdateService>();
        _httpHandler = new Mock<MockHttpMessageHandler> { CallBase = true };
        _httpClient = new HttpClient(_httpHandler.Object);
        _timerFactory = new Mock<Func<TimerCallback, object, TimeSpan, Timer>>();
        _fileWrapper = new Mock<IFileWrapper>();
        _fileSystem = new Mock<IFileSystem>();
        _fileSystem.Setup(f => f.File).Returns(_fileWrapper.Object);
        _folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_folder);
    }

    [TestCleanup]
    public void Cleanup()
    {
        // Disposing a data file stops its watcher.
        foreach (var file in _files)
        {
            file.Dispose();
        }
        _httpClient.Dispose();
        _reported.Dispose();
        try
        {
            Directory.Delete(_folder, true);
        }
        catch (IOException)
        {
            // A watcher can still hold the folder for a moment.
        }
        _logger.AssertMaxErrors(0);
        _logger.AssertMaxWarnings(0);
    }

    /// <summary>
    /// The days the new file's header is published after the loaded data.
    /// One is later and is loaded. Zero is the same and minus one is
    /// earlier, and neither is loaded.
    /// </summary>
    private static bool IsLater(int days) => days > 0;

    [TestMethod]
    [DataRow(1)]
    [DataRow(0)]
    [DataRow(-1)]
    public void DataUpdateService_PublishedDate_WatchedFile(int days)
    {
        string dataFilePath = Path.Combine(_folder, "data.dat");
        File.WriteAllText(dataFilePath, "loaded");
        var engine = CreateEngine(days);
        var file = CreateDataFile(engine, new DataFileConfiguration
        {
            FileSystemWatcherEnabled = true,
            DataFilePath = dataFilePath
        });
        var service = CreateService(new RealFileSystem());

        service.RegisterDataFile(file);
        // Written now, so its modification time is newer than the data
        // the engine loaded.
        File.WriteAllText(dataFilePath, "new");
        WaitForReports(IsLater(days));

        AssertOutcome(engine, file, IsLater(days));
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(0)]
    [DataRow(-1)]
    public void DataUpdateService_PublishedDate_PolledFile(int days)
    {
        string dataFilePath = Path.Combine(_folder, "data.dat");
        string tempFilePath = Path.Combine(_folder, "temp.dat");
        var engine = CreateEngine(days);
        var file = CreateDataFile(engine, new DataFileConfiguration
        {
            FileSystemWatcherEnabled = false,
            DataFilePath = dataFilePath
        });
        file.TempDataFilePath = tempFilePath;
        // The data file was modified after the copy the engine uses.
        _fileWrapper.Setup(w => w.GetLastWriteTimeUtc(dataFilePath))
            .Returns(LoadedPublished.AddDays(2));
        _fileWrapper.Setup(w => w.GetLastWriteTimeUtc(tempFilePath))
            .Returns(LoadedPublished.AddDays(1));
        var service = CreateService(_fileSystem.Object);

        var status = service.CheckForUpdate(engine.Object);

        Assert.AreEqual(
            IsLater(days) ?
                AutoUpdateStatus.AUTO_UPDATE_SUCCESS :
                AutoUpdateStatus.AUTO_UPDATE_NOT_NEEDED,
            status);
        AssertOutcome(engine, file, IsLater(days));
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(0)]
    [DataRow(-1)]
    public void DataUpdateService_PublishedDate_DownloadedFile(int days)
    {
        string dataFilePath = Path.Combine(_folder, "data.dat");
        File.WriteAllText(dataFilePath, "loaded");
        var engine = CreateEngine(days);
        engine.Setup(e => e.TempDataDirPath).Returns(_folder);
        var file = CreateDataFile(engine, new DataFileConfiguration
        {
            FileSystemWatcherEnabled = false,
            DataFilePath = dataFilePath,
            DataUpdateUrl = "http://www.test.com",
            VerifyMd5 = true,
            DecompressContent = true,
            VerifyModifiedSince = false
        });
        file.TempDataDirPath = _folder;
        ServeDownload();
        var service = CreateService(new RealFileSystem());

        var status = service.CheckForUpdate(engine.Object);

        Assert.AreEqual(
            IsLater(days) ?
                AutoUpdateStatus.AUTO_UPDATE_SUCCESS :
                AutoUpdateStatus.AUTO_UPDATE_NOT_NEEDED,
            status);
        // The date is read from the download before it is copied over
        // the data file.
        engine.As<IDataFilePublishedDateEngine>().Verify(r =>
            r.GetDataFilePublishedDate(
                It.IsAny<string>(),
                It.Is<string>(p => p != dataFilePath)),
            Times.Once());
        AssertOutcome(engine, file, IsLater(days));
        Assert.AreEqual(
            IsLater(days) == false,
            File.ReadAllText(dataFilePath) == "loaded",
            "The data file is replaced only by newer data.");
    }

    /// <summary>
    /// An engine that has loaded no data has every new file loaded, and
    /// is not asked for the file's date.
    /// </summary>
    [TestMethod]
    public void DataUpdateService_PublishedDate_NothingLoaded()
    {
        string dataFilePath = Path.Combine(_folder, "data.dat");
        string tempFilePath = Path.Combine(_folder, "temp.dat");
        var engine = CreateEngine(-1);
        var file = CreateDataFile(engine, new DataFileConfiguration
        {
            FileSystemWatcherEnabled = false,
            DataFilePath = dataFilePath
        });
        file.DataPublishedDateTime = default;
        file.TempDataFilePath = tempFilePath;
        _fileWrapper.Setup(w => w.GetLastWriteTimeUtc(dataFilePath))
            .Returns(LoadedPublished.AddDays(2));
        _fileWrapper.Setup(w => w.GetLastWriteTimeUtc(tempFilePath))
            .Returns(LoadedPublished.AddDays(1));
        var service = CreateService(_fileSystem.Object);

        var status = service.CheckForUpdate(engine.Object);

        Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
        engine.Verify(e => e.RefreshData(file.Identifier), Times.Once());
        engine.As<IDataFilePublishedDateEngine>().Verify(r =>
            r.GetDataFilePublishedDate(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never());
    }

    /// <summary>
    /// A file whose header cannot be read is loaded, so a file the engine
    /// cannot use fails as any failed refresh does.
    /// </summary>
    [TestMethod]
    public void DataUpdateService_PublishedDate_HeaderUnreadable()
    {
        string dataFilePath = Path.Combine(_folder, "data.dat");
        string tempFilePath = Path.Combine(_folder, "temp.dat");
        var engine = new Mock<IOnPremiseAspectEngine>();
        engine.As<IDataFilePublishedDateEngine>()
            .Setup(r => r.GetDataFilePublishedDate(
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Throws(new IOException("The file is cut short."));
        var file = CreateDataFile(engine, new DataFileConfiguration
        {
            FileSystemWatcherEnabled = false,
            DataFilePath = dataFilePath
        });
        file.TempDataFilePath = tempFilePath;
        _fileWrapper.Setup(w => w.GetLastWriteTimeUtc(dataFilePath))
            .Returns(LoadedPublished.AddDays(2));
        _fileWrapper.Setup(w => w.GetLastWriteTimeUtc(tempFilePath))
            .Returns(LoadedPublished.AddDays(1));
        var service = CreateService(_fileSystem.Object);

        var status = service.CheckForUpdate(engine.Object);

        Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
        engine.Verify(e => e.RefreshData(file.Identifier), Times.Once());
        engine.As<IDataFilePublishedDateEngine>().Verify(r =>
            r.GetDataFilePublishedDate(file.Identifier, dataFilePath),
            Times.Once());
    }

    /// <summary>
    /// A file whose header gives no published date is loaded, as a file
    /// whose header cannot be read is.
    /// </summary>
    [TestMethod]
    public void DataUpdateService_PublishedDate_HeaderWithoutDate()
    {
        var engine = new Mock<IOnPremiseAspectEngine>();
        engine.As<IDataFilePublishedDateEngine>()
            .Setup(r => r.GetDataFilePublishedDate(
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(default(DateTime));
        var file = CreatePolledFile(engine);
        var service = CreateService(_fileSystem.Object);

        var status = service.CheckForUpdate(engine.Object);

        Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
        AssertOutcome(engine, file, true);
    }

    /// <summary>
    /// Published dates are compared in UTC. A local date is converted to
    /// UTC and a date of unspecified kind is taken to be UTC, so only a
    /// date that is later in UTC is loaded.
    /// </summary>
    [TestMethod]
    [DataRow(DateTimeKind.Local, 1)]
    [DataRow(DateTimeKind.Local, 0)]
    [DataRow(DateTimeKind.Local, -1)]
    [DataRow(DateTimeKind.Unspecified, 1)]
    [DataRow(DateTimeKind.Unspecified, 0)]
    [DataRow(DateTimeKind.Unspecified, -1)]
    public void DataUpdateService_PublishedDate_DateKind(
        DateTimeKind kind,
        int days)
    {
        var utc = LoadedPublished.AddDays(days);
        var published = kind == DateTimeKind.Local ?
            utc.ToLocalTime() :
            DateTime.SpecifyKind(utc, DateTimeKind.Unspecified);
        var engine = new Mock<IOnPremiseAspectEngine>();
        engine.As<IDataFilePublishedDateEngine>()
            .Setup(r => r.GetDataFilePublishedDate(
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(published);
        var file = CreatePolledFile(engine);
        var service = CreateService(_fileSystem.Object);

        var status = service.CheckForUpdate(engine.Object);

        Assert.AreEqual(
            IsLater(days) ?
                AutoUpdateStatus.AUTO_UPDATE_SUCCESS :
                AutoUpdateStatus.AUTO_UPDATE_NOT_NEEDED,
            status);
        AssertOutcome(engine, file, IsLater(days));
    }

    /// <summary>
    /// The loaded data's published date is compared in UTC too.
    /// </summary>
    [TestMethod]
    [DataRow(1)]
    [DataRow(0)]
    [DataRow(-1)]
    public void DataUpdateService_PublishedDate_LoadedDateLocal(int days)
    {
        var engine = CreateEngine(days);
        var file = CreatePolledFile(engine);
        file.DataPublishedDateTime = LoadedPublished.ToLocalTime();
        var service = CreateService(_fileSystem.Object);

        var status = service.CheckForUpdate(engine.Object);

        Assert.AreEqual(
            IsLater(days) ?
                AutoUpdateStatus.AUTO_UPDATE_SUCCESS :
                AutoUpdateStatus.AUTO_UPDATE_NOT_NEEDED,
            status);
        AssertOutcome(engine, file, IsLater(days));
    }

    /// <summary>
    /// A data file checked without a watcher, modified after the copy the
    /// engine uses.
    /// </summary>
    private AspectEngineDataFile CreatePolledFile(
        Mock<IOnPremiseAspectEngine> engine)
    {
        string dataFilePath = Path.Combine(_folder, "data.dat");
        string tempFilePath = Path.Combine(_folder, "temp.dat");
        var file = CreateDataFile(engine, new DataFileConfiguration
        {
            FileSystemWatcherEnabled = false,
            DataFilePath = dataFilePath
        });
        file.TempDataFilePath = tempFilePath;
        _fileWrapper.Setup(w => w.GetLastWriteTimeUtc(dataFilePath))
            .Returns(LoadedPublished.AddDays(2));
        _fileWrapper.Setup(w => w.GetLastWriteTimeUtc(tempFilePath))
            .Returns(LoadedPublished.AddDays(1));
        return file;
    }

    /// <summary>
    /// An engine whose new files are published the given number of days
    /// after the data it has loaded.
    /// </summary>
    private static Mock<IOnPremiseAspectEngine> CreateEngine(int days)
    {
        var engine = new Mock<IOnPremiseAspectEngine>();
        engine.As<IDataFilePublishedDateEngine>()
            .Setup(r => r.GetDataFilePublishedDate(
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(LoadedPublished.AddDays(days));
        return engine;
    }

    private AspectEngineDataFile CreateDataFile(
        Mock<IOnPremiseAspectEngine> engine,
        DataFileConfiguration configuration)
    {
        var file = new AspectEngineDataFile
        {
            Engine = engine.Object,
            Configuration = configuration,
            DataPublishedDateTime = LoadedPublished
        };
        engine.Setup(e => e.GetDataFileMetaData(It.IsAny<string>()))
            .Returns(file);
        _files.Add(file);
        return file;
    }

    private DataUpdateService CreateService(IFileSystem fileSystem)
    {
        var service = new DataUpdateService(
            _logger,
            _httpClient,
            fileSystem,
            _timerFactory.Object);
        service.CheckForUpdateComplete += (sender, e) =>
        {
            lock (_reports)
            {
                _reports.Add(e);
            }
            _reported.Set();
        };
        return service;
    }

    private void ServeDownload()
    {
        _httpHandler.Setup(h => h.Send(It.IsAny<HttpRequestMessage>()))
            .Returns(() =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(
                        File.OpenRead(Path.Combine("Resources", "file.gz")))
                };
                response.Content.Headers.Add("Content-MD5", DownloadMd5);
                return response;
            });
    }

    /// <summary>
    /// Waits for the watcher to report a check, and for a success when
    /// one is expected, then for any further events of the same write.
    /// </summary>
    private void WaitForReports(bool success)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(
            CheckTimeoutMilliseconds);
        while (DateTime.UtcNow < deadline)
        {
            _reported.Wait(deadline - DateTime.UtcNow);
            lock (_reports)
            {
                if (success == false || _reports.Any(r =>
                    r.Status == AutoUpdateStatus.AUTO_UPDATE_SUCCESS))
                {
                    break;
                }
                _reported.Reset();
            }
        }
        Thread.Sleep(WatcherSettleMilliseconds);
    }

    /// <summary>
    /// A newer file was loaded and reported as a success. Any other file
    /// left the engine alone and was reported as not needed.
    /// </summary>
    private void AssertOutcome(
        Mock<IOnPremiseAspectEngine> engine,
        AspectEngineDataFile file,
        bool loaded)
    {
        engine.Verify(
            e => e.RefreshData(file.Identifier),
            loaded ? Times.Once() : Times.Never());
        engine.As<IDataFilePublishedDateEngine>().Verify(r =>
            r.GetDataFilePublishedDate(file.Identifier, It.IsAny<string>()),
            Times.AtLeastOnce());
        lock (_reports)
        {
            Assert.IsNotEmpty(_reports, "No check was reported.");
            if (loaded)
            {
                Assert.IsTrue(_reports.Any(r =>
                    r.Status == AutoUpdateStatus.AUTO_UPDATE_SUCCESS));
            }
            else
            {
                Assert.IsTrue(_reports.All(r =>
                    r.Status == AutoUpdateStatus.AUTO_UPDATE_NOT_NEEDED));
            }
        }
    }
}
