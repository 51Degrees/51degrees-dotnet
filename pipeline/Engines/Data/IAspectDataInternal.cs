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

using FiftyOne.Pipeline.Engines.FlowElements;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Engines.Data
{
    /// <summary>
    /// What the engines write to the aspect data they process, other than
    /// its values. <see cref="AspectDataBase"/> implements it explicitly,
    /// so the engines reach these members without casting to the class.
    /// </summary>
    internal interface IAspectDataInternal
    {
        /// <summary>
        /// True if the upstream cloud request did not return a usable
        /// response. See <see cref="MarkCloudRequestFailed"/>.
        /// </summary>
        bool CloudRequestFailed { get; }

        /// <summary>
        /// Add an engine that populates this data, beside the one that
        /// created it.
        /// </summary>
        /// <param name="engine">The engine.</param>
        void AddEngine(IAspectEngine engine);

        /// <summary>
        /// Add a process task to the lazy loading tasks for this data. The
        /// property accessors only complete once all such tasks have.
        /// </summary>
        /// <param name="processTask">The task.</param>
        void AddProcessTask(Task processTask);

        /// <summary>
        /// Record that this data came from the results cache.
        /// </summary>
        void SetCacheHit();

        /// <summary>
        /// Mark this data as produced for a request whose upstream cloud
        /// call failed, so a missing property reports that reason.
        /// </summary>
        void MarkCloudRequestFailed();
    }
}
