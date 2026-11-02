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

using FiftyOne.IpIntelligence.Engine.OnPremise.FlowElements;
using FiftyOne.IpIntelligence.Examples;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using ExampleConstants = FiftyOne.IpIntelligence.Examples.Constants;

namespace FiftyOne.IpIntelligence.Example.Tests.OnPremise;

/// <summary>
/// Checks that the IP address values <see cref="System.Net.IPAddress"/>
/// accepts but which are not ordinary addresses, such as a single number or
/// an address with a scope, pass through the engine without an exception.
/// The Compare example only rejects a truth record whose IP address can not
/// be parsed, so anything that parses must not stop a consumer.
/// </summary>
[TestClass]
public class TestCompareOddIps
{
    private static string _dataFile;
    private static string _skipReason;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        _dataFile = ExampleUtils.FindDataFile(
            ExampleConstants.ENTERPRISE_IPI_DATA_FILE_NAME);
        if (String.IsNullOrWhiteSpace(_dataFile) ||
            File.Exists(_dataFile) == false)
        {
            _skipReason =
                "This test requires an on-premise IP Intelligence data " +
                $"file. Place a '{ExampleConstants.ENTERPRISE_IPI_DATA_FILE_NAME}' " +
                "file in the repository or set the " +
                $"'{ExampleConstants.IP_INTELLIGENCE_DATA_FILE_ENV_VAR}' " +
                "environment variable to its path.";
        }
    }

    [TestMethod]
    [DataRow("1")]
    [DataRow("1.2")]
    [DataRow("1.2.3")]
    [DataRow("001.002.003.004")]
    [DataRow("0.0.0.0")]
    [DataRow("::")]
    [DataRow("fe80::1%eth0")]
    [DataRow("fe80::1%12")]
    public void OddIp_DoesNotThrow(string ip)
    {
        if (_skipReason != null)
        {
            Assert.Inconclusive(_skipReason);
        }
        using var loggerFactory = new LoggerFactory();
        using var engine = new IpiOnPremiseEngineBuilder(loggerFactory)
            .SetPerformanceProfile(PerformanceProfiles.LowMemory)
            .SetAutoUpdate(false)
            .SetDataFileSystemWatcher(false)
            .Build(_dataFile, false);
        using var pipeline = new PipelineBuilder(loggerFactory)
            .AddFlowElement(engine)
            .SetAutoDisposeElements(false)
            .Build();

        using var flowData = pipeline.CreateFlowData();
        flowData.AddEvidence("query.client-ip", ip);
        flowData.Process();
        var data = flowData.Get<IIpIntelligenceData>();

        // The values are read the same way the Compare example reads them.
        // Whether they have values depends on the data file, only that they
        // can be read matters here.
        _ = data.Latitude.HasValue;
        _ = data.Longitude.HasValue;
        _ = data.Areas.HasValue;
    }
}
