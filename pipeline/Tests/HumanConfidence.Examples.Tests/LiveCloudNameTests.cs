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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FiftyOne.HumanConfidence.Examples.Tests;

/// <summary>
/// The live tests read the cloud and the keys by their own names. A test
/// that needs the staging cloud reads it from its own variable, with no
/// default, and its Bespoke key from its own variable, so it never calls
/// the production cloud or uses the production key, and it is in the
/// category the shared CI flow leaves out. The tests change the
/// environment of the whole process, so they run apart from the others.
/// </summary>
[TestClass]
[DoNotParallelize]
public class LiveCloudNameTests
{
    private const string Staging = "https://staging.example.test/api/v4/";
    private const string Production = "https://production.example.test/api/v4/";

    private static readonly string[] Names =
    {
        LiveCloudTests.StagingEndpointVariable,
        LiveCloudTests.StagingKeyName,
        LiveCloudTests.EndpointVariable,
        LiveCloudTests.KeyName,
        LiveCloudTests.BasicKeyName,
    };

    /// <summary>
    /// The tests that need the staging cloud.
    /// </summary>
    private static readonly string[] StagingTests =
    {
        nameof(LiveCloudTests.ThePassiveCallAnswersASourceAndALevel),
        nameof(LiveCloudTests.AKeyOutsideBespokeGetsNoSource),
    };

    private readonly Dictionary<string, string?> _saved =
        new Dictionary<string, string?>();

    [TestInitialize]
    public void Init()
    {
        foreach (var name in Names)
        {
            _saved[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var saved in _saved)
        {
            Environment.SetEnvironmentVariable(saved.Key, saved.Value);
        }
    }

    [TestMethod]
    public void TheStagingCloudIsReadFromItsName()
    {
        Environment.SetEnvironmentVariable(
            LiveCloudTests.StagingEndpointVariable, Staging.TrimEnd('/'));
        Assert.AreEqual(Staging, LiveCloudTests.StagingEndpoint());
    }

    [TestMethod]
    public void WithoutTheStagingCloudTheTestSkipsNamingIt()
    {
        Environment.SetEnvironmentVariable(
            LiveCloudTests.EndpointVariable, Production);
        var message = Skipped(() => LiveCloudTests.StagingEndpoint());
        Assert.IsNotNull(message,
            "A staging test fell back on another cloud.");
        StringAssert.Contains(message, LiveCloudTests.StagingEndpointVariable);
    }

    [TestMethod]
    public void TheProductionCloudIsNotReadFromTheStagingName()
    {
        Environment.SetEnvironmentVariable(
            LiveCloudTests.StagingEndpointVariable, Staging);
        Assert.AreEqual(LiveCloudTests.DefaultEndpoint,
            LiveCloudTests.Endpoint());
        Environment.SetEnvironmentVariable(
            LiveCloudTests.EndpointVariable, Production);
        Assert.AreEqual(Production, LiveCloudTests.Endpoint());
    }

    [TestMethod]
    public void TheStagingKeyIsReadFromItsName()
    {
        Environment.SetEnvironmentVariable(
            LiveCloudTests.StagingKeyName, "staging");
        Assert.AreEqual("staging",
            LiveCloudTests.RequireStagingKey(LiveCloudTests.StagingKeyName));
    }

    [TestMethod]
    public void WithoutTheStagingKeyTheTestSkipsNamingIt()
    {
        Environment.SetEnvironmentVariable(
            LiveCloudTests.KeyName, "production");
        var message = Skipped(() =>
            LiveCloudTests.RequireStagingKey(LiveCloudTests.StagingKeyName));
        Assert.IsNotNull(message,
            "A staging test used the production key.");
        StringAssert.Contains(message, LiveCloudTests.StagingKeyName);
    }

    [TestMethod]
    public void OnlyTheTestsThatNeedTheStagingCloudAreInItsCategory()
    {
        var inCategory = typeof(LiveCloudTests).GetMethods()
            .Where(m => m.GetCustomAttribute<TestMethodAttribute>() != null)
            .Where(m => m.GetCustomAttributes<TestCategoryAttribute>()
                .SelectMany(c => c.TestCategories)
                .Contains(LiveCloudTests.StagingCategory))
            .Select(m => m.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(
            StagingTests.OrderBy(n => n, StringComparer.Ordinal).ToArray(),
            inCategory,
            "The shared CI flow leaves out the category, so a test that " +
            "calls the staging cloud must be in it, and no other test.");
    }

    /// <summary>
    /// The message the action skipped with, or null when it returned.
    /// Any other failure is passed on.
    /// </summary>
    private static string? Skipped(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (AssertInconclusiveException ex)
        {
            return ex.Message;
        }
    }
}
