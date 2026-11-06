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

using FiftyOne.IpIntelligence.Engine.OnPremise.FlowElements;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using static FiftyOne.IpIntelligence.Examples.ExampleUtils;

namespace FiftyOne.IpIntelligence.Examples.OnPremise
{
    public static class ExampleUtils
    {
        /// <summary>
        /// Shown in place of a value for a property that the data file in
        /// use does not have. Every property the examples ask for is in the
        /// Enterprise data file.
        /// </summary>
        public const string NEEDS_ENTERPRISE = "needs the Enterprise data file";

        /// <summary>
        /// Get information about the specified data file
        /// </summary>
        /// <param name="dataFile"></param>
        /// <param name="engineBuilder"></param>
        public static DataFileInfo GetDataFileInfo(string dataFile,
            IpiOnPremiseEngineBuilder engineBuilder)
        {
            DataFileInfo result = new DataFileInfo();

            using (var engine = engineBuilder
                .Build(dataFile, false))
            {
                result = GetDataFileInfo(engine);
            }

            return result;
        }

        /// <summary>
        /// Get information about the data file used by the specified engine
        /// </summary>
        /// <param name="engine"></param>
        public static DataFileInfo GetDataFileInfo(IpiOnPremiseEngine engine)
        {
            DataFileInfo result = new DataFileInfo();
            result.PublishDate = engine.DataFiles[0].DataPublishedDateTime;
            result.Tier = engine.DataSourceTier;
            result.Filepath = engine.DataFiles[0].DataFilePath;
            result.Free = IsFreeDataFile(engine);
            return result;
        }

        /// <summary>
        /// Display information about the data file and log warnings if specific requirements
        /// are not met.
        /// </summary>
        /// <param name="pipeline"></param>
        /// <param name="logger"></param>
        public static void CheckDataFile(IPipeline pipeline, ILogger logger)
        {
            // Get the 'engine' element within the pipeline that performs IP Intelligence.
            // We can use this to get details about the data file as well as meta-data describing
            // things such as the available properties.
            var engine = pipeline.GetElement<IpiOnPremiseEngine>();
            CheckDataFile(engine, logger);
        }

        /// <summary>
        /// Display information about the data file and log warnings if specific requirements
        /// are not met.
        /// </summary>
        /// <param name="engine"></param>
        /// <param name="logger"></param>
        public static void CheckDataFile(IpiOnPremiseEngine engine, ILogger logger)
        {
            if (engine != null)
            {
                var info = GetDataFileInfo(engine);
                LogDataFileInfo(info, logger);
                LogDataFileStandardWarnings(info, logger);
            }
        }

        /// <summary>
        /// Reports whether the engine's data file has the property. A free
        /// data file leaves out the properties only the Enterprise data file
        /// has.
        /// </summary>
        public static bool HasProperty(IpiOnPremiseEngine engine, string name)
        {
            return engine.Properties.Any(p => string.Equals(
                p.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Reports whether the engine uses one of the free data files, which
        /// have only the registration properties (Lite) or Asn and AsnName
        /// (Asn). The tier cannot tell them apart, as every IP Intelligence
        /// file reports the same one. The Enterprise data file adds the
        /// range, location and connection properties, IpRangeStart among
        /// them.
        /// </summary>
        public static bool IsFreeDataFile(IpiOnPremiseEngine engine)
        {
            return HasProperty(engine, "IpRangeStart") == false;
        }

        /// <summary>
        /// For an example that needs properties only the Enterprise data file
        /// has, reports when the data file has none of them, and then says so
        /// and where to ask for a copy of the Enterprise data file. The data
        /// file is opened with the low memory profile, which reads little of
        /// it.
        /// </summary>
        /// <param name="dataFile">
        /// The data file the example would use.
        /// </param>
        /// <param name="purpose">
        /// What the example does, as the start of a sentence, for example
        /// "Comparing known locations with IP Intelligence".
        /// </param>
        /// <param name="properties">
        /// The properties the example needs.
        /// </param>
        /// <param name="output">
        /// Where to write the explanation.
        /// </param>
        /// <param name="contactUs">
        /// The link to the contact page, tagged with the example's file so that
        /// an enquiry can be traced to the example.
        /// </param>
        /// <returns>
        /// True when the data file has none of the properties, so the example
        /// has nothing to show.
        /// </returns>
        public static bool LacksEnterpriseProperties(
            string dataFile,
            string purpose,
            IReadOnlyList<string> properties,
            TextWriter output,
            string contactUs)
        {
            using (var engine = new IpiOnPremiseEngineBuilder(NullLoggerFactory.Instance)
                .SetPerformanceProfile(PerformanceProfiles.LowMemory)
                .SetAutoUpdate(false)
                .SetDataFileSystemWatcher(false)
                .SetDataUpdateOnStartup(false)
                .Build(dataFile, false))
            {
                return LacksEnterpriseProperties(
                    engine, purpose, properties, output, contactUs);
            }
        }

        /// <summary>
        /// As the overload that takes a data file, for an engine the example
        /// has already built.
        /// </summary>
        public static bool LacksEnterpriseProperties(
            IpiOnPremiseEngine engine,
            string purpose,
            IReadOnlyList<string> properties,
            TextWriter output,
            string contactUs)
        {
            if (properties.Any(p => HasProperty(engine, p)))
            {
                return false;
            }
            output.WriteLine();
            output.WriteLine($"{purpose} needs the {JoinNames(properties)} properties, " +
                "which only the Enterprise data file has. The data file in use has " +
                "none of them, so the example stops here. Contact us for a copy of " +
                $"the Enterprise data file at {contactUs}");
            return true;
        }

        /// <summary>
        /// Writes, after an example has shown properties that the data file in
        /// use does not have, where to ask for a copy of the Enterprise data
        /// file.
        /// </summary>
        /// <param name="output"></param>
        /// <param name="contactUs">
        /// The link to the contact page, tagged with the example's file so that
        /// an enquiry can be traced to the example.
        /// </param>
        public static void WriteEnterpriseNote(TextWriter output, string contactUs)
        {
            output.WriteLine();
            output.WriteLine("Some of these properties need the Enterprise data " +
                $"file. Contact us for a copy at {contactUs}");
        }

        /// <summary>
        /// Writes, after an example that shows no properties has run on a free
        /// data file, what the Enterprise data file adds and where to ask for a
        /// copy.
        /// </summary>
        /// <param name="output"></param>
        /// <param name="contactUs">
        /// The link to the contact page, tagged with the example's file so that
        /// an enquiry can be traced to the example.
        /// </param>
        public static void WriteEnterpriseOffer(TextWriter output, string contactUs)
        {
            output.WriteLine();
            output.WriteLine("This example used a free data file. The Enterprise " +
                "data file adds the location, range and connection properties. " +
                $"Contact us for a copy at {contactUs}");
        }

        /// <summary>
        /// The names as a list in words, for example "A, B and C".
        /// </summary>
        private static string JoinNames(IReadOnlyList<string> names)
        {
            return names.Count > 1 ?
                string.Join(", ", names.Take(names.Count - 1)) + " and " +
                    names[names.Count - 1] :
                string.Join("", names);
        }
    }
}
