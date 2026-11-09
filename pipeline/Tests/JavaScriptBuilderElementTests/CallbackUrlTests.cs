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

using FiftyOne.Common.TestHelpers;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Core.TypedMap;
using FiftyOne.Pipeline.JavaScriptBuilder.Data;
using FiftyOne.Pipeline.JavaScriptBuilder.FlowElement;
using FiftyOne.Pipeline.JavaScriptBuilder.TemplateData;
using FiftyOne.Pipeline.JsonBuilder.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Diagnostics;

namespace FiftyOne.Pipeline.JavaScript.Tests
{
    /// <summary>
    /// Checks the callback URL against the cases every implementation of the
    /// JavaScript builder shares, in the same order, so that each one refuses
    /// and writes the same URLs. Then checks that the element, and a subclass
    /// that builds its URL from the request, write only a URL that passes
    /// them, and write it so that it cannot end the string the script holds
    /// it in.
    /// </summary>
    /// <remarks>
    /// No browser is used. A script is rendered from mocked evidence and
    /// checked with <c>node --check</c>, so Node must be on the path, and
    /// each test that renders a script is inconclusive when it is not.
    /// </remarks>
    [TestClass]
    public class CallbackUrlTests
    {
        private const string Endpoint = "/51dpipeline/json";

        /// <summary>
        /// The start of the request the script makes with the callback URL.
        /// With no device detection in the pipeline the script uses
        /// XMLHttpRequest rather than fetch.
        /// </summary>
        private const string Request = "createCORSRequest('POST', '";

        private TestLoggerFactory _loggerFactory;

        [TestInitialize]
        public void Init()
        {
            _loggerFactory = new TestLoggerFactory();
        }

        private static CallbackUrl Url(string protocol, string host) =>
            CallbackUrl.Of(protocol, host, Endpoint);

        private static void AssertValid(
            string expected, CallbackUrl url, string given)
        {
            Assert.IsNotNull(url, $"{given} gave no URL");
            Assert.IsTrue(url.Valid, $"{given} was refused as {url.Url}");
            Assert.AreEqual(expected, url.Url, given);
        }

        private static void AssertInvalid(CallbackUrl url, string given)
        {
            Assert.IsNotNull(url, $"{given} gave no URL");
            Assert.IsFalse(url.Valid, $"{given} was accepted as {url.Url}");
        }

        [TestMethod]
        public void CallbackUrl_AddsASingleSlash()
        {
            AssertValid(
                "https://example.com/51dpipeline/json",
                CallbackUrl.Of("https", "example.com", "51dpipeline/json"),
                "an endpoint without a slash");
        }

        [TestMethod]
        public void CallbackUrl_KeepsASingleSlashOnTheEndpoint()
        {
            AssertValid(
                "https://example.com/51dpipeline/json",
                CallbackUrl.Of("https", "example.com", "/51dpipeline/json"),
                "an endpoint with a slash");
        }

        [TestMethod]
        public void CallbackUrl_CollapsesADoubleSlash()
        {
            AssertValid(
                "http://example.com/json",
                CallbackUrl.Of("http", "example.com/", "/json"),
                "a slash on both");
        }

        [TestMethod]
        public void CallbackUrl_MissingWhenAPartIsEmpty()
        {
            Assert.IsNull(CallbackUrl.Of("https", "", "/json"));
            Assert.IsNull(CallbackUrl.Of("", "example.com", "/json"));
            Assert.IsNull(CallbackUrl.Of("https", "example.com", ""));
            Assert.IsNull(CallbackUrl.Of("https", "  ", "/json"));
        }

        [TestMethod]
        [DataRow("EXAMPLE.com", "https://example.com/51dpipeline/json")]
        [DataRow("example.com:8080", "https://example.com:8080/51dpipeline/json")]
        [DataRow("example.com:65535", "https://example.com:65535/51dpipeline/json")]
        [DataRow("a_b.com", "https://a_b.com/51dpipeline/json")]
        [DataRow("-ab.com", "https://-ab.com/51dpipeline/json")]
        [DataRow("127.0.0.1:80", "https://127.0.0.1:80/51dpipeline/json")]
        [DataRow("example.com", "https://example.com/51dpipeline/json")]
        [DataRow("1.2.3.4", "https://1.2.3.4/51dpipeline/json")]
        [DataRow("1.2.3.4:8080", "https://1.2.3.4:8080/51dpipeline/json")]
        [DataRow("1.2.3.4/path", "https://1.2.3.4/path/51dpipeline/json")]
        [DataRow("0.0.0.0", "https://0.0.0.0/51dpipeline/json")]
        [DataRow("101.2.3.4", "https://101.2.3.4/51dpipeline/json")]
        [DataRow("example.co1", "https://example.co1/51dpipeline/json")]
        [DataRow("123.example.com", "https://123.example.com/51dpipeline/json")]
        [DataRow("[::1]", "https://[::1]/51dpipeline/json")]
        [DataRow("[::1]:8080", "https://[::1]:8080/51dpipeline/json")]
        [DataRow("example.com.", "https://example.com./51dpipeline/json")]
        [DataRow("example.com/path", "https://example.com/path/51dpipeline/json")]
        [DataRow("example.com/a b", "https://example.com/a%20b/51dpipeline/json")]
        [DataRow("example.com/a\"b", "https://example.com/a%22b/51dpipeline/json")]
        [DataRow("example.com/a%zzb", "https://example.com/a%25zzb/51dpipeline/json")]
        [DataRow("example.com/a%41b", "https://example.com/a%41b/51dpipeline/json")]
        [DataRow("example.com/\u00fc", "https://example.com/%C3%BC/51dpipeline/json")]
        [DataRow("example.com/a\nb", "https://example.com/a%0Ab/51dpipeline/json")]
        [DataRow("example.com/a\u2028b", "https://example.com/a%E2%80%A8b/51dpipeline/json")]
        public void CallbackUrl_AcceptsHost(string host, string expected)
        {
            AssertValid(expected, Url("https", host), $"host {host}");
        }

        [TestMethod]
        [DataRow("example.com:65536")]
        [DataRow("example.com:99999")]
        [DataRow("example.com:abc")]
        [DataRow("exa mple.com")]
        [DataRow("example.com');alert(1);//")]
        [DataRow("a\"b.com")]
        [DataRow("a'b.com")]
        [DataRow("a<b.com")]
        [DataRow("::1")]
        [DataRow("[::1")]
        [DataRow("[v1.x]")]
        [DataRow("[1.2.3.4]")]
        [DataRow("a@b@example.com")]
        [DataRow("a..b.com")]
        [DataRow("%41.com")]
        [DataRow("a\\b.com")]
        [DataRow("a`b.com")]
        [DataRow("a{b}.com")]
        [DataRow("a|b.com")]
        [DataRow("a^b.com")]
        [DataRow("a(b).com")]
        [DataRow("a;b.com")]
        [DataRow("a,b.com")]
        [DataRow("a!b.com")]
        [DataRow("a*b.com")]
        [DataRow("a+b.com")]
        [DataRow("a=b.com")]
        [DataRow("a$b.com")]
        [DataRow("a&b.com")]
        [DataRow("a~b.com")]
        [DataRow("a\tb.com")]
        [DataRow("a\nb.com")]
        [DataRow(" example.com")]
        [DataRow("example.com ")]
        [DataRow("example.com:8080');alert(1);//")]
        // User information, or an @ anywhere in the host value.
        [DataRow("user@example.com")]
        [DataRow("user:pw@example.com")]
        [DataRow("@example.com")]
        [DataRow(":@example.com")]
        [DataRow("user@example.com:8080")]
        [DataRow("user@[::1]")]
        [DataRow("user@example.com/path")]
        [DataRow("u%41@example.com")]
        [DataRow("u x@example.com")]
        [DataRow("u\tx@example.com")]
        [DataRow("u\"x@example.com")]
        [DataRow("u'x@example.com")]
        [DataRow("u\\x@example.com")]
        [DataRow("u\nx@example.com")]
        [DataRow("u\u0001x@example.com")]
        [DataRow("example.com/a@b")]
        [DataRow("user:12/ss@example.com")]
        [DataRow("user:pa/ss@example.com")]
        [DataRow("user:pa?ss@example.com")]
        [DataRow("exa mple.com/a@b")]
        // A query or fragment.
        [DataRow("example.com?x")]
        [DataRow("example.com#x")]
        [DataRow("example.com?")]
        [DataRow("example.com#")]
        [DataRow("example.com:8080?x")]
        // A port that is present but empty.
        [DataRow("example.com:")]
        [DataRow("example.com:/path")]
        [DataRow("[::1]:")]
        [DataRow("https://example.com")]
        [DataRow("http://example.com")]
        [DataRow("[::1]#x")]
        [DataRow("example.com/path?x")]
        [DataRow("example.com/path#x")]
        [DataRow("example.com?');alert(1);//")]
        [DataRow("example.com#');alert(1);//")]
        // A name whose last label is a number must be an IPv4 address.
        [DataRow("example.123")]
        [DataRow("example.0x1f")]
        [DataRow("example.0X")]
        [DataRow("1.2.3.999")]
        [DataRow("1.2.3")]
        [DataRow("127.1")]
        [DataRow("0x7f.0.0.1")]
        [DataRow("01.2.3.4")]
        [DataRow("example.123.")]
        [DataRow("1.2.3.4.")]
        [DataRow("1.2.3.4.5")]
        [DataRow("1")]
        [DataRow("300.1.1.1")]
        [DataRow("0x7f.1")]
        public void CallbackUrl_RefusesHost(string host)
        {
            AssertInvalid(Url("https", host), $"host {host}");
        }

        /// <summary>
        /// Inside the brackets only an IPv6 address, so a zone identifier is
        /// refused, and after them only a port.
        /// </summary>
        [TestMethod]
        [DataRow("[fe80::1%25]")]
        [DataRow("[fe80::1%25eth0]")]
        [DataRow("[fe80::1%25eth0]:8080")]
        [DataRow("[FE80::1%25ETH0]")]
        [DataRow("[fe80::1%251]")]
        [DataRow("[::1%25lo]")]
        [DataRow("[fe80::1%]")]
        [DataRow("[fe80::1%25eth0]/path")]
        [DataRow("[fe80::1%25'+alert(1)+']")]
        [DataRow("[fe80::1%eth0]")]
        [DataRow("[fe80::1%25eth%0]")]
        [DataRow("[::1]x")]
        [DataRow("[::1]80")]
        [DataRow("[::1]:80x")]
        [DataRow("[::1]]")]
        public void CallbackUrl_RefusesZoneOrTextAfterTheBracket(string host)
        {
            AssertInvalid(Url("https", host), $"host {host}");
        }

        /// <summary>
        /// A port goes by its value, so leading zeros are allowed, and a port
        /// too long to read is refused rather than read as small enough.
        /// </summary>
        [TestMethod]
        public void CallbackUrl_ChecksThePortByItsValue()
        {
            AssertValid(
                "https://example.com:00080/51dpipeline/json",
                Url("https", "example.com:00080"),
                "host example.com:00080");
            AssertValid(
                "https://example.com:0065535/51dpipeline/json",
                Url("https", "example.com:0065535"),
                "host example.com:0065535");
            foreach (var host in new[] {
                "example.com:0065536",
                "example.com:123456",
                "example.com:4294967296",
                "example.com:99999999999999999999" })
            {
                AssertInvalid(Url("https", host), $"host {host}");
            }
        }

        /// <summary>
        /// Each character rule 8 lists, after the host and in the endpoint, and a '%' without two hexadecimal digits
        /// after it, even at the end.
        /// </summary>
        [TestMethod]
        public void CallbackUrl_EncodesTheCharactersRule8Lists()
        {
            AssertValid(
                "https://example.com/%22%5E%60%7B%7C%7D/j%22%5E%60%7B%7C%7D",
                CallbackUrl.Of(
                    "https", "example.com/\"^`{|}", "/j\"^`{|}"),
                "rule 8 characters");
            AssertValid(
                "https://example.com/a%25/51dpipeline/json",
                Url("https", "example.com/a%"),
                "host example.com/a%");
            AssertValid(
                "https://example.com/a%254/51dpipeline/json",
                Url("https", "example.com/a%4"),
                "host example.com/a%4");
            AssertValid(
                "https://example.com/a%41/51dpipeline/json",
                Url("https", "example.com/a%41"),
                "host example.com/a%41");
            AssertValid(
                "https://example.com/json%25",
                CallbackUrl.Of("https", "example.com", "/json%"),
                "endpoint /json%");
        }

        /// <summary>
        /// The URL kept for the log shows <c>***</c> in place of everything
        /// in the host value before its last '@'.
        /// </summary>
        [TestMethod]
        [DataRow("user:secret@example.com", "https://***@example.com/51dpipeline/json")]
        [DataRow("a@b@example.com", "https://***@example.com/51dpipeline/json")]
        [DataRow("u@[::1]:80x", "https://***@[::1]:80x/51dpipeline/json")]
        [DataRow("user:pa/ss@example.com", "https://***@example.com/51dpipeline/json")]
        [DataRow("user:pa?ss@example.com", "https://***@example.com/51dpipeline/json")]
        [DataRow("exa mple.com/a@b", "https://***@b/51dpipeline/json")]
        [DataRow("@example.com", "https://***@example.com/51dpipeline/json")]
        public void CallbackUrl_RefusedUrlForTheLogHasNoUserInformation(
            string host, string expected)
        {
            var url = Url("https", host);
            AssertInvalid(url, $"host {host}");
            Assert.AreEqual(expected, url.Url, $"host {host}");
        }

        /// <summary>
        /// A configured protocol made only of white space is not empty, so
        /// it is refused when the element is built.
        /// </summary>
        [TestMethod]
        [DataRow(" ")]
        [DataRow("\t")]
        [DataRow("\u00a0")]
        public void ConfiguredProtocol_WhiteSpace_IsRefused(string protocol)
        {
            Assert.ThrowsExactly<PipelineConfigurationException>(() =>
                new JavaScriptBuilderElementBuilder(_loggerFactory)
                    .SetProtocol(protocol));
        }

        [TestMethod]
        public void CallbackUrl_InvalidUrlIsTheOneBuilt()
        {
            var url = Url("https", "exa mple.com");
            AssertInvalid(url, "host exa mple.com");
            Assert.AreEqual("https://exa mple.com/51dpipeline/json", url.Url);
        }

        [TestMethod]
        [DataRow("https")]
        [DataRow("http")]
        [DataRow("HtTp")]
        public void CallbackUrl_AcceptsScheme(string protocol)
        {
            var url = Url(protocol, "example.com");
            Assert.IsNotNull(url);
            Assert.IsTrue(url.Valid, $"protocol {protocol}");
        }

        [TestMethod]
        public void CallbackUrl_WritesTheSchemeInLowerCase()
        {
            AssertValid(
                "https://example.com/51dpipeline/json",
                Url("HTTPS", "example.com"),
                "protocol HTTPS");
        }

        /// <summary>
        /// Only http and https, ignoring the case of ASCII letters only, so a
        /// letter outside ASCII that a case-insensitive comparison can match
        /// to an ASCII one, such as the long s, is refused.
        /// </summary>
        [TestMethod]
        [DataRow("ftp")]
        [DataRow("h+t-p.s")]
        [DataRow("ws")]
        [DataRow("javascript")]
        [DataRow("httpx")]
        [DataRow("https ")]
        [DataRow("1http")]
        [DataRow("h t")]
        [DataRow("x');alert(1);//")]
        [DataRow("a://b")]
        [DataRow("http\u017f")]
        [DataRow("HTTP\u017f")]
        public void CallbackUrl_RefusesScheme(string protocol)
        {
            AssertInvalid(Url(protocol, "example.com"), $"protocol {protocol}");
        }

        /// <summary>
        /// A quote, a backslash or a line break never reaches the script,
        /// wherever in the URL it was given.
        /// </summary>
        [TestMethod]
        [DataRow("example.com/');alert(1);//")]
        [DataRow("example.com/a\"b")]
        [DataRow("example.com/a\\b")]
        [DataRow("example.com/</script>")]
        public void CallbackUrl_NothingGivenCanEndTheString(string host)
        {
            var url = Url("https", host);
            Assert.IsNotNull(url, $"host {host}");
            Assert.IsTrue(url.Valid, $"host {host} was refused");
            foreach (var c in new[] {
                '\'', '"', '\\', '<', '>', '\n', '\r', '\u2028', '\u2029' })
            {
                Assert.AreEqual(-1, url.Url.IndexOf(c),
                    $"host {host} gave {url.Url}");
            }
        }

        [TestMethod]
        public void CallbackUrl_EncodesTheEndpoint()
        {
            AssertValid(
                "https://example.com/json%27x",
                CallbackUrl.Of("https", "example.com", "/json'x"),
                "the endpoint /json'x");
        }

        /// <summary>
        /// A host must be ASCII, so an internationalized name is given as
        /// A-labels, and anything else outside ASCII is refused.
        /// </summary>
        [TestMethod]
        [DataRow("b\u00fccher.de")]
        [DataRow("a\u00a0b.com")]
        [DataRow("a\u200bb.com")]
        [DataRow("a\u2028b.com")]
        public void CallbackUrl_RefusesHostOutsideAscii(string host)
        {
            AssertInvalid(Url("https", host), $"host {host}");
        }

        /// <summary>
        /// Not one of the shared cases, because only some languages' strings
        /// can hold a lone surrogate. It has no UTF-8 encoding, so it is
        /// written as U+FFFD, as a browser writes it.
        /// </summary>
        [TestMethod]
        public void CallbackUrl_WritesALoneSurrogateAsTheReplacement()
        {
            AssertValid(
                "https://example.com/%EF%BF%BD/51dpipeline/json",
                Url("https", "example.com/" + (char)0xD800),
                "a lone surrogate");
        }

        /// <summary>
        /// The element writes the URL exactly as it was checked.
        /// </summary>
        [TestMethod]
        [DataRow("localhost", "https://localhost/json")]
        [DataRow("example.com:8080", "https://example.com:8080/json")]
        [DataRow("example.com:00080", "https://example.com:00080/json")]
        [DataRow("[::1]", "https://[::1]/json")]
        [DataRow("example.com/a b", "https://example.com/a%20b/json")]
        [DataRow("example.com/'", "https://example.com/%27/json")]
        [DataRow("example.com/');x=1;//", "https://example.com/%27);x=1;//json")]
        public void Element_WritesTheCheckedUrl(string host, string expected)
        {
            var script = Render(Element(), host);

            Assert.Contains($"{Request}{expected}')", script,
                "The URL is not rendered as expected.");
            AssertParses(script);
        }

        /// <summary>
        /// A URL that fails the rules is left out, so the script has no
        /// callback, and the error is logged once with the URL quoted.
        /// </summary>
        [TestMethod]
        [DataRow("exa mple.com", "\"https://exa mple.com/json\"")]
        [DataRow("a\u2028b.com", "\"https://a\\u2028b.com/json\"")]
        [DataRow("example.com:65536", "\"https://example.com:65536/json\"")]
        [DataRow("[fe80::1%25'+alert(1)+']", "\"https://[fe80::1%25'+alert(1)+']/json\"")]
        [DataRow("user:secret@example.com", "\"https://***@example.com/json\"")]
        [DataRow("a@b@example.com", "\"https://***@example.com/json\"")]
        [DataRow("u@[::1]:80x", "\"https://***@[::1]:80x/json\"")]
        [DataRow("user:pa/ss@example.com", "\"https://***@example.com/json\"")]
        [DataRow("user:pa?ss@example.com", "\"https://***@example.com/json\"")]
        [DataRow("exa mple.com/a@b", "\"https://***@b/json\"")]
        [DataRow("example.123", "\"https://example.123/json\"")]
        [DataRow("https://example.com", "\"https://https://example.com/json\"")]
        public void Element_RefusedHost_HasNoCallbackAndIsLoggedOnce(
            string host, string logged)
        {
            var element = Element();
            foreach (var request in new[] { 1, 2 })
            {
                var script = Render(element, host);
                Assert.DoesNotContain(Request, script,
                    $"Request {request} has a callback URL.");
                AssertParses(script);
            }
            var errors = Errors();
            Assert.AreEqual(1, errors.Count,
                string.Join(Environment.NewLine, errors));
            Assert.Contains(logged, errors[0], "The URL is not quoted.");
        }

        /// <summary>
        /// A protocol from the request that is neither http nor https gives
        /// a script with no callback, logged once like any other URL that is
        /// not valid. One in upper case is written in lower case.
        /// </summary>
        [TestMethod]
        public void Element_ProtocolFromTheRequest_IsChecked()
        {
            var element = Element();
            foreach (var protocol in new[] { "ftp", "http\u017f" })
            {
                Assert.DoesNotContain(Request,
                    Render(element, "example.com", protocol),
                    $"Protocol {protocol} gave a callback URL.");
            }
            var errors = Errors();
            Assert.AreEqual(1, errors.Count,
                string.Join(Environment.NewLine, errors));
            Assert.Contains("\"ftp://example.com/json\"", errors[0],
                "The URL is not quoted.");

            Assert.Contains($"{Request}https://example.com/json')",
                Render(element, "example.com", "HTTPS"),
                "The protocol is not written in lower case.");
        }

        /// <summary>
        /// A configured protocol is used for every request, so one the
        /// script cannot send its request with is refused when the element
        /// is built, whether through the builder or the constructor.
        /// </summary>
        [TestMethod]
        [DataRow("ftp")]
        [DataRow("ws")]
        [DataRow("javascript")]
        [DataRow("https ")]
        [DataRow("http\u017f")]
        [DataRow("HTTP\u017f")]
        public void ConfiguredProtocol_OtherThanHttpOrHttps_IsRefused(
            string protocol)
        {
            Assert.ThrowsExactly<PipelineConfigurationException>(() =>
                new JavaScriptBuilderElementBuilder(_loggerFactory)
                    .SetProtocol(protocol));
            Assert.ThrowsExactly<PipelineConfigurationException>(() =>
                new JavaScriptBuilderElement(
                    _loggerFactory.CreateLogger<JavaScriptBuilderElement>(),
                    CreateData, "/json", null, false, false, null, protocol));
        }

        [TestMethod]
        [DataRow("http", "http://example.com/json")]
        [DataRow("https", "https://example.com/json")]
        [DataRow("HTTPS", "https://example.com/json")]
        public void ConfiguredProtocol_HttpOrHttps_IsUsed(
            string protocol, string expected)
        {
            var element = new JavaScriptBuilderElementBuilder(_loggerFactory)
                .SetEndpoint("/json")
                .SetMinify(false)
                .SetProtocol(protocol)
                .Build();

            Assert.Contains($"{Request}{expected}')",
                Render(element, "example.com", "ftp"),
                "The configured protocol is not used.");
        }

        /// <summary>
        /// A subclass that builds its URL from the request through the
        /// overload that takes the protocol, host and endpoint gets the same
        /// checks as the element.
        /// </summary>
        [TestMethod]
        public void Subclass_UrlFromTheRequest_IsChecked()
        {
            var element = new RequestUrlElement(
                _loggerFactory.CreateLogger<JavaScriptBuilderElement>());

            var script = Render(element, "Example.com/'");
            Assert.Contains(
                $"{Request}https://example.com/%27/api/v4/json')", script,
                "The URL is not rendered as expected.");
            AssertParses(script);

            script = Render(element, "a'b.com");
            Assert.DoesNotContain(Request, script,
                "A refused URL reached the script.");
            AssertParses(script);
            Assert.AreEqual(1, Errors().Count, "The refusal is not logged.");
        }

        /// <summary>
        /// A subclass that passes its own <see cref="Uri"/> to
        /// BuildJavaScript is not checked, but cannot write a quote either,
        /// because the template data percent-encodes every character that
        /// could end the string.
        /// </summary>
        [TestMethod]
        [DataRow("https://example.com/'/json", "https://example.com/%27/json")]
        [DataRow("https://u'x@example.com/json", "https://u%27x@example.com/json")]
        [DataRow("https://localhost/json", "https://localhost/json")]
        public void TemplateData_AppliesRule(string url, string expected)
        {
            var viaString = new JavaScriptResource(
                "fod", "{}", "abc-123", 1, false, false,
                url, "{}", false, true, false)
                .AsDictionary();
            var viaUri = new JavaScriptResource(
                "fod", "{}", "abc-123", 1, false, false,
                new Uri(url), "{}", false, true, false)
                .AsDictionary();

            foreach (var values in new[] { viaString, viaUri })
            {
                Assert.AreEqual(expected, values["_url"]);
            }
        }

        /// <summary>
        /// With no URL the template data has none either.
        /// </summary>
        [TestMethod]
        public void TemplateData_NoUrl_IsNull()
        {
            var values = new JavaScriptResource(
                "fod", "{}", "abc-123", 1, false, false,
                (Uri)null, "{}", false, false, false)
                .AsDictionary();

            Assert.IsNull(values["_url"]);
        }

        /// <summary>
        /// Builds its callback URL from the request's host, as a service
        /// that serves many sites from one pipeline does.
        /// </summary>
        private sealed class RequestUrlElement : JavaScriptBuilderElement
        {
            public RequestUrlElement(ILogger<JavaScriptBuilderElement> logger)
                : base(logger, CreateData, "/json", null, false, false)
            {
            }

            protected override void ProcessInternal(IFlowData data)
            {
                data.TryGetEvidence(
                    JavaScriptBuilder.Constants.EVIDENCE_HOST_KEY,
                    out object host);
                BuildJavaScript(data, GetOrAddToData(data), "{}", null, 1,
                    false, false, "https", host?.ToString(), "/api/v4/json",
                    "{}");
            }
        }

        private static IJavaScriptBuilderElementData CreateData(
            IPipeline pipeline,
            FlowElementBase<IJavaScriptBuilderElementData,
                IElementPropertyMetaData> element)
        {
            return new JavaScriptBuilderElementData(
                new Mock<ILogger<JavaScriptBuilderElementData>>().Object,
                pipeline);
        }

        private JavaScriptBuilderElement Element()
        {
            return new JavaScriptBuilderElementBuilder(_loggerFactory)
                .SetEndpoint("/json")
                .SetMinify(false)
                .Build();
        }

        /// <summary>
        /// The errors logged so far by every logger the tests created.
        /// </summary>
        private List<string> Errors()
        {
            return _loggerFactory.Loggers
                .SelectMany(l => l.ErrorEntries)
                .ToList();
        }

        /// <summary>
        /// Fails unless <c>node --check</c> accepts the script.
        /// </summary>
        private static void AssertParses(string script)
        {
            var path = Path.Combine(
                Path.GetTempPath(),
                $"callbackurl-script-{Guid.NewGuid():N}.js");
            File.WriteAllText(path, script);
            try
            {
                var start = new ProcessStartInfo("node")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
                start.ArgumentList.Add("--check");
                start.ArgumentList.Add(path);
                Process process;
                try
                {
                    process = Process.Start(start);
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    Assert.Inconclusive("Node is not on the path.");
                    return;
                }
                using (process)
                {
                    process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    Assert.AreEqual(0, process.ExitCode,
                        $"The script does not parse. {error}");
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        private delegate bool TryGetDelegate(string key, out object value);

        /// <summary>
        /// Processes a mocked request whose host evidence is
        /// <paramref name="host"/>, and protocol evidence
        /// <paramref name="protocol"/> when one is given, and returns the
        /// rendered script, which is not minified so that the URL can be
        /// found in it.
        /// </summary>
        private static string Render(
            JavaScriptBuilderElement element,
            string host,
            string protocol = null)
        {
            var evidence = new Dictionary<string, object>
            {
                [JavaScriptBuilder.Constants.EVIDENCE_HOST_KEY] = host,
            };
            if (protocol != null)
            {
                evidence[Core.Constants.EVIDENCE_PROTOCOL] = protocol;
            }

            var flowData = new Mock<IFlowData>();
            flowData.Setup(d => d.TryGetEvidence(
                    It.IsAny<string>(), out It.Ref<object>.IsAny))
                .Returns(new TryGetDelegate((string key, out object value) =>
                    evidence.TryGetValue(key, out value)));
            flowData.Setup(d => d.Get<IJsonBuilderElementData>())
                .Returns(() => new JsonBuilderElementData(
                    new Mock<ILogger<JsonBuilderElementData>>().Object,
                    null)
                {
                    Json = "{\"device\":{\"ismobile\":true}}"
                });
            flowData.Setup(d => d.GetEvidence().AsDictionary())
                .Returns(evidence);

            IJavaScriptBuilderElementData data = null;
            flowData.Setup(d => d.GetOrAdd(
                    It.IsAny<ITypedKey<IJavaScriptBuilderElementData>>(),
                    It.IsAny<Func<IPipeline, IJavaScriptBuilderElementData>>()))
                .Returns<ITypedKey<IJavaScriptBuilderElementData>,
                    Func<IPipeline, IJavaScriptBuilderElementData>>((k, f) =>
                    {
                        data = f(null);
                        return data;
                    });

            element.Process(flowData.Object);

            Assert.IsNotNull(data, "No script was rendered.");
            return data.JavaScript;
        }
    }
}
