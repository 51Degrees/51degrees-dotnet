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

using FiftyOne.DeviceDetection.PropertyKeyed.Data;
using FiftyOne.DeviceDetection.Shared;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FiftyOne.Data;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;

namespace FiftyOne.DeviceDetection.PropertyKeyed.Tests
{
    /// <summary>
    /// thread-safety.md, Element Data: a write to Element Data once the
    /// Flow Element that provides it has finished fails, except by a lazy
    /// property's task. <see cref="MultiDeviceData.Profiles"/> is resolved
    /// when first read, after processing, and gives each profile its
    /// ProfileId. These tests need no data file, so they run everywhere.
    /// </summary>
    [TestClass]
    public class MultiDeviceDataFinishedTests
    {
        private const string ProfileIdName = "ProfileId";

        /// <summary>
        /// Stands in for the Hash engine. Writes one value into its device
        /// data while it processes, and finishes with it.
        /// </summary>
        private class Device :
            FlowElementBase<IDeviceData, IElementPropertyMetaData>
        {
            public Device() : base(
                NullLogger<Device>.Instance,
                (pipeline, element) => new DeviceData(pipeline))
            { }

            public override string ElementDataKey => "device";

            public override IEvidenceKeyFilter EvidenceKeyFilter =>
                new EvidenceKeyFilterWhitelist(new List<string>());

            public override IList<IElementPropertyMetaData> Properties =>
                new List<IElementPropertyMetaData>();

            protected override void ProcessInternal(IFlowData data)
            {
                data.GetOrAdd(ElementDataKeyTyped, CreateElementData)
                    ["IsMobile"] = new AspectPropertyValue<bool>(true);
            }

            protected override void ManagedResourcesCleanup() { }

            protected override void UnmanagedResourcesCleanup() { }
        }

        private class DeviceData : DeviceDataBase
        {
            public DeviceData(IPipeline pipeline) : base(
                NullLogger<AspectDataBase>.Instance,
                pipeline,
                null,
                new Mock<IMissingPropertyService>().Object)
            { }
        }

        /// <summary>
        /// Stands in for a property keyed engine. Adds the profile IDs it
        /// finds to its Element Data while it processes.
        /// </summary>
        private class Keyed :
            FlowElementBase<IMultiDeviceData, IElementPropertyMetaData>
        {
            private readonly uint[] _profileIds;

            public Keyed(PropertyKeyedDataSet dataSet, params uint[] ids) :
                base(
                    NullLogger<Keyed>.Instance,
                    (pipeline, element) => new MultiDeviceData(
                        NullLogger<MultiDeviceData>.Instance,
                        pipeline,
                        null,
                        new Mock<IMissingPropertyService>().Object,
                        dataSet))
            {
                _profileIds = ids;
            }

            public override string ElementDataKey => "keyed";

            public override IEvidenceKeyFilter EvidenceKeyFilter =>
                new EvidenceKeyFilterWhitelist(new List<string>());

            public override IList<IElementPropertyMetaData> Properties =>
                new List<IElementPropertyMetaData>();

            protected override void ProcessInternal(IFlowData data)
            {
                var keyed = data.GetOrAdd(
                    ElementDataKeyTyped,
                    CreateElementData);
                foreach (var id in _profileIds)
                {
                    keyed.AddProfileId(id);
                }
            }

            protected override void ManagedResourcesCleanup() { }

            protected override void UnmanagedResourcesCleanup() { }
        }

        private static PropertyKeyedDataSet DataSet() =>
            new PropertyKeyedDataSet(
                new PipelineBuilder(NullLoggerFactory.Instance)
                    .AddFlowElement(new Device())
                    .Build(),
                "device",
                "test",
                new Mock<IFlowElement>().Object,
                new List<PropertyKeyedIndex>(),
                (properties, element) =>
                    new List<IFiftyOneAspectPropertyMetaData>());

        /// <summary>
        /// The profiles are read after the Hash engine has finished with
        /// each one's device data, and each still gets its ProfileId.
        /// </summary>
        [TestMethod]
        public void Profiles_ReadAfterProcessing_HaveTheirProfileId()
        {
            using var dataSet = DataSet();
            using var pipeline = new PipelineBuilder(
                    NullLoggerFactory.Instance)
                .AddFlowElement(new Keyed(dataSet, 42, 43))
                .Build();
            using var data = pipeline.CreateFlowData();
            data.Process();

            var profiles = data.Get<IMultiDeviceData>().Profiles;

            Assert.HasCount(2, profiles);
            Assert.AreEqual("42",
                ((IAspectPropertyValue)profiles[0][ProfileIdName]).Value);
            Assert.AreEqual("43",
                ((IAspectPropertyValue)profiles[1][ProfileIdName]).Value);
            Assert.IsTrue(profiles[0].IsMobile.Value);
        }

        /// <summary>
        /// Adding a profile ID or a profile once the engine has finished is
        /// a write, so it fails like any other.
        /// </summary>
        [TestMethod]
        public void AddingProfiles_AfterProcessing_Fails()
        {
            using var dataSet = DataSet();
            using var pipeline = new PipelineBuilder(
                    NullLoggerFactory.Instance)
                .AddFlowElement(new Keyed(dataSet, 42))
                .Build();
            using var data = pipeline.CreateFlowData();
            data.Process();
            var keyed = (MultiDeviceData)data.Get<IMultiDeviceData>();
            var profile = keyed.Profiles[0];

            Assert.ThrowsExactly<PipelineException>(
                () => keyed.AddProfileId(43));
            Assert.ThrowsExactly<PipelineException>(
                () => keyed.AddProfile(profile));
            Assert.HasCount(1, keyed.Profiles);
        }
    }
}

