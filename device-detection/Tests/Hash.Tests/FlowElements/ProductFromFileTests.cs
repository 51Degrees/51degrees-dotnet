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

using FiftyOne.DeviceDetection.Hash.Engine.OnPremise.FlowElements;
using FiftyOne.Pipeline.Engines;
using FiftyOne.Pipeline.Engines.FiftyOne.Data;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FiftyOne.DeviceDetection.Hash.Tests.FlowElements
{
    /// <summary>
    /// The product name the builder reads from a data file's header, which
    /// the data update service asks for before an engine has loaded the
    /// file, is the one an engine that has loaded the file gives.
    /// </summary>
    [TestClass]
    public class ProductFromFileTests
    {
        private sealed class ProductBuilder : DeviceDetectionHashEngineBuilder
        {
            public ProductBuilder(ILoggerFactory loggerFactory) :
                base(loggerFactory)
            {
            }

            public string Read(string fileName) => GetProductFromFile(fileName);
        }

        private static void AssertProductOfFile(string file, string product)
        {
            using (var loggerFactory = new LoggerFactory())
            using (var engine = new DeviceDetectionHashEngineBuilder(
                    loggerFactory)
                .SetPerformanceProfile(PerformanceProfiles.LowMemory)
                .SetAutoUpdate(false)
                .SetDataFileSystemWatcher(false)
                .SetDataUpdateOnStartup(false)
                .Build(file, false))
            {
                var dataFile = (FiftyOneDataFile)engine.DataFiles[0];
                Assert.AreEqual(product, dataFile.DataUpdateProduct,
                    "The loaded engine gives another product.");
                Assert.AreEqual(product,
                    new ProductBuilder(loggerFactory).Read(file),
                    "The header gives another product.");
            }
        }

        [TestMethod]
        public void ProductFromFile_Lite_IsTheProductOfTheLoadedFile()
        {
            AssertProductOfFile(
                TestHelpers.Utils.GetFilePath(
                    TestHelpers.Constants.LITE_HASH_DATA_FILE_NAME).FullName,
                "V4Lite");
        }

        [TestMethod]
        public void ProductFromFile_Tac_IsTheProductOfTheLoadedFile()
        {
            AssertProductOfFile(
                TestHelpers.Utils.GetPaidFilePath(
                    TestHelpers.Constants.TAC_HASH_DATA_FILE_NAME).FullName,
                "V4TAC");
        }
    }
}
