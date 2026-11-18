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

using FiftyOne.DeviceDetection.Hash.Engine.OnPremise.FlowElements;
using FiftyOne.DeviceDetection.TestHelpers;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Linq;

namespace FiftyOne.DeviceDetection.Tests.Core;

/// <summary>
/// The on-premise builder adds usage sharing after the engine, as the last
/// Flow Element of the Pipeline.
/// See the <see href="https://github.com/51Degrees/specifications/blob/a90ba516a3bfadc58dd7a51535d83af6bdaed291/pipeline-specification/pipeline-elements/usage-sharing-element.md#position-in-the-pipeline">Specification</see>
/// </summary>
[TestClass]
public class UsageSharingLastTests
{
    [TestMethod]
    public void OnPremise_ShareUsageOn_UsageSharingIsLast()
    {
        var datafile = Utils.GetFilePath(Constants.LITE_HASH_DATA_FILE_NAME);
        var updateService = new Mock<IDataUpdateService>();

        using var pipeline = new DeviceDetectionPipelineBuilder(
                new NullLoggerFactory(), null, updateService.Object)
            .UseOnPremise(datafile.FullName, null, false)
            .SetAutoUpdate(false)
            .SetDataFileSystemWatcher(false)
            .SetShareUsage(true)
            .Build();

        Assert.HasCount(2, pipeline.FlowElements);
        Assert.IsInstanceOfType<DeviceDetectionHashEngine>(
            pipeline.FlowElements.First());
        Assert.IsInstanceOfType<ShareUsageElement>(
            pipeline.FlowElements.Last());
    }

    /// <summary>
    /// An element the caller adds after turning usage sharing on still goes
    /// before usage sharing, which the builder adds when it builds.
    /// </summary>
    [TestMethod]
    public void OnPremise_ElementAddedAfterShareUsage_UsageSharingIsLast()
    {
        var datafile = Utils.GetFilePath(Constants.LITE_HASH_DATA_FILE_NAME);
        var updateService = new Mock<IDataUpdateService>();
        var loggerFactory = new NullLoggerFactory();

        using var pipeline = new DeviceDetectionPipelineBuilder(
                loggerFactory, null, updateService.Object)
            .UseOnPremise(datafile.FullName, null, false)
            .SetAutoUpdate(false)
            .SetDataFileSystemWatcher(false)
            .SetShareUsage(true)
            .AddFlowElement(new SequenceElementBuilder(loggerFactory).Build())
            .Build();

        Assert.HasCount(3, pipeline.FlowElements);
        Assert.IsInstanceOfType<ShareUsageElement>(
            pipeline.FlowElements.Last());
    }
}
