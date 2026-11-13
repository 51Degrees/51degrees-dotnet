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

using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.CloudRequestEngine.Tests
{
    /// <summary>
    /// Tests that the builder reads the cloud endpoint from the new
    /// environment variable first and from the old one when the new one is
    /// unset or empty, and that the old constant keeps its name and value.
    /// </summary>
    [TestClass]
    public class CloudRequestEngineOldEndpointNameTests
    {
        private const string NewName = "_51DEGREES_CLOUD_ENDPOINT";
        private const string OldName = "FOD_CLOUD_API_URL";
        private const string NewUrl = "http://localhost/new_name/";
        private const string OldUrl = "http://localhost/old_name/";
        private const string GivenUrl = "http://localhost/given/";

        /// <summary>
        /// Records the address of every request and answers the two
        /// discovery endpoints the builder calls.
        /// </summary>
        private sealed class RecordingHandler : HttpMessageHandler
        {
            public List<Uri> Requests { get; } = new List<Uri>();

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                lock (Requests)
                {
                    Requests.Add(request.RequestUri);
                }
                var last = request.RequestUri.Segments.Last()
                    .ToLowerInvariant();
                var content = last == Constants.EVIDENCE_KEYS_FILENAME
                    ? "[\"query.user-agent\"]"
                    : "{\"Products\":{\"device\":{\"DataTier\":" +
                        "\"CloudV4Free\",\"Properties\":[{\"Name\":" +
                        "\"IsMobile\",\"Type\":\"Boolean\",\"Category\":" +
                        "\"Device\",\"DelayExecution\":false}]}}}";
                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(content),
                    });
            }
        }

        private sealed class RecordingLogger : ILogger
        {
            public List<string> Entries { get; } = new List<string>();

            public IDisposable BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception exception,
                Func<TState, Exception, string> formatter)
            {
                lock (Entries)
                {
                    Entries.Add(logLevel + " " + formatter(state, exception));
                }
            }
        }

        private sealed class RecordingLoggerFactory : ILoggerFactory
        {
            public RecordingLogger Logger { get; } = new RecordingLogger();
            public void AddProvider(ILoggerProvider provider) { }
            public ILogger CreateLogger(string categoryName) => Logger;
            public void Dispose() { }
        }

        /// <summary>
        /// The logger every build that reads the old name is given. The
        /// deprecation is logged once in the process, so one logger has to
        /// see all of those builds for the count to mean anything.
        /// </summary>
        private static readonly RecordingLoggerFactory OldNameLoggers =
            new RecordingLoggerFactory();

        private RecordingHandler _handler;
        private string _previousNew;
        private string _previousOld;

        [TestInitialize]
        public void Init()
        {
            _handler = new RecordingHandler();
            _previousNew = Environment.GetEnvironmentVariable(NewName);
            _previousOld = Environment.GetEnvironmentVariable(OldName);
            Environment.SetEnvironmentVariable(NewName, null);
            Environment.SetEnvironmentVariable(OldName, null);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Environment.SetEnvironmentVariable(NewName, _previousNew);
            Environment.SetEnvironmentVariable(OldName, _previousOld);
        }

        /// <summary>
        /// Build an engine with no endpoint configured, so the builder
        /// takes it from the environment or the default.
        /// </summary>
        private void Build(ILoggerFactory loggers)
        {
            using var engine = new CloudRequestEngineBuilder(
                    loggers, new HttpClient(_handler))
                .SetResourceKey("abcdefgh")
                .Build();
        }

        /// <summary>
        /// Build an engine with its endpoint given in code, which the
        /// environment does not change.
        /// </summary>
        private void BuildWithEndpoint(ILoggerFactory loggers)
        {
            using var engine = new CloudRequestEngineBuilder(
                    loggers, new HttpClient(_handler))
                .SetEndPoint(GivenUrl)
                .SetResourceKey("abcdefgh")
                .Build();
        }

        /// <summary>
        /// Every request the build made went to an address under
        /// <paramref name="endpoint"/>.
        /// </summary>
        private void AssertEveryRequestWentTo(string endpoint)
        {
            Assert.AreNotEqual(0, _handler.Requests.Count);
            foreach (var request in _handler.Requests)
            {
                Assert.IsTrue(
                    request.AbsoluteUri.StartsWith(
                        endpoint, StringComparison.Ordinal),
                    $"A request went to '{request.AbsoluteUri}', which is " +
                    $"not under '{endpoint}'.");
            }
        }

        /// <summary>
        /// The log entries that say the old name is deprecated.
        /// </summary>
        private static List<string> Deprecations(RecordingLogger logger)
        {
            lock (logger.Entries)
            {
                return logger.Entries
                    .Where(entry => entry.Contains(OldName))
                    .ToList();
            }
        }

        [TestMethod]
        public void OnlyOldNameSet_GivesThatEndpoint()
        {
            Environment.SetEnvironmentVariable(OldName, OldUrl);

            Build(OldNameLoggers);

            AssertEveryRequestWentTo(OldUrl);
        }

#if NET9_0_OR_GREATER
        // .NET 8 deletes a variable that is set to an empty value, so an
        // empty value can only be set up from .NET 9.
        [TestMethod]
        public void NewNameEmpty_OldNameSet_GivesTheOldEndpoint()
        {
            Environment.SetEnvironmentVariable(NewName, string.Empty);
            Environment.SetEnvironmentVariable(OldName, OldUrl);

            Build(OldNameLoggers);

            AssertEveryRequestWentTo(OldUrl);
        }

        [TestMethod]
        public void NewNameEmpty_OldNameNotSet_GivesTheDefault()
        {
            Environment.SetEnvironmentVariable(NewName, string.Empty);

            Build(new RecordingLoggerFactory());

            AssertEveryRequestWentTo(Constants.CLOUD_URI_DEFAULT);
        }
#endif

        [TestMethod]
        public void EndpointGiven_WinsOverBothNames()
        {
            Environment.SetEnvironmentVariable(NewName, NewUrl);
            Environment.SetEnvironmentVariable(OldName, OldUrl);

            BuildWithEndpoint(new RecordingLoggerFactory());

            AssertEveryRequestWentTo(GivenUrl);
        }

        [TestMethod]
        public void EndpointGiven_OldNameIsNotReported()
        {
            Environment.SetEnvironmentVariable(OldName, OldUrl);
            var loggers = new RecordingLoggerFactory();
            // The deprecation is logged once in a process, so this test
            // runs as a process that has not logged it, and puts the
            // record back for the tests that follow.
            var logged = typeof(CloudRequestEngineBuilder).GetField(
                "_oldEndpointNameLogged",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(
                logged,
                "CloudRequestEngineBuilder has no _oldEndpointNameLogged.");
            var previous = logged.GetValue(null);
            logged.SetValue(null, 0);
            try
            {
                BuildWithEndpoint(loggers);
            }
            finally
            {
                logged.SetValue(null, previous);
            }

            AssertEveryRequestWentTo(GivenUrl);
            Assert.AreEqual(
                0,
                Deprecations(loggers.Logger).Count,
                "Every endpoint was given, so the old name was not used " +
                "and nothing is said about it.");
        }

        [TestMethod]
        public void BothNamesSet_NewNameWins()
        {
            Environment.SetEnvironmentVariable(NewName, NewUrl);
            Environment.SetEnvironmentVariable(OldName, OldUrl);
            var loggers = new RecordingLoggerFactory();

            Build(loggers);

            AssertEveryRequestWentTo(NewUrl);
            Assert.AreEqual(
                0,
                Deprecations(loggers.Logger).Count,
                "The old name was not read, so nothing is said about it.");
        }

        [TestMethod]
        public void NeitherNameSet_GivesTheDefault()
        {
            var loggers = new RecordingLoggerFactory();

            Build(loggers);

            AssertEveryRequestWentTo(Constants.CLOUD_URI_DEFAULT);
            Assert.AreEqual(0, Deprecations(loggers.Logger).Count);
        }

        [TestMethod]
        public void OldNameRead_DeprecationIsLoggedOnce()
        {
            Environment.SetEnvironmentVariable(OldName, OldUrl);

            Build(OldNameLoggers);
            Build(OldNameLoggers);

            var deprecations = Deprecations(OldNameLoggers.Logger);
            Assert.AreEqual(
                1,
                deprecations.Count,
                "The deprecation is logged once however many builds read " +
                "the old name:\n" + string.Join("\n", deprecations));
            StringAssert.StartsWith(
                deprecations[0], LogLevel.Warning.ToString());
            StringAssert.Contains(deprecations[0], "deprecated");
            StringAssert.Contains(deprecations[0], NewName);
        }

        [TestMethod]
        public void OldConstant_HoldsTheOldName()
        {
#pragma warning disable CS0618 // The deprecated constant is the subject.
            Assert.AreEqual(OldName, Constants.FOD_CLOUD_API_URL);
#pragma warning restore CS0618
            Assert.AreEqual(NewName, Constants.CLOUD_ENDPOINT_ENV_VAR);
        }

        [TestMethod]
        public void OldConstant_IsObsoleteAndPointsAtTheNewOne()
        {
            var obsolete = typeof(Constants)
                .GetField("FOD_CLOUD_API_URL")
                .GetCustomAttribute<ObsoleteAttribute>();

            Assert.IsNotNull(obsolete);
            Assert.IsFalse(
                obsolete.IsError,
                "Code that uses the old constant must still compile.");
            StringAssert.Contains(
                obsolete.Message, nameof(Constants.CLOUD_ENDPOINT_ENV_VAR));
        }
    }
}
