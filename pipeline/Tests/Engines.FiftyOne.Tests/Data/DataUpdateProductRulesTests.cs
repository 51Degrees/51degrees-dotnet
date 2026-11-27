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
using FiftyOne.Pipeline.Engines.Exceptions;
using FiftyOne.Pipeline.Engines.FiftyOne.Data;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Tests.Data
{
    /// <summary>
    /// The rules for a data file update when the distributor answers that
    /// the license keys hold several products: which answers are an error,
    /// what a product named in the URL formatter changes, and what is
    /// logged. A stand-in distributor answers here, so no license key is
    /// needed.
    /// </summary>
    [TestClass]
    public class DataUpdateProductRulesTests
    {
        private const string Key = "ABCDEFGHIJ1234567890";
        private const string MaskedKeys = "LicenseKeys=****************7890";
        private const string Download =
            "https://distributor.test/api/v2/download";
        private const string Identifier = "data";
        private const string StatusError = "HTTP status code '";
        private static readonly byte[] NewData =
            Encoding.ASCII.GetBytes("new data file");

        private TestLogger<DataUpdateService> _logger;
        private Mock<MockHttpMessageHandler> _httpHandler;
        private DataUpdateService _dataUpdate;
        private List<Uri> _requests;

        [TestInitialize]
        public void Init()
        {
            _requests = new List<Uri>();
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
        }

        /// <summary>
        /// The value of the Product parameter in a request, or null.
        /// </summary>
        private static string ProductOf(Uri uri)
        {
            return uri.Query.TrimStart('?').Split('&')
                .Where(p => p.StartsWith("Product=", StringComparison.Ordinal))
                .Select(p => p.Substring("Product=".Length))
                .FirstOrDefault();
        }

        /// <summary>
        /// The body that lists an address for each product given.
        /// </summary>
        private static string ListOf(params string[] products)
        {
            return "{\"AvailableUris\":[" + string.Join(",", products.Select(
                p => "\"" + Download + "?Type=HashV41&Product=" + p + "\"")) +
                "],\"Error\":\"There are multiple products associated " +
                "with this license key.\"}";
        }

        /// <summary>
        /// A stand-in distributor that answers 300 with the body given to
        /// a request that names no product, and to every request when
        /// <paramref name="always"/> is true. Any other request gets the
        /// data.
        /// </summary>
        private void DistributorAnswering(string body, bool always)
        {
            _httpHandler.Setup(h => h.Send(It.IsAny<HttpRequestMessage>()))
                .Returns((HttpRequestMessage request) =>
                {
                    _requests.Add(request.RequestUri);
                    if (always || ProductOf(request.RequestUri) == null)
                    {
                        return new HttpResponseMessage((HttpStatusCode)300)
                        {
                            Content = new StringContent(
                                body, Encoding.UTF8, "application/json"),
                        };
                    }
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(NewData),
                    };
                });
        }

        /// <summary>
        /// An engine that has loaded a data file of the type and tier given.
        /// </summary>
        private static Mock<IFiftyOneAspectEngine> Engine(
            string type,
            string tier,
            FiftyOneUrlFormatter formatter = null)
        {
            var engine = new Mock<IFiftyOneAspectEngine>();
            engine.Setup(e => e.GetDataDownloadType(It.IsAny<string>()))
                .Returns(type);
            engine.Setup(e => e.DataSourceTier).Returns(tier);
            var file = new FiftyOneDataFile(typeof(IFiftyOneAspectEngine))
            {
                Identifier = Identifier,
                Engine = engine.Object,
                Configuration = new DataFileConfiguration()
                {
                    Identifier = Identifier,
                    DataUpdateUrl = Download,
                    DataUpdateLicenseKeys = new[] { Key },
                    UrlFormatter = formatter ?? new FiftyOneUrlFormatter(),
                    MemoryOnly = true,
                    DecompressContent = false,
                    VerifyMd5 = false,
                    VerifyModifiedSince = false,
                    FileSystemWatcherEnabled = false,
                    AutomaticUpdatesEnabled = false,
                },
            };
            engine.Setup(e => e.GetDataFileMetaData(It.IsAny<string>()))
                .Returns(file);
            return engine;
        }

        /// <summary>
        /// Checks that the update fails with the error any other response
        /// gives, after the number of requests given.
        /// </summary>
        private void AssertStatusError(
            Mock<IFiftyOneAspectEngine> engine, int requests)
        {
            var ex = Assert.ThrowsExactly<DataUpdateException>(
                () => _dataUpdate.CheckForUpdate(engine.Object));

            Assert.IsTrue(
                ex.Message.StartsWith(StatusError, StringComparison.Ordinal),
                "The error is not the one for a response status.");
            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_HTTPS_ERR, ex.Status);
            Assert.IsFalse(ex.ToString().Contains(Key));
            Assert.AreEqual(requests, _requests.Count);
        }

        /// <summary>
        /// Checks that the update fails with the error that names the
        /// product needed and the products offered, after one request.
        /// </summary>
        private void AssertNotOffered(
            Mock<IFiftyOneAspectEngine> engine, string needed, string offered)
        {
            var ex = Assert.ThrowsExactly<DataUpdateException>(
                () => _dataUpdate.CheckForUpdate(engine.Object));

            Assert.IsTrue(
                ex.Message.StartsWith(
                    $"Product '{needed}' is not offered by data update " +
                    "service at '", StringComparison.Ordinal),
                "The error does not name the product needed.");
            Assert.IsTrue(
                ex.Message.EndsWith(
                    $"Products offered: '{offered}'", StringComparison.Ordinal),
                "The error does not name the products offered.");
            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_HTTPS_ERR, ex.Status);
            Assert.IsFalse(ex.ToString().Contains(Key));
            Assert.AreEqual(1, _requests.Count);
        }

        /// <summary>
        /// The URL formatter is given a product name, and the distributor
        /// still answers with a list that offers it. The answer is not
        /// followed. It is the error any other response gives, after the
        /// one request.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_ProductNamed_ListOffersIt_IsTheStatusError()
        {
            DistributorAnswering(ListOf("IPIV4Enterprise", "V4TAC"), true);
            var engine = Engine(
                "HashV41", "Enterprise", new FiftyOneUrlFormatter("V4TAC"));

            AssertStatusError(engine, 1);

            Assert.AreEqual("V4TAC", ProductOf(_requests[0]));
        }

        /// <summary>
        /// The URL formatter is given a product name, and the distributor
        /// answers with a list that does not offer it. No product is
        /// chosen from the list, so the error is the one any other
        /// response gives.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_ProductNamed_ListLacksIt_IsTheStatusError()
        {
            DistributorAnswering(ListOf("IPIV4Enterprise", "V4TAC"), true);
            var engine = Engine(
                "HashV41", "TAC", new FiftyOneUrlFormatter("V4Enterprise"));

            AssertStatusError(engine, 1);

            Assert.AreEqual("V4Enterprise", ProductOf(_requests[0]));
        }

        /// <summary>
        /// The list in the answer is empty. It is the error any other
        /// response gives.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_ListIsEmpty_IsTheStatusError()
        {
            DistributorAnswering("{\"AvailableUris\":[]}", false);

            AssertStatusError(Engine("HashV41", "TAC"), 1);
        }

        /// <summary>
        /// No address in the list names a product. It is the error any
        /// other response gives.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_ListNamesNoProduct_IsTheStatusError()
        {
            DistributorAnswering(
                "{\"AvailableUris\":[\"" + Download + "?Type=HashV41\",\"" +
                Download + "\"]}", false);

            AssertStatusError(Engine("HashV41", "TAC"), 1);
        }

        /// <summary>
        /// The answer has no list. It is the error any other response
        /// gives.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_AnswerHasNoList_IsTheStatusError()
        {
            DistributorAnswering("{\"Error\":\"several products\"}", false);

            AssertStatusError(Engine("HashV41", "TAC"), 1);
        }

        /// <summary>
        /// The answer is not JSON. It is the error any other response
        /// gives.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_AnswerIsNotJson_IsTheStatusError()
        {
            DistributorAnswering("several products", false);

            AssertStatusError(Engine("HashV41", "TAC"), 1);
        }

        /// <summary>
        /// The second request is answered with a list as well. It is not
        /// followed, and is the error any other response gives.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_SecondAnswerIsAList_IsTheStatusError()
        {
            DistributorAnswering(ListOf("IPIV4Enterprise", "V4TAC"), true);

            AssertStatusError(Engine("HashV41", "TAC"), 2);

            Assert.IsNull(ProductOf(_requests[0]));
            Assert.AreEqual("V4TAC", ProductOf(_requests[1]));
        }

        /// <summary>
        /// The data file is of a kind that has no product prefix. No
        /// product name is known, so the update fails with the error that
        /// names the products offered, and requests nothing more.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_KindHasNoPrefix_NamesNoProduct()
        {
            DistributorAnswering(ListOf("V4Enterprise", "V4TAC"), false);

            AssertNotOffered(
                Engine("OtherV1", "Enterprise"), "", "V4Enterprise,V4TAC");
        }

        /// <summary>
        /// The tier of the loaded data file is empty. No product name is
        /// known, so the update fails with the error that names the
        /// products offered.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_TierIsEmpty_NamesNoProduct()
        {
            DistributorAnswering(ListOf("V4Enterprise", "V4TAC"), false);

            AssertNotOffered(
                Engine("HashV41", ""), "", "V4Enterprise,V4TAC");
        }

        /// <summary>
        /// The list spells the product in another case. It is still
        /// chosen, and the second request carries the value as the list
        /// spells it.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_ProductInAnotherCase_IsChosen()
        {
            DistributorAnswering(ListOf("IPIV4Enterprise", "v4tac"), false);
            var engine = Engine("HashV41", "TAC");

            var status = _dataUpdate.CheckForUpdate(engine.Object);

            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
            Assert.AreEqual(2, _requests.Count);
            Assert.AreEqual("v4tac", ProductOf(_requests[1]));
        }

        /// <summary>
        /// The second request is logged as a request is, with the license
        /// key hidden as it is for the first. The choice of product is
        /// logged once, between the two.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_SeveralProducts_LogsTheSecondRequest()
        {
            DistributorAnswering(ListOf("IPIV4Enterprise", "V4TAC"), false);
            var engine = Engine("HashV41", "TAC");

            var status = _dataUpdate.CheckForUpdate(engine.Object);

            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
            var info = _logger.InfoEntries.ToList();
            var checks = info
                .Where(m => m.Contains("Checking for update from"))
                .ToList();
            Assert.AreEqual(2, checks.Count);
            Assert.IsFalse(checks[0].Contains("Product="));
            Assert.IsTrue(checks[0].Contains(MaskedKeys));
            Assert.IsTrue(checks[1].Contains("&Product=V4TAC"));
            Assert.IsTrue(checks[1].Contains(MaskedKeys));
            Assert.AreEqual(1, info.Count(m => m.Contains(
                "Requesting product 'V4TAC' from '")));
            Assert.IsFalse(
                _logger.ExtendedEntries.Any(e => e.Message.Contains(Key)),
                "A log entry holds the license key in full.");
        }
    }
}
