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
    /// Checks that the callback URL reaches the client script only in a
    /// form that cannot end the string the script holds it in. The host
    /// usually comes from the request, and a URL may carry a quote in its
    /// user information, path, query and fragment, so every character that
    /// could end the string or the script is percent-encoded.
    /// </summary>
    /// <remarks>
    /// No browser is used. The element is given the host as evidence, which
    /// is what reaches the script when no host is configured. The rendered
    /// script is checked with <c>node --check</c>, so Node must be on the
    /// path, and each test that renders a script is inconclusive when it is
    /// not.
    /// </remarks>
    [TestClass]
    public class CallbackUrlTests
    {
        private TestLoggerFactory _loggerFactory;

        [TestInitialize]
        public void Init()
        {
            _loggerFactory = new TestLoggerFactory();
        }

        /// <summary>
        /// A URL with no character that could end the string is written as
        /// it was before.
        /// </summary>
        [DataTestMethod]
        [DataRow("localhost", "https://localhost/json")]
        [DataRow("example.com:8080", "https://example.com:8080/json")]
        [DataRow("[::1]", "https://[::1]/json")]
        [DataRow("example.com/a b", "https://example.com/a%20b/json")]
        public void Url_Ordinary_IsUnchanged(string host, string expected)
        {
            var script = Render(host);

            AssertUrl(script, expected);
            AssertParses(script);
        }

        /// <summary>
        /// A quote after the host name, or in a user part, is written as
        /// %27, so it cannot end the string, and the script still parses.
        /// </summary>
        [DataTestMethod]
        [DataRow("example.com/'", "https://example.com/%27/json")]
        [DataRow("example.com?'", "https://example.com/?%27/json")]
        [DataRow("example.com#'", "https://example.com/#%27/json")]
        [DataRow("u'x@example.com", "https://u%27x@example.com/json")]
        [DataRow("example.com/');x=1;//", "https://example.com/%27);x=1;//json")]
        public void Url_Quote_IsEncoded(string host, string expected)
        {
            var script = Render(host);

            AssertUrl(script, expected);
            Assert.DoesNotContain(host, script,
                "The host was written into the script as it is.");
            AssertParses(script);
        }

        /// <summary>
        /// A line separator in the host, which <see cref="Uri"/> accepts,
        /// is written percent-encoded, because a JavaScript engine that
        /// predates ECMAScript 2019 ends the string there.
        /// </summary>
        [TestMethod]
        public void Url_LineSeparator_IsEncoded()
        {
            var script = Render("a\u2028b.com");

            AssertUrl(script, "https://a%E2%80%A8b.com/json");
            Assert.DoesNotContain("\u2028", script,
                "A line separator was written into the script.");
            AssertParses(script);
        }

        /// <summary>
        /// A subclass that passes its own URL to BuildJavaScript cannot
        /// write a quote either, because the template data applies the
        /// same rule.
        /// </summary>
        [DataTestMethod]
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
        /// Checks the URL in the request the script makes when it has new
        /// evidence. With no device detection in the pipeline the script
        /// uses XMLHttpRequest rather than fetch.
        /// </summary>
        private static void AssertUrl(string script, string expected)
        {
            Assert.Contains(
                $"createCORSRequest('POST', '{expected}')",
                script,
                "The URL is not rendered as expected.");
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
        /// <paramref name="host"/> and returns the rendered script, which is
        /// not minified so that the URL can be found in it.
        /// </summary>
        private string Render(string host)
        {
            var element = new JavaScriptBuilderElementBuilder(_loggerFactory)
                .SetEndpoint("/json")
                .SetMinify(false)
                .Build();

            var evidence = new Dictionary<string, object>
            {
                [JavaScriptBuilder.Constants.EVIDENCE_HOST_KEY] = host,
            };

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
