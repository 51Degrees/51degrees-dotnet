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

using FiftyOne.Pipeline.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net.Http;

namespace FiftyOne.Pipeline.Core.Tests.Services
{
    /// <summary>
    /// A license key never appears in full in a log entry or an error
    /// message.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/7cd9bc82797ea86c39dd90ed41232f3b0f167e00/pipeline-specification/features/logging.md#license-keys">Specification</see>
    /// </summary>
    [TestClass]
    public class LicenseKeyMaskTests
    {
        private const string Key = "ABCDEFGHIJ1234567890";
        private const string Masked = "****************7890";

        [TestMethod]
        public void Mask_ShowsLastFourCharacters()
        {
            Assert.AreEqual(Masked, LicenseKeyMask.Mask(Key));
        }

        [TestMethod]
        [DataRow("", "")]
        [DataRow("a", "*")]
        [DataRow("abcd", "****")]
        [DataRow("abcde", "*bcde")]
        public void Mask_ShortKeys_NeverShownInFull(
            string key, string expected)
        {
            Assert.AreEqual(expected, LicenseKeyMask.Mask(key));
        }

        [TestMethod]
        public void Mask_Null_ReturnsNull()
        {
            Assert.IsNull(LicenseKeyMask.Mask(null));
        }

        [TestMethod]
        [DataRow("https://d.example/api?LicenseKeys=" + Key + "&Type=1")]
        [DataRow("https://d.example/api?type=1&licensekeys=" + Key)]
        [DataRow("https://d.example/api?license=" + Key)]
        [DataRow("https://d.example/api?licence=" + Key + "#x")]
        public void MaskText_QueryParameter_Masked(string text)
        {
            var result = LicenseKeyMask.MaskText(text);

            Assert.IsFalse(result.Contains(Key));
            Assert.Contains(Masked, result);
        }

        [TestMethod]
        public void MaskText_SeveralKeysInQuery_EachMasked()
        {
            const string other = "ZYXWVUTSRQ0987654321";
            var url = "https://d.example/api?LicenseKeys=" + Key + "|" +
                other + "&Download=True";
            var encoded = "https://d.example/api?LicenseKeys=" + Key +
                "%7C" + other + "&Download=True";

            foreach (var text in new[] { url, encoded })
            {
                var result = LicenseKeyMask.MaskText(text);

                Assert.IsFalse(result.Contains(Key));
                Assert.IsFalse(result.Contains(other));
                Assert.Contains("7890", result);
                Assert.Contains("4321", result);
                Assert.Contains("Download=True", result);
            }
        }

        [TestMethod]
        public void MaskText_KnownKeyAnywhere_Masked()
        {
            var text = $"The key '{Key}' was refused, {Uri.EscapeDataString(Key)}";

            var result = LicenseKeyMask.MaskText(text, new[] { Key, null, "" });

            Assert.IsFalse(result.Contains(Key));
            Assert.Contains(Masked, result);
        }

        [TestMethod]
        public void MaskText_NoKey_Unchanged()
        {
            const string text = "Checking for update from https://d.example/api?Type=1";

            Assert.AreEqual(text, LicenseKeyMask.MaskText(text));
            Assert.IsNull(LicenseKeyMask.MaskText(null));
        }

        [TestMethod]
        public void MaskUri_KeyInQuery_Masked()
        {
            var uri = new Uri("https://d.example/api?LicenseKeys=" + Key);

            var result = LicenseKeyMask.MaskUri(uri);

            Assert.IsFalse(result.Contains(Key));
            Assert.Contains(Masked, result);
        }

        /// <summary>
        /// The message of an error from an HTTP library names the URL, so
        /// the exception that replaces it holds only the masked form, at
        /// every depth.
        /// </summary>
        [TestMethod]
        public void MaskException_MessageAndInnerMessagesMasked()
        {
            var url = "https://d.example/api?LicenseKeys=" + Key;
            var original = new HttpRequestException(
                "Error sending " + url,
                new InvalidOperationException("Inner " + url));

            var result = LicenseKeyMask.MaskException(original);

            Assert.IsFalse(result.ToString().Contains(Key));
            Assert.Contains(Masked, result.Message);
            Assert.Contains(typeof(HttpRequestException).FullName, result.Message);
            Assert.IsNotNull(result.InnerException);
            Assert.Contains(Masked, result.InnerException.Message);
        }
    }
}
