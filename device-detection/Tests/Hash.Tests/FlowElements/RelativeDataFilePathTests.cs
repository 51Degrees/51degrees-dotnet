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

using FiftyOne.DeviceDetection.Hash.Engine.OnPremise.FlowElements;
using FiftyOne.Pipeline.Engines;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace FiftyOne.DeviceDetection.Hash.Tests.FlowElements
{
    /// <summary>
    /// An engine given its data file by a path relative to the working
    /// directory loads that file, and keeps the full path it resolves to.
    /// </summary>
    [TestClass]
    public class RelativeDataFilePathTests
    {
        [TestMethod]
        public void Build_RelativePath_LoadsTheFileItNames()
        {
            var full = TestHelpers.Utils.GetFilePath(
                TestHelpers.Constants.LITE_HASH_DATA_FILE_NAME).FullName;
            var relative = RelativeToWorkingDirectory(full);
            var isRelative = Path.IsPathRooted(relative) == false;
            Assert.IsTrue(isRelative,
                $"'{relative}' is not relative, so it cannot test this.");

            using (var engine = new DeviceDetectionHashEngineBuilder(
                    new LoggerFactory())
                .SetPerformanceProfile(PerformanceProfiles.LowMemory)
                .SetAutoUpdate(false)
                .SetDataFileSystemWatcher(false)
                .SetDataUpdateOnStartup(false)
                .Build(relative, false))
            {
                var kept = engine.DataFiles[0].DataFilePath;
                var keptIsFull = Path.IsPathRooted(kept);
                Assert.IsTrue(keptIsFull,
                    $"The engine kept the relative path '{kept}'.");
                Assert.AreEqual(full, Path.GetFullPath(kept),
                    "The engine kept a path to another file.");
            }
        }

        /// <summary>
        /// The path relative to the working directory, written with this
        /// system's separator.
        /// </summary>
        private static string RelativeToWorkingDirectory(string path)
        {
            var directory = Directory.GetCurrentDirectory();
            if (directory.EndsWith(
                Path.DirectorySeparatorChar.ToString(),
                StringComparison.Ordinal) == false)
            {
                directory += Path.DirectorySeparatorChar;
            }
            var relative = new Uri(directory).MakeRelativeUri(new Uri(path));
            return Uri.UnescapeDataString(relative.ToString())
                .Replace('/', Path.DirectorySeparatorChar);
        }
    }
}
