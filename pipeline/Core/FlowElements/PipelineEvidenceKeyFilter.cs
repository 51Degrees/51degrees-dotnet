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

using FiftyOne.Pipeline.Core.Data;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Core.FlowElements
{
    /// <summary>
    /// The Evidence key filter of a <see cref="Pipeline"/>, and of a group
    /// of elements run in parallel (<see cref="ParallelElements"/>). It
    /// holds the Flow Elements and asks each element for its own filter
    /// every time it is used. It never copies an element's keys.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Creating it reads no element's filter, so building a Pipeline does
    /// not depend on any element being ready. A Cloud Request Engine that
    /// could not reach the cloud at start-up throws from its filter until
    /// discovery succeeds. The cloud request engine specification says such
    /// a failure "MUST NOT fail the build", and that a result obtained
    /// before discovery has completed must not be kept. Asking at the time
    /// meets both: the engine is asked again on the request that needs it,
    /// and its keys are included once it answers. An element whose filter
    /// changes, for example when its data is reloaded, is also followed.
    /// </para>
    /// <para>
    /// A key is included if any element's filter includes it. Its order
    /// is the one <see cref="EvidenceKeyFilterAggregator"/> gives for the
    /// same filters in the same order. That includes the filter of a
    /// parallel group, which is one of these. Its elements' filters count
    /// at the group's place, as the aggregator's did.
    /// </para>
    /// <para>
    /// It holds nothing that changes, so every thread can use it with no
    /// lock.
    /// </para>
    /// See the <see href="https://github.com/51Degrees/specifications/blob/9d12262654fe8914a5497bdacd85ca733898dd8b/pipeline-specification/pipeline-elements/cloud-request-engine.md?plain=1#L263-L271">Specification</see>
    /// </remarks>
    internal sealed class PipelineEvidenceKeyFilter : IEvidenceKeyFilter
    {
        /// <summary>
        /// The Pipeline's Flow Elements, in the order they were added.
        /// </summary>
        private readonly IReadOnlyList<IFlowElement> _elements;

        /// <summary>
        /// Constructor. Reads no element's filter.
        /// </summary>
        /// <param name="elements">
        /// The Pipeline's Flow Elements, which must not change after this.
        /// </param>
        internal PipelineEvidenceKeyFilter(IReadOnlyList<IFlowElement> elements)
        {
            _elements = elements ??
                throw new ArgumentNullException(nameof(elements));
        }

        /// <summary>
        /// Asks each element for its filter, and keeps none of them. An
        /// element whose filter is not yet available throws, as it would
        /// from <see cref="Include"/>.
        /// </summary>
        internal void AskEachElement()
        {
            for (int i = 0; i < _elements.Count; i++)
            {
                _ = _elements[i].EvidenceKeyFilter;
            }
        }

        /// <summary>
        /// True if any element's filter, asked now, includes the key.
        /// </summary>
        /// <param name="key">
        /// The key to check.
        /// </param>
        /// <returns>
        /// True if the key is included and false if not.
        /// </returns>
        public bool Include(string key)
        {
            for (int i = 0; i < _elements.Count; i++)
            {
                var filter = _elements[i].EvidenceKeyFilter;
                if (filter != null && filter.Include(key))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The order of the key, from the elements' filters asked now, with
        /// the precedence <see cref="EvidenceKeyFilterAggregator"/> gives.
        /// That is, first the case insensitive inclusion lists in element
        /// order, where the earlier element wins, then every other filter in
        /// element order.
        /// </summary>
        /// <param name="key">
        /// The key to check.
        /// </param>
        /// <returns>
        /// The order, where lower values indicate a higher order of
        /// precedence. Null if no element's filter includes the key.
        /// </returns>
        public int? Order(string key)
        {
            return OrderFromLists(key) ?? OrderFromOtherFilters(key);
        }

        /// <summary>
        /// The order of the key from the first case insensitive inclusion
        /// list that holds it, in element order. A parallel group's lists
        /// count at the group's place, as
        /// <see cref="EvidenceKeyFilterAggregator.AddFilter"/> merged them.
        /// </summary>
        private int? OrderFromLists(string key)
        {
            for (int i = 0; i < _elements.Count; i++)
            {
                var filter = _elements[i].EvidenceKeyFilter;
                if (filter is PipelineEvidenceKeyFilter group)
                {
                    var groupOrder = group.OrderFromLists(key);
                    if (groupOrder.HasValue)
                    {
                        return groupOrder;
                    }
                }
                else if (filter is EvidenceKeyFilterWhitelist list &&
                    IsMergedList(list) &&
                    list.Whitelist.TryGetValue(key, out var listOrder))
                {
                    return listOrder;
                }
            }
            return null;
        }

        /// <summary>
        /// The order of the key from the first other filter that gives one,
        /// in element order. A parallel group's other filters count at the
        /// group's place.
        /// </summary>
        private int? OrderFromOtherFilters(string key)
        {
            for (int i = 0; i < _elements.Count; i++)
            {
                var filter = _elements[i].EvidenceKeyFilter;
                if (filter == null)
                {
                    continue;
                }
                int? order;
                if (filter is PipelineEvidenceKeyFilter group)
                {
                    order = group.OrderFromOtherFilters(key);
                }
                else if (filter is EvidenceKeyFilterWhitelist list &&
                    IsMergedList(list) &&
                    list.GetType() == typeof(EvidenceKeyFilterWhitelist))
                {
                    // Already answered from its list above.
                    continue;
                }
                else
                {
                    order = filter.Order(key);
                }
                if (order.HasValue)
                {
                    return order;
                }
            }
            return null;
        }

        /// <summary>
        /// True for an inclusion list that
        /// <see cref="EvidenceKeyFilterAggregator.AddFilter"/> merges into
        /// its own list, being one with the case insensitive comparer.
        /// </summary>
        private static bool IsMergedList(EvidenceKeyFilterWhitelist list) =>
            list.Comparer == StringComparer.OrdinalIgnoreCase;
    }
}
