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

using FiftyOne.DeviceDetection.Cloud.Data;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace FiftyOne.DeviceDetection.Cloud.Tests;

/// <summary>
/// thread-safety.md, Element Data: a write to Element Data once the Flow
/// Element that provides it has finished fails. Adding a profile to
/// <see cref="MultiDeviceDataCloud"/> is a write.
/// </summary>
[TestClass]
public class MultiDeviceDataCloudFinishedTests
{
    /// <summary>
    /// Stands in for the hardware profile cloud engine, adding one profile
    /// while it processes.
    /// </summary>
    private class Profiles :
        FlowElementBase<MultiDeviceDataCloud, IElementPropertyMetaData>
    {
        public Profiles() : base(
            NullLogger<Profiles>.Instance,
            (pipeline, element) => new MultiDeviceDataCloud(
                NullLogger<AspectDataBase>.Instance, pipeline, null))
        { }

        public override string ElementDataKey => "hardware";

        public override IEvidenceKeyFilter EvidenceKeyFilter =>
            new EvidenceKeyFilterWhitelist(new List<string>());

        public override IList<IElementPropertyMetaData> Properties =>
            new List<IElementPropertyMetaData>();

        protected override void ProcessInternal(IFlowData data)
        {
            data.GetOrAdd(ElementDataKeyTyped, CreateElementData)
                .AddProfile(new DeviceDataCloud(
                    NullLogger<AspectDataBase>.Instance, data.Pipeline, null,
                    null));
        }

        protected override void ManagedResourcesCleanup() { }

        protected override void UnmanagedResourcesCleanup() { }
    }

    [TestMethod]
    public void AddProfile_AfterProcessing_Fails()
    {
        using var pipeline = new PipelineBuilder(NullLoggerFactory.Instance)
            .AddFlowElement(new Profiles())
            .Build();
        using var data = pipeline.CreateFlowData();
        data.Process();
        var profiles = (MultiDeviceDataCloud)data.Get("hardware");
        Assert.HasCount(1, profiles.Profiles);

        Assert.ThrowsExactly<PipelineException>(
            () => profiles.AddProfile(profiles.Profiles[0]));
        Assert.HasCount(1, profiles.Profiles);
    }
}