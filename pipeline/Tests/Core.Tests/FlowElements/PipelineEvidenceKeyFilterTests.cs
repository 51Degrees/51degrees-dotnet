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
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Core.Tests.FlowElements;

/// <summary>
/// The Pipeline's Evidence key filter depends only on the Flow Elements it
/// holds, which never change after it is built. So the Pipeline builds the
/// filter once, when it is built, and every request then reads it with no
/// lock, as usage sharing and the web integration do. The filter asks each
/// element for its own filter when it is used, so building the Pipeline
/// reads no element's filter.
/// See the <see href="https://github.com/51Degrees/specifications/blob/9d12262654fe8914a5497bdacd85ca733898dd8b/pipeline-specification/features/thread-safety.md#flow-elements">Specification</see>
/// </summary>
[TestClass]
public class PipelineEvidenceKeyFilterTests
{
    private static readonly ILoggerFactory LoggerFactory = new LoggerFactory();

    private static Mock<IFlowElement> ElementWithFilter(
        string elementDataKey,
        IEvidenceKeyFilter filter)
    {
        var element = new Mock<IFlowElement>();
        element.SetupGet(e => e.ElementDataKey).Returns(elementDataKey);
        element.Setup(e => e.Properties)
            .Returns(new List<IElementPropertyMetaData>());
        element.SetupGet(e => e.EvidenceKeyFilter).Returns(filter);
        return element;
    }

    [TestMethod]
    public void EvidenceKeyFilter_BuiltWithThePipeline()
    {
        using var pipeline = new PipelineBuilder(LoggerFactory)
            .AddFlowElement(new MultiplyByFiveElement())
            .Build();

        var first = pipeline.EvidenceKeyFilter;
        Assert.IsNotNull(first,
            "The filter exists as soon as the Pipeline is built.");
        Assert.IsTrue(first.Include("value"),
            "The filter accepts the Flow Element's Evidence.");
        Assert.AreSame(first, pipeline.EvidenceKeyFilter,
            "Every read gives the same filter.");
    }

    [TestMethod]
    public void EvidenceKeyFilter_SameAnswerOnManyThreads()
    {
        using var pipeline = new PipelineBuilder(LoggerFactory)
            .AddFlowElement(new MultiplyByFiveElement())
            .Build();

        var filters = new System.Collections.Concurrent.ConcurrentBag<
            IEvidenceKeyFilter>();
        var answers = new System.Collections.Concurrent.ConcurrentBag<bool>();
        System.Threading.Tasks.Parallel.For(0, 64, _ =>
        {
            var filter = pipeline.EvidenceKeyFilter;
            filters.Add(filter);
            answers.Add(filter.Include("value"));
        });

        var expected = pipeline.EvidenceKeyFilter;
        foreach (var filter in filters)
        {
            Assert.AreSame(expected, filter,
                "Every thread reads the same filter.");
        }
        foreach (var answer in answers)
        {
            Assert.IsTrue(answer,
                "Every thread finds the Flow Element's Evidence accepted.");
        }
    }

    /// <summary>
    /// The order of a key is the one the Evidence key filter aggregator
    /// gave when the Pipeline built one, so the earlier element's inclusion
    /// list wins.
    /// </summary>
    [TestMethod]
    public void EvidenceKeyFilter_OrderFromTheEarlierElement()
    {
        var first = ElementWithFilter("first", new EvidenceKeyFilterWhitelist(
            new Dictionary<string, int> { { "header.shared", 5 } }));
        var second = ElementWithFilter("second", new EvidenceKeyFilterWhitelist(
            new Dictionary<string, int>
            {
                { "header.shared", 1 },
                { "header.second", 2 },
            }));

        using var pipeline = new PipelineBuilder(LoggerFactory)
            .AddFlowElement(first.Object)
            .AddFlowElement(second.Object)
            .Build();

        Assert.AreEqual(5, pipeline.EvidenceKeyFilter.Order("header.shared"));
        Assert.AreEqual(5, pipeline.EvidenceKeyFilter.Order("HEADER.SHARED"),
            "Keys are not case sensitive.");
        Assert.AreEqual(2, pipeline.EvidenceKeyFilter.Order("header.second"));
        Assert.IsNull(pipeline.EvidenceKeyFilter.Order("header.none"));
    }

    /// <summary>
    /// A Cloud Request Engine that cannot reach the cloud at start-up throws
    /// from its Evidence key filter until discovery succeeds, which it
    /// retries on first use. The specification says such a transient
    /// failure "MUST NOT fail the build", and that a result obtained before
    /// discovery has completed must not be kept. So building the Pipeline
    /// must not read the element's filter, and once the element answers, the
    /// Pipeline's filter includes its keys, even if the filter was asked
    /// while the element was still recovering.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/9d12262654fe8914a5497bdacd85ca733898dd8b/pipeline-specification/pipeline-elements/cloud-request-engine.md?plain=1#L263-L271">Specification</see>
    /// </summary>
    [TestMethod]
    public void EvidenceKeyFilter_ElementStillRecovering_BuildSucceedsAndKeysFollow()
    {
        const string recoveringKey = "header.x-recovering";
        var answers = false;
        var recovering = new Mock<IFlowElement>();
        recovering.SetupGet(e => e.ElementDataKey).Returns("recovering");
        recovering.Setup(e => e.Properties)
            .Returns(new List<IElementPropertyMetaData>());
        // Throws as a recovering Cloud Request Engine does, until it answers.
        recovering.SetupGet(e => e.EvidenceKeyFilter).Returns(() => answers
            ? new EvidenceKeyFilterWhitelist(new List<string> { recoveringKey })
            : throw new PipelineTemporarilyUnavailableException(
                "The cloud service is still recovering."));

        IPipeline pipeline = null;
        try
        {
            pipeline = new PipelineBuilder(LoggerFactory)
                .AddFlowElement(new MultiplyByFiveElement())
                .AddFlowElement(recovering.Object)
                .Build();
        }
        catch (PipelineTemporarilyUnavailableException ex)
        {
            Assert.Fail(
                "Build() failed because an element's Evidence key filter " +
                "was not yet available: " + ex.Message);
        }

        using (pipeline)
        {
            // A request while the element is still recovering. It may fail,
            // but it must not leave the Pipeline without the element's keys.
            try
            {
                pipeline.EvidenceKeyFilter.Include(recoveringKey);
            }
            catch (PipelineTemporarilyUnavailableException)
            {
                // The element is asked again on the next request.
            }

            answers = true;

            Assert.IsTrue(pipeline.EvidenceKeyFilter.Include(recoveringKey),
                "Once the element answers, the Pipeline's filter accepts " +
                "its Evidence.");
            Assert.IsTrue(pipeline.EvidenceKeyFilter.Include("value"),
                "The filter still accepts the other element's Evidence.");
            Assert.IsFalse(pipeline.EvidenceKeyFilter.Include("header.x-none"),
                "The filter accepts no Evidence that no element accepts.");
        }
    }

    /// <summary>
    /// Elements run in parallel have their filters combined by the
    /// parallel group. Every request thread now asks for that combined
    /// filter, so two requests that arrive together must both get a
    /// complete answer. Here the first request is held inside the second
    /// element's filter while the second request checks that element's key.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/9d12262654fe8914a5497bdacd85ca733898dd8b/pipeline-specification/features/thread-safety.md#flow-elements">Specification</see>
    /// </summary>
    [TestMethod]
    public void EvidenceKeyFilter_ParallelElements_ConcurrentFirstRequestsSeeEveryKey()
    {
        var wait = System.TimeSpan.FromSeconds(10);
        var first = ElementWithFilter("first",
            new EvidenceKeyFilterWhitelist(new List<string> { "header.a" }));
        var secondFilter =
            new EvidenceKeyFilterWhitelist(new List<string> { "header.b" });
        var armed = 0;
        using var firstRequestInside = new System.Threading.ManualResetEventSlim();
        using var secondRequestDone = new System.Threading.ManualResetEventSlim();
        var second = new Mock<IFlowElement>();
        second.SetupGet(e => e.ElementDataKey).Returns("second");
        second.Setup(e => e.Properties)
            .Returns(new List<IElementPropertyMetaData>());
        // The first read after the Pipeline is built waits here until the
        // second request has finished. Every later read answers at once.
        second.SetupGet(e => e.EvidenceKeyFilter).Returns(() =>
        {
            if (System.Threading.Interlocked.Exchange(ref armed, 0) == 1)
            {
                firstRequestInside.Set();
                secondRequestDone.Wait(wait);
            }
            return secondFilter;
        });

        using var pipeline = new PipelineBuilder(LoggerFactory)
            .AddFlowElementsParallel(first.Object, second.Object)
            .Build();
        System.Threading.Volatile.Write(ref armed, 1);

        System.Exception firstError = null;
        var firstAnswer = false;
        var firstRequest = new System.Threading.Thread(() =>
        {
            try
            {
                firstAnswer = pipeline.EvidenceKeyFilter.Include("header.b");
            }
            catch (System.Exception ex)
            {
                firstError = ex;
            }
        });
        firstRequest.Start();
        Assert.IsTrue(firstRequestInside.Wait(wait),
            "The first request reached the second element's filter.");

        System.Exception secondError = null;
        var secondAnswer = false;
        try
        {
            secondAnswer = pipeline.EvidenceKeyFilter.Include("header.b");
        }
        catch (System.Exception ex)
        {
            secondError = ex;
        }
        finally
        {
            secondRequestDone.Set();
        }
        Assert.IsTrue(firstRequest.Join(wait),
            "The first request finished.");

        Assert.IsNull(secondError, $"The second request threw: {secondError}");
        Assert.IsTrue(secondAnswer,
            "A request that arrives while another is reading the filters " +
            "still finds the second element's Evidence accepted.");
        Assert.IsNull(firstError, $"The first request threw: {firstError}");
        Assert.IsTrue(firstAnswer,
            "The first request finds the second element's Evidence accepted.");
        Assert.IsTrue(pipeline.EvidenceKeyFilter.Include("header.a"),
            "The filter accepts the first element's Evidence.");
    }

    /// <summary>
    /// A Cloud Request Engine run in parallel with another element throws
    /// from its Evidence key filter until it recovers, as one on its own
    /// does. Its keys must be included once it answers, even if a request
    /// asked while it was still recovering.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/9d12262654fe8914a5497bdacd85ca733898dd8b/pipeline-specification/pipeline-elements/cloud-request-engine.md?plain=1#L263-L271">Specification</see>
    /// </summary>
    [TestMethod]
    public void EvidenceKeyFilter_ParallelElementStillRecovering_KeysFollow()
    {
        const string recoveringKey = "header.x-recovering";
        var answers = false;
        var other = ElementWithFilter("other",
            new EvidenceKeyFilterWhitelist(new List<string> { "header.a" }));
        var recovering = new Mock<IFlowElement>();
        recovering.SetupGet(e => e.ElementDataKey).Returns("recovering");
        recovering.Setup(e => e.Properties)
            .Returns(new List<IElementPropertyMetaData>());
        // Throws as a recovering Cloud Request Engine does, until it answers.
        recovering.SetupGet(e => e.EvidenceKeyFilter).Returns(() => answers
            ? new EvidenceKeyFilterWhitelist(new List<string> { recoveringKey })
            : throw new PipelineTemporarilyUnavailableException(
                "The cloud service is still recovering."));

        using var pipeline = new PipelineBuilder(LoggerFactory)
            .AddFlowElementsParallel(other.Object, recovering.Object)
            .Build();

        // A request while the element is still recovering.
        try
        {
            pipeline.EvidenceKeyFilter.Include(recoveringKey);
        }
        catch (PipelineTemporarilyUnavailableException)
        {
            // The element is asked again on the next request.
        }

        answers = true;

        Assert.IsTrue(pipeline.EvidenceKeyFilter.Include(recoveringKey),
            "Once the element answers, the Pipeline's filter accepts its " +
            "Evidence.");
        Assert.IsTrue(pipeline.EvidenceKeyFilter.Include("header.a"),
            "The filter still accepts the other element's Evidence.");
        Assert.IsFalse(pipeline.EvidenceKeyFilter.Include("header.x-none"),
            "The filter accepts no Evidence that no element accepts.");
    }

    /// <summary>
    /// The order of a key from elements run in parallel is the one the
    /// Evidence key filter aggregator gave. The inclusion lists of the
    /// elements in the group count at the group's place among the
    /// Pipeline's inclusion lists, and every inclusion list comes before a
    /// filter of any other kind.
    /// </summary>
    [TestMethod]
    public void EvidenceKeyFilter_ParallelElements_OrderAsBefore()
    {
        var custom = new Mock<IEvidenceKeyFilter>();
        custom.Setup(f => f.Include(It.IsAny<string>())).Returns(true);
        custom.Setup(f => f.Order(It.IsAny<string>())).Returns(9);
        var before = ElementWithFilter("before", custom.Object);
        var inGroup = ElementWithFilter("ingroup",
            new EvidenceKeyFilterWhitelist(new Dictionary<string, int>
            {
                { "header.shared", 5 },
            }));
        var alsoInGroup = ElementWithFilter("alsoingroup",
            new EvidenceKeyFilterWhitelist(new Dictionary<string, int>
            {
                { "header.group", 3 },
            }));
        var after = ElementWithFilter("after",
            new EvidenceKeyFilterWhitelist(new Dictionary<string, int>
            {
                { "header.shared", 1 },
                { "header.group", 2 },
            }));

        using var pipeline = new PipelineBuilder(LoggerFactory)
            .AddFlowElement(before.Object)
            .AddFlowElementsParallel(inGroup.Object, alsoInGroup.Object)
            .AddFlowElement(after.Object)
            .Build();

        Assert.AreEqual(5, pipeline.EvidenceKeyFilter.Order("header.shared"),
            "The group's list comes before the later element's list, and " +
            "before the earlier element's filter of another kind.");
        Assert.AreEqual(3, pipeline.EvidenceKeyFilter.Order("HEADER.GROUP"),
            "Every list in the group counts, with keys not case sensitive.");
        Assert.AreEqual(9, pipeline.EvidenceKeyFilter.Order("header.other"),
            "A key in no list takes the order of the other filter.");
    }
}
