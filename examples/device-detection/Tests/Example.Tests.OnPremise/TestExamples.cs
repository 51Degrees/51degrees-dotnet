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

// Ignore Spelling: Offline Yaml Metadata

using FiftyOne.DeviceDetection.Examples;
using FiftyOne.DeviceDetection.Hash.Engine.OnPremise.FlowElements;
using FiftyOne.Example.Tests.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FiftyOne.DeviceDetection.Example.Tests.OnPremise
{
    /// <summary>
    /// This test class ensures that the hash examples execute successfully.
    /// </summary>
    /// <remarks>
    /// Note that these test do not generally ensure the correctness 
    /// of the example, only that the example will run without 
    /// crashing or throwing any unhandled exceptions.
    /// </remarks>
    [TestClass]
    public class TestExamples
    {
        private string DataFile;

        private string EvidenceFile;

        /// <summary>
        /// Init method - set the data file for hash examples and additionally
        /// a User-Agents file for the performance example.
        /// </summary>
        [TestInitialize]
        public void Init()
        {
            // Set Device Data file
            DataFile = ExampleUtils.FindDataFile(
                Constants.LITE_HASH_DATA_FILE_NAME);

            // Set evidence file for offline processing example.
            EvidenceFile = Environment.GetEnvironmentVariable(
                Constants.EVIDENCE_FILE_ENV_VAR);
            if (string.IsNullOrWhiteSpace(EvidenceFile))
            {
                EvidenceFile = ExampleUtils.FindFile(
                    Constants.YAML_EVIDENCE_FILE_NAME);
            }
        }

        /// <summary>
        /// Test the GettingStarted Example
        /// </summary>
        [TestMethod]
        public void Example_OnPremise_GettingStarted()
        {
            var example = new Examples.OnPremise.GettingStartedConsole.Program.Example();
            example.Run(DataFile, new LoggerFactory(), TextWriter.Null);
        }

        /// <summary>
        /// Test the Offline Processing Example
        /// </summary>
        [TestMethod]
        public void Example_OnPremise_OfflineProcessing_Yaml()
        {
            Example_OnPremise_OfflineProcessing(
                new ExampleWriterYaml(TextWriter.Null));
        }

        /// <summary>
        /// Test the Offline Processing Example
        /// </summary>
        [TestMethod]
        public void Example_OnPremise_OfflineProcessing_Csv()
        {
            Example_OnPremise_OfflineProcessing(
                new ExampleWriterCsv(TextWriter.Null));
        }

        /// <summary>
        /// Test the Metadata Example
        /// </summary>
        [TestMethod]
        public void Example_OnPremise_Metadata()
        {
            var example = new Examples.OnPremise.Metadata.Program.Example();
            example.Run(DataFile, new LoggerFactory(), TextWriter.Null, 
                Pipeline.Engines.PerformanceProfiles.MaxPerformance);
        }

        /// <summary>
        /// Test the UpdateDataFile Example against a stand-in for the
        /// Distributor, with no product named. The stand-in lists two
        /// products, as the Distributor does for a license key that holds
        /// several, and the example asks for the product of the data file it
        /// starts from.
        /// </summary>
        [TestMethod]
        public void Example_OnPremise_UpdateDataFile()
        {
            var product = DataFileProduct();
            var requests = RunUpdateExample(product, null);
            Assert.IsTrue(requests.Count >= 2,
                "The example should have asked the stand-in twice.");
            Assert.AreEqual(new DataUpdateRequest(null, 300), requests[0],
                "The first request should name no product, and get the " +
                "list of products.");
            Assert.AreEqual(new DataUpdateRequest(product, 200), requests[1],
                "The second request should ask for the product of the data " +
                "file, and get the newer data file.");
        }

        /// <summary>
        /// Test the UpdateDataFile Example against a stand-in for the
        /// Distributor, with the product named as an argument. The first
        /// request asks for that product.
        /// </summary>
        [TestMethod]
        public void Example_OnPremise_UpdateDataFile_ProductArgument()
        {
            var product = DataFileProduct();
            var requests = RunUpdateExample(product, product);
            Assert.IsTrue(requests.Count >= 1,
                "The example should have asked the stand-in.");
            Assert.AreEqual(new DataUpdateRequest(product, 200), requests[0],
                "The first request should ask for the product given, and " +
                "get the newer data file.");
            Assert.IsFalse(requests.Any(r => r.Product == null),
                "No request should leave out the product given.");
        }

        /// <summary>
        /// Test the match metrics Example
        /// </summary>
        [TestMethod]
        public void Example_OnPremise_MatchMetrics()
        {
            Examples.OnPremise.MatchMetrics.Program.Initialize(
                DataFile, TextWriter.Null);
        }

        /// <summary>
        /// Test the offline processing example with 100 records to avoid delays.
        /// </summary>
        /// <param name="writer"></param>
        public void Example_OnPremise_OfflineProcessing(IExampleWriter writer)
        {
            var example = new Examples.OnPremise.OfflineProcessing.Program.Example();
            using (var reader = new StreamReader(File.OpenRead(EvidenceFile)))
            {
                example.Run(DataFile, reader, new LoggerFactory(), writer, 
                    Pipeline.Engines.PerformanceProfiles.MaxPerformance);
            }
        }

        /// <summary>
        /// The license key the update example sends the stand-in, which
        /// serves the data file whatever key it is sent.
        /// </summary>
        private const string StandInLicenseKey = "STANDINLICENSEKEY";

        /// <summary>
        /// A product the stand-in lists beside the data file's own, as the
        /// Distributor does for a license key that holds a Device Detection
        /// and an IP Intelligence product.
        /// </summary>
        private const string OtherProduct = "IPIV4Enterprise";

        /// <summary>
        /// The product of the data file, named by the library's own rule
        /// from an engine that has loaded the file, such as 'V4TAC' for
        /// 'TAC-HashV41.hash'. The update example refuses the free Lite
        /// data file, so the test needs a file the Distributor serves, which
        /// CI fetches and names in '_51DEGREES_DD_PATH'.
        /// </summary>
        private string DataFileProduct()
        {
            using (var loggerFactory = new LoggerFactory())
            using (var engine = new DeviceDetectionHashEngineBuilder(
                    loggerFactory)
                .SetPerformanceProfile(
                    Pipeline.Engines.PerformanceProfiles.LowMemory)
                .SetAutoUpdate(false)
                .SetDataFileSystemWatcher(false)
                .SetDataUpdateOnStartup(false)
                .Build(DataFile, false))
            {
                Assert.AreNotEqual("Lite", engine.DataSourceTier,
                    $"The update example needs a data file other than the " +
                    $"free Lite file, such as 'TAC-HashV41.hash', given in " +
                    $"'{Constants.DEVICE_DETECTION_DATA_FILE_ENV_VAR}'.");
                var dataFile = (Pipeline.Engines.Data.AspectEngineDataFile)
                    engine.DataFiles[0];
                var product = dataFile.DataUpdateProduct;
                Assert.IsNotNull(product,
                    $"The data file '{DataFile}' has no product name.");
                return product;
            }
        }

        /// <summary>
        /// Runs the UpdateDataFile example on a copy of the data file, with
        /// its update URL pointed at a stand-in for the Distributor. The
        /// stand-in serves another copy of the data file with its published
        /// date moved on to tomorrow, so the copy is newer than anything the
        /// example starts from. The update has completed when the example's
        /// data file holds that date.
        /// </summary>
        /// <param name="product">
        /// The product of the data file, which the stand-in lists.
        /// </param>
        /// <param name="productArgument">
        /// The product to give the example, or null for none.
        /// </param>
        /// <returns>
        /// The requests the stand-in received.
        /// </returns>
        private IReadOnlyList<DataUpdateRequest> RunUpdateExample(
            string product, string productArgument)
        {
            var directory = Directory.CreateTempSubdirectory(
                "51Degrees-UpdateDataFile-");
            try
            {
                var dataFile = Path.Combine(
                    directory.FullName, Path.GetFileName(DataFile));
                File.Copy(DataFile, dataFile);
                var served = Path.Combine(
                    directory.FullName, "served" + Path.GetExtension(DataFile));
                File.Copy(DataFile, served);
                var newer = DateTime.UtcNow.Date.AddDays(1);
                DataUpdateStandIn.WritePublished(served, newer);

                using (var standIn = new DataUpdateStandIn(
                    new[] { OtherProduct, product }, served))
                {
                    Examples.OnPremise.UpdateDataFile.Program.Initialize(
                        dataFile, StandInLicenseKey, false, productArgument,
                        standIn.Url);
                    Assert.AreEqual(
                        newer,
                        DataUpdateStandIn.ReadPublished(dataFile),
                        "The example's data file should hold the newer data " +
                        "the stand-in served.");
                    return standIn.Requests;
                }
            }
            finally
            {
                try
                {
                    directory.Delete(true);
                }
                catch (Exception ex) when (
                    ex is IOException || ex is UnauthorizedAccessException)
                {
                    // A file the engine has not yet released is left for
                    // the system to clear from the temporary folder.
                }
            }
        }
    }
}
