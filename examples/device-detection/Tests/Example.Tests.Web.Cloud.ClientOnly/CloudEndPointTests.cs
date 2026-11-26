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

using FiftyOne.DeviceDetection.Examples.Cloud.GettingStartedWeb.ClientOnly.Pages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FiftyOne.DeviceDetection.Example.Tests.Web.Cloud.ClientOnly
{
    /// <summary>
    /// The pages add /api/v4/ and the key to the cloud's address, and CI
    /// gives the endpoint ending /api/v4/, as the cloud request engine
    /// takes it. These need no browser and no key.
    /// </summary>
    [TestClass]
    public class CloudEndPointTests
    {
        [DataTestMethod]
        [DataRow("https://cloud.51degrees.com/api/v4/")]
        [DataRow("https://cloud.51degrees.com/api/v4")]
        [DataRow("https://cloud.51degrees.com/")]
        [DataRow("https://cloud.51degrees.com")]
        [DataRow(" https://cloud.51degrees.com/API/V4/ ")]
        public void CloudBase_DropsTheApiPath(string value)
        {
            Assert.AreEqual(
                "https://cloud.51degrees.com",
                Model.CloudBase(value));
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("  ")]
        public void CloudBase_NotSet_IsThePublicCloud(string value)
        {
            Assert.AreEqual(
                "https://cloud.51degrees.com",
                Model.CloudBase(value));
        }

        [TestMethod]
        public void CloudBase_KeepsAnotherPath()
        {
            Assert.AreEqual(
                "https://example.com/cloud",
                Model.CloudBase("https://example.com/cloud/api/v4/"));
        }
    }
}
