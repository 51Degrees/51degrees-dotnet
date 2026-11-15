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
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Core.Tests.FlowElements;

/// <summary>
/// A built Pipeline keeps the Flow Elements it was built with, and disposes
/// them only when it is set to.
/// </summary>
[TestClass]
public class PipelineImmutableTests
{
    private static readonly ILoggerFactory LoggerFactory = new LoggerFactory();

    private static Mock<IFlowElement> Element(string key)
    {
        var element = new Mock<IFlowElement>();
        element.SetupGet(e => e.ElementDataKey).Returns(key);
        element.Setup(e => e.Properties)
            .Returns(new List<IElementPropertyMetaData>());
        return element;
    }

    [TestMethod]
    public void FlowElements_CannotBeChangedThroughTheList()
    {
        using var pipeline = new PipelineBuilder(LoggerFactory)
            .AddFlowElement(Element("one").Object)
            .Build();

        var asCollection = pipeline.FlowElements as ICollection<IFlowElement>;
        var readOnly = asCollection == null || asCollection.IsReadOnly;
        Assert.IsTrue(readOnly,
            "The Pipeline's list of Flow Elements can be changed.");
    }

    [TestMethod]
    public void Builder_AddAfterBuild_LeavesThePipelineAsBuilt()
    {
        var builder = new PipelineBuilder(LoggerFactory)
            .AddFlowElement(Element("one").Object);
        using var pipeline = builder.Build();

        builder.AddFlowElement(Element("two").Object);

        var count = pipeline.FlowElements.Count;
        Assert.AreEqual(1, count,
            "A Flow Element added to the builder after it built the " +
            "Pipeline is in the Pipeline.");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Dispose_DisposesTheElementsOnlyWhenSetTo(bool autoDispose)
    {
        var element = Element("one");
        var disposable = element.As<IDisposable>();
        var pipeline = new PipelineBuilder(LoggerFactory)
            .AddFlowElement(element.Object)
            .SetAutoDisposeElements(autoDispose)
            .Build();

        pipeline.Dispose();

        disposable.Verify(d => d.Dispose(),
            autoDispose ? Times.Once() : Times.Never());
    }
}
