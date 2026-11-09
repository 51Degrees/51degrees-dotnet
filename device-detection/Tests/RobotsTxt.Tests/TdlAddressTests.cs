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
            var text = WriteTdl(EscapedTdl, annotations);

            Assert.Contains($"TDL: {EscapedTdl}\n", text);
            Assert.Contains($"# Terms {EscapedTdl}\n", text);
            Assert.DoesNotContain("Terms Of Use", text);
        }

        /// <summary>
        /// A TDL must be a URI (RFC 3986 section 3), which begins with a
        /// scheme. An entry without one is dropped on every system, even
        /// where .NET reads it as a file path.
        /// </summary>
        [DataRow("/terms/1.txt")]
        [DataRow("/terms")]
        [DataRow("terms/1.txt")]
        [DataRow("not a uri")]
        [TestMethod]
        public void EntryWithoutSchemeIsDropped(string entry)
        {
            Assert.IsNull(
                ResolveEntry(entry, resolver: null),
                $"The entry '{entry}' should be dropped.");
        }

        /// <summary>
        /// A TDL may hold only the characters RFC 3986 sections 2.1 to 2.3
        /// allow, with every '%' followed by two hex digits. Any other entry
        /// is dropped even where .NET would escape it and accept it.
        /// </summary>
        [DataRow("C:\\terms\\1.txt")]
        [DataRow("https://example.com/a b")]
        [DataRow("https://example.com/a\\b")]
        [DataRow("https://example.com/a%2")]
        [DataRow("https://example.com/a%zz")]
        [DataRow("https://example.com/\u00e9")]
        [DataRow("\\\\server\\share\\1.txt")]
        [TestMethod]
        public void EntryWithCharactersNotAllowedInAUriIsDropped(string entry)
        {
            Assert.IsNull(
                ResolveEntry(entry, resolver: null),
                $"The entry '{entry}' should be dropped.");
        }

        /// <summary>
        /// An entry that breaks the RFC 3986 Appendix A grammar is dropped
        /// even where every character is allowed on its own.
        /// </summary>
        [DataRow(":foo")]
        [DataRow("1http://a/b")]
        [DataRow("https://a/b#c#d")]
        [DataRow("https://a/b[c]")]
        [DataRow("https://a[b]/x")]
        [DataRow("https://a/b%")]
        [DataRow("https://[::1/x")]
        [DataRow("https://[1:2]/x")]
        [DataRow("https://[::1::2]/x")]
        [DataRow("https://[g::1]/x")]
        [DataRow("https://a:b/x")]
        [DataRow("https://a@b@c/x")]
        [TestMethod]
        public void EntryThatBreaksTheUriGrammarIsDropped(string entry)
        {
            Assert.IsNull(
                ResolveEntry(entry, resolver: null),
                $"The entry '{entry}' should be dropped.");
        }

        /// <summary>
        /// An entry that is not a URI is passed to the resolver, which can
        /// still recognise it, on every system.
        /// </summary>
        [TestMethod]
        public void EntryThatIsNotAUriGoesToTheResolver()
        {
            var resolved = ResolveEntry(
                "/terms/1.txt",
                new FixedResolver("/terms/1.txt"));

            Assert.AreEqual("https://example.com/resolved", resolved);
        }

        /// <summary>
        /// An entry that conforms to RFC 3986 section 3 and Appendix A is
        /// kept exactly as given, whatever the scheme.
        /// </summary>
        [DataRow("https://example.com/terms-v1")]
        [DataRow("http://example.com/terms-v1")]
        [DataRow(EscapedTdl)]
        [DataRow("ftp://example.com/terms/1.txt")]
        [DataRow("mailto:terms@example.com")]
        [DataRow("file:///terms/1.txt")]
        [DataRow("http:")]
        [DataRow("file:/t/1")]
        // RFC 3986 section 3.1 allows a one letter scheme, so this is a URI.
        [DataRow("c:/terms/1.txt")]
        [DataRow("HTTPS://Example.COM/T")]
        [DataRow("https://[2001:db8::1]/t")]
        [DataRow("https://[::ffff:1.2.3.4]:8080/t")]
        [DataRow("https://[v1.a:b]/t")]
        [DataRow("https://user:pw@example.com:443/t")]
        [DataRow("https://example.com/a?b=c&d=e")]
        [DataRow("https://example.com/a#frag")]
        [DataRow("https://example.com/a?q#frag")]
        [DataRow("urn:isbn:0451450523")]
        [TestMethod]
        public void EntryWithSchemeIsKept(string entry)
        {
            Assert.AreEqual(entry, ResolveEntry(entry, resolver: null));
        }

        /// <summary>
        /// White space around an entry is not part of the TDL.
        /// </summary>
        [TestMethod]
        public void EntryIsTrimmed()
        {
            Assert.AreEqual(
                "https://example.com/t",
                ResolveEntry(" https://example.com/t ", resolver: null));
        }

        /// <summary>
        /// A TDL is written exactly as given, because the document it
        /// names must not change. None of these values is rewritten to a
        /// normal form.
        /// </summary>
        [DataRow("https://example.com")]
        [DataRow("HTTPS://Example.COM/T")]
        [DataRow("https://a:443/x")]
        [DataRow("https://a/b/../c")]
        [DataRow("https://a/%7Eb")]
        [TestMethod]
        public void TdlIsWrittenExactlyAsGiven(string tdl)
        {
            var text = WriteTdl(tdl, annotations: true);

            Assert.Contains($"# Terms {tdl}\n", text);
            Assert.Contains($"TDL: {tdl}\n", text);
        }

        private static string WriteTdl(string tdl, bool annotations)
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
                    new[] { new Uri(tdl) },
                    annotations,
                    CancellationToken.None);
            }
            return sb.ToString().Replace("\r\n", "\n");
        }

        private static string ResolveEntry(
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
            var result = method.Invoke(engine, new object[] { entry });
            return result is Uri uri ? uri.OriginalString : (string)result;
        }
    }
}
