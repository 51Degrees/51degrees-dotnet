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

using FiftyOne.Pipeline.Core.Configuration;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Web.Shared
{
    /// <summary>
    /// Where the web integration adds an element to a configuration, which
    /// is before the usage sharing element, as that is always last.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/a90ba516a3bfadc58dd7a51535d83af6bdaed291/pipeline-specification/features/web-integration.md#pipeline-configuration">Specification</see>
    /// </summary>
    internal static class UsageSharingPlace
    {
        /// <summary>
        /// Part of the name of every usage sharing builder, as a builder is
        /// named by its type with or without 'Builder' at the end.
        /// </summary>
        private const string UsageSharingName = "ShareUsage";

        /// <summary>
        /// The index to insert an element at so it comes after every other
        /// element but usage sharing.
        /// </summary>
        /// <param name="elements">The configured elements.</param>
        /// <returns>
        /// The index of the first usage sharing element, or the number of
        /// elements when there is none.
        /// </returns>
        internal static int Before(IList<ElementOptions> elements)
        {
            for (var i = 0; i < elements.Count; i++)
            {
                if (elements[i].BuilderName?.IndexOf(
                        UsageSharingName,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return i;
                }
            }
            return elements.Count;
        }
    }
}
