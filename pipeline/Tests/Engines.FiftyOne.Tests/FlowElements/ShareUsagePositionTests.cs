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

using FiftyOne.Pipeline.Core.Configuration;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.Engines.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Net.Http;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Tests.FlowElements;

/// <summary>
/// The usage sharing element is the last Flow Element of a Pipeline and is
/// never in a parallel group. A builder that adds it adds it last, and a
/// Pipeline that puts it anywhere else is refused, whether it is built in
/// code or from a configuration.
/// See the <see href="https://github.com/51Degrees/specifications/blob/a90ba516a3bfadc58dd7a51535d83af6bdaed291/pipeline-specification/pipeline-elements/usage-sharing-element.md#position-in-the-pipeline">Specification</see>
/// </summary>
[TestClass]
public class ShareUsagePositionTests
{
    /// <summary>
    /// Words the refusal must carry, as the specification asks for an
    /// error saying both.
    /// </summary>
    private const string MustBeLast =
        "usage sharing element must be the last Flow Element";
    private const string OutsideGroups = "outside any parallel group";

    private LoggerFactory _loggerFactory;
    private HttpClient _http;

    [TestInitialize]
    public void Init()
    {
        _loggerFactory = new LoggerFactory();
        _http = new HttpClient(new UsageMessageCapture());
    }

    [TestCleanup]
    public void Cleanup()
    {
        _http.Dispose();
        _loggerFactory.Dispose();
    }

    private ShareUsageElement NewShareUsage() =>
        new ShareUsageBuilder(_loggerFactory, _http).Build();

    private IFlowElement NewEngine() =>
        new EmptyEngineBuilder(_loggerFactory).Build();

    private IFlowElement NewSetHeaders() =>
        new SetHeadersElementBuilder(_loggerFactory).Build();

    /// <summary>
    /// The builders a configuration names, made by the service provider.
    /// </summary>
    private ServiceProvider NewServices() =>
        new ServiceCollection()
            .AddSingleton<ILoggerFactory>(_loggerFactory)
            .AddSingleton(_http)
            .AddSingleton<ShareUsageBuilder>()
            .AddSingleton<EmptyEngineBuilder>()
            .AddSingleton<SetHeadersElementBuilder>()
            .BuildServiceProvider();

    private static void AssertRefused(Action build)
    {
        var ex = Assert.ThrowsExactly<PipelineConfigurationException>(build);
        Assert.Contains(MustBeLast, ex.Message);
        Assert.Contains(OutsideGroups, ex.Message);
    }

    private static void AssertLast(IPipeline pipeline)
    {
        Assert.IsInstanceOfType<ShareUsageElement>(
            pipeline.FlowElements.Last(),
            "The usage sharing element should be the last Flow Element.");
        Assert.AreEqual(
            1,
            pipeline.FlowElements.Count(e => e is ShareUsageElement),
            "There should be one usage sharing element.");
    }

    /// <summary>
    /// The builder that adds usage sharing adds it after the elements
    /// given to it in code.
    /// </summary>
    [TestMethod]
    public void FiftyOnePipelineBuilder_InCode_AddsUsageSharingLast()
    {
        using var pipeline = new FiftyOnePipelineBuilder(_loggerFactory)
            .SetShareUsage(true)
            .AddFlowElement(NewEngine())
            .AddFlowElement(NewSetHeaders())
            .Build();

        AssertLast(pipeline);
    }

    /// <summary>
    /// The builder that adds usage sharing adds it after the elements a
    /// configuration names.
    /// </summary>
    [TestMethod]
    public void FiftyOnePipelineBuilder_FromConfiguration_AddsUsageSharingLast()
    {
        var options = new PipelineOptions();
        options.BuildParameters.Add("ShareUsage", true);
        options.Elements.Add(new ElementOptions() { BuilderName = "EmptyEngine" });
        options.Elements.Add(new ElementOptions() { BuilderName = "SetHeadersElement" });
        using var services = NewServices();

        using var pipeline = new FiftyOnePipelineBuilder(_loggerFactory, services)
            .BuildFromConfiguration(options);

        AssertLast(pipeline);
    }

    /// <summary>
    /// A usage sharing element added last, outside any group, is built.
    /// </summary>
    [TestMethod]
    public void PipelineBuilder_InCode_UsageSharingLast_Builds()
    {
        using var pipeline = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(NewEngine())
            .AddFlowElement(NewShareUsage())
            .Build();

        AssertLast(pipeline);
    }

    /// <summary>
    /// A usage sharing element added before another element is refused.
    /// </summary>
    [TestMethod]
    public void PipelineBuilder_InCode_UsageSharingBeforeAnotherElement_Refused()
    {
        using var shareUsage = NewShareUsage();
        using var engine = NewEngine();
        var builder = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(shareUsage)
            .AddFlowElement(engine);

        AssertRefused(() => builder.Build());
    }

    /// <summary>
    /// A usage sharing element in a parallel group is refused, even when
    /// the group is the last element.
    /// </summary>
    [TestMethod]
    public void PipelineBuilder_InCode_UsageSharingInParallelGroup_Refused()
    {
        using var engine = NewEngine();
        using var shareUsage = NewShareUsage();
        using var setHeaders = NewSetHeaders();
        var builder = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(engine)
            .AddFlowElementsParallel(shareUsage, setHeaders);

        AssertRefused(() => builder.Build());
    }

    /// <summary>
    /// A configuration that names usage sharing before another element is
    /// refused.
    /// </summary>
    [TestMethod]
    public void PipelineBuilder_FromConfiguration_UsageSharingBeforeAnotherElement_Refused()
    {
        var options = new PipelineOptions();
        options.Elements.Add(new ElementOptions() { BuilderName = "ShareUsage" });
        options.Elements.Add(new ElementOptions() { BuilderName = "EmptyEngine" });
        using var services = NewServices();
        var builder = new PipelineBuilder(_loggerFactory, services);

        AssertRefused(() => builder.BuildFromConfiguration(options));
    }

    /// <summary>
    /// A configuration that puts usage sharing in a parallel group is
    /// refused, even when the group is the last element.
    /// </summary>
    [TestMethod]
    public void PipelineBuilder_FromConfiguration_UsageSharingInParallelGroup_Refused()
    {
        var options = new PipelineOptions();
        options.Elements.Add(new ElementOptions() { BuilderName = "EmptyEngine" });
        var group = new ElementOptions();
        group.SubElements.Add(new ElementOptions() { BuilderName = "ShareUsage" });
        group.SubElements.Add(new ElementOptions() { BuilderName = "SetHeadersElement" });
        options.Elements.Add(group);
        using var services = NewServices();
        var builder = new PipelineBuilder(_loggerFactory, services);

        AssertRefused(() => builder.BuildFromConfiguration(options));
    }

    /// <summary>
    /// The builder that adds usage sharing does not move one that a
    /// configuration names before another element. It refuses the
    /// Pipeline instead.
    /// </summary>
    [TestMethod]
    public void FiftyOnePipelineBuilder_FromConfiguration_UsageSharingFirst_Refused()
    {
        var options = new PipelineOptions();
        options.BuildParameters.Add("ShareUsage", true);
        options.Elements.Add(new ElementOptions() { BuilderName = "ShareUsage" });
        options.Elements.Add(new ElementOptions() { BuilderName = "EmptyEngine" });
        using var services = NewServices();
        var builder = new FiftyOnePipelineBuilder(_loggerFactory, services);

        AssertRefused(() => builder.BuildFromConfiguration(options));
    }
}
