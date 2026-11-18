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
using FiftyOne.DeviceDetection.PropertyKeyed.FlowElements;
using FiftyOne.DeviceDetection.TestHelpers;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace FiftyOne.DeviceDetection.PropertyKeyed.Tests
{
    /// <summary>
    /// thread-safety.md, Element Data: "Each implementation MUST include a
    /// check, run by its automated tests, that processes every Flow Element
    /// in the repository, then writes to its Element Data and expects the
    /// error." This covers the Hash engine and the property keyed engines.
    /// It needs the TAC data file, and is skipped with a message when it is
    /// missing.
    /// </summary>
    [TestClass]
    public class EveryFlowElementFinishedTests
    {
        private static readonly Type[] Checked =
        {
            typeof(DeviceDetectionHashEngine),
            typeof(TacEngine),
            typeof(NativeEngine),
            typeof(PropertyKeyedDeviceEngine),
        };

        [TestMethod]
        public void EveryFlowElement_WriteAfterProcessingFails()
        {
            var file = Utils.GetPaidFilePath(
                Constants.TAC_HASH_DATA_FILE_NAME).FullName;
            var logger = NullLoggerFactory.Instance;
            var hash = new DeviceDetectionHashEngineBuilder(logger)
                .SetAutoUpdate(false)
                .SetDataFileSystemWatcher(false)
                .Build(file, false);
            var tac = new TacEngineBuilder(logger).Build();
            var native = new NativeEngineBuilder(logger).Build();
            var keyed = new PropertyKeyedDeviceEngine(
                logger,
                new System.Collections.Generic.List<string> { "IsMobile" },
                "IsMobile",
                "finished-profiles");
            using var pipeline = new PipelineBuilder(logger)
                .AddFlowElement(hash)
                .AddFlowElement(tac)
                .AddFlowElement(native)
                .AddFlowElement(keyed)
                .SetAutoDisposeElements(true)
                .Build();
            using var data = pipeline.CreateFlowData();
            data.AddEvidence("header.user-agent", "Mozilla/5.0");
            data.AddEvidence("query.tac", "35925406");
            data.AddEvidence("query.nativemodel", "SM-G960F");
            data.AddEvidence("query.ismobile", "True");
            data.Process();

            var failures = new System.Collections.Generic.List<string>();
            foreach (var element in new IFlowElement[]
                { hash, tac, native, keyed })
            {
                AssertWriteFails(data, element, failures);
            }
            Assert.IsEmpty(failures,
                string.Join(Environment.NewLine, failures));
        }

        /// <summary>
        /// Every public Flow Element in the Hash and property keyed packages
        /// is checked, so a new one is checked too.
        /// </summary>
        [TestMethod]
        public void EveryFlowElement_IsChecked()
        {
            var missing = Unchecked(
                Checked,
                typeof(DeviceDetectionHashEngine),
                typeof(TacEngine));
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