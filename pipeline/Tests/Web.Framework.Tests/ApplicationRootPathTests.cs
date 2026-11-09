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

using FiftyOne.Pipeline.Core.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Web.Framework.Tests
{
    [TestClass]
    public class ApplicationRootPathTests
    {
        private const string ApplicationRoot = @"C:\inetpub\site";

        // Stands in for HostingEnvironment.MapPath, which needs a hosted
        // application.
        private static string MapPath(string virtualPath)
        {
            return ApplicationRoot +
                virtualPath.Substring(1).Replace('/', '\\');
        }

        private static string Resolve(string dataFile)
        {
            var options = new PipelineOptions();
            options.Elements.Add(new ElementOptions
            {
                BuilderName = "Engine",
                BuildParameters = new Dictionary<string, object>
                {
                    { "DataFile", dataFile },
                },
            });
            WebPipeline.ResolveApplicationRootPaths(options, MapPath);
            return (string)options.Elements[0].BuildParameters["DataFile"];
        }

        [TestMethod]
        [DataRow("~/App_Data/data.hash")]
        [DataRow(@"~\App_Data\data.hash")]
        public void TildePathResolvesUnderApplicationRoot(string configured)
        {
            Assert.AreEqual(
                @"C:\inetpub\site\App_Data\data.hash",
                Resolve(configured));
        }

        [TestMethod]
        [DataRow("App_Data/data.hash")]
        [DataRow("data.hash")]
        [DataRow("~data.hash")]
        [DataRow(@"D:\data\data.hash")]
        public void OtherPathsAreUnchanged(string configured)
        {
            Assert.AreEqual(configured, Resolve(configured));
        }
    }
}
