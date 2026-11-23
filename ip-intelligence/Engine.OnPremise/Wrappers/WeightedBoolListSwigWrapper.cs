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

using FiftyOne.IpIntelligence.Engine.OnPremise.Interop;
using FiftyOne.Pipeline.Core.Data;
using System.Collections;
using System.Collections.Generic;

namespace FiftyOne.IpIntelligence.Engine.OnPremise.Wrappers
{
    /// <summary>
    /// A managed copy of a native list of weighted values, so the
    /// native list is freed as soon as the value has been read.
    /// </summary>
    internal class WeightedBoolListSwigWrapper : IReadOnlyList<IWeightedValue<bool>>
    {
        private readonly IWeightedValue<bool>[] _values;

        public IWeightedValue<bool> this[int index] => _values[index];

        public int Count => _values.Length;

        public WeightedBoolListSwigWrapper(
            WeightedBoolListSwig instance)
        {
            _values = new IWeightedValue<bool>[instance.Count];
            for (int i = 0; i < _values.Length; i++)
            {
                var item = instance[i];
                _values[i] = new WeightedValue<bool>(
                    item.getShortRawWeight(),
                    item.getValue());
            }
        }

        public IEnumerator<IWeightedValue<bool>> GetEnumerator()
        {
            return ((IEnumerable<IWeightedValue<bool>>)_values).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
