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

using FiftyOne.Pipeline.Core.Exceptions;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.Pipeline.Core.FlowElements
{
    /// <summary>
    /// Refuses a Pipeline whose usage sharing element is not its last Flow
    /// Element or is in a parallel group.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/a90ba516a3bfadc58dd7a51535d83af6bdaed291/pipeline-specification/pipeline-elements/usage-sharing-element.md#position-in-the-pipeline">Specification</see>
    /// </summary>
    internal static class UsageSharingPosition
    {
        /// <summary>
        /// Throws if an <see cref="IUsageSharingElement"/> is anywhere but
        /// last in the elements, or is in a parallel group.
        /// </summary>
        /// <param name="elements">
        /// The elements of the Pipeline, in the order they run.
        /// </param>
        /// <exception cref="PipelineConfigurationException">
        /// Thrown if a usage sharing element is anywhere else.
        /// </exception>
        internal static void Check(IReadOnlyList<IFlowElement> elements)
        {
            for (var i = 0; i < elements.Count; i++)
            {
                var element = elements[i];
                if ((element is IUsageSharingElement &&
                        i < elements.Count - 1) ||
                    (element is ParallelElements group &&
                        HasUsageSharing(group)))
                {
                    throw new PipelineConfigurationException(
                        Messages.ExceptionUsageSharingNotLast);
                }
            }
        }

        private static bool HasUsageSharing(ParallelElements group) =>
            group.FlowElements.Any(e =>
                e is IUsageSharingElement ||
                (e is ParallelElements inner && HasUsageSharing(inner)));
    }
}
