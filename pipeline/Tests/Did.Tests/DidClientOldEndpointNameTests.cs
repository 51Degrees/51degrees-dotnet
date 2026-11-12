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

using FiftyOne.Did.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace FiftyOne.Did.Tests
{
    /// <summary>
    /// Tests that <see cref="DidClient"/> reads the cloud endpoint from the
    /// new environment variable first and from the old one when the new one
    /// is unset or empty, and that the old constant keeps its name and
    /// value. Nothing here touches the network.
    /// </summary>
    [TestClass]
    public class DidClientOldEndpointNameTests
    {
        private const string Resource = "AQAAAresourcekey";
        private const string NewName = "_51DEGREES_CLOUD_ENDPOINT";
        private const string OldName = "FOD_CLOUD_API_URL";
        private const string NewConstant = "CloudEndpointEnvironmentVariable";
        private const string NewUrl = "https://new.example/api/v4/";
        private const string OldUrl = "https://old.example/api/v4/";

        /// <summary>
        /// Keeps every warning written to <see cref="Trace"/>.
        /// </summary>
        private sealed class RecordingListener : TraceListener
        {
            public List<string> Warnings { get; } = new List<string>();

            public override void TraceEvent(
                TraceEventCache? eventCache,
                string source,
                TraceEventType eventType,
                int id,
                string? message)
            {
                if (eventType == TraceEventType.Warning && message != null)
                {
                    lock (Warnings)
                    {
                        Warnings.Add(message);
                    }
                }
            }

            public override void Write(string? message) { }

            public override void WriteLine(string? message) { }
        }

        /// <summary>
        /// Listens for the whole class. The deprecation is written once in
        /// the process, so one listener has to see every client that reads
        /// the old name for the count to mean anything.
        /// </summary>
        private static readonly RecordingListener Listener =
            new RecordingListener();

        private FakeHttpHandler _handler = null!;
        private string? _previousNew;
        private string? _previousOld;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            Trace.Listeners.Add(Listener);
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            Trace.Listeners.Remove(Listener);
        }

        [TestInitialize]
        public void TestInitialize()
        {
            _handler = new FakeHttpHandler();
            _previousNew = Environment.GetEnvironmentVariable(NewName);
            _previousOld = Environment.GetEnvironmentVariable(OldName);
            Environment.SetEnvironmentVariable(NewName, null);
            Environment.SetEnvironmentVariable(OldName, null);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            Environment.SetEnvironmentVariable(NewName, _previousNew);
            Environment.SetEnvironmentVariable(OldName, _previousOld);
        }

        /// <summary>
        /// A client given no endpoint, so it takes one from the
        /// environment or the default.
        /// </summary>
        private DidClient NewClient() =>
            new DidClient(Resource, null, null, _handler.Client);

        /// <summary>
        /// The warnings that say the old name is deprecated.
        /// </summary>
        private static List<string> Deprecations()
        {
            lock (Listener.Warnings)
            {
                return Listener.Warnings
                    .Where(warning => warning.Contains(OldName))
                    .ToList();
            }
        }

        [TestMethod]
        public void OnlyOldNameSet_GivesThatEndpoint()
        {
            Environment.SetEnvironmentVariable(OldName, OldUrl);

            using var client = NewClient();

            Assert.AreEqual(OldUrl, client.Endpoint);
        }

        [TestMethod]
        public void NewNameEmpty_OldNameSet_GivesTheOldEndpoint()
        {
            Environment.SetEnvironmentVariable(NewName, string.Empty);
            Environment.SetEnvironmentVariable(OldName, OldUrl);

            using var client = NewClient();

            Assert.AreEqual(OldUrl, client.Endpoint);
        }

        [TestMethod]
        public void BothNamesSet_NewNameWins()
        {
            Environment.SetEnvironmentVariable(NewName, NewUrl);
            Environment.SetEnvironmentVariable(OldName, OldUrl);

            using var client = NewClient();

            Assert.AreEqual(NewUrl, client.Endpoint);
        }

        [TestMethod]
        public void NeitherNameSet_GivesTheDefault()
        {
            using var client = NewClient();

            Assert.AreEqual(DidClient.DefaultEndpoint, client.Endpoint);
        }

        [TestMethod]
        public void EndpointGiven_WinsOverBothNames()
        {
            Environment.SetEnvironmentVariable(NewName, NewUrl);
            Environment.SetEnvironmentVariable(OldName, OldUrl);

            using var client = new DidClient(
                Resource, null, "https://given.example/api/v4/",
                _handler.Client);

            Assert.AreEqual("https://given.example/api/v4/", client.Endpoint);
        }

        [TestMethod]
        public void OldNameRead_DeprecationIsWrittenOnce()
        {
            Environment.SetEnvironmentVariable(OldName, OldUrl);

            using var first = NewClient();
            using var second = NewClient();

            var deprecations = Deprecations();
            Assert.AreEqual(
                1,
                deprecations.Count,
                "The deprecation is written once however many clients read " +
                "the old name:\n" + string.Join("\n", deprecations));
            StringAssert.Contains(deprecations[0], "deprecated");
            StringAssert.Contains(deprecations[0], NewName);
        }

        [TestMethod]
        public void OldConstant_HoldsTheOldName()
        {
#pragma warning disable CS0618 // The deprecated constant is the subject.
            Assert.AreEqual(OldName, DidClient.EndpointEnvironmentVariable);
#pragma warning restore CS0618
        }

        [TestMethod]
        public void NewConstant_HoldsTheNewName()
        {
            var field = typeof(DidClient).GetField(NewConstant);

            Assert.IsNotNull(field, $"DidClient has no {NewConstant}.");
            Assert.AreEqual(NewName, field.GetRawConstantValue());
        }

        [TestMethod]
        public void OldConstant_IsObsoleteAndPointsAtTheNewOne()
        {
            var obsolete = typeof(DidClient)
                .GetField("EndpointEnvironmentVariable")!
                .GetCustomAttribute<ObsoleteAttribute>();

            Assert.IsNotNull(
                obsolete, "EndpointEnvironmentVariable is not obsolete.");
            Assert.IsFalse(
                obsolete.IsError,
                "Code that uses the old constant must still compile.");
            StringAssert.Contains(obsolete.Message, NewConstant);
        }
    }
}
