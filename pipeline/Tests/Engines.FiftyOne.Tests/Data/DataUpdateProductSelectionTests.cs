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
    /// A data file update with a license key that holds several products.
    /// The distributor answers the first request with the addresses on
    /// offer, and the update repeats its request for the product of the data
    /// file the engine has loaded. A stand-in distributor answers here, so
    /// no license key is needed.
    /// </summary>
    [TestClass]
    public class DataUpdateProductSelectionTests
    {
        private const string Key = "ABCDEFGHIJ1234567890";
        private const string Download =
            "https://distributor.test/api/v2/download";
        private const string Identifier = "data";
        private static readonly byte[] NewData =
            Encoding.ASCII.GetBytes("new data file");

        private Mock<MockHttpMessageHandler> _httpHandler;
        private DataUpdateService _dataUpdate;
        private List<Uri> _requests;
        private byte[] _refreshedWith;

        [TestInitialize]
        public void Init()
        {
            _requests = new List<Uri>();
            _refreshedWith = null;
            _httpHandler = new Mock<MockHttpMessageHandler>() { CallBase = true };
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(f => f.File).Returns(new Mock<IFileWrapper>().Object);
            fileSystem.Setup(f => f.Directory).Returns(
                new Mock<IDirectoryWrapper>().Object);
            _dataUpdate = new DataUpdateService(
                new TestLogger<DataUpdateService>(),
                new HttpClient(_httpHandler.Object),
                fileSystem.Object,
                new Mock<Func<TimerCallback, object, TimeSpan, Timer>>().Object);
        }

        /// <summary>
        /// The address the distributor offers for a product.
        /// </summary>
        private static string Offered(string type, string product)
        {
            return $"{Download}?LicenseKeys={Key}&LicenceKeys=" +
                $"&Type={type}&Product={product}";
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

        private static HttpResponseMessage Data()
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(NewData),
            };
        }

        /// <summary>
        /// The distributor's answer for a key that holds several products
        /// to a request that names none.
        /// </summary>
        private static HttpResponseMessage SeveralProducts(
            string type, params string[] products)
        {
            var uris = string.Join(",",
                products.Select(p => "\"" + Offered(type, p) + "\""));
            return new HttpResponseMessage((HttpStatusCode)300)
            {
                Content = new StringContent(
                    "{\"AvailableUris\":[" + uris + "],\"Error\":\"There are " +
                    "multiple products associated with this license key. " +
                    "Please use the URI returned in the Location header.\"}",
                    Encoding.UTF8,
                    "application/json"),
            };
        }

        /// <summary>
        /// A stand-in distributor for a key that holds the products given.
        /// A request that names no product gets the addresses on offer, a
        /// request for a product the key holds gets the data, and any
        /// other request is refused.
        /// </summary>
        private void DistributorWith(string type, params string[] products)
        {
            _httpHandler.Setup(h => h.Send(It.IsAny<HttpRequestMessage>()))
                .Returns((HttpRequestMessage request) =>
                {
                    _requests.Add(request.RequestUri);
                    var product = ProductOf(request.RequestUri);
                    if (product == null)
                    {
                        return SeveralProducts(type, products);
                    }
                    return products.Contains(product) ?
                        Data() :
                        new HttpResponseMessage(HttpStatusCode.Forbidden);
                });
        }

        /// <summary>
        /// A stand-in distributor that gives every request the same answer.
        /// </summary>
        private void DistributorAnswering(Func<HttpResponseMessage> answer)
        {
            _httpHandler.Setup(h => h.Send(It.IsAny<HttpRequestMessage>()))
                .Returns((HttpRequestMessage request) =>
                {
                    _requests.Add(request.RequestUri);
                    return answer();
                });
        }

        /// <summary>
        /// An engine that has loaded a data file of the type and tier given.
        /// </summary>
        /// <param name="type">
        /// The type of the data file.
        /// </param>
        /// <param name="tier">
        /// The data source tier the engine gives.
        /// </param>
        /// <param name="formatter">
        /// The URL formatter to use, or null for one that names no product.
        /// </param>
        /// <param name="fileProduct">
        /// The product value the engine reads from its data file, or null
        /// for an engine that gives only its tier.
        /// </param>
        private Mock<IFiftyOneAspectEngine> Engine(
            string type,
            string tier,
            FiftyOneUrlFormatter formatter = null,
            string fileProduct = null)
        {
            var engine = new Mock<IFiftyOneAspectEngine>();
            if (fileProduct != null)
            {
                engine.As<IDataFileProductEngine>()
                    .Setup(e => e.GetDataFileProduct(It.IsAny<string>()))
                    .Returns(fileProduct);
            }
            engine.Setup(e => e.GetDataDownloadType(It.IsAny<string>()))
                .Returns(type);
            engine.Setup(e => e.DataSourceTier).Returns(tier);
            engine.Setup(e => e.RefreshData(Identifier, It.IsAny<Stream>()))
                .Callback((string identifier, Stream data) =>
                {
                    using (var copy = new MemoryStream())
                    {
                        data.CopyTo(copy);
                        _refreshedWith = copy.ToArray();
                    }
                });
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

        private static IEnumerable<object[]> Products()
        {
            yield return new object[] { "HashV41", "TAC", "V4TAC" };
            yield return new object[] { "HashV41", "Enterprise", "V4Enterprise" };
            yield return new object[] { "IPIV41", "Enterprise", "IPIV4Enterprise" };
            yield return new object[] { "IPIV41", "IPIV4Enterprise", "IPIV4Enterprise" };
        }

        /// <summary>
        /// The key holds several products. The update repeats its request
        /// with the product of the loaded data file, and gives the engine
        /// the data from it.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(Products))]
        public void CheckForUpdate_SeveralProducts_RequestsProductOfLoadedFile(
            string type, string tier, string product)
        {
            var offered = new[] { "V4TAC", "V4Enterprise", "IPIV4Enterprise" }
                .Where(p => p != product).Take(1)
                .Concat(new[] { product }).ToArray();
            DistributorWith(type, offered);
            var engine = Engine(type, tier);

            var status = _dataUpdate.CheckForUpdate(engine.Object);

            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
            Assert.AreEqual(2, _requests.Count);
            Assert.IsNull(ProductOf(_requests[0]));
            // The first request again, with the product added.
            Assert.AreEqual(
                _requests[0].AbsoluteUri + "&Product=" + product,
                _requests[1].AbsoluteUri);
            CollectionAssert.AreEqual(NewData, _refreshedWith);
        }

        /// <summary>
        /// The key holds several products and none is the product of the
        /// loaded data file. The update fails with an error that names the
        /// products offered, and requests nothing more.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_SeveralProducts_NoneMatches_NamesProductsOffered()
        {
            DistributorWith("HashV41", "IPIV4Enterprise", "V4TAC");
            var engine = Engine("HashV41", "Enterprise");

            var ex = Assert.ThrowsExactly<DataUpdateException>(
                () => _dataUpdate.CheckForUpdate(engine.Object));

            Assert.AreEqual(
                "Product 'V4Enterprise' is not offered by data update " +
                $"service at '{Download}?LicenseKeys=****************7890" +
                "&Download=True&Type=HashV41' for engine " +
                "'IFiftyOneAspectEngine'. Products offered: " +
                "'IPIV4Enterprise,V4TAC'",
                ex.Message);
            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_HTTPS_ERR, ex.Status);
            Assert.DoesNotContain(Key, ex.ToString());
            Assert.AreEqual(1, _requests.Count);
            Assert.IsNull(_refreshedWith);
        }

        /// <summary>
        /// The key holds one product. The first request is answered with
        /// the data, and it is the only request.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_OneProduct_RequestsOnce()
        {
            DistributorAnswering(Data);
            var engine = Engine("HashV41", "TAC");

            var status = _dataUpdate.CheckForUpdate(engine.Object);

            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
            Assert.AreEqual(1, _requests.Count);
            Assert.AreEqual(
                $"{Download}?LicenseKeys={Key}&Download=True&Type=HashV41",
                _requests[0].AbsoluteUri);
            CollectionAssert.AreEqual(NewData, _refreshedWith);
        }

        /// <summary>
        /// The key does not hold the product asked for. The refusal is
        /// reported as it is for any other request.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_Refused_ReportsForbidden()
        {
            DistributorAnswering(
                () => new HttpResponseMessage(HttpStatusCode.Forbidden));
            var engine = Engine("HashV41", "TAC");

            var ex = Assert.ThrowsExactly<DataUpdateException>(
                () => _dataUpdate.CheckForUpdate(engine.Object));

            Assert.AreEqual(
                AutoUpdateStatus.AUTO_UPDATE_ERR_403_FORBIDDEN, ex.Status);
            Assert.AreEqual(1, _requests.Count);
            Assert.IsNull(_refreshedWith);
        }

        /// <summary>
        /// The URL formatter is given a product name. An update on start-up,
        /// when no data file is loaded, names that product in its first
        /// request, and makes no other request.
        /// </summary>
        [TestMethod]
        public void UpdateOnStartup_ProductNamed_FirstRequestNamesProduct()
        {
            DistributorWith("HashV41", "IPIV4Enterprise", "V4TAC");
            using (var file = new FiftyOneDataFile(typeof(IFiftyOneAspectEngine))
            {
                Identifier = Identifier,
                DataDownloadType = "HashV41",
                Configuration = new DataFileConfiguration()
                {
                    Identifier = Identifier,
                    DataUpdateUrl = Download,
                    DataUpdateLicenseKeys = new[] { Key },
                    UrlFormatter = new FiftyOneUrlFormatter("V4TAC"),
                    MemoryOnly = true,
                    DecompressContent = false,
                    VerifyMd5 = false,
                    VerifyModifiedSince = false,
                    FileSystemWatcherEnabled = false,
                    AutomaticUpdatesEnabled = false,
                    UpdateOnStartup = true,
                },
            })
            {
                _dataUpdate.RegisterDataFile(file);

                Assert.AreEqual(1, _requests.Count);
                Assert.AreEqual(
                    $"{Download}?LicenseKeys={Key}&Download=True" +
                    "&Type=HashV41&Product=V4TAC",
                    _requests[0].AbsoluteUri);
                using (var data = new MemoryStream())
                {
                    file.Configuration.DataStream.CopyTo(data);
                    CollectionAssert.AreEqual(NewData, data.ToArray());
                }
            }
        }

        /// <summary>
        /// The URL formatter is given a product name and the engine has
        /// loaded a data file of another tier. The product named is the one
        /// requested.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_ProductNamed_RequestsProductNamed()
        {
            DistributorWith("HashV41", "IPIV4Enterprise", "V4TAC");
            var engine = Engine(
                "HashV41", "Lite", new FiftyOneUrlFormatter("V4TAC"));

            var status = _dataUpdate.CheckForUpdate(engine.Object);

            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
            Assert.AreEqual(1, _requests.Count);
            Assert.AreEqual("V4TAC", ProductOf(_requests[0]));
            CollectionAssert.AreEqual(NewData, _refreshedWith);
        }

        /// <summary>
        /// A URL formatter given no product name has none, and one given a
        /// name keeps it.
        /// </summary>
        [TestMethod]
        public void Formatter_Product_IsTheNameGiven()
        {
            Assert.IsNull(new FiftyOneUrlFormatter().Product);
            Assert.AreEqual("V4TAC", new FiftyOneUrlFormatter("V4TAC").Product);
        }

        /// <summary>
        /// The engine gives the product value of its data file apart from
        /// its data source tier. The product requested is made from that
        /// value.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_SeveralProducts_RequestsProductOfDataFile()
        {
            DistributorWith("IPIV41", "V4TAC", "IPIV4Enterprise");
            var engine = Engine(
                "IPIV41", "IPIV41", fileProduct: "Enterprise");

            var status = _dataUpdate.CheckForUpdate(engine.Object);

            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
            Assert.AreEqual(2, _requests.Count);
            Assert.AreEqual("IPIV4Enterprise", ProductOf(_requests[1]));
            CollectionAssert.AreEqual(NewData, _refreshedWith);
        }
    }
}
