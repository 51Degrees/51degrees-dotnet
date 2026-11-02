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

namespace FiftyOne.Common.TestHelpers
{
    /// <summary>
    /// An implementation of <see cref="ILoggerFactory"/> that will always 
    /// create <see cref="TestLogger"/> instances.
    /// </summary>
    public class TestLoggerFactory : ILoggerFactory
    {
        private readonly object _lock = new object();

        // A list of the loggers that have been created by this factory.
        public List<TestLogger> Loggers { get; set; } = new List<TestLogger>();

        public void AddProvider(ILoggerProvider provider)
        {
            throw new NotImplementedException();
        }

        public ILogger CreateLogger(string categoryName)
        {
            var logger = new TestLogger(categoryName);
            lock (_lock)
            {
                Loggers.Add(logger);
            }
            return logger;
        }

        public void Dispose()
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
            var allWarnings = Loggers.SelectMany(l => l.WarningEntries);
            if (allWarnings.Count() > count)
            {
                var message = $"{allWarnings.Count()} warnings occurred " +
                    "during test " +
                    $" {(count > 0 ? $"(expected no more than {count})" : "")}:";
                foreach (var warning in allWarnings)
                {
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
            var allErrors = Loggers.SelectMany(l => l.ErrorEntries);
            if (allErrors.Count() > count)
            {
                var message = $"{allErrors.Count()} errors occurred during test" +
                    $"{(count > 0 ? $" (expected no more than {count})" : "")}:";
                foreach (var error in allErrors)
                {
                    message += Environment.NewLine;
                    message += Environment.NewLine;
                    message += error;
                }
                Assert.Fail(message);
            }
        }
    }
}
