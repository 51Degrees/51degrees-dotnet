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

using FiftyOne.DeviceDetection.RobotsTxt.FlowElements;
using FiftyOne.DeviceDetection.RobotsTxt.Model;
using FiftyOne.DeviceDetection.RobotsTxt.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

namespace FiftyOne.DeviceDetection.RobotsTxt.Tests
{
    /// <summary>
    /// Tests that a Terms Document Locator address is written exactly as
    /// it was given and that an entry is accepted or refused the same way
    /// on every operating system. Neither needs a data file.
    /// </summary>
    [TestClass]
    public class TdlAddressTests
    {
        private const string EscapedTdl =
            "https://example.com/Terms%20Of%20Use/1.txt";

        /// <summary>
        /// A resolver that knows one id and returns a fixed address.
        /// </summary>
        private sealed class FixedResolver : ITdlSourceResolver
        {
            private readonly string _id;

            public FixedResolver(string id)
            {
                _id = id;
            }

            public bool IsKnown(string id) =>
                string.Equals(id, _id, StringComparison.OrdinalIgnoreCase);

            public Uri Resolve(string id) =>
                IsKnown(id) ? new Uri("https://example.com/resolved") : null;
        }

        /// <summary>
        /// A TDL holding an escape is written unchanged, in the plain and
        /// the annotated text, because a space is not allowed in the path
        /// of an address.
        /// </summary>
        [DataRow(false)]
        [DataRow(true)]
        [TestMethod]
        public void EscapedTdlIsWrittenUnchanged(bool annotations)
        {
            var gen = new GeneratorService(new RobotsTxtModel
            {
                Usages = Array.Empty<UsageModel>(),
                Crawlers = Array.Empty<CrawlerModel>(),
            });
            var sb = new StringBuilder();
            using (var writer = new StringWriter(sb))
            {
                gen.Write(
                    writer,
                    new HashSet<string>(),
                    new[] { new Uri(EscapedTdl) },
                    annotations,
                    CancellationToken.None);
            }
            var text = sb.ToString().Replace("\r\n", "\n");

            Assert.Contains($"TDL: {EscapedTdl}\n", text);
            Assert.Contains($"# Terms {EscapedTdl}\n", text);
            Assert.DoesNotContain("Terms Of Use", text);
        }

        /// <summary>
        /// Only an http or https address is a TDL, so an entry that .NET
        /// reads as a file path on one system and refuses on another is
        /// dropped on all of them.
        /// </summary>
        [DataRow("/terms/1.txt")]
        [DataRow("/terms")]
        [DataRow("terms/1.txt")]
        [DataRow("file:///terms/1.txt")]
        [DataRow("ftp://example.com/terms/1.txt")]
        [DataRow("mailto:terms@example.com")]
        [DataRow("not a uri")]
        [TestMethod]
        public void EntryThatIsNotHttpOrHttpsIsDropped(string entry)
        {
            Assert.IsNull(
                ResolveEntry(entry, resolver: null),
                $"The entry '{entry}' should be dropped.");
        }

        /// <summary>
        /// An entry that is not an http or https address is passed to the
        /// resolver, which can still recognise it, on every system.
        /// </summary>
        [TestMethod]
        public void EntryThatIsNotHttpOrHttpsGoesToTheResolver()
        {
            var resolved = ResolveEntry(
                "/terms/1.txt",
                new FixedResolver("/terms/1.txt"));

            Assert.AreEqual(new Uri("https://example.com/resolved"), resolved);
        }

        /// <summary>
        /// An http or https address is kept, with its escapes.
        /// </summary>
        [DataRow("https://example.com/terms-v1")]
        [DataRow("http://example.com/terms-v1")]
        [DataRow(EscapedTdl)]
        [TestMethod]
        public void HttpAndHttpsEntriesAreKept(string entry)
        {
            Assert.AreEqual(
                entry,
                ResolveEntry(entry, resolver: null)?.AbsoluteUri);
        }

        private static Uri ResolveEntry(
            string entry,
            ITdlSourceResolver resolver)
        {
            var engine = new RobotsTxtEngine(
                null,
                NullLoggerFactory.Instance,
                resolver);
            var method = typeof(RobotsTxtEngine).GetMethod(
                "ResolveTdlEntry",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "ResolveTdlEntry should exist.");
            return (Uri)method.Invoke(engine, new object[] { entry });
        }
    }
}
