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
using FiftyOne.DeviceDetection.PropertyKeyed.Data;
using FiftyOne.DeviceDetection.PropertyKeyed.FlowElements;
using FiftyOne.DeviceDetection.TestHelpers;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FiftyOne.DeviceDetection.PropertyKeyed.Tests
{
    /// <summary>
    /// A property keyed engine on a Hash engine whose builder names no
    /// property to index, which is the case the specification's rules on
    /// the native lookup are about. It runs on the free Lite data file,
    /// keyed on PlatformName, because the rules hold for any property.
    /// <list type="bullet">
    /// <item>device-detection-on-premise.md: "A language MUST use this
    /// native function for such a lookup rather than keep an index of its
    /// own, because an index of its own can match differently (the .NET
    /// fallback index matches without regard to case), so that every
    /// language gives the same answers."</item>
    /// <item>hardware-profile-lookup-on-premise.md: "A lookup MUST return
    /// every profile holding the value, each once, in the order of the
    /// profiles in the data file, which is ascending profile id."</item>
    /// <item>hardware-profile-lookup-on-premise.md: "An Engine of this kind
    /// MUST find the profiles through the native lookup, as the profiles
    /// for a property value require, rather than build an index of its own
    /// by processing every hardware profile at start-up."</item>
    /// </list>
    /// </summary>
    [TestClass]
    public class NativeLookupTests
    {
        private const string PROPERTY = "PlatformName";
        private const string ELEMENT_DATA_KEY = "platform-profiles";

        private static ILoggerFactory _loggerFactory;
        private static IPipeline _pipeline;
        private static DeviceDetectionHashEngine _hash;
        private static PropertyKeyedDeviceEngine _engine;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            var dataFile = Utils.GetFilePath(
                Constants.LITE_HASH_DATA_FILE_NAME).FullName;
            _loggerFactory = LoggerFactory.Create(b => b
                .SetMinimumLevel(LogLevel.Warning));
            // No SetIndexedProperties, so the Hash engine's data set holds
            // no index of its own and the native lookup reads every profile.
            _hash = new DeviceDetectionHashEngineBuilder(_loggerFactory)
                .SetAutoUpdate(false)
                .SetDataFileSystemWatcher(false)
                .Build(dataFile, false);
            _engine = new PropertyKeyedDeviceEngine(
                _loggerFactory,
                new List<string> { PROPERTY },
                PROPERTY,
                ELEMENT_DATA_KEY);
            _pipeline = new PipelineBuilder(_loggerFactory)
                .AddFlowElement(_hash)
                .AddFlowElement(_engine)
                .SetAutoDisposeElements(true)
                .Build();
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            _pipeline?.Dispose();
            _loggerFactory?.Dispose();
        }

        /// <summary>
        /// The number of values the tests look up. Without the Hash
        /// engine's index each look up reads every profile, so the tests
        /// take a sample rather than every value.
        /// </summary>
        private const int SampleSize = 8;

        /// <summary>
        /// Every spelling of the property's values that the data file
        /// holds.
        /// </summary>
        private static List<string> AllValues()
        {
            var values = _hash.Properties
                .Single(p => p.Name == PROPERTY)
                .Values
                .Select(v => v.Name)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(v => v, StringComparer.Ordinal)
                .ToList();
            Assert.AreNotEqual(0, values.Count);
            return values;
        }

        /// <summary>
        /// A sample of the values spread evenly over all of them.
        /// </summary>
        private static List<string> Values()
        {
            var all = AllValues();
            var step = Math.Max(1, all.Count / SampleSize);
            return all.Where((v, i) => i % step == 0)
                .Take(SampleSize)
                .ToList();
        }

        private static List<uint> EngineProfileIds(string value)
        {
            using (var data = _pipeline.CreateFlowData())
            {
                data.AddEvidence("query." + PROPERTY, value);
                data.Process();
                Assert.IsNull(data.Errors,
                    data.Errors == null ? "" : string.Join("; ",
                        data.Errors.Select(e => e.ExceptionData.Message)));
                var result = data.GetFromElement(_engine);
                return result.Profiles
                    .Select(p => uint.Parse(
                        ((IAspectPropertyValue)p["ProfileId"]).Value
                            .ToString(),
                        CultureInfo.InvariantCulture))
                    .ToList();
            }
        }

        /// <summary>
        /// The engine builds no index of its own by processing every
        /// profile at start-up.
        /// </summary>
        [TestMethod]
        public void NoIndexOfItsOwn()
        {
            var index = _engine.DataSet.PropertyIndexes
                .Single(i => i.MetaData.Name == PROPERTY);
            Assert.IsEmpty(index.ValueData,
                $"The engine indexed {index.ValueData.Count} values of " +
                $"{PROPERTY} itself by processing every profile.");
        }

        /// <summary>
        /// For each value in the sample, the engine gives what the native
        /// function gives, which is every profile holding that exact
        /// spelling, each once, in ascending profile id.
        /// </summary>
        [TestMethod]
        public void Values_TheNativeFunctionsProfiles()
        {
            var differences = new List<string>();
            foreach (var value in Values())
            {
                var native = _hash.GetProfileIdsForPropertyValue(
                    PROPERTY,
                    value);
                var actual = EngineProfileIds(value);
                if (actual.SequenceEqual(native) == false)
                {
                    differences.Add(
                        $"'{value}' native [{string.Join(",", native)}] " +
                        $"engine [{string.Join(",", actual)}]");
                }
                Assert.HasCount(
                    actual.Count,
                    actual.Distinct().ToList(),
                    $"'{value}' gave a profile more than once.");
                CollectionAssert.AreEqual(
                    actual.OrderBy(i => i).ToList(),
                    actual,
                    $"'{value}' gave profiles out of ascending order.");
            }
            Assert.IsEmpty(differences,
                string.Join("; ", differences.Take(5)));
        }

        /// <summary>
        /// A value given in another case than the data file holds matches
        /// nothing.
        /// </summary>
        [TestMethod]
        public void ValueInAnotherCase_FindsNothing()
        {
            var values = Values();
            var held = new HashSet<string>(AllValues(), StringComparer.Ordinal);
            var others = values
                .SelectMany(v => new[]
                {
                    v.ToUpperInvariant(),
                    v.ToLowerInvariant()
                })
                .Where(v => held.Contains(v) == false)
                .Distinct(StringComparer.Ordinal)
                .Take(SampleSize)
                .ToList();
            Assert.AreNotEqual(0, others.Count);

            var found = others
                .Where(v => EngineProfileIds(v).Count > 0)
                .ToList();
            Assert.IsEmpty(found,
                $"{found.Count} of {others.Count} values in another case " +
                "found profiles, for example " +
                string.Join("; ", found.Take(5)));
        }
    }
}
