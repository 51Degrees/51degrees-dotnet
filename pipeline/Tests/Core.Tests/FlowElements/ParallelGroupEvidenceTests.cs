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
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.Pipeline.Core.Tests.FlowElements;

/// <summary>
/// evidence.md, Adding Evidence values: "A Flow Element that runs in a
/// parallel group MUST NOT add values, as thread safety explains." The
/// other elements of the group read the Evidence at the same time, so the
/// Flow Data refuses a value an element of a group adds to it, and records
/// a configuration error against that element for each key.
/// </summary>
[TestClass]
public class ParallelGroupEvidenceTests
{
    private static readonly string[] Names = { "query.added", "query.sequence" };

    private static string Refused(string key) =>
        $"An element of a parallel group tried to add the evidence '{key}', " +
        "which was refused, because the other elements of the group read " +
        "the evidence at the same time. Add the element on its own, before " +
        "the elements that read the value.";

    private static Mock<IFlowElement> Element(
        string name,
        Action<IFlowData> process)
    {
        var mock = new Mock<IFlowElement>();
        mock.SetupGet(e => e.ElementDataKey).Returns(name);
        mock.SetupGet(e => e.Properties)
            .Returns(new List<IElementPropertyMetaData>());
        mock.SetupGet(e => e.EvidenceKeyFilter)
            .Returns(new EvidenceKeyFilterWhitelist(new List<string>()));
        mock.Setup(e => e.Process(It.IsAny<IFlowData>())).Callback(process);
        return mock;
    }

    // An element that records what the Evidence held for each name when it
    // ran, under "<element> <name>".
    private static Mock<IFlowElement> Reader(
        string name,
        ConcurrentDictionary<string, string> seen) =>
        Element(name, data => Record(data, name, seen));

    private static void Record(
        IFlowData data,
        string name,
        ConcurrentDictionary<string, string> seen)
    {
        foreach (var key in Names)
        {
            seen[$"{name} {key}"] = data.TryGetEvidence(key, out object value)
                ? value.ToString()
                : "absent";
        }
    }

    private static IFlowData Supplied(IPipeline pipeline)
    {
        var data = pipeline.CreateFlowData();
        data.AddEvidence("header.User-Agent", "test");
        data.AddEvidence("query.Sequence", "5");
        return data;
    }

    private static List<(string Key, string Message, Type Type)> Errors(
        IFlowData data) =>
        (data.Errors ?? new List<IFlowError>())
            .Select(e => (
                e.FlowElement?.ElementDataKey,
                e.ExceptionData.Message,
                e.ExceptionData.GetType()))
            .ToList();

    /// <summary>
    /// An element of a group that adds values adds nothing, so it, the
    /// other element of the group and the element after the group all see
    /// the Evidence from before the group, and an error is recorded against
    /// it for each key, in the order it tried them.
    /// </summary>
    [TestMethod]
    public void ElementOfGroup_AddsEvidence_RefusedWithAnErrorForEachKey()
    {
        var seen = new ConcurrentDictionary<string, string>();
        var adder = Element("adder", data =>
        {
            data.AddEvidence("query.added", "1");
            data.AddEvidence("QUERY.SEQUENCE", "6");
            Record(data, "adder", seen);
        });
        using var pipeline = new PipelineBuilder(NullLoggerFactory.Instance)
            .AddFlowElementsParallel(adder.Object, Reader("other", seen).Object)
            .AddFlowElement(Reader("after", seen).Object)
            .SetSuppressProcessExceptions(true)
            .Build();
        using var data = Supplied(pipeline);

        data.Process();

        foreach (var name in new[] { "adder", "other", "after" })
        {
            Assert.AreEqual("absent", seen[$"{name} query.added"], name);
            Assert.AreEqual("5", seen[$"{name} query.sequence"], name);
        }
        CollectionAssert.AreEqual(
            new[]
            {
                ("adder", Refused("query.added"), typeof(PipelineConfigurationException)),
                ("adder", Refused("QUERY.SEQUENCE"), typeof(PipelineConfigurationException)),
            },
            Errors(data));
    }

    /// <summary>
    /// The form that adds several values at once is refused for each key.
    /// </summary>
    [TestMethod]
    public void ElementOfGroup_AddsADictionary_RefusedForEachKey()
    {
        var seen = new ConcurrentDictionary<string, string>();
        var adder = Element("adder", data => data.AddEvidence(
            new Dictionary<string, object>
            {
                { "query.added", "1" },
                { "query.sequence", "6" },
            }));
        using var pipeline = new PipelineBuilder(NullLoggerFactory.Instance)
            .AddFlowElementsParallel(adder.Object, Reader("other", seen).Object)
            .AddFlowElement(Reader("after", seen).Object)
            .SetSuppressProcessExceptions(true)
            .Build();
        using var data = Supplied(pipeline);

        data.Process();

        Assert.AreEqual("absent", seen["after query.added"]);
        Assert.AreEqual("5", seen["after query.sequence"]);
        CollectionAssert.AreEqual(
            new[]
            {
                ("adder", Refused("query.added"), typeof(PipelineConfigurationException)),
                ("adder", Refused("query.sequence"), typeof(PipelineConfigurationException)),
            },
            Errors(data));
    }

    /// <summary>
    /// The Evidence the Flow Data gives out is refused the same way when an
    /// element of a group writes to it directly, through its indexer,
    /// PopulateFrom or PopulateFromDictionary.
    /// </summary>
    [TestMethod]
    [DataRow("indexer")]
    [DataRow("PopulateFrom")]
    [DataRow("PopulateFromDictionary")]
    public void ElementOfGroup_WritesTheEvidenceDirectly_Refused(string route)
    {
        var seen = new ConcurrentDictionary<string, string>();
        var adder = Element("adder", data =>
        {
            var evidence = data.GetEvidence();
            var values = new Dictionary<string, object>
            {
                { "query.added", "1" },
            };
            switch (route)
            {
                case "indexer":
                    evidence["query.added"] = "1";
                    break;
                case "PopulateFrom":
                    evidence.PopulateFrom(values);
                    break;
                default:
#pragma warning disable CS0618 // The deprecated form is still public.
                    evidence.PopulateFromDictionary(values);
#pragma warning restore CS0618
                    break;
            }
        });
        using var pipeline = new PipelineBuilder(NullLoggerFactory.Instance)
            .AddFlowElementsParallel(adder.Object, Reader("other", seen).Object)
            .AddFlowElement(Reader("after", seen).Object)
            .SetSuppressProcessExceptions(true)
            .Build();
        using var data = Supplied(pipeline);

        data.Process();

        Assert.AreEqual("absent", seen["after query.added"]);
        CollectionAssert.AreEqual(
            new[]
            {
                ("adder", Refused("query.added"), typeof(PipelineConfigurationException)),
            },
            Errors(data));
    }

    /// <summary>
    /// With errors not suppressed, the refused value fails processing with
    /// the configuration error once every element has run.
    /// </summary>
    [TestMethod]
    public void RefusedEvidence_FailsProcessing_UnlessErrorsAreSuppressed()
    {
        var seen = new ConcurrentDictionary<string, string>();
        var adder = Element("adder", data => data.AddEvidence("query.added", "1"));
        using var pipeline = new PipelineBuilder(NullLoggerFactory.Instance)
            .AddFlowElementsParallel(adder.Object, Reader("other", seen).Object)
            .AddFlowElement(Reader("after", seen).Object)
            .SetSuppressProcessExceptions(false)
            .Build();
        using var data = Supplied(pipeline);

        var thrown = Assert.ThrowsExactly<AggregateException>(() => data.Process());

        Assert.HasCount(1, thrown.InnerExceptions);
        Assert.IsInstanceOfType<PipelineConfigurationException>(
            thrown.InnerExceptions[0]);
        Assert.AreEqual(Refused("query.added"), thrown.InnerExceptions[0].Message);
        Assert.AreEqual("absent", seen["after query.added"]);
        Assert.IsFalse(data.TryGetEvidence("query.added", out object _));
    }

    /// <summary>
    /// An element on its own, and a group of one, which the builder adds as
    /// the element on its own, add values that later elements read.
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ElementOnItsOwn_AddsEvidence(bool asGroupOfOne)
    {
        var seen = new ConcurrentDictionary<string, string>();
        var adder = Element("adder", data => data.AddEvidence("query.added", "1"));
        var builder = new PipelineBuilder(NullLoggerFactory.Instance)
            .SetSuppressProcessExceptions(false);
        if (asGroupOfOne)
        {
            builder.AddFlowElementsParallel(adder.Object);
        }
        else
        {
            builder.AddFlowElement(adder.Object);
        }
        using var pipeline = builder
            .AddFlowElement(Reader("after", seen).Object)
            .Build();
        using var data = Supplied(pipeline);

        data.Process();

        Assert.AreEqual("1", seen["after query.added"]);
        Assert.IsEmpty(Errors(data));
    }

    /// <summary>
    /// An element of a group may add values to a Flow Data of its own, such
    /// as one it processes with another Pipeline, because no other element
    /// of the group reads that Flow Data.
    /// </summary>
    [TestMethod]
    public void ElementOfGroup_AddsToItsOwnFlowData()
    {
        var seen = new ConcurrentDictionary<string, string>();
        using var inner = new PipelineBuilder(NullLoggerFactory.Instance)
            .AddFlowElement(Reader("inner", seen).Object)
            .SetSuppressProcessExceptions(false)
            .Build();
        IList<IFlowError> innerErrors = null;
        var element = Element("outer", data =>
        {
            using var own = inner.CreateFlowData();
            own.AddEvidence("query.added", "1");
            own.Process();
            innerErrors = own.Errors;
        });
        using var pipeline = new PipelineBuilder(NullLoggerFactory.Instance)
            .AddFlowElementsParallel(element.Object, Reader("other", seen).Object)
            .SetSuppressProcessExceptions(false)
            .Build();
        using var data = Supplied(pipeline);

        data.Process();

        Assert.AreEqual("1", seen["inner query.added"]);
        Assert.IsTrue(innerErrors == null || innerErrors.Count == 0);
        Assert.IsEmpty(Errors(data));
    }
}
