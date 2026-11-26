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
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FiftyOne.HumanConfidence.Examples.Tests;

/// <summary>
/// The example page decides whether a redeem gave the true Value with its
/// own <c>redeemed</c> function. The cloud now says so in <c>status</c> and
/// gives <c>humanConfidence</c> Medium on every failure, and earlier
/// releases said so in <c>result</c> and gave a Value only on success or
/// Low on expired and replayed. These run the page's function in Node on
/// both shapes, so a failure never shows as a redeemed Value. Where Node is
/// absent the test says so rather than passing on nothing.
/// </summary>
[TestClass]
public class PageRedeemedTests
{
    private const string Page =
        "pipeline/Examples/HumanConfidence/HumanConfidenceWeb/page.html";

    private static readonly Regex RedeemedFunction = new Regex(
        @"function redeemed\(answer\) \{.*?\n  \}",
        RegexOptions.Singleline);

    private static bool Redeemed(string answerJson)
    {
        var page = File.ReadAllText(Path.Combine(
            ExampleWithoutKey.RepositoryRoot(), Page));
        var function = RedeemedFunction.Match(page);
        Assert.IsTrue(function.Success,
            "The page has no redeemed(answer) function.");
        var script = function.Value + "\nprocess.stdout.write(String(" +
            "redeemed(JSON.parse(process.argv[1]))));";
        var start = new ProcessStartInfo("node")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("-e");
        start.ArgumentList.Add(script);
        start.ArgumentList.Add(answerJson);
        Process process;
        try
        {
            process = Process.Start(start);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Assert.Inconclusive("Node is needed to run the page's script.");
            throw;
        }
        using (process)
        {
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.AreEqual(0, process.ExitCode, error);
            return bool.Parse(output);
        }
    }

    [DataTestMethod]
    [DataRow("{\"humanConfidence\":\"High\",\"status\":\"success\",\"verifiedAt\":\"2026-11-26T13:00:00Z\",\"secondsSinceVerified\":1}", DisplayName = "Current success")]
    [DataRow("{\"humanConfidence\":\"Low\",\"verifiedAt\":\"2026-11-26T13:00:00Z\",\"secondsSinceVerified\":1}", DisplayName = "Earlier success")]
    public void Redeemed_Success_IsRedeemed(string answer)
    {
        Assert.IsTrue(Redeemed(answer));
    }

    [DataTestMethod]
    [DataRow("expired")]
    [DataRow("replayed")]
    [DataRow("unreadable")]
    [DataRow("unconfirmed")]
    public void Redeemed_CurrentFailure_WithMedium_IsNotRedeemed(string status)
    {
        Assert.IsFalse(Redeemed(JsonSerializer.Serialize(new
        {
            humanConfidence = "Medium",
            status,
        })));
    }

    [DataTestMethod]
    [DataRow("{\"humanConfidence\":\"Low\",\"result\":\"expired\",\"verifiedAt\":\"2026-11-26T13:00:00Z\",\"secondsSinceVerified\":12}", DisplayName = "Earlier expired")]
    [DataRow("{\"humanConfidence\":\"Low\",\"result\":\"replayed\"}", DisplayName = "Earlier replayed")]
    [DataRow("{\"result\":\"unreadable\"}", DisplayName = "Earlier unreadable")]
    [DataRow("{\"result\":\"unconfirmed\"}", DisplayName = "Earlier unconfirmed")]
    [DataRow("{\"error\":\"no cloud\"}", DisplayName = "Example error")]
    public void Redeemed_EarlierFailure_IsNotRedeemed(string answer)
    {
        Assert.IsFalse(Redeemed(answer));
    }
}
