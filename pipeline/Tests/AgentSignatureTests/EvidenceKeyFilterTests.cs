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
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Web.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.Pipeline.AgentSignature.Tests
{
    /// <summary>
    /// Checks the evidence the element accepts and where it reads a covered
    /// header from. The element accepts only the eight keys the
    /// specification names, because a pipeline's accepted evidence decides
    /// its cache keys, the ETag and the Vary header. A signature covering
    /// any other header reports Unverified with the ComponentUnavailable
    /// reason, and that header is not read from other evidence or from the
    /// web request a web integration adds as 'server.http-request'.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/4612778babd00c032a69ad41608e5c6fed38dc33/pipeline-specification/pipeline-elements/agent-signature-element.md#accepted-evidence">Specification</see>
    /// </summary>
    [TestClass]
    public class EvidenceKeyFilterTests
    {
        /// <summary>
        /// The header a signature covers in the tests below, being an
        /// ordinary request header rather than one the element accepts.
        /// </summary>
        private const string OrdinaryHeaderKey = "header.content-type";

        /// <summary>
        /// The value of that header the signature was made over.
        /// </summary>
        private const string OrdinaryHeaderValue = "application/json";

        /// <summary>
        /// The key a web integration adds the web request under, written
        /// out rather than read from the package, so the tests check the
        /// key the specification gives.
        /// </summary>
        private const string HttpRequestKey = "server.http-request";

        /// <summary>
        /// The eight keys the specification names, and the only ones the
        /// element accepts.
        /// </summary>
        private static readonly string[] AcceptedKeys = new[]
        {
            "header.signature",
            "header.signature-input",
            "header.signature-agent",
            "header.host",
            "header.protocol",
            "server.request-method",
            "server.request-path",
            "server.request-query",
        };

        /// <summary>
        /// A web request as a web integration adds it, being the read only
        /// view the integrations share, keyed as the request's values would
        /// be as evidence.
        /// </summary>
        private sealed class WebRequest : RequestEvidenceView
        {
            private readonly IDictionary<string, object> _values;

            public WebRequest(IDictionary<string, object> values)
            {
                _values = values;
            }

            protected override void AddValues(
                IDictionary<string, object> values)
            {
                foreach (var entry in _values)
                {
                    values[entry.Key] = entry.Value;
                }
            }
        }

        /// <summary>
        /// The element accepts the eight keys the specification names, in
        /// any case, and nothing else.
        /// </summary>
        /// <param name="key">The evidence key.</param>
        /// <param name="expected">Whether the element accepts it.</param>
        /// <param name="why">Why, for the assertion message.</param>
        [DataTestMethod]
        [DataRow(
            "header.signature",
            true,
            "it carries the signature itself")]
        [DataRow(
            "header.signature-input",
            true,
            "it says what the signature covers")]
        [DataRow(
            "header.signature-agent",
            true,
            "it says where the agent publishes its keys")]
        [DataRow(
            "header.host",
            true,
            "'@authority' is built from it")]
        [DataRow(
            "header.protocol",
            true,
            "'@authority' and '@scheme' are built from it")]
        [DataRow(
            "server.request-method",
            true,
            "'@method' is built from it")]
        [DataRow(
            "server.request-path",
            true,
            "'@path' and '@target-uri' are built from it")]
        [DataRow(
            "server.request-query",
            true,
            "'@query' and '@target-uri' are built from it")]
        [DataRow(
            "HEADER.Signature-Input",
            true,
            "an evidence key is matched whatever its case")]
        [DataRow(
            "header.content-type",
            false,
            "a covered header beyond the eight keys is read from the " +
            "evidence or the web request without being accepted")]
        [DataRow(
            "header.if-none-match",
            false,
            "a revalidation header would put itself into the ETag")]
        [DataRow(
            "header.x-something-nobody-has-heard-of",
            false,
            "only the keys the specification names are accepted")]
        [DataRow(
            HttpRequestKey,
            false,
            "the web request is read without being accepted")]
        [DataRow(
            "query.signature",
            false,
            "a query string value is written by whoever sent the " +
            "request, so it is only read where a service has said it " +
            "receives forwarded evidence")]
        [DataRow(
            "query.foo",
            false,
            "a query string value is not one of the eight keys")]
        [DataRow(
            "cookie.bar",
            false,
            "a cookie is not one of the eight keys")]
        [DataRow(
            "server.client-ip",
            false,
            "the address the request came from says nothing about what " +
            "the signature covers")]
        [DataRow(
            "headerless",
            false,
            "it only looks like a header key")]
        public void EvidenceKeyIsAcceptedOnlyWhenTheSpecificationNamesIt(
            string key,
            bool expected,
            string why)
        {
            using (var harness = ElementHarness.Create())
            {
                Assert.AreEqual(
                    expected,
                    harness.Element.EvidenceKeyFilter.Include(key),
                    "The element should " +
                    (expected ? "accept" : "not accept") +
                    " the evidence key '" + key + "', because " + why +
                    ".");
                Assert.AreEqual(
                    expected,
                    harness.Element.EvidenceKeyFilter.Order(key).HasValue,
                    "The evidence key '" + key + "' should " +
                    (expected ? "be given an order, because the element " +
                        "accepts it" : "be given no order, because the " +
                        "element does not accept it") + ".");
            }
        }

        /// <summary>
        /// The published whitelist holds the eight keys and no others.
        /// </summary>
        [TestMethod]
        public void WhitelistHoldsExactlyTheEightKeys()
        {
            using (var harness = ElementHarness.Create())
            {
                var whitelist = ((EvidenceKeyFilterWhitelist)harness.Element
                    .EvidenceKeyFilter).Whitelist;

                CollectionAssert.AreEquivalent(
                    AcceptedKeys.Select(k => k.ToLowerInvariant()).ToList(),
                    whitelist.Keys.Select(k => k.ToLowerInvariant()).ToList(),
                    "Expected the eight keys the specification names, and " +
                    "the whitelist holds '" +
                    string.Join("', '", whitelist.Keys) + "'.");
            }
        }

        /// <summary>
        /// The published whitelist names the host header spelled exactly as
        /// the shared key is, so that combining it with another element's
        /// whitelist by ordinal comparison yields one key, not two.
        /// </summary>
        [TestMethod]
        public void HostKeyIsPublishedInTheSharedSpelling()
        {
            using (var harness = ElementHarness.Create())
            {
                var whitelist = ((EvidenceKeyFilterWhitelist)harness.Element
                    .EvidenceKeyFilter).Whitelist;

                Assert.IsTrue(
                    whitelist.Keys.Contains(
                        Core.Constants.EVIDENCE_HOST_KEY,
                        StringComparer.Ordinal),
                    "Expected '" + Core.Constants.EVIDENCE_HOST_KEY + "' " +
                    "but the whitelist holds '" +
                    string.Join("', '", whitelist.Keys) + "'.");
            }
        }

        /// <summary>
        /// A pipeline holding the element does not accept a header the
        /// element only reads, so that header takes no part in the
        /// pipeline's cache keys, its ETag or its Vary header. This holds
        /// whether or not forwarded evidence is trusted.
        /// </summary>
        [TestMethod]
        public void UnrelatedHeaderDoesNotReachThePipelinesAcceptedEvidence()
        {
            foreach (var trusted in new[] { false, true })
            {
                using (var harness = ElementHarness.Create(
                    builder => builder.SetTrustForwardedEvidence(trusted)))
                {
                    foreach (var key in new[]
                    {
                        OrdinaryHeaderKey,
                        "header.if-none-match",
                        "header.user-agent",
                        HttpRequestKey,
                    })
                    {
                        Assert.IsFalse(
                            harness.Pipeline.EvidenceKeyFilter.Include(key),
                            "The pipeline should not accept '" + key +
                            "' with forwarded evidence " +
                            (trusted ? "trusted" : "not trusted") +
                            ", because the agent signature element only " +
                            "accepts the eight keys the specification " +
                            "names.");
                    }
                    foreach (var key in AcceptedKeys)
                    {
                        Assert.IsTrue(
                            harness.Pipeline.EvidenceKeyFilter.Include(key),
                            "The pipeline should accept '" + key +
                            "', because the agent signature element " +
                            "needs it.");
                    }
                }
            }
        }

        /// <summary>
        /// A signature covering an ordinary request header is Unverified
        /// where the caller supplies that header as evidence, because the
        /// element does not accept the header and so does not read it.
        /// </summary>
        [TestMethod]
        public void SignatureCoveringAHeaderInOtherEvidenceIsUnverified()
        {
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var result = harness.ProcessSigned(
                    SignOverTheOrdinaryHeader(),
                    new Dictionary<string, string>
                    {
                        { OrdinaryHeaderKey, OrdinaryHeaderValue },
                    });

                AssertComponentUnavailable(result);
            }
        }

        /// <summary>
        /// A signature covering an ordinary request header is Unverified
        /// where only the web request holds that header, because the
        /// element does not read a header it does not accept from the web
        /// request. The header is spelled as a request would carry it.
        /// </summary>
        [TestMethod]
        public void SignatureCoveringAHeaderOnlyInTheWebRequestIsUnverified()
        {
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var result = harness.ProcessSignedWithWebRequest(
                    SignOverTheOrdinaryHeader(),
                    new WebRequest(new Dictionary<string, object>
                    {
                        { "header.Content-Type", OrdinaryHeaderValue },
                    }));

                AssertComponentUnavailable(result);
            }
        }

        /// <summary>
        /// Where the evidence and the web request both hold a covered
        /// header the element does not accept, neither is read.
        /// </summary>
        [TestMethod]
        public void HeaderInBothEvidenceAndTheWebRequestIsUnverified()
        {
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var result = harness.ProcessSignedWithWebRequest(
                    SignOverTheOrdinaryHeader(),
                    new WebRequest(new Dictionary<string, object>
                    {
                        { OrdinaryHeaderKey, "text/plain" },
                    }),
                    new Dictionary<string, string>
                    {
                        { OrdinaryHeaderKey, OrdinaryHeaderValue },
                    });

                AssertComponentUnavailable(result);
            }
        }

        /// <summary>
        /// A signature covering a header that neither the evidence nor the
        /// web request holds cannot be rebuilt, so the element says so
        /// rather than reporting a mismatch.
        /// </summary>
        [TestMethod]
        public void SignatureCoveringAHeaderInNeitherPlaceIsUnverified()
        {
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var result = harness.ProcessSignedWithWebRequest(
                    SignOverTheOrdinaryHeader(),
                    new WebRequest(new Dictionary<string, object>
                    {
                        { "header.accept", "*/*" },
                    }));

                AssertComponentUnavailable(result);
            }
        }

        /// <summary>
        /// A signature covering a header that did not arrive at all, with
        /// no web request either, cannot be rebuilt.
        /// </summary>
        [TestMethod]
        public void SignatureCoveringAHeaderThatDidNotArriveIsUnverified()
        {
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var result = harness.ProcessSigned(
                    SignOverTheOrdinaryHeader());

                AssertComponentUnavailable(result);
            }
        }

        /// <summary>
        /// With forwarded evidence trusted, the element accepts the eight
        /// names under the query prefix, in any case, and no other query
        /// key. With it not trusted, which is the default, the element
        /// accepts no query key at all.
        /// </summary>
        [TestMethod]
        public void ForwardedKeysAreAcceptedOnlyWhenTrustedAndOnlyTheEight()
        {
            foreach (var trusted in new[] { false, true })
            {
                using (var harness = ElementHarness.Create(
                    builder => builder.SetTrustForwardedEvidence(trusted)))
                {
                    var filter = harness.Element.EvidenceKeyFilter;
                    foreach (var key in AcceptedKeys.Select(ForwardedForm))
                    {
                        Assert.AreEqual(
                            trusted,
                            filter.Include(key),
                            "The element should " +
                            (trusted ? "accept" : "not accept") +
                            " the forwarded key '" + key +
                            "' with forwarded evidence " +
                            (trusted ? "trusted" : "not trusted") + ".");
                        Assert.AreEqual(
                            trusted,
                            filter.Order(key).HasValue,
                            "The forwarded key '" + key + "' should " +
                            (trusted ? "be given an order" :
                                "be given no order") + ".");
                    }
                    Assert.AreEqual(
                        trusted,
                        filter.Include("QUERY.Request-Path"),
                        "A forwarded key is matched whatever its case.");
                    foreach (var key in new[]
                    {
                        "query.foo",
                        "query.content-type",
                        "query.if-none-match",
                        "query.http-request",
                        "query.client-ip",
                        "query.",
                        "query",
                    })
                    {
                        Assert.IsFalse(
                            filter.Include(key),
                            "The element should not accept '" + key +
                            "' with forwarded evidence " +
                            (trusted ? "trusted" : "not trusted") +
                            ", because it is not one of the eight names " +
                            "under the query prefix.");
                        Assert.IsFalse(
                            filter.Order(key).HasValue,
                            "The key '" + key + "' should be given no " +
                            "order.");
                    }
                }
            }
        }

        /// <summary>
        /// A pipeline holding the element, with forwarded evidence trusted,
        /// accepts the eight names under the query prefix and no other
        /// query key, so a visitor's query string parameters take no part
        /// in the pipeline's cache keys, its ETag or its Vary header.
        /// </summary>
        [TestMethod]
        public void TrustedPipelineAcceptsOnlyTheEightForwardedKeys()
        {
            using (var harness = ElementHarness.Create(
                builder => builder.SetTrustForwardedEvidence(true)))
            {
                foreach (var key in AcceptedKeys.Select(ForwardedForm))
                {
                    Assert.IsTrue(
                        harness.Pipeline.EvidenceKeyFilter.Include(key),
                        "The pipeline should accept '" + key + "'.");
                }
                foreach (var key in new[]
                {
                    "query.foo",
                    "query.utm_source",
                    "query.content-type",
                })
                {
                    Assert.IsFalse(
                        harness.Pipeline.EvidenceKeyFilter.Include(key),
                        "The pipeline should not accept '" + key +
                        "', because it is not one of the eight names " +
                        "under the query prefix.");
                }
            }
        }

        /// <summary>
        /// A forwarded signature covering an ordinary header is Unverified
        /// where the caller supplies that header under the query prefix,
        /// because the header is not one of the eight names.
        /// </summary>
        [TestMethod]
        public void ForwardedHeaderInTheEvidenceIsUnverified()
        {
            var signed = SignOverTheOrdinaryHeader();
            using (var harness = ElementHarness.CreateWithTestKey(
                builder => builder.SetTrustForwardedEvidence(true)))
            {
                var forwarded = Forwarded(signed);
                forwarded["query.content-type"] = OrdinaryHeaderValue;

                AssertComponentUnavailable(
                    harness.ProcessSignedWithWebRequest(
                        signed,
                        new WebRequest(new Dictionary<string, object>()),
                        forwarded));
            }
        }

        /// <summary>
        /// A forwarded signature covering an ordinary header is Unverified
        /// where only the web request holds that header, whose query string
        /// values carry the query prefix.
        /// </summary>
        [TestMethod]
        public void ForwardedHeaderOnlyInTheWebRequestIsUnverified()
        {
            var signed = SignOverTheOrdinaryHeader();
            using (var harness = ElementHarness.CreateWithTestKey(
                builder => builder.SetTrustForwardedEvidence(true)))
            {
                AssertComponentUnavailable(
                    harness.ProcessSignedWithWebRequest(
                        signed,
                        new WebRequest(new Dictionary<string, object>
                        {
                            { "query.content-type", OrdinaryHeaderValue },
                        }),
                        Forwarded(signed)));
            }
        }

        /// <summary>
        /// Where the evidence and the web request both hold a covered
        /// header under the query prefix, neither is read.
        /// </summary>
        [TestMethod]
        public void ForwardedHeaderInBothEvidenceAndTheWebRequestIsUnverified()
        {
            var signed = SignOverTheOrdinaryHeader();
            using (var harness = ElementHarness.CreateWithTestKey(
                builder => builder.SetTrustForwardedEvidence(true)))
            {
                var forwarded = Forwarded(signed);
                forwarded["query.content-type"] = OrdinaryHeaderValue;

                AssertComponentUnavailable(
                    harness.ProcessSignedWithWebRequest(
                        signed,
                        new WebRequest(new Dictionary<string, object>
                        {
                            { "query.content-type", "text/plain" },
                        }),
                        forwarded));
            }
        }

        /// <summary>
        /// The two prefixes are not mixed in one signature. A forwarded
        /// signature covering a header that only the header prefix holds
        /// cannot be rebuilt, because the header describes the call the
        /// service received and not the request the agent signed.
        /// </summary>
        [TestMethod]
        public void ForwardedSignatureDoesNotTakeAHeaderFromTheHeaderPrefix()
        {
            var signed = SignOverTheOrdinaryHeader();
            using (var harness = ElementHarness.CreateWithTestKey(
                builder => builder.SetTrustForwardedEvidence(true)))
            {
                var forwarded = Forwarded(signed);
                forwarded[OrdinaryHeaderKey] = OrdinaryHeaderValue;

                AssertComponentUnavailable(
                    harness.ProcessSignedWithWebRequest(
                        signed,
                        new WebRequest(new Dictionary<string, object>
                        {
                            { OrdinaryHeaderKey, OrdinaryHeaderValue },
                        }),
                        forwarded));
            }
        }

        /// <summary>
        /// With forwarded evidence not trusted, a query string parameter is
        /// never read as a covered header, so a visitor cannot supply one.
        /// </summary>
        [TestMethod]
        public void QueryStringValueIsNotReadAsAHeaderWhenNotTrusted()
        {
            var signed = SignOverTheOrdinaryHeader();
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                AssertComponentUnavailable(
                    harness.ProcessSignedWithWebRequest(
                        signed,
                        new WebRequest(new Dictionary<string, object>
                        {
                            { "query.content-type", OrdinaryHeaderValue },
                        }),
                        new Dictionary<string, string>
                        {
                            { "query.content-type", OrdinaryHeaderValue },
                        }));
            }
        }

        /// <summary>
        /// The name of one of the eight keys as a forwarded request
        /// carries it, with its prefix replaced by the query prefix.
        /// </summary>
        private static string ForwardedForm(string key)
        {
            return "query." + key.Substring(key.IndexOf('.') + 1);
        }

        /// <summary>
        /// The evidence a caller's own pipeline forwards for a signed
        /// request, being the signature headers, the host and the protocol
        /// under the query prefix.
        /// </summary>
        private static Dictionary<string, string> Forwarded(
            SignedRequest signed)
        {
            return new Dictionary<string, string>
            {
                { "query.signature", signed.Signature },
                { "query.signature-input", signed.SignatureInput },
                { "query.signature-agent", signed.SignatureAgent },
                { "query.host", "example.com" },
                { "query.protocol", "https" },
            };
        }

        private static SignedRequest SignOverTheOrdinaryHeader()
        {
            var options = new SigningOptions();
            options.ExtraComponents.Add(
                new KeyValuePair<string, string>(
                    "\"content-type\"", OrdinaryHeaderValue));
            return RequestSigner.Sign(options);
        }

        private static void AssertComponentUnavailable(
            IAgentSignatureData result)
        {
            Assert.AreEqual(
                Constants.STATUS_UNVERIFIED,
                result.AgentSignature.Value,
                "Expected Unverified for a signature covering a header " +
                "the request did not carry. The status was '" +
                result.AgentSignature.Value + "' with the reason '" +
                result.AgentSignatureReason.Value + "'.");
            Assert.AreEqual(
                Constants.REASON_COMPONENT_UNAVAILABLE,
                result.AgentSignatureReason.Value,
                "Expected the ComponentUnavailable reason, and the " +
                "reason was '" + result.AgentSignatureReason.Value +
                "'.");
        }
    }
}
