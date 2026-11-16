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
using System.IO;
using System.Reflection;

namespace FiftyOne.Pipeline.CloudRequestEngine.Examples.Tests;

/// <summary>
/// Runs the cloud engine example's Main as a script or a test runs it,
/// with the input redirected, and checks that it ends normally after
/// writing Find out more. Console.ReadKey throws when the input is
/// redirected, so the example must only wait for a key when there is a
/// keyboard. The test replaces the console's output, so it runs apart
/// from the other tests.
/// </summary>
[TestClass]
[DoNotParallelize]
public class CloudEngineRedirectedInputTests
{
    [TestMethod]
    public void CloudEngine_InputRedirected_Ends()
    {
        if (Console.IsInputRedirected == false)
        {
            Assert.Inconclusive(
                "The input of the test process is a keyboard, so the " +
                "example would wait for a key. Run the tests with the " +
                "input redirected, as CI does.");
        }
        // Main is private in the example, so it is found by name.
        var main = typeof(global::Examples.CloudEngine.Program).GetMethod(
            "Main",
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Static);
        Assert.IsNotNull(main, "The cloud engine example has no Main.");
        var console = Console.Out;
        using var output = new StringWriter();
        Console.SetOut(output);
        try
        {
            main.Invoke(
                null,
                BindingFlags.DoNotWrapExceptions,
                null,
                new object[] { Array.Empty<string>() },
                null);
        }
        finally
        {
            Console.SetOut(console);
        }
        var text = output.ToString();
        StringAssert.Contains(text, "Find out more");
        StringAssert.Contains(text, "Example complete.");
    }
}
