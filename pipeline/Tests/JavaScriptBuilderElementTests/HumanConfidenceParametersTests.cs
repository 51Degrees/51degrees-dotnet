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
using FiftyOne.Pipeline.JsonBuilder.Data;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.JavaScript.Tests
{
    /// <summary>
    /// The client script sends back every query parameter its own request
    /// carried, so a page that loads it with its Human Confidence page id
    /// sends the page id on each of its calls. A sealed Human Confidence
    /// result is never written into the script.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/human-confidence.md#what-a-package-never-exposes">Specification</see>
    /// </summary>
    [TestClass]
    public class HumanConfidenceParametersTests
    {
        private const string PageId = "0123456789abcdef0123456789abcdef";
        private const string SealedResult = "SEALEDresultQWERTY123456";

        private delegate bool TryGetDelegate(string key, out object value);

        private delegate bool TryGetElementDataDelegate(
            string key, out IElementData value);

        [TestMethod]
        public void PageId_IsSentBackByTheScript()
        {
            var script = Render(new Dictionary<string, object>
            {
                ["query.humanconfidencepage"] = PageId,
            });

            StringAssert.Contains(script, PageId);
            StringAssert.Contains(script, "humanconfidencepage");
        }

        [TestMethod]
        [DataRow("query.humanconfidence")]
        [DataRow("query.HumanConfidence")]
        public void SealedResult_IsNeverWrittenIntoTheScript(string key)
        {
            var script = Render(new Dictionary<string, object>
            {
                ["query.humanconfidencepage"] = PageId,
                [key] = SealedResult,
            });

            StringAssert.Contains(script, PageId);
            Assert.DoesNotContain(SealedResult, script);
        }

        /// <summary>
        /// Processes a mocked request with the query Evidence given and
        /// returns the rendered script, not minified.
        /// </summary>
        private static string Render(Dictionary<string, object> query)
        {
            var element = new JavaScriptBuilderElementBuilder(new TestLoggerFactory())
                .SetEndpoint("/json")
                .SetMinify(false)
                .Build();
            var evidence = new Dictionary<string, object>(query)
            {
                [JavaScriptBuilder.Constants.EVIDENCE_HOST_KEY] = "localhost",
            };
            var flowData = new Mock<IFlowData>();
            flowData.Setup(d => d.TryGetEvidence(
                    It.IsAny<string>(), out It.Ref<object>.IsAny))
                .Returns(new TryGetDelegate((string key, out object value) =>
                    evidence.TryGetValue(key, out value)));
            flowData.Setup(d => d.TryGet(
                    It.IsAny<string>(), out It.Ref<IElementData>.IsAny))
                .Returns(new TryGetElementDataDelegate(
                    (string key, out IElementData value) =>
                    {
                        value = null;
                        return false;
                    }));
            flowData.Setup(d => d.Get<IJsonBuilderElementData>())
                .Returns(() => new JsonBuilderElementData(
                    new Mock<ILogger<JsonBuilderElementData>>().Object,
                    null)
                {
                    Json = "{\"derived\":{\"humanconfidence\":\"Low\"}}"
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
