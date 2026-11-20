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

using FiftyOne.Pipeline.AgentSignature.Keys;
using FiftyOne.Pipeline.AgentSignature.Parsing;
using FiftyOne.Pipeline.AgentSignature.Tests.Helpers;
using FiftyOne.Pipeline.AgentSignature.Verification;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Net;

namespace FiftyOne.Pipeline.AgentSignature.Tests;

/// <summary>
/// Rules of the agent signature element's specification that the element
/// meets and that no other test would fail on if it stopped meeting them.
/// </summary>
[TestClass]
public class SpecificationRulesTests
{
    private const string DataUriPrefix =
        "data:" + Constants.DIRECTORY_MEDIA_TYPE + ",";

    /// <summary>
    /// Property names are matched without regard to case.
    /// </summary>
    [TestMethod]
    public void PropertyNames_MatchedWithoutRegardToCase()
    {
        using var harness = ElementHarness.Create();
        var data = harness.ProcessPlainRequest();
        var status = data.AgentSignature.Value;

        foreach (var name in new[]
        {
            Constants.PROPERTY_STATUS.ToUpperInvariant(),
            "AgentSignature",
            "aGENTsIGNATURE",
        })
        {
            Assert.AreEqual(
                status,
                ((IAspectPropertyValue<string>)data[name]).Value,
                $"Reading '{name}' did not give the status.");
        }
    }

    /// <summary>
    /// A member of type directory is a bare origin, with at most the path
    /// '/' and with no query, fragment or user information.
    /// </summary>
    [DataTestMethod]
    [DataRow("sig1=\"https://agent.example/keys\"")]
    [DataRow("sig1=\"https://agent.example/?a=1\"")]
    [DataRow("sig1=\"https://agent.example/#x\"")]
    [DataRow("sig1=\"https://user@agent.example/\"")]
    public void DirectoryMember_NotABareOrigin_Refused(string header)
    {
        Assert.IsFalse(
            SignatureAgentEntry.TryParse(header, false, out _),
            $"The header {header} was accepted.");
    }

    /// <summary>
    /// A bare origin, with or without the path '/', is accepted, so the
    /// refusals above are for what they name and not for every member.
    /// </summary>
    [DataTestMethod]
    [DataRow("sig1=\"https://agent.example\"")]
    [DataRow("sig1=\"https://agent.example/\"")]
    public void DirectoryMember_BareOrigin_Accepted(string header)
    {
        Assert.IsTrue(
            SignatureAgentEntry.TryParse(header, false, out var entries),
            $"The header {header} was refused.");
        Assert.AreEqual(1, entries.Count);
    }

    /// <summary>
    /// The scheme is https, and the sole exception is a data URI of type
    /// directory.
    /// </summary>
    [DataTestMethod]
    [DataRow("sig1=\"http://agent.example\"")]
    [DataRow("sig1=\"http://agent.example/jwks.json\";type=\"jwks_uri\"")]
    [DataRow("sig1=\"http://agent.example/card.json\";type=\"cimd\"")]
    [DataRow("sig1=\"ftp://agent.example\"")]
    [DataRow("sig1=\"" + DataUriPrefix + "%7B%7D\";type=\"jwks_uri\"")]
    [DataRow("sig1=\"" + DataUriPrefix + "%7B%7D\";type=\"cimd\"")]
    public void Member_NotHttpsOrDirectoryData_Refused(string header)
    {
        Assert.IsFalse(
            SignatureAgentEntry.TryParse(header, false, out _),
            $"The header {header} was accepted.");
    }

    /// <summary>
    /// A data URI states the directory media type, so one whose media type
    /// is implied, which means text/plain, or is another type is refused,
    /// and one that states the directory media type is read.
    /// </summary>
    [DataTestMethod]
    [DataRow("sig1=\"data:,%7B%7D\"", false)]
    [DataRow("sig1=\"data:text/plain,%7B%7D\"", false)]
    [DataRow("sig1=\"data:application/json,%7B%7D\"", false)]
    [DataRow("sig1=\"" + DataUriPrefix + "%7B%7D\"", true)]
    public void DataMember_MediaTypeStated(string header, bool accepted)
    {
        var parsed = SignatureAgentEntry.TryParse(
            header, false, out var entries);

        Assert.AreEqual(accepted, parsed,
            $"The header {header} was {(parsed ? "accepted" : "refused")}.");
        if (accepted)
        {
            Assert.IsNotNull(entries[0].InlineDirectory);
        }
    }

    /// <summary>
    /// A component identifier that is not a string makes the signature
    /// base impossible to build, so the signature cannot be verified.
    /// </summary>
    [DataTestMethod]
    [DataRow("1")]
    [DataRow("?1")]
    [DataRow("token")]
    public void ComponentNotAString_SignatureBaseNotBuilt(string item)
    {
        Assert.IsTrue(StructuredFieldParser.TryParseItem(item, out var sf));

        var built = SignatureBase.TryBuild(
            new List<SfItem>() { sf },
            "(1);created=1",
            null,
            out var text);

        Assert.IsFalse(built,
            $"A signature base was built with the component {item}.");
        Assert.IsNull(text);
    }

    /// <summary>
    /// A key directory response is read only when its status is 200, so a
    /// valid directory served with another success status leaves the
    /// directory unavailable.
    /// </summary>
    [DataTestMethod]
    [DataRow(HttpStatusCode.NonAuthoritativeInformation)]
    [DataRow(HttpStatusCode.PartialContent)]
    public void DirectoryResponse_StatusNot200_NotRead(HttpStatusCode status)
    {
        using var harness = ElementHarness.Create();
        harness.Handler.Add(
            Fixtures.SignatureAgentDirectoryUrl,
            new FakeResponse
            {
                StatusCode = status,
                Body = DirectoryBody(),
                MediaType = Constants.DIRECTORY_MEDIA_TYPE,
            });

        var result = harness.ProcessSigned(
            RequestSigner.Sign(new SigningOptions()));

        Assert.AreNotEqual(
            Constants.STATUS_VERIFIED,
            result.AgentSignature.Value,
            $"A directory served with status {(int)status} was read.");
        Assert.AreEqual(
            Constants.REASON_DIRECTORY_UNAVAILABLE,
            result.AgentSignatureReason.Value);
    }

    /// <summary>
    /// A key directory is an object with a 'keys' array holding at least
    /// one key it can read.
    /// </summary>
    [DataTestMethod]
    [DataRow("{}")]
    [DataRow("{\"keys\":[]}")]
    [DataRow("{\"keys\":{}}")]
    [DataRow("{\"keys\":[1,\"a\"]}")]
    [DataRow("[]")]
    public void Directory_WithoutAKey_Refused(string json)
    {
        Assert.IsFalse(
            KeyDirectory.TryParse(json, out _),
            $"The directory {json} was read.");
    }

    /// <summary>
    /// A directory holding a key is read, so the refusals above are for
    /// what they name.
    /// </summary>
    [TestMethod]
    public void Directory_WithAKey_Read()
    {
        Assert.IsTrue(KeyDirectory.TryParse(DirectoryBody(), out _));
    }

    private static string DirectoryBody()
    {
        return "{\"keys\":[" +
            RequestSigner.PublicPart(Fixtures.Ed25519Key()) + "]}";
    }
}
