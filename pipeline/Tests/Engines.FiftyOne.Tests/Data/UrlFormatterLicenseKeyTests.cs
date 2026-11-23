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
using FiftyOne.Pipeline.Engines.FiftyOne.Data;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Tests.Data
{
    /// <summary>
    /// The URL that <see cref="FiftyOneUrlFormatter"/> builds joins several
    /// license keys with a bar, which the absolute URI writes as %7C. A
    /// failed update that names that URL holds each key only in masked form.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/c9939a787cc71f711e3c82755308482fc1cf63ce/pipeline-specification/features/logging.md#license-keys">Specification</see>
    /// </summary>
    [TestClass]
    public class UrlFormatterLicenseKeyTests
    {
        private const string KeyOne = "ABCDEFGHIJ1234567890";
        private const string KeyTwo = "ZYXWVUTSRQ0987654321";

        [TestMethod]
        [DataRow(HttpStatusCode.Forbidden)]
        [DataRow(HttpStatusCode.NotModified)]
        [DataRow(HttpStatusCode.OK)]
        public void FormattedUrl_TwoKeys_OnlyMaskedFormsAppear(
            HttpStatusCode status)
        {
            var logger = new TestLogger<DataUpdateService>();
            var handler = new Mock<MockHttpMessageHandler>() { CallBase = true };
            handler.Setup(h => h.Send(It.IsAny<HttpRequestMessage>()))
                .Returns(() => new HttpResponseMessage(status)
                {
                    Content = new StringContent("x", Encoding.UTF8),
                });
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(f => f.File).Returns(new Mock<IFileWrapper>().Object);
            fileSystem.Setup(f => f.Directory).Returns(
                new Mock<IDirectoryWrapper>().Object);
            var service = new DataUpdateService(
                logger,
                new HttpClient(handler.Object),
                fileSystem.Object,
                new Mock<Func<TimerCallback, object, TimeSpan, Timer>>().Object);
            var engine = new Mock<IFiftyOneAspectEngine>();
            engine.Setup(e => e.TempDataDirPath).Returns(System.IO.Path.GetTempPath());
            var file = new FiftyOneDataFile(typeof(IOnPremiseAspectEngine))
            {
                Engine = engine.Object,
                DataDownloadType = "test",
                Configuration = new DataFileConfiguration()
                {
                    AutomaticUpdatesEnabled = false,
                    FileSystemWatcherEnabled = false,
                    LicenseKeyRequiredForUpdates = true,
                    DataUpdateLicenseKeys = new List<string>() { KeyOne, KeyTwo },
                    DataUpdateUrl = "https://download.test.com/api",
                    UrlFormatter = new FiftyOneUrlFormatter(),
                    VerifyMd5 = false,
                },
            };
            engine.Setup(e => e.GetDataFileMetaData(It.IsAny<string>())).Returns(file);
            Assert.Contains("%7C", file.FormattedUri.AbsoluteUri,
                "The test needs the URL form with the bar encoded.");

            var text = new StringBuilder();
            try
            {
                service.CheckForUpdate(engine.Object);
            }
            catch (Exception ex)
            {
                text.AppendLine(ex.ToString());
            }
            foreach (var entry in logger.ExtendedEntries)
            {
                text.AppendLine(entry.Message);
                text.AppendLine(entry.Exception?.ToString());
            }

            var all = text.ToString();
            Assert.IsFalse(all.Contains(KeyOne), all);
            Assert.IsFalse(all.Contains(KeyTwo), all);
            Assert.IsFalse(all.Contains("ABCDEFGHIJ"), all);
            Assert.IsFalse(all.Contains("ZYXWVUTSRQ"), all);
            Assert.Contains("****************7890", all);
            Assert.Contains("****************4321", all);
        }
    }
}
