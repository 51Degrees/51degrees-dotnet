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
using FiftyOne.DeviceDetection.PropertyKeyed.Services;
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
    /// Compares the TAC and NativeModel engines on one data file with two
    /// Hash engines, one whose data set indexes those properties itself and
    /// one that does not, so that the engines fall back to the index the
    /// data set service builds by processing every profile. The Hash
    /// engine's index matches a value's exact spelling, where the fallback
    /// matches ignoring case.
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
            var dataFile = Utils.GetPaidFilePath(
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
            Assert.IsEmpty(_managed.Hash.IndexedProperties);
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
            Assert.IsEmpty(ManagedIndex(_native, property),
                "The managed index was built although the Hash engine " +
                "indexes the property.");
            Assert.AreNotEqual(0, ManagedIndex(_managed, property).Count,
                "The managed index was not built for a Hash engine that " +
                "does not index the property.");
        }

        /// <summary>
        /// The profiles holding each spelling of each value, read by
        /// processing every profile as the data set service does, but keyed
        /// on the exact spelling. Built once for each property.
        /// </summary>
        private static Dictionary<string, List<uint>> ExactIndex(
            string property)
        {
            if (_exactIndexes.TryGetValue(property, out var index))
            {
                return index;
            }
            index = new Dictionary<string, List<uint>>(StringComparer.Ordinal);
            var query = new PropertyValueQueryService(
                new[] { property },
                _loggerFactory);
            using (var context = query.CreateContext(_managed.Hash))
            {
                foreach (var (profile, device) in query.Query(context))
                {
                    if (!(device[property] is IAspectPropertyValue value) ||
                        value.HasValue == false)
                    {
                        continue;
                    }
                    var spellings = value.Value is IEnumerable<string> list ?
                        list :
                        new[] { value.Value.ToString() };
                    foreach (var spelling in spellings.Distinct(
                        StringComparer.Ordinal))
                    {
                        if (index.TryGetValue(spelling, out var ids) == false)
                        {
                            ids = new List<uint>();
                            index.Add(spelling, ids);
                        }
                        ids.Add(profile.ProfileId);
                    }
                }
            }
            _exactIndexes[property] = index;
            return index;
        }

        private static readonly Dictionary<string, Dictionary<string, List<uint>>>
            _exactIndexes =
                new Dictionary<string, Dictionary<string, List<uint>>>();

        /// <summary>
        /// The values that another value spells the same ignoring case.
        /// </summary>
        private static HashSet<string> SharedSpellings(
            IEnumerable<string> values)
        {
            return new HashSet<string>(
                values
                    .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .SelectMany(g => g),
                StringComparer.Ordinal);
        }

        /// <summary>
        /// For every value of the property, the Hash engine's index gives
        /// the profiles holding that exact spelling, in the same order as
        /// processing every profile finds them. The test also writes how far
        /// that is from the index that matches ignoring case.
        /// </summary>
        [TestMethod]
        [DataRow(TAC)]
        [DataRow(NATIVE_MODEL)]
        public void EveryValue_NativeIndexGivesTheExactProfiles(
            string property)
        {
            var exact = ExactIndex(property);
            var values = new HashSet<string>(exact.Keys, StringComparer.Ordinal);
            foreach (var value in _managed.Hash.Properties
                .Single(p => p.Name == property).Values)
            {
                values.Add(value.Name);
            }

            var differences = new List<string>();
            var changed = 0;
            var ignoringCase = ManagedIndex(_managed, property);
            foreach (var value in values.OrderBy(v => v, StringComparer.Ordinal))
            {
                var expected = exact.TryGetValue(value, out var ids) ?
                    ids :
                    new List<uint>();
                var actual = _native.Hash.GetProfileIdsForPropertyValue(
                    property,
                    value);
                if (expected.SequenceEqual(actual) == false)
                {
                    differences.Add(
                        $"'{value}' exact [{string.Join(",", expected)}] " +
                        $"native [{string.Join(",", actual)}]");
                }
                var old = ignoringCase.TryGetValue(value, out var oldIds) ?
                    oldIds.Distinct() :
                    Enumerable.Empty<uint>();
                if (old.SequenceEqual(expected) == false)
                {
                    changed++;
                }
            }

            Console.WriteLine(
                $"{property}: {values.Count} values. {changed} give other " +
                "profiles than the index that ignores case, " +
                $"{SharedSpellings(exact.Keys).Count} are spelled the same " +
                "as another ignoring case, and that index repeats a profile " +
                $"for {ignoringCase.Values.Count(l => l.Distinct().Count() != l.Count)}.");
            Assert.AreNotEqual(0, values.Count);
            Assert.IsEmpty(differences,
                $"{differences.Count} of {values.Count} {property} values " +
                "differ, for example " +
                string.Join("; ", differences.Take(5)));
        }

        /// <summary>
        /// The engines give the profiles holding the exact spelling of the
        /// evidence, for values spelled the same as no other and for values
        /// spelled the same as another ignoring case.
        /// </summary>
        [TestMethod]
        [DataRow(TAC)]
        [DataRow(NATIVE_MODEL)]
        public void Engines_GiveTheExactProfiles(string property)
        {
            var exact = ExactIndex(property);
            var values = exact.Keys
                .OrderBy(v => v, StringComparer.Ordinal)
                .Take(SampleSize)
                .Concat(SharedSpellings(exact.Keys)
                    .OrderBy(v => v, StringComparer.Ordinal)
                    .Take(SampleSize))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var differences = new List<string>();
            foreach (var value in values)
            {
                var expected = exact[value]
                    .Select(id => id.ToString(CultureInfo.InvariantCulture))
                    .ToList();
                var actual = ProfileIds(_native, property, value);
                if (expected.SequenceEqual(actual) == false)
                {
                    differences.Add(
                        $"'{value}' exact [{string.Join(",", expected)}] " +
                        $"engine [{string.Join(",", actual)}]");
                }
            }
            Assert.IsEmpty(differences,
                $"{differences.Count} of {values.Count} {property} values " +
                "differ, for example " +
                string.Join("; ", differences.Take(5)));
        }

        /// <summary>
        /// With the Hash engine's index, a NativeModel in a case that no
        /// value in the data file has finds no profiles, because the index
        /// matches the exact spelling. Without it, the engine still matches
        /// ignoring case.
        /// </summary>
        [TestMethod]
        public void ValueInAnotherCase_FindsNothingWithTheNativeIndex()
        {
            var exact = ExactIndex(NATIVE_MODEL);
            var values = exact.Keys
                .Where(v => v.ToUpperInvariant() != v.ToLowerInvariant())
                .OrderBy(v => v, StringComparer.Ordinal)
                .Select(v => v == v.ToUpperInvariant() ?
                    v.ToLowerInvariant() :
                    v.ToUpperInvariant())
                .Where(v => exact.ContainsKey(v) == false)
                .Take(SampleSize)
                .ToList();
            Assert.AreNotEqual(0, values.Count);

            var found = values
                .Where(v => ProfileIds(_native, NATIVE_MODEL, v).Count > 0)
                .ToList();
            Assert.IsEmpty(found,
                $"{found.Count} of {values.Count} values in another case " +
                "found profiles, for example " +
                string.Join("; ", found.Take(5)));

            var notFoundWithoutIndex = values
                .Where(v => ProfileIds(_managed, NATIVE_MODEL, v).Count == 0)
                .ToList();
            Assert.IsEmpty(notFoundWithoutIndex,
                $"{notFoundWithoutIndex.Count} of {values.Count} values in " +
                "another case found nothing without the Hash engine's " +
                "index, for example " +
                string.Join("; ", notFoundWithoutIndex.Take(5)));
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
