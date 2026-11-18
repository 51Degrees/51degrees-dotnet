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
using FiftyOne.IpIntelligence.TestHelpers;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Linq;

namespace FiftyOne.IpIntelligence.Tests.Core;

/// <summary>
/// The on-premise builder adds usage sharing after the engine, as the last
/// Flow Element of the Pipeline.
/// See the <see href="https://github.com/51Degrees/specifications/blob/a90ba516a3bfadc58dd7a51535d83af6bdaed291/pipeline-specification/pipeline-elements/usage-sharing-element.md#position-in-the-pipeline">Specification</see>
/// </summary>
[TestClass]
public class UsageSharingLastTests
{
    /// <summary>
    /// The free data file, which every build has.
    /// </summary>
    private const string AsnDataFileName = "51Degrees-IPIV4AsnIpiV41.ipi";

    [TestMethod]
    public void OnPremise_ShareUsageOn_UsageSharingIsLast()
    {
        var datafile = Utils.GetFilePath(AsnDataFileName);
        var updateService = new Mock<IDataUpdateService>();

        using var pipeline = new IpiPipelineBuilder(
                new NullLoggerFactory(), null, updateService.Object)
            .UseOnPremise(datafile.FullName, null, false)
            .SetAutoUpdate(false)
            .SetDataFileSystemWatcher(false)
            .SetShareUsage(true)
            .Build();

        Assert.HasCount(2, pipeline.FlowElements);
        Assert.IsInstanceOfType<IpiOnPremiseEngine>(
            pipeline.FlowElements.First());
        Assert.IsInstanceOfType<ShareUsageElement>(
            pipeline.FlowElements.Last());
    }
}
