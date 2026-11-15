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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FiftyOne.Pipeline.JavaScript.Tests
{
    /// <summary>
    /// Checks that the rendered script ends each line with LF, as the
    /// template in javascript-templates does, whatever line endings the
    /// checkout gave the copy of the template built into the assembly.
    /// </summary>
    [TestClass]
    public class TemplateLineEndingTests
    {
        private delegate void GetValueCallback(
            string key,
            out object result);

        /// <summary>
        /// The script that is not minified keeps the template's lines, and
        /// none of them ends with a carriage return.
        /// </summary>
        [TestMethod]
        public void Script_LinesEndWithLineFeedOnly()
        {
            var element = new JavaScriptBuilderElementBuilder(
                    new TestLoggerFactory())
                .SetEndpoint("/json")
                .SetMinify(false)
                .Build();

            var script = Render(element);

            Assert.IsTrue(
                script.Count(c => c == '\n') > 100,
                "The script should keep the template's lines.");
            Assert.AreEqual(
                -1,
                script.IndexOf('\r'),
                "The script should hold no carriage return.");
        }

        /// <summary>
        /// Processes a mocked request carrying a small device payload, with
        /// the JSON written on one line so that only the template gives the
        /// script its lines, and returns the rendered script.
        /// </summary>
        private static string Render(JavaScriptBuilderElement element)
        {
            var flowData = new Mock<IFlowData>();
            var json = new JObject
            {
                ["device"] = new JObject(new JProperty("ismobile", true))
            };
            flowData.Setup(d => d.Get<IJsonBuilderElementData>())
                .Returns(() => new JsonBuilderElementData(
                    new Mock<ILogger<JsonBuilderElementData>>().Object,
                    null)
                {
                    Json = json.ToString(Formatting.None)
                });
            flowData.Setup(d => d.TryGetEvidence(
                    JavaScriptBuilder.Constants.EVIDENCE_HOST_KEY,
                    out It.Ref<object>.IsAny))
                .Callback(new GetValueCallback(
                    (string key, out object result) =>
                    {
                        result = "localhost";
                    }))
                .Returns(true);
            flowData.Setup(d => d.GetEvidence().AsDictionary())
                .Returns(new Dictionary<string, object>());

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
