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

using FiftyOne.Common.Wrappers;
using System;

namespace FiftyOne.Common.TestHelpers
{
    /// <summary>
    /// Test implementation of <see cref="IDateTimeWrapper"/> used to artificially 
    /// control the time returned to workers and builders for testing.
    /// </summary>
    public class TestDateTime : IDateTimeWrapper
    {
        public DateTime Now => Current.ToLocalTime();

        public DateTime UtcNow => Current;

        public DateTime Today => Current.Date;

        /// <summary>
        /// The current date time that the test service will return.
        /// </summary>
        public DateTime Current { get; private set; }

        /// <summary>
        /// Constructs a new instance of <see cref="TestDateTime"/>.
        /// </summary>
        /// <param name="dateTime"></param>
        public TestDateTime(DateTime dateTime)
        {
            Current = dateTime;
        }

        /// <summary>
        /// Increment the date time returned.
        /// </summary>
        /// <param name="increment"></param>
        public void Increment(TimeSpan increment)
        {
            Current = Current.Add(increment);
        }

        /// <summary>
        /// Explicitly sets the current datetime.
        /// </summary>
        /// <param name="value"></param>
        public void Set(DateTime value)
        {
            Current = value;
        }
    }
}