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

using FiftyOne.IpIntelligence.Engine.OnPremise.FlowElements;
using FiftyOne.Pipeline.Engines;
using FiftyOne.Pipeline.Engines.FiftyOne.Data;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace FiftyOne.IpIntelligence.OnPremise.Tests.Core.FlowElements
{
    /// <summary>
    /// The product name the builder reads from a data file's header, which
    /// the data update service asks for before an engine has loaded the
    /// file, is the one an engine that has loaded the file gives.
    /// </summary>
    [TestClass]
    public class ProductFromFileTests
    {
        private const string AsnDataFileName = "51Degrees-IPIV4AsnIpiV41.ipi";

        private sealed class ProductBuilder : IpiOnPremiseEngineBuilder
        {
            public ProductBuilder(ILoggerFactory loggerFactory) :
                base(loggerFactory)
            {
            }

            public string Read(string fileName) => GetProductFromFile(fileName);
        }

        /// <summary>
        /// The product of the file read both ways.
        /// </summary>
        private static string ProductOfFile(string file)
        {
            using (var loggerFactory = new LoggerFactory())
            using (var engine = new IpiOnPremiseEngineBuilder(loggerFactory)
                .SetPerformanceProfile(PerformanceProfiles.LowMemory)
                .SetAutoUpdate(false)
                .SetDataFileSystemWatcher(false)
                .SetDataUpdateOnStartup(false)
                .Build(file, false))
            {
                var dataFile = (FiftyOneDataFile)engine.DataFiles[0];
                var fromHeader = new ProductBuilder(loggerFactory).Read(file);
                Assert.AreEqual(dataFile.DataUpdateProduct, fromHeader,
                    "The header gives another product than the loaded " +
                    "engine.");
                return fromHeader;
            }
        }

        [TestMethod]
        public void ProductFromFile_Asn_IsTheProductOfTheLoadedFile()
        {
            var product = ProductOfFile(
                TestHelpers.Utils.GetFilePath(AsnDataFileName).FullName);
            Assert.IsTrue(
                product.StartsWith("IPIV4", StringComparison.Ordinal),
                $"The product '{product}' is not an IP Intelligence product.");
        }

        [TestMethod]
        public void ProductFromFile_Enterprise_IsTheProductOfTheLoadedFile()
        {
            Assert.AreEqual(
                "IPIV4Enterprise",
                ProductOfFile(
                    TestHelpers.Utils.GetPaidFilePath(
                        TestHelpers.Constants.IPI_DATA_FILE_NAME).FullName));
        }
    }
}
