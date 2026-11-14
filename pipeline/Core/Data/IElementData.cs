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

using FiftyOne.Pipeline.Core.FlowElements;

namespace FiftyOne.Pipeline.Core.Data
{
    /// <summary>
    /// Represents property values that have been determined by a 
    /// specific FlowElement based on the supplied evidence.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/conceptual-overview.md#element-data">Specification</see>
    /// </summary>
    /// <remarks>
    /// An instance is written only by the Flow Element that creates it,
    /// while that Flow Element processes. It is not intended to be written
    /// from several threads at once, nor read while it is being written.
    /// Once the Flow Element has finished, the instance is safe to read from
    /// several threads at once, as the Flow Elements of a parallel group do.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/thread-safety.md#element-data">thread safety specification</see>.
    /// </remarks>
    public interface IElementData : IData
    {
        /// <summary>
        /// The <see cref="IPipeline"/> that this instance was created by
        /// </summary>
        IPipeline Pipeline { get; set; }
    }
}
