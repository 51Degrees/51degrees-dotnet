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

using FiftyOne.DeviceDetection.Hash.Engine.OnPremise.FlowElements;
using Constants = FiftyOne.DeviceDetection.TestHelpers.Constants;
using Utils = FiftyOne.DeviceDetection.TestHelpers.Utils;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FiftyOne.DeviceDetection.Hash.Tests.FlowElements
{
    /// <summary>
    /// Evidence keys are case-insensitive, so a key whose prefix or field is
    /// written in other letters gives the same result as the key in lower
    /// case.
    /// </summary>
    [TestClass]
    public class EvidenceKeyCaseTests
    {
        private static ILoggerFactory _loggerFactory;
        private static DeviceDetectionHashEngine _engine;
        private static IPipeline _pipeline;

        [ClassInitialize]
        public static void Init(TestContext context)
        {
            _loggerFactory = new LoggerFactory();
            _engine = new DeviceDetectionHashEngineBuilder(_loggerFactory)
                .SetPerformanceProfile(PerformanceProfiles.MaxPerformance)
                .SetAutoUpdate(false)
                .SetDataFileSystemWatcher(false)
                .SetDataUpdateOnStartup(false)
                .Build(
                    Utils.GetFilePath(Constants.LITE_HASH_DATA_FILE_NAME)
                        .FullName,
                    false);
            // Disposing the pipeline also disposes the engine.
            _pipeline = new PipelineBuilder(_loggerFactory)
                .AddFlowElement(_engine)
                .Build();
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            _pipeline?.Dispose();
        }

        [TestMethod]
        [DataRow("Header.User-Agent")]
        [DataRow("HEADER.USER-AGENT")]
        [DataRow("header.User-Agent")]
        [DataRow("hEaDeR.user-agent")]
        public void Process_KeyInOtherLetters_SameDevice(string key)
        {
            var expected = DeviceId("header.user-agent");

            Assert.AreNotEqual(
                "0-0-0-0",
                expected,
                "The lower case key found no device, so the test proves " +
                "nothing.");
            Assert.AreEqual(expected, DeviceId(key));
        }

        private static string DeviceId(string key)
        {
            using (var data = _pipeline.CreateFlowData())
            {
                data.AddEvidence(key, Constants.MobileUserAgent);
                data.Process();
                return data.GetFromElement(_engine).DeviceId.Value;
            }
        }
    }
}
