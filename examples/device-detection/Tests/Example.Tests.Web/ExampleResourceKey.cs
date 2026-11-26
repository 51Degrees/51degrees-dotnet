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
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.DeviceDetection.Example.Tests.Web;

/// <summary>
/// Gives a cloud example that reads its resource key from the environment
/// the key in <see cref="Name"/>, the key that carries every property.
/// While an instance is alive, <see cref="ExampleName"/>, the one name the
/// examples read, holds that key. Disposing the instance puts back the
/// value the variable had before.
/// </summary>
public sealed class ExampleResourceKey : IDisposable
{
    /// <summary>
    /// The environment variable the tests read the key from.
    /// </summary>
    public const string Name = "_51DEGREES_RESOURCE_KEY_BESPOKE";

    /// <summary>
    /// The environment variable the examples read their key from.
    /// </summary>
    public const string ExampleName = "_51DEGREES_RESOURCE_KEY";

    /// <summary>
    /// What a test skips or fails with when <see cref="Name"/> is not set.
    /// </summary>
    public const string NoKey =
        "This test runs a cloud example against the live 51Degrees cloud " +
        "service, so it needs a resource key, and " + Name + " is not set.";

    /// <summary>
    /// The environment variable CI sets to false on a build entry that
    /// leaves out the live cloud tests.
    /// </summary>
    public const string LiveCloudTestsVariable = "LIVE_CLOUD_TESTS";

    /// <summary>
    /// What a test skips with when <see cref="LiveCloudTestsVariable"/> is
    /// false.
    /// </summary>
    public const string NotRunHere =
        "Live cloud tests are not run on this build entry, as " +
        LiveCloudTestsVariable + " is false.";

    /// <summary>
    /// False when <see cref="LiveCloudTestsVariable"/> is false, so the live
    /// cloud tests skip instead of failing for want of a key.
    /// </summary>
    public static bool RunHere => string.Equals(
        Environment.GetEnvironmentVariable(LiveCloudTestsVariable),
        "false",
        StringComparison.OrdinalIgnoreCase) == false;

    private readonly string _previous;
    private bool _disposed;

    private ExampleResourceKey(string key)
    {
        _previous = Environment.GetEnvironmentVariable(ExampleName);
        Environment.SetEnvironmentVariable(ExampleName, key);
    }

    /// <summary>
    /// The key, or null when <see cref="Name"/> is not set.
    /// </summary>
    public static string Find()
    {
        var value = Environment.GetEnvironmentVariable(Name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// Puts the key in <see cref="ExampleName"/>.
    /// </summary>
    /// <returns>
    /// The instance that puts the earlier value back, or null when
    /// <see cref="Name"/> is not set, which leaves the environment alone.
    /// </returns>
    public static ExampleResourceKey Set()
    {
        var key = Find();
        return key == null ? null : new ExampleResourceKey(key);
    }

    /// <summary>
    /// Starts a server with the key in <see cref="ExampleName"/>, and puts
    /// the earlier value back once the server has stopped, because an
    /// example can read the variable on each request. Skips the test when
    /// <see cref="Name"/> is not set, or when <see cref="RunHere"/> is false.
    /// </summary>
    /// <param name="start">Starts the example's server.</param>
    /// <param name="stop">Stops the server.</param>
    /// <returns>The task that ends when the server has stopped.</returns>
    public static Task Serve(
        Func<CancellationToken, Task> start,
        CancellationToken stop)
    {
        if (RunHere == false)
        {
            Assert.Inconclusive(NotRunHere);
        }
        var key = Set();
        if (key == null)
        {
            Assert.Inconclusive(NoKey);
        }
        Task server;
        try
        {
            server = start(stop);
        }
        catch
        {
            key.Dispose();
            throw;
        }
        return server.ContinueWith(
            finished =>
            {
                key.Dispose();
                return finished;
            },
            TaskScheduler.Default).Unwrap();
    }

    public void Dispose()
    {
        if (_disposed == false)
        {
            _disposed = true;
            Environment.SetEnvironmentVariable(ExampleName, _previous);
        }
    }
}
