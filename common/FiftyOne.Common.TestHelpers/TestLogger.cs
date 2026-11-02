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

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FiftyOne.Common.TestHelpers
{
    public class TestLogger<TLog> : TestLogger, ILogger<TLog>
    {
        public TestLogger() : base(typeof(TLog).FullName)
        {
        }
    }

    /// <summary>
    /// Implementation of <see cref="ILogger"/> that will track errors and 
    /// warnings that are logged in order to later assert that no
    /// more than a certain number of errors/warnings have been logged.
    /// </summary>
    public class TestLogger : ILogger
    {
        /// <summary>
        /// Container for the logged data and its meta.
        /// </summary>
        public sealed class ExtendedLogEntry
        {
            public readonly DateTime Timestamp;
            public readonly LogLevel LogLevel;
            public readonly string Message;
            public readonly Exception Exception;

            internal ExtendedLogEntry(
                DateTime timestamp,
                LogLevel logLevel,
                string message,
                Exception exception)
            {
                Timestamp = timestamp;
                LogLevel = logLevel;
                Message = message;
                Exception = exception;
            }

            public override string ToString()
            {
                StringBuilder s = new StringBuilder();
                s.Append(nameof(ExtendedLogEntry));
                s.Append("(");
                s.Append(nameof(Timestamp)); s.Append(":"); s.Append(Timestamp);
                s.Append(", ");
                s.Append(nameof(LogLevel)); s.Append(":"); s.Append(LogLevel);
                s.Append(", ");
                s.Append(nameof(Message)); s.Append(":"); s.Append(Message);
                s.Append(", ");
                s.Append(nameof(Exception)); s.Append(":"); s.Append(Exception);
                s.Append(")");
                return s.ToString();
            }
        }

        /// <summary>
        /// List of log entries sent to the logger. 
        /// </summary>
        private readonly List<ExtendedLogEntry> _entries = new List<ExtendedLogEntry>();

        /// <summary>
        /// List of log entries sent to the logger. 
        /// </summary>
        public IReadOnlyList<ExtendedLogEntry> ExtendedEntries => CurrentEntries.ToList();
        
        /// <summary>
        /// Locks the length of the collection to allow mutations during enumeration without causing exception.
        /// </summary>
        private IEnumerable<ExtendedLogEntry> CurrentEntries => _entries.Take(_entries.Count);

        /// <summary>
        /// List of log entries sent to the logger. 
        /// </summary>
        public IReadOnlyList<KeyValuePair<LogLevel, string>> Entries 
            => CurrentEntries.Select(t => new KeyValuePair<LogLevel, string>(t.LogLevel, t.Message)).ToList();

        /// <summary>
        /// Convenience filter for convenience properties. 
        /// </summary>
        private IEnumerable<string> GetMessages(LogLevel logLevel)
            => CurrentEntries.Where(i => i.LogLevel == logLevel).Select(i => i.Message);

        /// <summary>
        /// Enumerable of the text of critical entries that have been logged.
        /// </summary>
        public IEnumerable<string> CriticalEntries => GetMessages(LogLevel.Critical);

        /// <summary>
        /// Enumerable of the text of warning entries that have been logged.
        /// </summary>
        public IEnumerable<string> WarningEntries => GetMessages(LogLevel.Warning);

        /// <summary>
        /// Enumerable of the text of error entries that have been logged.
        /// </summary>
        public IEnumerable<string> ErrorEntries => GetMessages(LogLevel.Error);

        /// <summary>
        /// Enumerable of the text of information entries that have been logged.
        /// </summary>
        public IEnumerable<string> InfoEntries => GetMessages(LogLevel.Information);

        /// <summary>
        /// Enumerable of the text of debug entries that have been logged.
        /// </summary>
        public IEnumerable<string> DebugEntries => GetMessages(LogLevel.Debug);

        /// <summary>
        /// Enumerable of the text of trace entries that have been logged.
        /// </summary>
        public IEnumerable<string> TraceLogged => GetMessages(LogLevel.Trace);

        /// <summary>
        /// The category is usually the name of the type that the logger is for (if any)
        /// </summary>
        public string Category { get; private set; }

        [Obsolete]
        public IEnumerable<string> WarningsLogged => WarningEntries;

        [Obsolete]
        public IEnumerable<string> ErrorsLogged => ErrorEntries;

        public TestLogger(string category)
        {
            Category = category;
        }

        public TestLogger() : this("")
        {
        }

        /// <summary>
        /// Throw an AssertFailedException if more than the specified number
        /// of warnings have been logged.
        /// </summary>
        /// <param name="count">
        /// The maximum number of logged warnings to allow.
        /// </param>
        public void AssertMaxWarnings(int count)
        {
            if (WarningEntries != null && WarningEntries.Count() > count)
            {
                var message = $"{WarningEntries.Count()} warnings occurred " +
                    "during test " +
                    $" {(count > 0 ? $"(expected no more than {count})" : "")}:";
                foreach (var warning in WarningEntries)
                {
                    message += Environment.NewLine;
                    message += Environment.NewLine;
                    message += warning;
                }
                Assert.Fail(message);
            }
        }

        /// <summary>
        /// Throw an AssertFailedException if more than the specified number
        /// of errors have been logged.
        /// </summary>
        /// <param name="count">
        /// The maximum number of logged errors to allow.
        /// </param>
        public void AssertMaxErrors(int count)
        {
            if (ErrorEntries != null && ErrorEntries.Count() > count)
            {
                var message = $"{ErrorEntries.Count()} errors occurred during test" +
                    $"{(count > 0 ? $" (expected no more than {count})" : "")}:";
                foreach (var error in ErrorEntries)
                {
                    message += Environment.NewLine;
                    message += Environment.NewLine;
                    message += error;
                }
                Assert.Fail(message);
            }
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            throw new NotImplementedException();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel, 
            EventId eventId, 
            TState state, 
            Exception exception, 
            Func<TState, Exception, string> formatter)
        {
            var value = formatter(state, exception);
            _entries.Add(new ExtendedLogEntry(
                DateTime.Now,
                logLevel,
                value,
                exception));
        }
    }
}
