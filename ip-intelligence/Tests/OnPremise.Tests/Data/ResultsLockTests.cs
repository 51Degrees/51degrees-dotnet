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
using FiftyOne.IpIntelligence.Engine.OnPremise.Data;
using FiftyOne.IpIntelligence.Engine.OnPremise.FlowElements;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.IpIntelligence.OnPremise.Tests.Data;

/// <summary>
/// Finished IP Intelligence data may be read from several threads at once,
/// so every read of its native results holds the lock the data keeps for
/// them. Each test holds that lock on another thread and checks that a read
/// waits until it is released.
/// </summary>
[TestClass]
[TestCategory("IpIntelligence")]
[TestCategory("OnPremise")]
public class ResultsLockTests
{
    /// <summary>
    /// How long the other thread holds the lock. A read that does not wait
    /// for the lock finishes long before this.
    /// </summary>
    private const int HoldMilliseconds = 500;

    /// <summary>
    /// The name of the lock's field in the data's base class.
    /// </summary>
    private const string LockFieldName = "_getLock";

    /// <summary>
    /// The free data file, so that the tests need no licence.
    /// </summary>
    private const string AsnDataFileName = "51Degrees-IPIV4AsnIpiV41.ipi";

    private const string TestIp = "8.8.8.8";

    private const string MissingProperty = "NotAPropertyAnyDataFileHas";

    private static readonly TestLoggerFactory _logger =
        new TestLoggerFactory();

    /// <summary>
    /// A value read by name is read under the lock.
    /// </summary>
    [TestMethod]
    public void Value_WaitsForTheResultsLock()
    {
        AssertReadWaits((data, name) => data[name]);
    }

    /// <summary>
    /// The public list of a Property's values is read under the lock.
    /// </summary>
    [TestMethod]
    public void GetValues_WaitsForTheResultsLock()
    {
        AssertReadWaits(
            (data, name) => ((IIpDataOnPremise)data).GetValues(name));
    }

    /// <summary>
    /// Finding whether the results hold a Property reads them, so reading a
    /// Property they do not hold waits for the lock too.
    /// </summary>
    [TestMethod]
    public void MissingProperty_WaitsForTheResultsLock()
    {
        AssertReadWaits((data, name) =>
        {
            try
            {
                return data[MissingProperty];
            }
            catch (PropertyMissingException)
            {
                return null;
            }
        });
    }

    /// <summary>
    /// Reads once to warm anything cached on the engine, then holds the lock
    /// on another thread and reads again, checking that the read finished
    /// only after the lock was released. The read is given the name of a
    /// Property the data file holds.
    /// </summary>
    private static void AssertReadWaits(
        Func<IIpIntelligenceData, string, object> read)
    {
        var file = TestHelpers.Utils.GetFilePath(AsnDataFileName);
        using var engine = new IpiOnPremiseEngineBuilder(_logger, null)
            .SetPerformanceProfile(PerformanceProfiles.LowMemory)
            .SetAutoUpdate(false)
            .SetDataFileSystemWatcher(false)
            .Build(file.FullName, false);
        using var pipeline = new PipelineBuilder(_logger)
            .AddFlowElement(engine)
            .Build();
        var name = engine.Properties
            .OfType<PropertyMetaDataIpi>()
            .First()
            .Name;
        using var flowData = pipeline.CreateFlowData();
        flowData.AddEvidence("query.client-ip", TestIp);
        flowData.Process();
        var data = flowData.Get<IIpIntelligenceData>();
        read(data, name);
        var resultsLock = GetResultsLock(data);
        var released = 0;
        using var held = new ManualResetEventSlim();
        var holder = Task.Run(() =>
        {
            lock (resultsLock)
            {
                held.Set();
                Thread.Sleep(HoldMilliseconds);
                Volatile.Write(ref released, 1);
            }
        });
        held.Wait();
        read(data, name);
        var releasedBeforeTheRead = Volatile.Read(ref released);
        holder.Wait();
        Assert.AreEqual(1, releasedBeforeTheRead,
            "The read finished while another thread held the results " +
            "lock, so two threads could read the native results at once.");
    }

    private static object GetResultsLock(IIpIntelligenceData data)
    {
        for (var type = data.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(
                LockFieldName,
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly);
            if (field != null)
            {
                return field.GetValue(data);
            }
        }
        Assert.Fail("The data keeps no field named " + LockFieldName +
            " to lock its results with.");
        return null;
    }
}
