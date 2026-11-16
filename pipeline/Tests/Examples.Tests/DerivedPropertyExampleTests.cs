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
using System.Linq;
using Ex = Examples;

namespace FiftyOne.Pipeline.Examples.Tests
{
    /// <summary>
    /// The derived property example shows what the required examples page
    /// asks of it: a script held as text, a script read from a file, the
    /// Pipeline built from a configuration file, and a request missing a
    /// source property answered with no value and a message naming it.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class DerivedPropertyExampleTests
    {
        private const string FromConfiguration =
            "1. Configured from derived-property-options.json";
        private const string FromCode =
            "2. Configured with the builder in code";
        private const string NoValue = "no value, because";

        [TestMethod]
        public void DerivedPropertyExample_ShowsTheFourCases()
        {
            var output = Run();

            var configuration = Between(output, FromConfiguration, FromCode);
            var code = Between(output, FromCode, "Find out more");

            // The configuration file names the script file.
            AssertValueThenNone(configuration, "derived.ReaderEngagement ");
            // The builder in code adds the same file and a script held as
            // text.
            AssertValueThenNone(code, "derived.ReaderEngagement ");
            AssertValueThenNone(code, "derived.ReaderEngagementIsHigh ");
        }

        // The first request prints a value for the property and the second,
        // which leaves session.PointerMoved out, prints no value with a
        // message naming the property that was missing.
        private static void AssertValueThenNone(string section, string name)
        {
            var lines = section
                .Split(new[] { '\n' }, StringSplitOptions.None)
                .Select(l => l.Trim())
                .Where(l => l.StartsWith(name.Trim() + " "))
                .ToList();
            Assert.AreEqual(2, lines.Count,
                $"Expected '{name.Trim()}' for two requests in:\n{section}");
            Assert.IsFalse(lines[0].Contains(NoValue),
                $"The request with every source property has no value: " +
                lines[0]);
            Assert.IsTrue(lines[1].Contains(NoValue),
                $"The request missing a source property has a value: " +
                lines[1]);
            Assert.IsTrue(
                lines[1].IndexOf(
                    "PointerMoved", StringComparison.OrdinalIgnoreCase) >= 0,
                $"The message does not name the missing property: " +
                lines[1]);
        }

        private static string Between(string text, string start, string end)
        {
            var from = text.IndexOf(start, StringComparison.Ordinal);
            Assert.IsTrue(from >= 0, $"'{start}' is not in the output.");
            var to = text.IndexOf(end, from, StringComparison.Ordinal);
            Assert.IsTrue(to > from, $"'{end}' does not follow '{start}'.");
            return text.Substring(from, to - from);
        }

        private static string Run()
        {
            var original = Console.Out;
            using (var writer = new StringWriter())
            {
                Console.SetOut(writer);
                try
                {
                    new Ex.DerivedProperty.Program().RunExample();
                }
                finally
                {
                    Console.SetOut(original);
                }
                return writer.ToString().Replace("\r\n", "\n");
            }
        }
    }
}
