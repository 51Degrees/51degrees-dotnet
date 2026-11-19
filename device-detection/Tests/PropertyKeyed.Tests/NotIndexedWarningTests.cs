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

using FiftyOne.DeviceDetection.Hash.Engine.OnPremise.FlowElements;
using FiftyOne.DeviceDetection.PropertyKeyed.FlowElements;
using FiftyOne.DeviceDetection.TestHelpers;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.DeviceDetection.PropertyKeyed.Tests;

/// <summary>
/// A property keyed engine whose property the Hash engine does not index
/// works, but reads every profile on each lookup. The specification only
/// recommends the index, so the configuration is not refused. One warning
/// is logged when the Pipeline is built, saying how to index the property,
/// and none for each request.
/// </summary>
[TestClass]
public class NotIndexedWarningTests
{
    private const string PROPERTY = "PlatformName";
    private const string ELEMENT_DATA_KEY = "platform-profiles";
    private const string HOW_TO_INDEX = "SetIndexedProperties";
    private const int REQUESTS = 3;

    [TestMethod]
    public void NotIndexed_OneWarningWhenThePipelineIsBuilt()
    {
        var warnings = WarningsFrom(index: false);
        Assert.HasCount(
            1,
            warnings,
            "Expected one warning naming " + HOW_TO_INDEX + " for " +
            REQUESTS + " requests, but got: " + string.Join(" | ", warnings));
        Assert.Contains(PROPERTY, warnings[0]);
    }

    [TestMethod]
    public void Indexed_NoWarning()
    {
        var warnings = WarningsFrom(index: true);
        Assert.IsEmpty(
            warnings,
            "No warning is needed when the property is indexed, but got: " +
            string.Join(" | ", warnings));
    }

    /// <summary>
    /// Builds a Pipeline, processes a few requests, and returns the
    /// warnings that say how to index the property.
    /// </summary>
    private static List<string> WarningsFrom(bool index)
    {
        var capture = new MessageCapture();
        using (var loggerFactory = LoggerFactory.Create(b => b
            .AddProvider(capture)
            .SetMinimumLevel(LogLevel.Trace)))
        {
            var dataFile = Utils.GetFilePath(
                Constants.LITE_HASH_DATA_FILE_NAME).FullName;
            var builder = new DeviceDetectionHashEngineBuilder(loggerFactory)
                .SetAutoUpdate(false)
                .SetDataFileSystemWatcher(false);
            if (index)
            {
                builder.SetIndexedProperties(PROPERTY);
            }
            using (var hash = builder.Build(dataFile, false))
            using (var engine = new PropertyKeyedDeviceEngine(
                loggerFactory,
                new List<string> { PROPERTY },
                PROPERTY,
                ELEMENT_DATA_KEY))
            using (var pipeline = new PipelineBuilder(loggerFactory)
                .AddFlowElement(hash)
                .AddFlowElement(engine)
                .Build())
            {
                for (var i = 0; i < REQUESTS; i++)
                {
                    using (var data = pipeline.CreateFlowData())
                    {
                        data.AddEvidence("query." + PROPERTY, "Android");
                        data.Process();
                    }
                }
            }
        }
        return capture.Warnings
            .Where(w => w.Contains(HOW_TO_INDEX, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>
    /// Keeps the text of every warning logged.
    /// </summary>
    private sealed class MessageCapture : ILoggerProvider
    {
        private readonly List<string> _warnings = new List<string>();
        private readonly object _lock = new object();

        public IReadOnlyList<string> Warnings
        {
            get { lock (_lock) { return _warnings.ToList(); } }
        }

        public ILogger CreateLogger(string categoryName) =>
            new Logger(this);

        public void Dispose() { }

        private sealed class Logger : ILogger
        {
            private readonly MessageCapture _owner;

            public Logger(MessageCapture owner) => _owner = owner;

            public IDisposable BeginScope<TState>(TState state) => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception exception,
                Func<TState, Exception, string> formatter)
            {
                if (logLevel != LogLevel.Warning)
                {
                    return;
                }
                lock (_owner._lock)
                {
                    _owner._warnings.Add(formatter(state, exception));
                }
            }
        }
    }
}
