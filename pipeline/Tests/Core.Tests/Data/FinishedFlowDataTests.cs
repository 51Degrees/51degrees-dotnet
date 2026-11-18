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
using FiftyOne.Pipeline.Core.TypedMap;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Core.Tests.Data;

/// <summary>
/// thread-safety.md, Flow Data: "Adding Evidence, Element Data, errors or
/// warnings to a Flow Data that has finished processing MUST fail with an
/// error." Writes before and during processing are unaffected, and reads,
/// including asking for Element Data that is already there, still work.
/// </summary>
[TestClass]
public class FinishedFlowDataTests
{
    /// <summary>
    /// An element that adds Evidence, its own Element Data, an error and a
    /// warning while it processes, and then throws if asked to.
    /// </summary>
    private static Mock<IFlowElement> Writer(bool fail)
    {
        var mock = new Mock<IFlowElement>();
        mock.SetupGet(e => e.ElementDataKey).Returns("writer");
        mock.SetupGet(e => e.Properties)
            .Returns(new List<IElementPropertyMetaData>());
        mock.SetupGet(e => e.EvidenceKeyFilter)
            .Returns(new EvidenceKeyFilterWhitelist(new List<string>()));
        mock.Setup(e => e.Process(It.IsAny<IFlowData>())).Callback(
            (IFlowData data) =>
            {
                data.AddEvidence("query.added", "during");
                data.GetOrAdd("writer", p => new TestElementData(p));
                data.AddError(
                    new InvalidOperationException("recorded"),
                    mock.Object,
                    false,
                    false);
                data.AddWarning("recorded", mock.Object);
                if (fail)
                {
                    throw new InvalidOperationException("thrown");
                }
            });
        return mock;
    }

    private static void AssertReadOnly(IFlowData data)
    {
        Assert.ThrowsExactly<PipelineException>(
            () => data.AddEvidence("query.after", "value"),
            "Adding Evidence");
        Assert.ThrowsExactly<PipelineException>(
            () => data.AddEvidence(new Dictionary<string, object>
            {
                { "query.after", "value" },
            }),
            "Adding a dictionary of Evidence");
        Assert.ThrowsExactly<PipelineException>(
            () => data.GetEvidence()["query.after"] = "value",
            "Writing the Evidence directly");
        Assert.ThrowsExactly<PipelineException>(
            () => data.GetOrAdd("other", p => new TestElementData(p)),
            "Adding Element Data by name");
        Assert.ThrowsExactly<PipelineException>(
            () => data.GetOrAdd(
                new TypedKey<TestElementData>("other"),
                p => new TestElementData(p)),
            "Adding Element Data by typed key");
        Assert.ThrowsExactly<NotSupportedException>(
            () => data.ElementDataAsDictionary().Add(
                "other", new TestElementData(data.Pipeline)),
            "Adding Element Data through the dictionary");
        Assert.ThrowsExactly<PipelineException>(
            () => data.AddError(new InvalidOperationException("after"), null),
            "Adding an error");
        Assert.ThrowsExactly<NotSupportedException>(
            () => data.Errors.Add(new FlowError(
                new InvalidOperationException("after"), null)),
            "Adding an error through the list of errors");
        Assert.ThrowsExactly<PipelineException>(
            () => data.AddWarning("after", null),
            "Adding a warning");

        // Reads still work, including asking for Element Data that is
        // already there.
        Assert.AreSame(
            data.Get("writer"),
            data.GetOrAdd("writer", p => new TestElementData(p)));
        Assert.IsTrue(data.TryGetEvidence("query.added", out string added));
        Assert.AreEqual("during", added);
        Assert.IsFalse(data.TryGetEvidence("query.after", out string _));
        Assert.HasCount(1, data.GetWarnings());
        Assert.IsFalse(data.ElementDataAsDictionary().ContainsKey("other"));
    }

    /// <summary>
    /// Writes during processing work, and every write after it fails.
    /// </summary>
    [TestMethod]
    public void FinishedFlowData_RefusesWrites()
    {
        var writer = Writer(false);
        using var pipeline = new PipelineBuilder(NullLoggerFactory.Instance)
            .AddFlowElement(writer.Object)
            .Build();
        using var data = pipeline.CreateFlowData();

        data.Process();

        Assert.HasCount(1, data.Errors);
        AssertReadOnly(data);
        Assert.HasCount(1, data.Errors);
    }

    /// <summary>
    /// A Flow Data whose processing ended with an exception is finished
    /// too.
    /// </summary>
    [TestMethod]
    public void FlowDataThatThrew_RefusesWrites()
    {
        var writer = Writer(true);
        using var pipeline = new PipelineBuilder(NullLoggerFactory.Instance)
            .AddFlowElement(writer.Object)
            .Build();
        using var data = pipeline.CreateFlowData();

        Assert.Throws<Exception>(() => data.Process());

        AssertReadOnly(data);
    }

    /// <summary>
    /// A Flow Data processed by calling the public Process method of the
    /// Pipeline class is finished too once that returns.
    /// </summary>
    [TestMethod]
    public void FlowDataProcessedByThePipeline_RefusesWrites()
    {
        var writer = Writer(false);
        using var pipeline = new PipelineBuilder(NullLoggerFactory.Instance)
            .AddFlowElement(writer.Object)
            .Build();
        using var data = pipeline.CreateFlowData();

        ((Core.FlowElements.Pipeline)pipeline).Process(data);

        Assert.ThrowsExactly<PipelineException>(
            () => data.AddEvidence("query.after", "value"),
            "Adding Evidence");
        Assert.ThrowsExactly<PipelineException>(
            () => data.GetEvidence()["query.after"] = "value",
            "Writing the Evidence directly");
        Assert.ThrowsExactly<PipelineException>(
            () => data.GetOrAdd("other", p => new TestElementData(p)),
            "Adding Element Data");
        Assert.ThrowsExactly<PipelineException>(
            () => data.AddError(new InvalidOperationException("after"), null),
            "Adding an error");
        Assert.ThrowsExactly<NotSupportedException>(
            () => data.Errors.Add(new FlowError(
                new InvalidOperationException("after"), null)),
            "Adding an error through the list of errors");
        Assert.ThrowsExactly<PipelineException>(
            () => data.AddWarning("after", null),
            "Adding a warning");
        Assert.HasCount(1, data.Errors);
    }
}
