/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2025 51 Degrees Mobile Experts Limited, Davidson House,
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
using System.Collections.Generic;
using System.Text;

namespace FiftyOne.Caching.Tests.Loaders
{
    /// <summary>
    /// Test loader that always returns null.
    /// Calls to the loader are tracked, as are cancellations.
    /// A delay can be provided at construction. This will be the time
    /// the Task takes before completing, or the time for the load method
    /// to return if using the ILoaderValue interface method.
    /// </summary>
    /// <typeparam name="TKey">
    /// Type for the key.
    /// </typeparam>
    /// <typeparam name="TValue">
    /// Type for the value.
    /// </typeparam>
    internal class NullLoader<TKey, TValue> : TrackingLoaderBase<TKey, TValue>
        where TValue : class
    {
        protected override TValue GetValue(TKey key)
        {
            return null;
        }
    }
}
