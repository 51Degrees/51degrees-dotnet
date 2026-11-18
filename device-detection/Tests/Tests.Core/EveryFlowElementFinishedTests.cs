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

using FiftyOne.DeviceDetection.Apple;
using FiftyOne.DeviceDetection.Hash.Engine.OnPremise.FlowElements;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace FiftyOne.DeviceDetection.Tests.Core
{
    /// <summary>
    /// thread-safety.md, Element Data: "Each implementation MUST include a
    /// check, run by its automated tests, that processes every Flow Element
    /// in the repository, then writes to its Element Data and expects the
    /// error." This covers the Apple profile engine and the Hash engine
    /// with the Lite data file. The other Device Detection packages have
    /// their own.
    /// </summary>
    [TestClass]
    public class EveryFlowElementFinishedTests
    {
        [TestMethod]
        public void EveryFlowElement_WriteAfterProcessingFails()
        {
            var file = TestHelpers.Utils.GetFilePath(
                TestHelpers.Constants.LITE_HASH_DATA_FILE_NAME).FullName;
            var logger = NullLoggerFactory.Instance;
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
                AppleDecisionTreeTests.JSON_TEST_TREE));
            var apple = new AppleProfileEngineBuilder(logger)
                .SetAutoUpdate(false)
                .SetDataUpdateOnStartup(false)
                .SetDataFileSystemWatcher(false)
                .Build(stream);
            var hash = new DeviceDetectionHashEngineBuilder(logger)
                .SetPerformanceProfile(
                    Pipeline.Engines.PerformanceProfiles.LowMemory)
                .SetAutoUpdate(false)
                .SetDataUpdateOnStartup(false)
                .SetDataFileSystemWatcher(false)
                .Build(file, false);
            using var pipeline = new PipelineBuilder(logger)
                .AddFlowElement(apple)
                .AddFlowElement(hash)
                .Build();
            using var data = pipeline.CreateFlowData();
            data.AddEvidence(Constants.EVIDENCE_FAMILY_KEY, "iPad");
            data.AddEvidence(Constants.EVIDENCE_SCREEN_HEIGHT_KEY, 1024);
            data.AddEvidence("header.user-agent", "Mozilla/5.0");
            data.Process();

            var failures = new System.Collections.Generic.List<string>();
            AssertWriteFails(data, apple, failures);
            AssertWriteFails(data, hash, failures);
            Assert.IsEmpty(failures,
                string.Join(Environment.NewLine, failures));
        }

        /// <summary>
        /// Every public Flow Element in the Device Detection package is
        /// checked, so a new one is checked too.
        /// </summary>
        [TestMethod]
        public void EveryFlowElement_IsChecked()
        {
            var missing = Unchecked(
                new[]
                {
                    typeof(AppleProfileEngine),
                    typeof(DeviceDetectionHashEngine),
                },
                typeof(AppleProfileEngine),
                typeof(DeviceDetectionHashEngine));
            Assert.IsEmpty(missing,
                "These Flow Elements are not checked: " +
                string.Join(", ", missing));
        }
        private static void AssertWriteFails(
            IFlowData data,
            IFlowElement element,
            System.Collections.Generic.List<string> failures)
        {
            var name = element.GetType().Name;
            if (data.TryGet(element.ElementDataKey, out var elementData) ==
                false || elementData == null)
            {
                failures.Add($"{name} added no Element Data");
                return;
            }
            // Reading the values keeps them, which is not a write.
            _ = elementData.AsDictionary();
            try
            {
                elementData["written.after.processing"] = "value";
                failures.Add($"{name}: a write after processing succeeded");
            }
            catch (PipelineException)
            {
                // Expected.
            }
        }

        private static System.Collections.Generic.List<string> Unchecked(
            System.Collections.Generic.IEnumerable<Type> checkedTypes,
            params Type[] assemblyTypes)
        {
            var known = new System.Collections.Generic.HashSet<Type>(
                checkedTypes);
            return assemblyTypes
                .Select(t => t.Assembly)
                .Distinct()
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsClass &&
                    t.IsAbstract == false &&
                    t.IsGenericTypeDefinition == false &&
                    t.IsPublic &&
                    IsFlowElement(t) &&
                    known.Contains(t) == false)
                .Select(t => t.FullName)
                .OrderBy(n => n)
                .ToList();
        }

        private static bool IsFlowElement(Type type)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                if (t.IsGenericType &&
                    t.GetGenericTypeDefinition() ==
                        typeof(FlowElementBase<,>))
                {
                    return true;
                }
            }
            return false;
        }
    }
}