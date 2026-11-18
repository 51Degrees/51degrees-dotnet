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

using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Core.Tests.Data
{
    /// <summary>
    /// Element Data that can be pooled goes back to its Flow Element's pool
    /// when its Flow Data is disposed, and the next request takes it from
    /// there instead of allocating a new instance. See the reusing Element
    /// Data section of resource-cleanup.md in the specification.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class ElementDataPoolTests
    {
        private const int Requests = 1000;

        private const int Threads = 8;

        /// <summary>
        /// Element Data whose only state is its values, so it opts in to
        /// pooling. Counts the instances created.
        /// </summary>
        private sealed class PoolableData : ElementDataBase
        {
            private static int _created;

            public static int Created => Volatile.Read(ref _created);

            public static void ResetCount() =>
                Interlocked.Exchange(ref _created, 0);

            public PoolableData(IPipeline pipeline)
                : base(NullLogger<ElementDataBase>.Instance, pipeline)
            {
                Interlocked.Increment(ref _created);
            }

            protected override bool IsPoolable => true;
        }

        /// <summary>
        /// Writes the request's value under a key named after it, so a
        /// value left from an earlier request shows as an extra key.
        /// </summary>
        private sealed class PoolingElement
            : FlowElementBase<PoolableData, IElementPropertyMetaData>
        {
            public PoolingElement()
                : base(NullLogger<PoolingElement>.Instance,
                    (p, e) => new PoolableData(p))
            {
            }

            public override string ElementDataKey => "pooled";

            public override IEvidenceKeyFilter EvidenceKeyFilter { get; } =
                new EvidenceKeyFilterWhitelist(new List<string>() { "value" });

            public override IList<IElementPropertyMetaData> Properties { get; } =
                new List<IElementPropertyMetaData>();

            protected override void ProcessInternal(IFlowData data)
            {
                var value = (string)data.GetEvidence()["value"];
                var elementData = data.GetOrAdd(
                    ElementDataKeyTyped, CreateElementData);
                elementData["key-" + value] = value;
            }

            protected override void ManagedResourcesCleanup()
            {
            }

            protected override void UnmanagedResourcesCleanup()
            {
            }
        }

        private static void ProcessRequests(
            IPipeline pipeline, PoolingElement element, string name, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var value = $"{name}-{i}";
                using (var data = pipeline.CreateFlowData())
                {
                    data.AddEvidence("value", value);
                    data.Process();
                    var values = data.GetFromElement(element).AsDictionary();
                    Assert.HasCount(1, values,
                        "A pooled instance must start with no values.");
                    Assert.AreEqual(value, values["key-" + value]);
                }
            }
        }

        /// <summary>
        /// One thread processing request after request allocates one
        /// instance, and each request sees only its own value.
        /// </summary>
        [TestMethod]
        public void OneThread_AllocatesOneInstance()
        {
            PoolableData.ResetCount();
            using var element = new PoolingElement();
            using var pipeline = new PipelineBuilder(
                NullLoggerFactory.Instance)
                .AddFlowElement(element)
                .Build();

            ProcessRequests(pipeline, element, "one", Requests);

            Assert.AreEqual(1, PoolableData.Created,
                $"{Requests} requests in turn should reuse one instance.");
        }

        /// <summary>
        /// Several threads, each processing request after request, allocate
        /// no more instances than there are threads.
        /// </summary>
        [TestMethod]
        public void SeveralThreads_AllocateAnInstanceEachAtMost()
        {
            PoolableData.ResetCount();
            using var element = new PoolingElement();
            using var pipeline = new PipelineBuilder(
                NullLoggerFactory.Instance)
                .AddFlowElement(element)
                .Build();

            var tasks = Enumerable.Range(0, Threads)
                .Select(t => Task.Factory.StartNew(
                    () => ProcessRequests(pipeline, element, $"t{t}", Requests),
                    TaskCreationOptions.LongRunning))
                .ToArray();
            Task.WaitAll(tasks);

            Assert.IsLessThanOrEqualTo(Threads, PoolableData.Created,
                $"{Threads * Requests} requests on {Threads} threads " +
                $"should need at most {Threads} instances.");
        }
    }
}
