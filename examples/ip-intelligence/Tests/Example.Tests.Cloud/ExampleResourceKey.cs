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

namespace FiftyOne.IpIntelligence.Example.Tests.Cloud;

/// <summary>
/// The resource key the cloud example tests give the examples, read from
/// <see cref="Name"/>, the key that carries every property. A test gives
/// the key to an example the way the example takes one. For an example run
/// in this process that reads the environment, an instance puts the key in
/// <see cref="ExampleName"/> and puts the earlier value back when it is
/// disposed.
/// </summary>
internal sealed class ExampleResourceKey : IDisposable
{
    /// <summary>
    /// The environment variable the tests read the key from.
    /// </summary>
    internal const string Name = "_51DEGREES_RESOURCE_KEY_BESPOKE";

    /// <summary>
    /// The environment variable the examples read their key from.
    /// </summary>
    internal const string ExampleName = "_51DEGREES_RESOURCE_KEY";

    private readonly string _previous;
    private bool _disposed;

    private ExampleResourceKey(string key)
    {
        _previous = Environment.GetEnvironmentVariable(ExampleName);
        Environment.SetEnvironmentVariable(ExampleName, key);
    }

    /// <summary>
    /// The key, skipping the test when <see cref="Name"/> is not set.
    /// </summary>
    internal static string Require()
    {
        var key = Environment.GetEnvironmentVariable(Name);
        if (string.IsNullOrWhiteSpace(key))
        {
            Assert.Inconclusive(
                "This test runs a cloud example against the live 51Degrees " +
                $"cloud service, so it needs a resource key, and {Name} is " +
                "not set.");
        }
        return key;
    }

    /// <summary>
    /// Puts the key in <see cref="ExampleName"/>, skipping the test when
    /// <see cref="Name"/> is not set.
    /// </summary>
    /// <returns>The instance that puts the earlier value back.</returns>
    internal static ExampleResourceKey Set() => new ExampleResourceKey(Require());

    public void Dispose()
    {
        if (_disposed == false)
        {
            _disposed = true;
            Environment.SetEnvironmentVariable(ExampleName, _previous);
        }
    }
}
