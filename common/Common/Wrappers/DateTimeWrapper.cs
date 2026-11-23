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

using System;

namespace FiftyOne.Common.Wrappers
{
    /// <summary>
    /// Wraps the .NET DateTime properties that return the current real date 
    /// and time information.
    /// </summary>
    public class DateTimeWrapper : IDateTimeWrapper
    {
        /// <summary>
        /// Gets a <see cref="DateTime"/> object that is set to the current 
        /// date and time on this computer, expressed as the local time.
        /// </summary>
        public DateTime Now
        {
            get { return DateTime.Now; }
        }

        /// <summary>
        /// Gets a <see cref="DateTime"/> object that is set to the current 
        /// date and time on this computer, expressed as the Coordinated 
        /// Universal Time (UTC).
        /// </summary>
        public DateTime UtcNow
        {
            get { return DateTime.UtcNow; }
        }

        /// <summary>
        /// Gets the current date. An object that is set to today's date, with 
        /// the time component set to 00:00:00.
        /// </summary>
        public DateTime Today
        {
            get { return DateTime.Today; }
        }
    }
}
