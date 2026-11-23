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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Core.Tests.FlowElements;

/// <summary>
/// The list <see cref="IFlowElement.Properties"/> returns, which is a
/// read only projection of <see cref="FlowElementBase{T, TMeta}.Properties"/>
/// made once for each list that property returns.
/// </summary>
[TestClass]
public class FlowElementPropertiesTests
{
    private class Element :
        FlowElementBase<IElementData, IElementPropertyMetaData>
    {
        public Element() : base(null) { }

        public IList<IElementPropertyMetaData> Current { get; set; }

        public override string ElementDataKey => "properties";

        public override IEvidenceKeyFilter EvidenceKeyFilter =>
            new EvidenceKeyFilterWhitelist(new List<string>());

        public override IList<IElementPropertyMetaData> Properties =>
            Current;

        protected override void ProcessInternal(IFlowData data) { }

        protected override void ManagedResourcesCleanup() { }

        protected override void UnmanagedResourcesCleanup() { }
    }

    private static List<IElementPropertyMetaData> NewList(
        Element element, params string[] names)
    {
        var list = new List<IElementPropertyMetaData>();
        foreach (var name in names)
        {
            list.Add(new ElementPropertyMetaData(
                element, name, typeof(string), true));
        }
        return list;
    }

    [TestMethod]
    public void FlowElement_Properties_SameListReturnsSameInstance()
    {
        using var element = new Element();
        element.Current = NewList(element, "one", "two");
        IFlowElement flowElement = element;

        var first = flowElement.Properties;
        var second = flowElement.Properties;

        Assert.AreSame(first, second);
        CollectionAssert.AreEqual(
            (List<IElementPropertyMetaData>)element.Current,
            new List<IElementPropertyMetaData>(first));
    }

    [TestMethod]
    public void FlowElement_Properties_NewListReturnsNewInstance()
    {
        using var element = new Element();
        element.Current = NewList(element, "one");
        IFlowElement flowElement = element;
        var first = flowElement.Properties;

        element.Current = NewList(element, "two", "three");
        var second = flowElement.Properties;

        Assert.AreNotSame(first, second);
        Assert.AreEqual(1, first.Count);
        Assert.AreEqual(2, second.Count);
        Assert.AreEqual("two", second[0].Name);
    }

    [TestMethod]
    public void FlowElement_Properties_IsReadOnly()
    {
        using var element = new Element();
        element.Current = NewList(element, "one");
        IFlowElement flowElement = element;

        Assert.IsTrue(flowElement.Properties.IsReadOnly);
        Assert.ThrowsExactly<NotSupportedException>(() =>
            flowElement.Properties.Add(element.Current[0]));
    }

    [TestMethod]
    public void FlowElement_Pipelines_ShowsPipelinesAddedLater()
    {
        using var element = new Element();
        var before = element.Pipelines;

        element.AddPipeline(null);

        Assert.AreSame(before, element.Pipelines);
        Assert.AreEqual(1, before.Count);
    }
}
