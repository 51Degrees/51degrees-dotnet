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
using FiftyOne.Pipeline.Engines.Exceptions;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;

namespace FiftyOne.Pipeline.Engines.Tests.Services
{
    /// <summary>
    /// A license key in the URL of a data update request never appears in
    /// full in a log entry or an error message.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/c9939a787cc71f711e3c82755308482fc1cf63ce/pipeline-specification/features/data-updates.md#logging">Specification</see>
    /// </summary>
    [TestClass]
    public class DataUpdateServiceLicenseKeyTests
    {
        private const string Key = "ABCDEFGHIJ1234567890";
        private const string Masked = "****************7890";
        private const string Url =
            "http://www.test.com/api/download?LicenseKeys=" + Key +
            "&Download=True&Type=1";

        private TestLogger<DataUpdateService> _logger;
        private Mock<MockHttpMessageHandler> _httpHandler;
        private DataUpdateService _dataUpdate;

        [TestInitialize]
        public void Init()
        {
            _logger = new TestLogger<DataUpdateService>();
            _httpHandler = new Mock<MockHttpMessageHandler>() { CallBase = true };
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(f => f.File).Returns(new Mock<IFileWrapper>().Object);
            fileSystem.Setup(f => f.Directory).Returns(
                new Mock<IDirectoryWrapper>().Object);
            _dataUpdate = new DataUpdateService(
                _logger,
                new HttpClient(_httpHandler.Object),
                fileSystem.Object,
                new Mock<Func<TimerCallback, object, TimeSpan, Timer>>().Object);
            _dataUpdate.DebugLoggingEnabled = true;
        }

        private static IEnumerable<object[]> Responses()
        {
            foreach (var status in new[]
            {
                HttpStatusCode.OK,
                HttpStatusCode.NotModified,
                HttpStatusCode.Forbidden,
                (HttpStatusCode)429,
                HttpStatusCode.InternalServerError,
            })
            {
                yield return new object[] { (int)status, false };
            }
            yield return new object[] { 0, true };
        }

        /// <summary>
        /// Whatever the answer, or if there is none, no log entry and no
        /// exception holds the key in full.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(Responses))]
        public void CheckForUpdate_KeyInUrl_OnlyMaskedFormAppears(
            int status, bool throwsFromHttp)
        {
            _httpHandler.Setup(h => h.Send(It.IsAny<HttpRequestMessage>()))
                .Returns((HttpRequestMessage request) =>
                {
                    if (throwsFromHttp)
                    {
                        // An HTTP library error names the URL it requested.
                        throw new HttpRequestException(
                            "An error occurred sending " +
                            request.RequestUri.AbsoluteUri,
                            new InvalidOperationException(
                                "Inner " + request.RequestUri.AbsoluteUri));
                    }
                    return new HttpResponseMessage((HttpStatusCode)status)
                    {
                        Content = new StringContent("x", Encoding.UTF8),
                    };
                });
            var engine = new Mock<IOnPremiseAspectEngine>();
            engine.Setup(e => e.TempDataDirPath).Returns(System.IO.Path.GetTempPath());
            var file = new AspectEngineDataFile()
            {
                Engine = engine.Object,
                Configuration = new DataFileConfiguration()
                {
                    DataUpdateUrl = Url,
                    DataUpdateLicenseKeys = new[] { Key },
                    FileSystemWatcherEnabled = false,
                    AutomaticUpdatesEnabled = false,
                    VerifyMd5 = false,
                },
            };
            engine.Setup(e => e.GetDataFileMetaData(It.IsAny<string>())).Returns(file);

            var text = new StringBuilder();
            try
            {
                _dataUpdate.CheckForUpdate(engine.Object);
            }
            catch (Exception ex)
            {
                text.AppendLine(ex.ToString());
            }

            foreach (var entry in _logger.ExtendedEntries)
            {
                text.AppendLine(entry.Message);
                text.AppendLine(entry.Exception?.ToString());
            }
            var all = text.ToString();
            Assert.IsFalse(all.Contains(Key),
                "The key appears in full in:" + Environment.NewLine + all);
            Assert.IsFalse(all.Contains("ABCDEFGHIJ"),
                "The start of the key appears in:" + Environment.NewLine + all);
            Assert.IsTrue(all.Contains(Masked),
                "The masked key is missing from:" + Environment.NewLine + all);
        }
    }
}
