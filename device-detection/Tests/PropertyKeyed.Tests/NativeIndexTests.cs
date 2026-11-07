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
using FiftyOne.DeviceDetection.PropertyKeyed.FlowElements;
using FiftyOne.DeviceDetection.TestHelpers;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.DeviceDetection.PropertyKeyed.Tests
{
    /// <summary>
    /// Compares the TAC and NativeModel engines on one data file with two
    /// Hash engines, one whose data set indexes those properties itself and
    /// one that does not, so that the engines fall back to the index the
    /// data set service builds by processing every profile.
    /// </summary>
    [TestClass]
    public class NativeIndexTests
    {
        private const string TAC = "TAC";
        private const string NATIVE_MODEL = "NativeModel";

        /// <summary>
        /// The number of values the tests that process evidence use, enough
        /// to cover values held by one profile and by several.
        /// </summary>
        private const int SampleSize = 200;

        private static ILoggerFactory _loggerFactory;
        private static Side _managed;
        private static Side _native;

        private class Side
        {
            public IPipeline Pipeline;
            public DeviceDetectionHashEngine Hash;
            public TacEngine Tac;
            public NativeEngine Native;

            public PropertyKeyedDeviceBaseEngine Engine(string property) =>
                property == TAC ?
                    (PropertyKeyedDeviceBaseEngine)Tac :
                    Native;
        }

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            var dataFile = Utils.GetFilePath(
                Constants.TAC_HASH_DATA_FILE_NAME).FullName;
            _loggerFactory = LoggerFactory.Create(b => b
                .SetMinimumLevel(LogLevel.Warning));
            _managed = Build(dataFile, false);
            _native = Build(dataFile, true);
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            _managed?.Pipeline.Dispose();
            _native?.Pipeline.Dispose();
        }

        private static Side Build(string dataFile, bool indexed)
        {
            var builder = new DeviceDetectionHashEngineBuilder(_loggerFactory);
            if (indexed)
            {
                builder.SetIndexedProperties(NATIVE_MODEL, TAC);
            }
            var side = new Side()
            {
                Hash = builder
                    .SetAutoUpdate(false)
                    .SetDataFileSystemWatcher(false)
                    .Build(dataFile, false),
                Tac = new TacEngineBuilder(_loggerFactory).Build(),
                Native = new NativeEngineBuilder(_loggerFactory).Build(),
            };
            side.Pipeline = new PipelineBuilder(_loggerFactory)
                .AddFlowElement(side.Hash)
                .AddFlowElement(side.Tac)
                .AddFlowElement(side.Native)
                .SetAutoDisposeElements(true)
                .Build();
            return side;
        }

        private static IReadOnlyDictionary<string, List<uint>> ManagedIndex(
            Side side,
            string property)
        {
            return side.Engine(property).DataSet.PropertyIndexes
                .Single(i => i.MetaData.Name == property)
                .ValueData;
        }

        /// <summary>
        /// The Hash engine indexes the properties named with
        /// SetIndexedProperties, and only those.
        /// </summary>
        [TestMethod]
        public void IndexedProperties_AreThoseNamed()
        {
            var indexed = string.Join(",", _native.Hash.IndexedProperties
                .OrderBy(p => p, StringComparer.Ordinal));
            Assert.AreEqual(NATIVE_MODEL + "," + TAC, indexed);
            Assert.AreEqual(0, _managed.Hash.IndexedProperties.Count);
        }

        /// <summary>
        /// A property the Hash engine's data set indexes is not indexed
        /// again by processing every profile, and one it does not index is.
        /// </summary>
        [TestMethod]
        [DataRow(TAC)]
        [DataRow(NATIVE_MODEL)]
        public void ManagedIndex_BuiltOnlyWithoutTheNativeIndex(
            string property)
        {
            Assert.AreEqual(0, ManagedIndex(_native, property).Count,
                "The managed index was built although the Hash engine " +
                "indexes the property.");
            Assert.AreNotEqual(0, ManagedIndex(_managed, property).Count,
                "The managed index was not built for a Hash engine that " +
                "does not index the property.");
        }

        /// <summary>
        /// For every value of the property, the Hash engine's index gives
        /// the same profiles in the same order as the index built by
        /// processing every profile.
        /// </summary>
        [TestMethod]
        [DataRow(TAC)]
        [DataRow(NATIVE_MODEL)]
        public void EveryValue_NativeIndexGivesTheManagedProfiles(
            string property)
        {
            var managed = ManagedIndex(_managed, property);
            var values = new HashSet<string>(managed.Keys, StringComparer.Ordinal);
            foreach (var value in _managed.Hash.Properties
                .Single(p => p.Name == property).Values)
            {
                values.Add(value.Name);
            }

            var differences = new List<string>();
            foreach (var value in values.OrderBy(v => v, StringComparer.Ordinal))
            {
                var expected = managed.TryGetValue(value, out var ids) ?
                    ids :
                    new List<uint>();
                var actual = _native.Hash.GetProfileIdsForPropertyValue(
                    property,
                    value);
                if (expected.SequenceEqual(actual) == false)
                {
                    differences.Add(
                        $"'{value}' managed [{string.Join(",", expected)}] " +
                        $"native [{string.Join(",", actual)}]");
                }
            }

            Console.WriteLine(
                $"{property}: compared {values.Count} values, " +
                $"{differences.Count} differ.");
            Assert.AreNotEqual(0, values.Count);
            Assert.AreEqual(0, differences.Count,
                $"{differences.Count} of {values.Count} {property} values " +
                "differ, for example " +
                string.Join("; ", differences.Take(5)));
        }

        /// <summary>
        /// The engines give the same profiles for the same evidence whether
        /// the Hash engine indexes the property or not.
        /// </summary>
        [TestMethod]
        [DataRow(TAC)]
        [DataRow(NATIVE_MODEL)]
        public void Engines_GiveTheSameProfiles(string property)
        {
            var values = ManagedIndex(_managed, property).Keys
                .OrderBy(v => v, StringComparer.Ordinal)
                .Take(SampleSize)
                .ToList();
            AssertSameProfiles(property, values);
        }

        /// <summary>
        /// The engines match a value ignoring case, as the index built by
        /// processing every profile does, so the same evidence in another
        /// case gives the same profiles whether the Hash engine indexes the
        /// property or not.
        /// </summary>
        [TestMethod]
        public void ValueInAnotherCase_GivesTheSameProfiles()
        {
            var values = ManagedIndex(_managed, NATIVE_MODEL).Keys
                .Where(v => v.ToUpperInvariant() != v.ToLowerInvariant())
                .OrderBy(v => v, StringComparer.Ordinal)
                .Take(SampleSize)
                .Select(v => v == v.ToUpperInvariant() ?
                    v.ToLowerInvariant() :
                    v.ToUpperInvariant())
                .ToList();
            Assert.AreNotEqual(0, values.Count);
            AssertSameProfiles(NATIVE_MODEL, values);
        }

        private static void AssertSameProfiles(
            string property,
            IReadOnlyList<string> values)
        {
            var differences = new List<string>();
            foreach (var value in values)
            {
                var expected = ProfileIds(_managed, property, value);
                var actual = ProfileIds(_native, property, value);
                if (expected.SequenceEqual(actual) == false)
                {
                    differences.Add(
                        $"'{value}' managed [{string.Join(",", expected)}] " +
                        $"native [{string.Join(",", actual)}]");
                }
            }
            Assert.AreEqual(0, differences.Count,
                $"{differences.Count} of {values.Count} {property} values " +
                "differ, for example " +
                string.Join("; ", differences.Take(5)));
        }

        private static List<string> ProfileIds(
            Side side,
            string property,
            string value)
        {
            using (var data = side.Pipeline.CreateFlowData())
            {
                data.AddEvidence(
                    "query." + property.ToLowerInvariant(),
                    value);
                data.Process();
                var result = data.GetFromElement(side.Engine(property));
                return result.Profiles
                    .Select(p => ((IAspectPropertyValue)p["ProfileId"])
                        .Value.ToString())
                    .ToList();
            }
        }
    }
}
