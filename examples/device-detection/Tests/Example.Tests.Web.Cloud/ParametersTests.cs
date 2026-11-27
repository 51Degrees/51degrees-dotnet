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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.DeviceDetection.Example.Tests.Web.Cloud
{
    /// <summary>
    /// The Accept-CH cases. The single header cases use the licence key
    /// and ask for one SetHeader property each, named with its component,
    /// and no case carries a key, so no key is part of a test's name. The
    /// tests read no key and call nothing.
    /// </summary>
    [TestClass]
    public class ParametersTests
    {
        private static readonly string[] SetHeaderProperties =
        {
            Parameters.BROWSER_PROPERTY,
            Parameters.HARDWARE_PROPERTY,
            Parameters.PLATFORM_PROPERTY,
        };

        [TestMethod]
        public void TheLicenceKeyIsTheBespokeLicence()
        {
            Assert.AreEqual(
                "_51DEGREES_LICENSE_KEY_BESPOKE",
                Parameters.LICENSE_KEY_ENV_VAR);
        }

        [TestMethod]
        public void EachSingleHeaderCaseAsksForItsOwnPropertyOnly()
        {
            var expected = new Dictionary<string, string>()
            {
                { "browser", "device.SetHeaderBrowserAccept-CH" },
                { "hardware", "device.SetHeaderHardwareAccept-CH" },
                { "platform", "device.SetHeaderPlatformAccept-CH" },
            };
            foreach (var testCase in expected)
            {
                var asked = Parameters.LICENSE_KEY_CASES[testCase.Key]
                    .Split(',');
                CollectionAssert.AreEquivalent(
                    new[] { testCase.Value },
                    asked.Where(p => p.Contains("SetHeader")).ToArray(),
                    $"The '{testCase.Key}' case.");
            }
        }

        [TestMethod]
        public void TheNoneCaseAsksForNoSetHeaderProperty()
        {
            var asked = Parameters.LICENSE_KEY_CASES["none"].Split(',');
            Assert.IsTrue(asked.Length > 0);
            Assert.IsFalse(
                asked.Any(p => p.Contains("SetHeader")),
                string.Join(",", asked));
        }

        [TestMethod]
        public void EveryPropertyAskedForNamesItsComponent()
        {
            foreach (var values in Parameters.LICENSE_KEY_CASES.Values)
            {
                foreach (var property in values.Split(','))
                {
                    StringAssert.StartsWith(property, "device.");
                }
            }
        }

        [TestMethod]
        public void NoCaseCarriesAKey()
        {
            var keys = new[]
            {
                Environment.GetEnvironmentVariable(
                    Parameters.LICENSE_KEY_ENV_VAR),
                LiveCloudKey.Find(),
            }.Where(k => string.IsNullOrEmpty(k) == false).ToList();
            foreach (var row in Parameters.VerifyCloudExample_DATA())
            {
                foreach (var value in row.OfType<string>())
                {
                    CollectionAssert.DoesNotContain(keys, value);
                }
            }
        }

        [TestMethod]
        public void OnlyChromeAndEdgeExpectAcceptCH()
        {
            foreach (var row in Parameters.VerifyCloudExample_DATA())
            {
                var userAgent = (string)row[2];
                var expected = (List<string>)row[3];
                var chromium =
                    userAgent == Web.Parameters.CHROME_UA ||
                    userAgent == Web.Parameters.EDGE_UA;
                var setHeader = row[1] == null
                    ? SetHeaderProperties.Length
                    : ((string)row[1]).Split(',')
                        .Count(p => SetHeaderProperties.Contains(p));
                Assert.AreEqual(
                    chromium && setHeader > 0,
                    expected.Count > 0,
                    $"The '{row[0]}' case for {userAgent}.");
            }
        }
    }
}
