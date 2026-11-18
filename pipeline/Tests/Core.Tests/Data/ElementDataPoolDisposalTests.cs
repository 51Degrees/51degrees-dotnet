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
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Core.Tests.Data
{
    /// <summary>
    /// Returning Element Data to its pool never stops the disposal of the
    /// rest of the Flow Data, and a pooled instance does not keep the
    /// Pipeline it was last used with.
    /// </summary>
    [TestClass]
    public class ElementDataPoolDisposalTests
    {
        /// <summary>
        /// Poolable Element Data whose reset can be made to fail.
        /// </summary>
        private sealed class PoolableData : ElementDataBase
        {
            public bool FailReset { get; set; }

            public PoolableData(IPipeline pipeline)
                : base(NullLogger<ElementDataBase>.Instance, pipeline)
            {
            }

            protected override bool IsPoolable => true;

            protected override void ResetForPool()
            {
                if (FailReset)
                {
                    throw new InvalidOperationException("Reset failed.");
                }
                base.ResetForPool();
            }
        }

        /// <summary>
        /// Element Data that releases something when disposed.
        /// </summary>
        private sealed class DisposableData : ElementDataBase, IDisposable
        {
            public bool Disposed { get; private set; }

            public DisposableData(IPipeline pipeline)
                : base(NullLogger<ElementDataBase>.Instance, pipeline)
            {
            }

            public void Dispose()
            {
                Disposed = true;
            }
        }

        private sealed class Element<T> : FlowElementBase<T,
            IElementPropertyMetaData>
            where T : class, IElementData
        {
            private readonly string _key;

            public Element(
                string key,
                Func<IPipeline, FlowElementBase<T, IElementPropertyMetaData>,
                    T> factory)
                : base(NullLogger<Element<T>>.Instance, factory)
            {
                _key = key;
            }

            public override string ElementDataKey => _key;

            public override IEvidenceKeyFilter EvidenceKeyFilter { get; } =
                new EvidenceKeyFilterWhitelist(new List<string>());

            public override IList<IElementPropertyMetaData> Properties
            { get; } = new List<IElementPropertyMetaData>();

            protected override void ProcessInternal(IFlowData data)
            {
                var elementData = data.GetOrAdd(
                    ElementDataKeyTyped, CreateElementData);
                elementData["value"] = _key;
            }

            protected override void ManagedResourcesCleanup() { }

            protected override void UnmanagedResourcesCleanup() { }
        }

        /// <summary>
        /// A pooled instance that fails to reset is left out of its pool,
        /// and the Flow Data still disposes the Element Data after it.
        /// </summary>
        [TestMethod]
        public void ResetFails_RestOfTheFlowDataStillDisposed()
        {
            using var pooled = new Element<PoolableData>(
                "pooled", (p, e) => new PoolableData(p));
            using var disposable = new Element<DisposableData>(
                "disposable", (p, e) => new DisposableData(p));
            using var pipeline = new PipelineBuilder(
                NullLoggerFactory.Instance)
                .AddFlowElement(pooled)
                .AddFlowElement(disposable)
                .Build();

            var data = pipeline.CreateFlowData();
            data.Process();
            data.GetFromElement(pooled).FailReset = true;
            var disposableData = data.GetFromElement(disposable);

            data.Dispose();

            Assert.IsTrue(disposableData.Disposed,
                "A failed return to a pool must not stop the disposal.");
        }

        /// <summary>
        /// An instance waiting in its pool does not keep the Pipeline it
        /// was last used with.
        /// </summary>
        [TestMethod]
        public void PooledInstance_DoesNotKeepThePipeline()
        {
            using var pooled = new Element<PoolableData>(
                "pooled", (p, e) => new PoolableData(p));
            using var pipeline = new PipelineBuilder(
                NullLoggerFactory.Instance)
                .AddFlowElement(pooled)
                .Build();

            PoolableData instance;
            using (var data = pipeline.CreateFlowData())
            {
                data.Process();
                instance = data.GetFromElement(pooled);
            }

            Assert.IsNull(instance.Pipeline);
        }
    }
}
