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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace FiftyOne.Pipeline.JavaScript.Tests
{
    /// <summary>
    /// Measures the time minification adds to each script the JavaScript
    /// builder renders, and fails when it is far beyond what every runner
    /// measures, so a slow minification library is caught before release.
    /// </summary>
    [TestClass]
    public class MinifyPerformanceTests
    {
        // Renders of each kind before measuring, so the first render's
        // one-off costs are not counted.
        private const int WarmUpRenders = 10;

        // Renders of each kind measured, alternating between the two.
        private const int MeasuredRenders = 100;

        // Properties in the payload, about the size of a device detection
        // response.
        private const int PayloadProperties = 200;

        // The most minification may add to the median script. Every
        // runner and configuration the builds use measured 6 to 12 ms,
        // apart from macOS on Intel at 43 and 58 ms, so 500 ms is about
        // nine times the slowest and only a gross slowdown reaches it.
        private const double MaxAddedMilliseconds = 500;

        private delegate void GetValueCallback(
            string key,
            out object result);

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void Minify_AddedTimePerScript_WithinLimit()
        {
            var renderer = new Renderer(Payload());
            var minified = Element(true);
            var plain = Element(false);
            for (var i = 0; i < WarmUpRenders; i++)
            {
                renderer.Render(minified);
                renderer.Render(plain);
            }

            var minifiedTimes = new List<double>();
            var plainTimes = new List<double>();
            for (var i = 0; i < MeasuredRenders; i++)
            {
                minifiedTimes.Add(Time(renderer, minified));
                plainTimes.Add(Time(renderer, plain));
            }
            var minifiedMedian = Median(minifiedTimes);
            var plainMedian = Median(plainTimes);
            var added = minifiedMedian - plainMedian;

            var message =
                $"Minification added {added:F3} ms to the median script " +
                $"({minifiedMedian:F3} ms minified, {plainMedian:F3} ms " +
                $"not, over {MeasuredRenders} renders of each). The limit " +
                $"is {MaxAddedMilliseconds} ms.";
            TestContext.WriteLine(message);
            Console.WriteLine(message);
            Assert.IsTrue(
                renderer.Render(minified).Length <
                    renderer.Render(plain).Length,
                "The minified script is not shorter, so it was not " +
                "minified and the measurement shows nothing.");
            Assert.IsTrue(added < MaxAddedMilliseconds, message);
        }

        private static JavaScriptBuilderElement Element(bool minify)
        {
            return new JavaScriptBuilderElementBuilder(
                    new TestLoggerFactory())
                .SetEndpoint("/json")
                .SetMinify(minify)
                .Build();
        }

        // A payload with many properties, as the JSON builder writes it.
        private static string Payload()
        {
            var device = new JObject();
            for (var i = 0; i < PayloadProperties; i++)
            {
                device.Add($"property{i}", $"value of property {i}");
            }
            return new JObject { ["device"] = device }
                .ToString(Formatting.None);
        }

        private static double Time(
            Renderer renderer,
            JavaScriptBuilderElement element)
        {
            var stopwatch = Stopwatch.StartNew();
            renderer.Render(element);
            stopwatch.Stop();
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static double Median(List<double> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            var middle = sorted.Count / 2;
            return sorted.Count % 2 == 1 ?
                sorted[middle] :
                (sorted[middle - 1] + sorted[middle]) / 2;
        }

        /// <summary>
        /// A mocked request carrying the payload, set up once so that only
        /// the rendering is timed.
        /// </summary>
        private sealed class Renderer
        {
            private readonly Mock<IFlowData> _flowData =
                new Mock<IFlowData>();
            private IJavaScriptBuilderElementData _data;

            public Renderer(string json)
            {
                _flowData.Setup(d => d.Get<IJsonBuilderElementData>())
                    .Returns(() => new JsonBuilderElementData(
                        new Mock<ILogger<JsonBuilderElementData>>().Object,
                        null)
                    {
                        Json = json
                    });
                _flowData.Setup(d => d.TryGetEvidence(
                        JavaScriptBuilder.Constants.EVIDENCE_HOST_KEY,
                        out It.Ref<object>.IsAny))
                    .Callback(new GetValueCallback(
                        (string key, out object result) =>
                        {
                            result = "localhost";
                        }))
                    .Returns(true);
                _flowData.Setup(d => d.GetEvidence().AsDictionary())
                    .Returns(new Dictionary<string, object>());
                _flowData.Setup(d => d.GetOrAdd(
                        It.IsAny<ITypedKey<IJavaScriptBuilderElementData>>(),
                        It.IsAny<Func<IPipeline,
                            IJavaScriptBuilderElementData>>()))
                    .Returns<ITypedKey<IJavaScriptBuilderElementData>,
                        Func<IPipeline, IJavaScriptBuilderElementData>>(
                        (k, f) =>
                        {
                            _data = f(null);
                            return _data;
                        });
            }

            public string Render(JavaScriptBuilderElement element)
            {
                _data = null;
                element.Process(_flowData.Object);
                Assert.IsNotNull(_data, "No script was rendered.");
                return _data.JavaScript;
            }
        }
    }
}
