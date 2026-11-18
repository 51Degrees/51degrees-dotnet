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

using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Examples.Shared;
using FiftyOne.Pipeline.IpSplitter;
using FiftyOne.Pipeline.Math;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ClientSide = Examples.ClientSideEvidence.Shared;
using Simple = Examples.CustomFlowElement.FlowElements;
using OnPremise = Examples.OnPremiseEngine.FlowElements;

namespace FiftyOne.Pipeline.Examples.Tests
{
    /// <summary>
    /// thread-safety.md, Element Data: "Each implementation MUST include a
    /// check, run by its automated tests, that processes every Flow Element
    /// in the repository, then writes to its Element Data and expects the
    /// error." This covers the Flow Elements of the Pipeline examples. The
    /// cloud engine example needs the cloud service and targets net8.0
    /// only, so it is not referenced here.
    /// </summary>
    [TestClass]
    public class EveryFlowElementFinishedTests
    {
        private static readonly Assembly[] Assemblies =
        {
            typeof(Simple.SimpleFlowElement).Assembly,
            typeof(OnPremise.SimpleOnPremiseEngine).Assembly,
            typeof(ClientSide.SimpleClientSideElement).Assembly,
            typeof(PrimeCheckerEngine).Assembly,
            typeof(IpSplitterElementBuilder).Assembly,
            typeof(MathElementBuilder).Assembly,
        };

        /// <summary>
        /// Each Flow Element checked, with the evidence it reads.
        /// </summary>
        private static IEnumerable<(Type Type, Func<IFlowElement> Build,
            string Key, object Value)> Cases()
        {
            var logger = NullLoggerFactory.Instance;
            yield return (typeof(Simple.SimpleFlowElement),
                () => new Simple.SimpleFlowElementBuilder(logger).Build(),
                "date-of-birth",
                new DateTime(1992, 12, 18));
            yield return (typeof(OnPremise.SimpleOnPremiseEngine),
                () => new OnPremise.SimpleOnPremiseEngineBuilder(logger, null)
                    .SetAutoUpdate(false)
                    .SetDataFileSystemWatcher(false)
                    .Build("starsigns.csv", false),
                "date-of-birth",
                new DateTime(1992, 12, 18));
            yield return (typeof(ClientSide.SimpleClientSideElement),
                () => new ClientSide.SimpleClientSideElementBuilder(logger)
                    .Build(),
                "cookie.date-of-birth",
                "18/12/1992");
            yield return (typeof(PrimeCheckerEngine),
                () => new PrimeCheckerEngineBuilder(logger).Build(),
                FiftyOne.Pipeline.Examples.Shared.Constants
                    .PRIMECHECKER_EVIDENCE_KEY,
                7);
            yield return (typeof(IpSplitterElement),
                () => new IpSplitterElementBuilder(logger).Build(),
                Core.Constants.EVIDENCE_CLIENTIP_KEY,
                "127.0.0.1");
            yield return (typeof(MathElement),
                () => new MathElementBuilder(logger).Build(),
                FiftyOne.Pipeline.Math.Constants.EVIDENCE_OPERATION_KEY,
                "1 plus 2");
        }

        /// <summary>
        /// Every example Flow Element is processed, then a write to its
        /// Element Data fails.
        /// </summary>
        [TestMethod]
        public void EveryFlowElement_WriteAfterProcessingFails()
        {
            var failures = new List<string>();
            foreach (var (type, build, key, value) in Cases())
            {
                var element = build();
                Assert.AreEqual(type, element.GetType());
                var name = type.Name;
                // The IP splitter example does not implement its cleanup,
                // so the elements are not disposed with the Pipeline.
                using var pipeline = new PipelineBuilder(
                        NullLoggerFactory.Instance)
                    .AddFlowElement(element)
                    .SetAutoDisposeElements(false)
                    .Build();
                using var data = pipeline.CreateFlowData();
                data.AddEvidence(key, value);
                data.Process();
                if (data.TryGet(element.ElementDataKey, out var elementData)
                    == false || elementData == null)
                {
                    failures.Add($"{name} added no Element Data");
                    continue;
                }
                try
                {
                    elementData["written.after.processing"] = "value";
                    failures.Add($"{name}: a write after processing " +
                        "succeeded");
                }
                catch (PipelineException)
                {
                    // Expected.
                }
            }
            Assert.IsEmpty(failures,
                string.Join(Environment.NewLine, failures));
        }

        /// <summary>
        /// Every public Flow Element in the example assemblies referenced
        /// here is checked, so a new one is checked too.
        /// </summary>
        [TestMethod]
        public void EveryFlowElement_IsChecked()
        {
            var checkedTypes = new HashSet<Type>(
                Cases().Select(c => c.Type));
            var missing = Assemblies
                .Distinct()
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsClass &&
                    t.IsAbstract == false &&
                    t.IsGenericTypeDefinition == false &&
                    t.IsPublic &&
                    IsFlowElement(t) &&
                    checkedTypes.Contains(t) == false)
                .Select(t => t.FullName)
                .OrderBy(n => n)
                .ToList();
            Assert.IsEmpty(missing,
                "These Flow Elements are not checked: " +
                string.Join(", ", missing));
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
