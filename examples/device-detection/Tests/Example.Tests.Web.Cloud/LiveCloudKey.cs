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

using System;

namespace FiftyOne.DeviceDetection.Example.Tests.Web.Cloud;

/// <summary>
/// The resource key for the tests that run the cloud web examples against
/// the live cloud service, and the message they skip with when there is
/// none. A skipped test names the variable, and never passes, because a
/// cloud test that passes without calling the cloud has proved nothing.
/// The tests give the key to an example the way the example takes one.
/// </summary>
internal static class LiveCloudKey
{
    /// <summary>
    /// The environment variable the key is read from, which holds the key
    /// that carries every property.
    /// </summary>
    internal const string Name = "_51DEGREES_RESOURCE_KEY_BESPOKE";

    /// <summary>
    /// The key from <see cref="Name"/>, or null when it is not set.
    /// </summary>
    private static string FromEnvironment()
    {
        var value = Environment.GetEnvironmentVariable(Name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private const string AccessiblePropertiesUrl =
        "https://cloud.51degrees.com/api/v4/accessibleproperties?resource=<key>";

    private const string ConfigureUrl =
        "https://51degrees.com/configure?utm_source=code&utm_medium=example&utm_campaign=51degrees-dotnet&utm_content=examples-device-detection-tests-example.tests.web.cloud-livecloudkey.cs&utm_term=resource-key";

    /// <summary>
    /// The key, or null when <see cref="Name"/> is not set.
    /// </summary>
    internal static string Find() => FromEnvironment();

    /// <summary>
    /// What the tests skip with when <see cref="Find"/> finds no key.
    /// </summary>
    /// <param name="product">The product the key has to carry.</param>
    /// <param name="asksFor">What the tests ask the service for.</param>
    internal static string NoKey(string product, string asksFor) =>
        "These tests call the live 51Degrees cloud service, so they need a " +
        "resource key and there is nothing to fall back on. " +
        $"{Name} is not set. The key " +
        $"has to carry the {product} product, because these tests ask the " +
        $"service for {asksFor}. To see the products a key carries, open " +
        $"{AccessiblePropertiesUrl}. Create a key at {ConfigureUrl}.";
}
