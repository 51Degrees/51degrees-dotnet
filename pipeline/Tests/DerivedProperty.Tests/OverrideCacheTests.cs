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

using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.DerivedProperty.FlowElements;
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace FiftyOne.Pipeline.DerivedProperty.Tests;

/// <summary>
/// thread-safety.md, Aspect Data: "An Engine whose Element Data another
/// Flow Element also writes ... MUST NOT allow a cache to be added either."
/// A script that replaces a value in an engine's Element Data writes it,
/// so a pipeline where that engine has a results cache is refused.
/// </summary>
[TestClass]
public class OverrideCacheTests
{
    private const string OverrideScript =
        "Format: 1\n" +
        "Name: ValueOneOverride\n" +
        "Version: 1.0.0\n" +
        "Output:\n" +
        "  Name: empty-aspect.valueone\n" +
        "  Description: Replaces the engine's value.\n" +
        "  ValueType: int\n" +
        "  IsList: false\n" +
        "Rules:\n" +
        "  - When: { Property: empty-aspect.valuetwo, Gt: 0 }\n" +
        "    Then: 3\n" +
        "  - Else: 4\n";

    private static IPipeline Build(bool cache)
    {
        var builder = new EmptyEngineBuilder(NullLoggerFactory.Instance);
        if (cache)
        {
            builder.SetCache(new CacheConfiguration { Size = 10 });
        }
        return new PipelineBuilder(NullLoggerFactory.Instance)
            .AddFlowElement(builder.Build())
            .AddFlowElement(
                new DerivedPropertyElementBuilder(NullLoggerFactory.Instance)
                    .AddScript("ValueOneOverride", OverrideScript)
                    .Build())
            .Build();
    }

    /// <summary>
    /// The same pipeline with no cache builds, so the refusal below is for
    /// the cache alone.
    /// </summary>
    [TestMethod]
    public void Override_TargetWithoutCache_Builds()
    {
        using var pipeline = Build(false);
    }

    [TestMethod]
    public void Override_TargetWithCache_IsRefused()
    {
        var exception = Assert.ThrowsExactly<PipelineConfigurationException>(
            () => Build(true));
        StringAssert.Contains(exception.Message, "cache");
        StringAssert.Contains(exception.Message, "ValueOneOverride");
    }
}
