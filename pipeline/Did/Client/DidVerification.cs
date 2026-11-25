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

using System.Text.Json;

namespace FiftyOne.Did.Client
{
    /// <summary>
    /// The typed answer from the cloud's verify endpoint, built by
    /// <see cref="DidClient.VerifyDetailedAsync(string, System.Threading.CancellationToken)"/>
    /// from the JSON body. <see cref="Raw"/> keeps the body as received and
    /// <see cref="StatusCode"/> the HTTP status, so nothing the cloud said
    /// is lost in the mapping.
    /// </summary>
    public sealed class DidVerification
    {
        /// <summary>
        /// Creates a verification from its parts. Callers normally get one
        /// from <see cref="DidClient"/> rather than building one.
        /// </summary>
        /// <param name="valid">Whether the signature is valid.</param>
        /// <param name="humanConfidence">The Human Confidence Value, or null.</param>
        /// <param name="humanConfidenceNullReason">Why there is no Value, or null.</param>
        /// <param name="statusCode">The HTTP status.</param>
        /// <param name="raw">The response body.</param>
        public DidVerification(
            bool valid,
            HumanConfidenceValue? humanConfidence,
            string? humanConfidenceNullReason,
            int statusCode,
            string raw)
        {
            Valid = valid;
            HumanConfidence = humanConfidence;
            HumanConfidenceNullReason = humanConfidenceNullReason;
            StatusCode = statusCode;
            Raw = raw ?? string.Empty;
        }

        /// <summary>
        /// Whether the cloud found the identifier well formed and its
        /// signature valid.
        /// </summary>
        public bool Valid { get; }

        /// <summary>
        /// The Human Confidence Value the 51Did carries, present when the
        /// signature is valid and the call presented a License Key valid
        /// for Human Confidence. Null when the cloud sent no
        /// <c>humanConfidence</c>, which a cloud release that predates the
        /// field never does, or sent a word this client does not recognise,
        /// which stays in <see cref="Raw"/>.
        /// </summary>
        public HumanConfidenceValue? HumanConfidence { get; }

        /// <summary>
        /// The cloud's reason for giving no Human Confidence Value, such as
        /// a call made without a License Key valid for Human Confidence, or
        /// null when the cloud sent none.
        /// </summary>
        public string? HumanConfidenceNullReason { get; }

        /// <summary>
        /// The HTTP status the cloud answered with, 200 for a valid
        /// signature and 400 for an invalid one.
        /// </summary>
        public int StatusCode { get; }

        /// <summary>The response body as received.</summary>
        public string Raw { get; }
    }

    /// <summary>
    /// Reads the Human Confidence fields that the 51Did verify and redeem
    /// answers share.
    /// </summary>
    internal static class HumanConfidenceFields
    {
        /// <summary>The field holding the Value.</summary>
        internal const string Value = "humanConfidence";

        /// <summary>The field holding the reason there is no Value.</summary>
        internal const string NullReason = "humanConfidenceNullReason";

        /// <summary>
        /// Maps a <c>humanConfidence</c> string to its Value, in any case.
        /// Anything not recognised, including null, is null.
        /// </summary>
        internal static HumanConfidenceValue? Parse(string? value)
        {
            switch (value?.ToLowerInvariant())
            {
                case "high": return HumanConfidenceValue.High;
                case "medium": return HumanConfidenceValue.Medium;
                case "low": return HumanConfidenceValue.Low;
                case "absent": return HumanConfidenceValue.Absent;
                default: return null;
            }
        }

        /// <summary>The Value in an answer, or null.</summary>
        internal static HumanConfidenceValue? ReadValue(JsonElement root)
            => Parse(ReadString(root, Value));

        /// <summary>The reason in an answer, or null.</summary>
        internal static string? ReadNullReason(JsonElement root)
            => ReadString(root, NullReason);

        private static string? ReadString(JsonElement element, string name)
            => element.TryGetProperty(name, out var value)
                && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }
}
