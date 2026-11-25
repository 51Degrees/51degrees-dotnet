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
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Web;
using static FiftyOne.Did.Tests.FodIdTestFactory;

// Some tests here read the same answers through the obsolete RedeemAsync
// and the obsolete failure members of ContextOutcome, to show that they
// keep working.
#pragma warning disable CS0618

namespace FiftyOne.Did.Tests
{
    /// <summary>
    /// Tests for the names the cloud's 51Did endpoints take and return
    /// beside the ones they always have: <c>sealedResult</c> and
    /// <c>transaction</c> in, <c>status</c>, <c>humanConfidence</c>,
    /// <c>humanConfidenceNullReason</c> and <c>obsolete</c> out. Every
    /// answer is recorded JSON in the cloud's shape, against a recorded
    /// transport, so nothing here touches the network.
    /// </summary>
    [TestClass]
    public class DidClientSealedResultTests
    {
        private const string Resource = "AQAAAresourcekey";
        private const string Licence = "AQAAAlicencekey";
        private const string Endpoint = "https://cloud.example.test/api/v4/";

        private static readonly DateTime Created =
            new DateTime(2026, 11, 24, 9, 0, 0, DateTimeKind.Utc);

        private const string NullReason =
            "humanConfidence is a paid feature. You need a License Key valid "
            + "for Human Confidence, presented on this call, to retrieve it. "
            + "Visit https://51degrees.com/pricing for details";

        private const string Factors =
            "\"factors\":{\"transport\":\"verified\",\"device\":\"mismatch\","
            + "\"browserip\":\"verified\",\"connectionip\":\"mismatch\","
            + "\"asn\":\"verified\",\"platformname\":\"verified\","
            + "\"platformversion\":\"verified\",\"browsername\":\"verified\","
            + "\"browserversion\":\"notrecorded\"}";

        private const string Times =
            "\"verifiedAt\":\"2026-11-25T09:15:32Z\",\"secondsSinceVerified\":2";

        private const string ObsoleteMessages =
            "\"obsolete\":[\"result is obsolete. Use sealedResult.\","
            + "\"challenge is obsolete. Use transaction.\"]";

        // Each pair is an answer as a cloud release before status gave it,
        // then the same answer as the current cloud gives it, with every
        // added field present. The old fields are the same in both.
        private static readonly (string Name, int Status, string Before, string Now)[]
            Answers =
            {
                ("verified",
                    200,
                    "{\"signature\":\"verified\",\"context\":\"verified\"," + Times + "}",
                    "{\"signature\":\"verified\",\"context\":\"verified\","
                    + "\"humanConfidence\":\"High\",\"status\":\"success\","
                    + Times + "," + ObsoleteMessages + "}"),
                ("mismatch with factors",
                    200,
                    "{\"signature\":\"verified\",\"context\":\"mismatch\","
                    + Factors + "," + Times + "}",
                    "{\"signature\":\"verified\",\"context\":\"mismatch\","
                    + Factors + ",\"humanConfidence\":\"Absent\","
                    + "\"status\":\"success\"," + Times + "," + ObsoleteMessages + "}"),
                ("signature invalid, no licence for the Value",
                    200,
                    "{\"signature\":\"invalid\",\"context\":\"invaliddate\"," + Times + "}",
                    "{\"signature\":\"invalid\",\"context\":\"invaliddate\","
                    + "\"humanConfidenceNullReason\":\"" + NullReason + "\","
                    + "\"status\":\"success\"," + Times + "," + ObsoleteMessages + "}"),
                ("expired",
                    200,
                    "{\"context\":\"expired\",\"verifiedAt\":\"2026-11-25T09:15:32Z\","
                    + "\"secondsSinceVerified\":14}",
                    "{\"context\":\"expired\",\"status\":\"expired\","
                    + "\"verifiedAt\":\"2026-11-25T09:15:32Z\","
                    + "\"secondsSinceVerified\":14," + ObsoleteMessages + "}"),
                ("replayed",
                    200,
                    "{\"context\":\"replayed\"}",
                    "{\"context\":\"replayed\",\"status\":\"replayed\","
                    + ObsoleteMessages + "}"),
                ("unreadable",
                    200,
                    "{\"context\":\"unreadable\"}",
                    "{\"context\":\"unreadable\",\"status\":\"unreadable\","
                    + ObsoleteMessages + "}"),
                ("unconfirmed",
                    503,
                    "{\"context\":\"unconfirmed\"}",
                    "{\"context\":\"unconfirmed\",\"status\":\"unconfirmed\","
                    + ObsoleteMessages + "}"),
            };

        private FodIdTestFactory _factory = null!;
        private FakeHttpHandler _handler = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _factory = new FodIdTestFactory();
            _handler = new FakeHttpHandler();
        }

        private DidClient NewClient(string? licence = Licence) =>
            new DidClient(Resource, licence, Endpoint, _handler.Client);

        private FodId Signed() =>
            new FodId(_factory.SignedOwid(CanonicalPayload(), Created));

        private static void AssertSameOldFields(
            RedeemResult expected,
            RedeemResult actual,
            string name)
        {
            Assert.AreEqual(expected.Context, actual.Context, name);
            Assert.AreEqual(expected.ContextValue, actual.ContextValue, name);
            Assert.AreEqual(expected.Signature, actual.Signature, name);
            Assert.AreEqual(expected.VerifiedAt, actual.VerifiedAt, name);
            Assert.AreEqual(
                expected.SecondsSinceVerified, actual.SecondsSinceVerified, name);
            Assert.AreEqual(expected.StatusCode, actual.StatusCode, name);
            if (expected.Factors is null)
            {
                Assert.IsNull(actual.Factors, name);
            }
            else
            {
                Assert.IsNotNull(actual.Factors, name);
                CollectionAssert.AreEquivalent(
                    expected.Factors.ToArray(), actual.Factors!.ToArray(), name);
            }
        }

        // ----------------------------------------------------------------
        // Today's members read the current answers as they read the old
        // ----------------------------------------------------------------

        /// <summary>
        /// Every member a caller reads today gives the same answer whether
        /// the cloud added <c>status</c>, <c>humanConfidence</c>,
        /// <c>humanConfidenceNullReason</c> and <c>obsolete</c> or not.
        /// </summary>
        [TestMethod]
        public void FromResponse_AddedFields_LeaveEveryOldMemberAsItWas()
        {
            foreach (var (name, status, before, now) in Answers)
            {
                AssertSameOldFields(
                    RedeemResult.FromResponse(status, before),
                    RedeemResult.FromResponse(status, now),
                    name);
            }
        }

        /// <summary>
        /// The obsolete <see cref="DidClient.RedeemAsync(FodId, string, string, System.Threading.CancellationToken)"/>
        /// reads every current answer as it reads the old one, and sends
        /// the current names beside <c>result</c> and <c>challenge</c>, the
        /// names every cloud release reads.
        /// </summary>
        [TestMethod]
        public async Task RedeemAsync_CurrentAnswers_ReadAsTheOldOnes()
        {
            using var client = NewClient();
            var fodId = Signed();
            foreach (var (name, status, before, now) in Answers)
            {
                _handler.Enqueue((HttpStatusCode)status, before);
                _handler.Enqueue((HttpStatusCode)status, now);

                var old = await client.RedeemAsync(fodId, "sealed", "abc");
                var current = await client.RedeemAsync(fodId, "sealed", "abc");

                AssertSameOldFields(old, current, name);
            }
            foreach (var request in _handler.Requests)
            {
                var form = HttpUtility.ParseQueryString(request.Body!);
                Assert.AreEqual("sealed", form["result"]);
                Assert.AreEqual("abc", form["challenge"]);
                Assert.AreEqual("sealed", form["sealedResult"]);
                Assert.AreEqual("abc", form["transaction"]);
            }
        }

        /// <summary>
        /// The four failure members of <see cref="ContextOutcome"/> are
        /// obsolete but keep their values, and are still read from
        /// <c>context</c>.
        /// </summary>
        [TestMethod]
        public void ContextOutcome_FailureMembers_KeepTheirValuesAndAreRead()
        {
            // The values are constants, and pinning them is the point.
#pragma warning disable MSTEST0032
            Assert.AreEqual(4, (int)ContextOutcome.Expired);
            Assert.AreEqual(5, (int)ContextOutcome.Replayed);
            Assert.AreEqual(6, (int)ContextOutcome.Unreadable);
            Assert.AreEqual(7, (int)ContextOutcome.Unconfirmed);
#pragma warning restore MSTEST0032
            Assert.AreEqual(ContextOutcome.Expired, RedeemResult.ParseContext("expired"));
            Assert.AreEqual(ContextOutcome.Replayed, RedeemResult.ParseContext("replayed"));
            Assert.AreEqual(ContextOutcome.Unreadable, RedeemResult.ParseContext("unreadable"));
            Assert.AreEqual(ContextOutcome.Unconfirmed, RedeemResult.ParseContext("unconfirmed"));
        }

        [TestMethod]
        public void Obsolete_MembersNameTheirReplacement()
        {
            foreach (var name in new[] { "Expired", "Replayed", "Unreadable", "Unconfirmed" })
            {
                var obsolete = typeof(ContextOutcome).GetField(name)!
                    .GetCustomAttribute<ObsoleteAttribute>();
                Assert.IsNotNull(obsolete, name);
                StringAssert.Contains(obsolete!.Message, "RedeemResult.Outcome");
                StringAssert.Contains(obsolete.Message, "RedemptionOutcome." + name);
                Assert.IsFalse(obsolete.IsError, name);
            }
            foreach (var good in new[] { "Verified", "Mismatch", "NoContext", "Misconfigured", "InvalidDate" })
            {
                Assert.IsNull(
                    typeof(ContextOutcome).GetField(good)!
                        .GetCustomAttribute<ObsoleteAttribute>(),
                    good);
            }
            var redeems = typeof(DidClient).GetMethods()
                .Where(m => m.Name == nameof(DidClient.RedeemAsync))
                .ToArray();
            Assert.AreEqual(2, redeems.Length);
            foreach (var redeem in redeems)
            {
                var obsolete = redeem.GetCustomAttribute<ObsoleteAttribute>();
                Assert.IsNotNull(obsolete);
                StringAssert.Contains(obsolete!.Message, nameof(DidClient.RedeemSealedResultAsync));
                StringAssert.Contains(obsolete.Message, "sealedResult");
                StringAssert.Contains(obsolete.Message, "transaction");
                Assert.IsFalse(obsolete.IsError);
            }
            Assert.IsTrue(typeof(DidClient).GetMethods()
                .Where(m => m.Name == nameof(DidClient.RedeemSealedResultAsync))
                .All(m => m.GetCustomAttribute<ObsoleteAttribute>() is null));
        }

        // ----------------------------------------------------------------
        // Redeem with the current names
        // ----------------------------------------------------------------

        /// <summary>
        /// The current names are sent, and each obsolete name with the same
        /// value, so a cloud release that reads only the obsolete names
        /// still redeems.
        /// </summary>
        [TestMethod]
        public async Task RedeemSealedResult_SendsSealedResultAndTransaction()
        {
            _handler.Enqueue(HttpStatusCode.OK, Answers[0].Now);
            using var client = NewClient();
            var fodId = Signed();

            var redeemed = await client.RedeemSealedResultAsync(
                fodId, "sealed", "txn-1");

            Assert.AreEqual(RedemptionOutcome.Success, redeemed.Outcome);
            var request = _handler.Requests.Single();
            Assert.AreEqual(HttpMethod.Post, request.Method);
            Assert.AreEqual(Endpoint + "id/redeem", request.Uri.AbsoluteUri);
            Assert.AreEqual("application/x-www-form-urlencoded", request.ContentType);
            Assert.AreEqual(DidClient.UserAgent, request.UserAgent);
            var form = HttpUtility.ParseQueryString(request.Body!);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "resource", "51did", "sealedResult", "transaction",
                    "result", "challenge", "license",
                },
                form.AllKeys);
            Assert.AreEqual(Resource, form["resource"]);
            Assert.AreEqual(fodId.AsBase64Url(), form["51did"]);
            Assert.AreEqual("sealed", form["sealedResult"]);
            Assert.AreEqual("txn-1", form["transaction"]);
            Assert.AreEqual("sealed", form["result"]);
            Assert.AreEqual("txn-1", form["challenge"]);
            Assert.AreEqual(Licence, form["license"]);
            Assert.IsFalse(request.Uri.AbsoluteUri.Contains(Licence));
        }

        [TestMethod]
        public async Task RedeemSealedResult_String_NoTransactionNoLicence()
        {
            _handler.Enqueue(HttpStatusCode.OK, Answers[0].Now);
            using var client = NewClient(null);
            var encoded = Signed().AsBase64Url();

            await client.RedeemSealedResultAsync(encoded, "sealed");

            var form = HttpUtility.ParseQueryString(_handler.Requests.Single().Body!);
            CollectionAssert.AreEquivalent(
                new[] { "resource", "51did", "sealedResult", "transaction", "result", "challenge" },
                form.AllKeys);
            Assert.AreEqual(encoded, form["51did"]);
            Assert.AreEqual(string.Empty, form["transaction"]);
            Assert.AreEqual(string.Empty, form["challenge"]);
        }

        [TestMethod]
        public async Task RedeemSealedResult_RefusesWhatRedeemRefuses()
        {
            using var client = NewClient();

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(
                () => client.RedeemSealedResultAsync((FodId)null!, "sealed"));
            var error = await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => client.RedeemSealedResultAsync("not a 51did", "sealed"));
            Assert.AreEqual("fodId", error.ParamName);
            Assert.AreEqual(0, _handler.Requests.Count);

            _handler.Enqueue(HttpStatusCode.NotFound, "", "text/plain");
            await Assert.ThrowsExactlyAsync<NotSupportedException>(
                () => client.RedeemSealedResultAsync(Signed(), "sealed"));

            _handler.Enqueue(HttpStatusCode.BadRequest,
                "{\"errors\":[\"Value for 51did is not a valid Base64-encoded 51Did.\"]}");
            await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => client.RedeemSealedResultAsync(Signed(), "sealed"));

            _handler.Enqueue(HttpStatusCode.Unauthorized, "{\"errors\":[\"no\"]}");
            var other = await Assert.ThrowsExactlyAsync<HttpRequestException>(
                () => client.RedeemSealedResultAsync(Signed(), "sealed"));
            Assert.AreEqual(HttpStatusCode.Unauthorized, other.StatusCode);
        }

        // ----------------------------------------------------------------
        // status
        // ----------------------------------------------------------------

        [TestMethod]
        public async Task Redeem_Status_IsReadIntoOutcome()
        {
            var expected = new[]
            {
                RedemptionOutcome.Success,
                RedemptionOutcome.Success,
                RedemptionOutcome.Success,
                RedemptionOutcome.Expired,
                RedemptionOutcome.Replayed,
                RedemptionOutcome.Unreadable,
                RedemptionOutcome.Unconfirmed,
            };
            using var client = NewClient();
            for (var i = 0; i < Answers.Length; i++)
            {
                _handler.Enqueue((HttpStatusCode)Answers[i].Status, Answers[i].Now);

                var redeemed = await client.RedeemSealedResultAsync(Signed(), "sealed");

                Assert.AreEqual(expected[i], redeemed.Outcome, Answers[i].Name);
                Assert.AreEqual(
                    RedeemResult.ToStatusValue(expected[i]),
                    redeemed.StatusValue,
                    Answers[i].Name);
                Assert.AreEqual(Answers[i].Status, redeemed.StatusCode, Answers[i].Name);
            }
        }

        /// <summary>
        /// A cloud release that predates <c>status</c> writes the word to
        /// <c>context</c>, so <see cref="RedeemResult.Outcome"/> reads the
        /// same from either release.
        /// </summary>
        [TestMethod]
        public void Outcome_WithoutStatus_IsTakenFromContext()
        {
            foreach (var (name, status, before, now) in Answers)
            {
                var old = RedeemResult.FromResponse(status, before);
                var current = RedeemResult.FromResponse(status, now);

                Assert.IsNull(old.StatusValue, name);
                Assert.AreEqual(current.Outcome, old.Outcome, name);
            }
        }

        [TestMethod]
        public void Outcome_UnknownStatus_IsUnreadableWithTheRawWord()
        {
            var redeemed = RedeemResult.FromResponse(200,
                "{\"signature\":\"verified\",\"context\":\"verified\","
                + "\"status\":\"something-new\"}");

            Assert.AreEqual(RedemptionOutcome.Unreadable, redeemed.Outcome);
            Assert.AreEqual("something-new", redeemed.StatusValue);
            // The verdict is still read as before.
            Assert.AreEqual(ContextOutcome.Verified, redeemed.Context);
        }

        [TestMethod]
        public void Outcome_NonJsonOrEmptyBody_IsUnreadable()
        {
            Assert.AreEqual(
                RedemptionOutcome.Unreadable,
                RedeemResult.FromResponse(200, "<html>").Outcome);
            Assert.AreEqual(
                RedemptionOutcome.Unreadable,
                RedeemResult.FromResponse(200, "{}").Outcome);
            Assert.AreEqual(
                RedemptionOutcome.Unreadable,
                RedeemResult.FromResponse(200, "[]").Outcome);
        }

        [TestMethod]
        public void Outcome_DefaultIsUnreadable()
        {
            // A constant, and pinning it is the point.
#pragma warning disable MSTEST0032
            Assert.AreEqual(RedemptionOutcome.Unreadable, default(RedemptionOutcome));
#pragma warning restore MSTEST0032
        }

        [TestMethod]
        public void Status_EveryWordRoundTripsInAnyCase()
        {
            foreach (RedemptionOutcome outcome in Enum.GetValues(typeof(RedemptionOutcome)))
            {
                var word = RedeemResult.ToStatusValue(outcome);
                Assert.AreEqual(outcome, RedeemResult.ParseStatus(word));
                Assert.AreEqual(outcome, RedeemResult.ParseStatus(word.ToUpperInvariant()));
            }
            CollectionAssert.AreEquivalent(
                new[] { "success", "expired", "replayed", "unconfirmed", "unreadable" },
                Enum.GetValues(typeof(RedemptionOutcome))
                    .Cast<RedemptionOutcome>()
                    .Select(RedeemResult.ToStatusValue)
                    .ToArray());
            Assert.AreEqual(RedemptionOutcome.Unreadable, RedeemResult.ParseStatus(null));
        }

        /// <summary>
        /// The constructor callers had before the new members still builds a
        /// result, taking the outcome from the context verdict.
        /// </summary>
        [TestMethod]
        public void Constructor_WithoutTheNewParts_TakesOutcomeFromContext()
        {
            var verified = new RedeemResult(
                ContextOutcome.Verified, "verified", SignatureOutcome.Verified,
                null, null, null, 200, "{}");
            var replayed = new RedeemResult(
                ContextOutcome.Replayed, "replayed", SignatureOutcome.Unknown,
                null, null, null, 200, "{}");

            Assert.AreEqual(RedemptionOutcome.Success, verified.Outcome);
            Assert.AreEqual(RedemptionOutcome.Replayed, replayed.Outcome);
            Assert.IsNull(verified.StatusValue);
            Assert.IsNull(verified.HumanConfidence);
            Assert.IsNull(verified.HumanConfidenceNullReason);
        }

        // ----------------------------------------------------------------
        // humanConfidence from redeem
        // ----------------------------------------------------------------

        [TestMethod]
        [DataRow("High", HumanConfidenceValue.High)]
        [DataRow("Medium", HumanConfidenceValue.Medium)]
        [DataRow("Low", HumanConfidenceValue.Low)]
        [DataRow("Absent", HumanConfidenceValue.Absent)]
        [DataRow("high", HumanConfidenceValue.High)]
        public async Task Redeem_HumanConfidence_IsRead(
            string word,
            HumanConfidenceValue expected)
        {
            _handler.Enqueue(HttpStatusCode.OK,
                "{\"signature\":\"verified\",\"context\":\"mismatch\"," + Factors
                + ",\"humanConfidence\":\"" + word + "\",\"status\":\"success\","
                + Times + "}");
            using var client = NewClient();

            var redeemed = await client.RedeemSealedResultAsync(Signed(), "sealed");

            Assert.AreEqual(expected, redeemed.HumanConfidence);
            Assert.IsNull(redeemed.HumanConfidenceNullReason);
            // The Value is there whatever the verdict is.
            Assert.AreEqual(ContextOutcome.Mismatch, redeemed.Context);
            Assert.AreEqual(9, redeemed.Factors!.Count);
        }

        [TestMethod]
        public async Task Redeem_WithoutAHumanConfidenceLicence_GivesTheReason()
        {
            _handler.Enqueue(HttpStatusCode.OK, Answers[2].Now);
            using var client = NewClient();

            var redeemed = await client.RedeemSealedResultAsync(Signed(), "sealed");

            Assert.IsNull(redeemed.HumanConfidence);
            Assert.AreEqual(NullReason, redeemed.HumanConfidenceNullReason);
            Assert.AreEqual(RedemptionOutcome.Success, redeemed.Outcome);
            Assert.AreEqual(SignatureOutcome.Invalid, redeemed.Signature);
        }

        [TestMethod]
        public void Redeem_HumanConfidence_MissingField_IsNull()
        {
            foreach (var (name, status, before, _) in Answers)
            {
                var old = RedeemResult.FromResponse(status, before);
                Assert.IsNull(old.HumanConfidence, name);
                Assert.IsNull(old.HumanConfidenceNullReason, name);
            }
            var jsonNull = RedeemResult.FromResponse(200,
                "{\"context\":\"verified\",\"humanConfidence\":null,"
                + "\"status\":\"success\"}");
            Assert.IsNull(jsonNull.HumanConfidence);
            var withReason = RedeemResult.FromResponse(200,
                "{\"context\":\"verified\",\"humanConfidenceNullReason\":\""
                + NullReason + "\",\"status\":\"success\"}");
            Assert.IsNull(withReason.HumanConfidence);
            Assert.AreEqual(NullReason, withReason.HumanConfidenceNullReason);
        }

        /// <summary>
        /// A <c>humanConfidence</c> field that is there with a word this
        /// client does not know reads as Absent, never as a missing field
        /// or as a Value, and the word stays in <see cref="RedeemResult.Raw"/>.
        /// </summary>
        [TestMethod]
        public void Redeem_HumanConfidence_UnknownWord_IsAbsent()
        {
            var unknown = RedeemResult.FromResponse(200,
                "{\"context\":\"verified\",\"humanConfidence\":\"Certain\","
                + "\"status\":\"success\"}");
            Assert.AreEqual(HumanConfidenceValue.Absent, unknown.HumanConfidence);
            Assert.IsNull(unknown.HumanConfidenceNullReason);
            StringAssert.Contains(unknown.Raw, "Certain");
            var notAString = RedeemResult.FromResponse(200,
                "{\"context\":\"verified\",\"humanConfidence\":3}");
            Assert.AreEqual(HumanConfidenceValue.Absent, notAString.HumanConfidence);
            var empty = RedeemResult.FromResponse(200,
                "{\"context\":\"verified\",\"humanConfidence\":\"\"}");
            Assert.AreEqual(HumanConfidenceValue.Absent, empty.HumanConfidence);
        }

        /// <summary>
        /// A 503 is the service saying it could not confirm first use, so it
        /// is Unconfirmed whatever the body says.
        /// </summary>
        [TestMethod]
        [DataRow("{\"context\":\"verified\",\"status\":\"success\"}")]
        [DataRow("{\"context\":\"verified\"}")]
        [DataRow("{\"context\":\"replayed\",\"status\":\"replayed\"}")]
        [DataRow("{\"status\":\"something-new\"}")]
        [DataRow("{}")]
        [DataRow("<html>busy</html>")]
        [DataRow("")]
        public void Redeem_503_IsUnconfirmedWhateverTheBodySays(string body)
        {
            var redeemed = RedeemResult.FromResponse(503, body);

            Assert.AreEqual(RedemptionOutcome.Unconfirmed, redeemed.Outcome);
            Assert.AreEqual(503, redeemed.StatusCode);
            Assert.AreEqual(body, redeemed.Raw);
        }

        [TestMethod]
        public async Task RedeemSealedResult_503WithASuccessBody_IsUnconfirmed()
        {
            _handler.Enqueue(HttpStatusCode.ServiceUnavailable, Answers[0].Now);
            using var client = NewClient();

            var redeemed = await client.RedeemSealedResultAsync(Signed(), "sealed");

            Assert.AreEqual(RedemptionOutcome.Unconfirmed, redeemed.Outcome);
            Assert.AreEqual(503, redeemed.StatusCode);
        }

        [TestMethod]
        public void Redeem_FailureOutcomes_CarryNoHumanConfidence()
        {
            foreach (var (name, status, _, now) in Answers.Skip(3))
            {
                var redeemed = RedeemResult.FromResponse(status, now);
                Assert.AreNotEqual(RedemptionOutcome.Success, redeemed.Outcome, name);
                Assert.IsNull(redeemed.HumanConfidence, name);
                Assert.IsNull(redeemed.HumanConfidenceNullReason, name);
            }
        }

        [TestMethod]
        public void Redeem_ObsoleteMessages_AreIgnoredAndKeptRaw()
        {
            var redeemed = RedeemResult.FromResponse(200, Answers[0].Now);

            Assert.AreEqual(RedemptionOutcome.Success, redeemed.Outcome);
            Assert.AreEqual(HumanConfidenceValue.High, redeemed.HumanConfidence);
            StringAssert.Contains(redeemed.Raw, "result is obsolete. Use sealedResult.");
        }

        // ----------------------------------------------------------------
        // humanConfidence from verify
        // ----------------------------------------------------------------

        [TestMethod]
        public async Task VerifyDetailed_WithLicence_PostsTheLicenceAndReadsTheValue()
        {
            _handler.Enqueue(HttpStatusCode.OK,
                "{\"valid\":true,\"humanConfidence\":\"Medium\"}");
            using var client = NewClient();
            var fodId = Signed();

            var verification = await client.VerifyDetailedAsync(fodId);

            Assert.IsTrue(verification.Valid);
            Assert.AreEqual(HumanConfidenceValue.Medium, verification.HumanConfidence);
            Assert.IsNull(verification.HumanConfidenceNullReason);
            Assert.AreEqual(200, verification.StatusCode);
            var request = _handler.Requests.Single();
            Assert.AreEqual(HttpMethod.Post, request.Method);
            Assert.AreEqual(Endpoint + "id/verify", request.Uri.AbsoluteUri);
            Assert.AreEqual("application/x-www-form-urlencoded", request.ContentType);
            Assert.AreEqual(DidClient.UserAgent, request.UserAgent);
            var form = HttpUtility.ParseQueryString(request.Body!);
            Assert.AreEqual(Resource, form["resource"]);
            Assert.AreEqual(fodId.AsBase64Url(), form["51did"]);
            Assert.AreEqual(fodId.AsBase64Url(), form["owid"]);
            Assert.AreEqual(Licence, form["license"]);
            Assert.IsFalse(request.Uri.AbsoluteUri.Contains(Licence));
        }

        [TestMethod]
        public async Task VerifyDetailed_WithoutLicence_MakesTheSameCallAsVerify()
        {
            _handler.Enqueue(HttpStatusCode.OK,
                "{\"valid\":true,\"humanConfidenceNullReason\":\"" + NullReason + "\"}");
            _handler.Enqueue(HttpStatusCode.OK,
                "{\"valid\":true,\"humanConfidenceNullReason\":\"" + NullReason + "\"}");
            using var client = NewClient(null);
            var encoded = Signed().AsBase64Url();

            var verification = await client.VerifyDetailedAsync(encoded);
            Assert.IsTrue(await client.VerifyAsync(encoded));

            Assert.IsTrue(verification.Valid);
            Assert.IsNull(verification.HumanConfidence);
            Assert.AreEqual(NullReason, verification.HumanConfidenceNullReason);
            Assert.AreEqual(HttpMethod.Get, _handler.Requests[0].Method);
            Assert.AreEqual(
                _handler.Requests[1].Uri.AbsoluteUri,
                _handler.Requests[0].Uri.AbsoluteUri);
        }

        /// <summary>
        /// <see cref="DidClient.VerifyAsync(FodId, System.Threading.CancellationToken)"/>
        /// reads the current verify answers as it reads the old ones, and
        /// its call does not change, licence key or not.
        /// </summary>
        [TestMethod]
        public async Task Verify_CurrentAnswers_ReadAsTheOldOnes()
        {
            _handler.Enqueue(HttpStatusCode.OK, "{\"valid\":true,\"humanConfidence\":\"High\"}");
            _handler.Enqueue(HttpStatusCode.OK,
                "{\"valid\":true,\"humanConfidenceNullReason\":\"" + NullReason + "\"}");
            _handler.Enqueue(HttpStatusCode.BadRequest, "{\"valid\":false}");
            using var client = NewClient();
            var fodId = Signed();

            Assert.IsTrue(await client.VerifyAsync(fodId));
            Assert.IsTrue(await client.VerifyAsync(fodId));
            Assert.IsFalse(await client.VerifyAsync(fodId));

            var url = fodId.AsBase64Url();
            foreach (var request in _handler.Requests)
            {
                Assert.AreEqual(HttpMethod.Get, request.Method);
                Assert.AreEqual(
                    Endpoint + "id/verify/" + Resource + "?51did=" + url + "&owid=" + url,
                    request.Uri.AbsoluteUri);
                Assert.IsNull(request.Body);
            }
        }

        [TestMethod]
        public async Task VerifyDetailed_InvalidSignature_HasNoValue()
        {
            _handler.Enqueue(HttpStatusCode.BadRequest, "{\"valid\":false}");
            using var client = NewClient();

            var verification = await client.VerifyDetailedAsync(Signed());

            Assert.IsFalse(verification.Valid);
            Assert.IsNull(verification.HumanConfidence);
            Assert.IsNull(verification.HumanConfidenceNullReason);
            Assert.AreEqual(400, verification.StatusCode);
            Assert.AreEqual("{\"valid\":false}", verification.Raw);
        }

        [TestMethod]
        public async Task VerifyDetailed_OldAnswer_HasNoValue()
        {
            _handler.Enqueue(HttpStatusCode.OK, "{\"valid\":true}");
            using var client = NewClient();

            var verification = await client.VerifyDetailedAsync(Signed());

            Assert.IsTrue(verification.Valid);
            Assert.IsNull(verification.HumanConfidence);
            Assert.IsNull(verification.HumanConfidenceNullReason);
        }

        /// <summary>
        /// A <c>humanConfidence</c> field that is there with a word this
        /// client does not know reads as Absent, apart from an answer with
        /// no field, which reads as null.
        /// </summary>
        [TestMethod]
        [DataRow("{\"valid\":true,\"humanConfidence\":\"Certain\"}")]
        [DataRow("{\"valid\":true,\"humanConfidence\":\"\"}")]
        [DataRow("{\"valid\":true,\"humanConfidence\":3}")]
        public async Task VerifyDetailed_UnknownWord_IsAbsent(string body)
        {
            _handler.Enqueue(HttpStatusCode.OK, body);
            using var client = NewClient();

            var verification = await client.VerifyDetailedAsync(Signed());

            Assert.IsTrue(verification.Valid);
            Assert.AreEqual(HumanConfidenceValue.Absent, verification.HumanConfidence);
            Assert.IsNull(verification.HumanConfidenceNullReason);
            Assert.AreEqual(body, verification.Raw);
        }

        [TestMethod]
        public async Task VerifyDetailed_NullField_IsNull()
        {
            _handler.Enqueue(HttpStatusCode.OK, "{\"valid\":true,\"humanConfidence\":null}");
            using var client = NewClient();

            var verification = await client.VerifyDetailedAsync(Signed());

            Assert.IsTrue(verification.Valid);
            Assert.IsNull(verification.HumanConfidence);
        }

        [TestMethod]
        public async Task VerifyDetailed_RefusesWhatVerifyRefuses()
        {
            using var client = NewClient();

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(
                () => client.VerifyDetailedAsync((FodId)null!));
            await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => client.VerifyDetailedAsync(" "));
            Assert.AreEqual(0, _handler.Requests.Count);

            _handler.Enqueue(HttpStatusCode.BadRequest,
                "{\"errors\":[\"The resource key does not include the 51Did properties.\"]}");
            var error = await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => client.VerifyDetailedAsync(Signed()));
            StringAssert.Contains(error.Message, "does not include the 51Did properties");

            _handler.Enqueue(HttpStatusCode.Forbidden, "{\"errors\":[\"no\"]}");
            var other = await Assert.ThrowsExactlyAsync<HttpRequestException>(
                () => client.VerifyDetailedAsync(Signed()));
            Assert.AreEqual(HttpStatusCode.Forbidden, other.StatusCode);
        }
    }
}
