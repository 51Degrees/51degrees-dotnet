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
using FiftyOne.Pipeline.Core.Tests.HelperClasses;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace FiftyOne.Pipeline.Core.Tests.FlowElements;

/// <summary>
/// thread-safety.md, Flow Data, and parallel-processing.md, How a group
/// uses the Flow Data. The members of a parallel group add their own
/// Element Data with one compare and swap, so no member waits for another.
/// They add their own errors and warnings under a lock. The parallel
/// element adds the exceptions members throw once every member has
/// stopped. The tests run one at a time, so that one test's tasks do not
/// hold up another's on the thread pool.
/// </summary>
[TestClass]
[DoNotParallelize]
public class ParallelGroupFlowDataTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

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

    private static IPipeline Group(params Mock<IFlowElement>[] members) =>
        new PipelineBuilder(NullLoggerFactory.Instance)
            .AddFlowElementsParallel(members.Select(m => m.Object).ToArray())
            .SetSuppressProcessExceptions(true)
            .Build();

    /// <summary>
    /// The slow member is still making its Element Data when the quick
    /// member adds its own. The quick member's add must not wait for it.
    /// </summary>
    [TestMethod]
    public void Members_AddElementData_WithoutWaitingForEachOther()
    {
        using var making = new ManualResetEventSlim();
        using var quickAdded = new ManualResetEventSlim();
        var slowSawQuick = false;
        var slow = Element("slow", data => data.GetOrAdd("slow", p =>
        {
            making.Set();
            slowSawQuick = quickAdded.Wait(Wait);
            return new TestElementData(p);
        }));
        var quick = Element("quick", data =>
        {
            making.Wait(Wait);
            data.GetOrAdd("quick", p => new TestElementData(p));
            quickAdded.Set();
        });
        using var pipeline = Group(slow, quick);
        using var data = pipeline.CreateFlowData();

        data.Process();

        Assert.IsTrue(slowSawQuick,
            "The quick member's add waited for the slow member's.");
        Assert.IsNotNull(data.Get("slow"));
        Assert.IsNotNull(data.Get("quick"));
        Assert.IsEmpty(data.Errors ?? new List<IFlowError>());
    }

    /// <summary>
    /// Many members add their Element Data at the same time, and every
    /// instance is there afterwards.
    /// </summary>
    [TestMethod]
    public void ManyMembers_AddElementDataAtOnce_NoneLost()
    {
        var names = Enumerable.Range(0, 8).Select(i => $"member{i}").ToArray();
        var members = names.Select(name => Element(name, data =>
            data.GetOrAdd(name, p => new TestElementData(p)))).ToArray();
        using var pipeline = Group(members);

        for (var i = 0; i < 200; i++)
        {
            using var data = pipeline.CreateFlowData();
            data.Process();
            CollectionAssert.AreEquivalent(names, data.GetDataKeys().ToArray());
        }
    }

    /// <summary>
    /// Members record errors and warnings at the same time, and none is
    /// lost.
    /// </summary>
    [TestMethod]
    public void Members_RecordErrorsAndWarningsAtOnce_NoneLost()
    {
        const int each = 500;
        var names = new[] { "a", "b", "c", "d" };
        Mock<IFlowElement>[] members = null;
        members = names.Select((name, index) => Element(name, data =>
        {
            for (var i = 0; i < each; i++)
            {
                data.AddWarning($"{name}{i}", null);
                data.AddError(
                    new InvalidOperationException($"{name}{i}"),
                    members[index].Object,
                    false,
                    false);
            }
        })).ToArray();
        using var pipeline = Group(members);
        using var data = pipeline.CreateFlowData();

        data.Process();

        Assert.HasCount(names.Length * each, data.GetWarnings());
        Assert.HasCount(names.Length * each, data.Errors);
    }

    /// <summary>
    /// The second member throws at once. The first member, still running,
    /// sees no error, and the errors are in the order of the group's
    /// members once every member has stopped.
    /// </summary>
    [TestMethod]
    public void MemberExceptions_AddedOnceEveryMemberHasStopped_InOrder()
    {
        var firstSawAnError = false;
        using var secondThrowing = new ManualResetEventSlim();
        var first = Element("first", data =>
        {
            secondThrowing.Wait(Wait);
            // Give the group time to add the second member's exception, if
            // it adds it while this member is still running. A busy thread
            // pool can delay that, so allow a few seconds.
            var until = DateTime.UtcNow.AddSeconds(3);
            while (firstSawAnError == false && DateTime.UtcNow < until)
            {
                firstSawAnError = (data.Errors?.Count ?? 0) > 0;
                Thread.Sleep(10);
            }
            throw new InvalidOperationException("first");
        });
        var second = Element("second", data =>
        {
            secondThrowing.Set();
            throw new InvalidOperationException("second");
        });
        using var pipeline = Group(first, second);
        using var data = pipeline.CreateFlowData();

        data.Process();

        Assert.IsFalse(firstSawAnError,
            "An exception was added while another member was running.");
        CollectionAssert.AreEqual(
            new[] { "first", "second" },
            data.Errors.Select(e => e.ExceptionData.Message).ToArray());
    }

    /// <summary>
    /// Outside a group, the Element Data is listed in the order it was
    /// added, which is the order of the Pipeline.
    /// </summary>
    [TestMethod]
    public void ElementData_ListedInTheOrderAdded()
    {
        var names = new[] { "c", "a", "b" };
        var builder = new PipelineBuilder(NullLoggerFactory.Instance);
        foreach (var name in names)
        {
            builder.AddFlowElement(Element(name, data =>
                data.GetOrAdd(name, p => new TestElementData(p))).Object);
        }
        using var pipeline = builder.Build();
        using var data = pipeline.CreateFlowData();

        data.Process();

        CollectionAssert.AreEqual(names, data.GetDataKeys().ToArray());
        CollectionAssert.AreEqual(
            names, data.ElementDataAsDictionary().Keys.ToArray());
    }
}
