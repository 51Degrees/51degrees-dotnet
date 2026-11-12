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
using System.Linq;

namespace FiftyOne.DeviceDetection.Cloud.Tests;

/// <summary>
/// The resource key for the tests that call the live cloud service, and
/// the messages they fail with when there is no key or the cloud answers
/// without what a test asked for. There is no skip, because a cloud test
/// that passes without calling the cloud has proved nothing.
/// </summary>
internal static class LiveCloudKey
{
    /// <summary>
    /// The environment variables the key is read from, in the order tried.
    /// </summary>
    internal static readonly string[] Names =
    {
        "_51DEGREES_RESOURCE_KEY",
        "_51DEGREES_RESOURCE_KEY_BESPOKE",
    };

    private const string AccessiblePropertiesUrl =
        "https://cloud.51degrees.com/api/v4/accessibleproperties?resource=<key>";

    private const string ConfigureUrl =
        "https://51degrees.com/configure?utm_source=code&utm_medium=example&utm_campaign=51degrees-dotnet&utm_content=device-detection-tests-cloud.tests-livecloudkey.cs&utm_term=resource-key";

    /// <summary>
    /// The key, failing the test when none of <see cref="Names"/> is set.
    /// </summary>
    /// <param name="product">The product the key has to carry.</param>
    /// <param name="asksFor">What the test asks the service for.</param>
    internal static string Require(string product, string asksFor)
    {
        var key = Names
            .Select(n => Environment.GetEnvironmentVariable(n))
            .FirstOrDefault(v => string.IsNullOrWhiteSpace(v) == false);
        if (key == null)
        {
            Assert.Fail(
                "This test calls the live 51Degrees cloud service, so it " +
                "needs a resource key and there is nothing to fall back " +
                $"on. None of {string.Join(", ", Names)} is set, tried in " +
                $"that order. The key has to carry the {product} product, " +
                $"because the test asks the service for {asksFor}. To see " +
                "the products a key carries, open " +
                $"{AccessiblePropertiesUrl}. Create a key at " +
                $"{ConfigureUrl}.");
        }
        return key;
    }

    /// <summary>
    /// The failure for an answer without the thing the test asked for,
    /// which reads as the key's entitlement rather than a fault.
    /// </summary>
    /// <param name="thing">What the answer did not have.</param>
    /// <param name="reason">The reason the cloud gave, if any.</param>
    /// <param name="product">The product that answers with it.</param>
    internal static string Missing(
        string thing,
        string reason,
        string product) =>
        $"The cloud answered without {thing} (" +
        (string.IsNullOrWhiteSpace(reason) ? "no reason given" : reason) +
        "), which is what a key that does not carry the " +
        $"{product} product gets.";
}
