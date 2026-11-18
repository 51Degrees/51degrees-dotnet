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
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Core.Tests.HelperClasses;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace FiftyOne.Pipeline.Core.Tests.FlowElements;

/// <summary>
/// thread-safety.md, Element Data: "A write through its write methods
/// while the instance is recorded as finished MUST fail with an error,
/// except by a lazy property's task." Element Data records when a Flow
/// Element that writes it finishes, whether it finished normally or threw,
/// and a Flow Element that shares the instance opens it for writing again
/// while it processes. Values kept once read are not writes.
/// </summary>
[TestClass]
public class FinishedElementDataTests
{
    /// <summary>
    /// Writes a value under its own name into the Element Data with the key
    /// given, which other writers may share, and optionally throws.
    /// </summary>
    private class Writer :
        FlowElementBase<TestElementData, IElementPropertyMetaData>
    {
        private readonly string _key;
        private readonly string _name;
        private readonly bool _fail;

        public Writer(string key, string name, bool fail) :
            base(
                NullLogger<Writer>.Instance,
                (pipeline, element) => new TestElementData(pipeline))
        {
            _key = key;
            _name = name;
            _fail = fail;
        }

        public override string ElementDataKey => _key;

        public override IEvidenceKeyFilter EvidenceKeyFilter =>
            new EvidenceKeyFilterWhitelist(new List<string>());

        public override IList<IElementPropertyMetaData> Properties =>
            new List<IElementPropertyMetaData>();

        protected override void ProcessInternal(IFlowData data)
        {
            data.GetOrAdd(ElementDataKey, p => CreateElementData(p))
                [_name] = "written";
            if (_fail)
            {
                throw new InvalidOperationException("thrown");
            }
        }

        protected override void ManagedResourcesCleanup() { }

        protected override void UnmanagedResourcesCleanup() { }
    }

    // Calls the protected KeepValues method, which a derived class uses to
    // keep values it has read.
    private static void Keep(ElementDataBase data, string key, object value)
    {
        var method = typeof(ElementDataBase).GetMethod(
            "KeepValues",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, "ElementDataBase has no KeepValues method");
        method.Invoke(data, new object[]
        {
            new[] { new KeyValuePair<string, object>(key, value) }
        });
    }

    private static IPipeline Build(params IFlowElement[] elements)
    {
        var builder = new PipelineBuilder(NullLoggerFactory.Instance)
            .SetSuppressProcessExceptions(true);
        foreach (var element in elements)
        {
            builder.AddFlowElement(element);
        }
        return builder.Build();
    }

    private static void AssertWriteFails(TestElementData data)
    {
        Assert.ThrowsExactly<PipelineException>(
            () => data["after"] = "value",
            "A write succeeded after the Flow Element had finished");
        Assert.ThrowsExactly<PipelineException>(
            () => data.Result = "value",
            "A setter succeeded after the Flow Element had finished");
        Assert.ThrowsExactly<PipelineException>(
            () => data.PopulateFrom(new Dictionary<string, object>
                {
                    { "after", "value" }
                }),
            "PopulateFrom succeeded after the Flow Element had finished");
    }

    [TestMethod]
    public void Finished_WriteFails()
    {
        using var pipeline = Build(new Writer("own", "first", false));
        using var data = pipeline.CreateFlowData();
        data.Process();

        var own = (TestElementData)data.Get("own");
        Assert.AreEqual("written", own["first"]);
        AssertWriteFails(own);
    }

    [TestMethod]
    public void FinishedWithException_WriteFails()
    {
        using var pipeline = Build(new Writer("own", "first", true));
        using var data = pipeline.CreateFlowData();
        data.Process();

        Assert.AreEqual(1, data.Errors.Count);
        AssertWriteFails((TestElementData)data.Get("own"));
    }

    [TestMethod]
    public void Shared_EachWriterWritesWhileItProcesses()
    {
        using var pipeline = Build(
            new Writer("shared", "first", false),
            new Writer("shared", "second", false));
        using var data = pipeline.CreateFlowData();
        data.Process();

        Assert.AreEqual(0, data.Errors?.Count ?? 0,
            string.Join(", ", data.Errors ?? new List<IFlowError>()));
        var shared = (TestElementData)data.Get("shared");
        Assert.AreEqual("written", shared["first"]);
        Assert.AreEqual("written", shared["second"]);
        AssertWriteFails(shared);
    }

    [TestMethod]
    public void BeforeProcessing_WriteWorks()
    {
        using var pipeline = Build(new Writer("own", "first", false));
        using var data = pipeline.CreateFlowData();
        var own = data.GetOrAdd("own", p => new TestElementData(p));
        own["before"] = "value";
        data.Process();

        Assert.AreEqual("value", own["before"]);
        Assert.AreEqual("written", own["first"]);
    }

    [TestMethod]
    public void NoOwner_WriteWorks()
    {
        var free = new TestElementData(null);
        free["value"] = 1;
        free.Result = 2;
        Assert.AreEqual(1, free["value"]);
        Assert.AreEqual(2, free.Result);
    }

    [TestMethod]
    public void ValuesKeptOnceRead_AreNotWrites()
    {
        using var pipeline = Build(new Writer("own", "first", false));
        using var data = pipeline.CreateFlowData();
        var kept = data.GetOrAdd("own", p => new TestElementData(p));
        data.Process();

        AssertWriteFails(kept);
        Keep(kept, "kept", "value");
        Assert.AreEqual("value", kept["kept"]);
    }
}
