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

namespace FiftyOne.Pipeline.Engines.FlowElements
{
    /// <summary>
    /// An on-premise engine that can read the published date from the header
    /// of a data file without loading it. The data update service uses it to
    /// load a watched or downloaded file only when its data is newer than the
    /// data the engine has loaded. An engine without it has every such file
    /// loaded.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/data-updates.md#newfileavailable-via-remote-or-file-watcher">Specification</see>
    /// </summary>
    public interface IDataFilePublishedDateEngine
    {
        /// <summary>
        /// Read the published date from the header of the data file at
        /// <paramref name="filePath"/>, in the same form the engine gives
        /// <see cref="Data.IAspectEngineDataFile.DataPublishedDateTime"/> once
        /// it has loaded that file.
        /// </summary>
        /// <param name="dataFileIdentifier">
        /// The identifier of the data file the file is for. An engine with
        /// one data file ignores it.
        /// </param>
        /// <param name="filePath">
        /// The path of the file to read.
        /// </param>
        /// <returns>
        /// The published date in the file's header, in UTC. The data update
        /// service compares dates in UTC: it converts a local date and takes
        /// a date of unspecified kind to be UTC. It treats a returned
        /// <c>default(DateTime)</c> as no date and loads the file.
        /// </returns>
        /// <exception cref="Exception">
        /// The file is missing, cannot be read, is not a data file of the
        /// engine's kind, or its header has no published date. The data
        /// update service then loads the file, so the refresh reports why
        /// it cannot be used.
        /// </exception>
        DateTime GetDataFilePublishedDate(
            string dataFileIdentifier,
            string filePath);
    }
}
