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

        private TestLogger<DataUpdateService> _logger;
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
            _logger = new TestLogger<DataUpdateService>();
            _dataUpdate = new DataUpdateService(
                _logger,
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
            Assert.IsNull(new FiftyOneUrlFormatter(null).Product);
            Assert.IsNull(new FiftyOneUrlFormatter("").Product);
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

        /// <summary>
        /// The message for a response that is not a success and that the
        /// update does not act on.
        /// </summary>
        private static string StatusMessage(string url)
        {
            return "HTTP status code 'MultipleChoices' from data update " +
                $"service at '{url}' for engine 'IFiftyOneAspectEngine'";
        }

        /// <summary>
        /// The first request of an update for a Device Detection data file,
        /// as it is shown in messages.
        /// </summary>
        private const string ShownFirstRequest =
            Download + "?LicenseKeys=****************7890" +
            "&Download=True&Type=HashV41";

        /// <summary>
        /// The repeated request is answered with the products on offer
        /// again. That answer is not acted on, and the update fails as it
        /// does for any other response.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_SeveralProducts_SecondAnswerIsNotFollowed()
        {
            DistributorAnswering(
                () => SeveralProducts("HashV41", "IPIV4Enterprise", "V4TAC"));
            var engine = Engine("HashV41", "TAC");

            var ex = Assert.ThrowsExactly<DataUpdateException>(
                () => _dataUpdate.CheckForUpdate(engine.Object));

            Assert.AreEqual(
                StatusMessage(ShownFirstRequest + "&Product=V4TAC"),
                ex.Message);
            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_HTTPS_ERR, ex.Status);
            Assert.AreEqual(2, _requests.Count);
            Assert.IsNull(_refreshedWith);
        }

        private static IEnumerable<object[]> AnswersWithNoProducts()
        {
            yield return new object[] { "not JSON" };
            yield return new object[] { "[]" };
            yield return new object[] { "{}" };
            yield return new object[] { "{\"AvailableUris\":\"none\"}" };
            yield return new object[] { "{\"AvailableUris\":[]}" };
            yield return new object[]
            {
                "{\"AvailableUris\":[\"" + Download +
                "?LicenseKeys=" + Key + "&Type=HashV41\"]}"
            };
        }

        /// <summary>
        /// A 300 response that does not list a product is not acted on,
        /// and the update fails as it does for any other response.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(AnswersWithNoProducts))]
        public void CheckForUpdate_AnswerListsNoProduct_ReportsStatus(string body)
        {
            DistributorAnswering(() => new HttpResponseMessage((HttpStatusCode)300)
            {
                Content = new StringContent(body, Encoding.UTF8),
            });
            var engine = Engine("HashV41", "TAC");

            var ex = Assert.ThrowsExactly<DataUpdateException>(
                () => _dataUpdate.CheckForUpdate(engine.Object));

            Assert.AreEqual(StatusMessage(ShownFirstRequest), ex.Message);
            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_HTTPS_ERR, ex.Status);
            Assert.DoesNotContain(Key, ex.ToString());
            Assert.AreEqual(1, _requests.Count);
        }

        /// <summary>
        /// The name of the Product parameter in an address on offer is
        /// matched ignoring case.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_SeveralProducts_ParameterNameIgnoresCase()
        {
            _httpHandler.Setup(h => h.Send(It.IsAny<HttpRequestMessage>()))
                .Returns((HttpRequestMessage request) =>
                {
                    _requests.Add(request.RequestUri);
                    if (ProductOf(request.RequestUri) != null)
                    {
                        return Data();
                    }
                    return new HttpResponseMessage((HttpStatusCode)300)
                    {
                        Content = new StringContent(
                            "{\"AvailableUris\":[\"" + Download +
                            "?LicenseKeys=" + Key + "&Type=HashV41" +
                            "&product=v4tac\"]}",
                            Encoding.UTF8),
                    };
                });
            var engine = Engine("HashV41", "TAC");

            var status = _dataUpdate.CheckForUpdate(engine.Object);

            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
            Assert.AreEqual(2, _requests.Count);
            Assert.AreEqual("v4tac", ProductOf(_requests[1]));
        }

        /// <summary>
        /// The URL formatter is given a product name, and the request for
        /// it is answered with the products on offer. That answer is not
        /// read or acted on, and no other request is made.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_ProductNamed_AnswerListingProductsIsNotFollowed()
        {
            DistributorAnswering(
                () => SeveralProducts("HashV41", "IPIV4Enterprise", "V4TAC"));
            var engine = Engine(
                "HashV41", "TAC", new FiftyOneUrlFormatter("V4TAC"));

            var ex = Assert.ThrowsExactly<DataUpdateException>(
                () => _dataUpdate.CheckForUpdate(engine.Object));

            Assert.AreEqual(
                StatusMessage(ShownFirstRequest + "&Product=V4TAC"),
                ex.Message);
            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_HTTPS_ERR, ex.Status);
            Assert.AreEqual(1, _requests.Count);
        }

        /// <summary>
        /// The URL formatter is given a product name that the license keys
        /// do not hold. The refusal is reported as it is for any request.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_ProductNamed_Refused_ReportsForbidden()
        {
            DistributorWith("HashV41", "IPIV4Enterprise", "V4TAC");
            var engine = Engine(
                "HashV41", "TAC", new FiftyOneUrlFormatter("V4Enterprise"));

            var ex = Assert.ThrowsExactly<DataUpdateException>(
                () => _dataUpdate.CheckForUpdate(engine.Object));

            Assert.AreEqual(
                AutoUpdateStatus.AUTO_UPDATE_ERR_403_FORBIDDEN, ex.Status);
            Assert.AreEqual(1, _requests.Count);
        }

        private static IEnumerable<object[]> FilesWithNoProductName()
        {
            // No tier.
            yield return new object[] { "HashV41", null };
            yield return new object[] { "HashV41", "" };
            // A kind of data file that has no prefix.
            yield return new object[] { "Other", "TAC" };
            yield return new object[] { null, "TAC" };
            // A prefix alone.
            yield return new object[] { "HashV41", "V4" };
            yield return new object[] { "IPIV41", "ipiv4" };
        }

        /// <summary>
        /// No product name is known for a data file with no tier or of a
        /// kind that has no prefix, and a prefix alone is not a product
        /// name.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(FilesWithNoProductName))]
        public void DataFile_Product_NotKnown(string type, string tier)
        {
            var engine = Engine(type, tier);

            Assert.IsNull(((FiftyOneDataFile)engine.Object
                .GetDataFileMetaData()).DataUpdateProduct);
        }

        /// <summary>
        /// No product name is known for a data file with no engine.
        /// </summary>
        [TestMethod]
        public void DataFile_Product_NotKnownWithoutAnEngine()
        {
            using (var file = new FiftyOneDataFile(typeof(IFiftyOneAspectEngine))
            {
                DataDownloadType = "HashV41",
            })
            {
                Assert.IsNull(file.DataUpdateProduct);
            }
        }

        /// <summary>
        /// The key holds several products and the engine cannot give its
        /// tier. The update fails with the error that names the products
        /// offered, with no product needed.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_SeveralProducts_TierNotAvailable_NamesProductsOffered()
        {
            DistributorWith("HashV41", "IPIV4Enterprise", "V4TAC");
            var engine = Engine("HashV41", "TAC");
            engine.Setup(e => e.DataSourceTier)
                .Throws(new InvalidOperationException("No data"));

            var ex = Assert.ThrowsExactly<DataUpdateException>(
                () => _dataUpdate.CheckForUpdate(engine.Object));

            Assert.AreEqual(
                "Product '' is not offered by data update service at '" +
                ShownFirstRequest + "' for engine 'IFiftyOneAspectEngine'. " +
                "Products offered: 'IPIV4Enterprise,V4TAC'",
                ex.Message);
            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_HTTPS_ERR, ex.Status);
            Assert.AreEqual(1, _requests.Count);
        }

        /// <summary>
        /// A stand-in for an HTTP client that follows redirects, with a
        /// distributor for a key that holds the products given. The
        /// distributor answers a request that names no product with a
        /// redirect to the address of the first product, which the client
        /// follows, so the service is given the answer from that address.
        /// </summary>
        private void RedirectingDistributorWith(
            string type, params string[] products)
        {
            _httpHandler.Setup(h => h.Send(It.IsAny<HttpRequestMessage>()))
                .Returns((HttpRequestMessage request) =>
                {
                    _requests.Add(request.RequestUri);
                    var product = ProductOf(request.RequestUri);
                    if (product == null)
                    {
                        // The client follows the redirect.
                        product = products[0];
                        request.RequestUri = new Uri(Offered(type, product));
                    }
                    // Only the first product on offer is of the type asked
                    // for in this stand-in when it starts with the type's
                    // family, so another family's address is not found.
                    var found = products.Contains(product) &&
                        product.StartsWith("IPI", StringComparison.Ordinal) ==
                        type.StartsWith("IPI", StringComparison.Ordinal);
                    return found ?
                        Data() :
                        new HttpResponseMessage(HttpStatusCode.NotFound);
                });
        }

        /// <summary>
        /// The HTTP client follows the distributor's redirect to the
        /// address of a product that is not the one of the loaded data
        /// file. The update repeats its request with the product of the
        /// loaded data file.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_ClientFollowsRedirectToAnotherProduct_RequestsProductOfLoadedFile()
        {
            RedirectingDistributorWith("HashV41", "IPIV4Enterprise", "V4TAC");
            var engine = Engine("HashV41", "TAC");

            var status = _dataUpdate.CheckForUpdate(engine.Object);

            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
            Assert.AreEqual(2, _requests.Count);
            Assert.IsNull(ProductOf(_requests[0]));
            Assert.AreEqual(
                _requests[0].AbsoluteUri + "&Product=V4TAC",
                _requests[1].AbsoluteUri);
            CollectionAssert.AreEqual(NewData, _refreshedWith);
        }

        /// <summary>
        /// The HTTP client follows the distributor's redirect to the
        /// address of the product of the loaded data file. That address is
        /// not the update URL, so its answer is not used. The update
        /// repeats its request with the product of the loaded data file.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_ClientFollowsRedirectToProductOfLoadedFile_RequestsProductOfLoadedFile()
        {
            RedirectingDistributorWith("HashV41", "V4TAC", "V4Enterprise");
            var engine = Engine("HashV41", "TAC");

            var status = _dataUpdate.CheckForUpdate(engine.Object);

            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
            Assert.AreEqual(2, _requests.Count);
            Assert.IsNull(ProductOf(_requests[0]));
            Assert.AreEqual(
                _requests[0].AbsoluteUri + "&Product=V4TAC",
                _requests[1].AbsoluteUri);
            CollectionAssert.AreEqual(NewData, _refreshedWith);
        }

        /// <summary>
        /// The HTTP client follows the distributor's redirect to the
        /// address of a product, and no product name is known for the
        /// loaded data file. The update fails with the error that names
        /// the product the redirect offered.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_ClientFollowsRedirect_ProductNotKnown_NamesProductOffered()
        {
            RedirectingDistributorWith("HashV41", "IPIV4Enterprise", "V4TAC");
            var engine = Engine("HashV41", "");

            var ex = Assert.ThrowsExactly<DataUpdateException>(
                () => _dataUpdate.CheckForUpdate(engine.Object));

            Assert.AreEqual(
                "Product '' is not offered by data update service at '" +
                ShownFirstRequest + "' for engine 'IFiftyOneAspectEngine'. " +
                "Products offered: 'IPIV4Enterprise'",
                ex.Message);
            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_HTTPS_ERR, ex.Status);
            Assert.AreEqual(1, _requests.Count);
            Assert.IsNull(_refreshedWith);
        }

        /// <summary>
        /// The URL formatter is given a product name and the HTTP client
        /// follows a redirect. The answer is used as it is.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_ProductNamed_ClientFollowsRedirect_RequestsOnce()
        {
            _httpHandler.Setup(h => h.Send(It.IsAny<HttpRequestMessage>()))
                .Returns((HttpRequestMessage request) =>
                {
                    _requests.Add(request.RequestUri);
                    request.RequestUri = new Uri(
                        "https://mirror.test/files?Product=Other");
                    return Data();
                });
            var engine = Engine(
                "HashV41", "TAC", new FiftyOneUrlFormatter("V4TAC"));

            var status = _dataUpdate.CheckForUpdate(engine.Object);

            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
            Assert.AreEqual(1, _requests.Count);
            CollectionAssert.AreEqual(NewData, _refreshedWith);
        }

        /// <summary>
        /// The answer a local server gives a request.
        /// </summary>
        private class LocalAnswer
        {
            public int Status { get; set; }
            public string Location { get; set; }
            public byte[] Body { get; set; } = new byte[0];
        }

        private static readonly byte[] OtherData =
            Encoding.ASCII.GetBytes("data file of another product");

        /// <summary>
        /// Held while a local server runs. The test methods run in
        /// parallel, and HttpListener refuses or resets connections when
        /// listeners in one process start and stop at the same time, so
        /// one local server runs at a time.
        /// </summary>
        private static readonly object LocalServerLock = new object();

        /// <summary>
        /// Run an update of a TAC Device Detection data file through an
        /// HTTP client against a local server that answers as given.
        /// </summary>
        /// <param name="answer">
        /// Gives the answer to a request from the address of the update
        /// URL and the address requested.
        /// </param>
        /// <param name="served">
        /// The path and query of each request the server was sent.
        /// </param>
        /// <param name="clientFollowsRedirects">
        /// True for an HTTP client with its default settings, which
        /// follows redirects. False for one set not to.
        /// </param>
        /// <returns>
        /// The status of the update.
        /// </returns>
        private AutoUpdateStatus UpdateFromLocalServer(
            Func<string, Uri, LocalAnswer> answer,
            out List<string> served,
            bool clientFollowsRedirects = true)
        {
            lock (LocalServerLock)
            {
                return UpdateFromLocalServerAlone(
                    answer, out served, clientFollowsRedirects);
            }
        }

        /// <summary>
        /// The body of <see cref="UpdateFromLocalServer"/>, run while
        /// <see cref="LocalServerLock"/> is held.
        /// </summary>
        private AutoUpdateStatus UpdateFromLocalServerAlone(
            Func<string, Uri, LocalAnswer> answer,
            out List<string> served,
            bool clientFollowsRedirects)
        {
            HttpListener listener = null;
            string address = null;
            for (var port = 50151; port < 50171 && address == null; port++)
            {
                var candidate = $"http://localhost:{port}/";
                var attempt = new HttpListener();
                attempt.Prefixes.Add(candidate);
                try
                {
                    attempt.Start();
                    listener = attempt;
                    address = candidate + "api/v2/download";
                }
                catch (HttpListenerException)
                {
                    attempt.Close();
                }
                catch (PlatformNotSupportedException)
                {
                    break;
                }
            }
            if (listener == null)
            {
                Assert.Inconclusive("No local HTTP server could be started.");
            }

            var requests = new List<string>();
            var server = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    while (listener.IsListening)
                    {
                        var context = listener.GetContext();
                        lock (requests)
                        {
                            requests.Add(context.Request.Url.PathAndQuery);
                        }
                        var result = answer(address, context.Request.Url);
                        context.Response.StatusCode = result.Status;
                        if (result.Location != null)
                        {
                            context.Response.RedirectLocation = result.Location;
                        }
                        context.Response.ContentLength64 = result.Body.Length;
                        context.Response.OutputStream.Write(
                            result.Body, 0, result.Body.Length);
                        context.Response.Close();
                    }
                }
                catch (HttpListenerException)
                {
                    // The listener was stopped.
                }
                catch (ObjectDisposedException)
                {
                    // The listener was closed.
                }
                catch (InvalidOperationException)
                {
                    // The listener was stopped.
                }
            });

            try
            {
                var fileSystem = new Mock<IFileSystem>();
                fileSystem.Setup(f => f.File)
                    .Returns(new Mock<IFileWrapper>().Object);
                fileSystem.Setup(f => f.Directory)
                    .Returns(new Mock<IDirectoryWrapper>().Object);
                using (var client = new HttpClient(new HttpClientHandler()
                {
                    AllowAutoRedirect = clientFollowsRedirects,
                }))
                {
                    var dataUpdate = new DataUpdateService(
                        _logger,
                        client,
                        fileSystem.Object,
                        new Mock<Func<TimerCallback, object, TimeSpan, Timer>>()
                            .Object);
                    var engine = Engine("HashV41", "TAC");
                    engine.Object.GetDataFileMetaData()
                        .Configuration.DataUpdateUrl = address;
                    return dataUpdate.CheckForUpdate(engine.Object);
                }
            }
            finally
            {
                listener.Stop();
                listener.Close();
                server.Wait(TimeSpan.FromSeconds(5));
                lock (requests)
                {
                    served = requests.ToList();
                }
            }
        }

        /// <summary>
        /// The answer of a distributor for a key that holds V4Enterprise
        /// and V4TAC, in that order. Each product has its own data.
        /// </summary>
        private static LocalAnswer SeveralProductsAnswer(
            string address, Uri requested, bool withLocation)
        {
            var product = ProductOf(requested);
            if (product == null)
            {
                var first = address + "?LicenseKeys=" + Key +
                    "&LicenceKeys=&Type=HashV41&Product=V4Enterprise";
                var second = address + "?LicenseKeys=" + Key +
                    "&LicenceKeys=&Type=HashV41&Product=V4TAC";
                return new LocalAnswer()
                {
                    Status = 300,
                    Location = withLocation ? first : null,
                    Body = Encoding.UTF8.GetBytes(
                        "{\"AvailableUris\":[\"" + first + "\",\"" +
                        second + "\"],\"Error\":\"There are multiple " +
                        "products associated with this license key. " +
                        "Please use the URI returned in the Location " +
                        "header.\"}"),
                };
            }
            return new LocalAnswer()
            {
                Status = 200,
                Body = product == "V4TAC" ? NewData : OtherData,
            };
        }

        /// <summary>
        /// The 300 answer carries a Location header for the first product
        /// on offer, as the distributor's does, and the HTTP client follows
        /// redirects. The update downloads the product of the loaded data
        /// file, not the product the header names.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_RedirectingClient_AnswerWithLocation_DownloadsProductOfLoadedFile()
        {
            var status = UpdateFromLocalServer(
                (address, requested) =>
                    SeveralProductsAnswer(address, requested, true),
                out var served);

            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
            CollectionAssert.AreEqual(NewData, _refreshedWith);
            Assert.AreEqual(
                $"/api/v2/download?LicenseKeys={Key}&Download=True" +
                "&Type=HashV41&Product=V4TAC",
                served.Last());
        }

        /// <summary>
        /// The 300 answer carries no Location header, so the HTTP client
        /// gives it to the update as it is. The update downloads the
        /// product of the loaded data file in two requests.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_RedirectingClient_AnswerWithoutLocation_DownloadsProductOfLoadedFile()
        {
            var status = UpdateFromLocalServer(
                (address, requested) =>
                    SeveralProductsAnswer(address, requested, false),
                out var served);

            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
            CollectionAssert.AreEqual(NewData, _refreshedWith);
            Assert.AreEqual(2, served.Count);
            Assert.AreEqual(
                $"/api/v2/download?LicenseKeys={Key}&Download=True" +
                "&Type=HashV41&Product=V4TAC",
                served[1]);
        }

        /// <summary>
        /// The update URL answers 302 to another address, which has the
        /// data. The redirect is followed as it always was, and the update
        /// asks for nothing more.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_RedirectingClient_Found_IsFollowed()
        {
            var status = UpdateFromLocalServer(
                (address, requested) =>
                    requested.AbsolutePath == "/api/v2/download" ?
                    new LocalAnswer()
                    {
                        Status = 302,
                        Location = address.Replace(
                            "/api/v2/download", "/mirror/file"),
                    } :
                    new LocalAnswer() { Status = 200, Body = NewData },
                out var served);

            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
            CollectionAssert.AreEqual(NewData, _refreshedWith);
            Assert.AreEqual(2, served.Count);
            Assert.AreEqual("/mirror/file", served[1]);
        }

        /// <summary>
        /// The repeated request carries the same If-Modified-Since date as
        /// the first.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_SeveralProducts_RepeatsIfModifiedSince()
        {
            var dates = new List<DateTimeOffset?>();
            _httpHandler.Setup(h => h.Send(It.IsAny<HttpRequestMessage>()))
                .Returns((HttpRequestMessage request) =>
                {
                    _requests.Add(request.RequestUri);
                    dates.Add(request.Headers.IfModifiedSince);
                    return ProductOf(request.RequestUri) == null ?
                        SeveralProducts("HashV41", "IPIV4Enterprise", "V4TAC") :
                        Data();
                });
            var engine = Engine("HashV41", "TAC");
            var file = engine.Object.GetDataFileMetaData();
            file.Configuration.VerifyModifiedSince = true;
            var published = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            file.DataPublishedDateTime = published;

            var status = _dataUpdate.CheckForUpdate(engine.Object);

            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
            Assert.AreEqual(2, dates.Count);
            Assert.AreEqual(new DateTimeOffset(published), dates[0]);
            Assert.AreEqual(dates[0], dates[1]);
        }

        /// <summary>
        /// The repeated request is logged with the product and the URL of
        /// the first request, and the license key is not in the log.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_SeveralProducts_LogsProductRequested()
        {
            DistributorWith("HashV41", "IPIV4Enterprise", "V4TAC");
            var engine = Engine("HashV41", "TAC");

            _dataUpdate.CheckForUpdate(engine.Object);

            var messages = _logger.ExtendedEntries
                .Select(e => e.Message).ToList();
            Assert.IsTrue(
                messages.Contains(
                    "Requesting product 'V4TAC' from '" + ShownFirstRequest +
                    "' for Engine 'IFiftyOneAspectEngine'"),
                string.Join(Environment.NewLine, messages));
            Assert.IsFalse(messages.Any(m => m != null && m.Contains(Key)));
        }

        /// <summary>
        /// A product on offer with an empty name is passed over.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_SeveralProducts_EmptyProductIsPassedOver()
        {
            DistributorAnswering(() => new HttpResponseMessage((HttpStatusCode)300)
            {
                Content = new StringContent(
                    "{\"AvailableUris\":[\"" + Download + "?LicenseKeys=" +
                    Key + "&Type=HashV41&Product=\"]}",
                    Encoding.UTF8),
            });
            var engine = Engine("HashV41", "");

            var ex = Assert.ThrowsExactly<DataUpdateException>(
                () => _dataUpdate.CheckForUpdate(engine.Object));

            Assert.AreEqual(StatusMessage(ShownFirstRequest), ex.Message);
            Assert.AreEqual(1, _requests.Count);
        }

        /// <summary>
        /// A URL formatter of the caller's own. The request is repeated
        /// with the product of the loaded data file added to its URL.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_OwnFormatter_SeveralProducts_RequestsProductOfLoadedFile()
        {
            DistributorWith("HashV41", "IPIV4Enterprise", "V4TAC");
            var engine = Engine("HashV41", "TAC");
            var formatter = new Mock<IDataUpdateUrlFormatter>();
            formatter
                .Setup(f => f.GetFormattedDataUpdateUri(
                    It.IsAny<IAspectEngineDataFile>()))
                .Returns(new Uri(Download + "?Type=HashV41"));
            engine.Object.GetDataFileMetaData()
                .Configuration.UrlFormatter = formatter.Object;

            var status = _dataUpdate.CheckForUpdate(engine.Object);

            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
            Assert.AreEqual(2, _requests.Count);
            Assert.AreEqual(
                Download + "?Type=HashV41&Product=V4TAC",
                _requests[1].AbsoluteUri);
        }

        private static IEnumerable<object[]> UrlsWithoutAFormatter()
        {
            yield return new object[]
            {
                Download + "?LicenseKeys=" + Key + "&Type=HashV41",
                Download + "?LicenseKeys=" + Key + "&Type=HashV41" +
                    "&Product=V4TAC",
            };
            yield return new object[]
            {
                "https://files.test/data",
                "https://files.test/data?Product=V4TAC",
            };
        }

        /// <summary>
        /// No URL formatter is used, so the data update URL is requested
        /// as it is. The request is repeated with the product of the
        /// loaded data file added to that URL.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(UrlsWithoutAFormatter))]
        public void CheckForUpdate_NoFormatter_SeveralProducts_RequestsProductOfLoadedFile(
            string url, string second)
        {
            DistributorWith("HashV41", "IPIV4Enterprise", "V4TAC");
            var engine = Engine("HashV41", "TAC");
            var configuration =
                engine.Object.GetDataFileMetaData().Configuration;
            configuration.UrlFormatter = null;
            configuration.DataUpdateUrl = url;

            var status = _dataUpdate.CheckForUpdate(engine.Object);

            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
            Assert.AreEqual(2, _requests.Count);
            Assert.AreEqual(url, _requests[0].AbsoluteUri);
            Assert.AreEqual(second, _requests[1].AbsoluteUri);
        }

        /// <summary>
        /// No URL formatter is used and no product name is known for the
        /// loaded data file. The update fails with the error that names
        /// the products offered, with no product needed.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_NoFormatter_ProductNotKnown_NamesProductsOffered()
        {
            DistributorWith("HashV41", "IPIV4Enterprise", "V4TAC");
            var engine = Engine("Other", "TAC");
            var configuration =
                engine.Object.GetDataFileMetaData().Configuration;
            configuration.UrlFormatter = null;
            configuration.DataUpdateUrl = "https://files.test/data";

            var ex = Assert.ThrowsExactly<DataUpdateException>(
                () => _dataUpdate.CheckForUpdate(engine.Object));

            Assert.AreEqual(
                "Product '' is not offered by data update service at " +
                "'https://files.test/data' for engine " +
                "'IFiftyOneAspectEngine'. Products offered: " +
                "'IPIV4Enterprise,V4TAC'",
                ex.Message);
            Assert.AreEqual(1, _requests.Count);
        }

        /// <summary>
        /// The data update URL of the caller names a product, with the
        /// name of the parameter in another case. It is requested as it
        /// is, and an answer that lists products is not acted on.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_UrlNamesProduct_AnswerListingProductsIsNotFollowed()
        {
            DistributorAnswering(
                () => SeveralProducts("HashV41", "IPIV4Enterprise", "V4TAC"));
            var engine = Engine("HashV41", "TAC");
            var configuration =
                engine.Object.GetDataFileMetaData().Configuration;
            configuration.UrlFormatter = null;
            configuration.DataUpdateUrl =
                "https://files.test/data?product=V4Enterprise";

            var ex = Assert.ThrowsExactly<DataUpdateException>(
                () => _dataUpdate.CheckForUpdate(engine.Object));

            Assert.AreEqual(
                StatusMessage("https://files.test/data?product=V4Enterprise"),
                ex.Message);
            Assert.AreEqual(1, _requests.Count);
            Assert.AreEqual(
                "https://files.test/data?product=V4Enterprise",
                _requests[0].AbsoluteUri);
        }

        /// <summary>
        /// A Product parameter with no value in the data update URL of
        /// the caller names no product, so the request is repeated with
        /// the product of the loaded data file.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_UrlHasEmptyProduct_RequestsProductOfLoadedFile()
        {
            _httpHandler.Setup(h => h.Send(It.IsAny<HttpRequestMessage>()))
                .Returns((HttpRequestMessage request) =>
                {
                    _requests.Add(request.RequestUri);
                    return _requests.Count == 1 ?
                        SeveralProducts("HashV41", "IPIV4Enterprise", "V4TAC") :
                        Data();
                });
            var engine = Engine("HashV41", "TAC");
            var configuration =
                engine.Object.GetDataFileMetaData().Configuration;
            configuration.UrlFormatter = null;
            configuration.DataUpdateUrl = "https://files.test/data?Product=";

            var status = _dataUpdate.CheckForUpdate(engine.Object);

            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
            Assert.AreEqual(2, _requests.Count);
            // The empty Product is replaced, so the URL names it once.
            Assert.AreEqual(
                "https://files.test/data?Product=V4TAC",
                _requests[1].AbsoluteUri);
        }

        /// <summary>
        /// The product given to the URL formatter is sent as it is given.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_ProductNamed_IsSentAsGiven()
        {
            DistributorAnswering(Data);
            var engine = Engine(
                "HashV41", "TAC", new FiftyOneUrlFormatter("v4Tac"));

            _dataUpdate.CheckForUpdate(engine.Object);

            Assert.AreEqual(1, _requests.Count);
            Assert.AreEqual(
                $"{Download}?LicenseKeys={Key}&Download=True" +
                "&Type=HashV41&Product=v4Tac",
                _requests[0].AbsoluteUri);
        }

        /// <summary>
        /// A URL formatter given an empty product name names no product,
        /// so the request is repeated with the product of the loaded data
        /// file.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_EmptyProductNamed_RequestsProductOfLoadedFile()
        {
            DistributorWith("HashV41", "IPIV4Enterprise", "V4TAC");
            var engine = Engine(
                "HashV41", "TAC", new FiftyOneUrlFormatter(""));

            var status = _dataUpdate.CheckForUpdate(engine.Object);

            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
            Assert.AreEqual(2, _requests.Count);
            Assert.AreEqual(
                $"{Download}?LicenseKeys={Key}&Download=True&Type=HashV41",
                _requests[0].AbsoluteUri);
            Assert.AreEqual("V4TAC", ProductOf(_requests[1]));
        }

        /// <summary>
        /// With a product given to the URL formatter, the product of the
        /// loaded data file is not looked up.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_ProductNamed_DoesNotLookUpProductOfLoadedFile()
        {
            DistributorAnswering(
                () => SeveralProducts("HashV41", "IPIV4Enterprise", "V4TAC"));
            var engine = Engine(
                "HashV41", "TAC", new FiftyOneUrlFormatter("V4TAC"),
                fileProduct: "TAC");

            Assert.ThrowsExactly<DataUpdateException>(
                () => _dataUpdate.CheckForUpdate(engine.Object));

            engine.Verify(e => e.DataSourceTier, Times.Never());
            engine.As<IDataFileProductEngine>().Verify(
                e => e.GetDataFileProduct(It.IsAny<string>()), Times.Never());
        }

        /// <summary>
        /// The HTTP client is set not to follow redirects, and the 300
        /// answer carries a Location header. The header is not followed,
        /// and the update downloads the product of the loaded data file
        /// in two requests.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_ClientFollowsNoRedirects_AnswerWithLocation_DownloadsProductOfLoadedFile()
        {
            var status = UpdateFromLocalServer(
                (address, requested) =>
                    SeveralProductsAnswer(address, requested, true),
                out var served,
                false);

            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
            CollectionAssert.AreEqual(NewData, _refreshedWith);
            Assert.AreEqual(2, served.Count);
            Assert.AreEqual(
                $"/api/v2/download?LicenseKeys={Key}&Download=True" +
                "&Type=HashV41&Product=V4TAC",
                served[1]);
        }

        /// <summary>
        /// The HTTP client is set not to follow redirects, and the update
        /// URL answers 302 to another address, which has the data. The
        /// update follows the redirect itself.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_ClientFollowsNoRedirects_Found_IsFollowed()
        {
            var status = UpdateFromLocalServer(
                (address, requested) =>
                    requested.AbsolutePath == "/api/v2/download" ?
                    new LocalAnswer()
                    {
                        Status = 302,
                        Location = "/mirror/file",
                    } :
                    new LocalAnswer() { Status = 200, Body = NewData },
                out var served,
                false);

            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
            CollectionAssert.AreEqual(NewData, _refreshedWith);
            Assert.AreEqual(2, served.Count);
            Assert.AreEqual("/mirror/file", served[1]);
        }

        /// <summary>
        /// The HTTP client is set not to follow redirects. The update URL
        /// answers 302 to another address, which answers 300 with the
        /// products on offer. The request is repeated to the address that
        /// answered 300, with the product of the loaded data file.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_ClientFollowsNoRedirects_FoundThenSeveralProducts_RepeatsAtAddressThatAnswered()
        {
            var status = UpdateFromLocalServer(
                (address, requested) =>
                {
                    if (requested.AbsolutePath == "/api/v2/download")
                    {
                        return new LocalAnswer()
                        {
                            Status = 302,
                            Location = address.Replace(
                                "/api/v2/download", "/mirror/download?Type=HashV41"),
                        };
                    }
                    return SeveralProductsAnswer(address, requested, true);
                },
                out var served,
                false);

            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
            CollectionAssert.AreEqual(NewData, _refreshedWith);
            Assert.AreEqual(3, served.Count);
            Assert.AreEqual(
                "/mirror/download?Type=HashV41&Product=V4TAC", served[2]);
        }


        /// <summary>
        /// The first request as the local server sees it.
        /// </summary>
        private const string LocalFirstRequest =
            "?LicenseKeys=" + Key + "&Download=True&Type=HashV41";

        /// <summary>
        /// The answers of a distributor for a key that holds
        /// IPIV4Enterprise and V4TAC, in that order. The first request gets
        /// 300 with the list and a Location header that names
        /// IPIV4Enterprise, as the distributor's does. The address in that
        /// header answers as <paramref name="followed"/> gives. A request
        /// for V4TAC gets its data.
        /// </summary>
        private static LocalAnswer LocationAnswer(
            string address, Uri requested, Func<LocalAnswer> followed)
        {
            var product = ProductOf(requested);
            var first = address + "?Type=HashV41&Product=IPIV4Enterprise";
            var second = address + "?Type=HashV41&Product=V4TAC";
            if (product == null)
            {
                return new LocalAnswer()
                {
                    Status = 300,
                    Location = first,
                    Body = Encoding.UTF8.GetBytes(
                        "{\"AvailableUris\":[\"" + first + "\",\"" +
                        second + "\"],\"Error\":\"There are multiple " +
                        "products associated with this license key. " +
                        "Please use the URI returned in the Location " +
                        "header.\"}"),
                };
            }
            if (product == "V4TAC" &&
                requested.Query.Contains("Download=True"))
            {
                return new LocalAnswer() { Status = 200, Body = NewData };
            }
            return followed();
        }

        /// <summary>
        /// Checks that the local server was sent the first request, then
        /// the address in the Location header, then the first request with
        /// the product of the loaded data file, and that the request for
        /// that product was logged naming the first request.
        /// </summary>
        private void AssertRequestedProductOfLoadedFile(
            List<string> served, string path)
        {
            Assert.AreEqual(3, served.Count, string.Join(", ", served));
            Assert.AreEqual(path + LocalFirstRequest, served[0]);
            Assert.AreEqual(
                path + "?Type=HashV41&Product=IPIV4Enterprise", served[1]);
            Assert.AreEqual(
                path + LocalFirstRequest + "&Product=V4TAC", served[2]);
            var messages = _logger.ExtendedEntries
                .Select(e => e.Message).ToList();
            Assert.AreEqual(
                1,
                messages.Count(m => m != null && m.StartsWith(
                    "Requesting product 'V4TAC' from 'http://localhost:",
                    StringComparison.Ordinal) && m.EndsWith(
                    path + "?LicenseKeys=****************7890" +
                    "&Download=True&Type=HashV41' for Engine " +
                    "'IFiftyOneAspectEngine'",
                    StringComparison.Ordinal)),
                string.Join(Environment.NewLine, messages));
            Assert.IsFalse(messages.Any(m => m != null && m.Contains(Key)));
        }

        /// <summary>
        /// The HTTP client follows the Location header of the 300 answer
        /// to the address of IPIV4Enterprise, which answers 404. The update
        /// does not report the 404. It asks for the product of the loaded
        /// data file, V4TAC, and installs it.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_RedirectingClient_LocationNotFound_InstallsProductOfLoadedFile()
        {
            string path = null;
            var status = UpdateFromLocalServer(
                (address, requested) =>
                {
                    path = new Uri(address).AbsolutePath;
                    return LocationAnswer(
                        address, requested,
                        () => new LocalAnswer() { Status = 404 });
                },
                out var served);

            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
            CollectionAssert.AreEqual(NewData, _refreshedWith);
            AssertRequestedProductOfLoadedFile(served, path);
        }

        /// <summary>
        /// The HTTP client follows the Location header of the 300 answer
        /// to the address of IPIV4Enterprise, which answers 200 with the
        /// data of that product. That data is not installed. The update
        /// asks for the product of the loaded data file, V4TAC, and
        /// installs it.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_RedirectingClient_LocationAnswersOtherProduct_InstallsProductOfLoadedFile()
        {
            string path = null;
            var status = UpdateFromLocalServer(
                (address, requested) =>
                {
                    path = new Uri(address).AbsolutePath;
                    return LocationAnswer(
                        address, requested,
                        () => new LocalAnswer()
                        {
                            Status = 200,
                            Body = OtherData,
                        });
                },
                out var served);

            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
            CollectionAssert.AreEqual(NewData, _refreshedWith);
            AssertRequestedProductOfLoadedFile(served, path);
        }

        /// <summary>
        /// The HTTP client follows the Location header of the 300 answer
        /// to the address of IPIV4Enterprise, which answers 404, and the
        /// request for V4TAC is refused. The update fails with the error
        /// for a refusal, naming the request for V4TAC.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_RedirectingClient_LocationNotFound_ProductRefused_ReportsForbidden()
        {
            string path = null;
            var ex = Assert.ThrowsExactly<DataUpdateException>(
                () => UpdateFromLocalServer(
                    (address, requested) =>
                    {
                        path = new Uri(address).AbsolutePath;
                        return ProductOf(requested) == "V4TAC" &&
                            requested.Query.Contains("Download=True") ?
                            new LocalAnswer() { Status = 403 } :
                            LocationAnswer(
                                address, requested,
                                () => new LocalAnswer() { Status = 404 });
                    },
                    out _));

            Assert.AreEqual(
                AutoUpdateStatus.AUTO_UPDATE_ERR_403_FORBIDDEN, ex.Status);
            Assert.IsTrue(
                ex.Message.StartsWith(
                    "Access denied to data update service at " +
                    "'http://localhost:", StringComparison.Ordinal),
                ex.Message);
            Assert.IsTrue(
                ex.Message.EndsWith(
                    path + "?LicenseKeys=****************7890" +
                    "&Download=True&Type=HashV41&Product=V4TAC' for " +
                    "engine 'IFiftyOneAspectEngine'",
                    StringComparison.Ordinal),
                ex.Message);
            Assert.IsNull(_refreshedWith);
        }

        /// <summary>
        /// The HTTP client follows the Location header of the 300 answer,
        /// and it names V4TAC, the product of the loaded data file. The
        /// address in the header is not the update URL, so its answer is
        /// not installed. The update asks for V4TAC at the update URL and
        /// installs that.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_RedirectingClient_LocationNamesProductOfLoadedFile_RequestsItFromUpdateUrl()
        {
            string path = null;
            var status = UpdateFromLocalServer(
                (address, requested) =>
                {
                    path = new Uri(address).AbsolutePath;
                    var product = ProductOf(requested);
                    if (product == null)
                    {
                        var offered = address + "?Type=HashV41&Product=V4TAC";
                        return new LocalAnswer()
                        {
                            Status = 300,
                            Location = offered,
                            Body = Encoding.UTF8.GetBytes(
                                "{\"AvailableUris\":[\"" + offered +
                                "\",\"" + address +
                                "?Type=HashV41&Product=V4Enterprise\"]}"),
                        };
                    }
                    return new LocalAnswer()
                    {
                        Status = 200,
                        Body = requested.Query.Contains("Download=True") ?
                            NewData : OtherData,
                    };
                },
                out var served);

            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
            CollectionAssert.AreEqual(NewData, _refreshedWith);
            Assert.AreEqual(3, served.Count, string.Join(", ", served));
            Assert.AreEqual(
                path + "?Type=HashV41&Product=V4TAC", served[1]);
            Assert.AreEqual(
                path + LocalFirstRequest + "&Product=V4TAC", served[2]);
        }

        /// <summary>
        /// The HTTP client follows a 302 from the update URL to another
        /// address, which answers 300 with the products on offer and no
        /// Location header. The request is repeated to the address that
        /// answered 300, with the product of the loaded data file, and the
        /// message names that address.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_RedirectingClient_FoundThenSeveralProducts_RepeatsAtAddressThatAnswered()
        {
            string path = null;
            var status = UpdateFromLocalServer(
                (address, requested) =>
                {
                    path = new Uri(address).AbsolutePath;
                    if (requested.AbsolutePath == path)
                    {
                        return new LocalAnswer()
                        {
                            Status = 302,
                            Location = address.Replace(
                                path, "/mirror/download?Type=HashV41"),
                        };
                    }
                    return SeveralProductsAnswer(address, requested, false);
                },
                out var served);

            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
            CollectionAssert.AreEqual(NewData, _refreshedWith);
            Assert.AreEqual(3, served.Count, string.Join(", ", served));
            Assert.AreEqual(
                "/mirror/download?Type=HashV41&Product=V4TAC", served[2]);
            var messages = _logger.ExtendedEntries
                .Select(e => e.Message).ToList();
            Assert.AreEqual(
                1,
                messages.Count(m => m != null && m.StartsWith(
                    "Requesting product 'V4TAC' from 'http://localhost:",
                    StringComparison.Ordinal) && m.EndsWith(
                    "/mirror/download?Type=HashV41' for Engine " +
                    "'IFiftyOneAspectEngine'", StringComparison.Ordinal)),
                string.Join(Environment.NewLine, messages));
        }

        /// <summary>
        /// A stand-in for an update URL that redirects the number of times
        /// given, each time to the next address on the same host, and then
        /// answers with the data.
        /// </summary>
        private void RedirectsThenData(int redirects)
        {
            _httpHandler.Setup(h => h.Send(It.IsAny<HttpRequestMessage>()))
                .Returns((HttpRequestMessage request) =>
                {
                    _requests.Add(request.RequestUri);
                    if (_requests.Count <= redirects)
                    {
                        var response = new HttpResponseMessage(
                            HttpStatusCode.Redirect);
                        response.Headers.Location = new Uri(
                            "https://mirror.test/file" + _requests.Count);
                        return response;
                    }
                    return Data();
                });
        }

        /// <summary>
        /// The update URL redirects 20 times before the data. The update
        /// follows each redirect and installs the data.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_TwentyRedirects_AreFollowed()
        {
            RedirectsThenData(20);
            var engine = Engine("HashV41", "TAC");

            var status = _dataUpdate.CheckForUpdate(engine.Object);

            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
            Assert.AreEqual(21, _requests.Count);
            Assert.AreEqual("https://mirror.test/file20", _requests[20].AbsoluteUri);
            CollectionAssert.AreEqual(NewData, _refreshedWith);
        }

        /// <summary>
        /// The update URL redirects 21 times. The 21st redirect is not
        /// followed, and the update fails with the error for its status.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_TwentyOneRedirects_ReportsStatus()
        {
            RedirectsThenData(21);
            var engine = Engine("HashV41", "TAC");

            var ex = Assert.ThrowsExactly<DataUpdateException>(
                () => _dataUpdate.CheckForUpdate(engine.Object));

            Assert.AreEqual(
                $"HTTP status code '{HttpStatusCode.Redirect}' from data " +
                "update service at 'https://mirror.test/file20' for engine " +
                "'IFiftyOneAspectEngine'",
                ex.Message);
            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_HTTPS_ERR, ex.Status);
            Assert.AreEqual(21, _requests.Count);
            Assert.IsNull(_refreshedWith);
        }

        /// <summary>
        /// The https update URL redirects to an http address. The redirect
        /// is not followed, and the update fails with the error for its
        /// status, naming the update URL.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_RedirectFromHttpsToHttp_IsNotFollowed()
        {
            _httpHandler.Setup(h => h.Send(It.IsAny<HttpRequestMessage>()))
                .Returns((HttpRequestMessage request) =>
                {
                    _requests.Add(request.RequestUri);
                    var response = new HttpResponseMessage(
                        HttpStatusCode.Redirect);
                    response.Headers.Location =
                        new Uri("http://mirror.test/file");
                    return response;
                });
            var engine = Engine("HashV41", "TAC");

            var ex = Assert.ThrowsExactly<DataUpdateException>(
                () => _dataUpdate.CheckForUpdate(engine.Object));

            Assert.AreEqual(
                $"HTTP status code '{HttpStatusCode.Redirect}' from data " +
                $"update service at '{ShownFirstRequest}' for engine " +
                "'IFiftyOneAspectEngine'",
                ex.Message);
            Assert.AreEqual(1, _requests.Count);
            Assert.IsNull(_refreshedWith);
        }

        /// <summary>
        /// The product chosen has characters that a query must encode. The
        /// second request is the first with Product added as its last
        /// parameter, percent-encoded.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_SeveralProducts_ProductIsLastAndEncoded()
        {
            _httpHandler.Setup(h => h.Send(It.IsAny<HttpRequestMessage>()))
                .Returns((HttpRequestMessage request) =>
                {
                    _requests.Add(request.RequestUri);
                    return _requests.Count == 1 ?
                        new HttpResponseMessage((HttpStatusCode)300)
                        {
                            Content = new StringContent(
                                "{\"AvailableUris\":[\"" + Download +
                                "?Type=HashV41&Product=V4A%26B%20C\"]}",
                                Encoding.UTF8, "application/json"),
                        } :
                        Data();
                });
            var engine = Engine("HashV41", "A&B C");

            var status = _dataUpdate.CheckForUpdate(engine.Object);

            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
            Assert.AreEqual(2, _requests.Count);
            Assert.AreEqual(
                _requests[0].AbsoluteUri + "&Product=V4A%26B%20C",
                _requests[1].AbsoluteUri);
        }

        /// <summary>
        /// After the answer that lists the products, the request for the
        /// product is logged, naming the first request, and then the
        /// second request is logged as a request is, naming its own URL.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_SeveralProducts_LogsProductThenSecondRequest()
        {
            DistributorWith("HashV41", "IPIV4Enterprise", "V4TAC");
            var engine = Engine("HashV41", "TAC");

            _dataUpdate.CheckForUpdate(engine.Object);

            var messages = _logger.ExtendedEntries
                .Select(e => e.Message).ToList();
            var requested = messages.IndexOf(
                "Requesting product 'V4TAC' from '" + ShownFirstRequest +
                "' for Engine 'IFiftyOneAspectEngine'");
            var second = messages.FindIndex(m => m != null && m.Contains(
                "Checking for update from " + ShownFirstRequest +
                "&Product=V4TAC"));
            Assert.IsTrue(requested >= 0, string.Join(Environment.NewLine, messages));
            Assert.IsTrue(second > requested, string.Join(Environment.NewLine, messages));
        }

        /// <summary>
        /// The list holds entries that are not strings. They are passed
        /// over, and the product is chosen from the others.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_SeveralProducts_EntryNotAStringIsPassedOver()
        {
            _httpHandler.Setup(h => h.Send(It.IsAny<HttpRequestMessage>()))
                .Returns((HttpRequestMessage request) =>
                {
                    _requests.Add(request.RequestUri);
                    return _requests.Count == 1 ?
                        new HttpResponseMessage((HttpStatusCode)300)
                        {
                            Content = new StringContent(
                                "{\"AvailableUris\":[1,{\"Product\":\"V4TAC\"}," +
                                "\"" + Offered("HashV41", "V4TAC") + "\"]}",
                                Encoding.UTF8, "application/json"),
                        } :
                        Data();
                });
            var engine = Engine("HashV41", "TAC");

            var status = _dataUpdate.CheckForUpdate(engine.Object);

            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
            Assert.AreEqual(2, _requests.Count);
            Assert.AreEqual("V4TAC", ProductOf(_requests[1]));
            CollectionAssert.AreEqual(NewData, _refreshedWith);
        }

        /// <summary>
        /// The list is named in another case than AvailableUris. It is not
        /// the list, so the update fails with the error for the status.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_ListNamedInAnotherCase_ReportsStatus()
        {
            _httpHandler.Setup(h => h.Send(It.IsAny<HttpRequestMessage>()))
                .Returns((HttpRequestMessage request) =>
                {
                    _requests.Add(request.RequestUri);
                    return new HttpResponseMessage((HttpStatusCode)300)
                    {
                        Content = new StringContent(
                            "{\"availableUris\":[\"" +
                            Offered("HashV41", "V4TAC") + "\"]}",
                            Encoding.UTF8, "application/json"),
                    };
                });
            var engine = Engine("HashV41", "TAC");

            var ex = Assert.ThrowsExactly<DataUpdateException>(
                () => _dataUpdate.CheckForUpdate(engine.Object));

            Assert.AreEqual(StatusMessage(ShownFirstRequest), ex.Message);
            Assert.AreEqual(1, _requests.Count);
        }

        /// <summary>
        /// The HTTP client follows a 302 from the update URL to another
        /// address, which answers 300 with a body that is not a product
        /// list. The error for the status names the address that answered
        /// 300.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_RedirectingClient_FoundThenNoList_ErrorNamesAddressThatAnswered()
        {
            string mirror = null;
            var ex = Assert.ThrowsExactly<DataUpdateException>(
                () => UpdateFromLocalServer(
                    (address, requested) =>
                    {
                        var path = new Uri(address).AbsolutePath;
                        mirror = address.Replace(
                            path, "/mirror/download?Type=HashV41");
                        return requested.AbsolutePath == path ?
                            new LocalAnswer()
                            {
                                Status = 302,
                                Location = mirror,
                            } :
                            new LocalAnswer()
                            {
                                Status = 300,
                                Body = Encoding.UTF8.GetBytes(
                                    "several products"),
                            };
                    },
                    out _));

            Assert.AreEqual(StatusMessage(mirror), ex.Message);
            Assert.IsNull(_refreshedWith);
        }

        /// <summary>
        /// An update on start-up with a key that holds several products
        /// and no product named. No data file is loaded, so no product
        /// name is known, and the update fails with the error that names
        /// the products offered.
        /// </summary>
        [TestMethod]
        public void UpdateOnStartup_SeveralProducts_NoProductNamed_NamesProductsOffered()
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
                    UrlFormatter = new FiftyOneUrlFormatter(),
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
                var ex = Assert.ThrowsExactly<DataUpdateException>(
                    () => _dataUpdate.RegisterDataFile(file));

                Assert.AreEqual(
                    "Product '' is not offered by data update service at '" +
                    ShownFirstRequest + "' for engine " +
                    "'IFiftyOneAspectEngine'. Products offered: " +
                    "'IPIV4Enterprise,V4TAC'",
                    ex.Message);
                Assert.AreEqual(1, _requests.Count);
            }
        }

        /// <summary>
        /// A data file for an update on start-up with no product named,
        /// whose engine has loaded nothing. The file at the configured
        /// location has the product name given in its header, which the
        /// reader returns for that path only, as the native engines'
        /// getProductFromFile does.
        /// </summary>
        private static FiftyOneDataFile StartupFile(
            string type, string path, string product)
        {
            return new FiftyOneDataFile(typeof(IFiftyOneAspectEngine))
            {
                Identifier = Identifier,
                DataDownloadType = type,
                ProductFromFile = p => p == path ? product : null,
                Configuration = new DataFileConfiguration()
                {
                    Identifier = Identifier,
                    DataFilePath = path,
                    DataUpdateUrl = Download,
                    DataUpdateLicenseKeys = new[] { Key },
                    UrlFormatter = new FiftyOneUrlFormatter(),
                    MemoryOnly = true,
                    DecompressContent = false,
                    VerifyMd5 = false,
                    VerifyModifiedSince = false,
                    FileSystemWatcherEnabled = false,
                    AutomaticUpdatesEnabled = false,
                    UpdateOnStartup = true,
                },
            };
        }

        /// <summary>
        /// An update on start-up with a key that holds several products and
        /// no product named, before any engine has loaded the data file. The
        /// product is read from the header of the file at the configured
        /// location, so the second request asks for it and the update
        /// succeeds. The native engines give the name with its prefix,
        /// 'V4TAC' or 'IPIV4Enterprise', and a tier alone, as an older
        /// reader might give, is given the prefix.
        /// </summary>
        [TestMethod]
        [DataRow("HashV41", "V4TAC", "V4TAC")]
        [DataRow("HashV41", "TAC", "V4TAC")]
        [DataRow("IPIV41", "IPIV4Enterprise", "IPIV4Enterprise")]
        public void UpdateOnStartup_SeveralProducts_FileNotLoaded_AsksForProductOfFile(
            string type, string value, string product)
        {
            DistributorWith(type, "IPIV4Enterprise", "V4TAC");
            var path = Path.GetTempFileName();
            try
            {
                using (var file = StartupFile(type, path, value))
                {
                    Assert.AreEqual(product, file.DataUpdateProduct);

                    _dataUpdate.RegisterDataFile(file);

                    Assert.AreEqual(2, _requests.Count);
                    Assert.IsNull(ProductOf(_requests[0]));
                    Assert.AreEqual(product, ProductOf(_requests[1]));
                    Assert.IsNotNull(file.Configuration.DataStream,
                        "The update did not complete with the data served.");
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Before an engine has loaded a data file, no product is known when
        /// the configured location holds no file, when the header cannot be
        /// read, or when there is no reader, so an update on start-up names
        /// none.
        /// </summary>
        [TestMethod]
        public void DataUpdateProduct_FileNotLoaded_NoProductWithoutAReadableFile()
        {
            var missing = Path.Combine(
                Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".hash");
            using (var file = StartupFile("HashV41", missing, "V4TAC"))
            {
                Assert.IsNull(file.DataUpdateProduct,
                    "A product was named for a file that is not there.");
            }

            var path = Path.GetTempFileName();
            try
            {
                using (var file = StartupFile("HashV41", path, "V4TAC"))
                {
                    file.ProductFromFile = p =>
                        throw new InvalidDataException("unreadable header");
                    Assert.IsNull(file.DataUpdateProduct,
                        "A product was named for a header that cannot be read.");

                    file.ProductFromFile = null;
                    Assert.IsNull(file.DataUpdateProduct,
                        "A product was named with no way to read one.");
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Once an engine has loaded a data file, the product comes from the
        /// engine, not from the file at the configured location.
        /// </summary>
        [TestMethod]
        public void DataUpdateProduct_FileLoaded_ProductOfEngine()
        {
            var path = Path.GetTempFileName();
            try
            {
                var engine = new Mock<IFiftyOneAspectEngine>();
                engine.Setup(e => e.DataSourceTier).Returns("TAC");
                using (var file = StartupFile("HashV41", path, "V4Lite"))
                {
                    file.Engine = engine.Object;
                    Assert.AreEqual("V4TAC", file.DataUpdateProduct);
                }
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
