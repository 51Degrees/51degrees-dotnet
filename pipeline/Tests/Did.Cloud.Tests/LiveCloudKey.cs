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

namespace FiftyOne.Did.Cloud.Tests;

/// <summary>
/// The resource key for the test that calls the live cloud service, and
/// the message it fails with when there is none. There is no skip,
/// because a cloud test that passes without calling the cloud has proved
/// nothing.
/// </summary>
internal static class LiveCloudKey
{
    /// <summary>
    /// The environment variables the key is read from, in the order tried.
    /// The 51Did workflow sets the first to each key in turn, so it has to
    /// come first for every pass to use its own key.
    /// </summary>
    internal static readonly string[] Names =
    {
        "_51DEGREES_RESOURCE_KEY",
        "_51DEGREES_RESOURCE_KEY_BESPOKE",
        "_51DEGREES_RESOURCE_KEY_51DID",
    };

    /// <summary>
    /// The old names of the key's variable, read in this order only when
    /// none of <see cref="Names"/> is set.
    /// </summary>
    internal static readonly string[] OldNames =
    {
        "51DEGREES_RESOURCE_KEY",
    };

    private static readonly HashSet<string> _oldNamesReported =
        new HashSet<string>();

    /// <summary>
    /// The key from the first of <see cref="Names"/> that is set. When
    /// none is, the key from the first of <see cref="OldNames"/> that is
    /// set, with each old name reported once on the error stream as
    /// deprecated.
    /// </summary>
    private static string? FromEnvironment()
    {
        var key = Names
            .Select(n => Environment.GetEnvironmentVariable(n))
            .FirstOrDefault(v => string.IsNullOrWhiteSpace(v) == false);
        if (key != null)
        {
            return key;
        }
        foreach (var oldName in OldNames)
        {
            var value = Environment.GetEnvironmentVariable(oldName);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }
            bool first;
            lock (_oldNamesReported)
            {
                first = _oldNamesReported.Add(oldName);
            }
            if (first)
            {
                Console.Error.WriteLine(
                    $"The environment variable {oldName} is deprecated " +
                    $"and {Names[0]} replaces it. Set {Names[0]} to the " +
                    "same value instead.");
            }
            return value;
        }
        return null;
    }

    private const string AccessiblePropertiesUrl =
        "https://cloud.51degrees.com/api/v4/accessibleproperties?resource=<key>";

    private const string ConfigureUrl =
        "https://51degrees.com/configure?utm_source=code&utm_medium=example&utm_campaign=51degrees-dotnet&utm_content=pipeline-tests-did.cloud.tests-livecloudkey.cs&utm_term=resource-key";

    /// <summary>
    /// The key, failing the test when none of <see cref="Names"/> is set.
    /// The test checks the pipeline against whatever the key carries, so
    /// the message asks for no particular product.
    /// </summary>
    /// <param name="asksFor">What the test asks the service for.</param>
    internal static string Require(string asksFor)
    {
        var key = FromEnvironment();
        if (key == null)
        {
            Assert.Fail(
                "This test calls the live 51Degrees cloud service, so it " +
                "needs a resource key and there is nothing to fall back " +
                $"on. None of {string.Join(", ", Names)} is set, tried in " +
                "that order. Any key will do, because the test asks the " +
                $"service for {asksFor}. To see the products a key " +
                $"carries, open {AccessiblePropertiesUrl}. Create a key at " +
                $"{ConfigureUrl}.");
        }
        return key;
    }
}
