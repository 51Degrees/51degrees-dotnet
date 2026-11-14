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

using FiftyOne.DeviceDetection.Hash.Engine.OnPremise.Data;
using FiftyOne.Pipeline.Engines;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.DeviceDetection.Hash.Tests.FlowElements
{
    /// <summary>
    /// Finished Device Detection data may be read from several threads at
    /// once, so every read of its native results holds the lock the data
    /// keeps for them. Each test holds that lock on another thread and
    /// checks that a read waits until it is released.
    /// </summary>
    [TestClass]
    public class ResultsLockTests : TestsBase
    {
        /// <summary>
        /// How long the other thread holds the lock. A read that does not
        /// wait for the lock finishes long before this.
        /// </summary>
        private const int HoldMilliseconds = 500;

        /// <summary>
        /// The name of the lock's field in the data's base class.
        /// </summary>
        private const string LockFieldName = "_getLock";

        private static readonly Dictionary<string, Func<IDeviceData, object>>
            Metrics = new Dictionary<string, Func<IDeviceData, object>>()
        {
            { "DeviceId", d => d.DeviceId },
            { "UserAgents", d => d.UserAgents },
            { "Iterations", d => d.Iterations },
            { "Drift", d => d.Drift },
            { "Difference", d => d.Difference },
            { "MatchedNodes", d => d.MatchedNodes },
            { "Method", d => d.Method },
        };

        /// <summary>
        /// A match metric is read under the lock.
        /// </summary>
        [TestMethod]
        [DataRow("DeviceId")]
        [DataRow("UserAgents")]
        [DataRow("Iterations")]
        [DataRow("Drift")]
        [DataRow("Difference")]
        [DataRow("MatchedNodes")]
        [DataRow("Method")]
        public void MatchMetric_WaitsForTheResultsLock(string name)
        {
            AssertReadWaits(Metrics[name]);
        }

        /// <summary>
        /// The public list of a Property's values is read under the lock.
        /// </summary>
        [TestMethod]
        public void GetValues_WaitsForTheResultsLock()
        {
            AssertReadWaits(
                data => ((DeviceDataHash)data).GetValues("BrowserName"));
        }

        /// <summary>
        /// The JSON of every value is read under the lock.
        /// </summary>
        [TestMethod]
        public void GetAllValuesJson_WaitsForTheResultsLock()
        {
            AssertReadWaits(
                data => ((DeviceDataHash)data).GetAllValuesJson());
        }

        /// <summary>
        /// Reads once to warm anything cached on the engine, then holds the
        /// lock on another thread and reads again, checking that the read
        /// finished only after the lock was released.
        /// </summary>
        private void AssertReadWaits(Func<IDeviceData, object> read)
        {
            InitWrapperAndUserAgents(PerformanceProfiles.MaxPerformance);
            using (var flowData = Wrapper.Pipeline.CreateFlowData())
            {
                flowData.AddEvidence(
                    "header.user-agent",
                    TestHelpers.Constants.MobileUserAgent);
                flowData.Process();
                var data = flowData.Get<IDeviceData>();
                read(data);
                var resultsLock = GetResultsLock(data);
                var released = 0;
                using (var held = new ManualResetEventSlim())
                {
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
                    read(data);
                    var releasedBeforeTheRead = Volatile.Read(ref released);
                    holder.Wait();
                    Assert.AreEqual(1, releasedBeforeTheRead,
                        "The read finished while another thread held the " +
                        "results lock, so two threads could read the " +
                        "native results at once.");
                }
            }
        }

        private static object GetResultsLock(IDeviceData data)
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
}
