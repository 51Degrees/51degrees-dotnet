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
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FiftyOne.Data;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Tests.FlowElements
{
    /// <summary>
    /// A check for an update that runs while an engine is loading its data
    /// file must not reach the engine, which has no data to answer with. The
    /// check is run from inside the engine's load, after the data file is
    /// added and before its data is loaded, so the test does not depend on
    /// when the data update service's timer fires.
    /// </summary>
    [TestClass]
    public class DataFileEngineWhileLoadingTests
    {
        private const string Identifier = "data";

        private const string Key = "ABCDEFGHIJ1234567890";

        private const string Download =
            "https://distributor.test/api/v2/download";

        /// <summary>
        /// An engine that, like the native engines, has nothing to answer
        /// with until its data is loaded, and runs an action at the start of
        /// each load.
        /// </summary>
        private sealed class LoadingEngine :
            FiftyOneOnPremiseAspectEngineBase<IAspectData>
        {
            private readonly Action _whileLoading;

            public LoadingEngine(Action whileLoading) : base(
                NullLogger<FiftyOneOnPremiseAspectEngineBase<IAspectData>>
                    .Instance,
                (pipeline, engine) => null,
                null)
            {
                _whileLoading = whileLoading;
            }

            public bool Loaded { get; private set; }

            /// <summary>
            /// The number of times the engine was asked about its data
            /// before it had loaded any.
            /// </summary>
            public int CallsBeforeLoad { get; private set; }

            private string Answer(string value)
            {
                if (Loaded == false)
                {
                    CallsBeforeLoad++;
                    throw new InvalidOperationException("No data is loaded.");
                }
                return value;
            }

            public override string DataSourceTier => Answer("TAC");

            public override string GetDataDownloadType(string identifier) =>
                Answer("HashV41");

            public override string ElementDataKey => "loading";

            public override IEvidenceKeyFilter EvidenceKeyFilter =>
                new EvidenceKeyFilterWhitelist(new List<string>());

            public override IList<IFiftyOneAspectPropertyMetaData> Properties =>
                new List<IFiftyOneAspectPropertyMetaData>();

            public override IEnumerable<IProfileMetaData> Profiles => null;

            public override IEnumerable<IComponentMetaData> Components => null;

            public override IEnumerable<IValueMetaData> Values => null;

            public override void RefreshData(string dataFileIdentifier)
            {
                _whileLoading();
                Loaded = true;
            }

            public override void RefreshData(
                string dataFileIdentifier, Stream data)
            {
                RefreshData(dataFileIdentifier);
            }

            protected override void ProcessEngine(
                IFlowData data, IAspectData aspectData)
            {
            }

            protected override void UnmanagedResourcesCleanup()
            {
            }
        }

        /// <summary>
        /// The data update service's timer check runs while the engine is
        /// loading. The data file does not name the engine yet, so the check
        /// asks the update URL without the engine and gets 304, and the
        /// engine is not asked about data it does not have. Once loaded, the
        /// data file names the engine.
        /// </summary>
        [TestMethod]
        public void TimerCheck_WhileEngineLoads_DoesNotReachTheEngine()
        {
            // The timer the data update service makes is captured, so the
            // check it would run can be run from inside the engine's load.
            TimerCallback check = null;
            object checkState = null;
            var timers = new List<Timer>();
            Timer TimerFactory(
                TimerCallback callback, object state, TimeSpan due)
            {
                check = callback;
                checkState = state;
                var timer = new Timer(
                    _ => { }, null, Timeout.Infinite, Timeout.Infinite);
                timers.Add(timer);
                return timer;
            }

            var requests = new List<Uri>();
            var http = new Mock<MockHttpMessageHandler>() { CallBase = true };
            http.Setup(h => h.Send(It.IsAny<HttpRequestMessage>()))
                .Returns((HttpRequestMessage request) =>
                {
                    requests.Add(request.RequestUri);
                    return new HttpResponseMessage(HttpStatusCode.NotModified);
                });
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(f => f.File).Returns(
                new Mock<IFileWrapper>().Object);
            fileSystem.Setup(f => f.Directory).Returns(
                new Mock<IDirectoryWrapper>().Object);
            var service = new DataUpdateService(
                new TestLogger<DataUpdateService>(),
                new HttpClient(http.Object),
                fileSystem.Object,
                TimerFactory);
            var statuses = new List<AutoUpdateStatus>();
            service.CheckForUpdateComplete += (sender, e) =>
                statuses.Add(e.Status);

            var dataFile = new FiftyOneDataFile(typeof(LoadingEngine))
            {
                Identifier = Identifier,
                DataDownloadType = "HashV41",
                Configuration = new DataFileConfiguration()
                {
                    Identifier = Identifier,
                    DataUpdateUrl = Download,
                    DataUpdateLicenseKeys = new[] { Key },
                    UrlFormatter = new FiftyOneUrlFormatter(),
                    MemoryOnly = true,
                    DecompressContent = false,
                    VerifyMd5 = false,
                    VerifyModifiedSince = false,
                    FileSystemWatcherEnabled = false,
                    AutomaticUpdatesEnabled = true,
                    UpdateOnStartup = false,
                },
            };
            try
            {
                service.RegisterDataFile(dataFile);
                Assert.IsNotNull(check, "The data update service set no timer.");

                IOnPremiseAspectEngine engineWhileLoading = null;
                using (var engine = new LoadingEngine(() =>
                {
                    engineWhileLoading = dataFile.Engine;
                    check(checkState);
                }))
                {
                    engine.AddDataFile(dataFile);

                    Assert.IsNull(engineWhileLoading,
                        "The data file named the engine before the engine " +
                        "had loaded its data.");
                    Assert.AreEqual(0, engine.CallsBeforeLoad,
                        "The check asked the engine about its data before " +
                        "the engine had loaded any.");
                    CollectionAssert.AreEqual(
                        new[] { AutoUpdateStatus.AUTO_UPDATE_NOT_NEEDED },
                        statuses,
                        "The check did not end as not needed.");
                    Assert.AreEqual(1, requests.Count,
                        "The check did not ask the update URL once.");
                    Assert.AreSame(engine, dataFile.Engine,
                        "The data file does not name the engine once it has " +
                        "loaded its data.");
                }
            }
            finally
            {
                dataFile.Dispose();
                foreach (var timer in timers)
                {
                    timer.Dispose();
                }
            }
        }
    }
}
