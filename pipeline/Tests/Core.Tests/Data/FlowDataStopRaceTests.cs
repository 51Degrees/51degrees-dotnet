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

using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;

namespace FiftyOne.Pipeline.Core.Tests.Data;

/// <summary>
/// Races setting <see cref="FlowData.Stop"/> on one thread against the
/// first read of <see cref="FlowData.StopToken"/> on another, which is
/// when the token's source is created.
/// </summary>
/// <remarks>
/// Only public members are used, so the file can be compiled into a test
/// project for .NET Framework as well.
/// </remarks>
[TestClass]
public class FlowDataStopRaceTests
{
    private const int Iterations = 20000;

    /// <summary>
    /// Whichever thread goes first, the token read on the other thread is
    /// cancelled once both have finished.
    /// </summary>
    [TestMethod]
    public void FlowData_StopRacingFirstStopTokenRead_TokenIsCancelled()
    {
        using (var pipeline = new PipelineBuilder(NullLoggerFactory.Instance)
            .Build())
        using (var start = new Barrier(2))
        {
            var data = new FlowData[Iterations];
            var tokens = new CancellationToken[Iterations];
            for (var i = 0; i < Iterations; i++)
            {
                data[i] = (FlowData)pipeline.CreateFlowData();
            }

            var stopper = new Thread(() =>
            {
                for (var i = 0; i < Iterations; i++)
                {
                    start.SignalAndWait();
#pragma warning disable CS0618 // Stop is obsolete
                    data[i].Stop = true;
#pragma warning restore CS0618
                }
            });
            stopper.Start();
            for (var i = 0; i < Iterations; i++)
            {
                start.SignalAndWait();
                tokens[i] = data[i].StopToken;
            }
            stopper.Join();

            var missed = 0;
            for (var i = 0; i < Iterations; i++)
            {
                if (tokens[i].IsCancellationRequested == false)
                {
                    missed++;
                }
                data[i].Dispose();
            }
            Assert.AreEqual(0, missed,
                $"{missed} of {Iterations} stop tokens were not cancelled " +
                "after Stop was set.");
        }
    }
}
