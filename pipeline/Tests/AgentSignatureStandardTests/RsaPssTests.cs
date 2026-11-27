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

using FiftyOne.Pipeline.AgentSignature.Data;
using FiftyOne.Pipeline.AgentSignature.Tests.Helpers;
using FiftyOne.Pipeline.AgentSignature.Tests.Standard.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Text;

namespace FiftyOne.Pipeline.AgentSignature.Tests.Standard
{
    /// <summary>
    /// The specification requires rsa-pss-sha512 to be verified. On .NET
    /// Framework, which loads the netstandard2.0 build, RSA.Create() gives
    /// an RSA that cannot verify PSS padding, so these tests run on .NET
    /// Framework as well as on net8.0.
    /// </summary>
    [TestClass]
    public class RsaPssTests
    {
        /// <summary>
        /// The algorithm's RFC 9421 registry name.
        /// </summary>
        private const string Algorithm = "rsa-pss-sha512";

        /// <summary>
        /// A signature length greater than the 256 bytes of the 2048 bit
        /// RSA test key.
        /// </summary>
        private const int LongerThanTheKeyBytes = 300;

        /// <summary>
        /// A request signed with the RSA test key and rsa-pss-sha512 reads
        /// Verified.
        /// </summary>
        [TestMethod]
        public void AnRsaPssSignatureReadsVerified()
        {
            using (var harness = CreateHarness())
            {
                var result = harness.ProcessSigned(Sign());

                Assert.AreEqual(
                    Constants.STATUS_VERIFIED,
                    result.AgentSignature.Value,
                    "Expected Verified, because the request was signed " +
                    "with the key the agent publishes. " + Describe(result));
                Assert.AreEqual(
                    Constants.REASON_VERIFIED,
                    result.AgentSignatureReason.Value,
                    "Expected the Verified reason. " + Describe(result));
                Assert.AreEqual(
                    Algorithm,
                    result.AgentSignatureAlgorithm.Value,
                    "Expected the algorithm the signature names. " +
                    Describe(result));
            }
        }

        /// <summary>
        /// The same request carrying a signature of other content reads
        /// Invalid with the SignatureMismatch reason, so the Verified above
        /// comes from checking the signature.
        /// </summary>
        [TestMethod]
        public void AnRsaPssSignatureOfOtherContentReadsSignatureMismatch()
        {
            using (var harness = CreateHarness())
            {
                var signed = Sign();
                var other = RequestSigner.SignBytes(
                    Fixtures.RsaKey(),
                    Encoding.ASCII.GetBytes(signed.SignatureBase + " "));
                signed.Signature =
                    "sig1=:" + Convert.ToBase64String(other) + ":";

                var result = harness.ProcessSigned(signed);

                Assert.AreEqual(
                    Constants.STATUS_INVALID,
                    result.AgentSignature.Value,
                    "Expected Invalid, because the signature is of other " +
                    "content. " + Describe(result));
                Assert.AreEqual(
                    Constants.REASON_SIGNATURE_MISMATCH,
                    result.AgentSignatureReason.Value,
                    "Expected the SignatureMismatch reason. " +
                    Describe(result));
            }
        }

        /// <summary>
        /// A signature longer than the key reads Invalid with the
        /// SignatureMismatch reason rather than raising an error out of the
        /// element.
        /// </summary>
        [TestMethod]
        public void AnRsaPssSignatureLongerThanTheKeyReadsSignatureMismatch()
        {
            using (var harness = CreateHarness())
            {
                var signed = Sign();
                var tooLong = new byte[LongerThanTheKeyBytes];
                for (var i = 0; i < tooLong.Length; i++)
                {
                    tooLong[i] = 0xFF;
                }
                signed.Signature =
                    "sig1=:" + Convert.ToBase64String(tooLong) + ":";

                var result = harness.ProcessSigned(signed);

                Assert.AreEqual(
                    Constants.STATUS_INVALID,
                    result.AgentSignature.Value,
                    "Expected Invalid, because the signature cannot have " +
                    "been made with the key. " + Describe(result));
                Assert.AreEqual(
                    Constants.REASON_SIGNATURE_MISMATCH,
                    result.AgentSignatureReason.Value,
                    "Expected the SignatureMismatch reason. " +
                    Describe(result));
            }
        }

        /// <summary>
        /// A harness whose agent publishes the public part of the RSA test
        /// key.
        /// </summary>
        private static StandardHarness CreateHarness()
        {
            var harness = StandardHarness.Create();
            harness.Handler.AddDirectory(
                Fixtures.SignatureAgentDirectoryUrl,
                RequestSigner.PublicPart(Fixtures.RsaKey()));
            return harness;
        }

        /// <summary>
        /// A request signed with the RSA test key and rsa-pss-sha512.
        /// </summary>
        private static SignedRequest Sign() =>
            RequestSigner.Sign(new SigningOptions
            {
                KeyJson = Fixtures.RsaKey(),
                KeyId = Fixtures.RsaThumbprint,
                Algorithm = Algorithm,
            });

        /// <summary>
        /// Describe what the element made of a request, so that every
        /// assertion message carries the status and the reason.
        /// </summary>
        private static string Describe(IAgentSignatureData result) =>
            "The status was '" + result.AgentSignature.Value +
            "' with the reason '" + result.AgentSignatureReason.Value + "'.";
    }
}
