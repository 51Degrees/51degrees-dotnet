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
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.DeviceDetection.Example.Tests.Cloud;

/// <summary>
/// Checks what a hardware lookup example printed, being a heading for each
/// value it looks up with a line per matching device under it, or one line
/// saying the resource key does not include the lookup.
/// </summary>
internal static class HardwareLookupOutput
{
    /// <summary>
    /// The words the examples print when the resource key does not include
    /// the hardware lookup.
    /// </summary>
    private const string NoLookup =
        "does not include HardwarePlatform.Profiles";

    /// <summary>
    /// The start of each heading the examples print before a lookup's
    /// devices.
    /// </summary>
    private const string HeadingStart = "Which devices are associated";

    /// <summary>
    /// How the examples render a property that has no value.
    /// </summary>
    private const string NoValue = "No value";


    /// <summary>
    /// Fails unless every value the example looked up lists at least one
    /// device with a vendor and a model. A resource key without the lookup
    /// fails by naming the property it lacks.
    /// </summary>
    /// <param name="output">Everything the example printed.</param>
    /// <param name="keyVariable">
    /// The environment variable the resource key came from.
    /// </param>
    /// <param name="lookup">The lookup, as in "TAC lookup".</param>
    /// <param name="subject">
    /// What each value is, as the heading names it, as in "TAC".
    /// </param>
    /// <param name="values">The values the example looks up.</param>
    public static void AssertListsDevices(
        string output,
        string keyVariable,
        string lookup,
        string subject,
        params string[] values)
    {
        if (output.Contains(NoLookup, StringComparison.Ordinal))
        {
            Assert.Fail(
                $"The resource key in {keyVariable} does not include " +
                $"HardwarePlatform.Profiles, which the {lookup} needs." +
                Environment.NewLine + output);
        }
        var lines = Lines(output);
        foreach (var value in values)
        {
            var heading = $"{HeadingStart} with the {subject} '{value}'?";
            var listed = lines
                .SkipWhile(l => l != heading)
                .Skip(1)
                .TakeWhile(l => l.StartsWith('\t'));
            if (listed.Any(HasVendorAndModel) == false)
            {
                Assert.Fail(
                    "No device with a vendor and a model was listed for " +
                    $"the {subject} '{value}'." +
                    Environment.NewLine + output);
            }
        }
    }

    /// <summary>
    /// Fails unless the example printed, on a line of its own, the line it
    /// gives for a resource key without the lookup, carrying both links,
    /// and printed no lookup heading.
    /// </summary>
    /// <param name="output">Everything the example printed.</param>
    /// <param name="expectedLine">The example's own line.</param>
    public static void AssertSaysTheKeyLacksTheLookup(
        string output,
        string expectedLine)
    {
        var lines = Lines(output);
        Assert.IsTrue(lines.Contains(expectedLine),
            "The example should say on one line that the resource key " +
            "does not include the lookup." + Environment.NewLine + output);
        StringAssert.Contains(expectedLine, NoLookup);
        StringAssert.Contains(expectedLine,
            "https://51degrees.com/configure/QKyYH5XT?utm_source=code&" +
            "utm_medium=example&utm_campaign=51degrees-dotnet&");
        StringAssert.Contains(expectedLine,
            "https://51degrees.com/contact-us?utm_source=code&" +
            "utm_medium=example&utm_campaign=51degrees-dotnet&");
        Assert.IsFalse(
            lines.Any(l => l.StartsWith(HeadingStart, StringComparison.Ordinal)),
            "The example should look nothing up." +
            Environment.NewLine + output);
    }

    /// <summary>
    /// True when the line is a device line, being a tab, the vendor, the
    /// name and the model in the final parentheses, and neither the vendor
    /// nor the model is empty or rendered as having no value. The model
    /// may hold parentheses of its own.
    /// </summary>
    internal static bool HasVendorAndModel(string line)
    {
        if (line.StartsWith('\t') == false || line.EndsWith(')') == false)
        {
            return false;
        }
        var text = line.Substring(1);
        var open = OpeningParenthesis(text);
        if (open <= 0)
        {
            return false;
        }
        var vendor = text.Split(' ')[0];
        var model = text.Substring(open + 1, text.Length - open - 2);
        return IsValue(vendor) &&
            IsValue(model) &&
            text.StartsWith(NoValue, StringComparison.Ordinal) == false;
    }

    /// <summary>
    /// The position of the parenthesis that opens the one closing the text,
    /// or -1 when there is none.
    /// </summary>
    private static int OpeningParenthesis(string text)
    {
        var depth = 0;
        for (var i = text.Length - 1; i >= 0; i--)
        {
            if (text[i] == ')')
            {
                depth++;
            }
            else if (text[i] == '(' && --depth == 0)
            {
                return i;
            }
        }
        return -1;
    }

    private static bool IsValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) == false &&
            value.StartsWith(NoValue, StringComparison.Ordinal) == false;
    }

    private static IReadOnlyList<string> Lines(string output)
    {
        return output.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
    }
}
