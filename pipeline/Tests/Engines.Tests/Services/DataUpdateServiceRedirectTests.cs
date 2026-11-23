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
using FiftyOne.Pipeline.Engines.TestHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Engines.Tests.Services
{
    /// <summary>
    /// The answer that lists the products the license keys hold carries a
    /// Location header that names the first of them. The HTTP client the
    /// data update service makes for itself does not follow it, so the
    /// service reads the list and asks for the product of the data file.
    /// A local server answers here, over a real connection.
    /// </summary>
    [TestClass]
    public class DataUpdateServiceRedirectTests
    {
        private const string Identifier = "data";

        /// <summary>
        /// A URL formatter that names the product 'V4TAC'.
        /// </summary>
        private class ProductFormatter : IDataUpdateProductFormatter
        {
            public string GetDataUpdateProduct(IAspectEngineDataFile dataFile)
            {
                return "V4TAC";
            }

            public Uri GetFormattedDataUpdateUri(IAspectEngineDataFile dataFile)
            {
                return GetFormattedDataUpdateUri(dataFile, null);
            }

            public Uri GetFormattedDataUpdateUri(
                IAspectEngineDataFile dataFile, string product)
            {
                return new Uri(
                    dataFile.Configuration.DataUpdateUrl + "?Download=True" +
                    (product == null ? "" : "&Product=" + product));
            }

            [Obsolete("Use the GetFormattedDataUpdateUri method instead.")]
            public string GetFormattedDataUpdateUrl(
                IAspectEngineDataFile dataFile)
            {
                return GetFormattedDataUpdateUri(dataFile, null).AbsoluteUri;
            }
        }

        /// <summary>
        /// Answers each request until the listener is stopped. A request
        /// that names no product gets the list, with a Location header
        /// that names the first address in it. A request that names a
        /// product gets data.
        /// </summary>
        private static async Task Serve(
            HttpListener listener, string download, List<string> requests)
        {
            while (true)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync();
                }
                catch (Exception)
                {
                    // The listener was stopped.
                    return;
                }
                var query = context.Request.Url.Query;
                lock (requests)
                {
                    requests.Add(query);
                }
                byte[] body;
                if (query.Contains("Product="))
                {
                    context.Response.StatusCode = 200;
                    body = Encoding.ASCII.GetBytes("new data file");
                }
                else
                {
                    var first = download + "?Type=HashV41&Product=IPIV4Enterprise";
                    var second = download + "?Type=HashV41&Product=V4TAC";
                    context.Response.StatusCode = 300;
                    context.Response.AddHeader("Location", first);
                    context.Response.ContentType = "application/json";
                    body = Encoding.UTF8.GetBytes(
                        "{\"AvailableUris\":[\"" + first + "\",\"" + second +
                        "\"],\"Error\":\"There are multiple products " +
                        "associated with this license key. Please use the " +
                        "URI returned in the Location header.\"}");
                }
                context.Response.ContentLength64 = body.Length;
                await context.Response.OutputStream.WriteAsync(
                    body, 0, body.Length);
                context.Response.Close();
            }
        }

        /// <summary>
        /// The Location header of the answer is not followed. The second
        /// request is the first with the product of the data file added.
        /// </summary>
        [TestMethod]
        public void CheckForUpdate_SeveralProducts_LocationIsNotFollowed()
        {
            var root =
                $"http://localhost:{TestHttpListener.GetRandomUnusedPort()}/";
            var download = root + "download";
            var requests = new List<string>();
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(f => f.File).Returns(new Mock<IFileWrapper>().Object);
            fileSystem.Setup(f => f.Directory).Returns(
                new Mock<IDirectoryWrapper>().Object);
            var engine = new Mock<IOnPremiseAspectEngine>();
            var file = new AspectEngineDataFile()
            {
                Identifier = Identifier,
                Engine = engine.Object,
                Configuration = new DataFileConfiguration()
                {
                    Identifier = Identifier,
                    DataUpdateUrl = download,
                    UrlFormatter = new ProductFormatter(),
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

            var listener = new HttpListener();
            listener.Prefixes.Add(root);
            listener.Start();
            try
            {
                var serving = Serve(listener, download, requests);
                using (var client = DataUpdateService.CreateHttpClient())
                {
                    var dataUpdate = new DataUpdateService(
                        new TestLogger<DataUpdateService>(),
                        client,
                        fileSystem.Object,
                        new Mock<Func<TimerCallback, object, TimeSpan, Timer>>()
                            .Object);

                    var status = dataUpdate.CheckForUpdate(engine.Object);

                    Assert.AreEqual(
                        AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status);
                }
            }
            finally
            {
                listener.Stop();
                listener.Close();
            }

            lock (requests)
            {
                Assert.AreEqual(2, requests.Count);
                Assert.AreEqual("?Download=True", requests[0]);
                Assert.AreEqual("?Download=True&Product=V4TAC", requests[1]);
                Assert.IsFalse(
                    requests.Any(r => r.Contains("IPIV4Enterprise")),
                    "The address in the Location header was requested.");
            }
        }
    }
}
