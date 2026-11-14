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

using FiftyOne.Pipeline.Engines;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Compare = FiftyOne.IpIntelligence.Examples.OnPremise.Compare.Program;
using Metrics = FiftyOne.IpIntelligence.Examples.OnPremise.Metrics.Program;

namespace FiftyOne.IpIntelligence.Example.Tests.OnPremise;

/// <summary>
/// The Metrics and Compare examples run in a hosted worker. A run that
/// fails must leave the process with a failure exit code, so that a script
/// or a test that runs the example can tell. Each test runs a worker on a
/// file that is not a data file, so building the engine fails, and checks
/// the exit code the process would end with. The tests change the
/// process's exit code, so they put it back and run apart from the others.
/// </summary>
[TestClass]
[TestCategory(TestCategories.Unit)]
[DoNotParallelize]
public class TestWorkerExitCode
{
    /// <summary>
    /// The exit code the examples end with when a run fails.
    /// </summary>
    private const int FailedExitCode = 1;

    /// <summary>
    /// How long a worker is given to fail and stop.
    /// </summary>
    private const int WorkerWaitSeconds = 60;

    /// <summary>
    /// Records whether the worker stopped the application.
    /// </summary>
    private sealed class Lifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public bool Stopped { get; private set; }

        public void StopApplication() => Stopped = true;
    }

    private int _previousExitCode;
    private string _notADataFile;

    [TestInitialize]
    public void Init()
    {
        _previousExitCode = Environment.ExitCode;
        Environment.ExitCode = 0;
        _notADataFile = Path.GetTempFileName();
        File.WriteAllText(_notADataFile, "This is not a data file.");
    }

    [TestCleanup]
    public void Cleanup()
    {
        Environment.ExitCode = _previousExitCode;
        File.Delete(_notADataFile);
    }

    /// <summary>
    /// Runs the worker until it finishes, and checks that it handled the
    /// failure itself and stopped the application.
    /// </summary>
    private static async Task RunWorker(
        BackgroundService worker,
        Lifetime lifetime)
    {
        await worker.StartAsync(CancellationToken.None);
        var finished = await Task.WhenAny(
            worker.ExecuteTask,
            Task.Delay(TimeSpan.FromSeconds(WorkerWaitSeconds)));
        Assert.AreSame(
            worker.ExecuteTask,
            finished,
            $"The worker did not finish in {WorkerWaitSeconds} seconds.");
        Assert.IsTrue(
            worker.ExecuteTask.IsCompletedSuccessfully,
            "The worker let the failure out rather than handling it: " +
            worker.ExecuteTask.Exception?.InnerException?.Message);
        Assert.IsTrue(
            lifetime.Stopped,
            "The worker did not stop the application.");
    }

    [TestMethod]
    public async Task Metrics_RunFails_EndsWithFailedExitCode()
    {
        var lifetime = new Lifetime();
        using var output = new StreamWriter(new MemoryStream());
        var worker = new Metrics.Worker(lifetime, new Metrics.Configuration
        {
            DataFile = _notADataFile,
            LoggerFactory = NullLoggerFactory.Instance,
            Output = output,
            SamplePercentage = 1,
            MaxSamplesPerRange = 1,
            RangePercentage = 1,
            MaxProfilesWithoutRange = 1,
            PerformanceProfile = PerformanceProfiles.LowMemory,
        });

        await RunWorker(worker, lifetime);

        Assert.AreEqual(
            FailedExitCode,
            Environment.ExitCode,
            "The metrics run failed, so the process must end with a " +
            "failure exit code.");
    }

    [TestMethod]
    public async Task Compare_RunFails_EndsWithFailedExitCode()
    {
        var lifetime = new Lifetime();
        using var output = new StreamWriter(new MemoryStream());
        var worker = new Compare.Worker(lifetime, new Compare.Configuration
        {
            DataFile = _notADataFile,
            CsvTruthFile = _notADataFile,
            LoggerFactory = NullLoggerFactory.Instance,
            Output = output,
            PerformanceProfile = PerformanceProfiles.LowMemory,
        });

        await RunWorker(worker, lifetime);

        Assert.AreEqual(
            FailedExitCode,
            Environment.ExitCode,
            "The comparison failed, so the process must end with a " +
            "failure exit code.");
    }
}
