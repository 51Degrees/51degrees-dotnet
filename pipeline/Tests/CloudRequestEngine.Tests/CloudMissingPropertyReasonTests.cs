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

using FiftyOne.Pipeline.CloudRequestEngine.Services;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;

namespace FiftyOne.Pipeline.CloudRequestEngine.Tests
{
    /// <summary>
    /// The reason a cloud engine that has no property metadata gives for a
    /// missing property. Its element data type has a typed accessor for
    /// 'ismobile', which only counts for an on-premise engine.
    /// </summary>
    [TestClass]
    public class CloudMissingPropertyReasonTests
    {
        private const string PROPERTY = "ismobile";

        /// <summary>
        /// A cloud engine whose request for its property list failed gives
        /// CloudRequestFailed, whether the request failed or was held back
        /// while the request engine recovers from earlier failures.
        /// </summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void NoMetadata_PropertyListRequestFailed_CloudRequestFailed(
            bool recovering)
        {
            var engine = new CloudAspectEngineBaseTests.TestInstance();
            var requestEngine =
                new Mock<CloudAspectEngineBaseTests.TestRequestEngine>()
                {
                    CallBase = true
                };
            Exception failure = recovering ?
                new CloudRequestEngineTemporarilyUnavailableException(
                    "Recovering from an earlier failure.") :
                new CloudRequestException("The request failed.");
            requestEngine.Setup(e => e.PublicProperties).Throws(failure);
            using var pipeline = new PipelineBuilder(new LoggerFactory())
                .AddFlowElement(requestEngine.Object)
                .AddFlowElement(engine)
                .Build();
            Assert.IsFalse(engine.HasLoadedProperties);

            var result = MissingPropertyServiceCloud.Instance
                .GetMissingPropertyReason(PROPERTY, engine);

            Assert.AreEqual(
                MissingPropertyReason.CloudRequestFailed,
                result.Reason,
                result.Description);
            Assert.AreEqual(
                $"Property '{PROPERTY}' not found in data for element " +
                $"'{engine.ElementDataKey}'. This is because an upstream " +
                "cloud request failed, so no data was available to " +
                "populate the property.",
                result.Description);
        }

        /// <summary>
        /// A cloud engine that has not loaded its property metadata, and
        /// whose request for it has not failed, gives Unknown. It does not
        /// tell a cloud user to upgrade a data file they do not have.
        /// </summary>
        [TestMethod]
        public void NoMetadata_NotRequested_Unknown()
        {
            var engine = new CloudAspectEngineBaseTests.TestInstance();
            Assert.IsFalse(engine.HasLoadedProperties);

            var result = MissingPropertyServiceCloud.Instance
                .GetMissingPropertyReason(PROPERTY, engine);

            Assert.AreEqual(
                MissingPropertyReason.Unknown,
                result.Reason,
                result.Description);
        }
    }
}
