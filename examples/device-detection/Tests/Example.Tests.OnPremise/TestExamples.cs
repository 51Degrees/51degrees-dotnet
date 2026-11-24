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
using FiftyOne.DeviceDetection.Examples.OnPremise.UpdateDataFile;
using FiftyOne.DeviceDetection.Hash.Engine.OnPremise.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

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
        private string LicenseKey;

        private string DataFile;

        private string EvidenceFile;

        /// <summary>
        /// Init method - specify License Key to run examples here or 
        /// set a License Key in an environment variable called 'ResourceKey'.
        /// Set data file for hash examples and additionally a User-Agents file
        /// for the performance example.
        /// </summary>
        [TestInitialize]
        public void Init()
        {
            // Set license key for autoupdate examples, read as the data
            // file update example reads it.
            var licenseKey = Examples.OnPremise.UpdateDataFile.Program
                .LicenseKeyFromEnvironment();
            LicenseKey = string.IsNullOrWhiteSpace(licenseKey) == false ?
                licenseKey: "!!YOUR_LICENSE_KEY!!";

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
        /// Test the UpdateDataFile Example. It asks for the product in
        /// '_51DEGREES_DD_PRODUCT' when that is set, and otherwise names no
        /// product. A license key that holds several products needs the
        /// product named, as the update on start-up runs before a data file
        /// is loaded.
        /// </summary>
        [TestMethod]
        public void Example_OnPremise_UpdateDataFile()
        {
            VerifyLicenseKeyAvailable();
            var product = Examples.OnPremise.UpdateDataFile.Program
                .ProductFromEnvironment();
            AutoUpdateStatus status;
            try
            {
                status = Examples.OnPremise.UpdateDataFile.Program.Initialize(
                    DataFile, LicenseKey, false, product);
            }
            catch (Exception ex)
            {
                throw new AssertFailedException(
                    UpdateFailed(product, ex), ex);
            }
            if (status == AutoUpdateStatus.AUTO_UPDATE_ERR_429_TOO_MANY_ATTEMPTS)
            {
                Assert.Inconclusive("The data update service answered 429, " +
                    "too many attempts, so the example stopped before the " +
                    "engine was refreshed.");
            }
            Assert.AreEqual(AutoUpdateStatus.AUTO_UPDATE_SUCCESS, status,
                "The engine should be refreshed from the data file the " +
                "update on start-up gave.");
        }

        /// <summary>
        /// When the data update service answers 429, too many attempts, the
        /// UpdateDataFile example stops and returns that status, so a caller
        /// can tell that no update happened.
        /// </summary>
        [TestMethod]
        public void Example_OnPremise_UpdateDataFile_TooManyAttempts()
        {
            var folder = Path.Combine(Path.GetTempPath(),
                $"51Degrees-UpdateDataFile-{Guid.NewGuid():N}");
            Directory.CreateDirectory(folder);
            try
            {
                using (var loggerFactory = new LoggerFactory())
                using (var httpClient = new HttpClient(
                    new StatusHandler((HttpStatusCode)429)))
                {
                    var service = new DataUpdateService(
                        loggerFactory.CreateLogger<DataUpdateService>(),
                        httpClient);
                    var example = new Program.Example(
                        new DeviceDetectionPipelineBuilder(
                            loggerFactory, httpClient, service),
                        new DeviceDetectionHashEngineBuilder(
                            loggerFactory, service),
                        new CompletionListener(service),
                        loggerFactory.CreateLogger<Program.Example>());

                    var status = example.Run(
                        Path.Combine(folder, "Enterprise-HashV41.hash"),
                        "XXXX", false, "V4TAC");

                    Assert.AreEqual(
                        AutoUpdateStatus.AUTO_UPDATE_ERR_429_TOO_MANY_ATTEMPTS,
                        status);
                }
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <summary>
        /// Answers every request with the same status code and no content.
        /// </summary>
        private class StatusHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _status;

            public StatusHandler(HttpStatusCode status)
            {
                _status = status;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(_status)
                {
                    RequestMessage = request,
                });
            }
        }

        /// <summary>
        /// A failed update names the product it asked for and the setting
        /// that product came from.
        /// </summary>
        [TestMethod]
        public void Example_OnPremise_UpdateDataFile_FailureNamesTheProduct()
        {
            var failure = new InvalidOperationException("Update failed.");
            var named = UpdateFailed("V4TAC", failure);
            StringAssert.Contains(named, "'V4TAC'");
            StringAssert.Contains(named, Constants.DATA_UPDATE_PRODUCT_ENV_VAR);
            StringAssert.Contains(named, "Update failed.");

            var unnamed = UpdateFailed(null, failure);
            StringAssert.Contains(unnamed, "no product");
            StringAssert.Contains(unnamed,
                $"'{Constants.DATA_UPDATE_PRODUCT_ENV_VAR}' is not set");
            StringAssert.Contains(unnamed, "Update failed.");
        }

        /// <summary>
        /// Without a license key the update test is inconclusive, except in
        /// GitHub Actions, where the key is always given, so it fails.
        /// </summary>
        [TestMethod]
        public void Example_OnPremise_UpdateDataFile_NoKey()
        {
            Assert.ThrowsException<AssertInconclusiveException>(
                () => VerifyLicenseKeyAvailable("!!YOUR_LICENSE_KEY!!", false));
            var failed = Assert.ThrowsException<AssertFailedException>(
                () => VerifyLicenseKeyAvailable("!!YOUR_LICENSE_KEY!!", true));
            StringAssert.Contains(failed.Message,
                Constants.DATA_UPDATE_LICENSE_KEY_ENV_VAR);
            Assert.ThrowsException<AssertFailedException>(
                () => VerifyLicenseKeyAvailable(null, true));
            VerifyLicenseKeyAvailable("XXXX", true);
        }

        /// <summary>
        /// The update URL the UpdateDataFile example sets for a product
        /// names the license key, the type and the product.
        /// </summary>
        [TestMethod]
        public void Example_OnPremise_UpdateDataFile_UrlNamesProduct()
        {
            Assert.AreEqual(
                "https://distributor.51degrees.com/api/v2/download" +
                "?LicenseKeys=XXXX&Download=True&Type=HashV41&Product=V4TAC",
                Examples.OnPremise.UpdateDataFile.Program.DataUpdateUrl(
                    "XXXX", "V4TAC"));
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

        private void VerifyLicenseKeyAvailable()
        {
            VerifyLicenseKeyAvailable(LicenseKey,
                Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true");
        }

        /// <summary>
        /// Stops the test when there is no license key. In GitHub Actions
        /// the key is always given, so a missing key fails the test there,
        /// and anywhere else the test is inconclusive.
        /// </summary>
        private static void VerifyLicenseKeyAvailable(
            string licenseKey, bool inGitHubActions)
        {
            if (string.IsNullOrWhiteSpace(licenseKey) == true ||
                licenseKey.StartsWith("!!") == true)
            {
                var message = "This test requires a 51Degrees license key. This can be " +
                    "specified in the TestHashExamples.Init method or by setting an Environment " +
                    $"variable called '{Constants.DATA_UPDATE_LICENSE_KEY_ENV_VAR}'";
                if (inGitHubActions)
                {
                    Assert.Fail(message);
                }
                Assert.Inconclusive(message);
            }
        }

        /// <summary>
        /// What a failed update says: the product it asked for and where
        /// that came from, then the failure.
        /// </summary>
        private static string UpdateFailed(string product, Exception failure)
        {
            var asked = product == null ?
                "The update named no product, as " +
                    $"'{Constants.DATA_UPDATE_PRODUCT_ENV_VAR}' is not set. " +
                    "A license key that holds several products needs it." :
                $"The update asked for the product '{product}', from " +
                    $"'{Constants.DATA_UPDATE_PRODUCT_ENV_VAR}'.";
            return $"{asked} {failure.Message}";
        }
    }
}
