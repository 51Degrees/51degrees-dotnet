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

using FiftyOne.Common.TestHelpers;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace FiftyOne.Pipeline.Engines.Tests.FlowElements;

/// <summary>
/// Checks the lazy loading settings of the base of the pre-packaged
/// pipeline builders.
/// </summary>
[TestClass]
public class PrePackagedPipelineBuilderBaseTests
{
    /// <summary>
    /// Opens the settings the base keeps.
    /// </summary>
    private class TestBuilder :
        PrePackagedPipelineBuilderBase<TestBuilder>
    {
        public TestBuilder() : base(new TestLoggerFactory())
        {
        }

        public bool Lazy => LazyLoading;

        public TimeSpan Timeout => LazyLoadingTimeout;
    }

    /// <summary>
    /// The whole number given to UseLazyLoading is the timeout in
    /// milliseconds, as its parameter's name and documentation say.
    /// </summary>
    [DataRow(250)]
    [DataRow(5000)]
    [TestMethod]
    public void UseLazyLoading_TimeoutIsInMilliseconds(int milliseconds)
    {
        var builder = new TestBuilder().UseLazyLoading(milliseconds);

        Assert.IsTrue(builder.Lazy);
        Assert.AreEqual(
            TimeSpan.FromMilliseconds(milliseconds),
            builder.Timeout);
    }

    /// <summary>
    /// Without a timeout the default of 5 seconds is used.
    /// </summary>
    [TestMethod]
    public void UseLazyLoading_DefaultTimeoutIsFiveSeconds()
    {
        var builder = new TestBuilder().UseLazyLoading();

        Assert.IsTrue(builder.Lazy);
        Assert.AreEqual(TimeSpan.FromSeconds(5), builder.Timeout);
    }
}
