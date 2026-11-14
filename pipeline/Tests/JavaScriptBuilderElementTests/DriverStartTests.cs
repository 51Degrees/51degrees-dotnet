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
using OpenQA.Selenium;

namespace FiftyOne.Pipeline.JavaScript.Tests;

/// <summary>
/// The browser tests start Chrome again when it does not create a session,
/// and do not when the failure is of another kind. These tests need no
/// browser.
/// </summary>
[TestClass]
public class DriverStartTests
{
    private const string Started = "started";

    private const string SessionNotCreated =
        "session not created from disconnected: unable to connect to renderer";

    [TestMethod]
    public void StartWithRetry_SessionNotCreatedAtFirst_StartsOnTheLastAttempt()
    {
        var calls = 0;

        var result = JavaScriptBuilderElementTestsBase.StartWithRetry(() =>
        {
            calls++;
            if (calls < JavaScriptBuilderElementTestsBase.DriverStartAttempts)
            {
                throw new InvalidOperationException(SessionNotCreated);
            }
            return Started;
        });

        Assert.AreEqual(Started, result);
        Assert.AreEqual(
            JavaScriptBuilderElementTestsBase.DriverStartAttempts, calls);
    }

    /// <summary>
    /// Each further start is written as one line for the build log, with
    /// the first line of the reason.
    /// </summary>
    [TestMethod]
    public void StartWithRetry_SessionNotCreatedOnce_WritesOneLineForTheBuildLog()
    {
        var calls = 0;
        var written = new StringWriter();
        var console = Console.Out;
        Console.SetOut(written);
        try
        {
            JavaScriptBuilderElementTestsBase.StartWithRetry(() =>
            {
                calls++;
                if (calls == 1)
                {
                    throw new InvalidOperationException(
                        "session not created\nfrom disconnected");
                }
                return Started;
            });
        }
        finally
        {
            Console.SetOut(console);
        }

        Assert.Contains(
            "Build log: Chrome did not create a session on attempt 1 of " +
            JavaScriptBuilderElementTestsBase.DriverStartAttempts +
            ". session not created" + Environment.NewLine,
            written.ToString());
        Assert.DoesNotContain("from disconnected", written.ToString());
    }

    [TestMethod]
    public void StartWithRetry_SessionNeverCreated_ThrowsAfterTheLastAttempt()
    {
        var calls = 0;

        var thrown = Assert.ThrowsExactly<InvalidOperationException>(() =>
            JavaScriptBuilderElementTestsBase.StartWithRetry<string>(() =>
            {
                calls++;
                throw new InvalidOperationException(SessionNotCreated);
            }));

        Assert.AreEqual(SessionNotCreated, thrown.Message);
        Assert.AreEqual(
            JavaScriptBuilderElementTestsBase.DriverStartAttempts, calls);
    }

    [TestMethod]
    public void StartWithRetry_DriverNotInstalled_IsNotTriedAgain()
    {
        var calls = 0;

        Assert.ThrowsExactly<WebDriverException>(() =>
            JavaScriptBuilderElementTestsBase.StartWithRetry<string>(() =>
            {
                calls++;
                throw new WebDriverException("no driver");
            }));

        Assert.AreEqual(1, calls);
    }
}
