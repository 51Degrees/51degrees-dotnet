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

using FiftyOne.Did.Client;
using FiftyOne.Did.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FiftyOne.Did.Tests
{
    /// <summary>
    /// Live tests for <see cref="DidClient"/> against the cloud. They
    /// create a 51Did through the cloud <c>json</c> endpoint with the
    /// resource key from the environment, exactly as
    /// <see cref="CloudFodIdTests"/> does, then verify it offline and
    /// through the cloud, and redeem both a garbage result and one the
    /// cloud sealed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The resource key is found by <see cref="LiveCloudKey"/>, which fails
    /// the tests when none is set, and has to carry the FODid product.
    /// <c>_51DEGREES_LICENSE_KEY_51DID</c>, then
    /// <c>_51DEGREES_LICENSE_KEY</c> (or <c>LICENSE_KEY</c>), is read for
    /// the license key and is optional. When one is set it must belong to
    /// the resource key's account and carry the 51Did product, because a
    /// sealed result must then read back as a verdict.
    /// <c>FOD_CLOUD_API_URL</c> points every call at another host.
    /// </para>
    /// <para>
    /// These are integration tests that use the live cloud service, so
    /// any problem with that service could affect the result.
    /// </para>
    /// </remarks>
    [TestClass]
    public class CloudDidClientTests
    {
        private const string UserAgent =
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) " +
            "AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 " +
            "Mobile/15E148 Safari/604.1";

        /// <summary>
        /// A client IP for the request. 203.0.113.0/24 is the TEST-NET-3
        /// range reserved for documentation (RFC 5737).
        /// </summary>
        private const string ClientIp = "203.0.113.42";

        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        };

        private static string? LicenceKey() =>
            FirstSet(
                "_51DEGREES_LICENSE_KEY_51DID",
                "_51DEGREES_LICENSE_KEY");

        private static string? FirstSet(params string[] names)
        {
            foreach (var name in names)
            {
                var value = Environment.GetEnvironmentVariable(name);
                if (string.IsNullOrWhiteSpace(value) == false)
                {
                    return value;
                }
            }
            return null;
        }

        /// <summary>
        /// A client for the key in the environment, failing the test when
        /// there is none.
        /// </summary>
        private static DidClient Client() =>
            new DidClient(
                LiveCloudKey.Require(
                    "FODid",
                    "a non-marketing 51Did, which they verify and redeem"),
                LicenceKey());

        /// <summary>
        /// Creates a 51Did through the cloud json endpoint and parses it,
        /// failing the test when the key returns none.
        /// </summary>
        private static async Task<FodId> CreateAsync(DidClient client) =>
            FodId.FromBase64(await CreateEncodedAsync(client));

        /// <summary>
        /// The 51Did exactly as the cloud json endpoint returned it, failing
        /// the test when the key returns none.
        /// </summary>
        private static async Task<string> CreateEncodedAsync(DidClient client)
        {
            var url =
                $"{client.Endpoint}json?resource={Uri.EscapeDataString(client.ResourceKey)}" +
                $"&user-agent={Uri.EscapeDataString(UserAgent)}" +
                $"&client-ip={Uri.EscapeDataString(ClientIp)}" +
                "&id.usage=non-marketing";
            using var response = await Http.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();
            Assert.IsTrue(response.IsSuccessStatusCode,
                $"Cloud creation request failed " +
                $"({(int)response.StatusCode} {response.StatusCode}): {body}");
            using var document = JsonDocument.Parse(body);
            JsonElement? fodid =
                document.RootElement.TryGetProperty("fodid", out var element)
                    ? element
                    : null;
            var id = fodid is JsonElement found
                && found.TryGetProperty("idprobglobal", out var value)
                && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
            if (string.IsNullOrEmpty(id))
            {
                Assert.Fail(LiveCloudKey.Missing(
                    "fodid.idprobglobal",
                    LiveCloudKey.NullReason(fodid, "idprobglobal"),
                    "FODid"));
            }
            return id;
        }

        /// <summary>
        /// Seals a creator context result for the 51Did, standing in for
        /// the browser call the client does not carry, failing the test
        /// when the host returns none.
        /// </summary>
        private static async Task<string> SealAsync(
            DidClient client, string encoded, string challenge)
        {
            var url =
                $"{client.Endpoint}id/verify-context/{Uri.EscapeDataString(client.ResourceKey)}" +
                $"?51did={Uri.EscapeDataString(encoded)}" +
                $"&challenge={Uri.EscapeDataString(challenge)}";
            using var response = await Http.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();
            Assert.IsTrue(response.IsSuccessStatusCode,
                $"verify-context request failed " +
                $"({(int)response.StatusCode} {response.StatusCode}): {body}");
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var sealedResult = root.TryGetProperty("result", out var result)
                && result.ValueKind == JsonValueKind.String
                ? result.GetString()
                : null;
            if (string.IsNullOrEmpty(sealedResult))
            {
                var errors = root.TryGetProperty("errors", out var listed)
                    ? listed.GetRawText()
                    : "no reason given";
                Assert.Fail(
                    "The cloud answered verify-context without a sealed " +
                    $"result ({errors}), which the production cloud " +
                    $"returns, so the host at {client.Endpoint} holds no " +
                    "creator context secret.");
            }
            return sealedResult;
        }

        /// <summary>
        /// The failure for a host that answers 404 for the creator context,
        /// which the production cloud offers.
        /// </summary>
        private static void NoCreatorContext(NotSupportedException e) =>
            Assert.Fail(
                "The cloud answered without the creator context " +
                $"({e.Message}), which the production cloud offers, so " +
                "the host the test called is not configured for it.");

        [TestMethod]
        public async Task Create_VerifyOffline_VerifyThroughCloud()
        {
            using var client = Client();
            var fodId = await CreateAsync(client);

            var key = await client.PublicKeyForAsync(fodId);
            Assert.IsNotNull(key,
                $"No published key covers the identifier's date {fodId.Date:o}.");
            Assert.AreEqual(
                SignatureCheck.Verified,
                await client.VerifySignatureDetailedAsync(fodId),
                "offline signature check");
            Assert.IsTrue(await client.VerifySignatureAsync(fodId));
            Assert.IsTrue(await client.VerifyAsync(fodId), "cloud signature check");

            Console.WriteLine(
                $"domain={fodId.Domain} date={fodId.Date:o} " +
                $"key starts {key!.StartsAt:o} " +
                $"payload={fodId.Payload.Length} bytes");
        }

        [TestMethod]
        public async Task Redeem_GarbageResult_IsUnreadable()
        {
            using var client = Client();
            var fodId = await CreateAsync(client);

            RedeemResult result;
            try
            {
                result = await client.RedeemAsync(
                    fodId, "not-base64url!!", "0123456789abcdef");
            }
            catch (NotSupportedException e)
            {
                NoCreatorContext(e);
                return;
            }

            // Every cryptographic failure is the one word, with status 200,
            // by design.
            Assert.AreEqual(200, result.StatusCode, result.Raw);
            Assert.AreEqual(ContextOutcome.Unreadable, result.Context, result.Raw);
            Assert.AreEqual(SignatureOutcome.Unknown, result.Signature);
        }

        [TestMethod]
        public async Task Redeem_SealedResult_ReadsVerdictWithLicenseKey()
        {
            using var client = Client();
            var encoded = await CreateEncodedAsync(client);

            var challenge = Guid.NewGuid().ToString("N");
            var sealedResult = await SealAsync(client, encoded, challenge);

            RedeemResult result;
            try
            {
                result = await client.RedeemAsync(encoded, sealedResult, challenge);
            }
            catch (NotSupportedException e)
            {
                NoCreatorContext(e);
                return;
            }

            if (LicenceKey() == null)
            {
                Assert.AreEqual(ContextOutcome.Unreadable, result.Context,
                    "without a license key the sealed result cannot be read: " +
                    result.Raw);
                return;
            }

            Assert.AreNotEqual(ContextOutcome.Unreadable, result.Context,
                "the license key reads the sealed result, so it belongs to " +
                "the resource key's account and carries 51Did: " + result.Raw);
            Assert.AreEqual(SignatureOutcome.Verified, result.Signature, result.Raw);
            // The 51Did was created with a User-Agent and client IP that are
            // not this machine's, so factors are expected to differ. What
            // matters is that each one is reported.
            Assert.IsNotNull(result.Factors, result.Raw);
            CollectionAssert.AreEquivalent(
                FactorName.All.ToList(), result.Factors!.Keys.ToList(), result.Raw);

            Console.WriteLine(
                $"redeemed: context={result.ContextValue} " +
                $"signature={result.Signature} factors={result.Factors.Count}");
        }
    }
}
