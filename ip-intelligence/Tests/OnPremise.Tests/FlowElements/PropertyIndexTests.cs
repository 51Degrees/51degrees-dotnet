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

using FiftyOne.Common.TestHelpers;
using FiftyOne.IpIntelligence.Engine.OnPremise.Data;
using FiftyOne.IpIntelligence.Engine.OnPremise.FlowElements;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FiftyOne.IpIntelligence.OnPremise.Tests.FlowElements
{
    /// <summary>
    /// Covers reading a property by its required property index, which is the
    /// place of its name in the list the engine was built with.
    /// </summary>
    [TestClass]
    [TestCategory("IpIntelligence")]
    [TestCategory("OnPremise")]
    public class PropertyIndexTests
    {
        private static readonly TestLoggerFactory _logger =
            new TestLoggerFactory();

        private const string TestIp = "8.8.8.8";

        private const string AsnDataFileName = "51Degrees-IPIV4AsnIpiV41.ipi";

        /// <summary>
        /// Names in an order that is neither data file's own, as a data file
        /// lists its properties by name.
        /// </summary>
        private static readonly string[] BuildNames =
        {
            "RegisteredName",
            "AsnName",
            "RegisteredCountry",
            "Asn",
        };

        [TestMethod]
        public void PropertyIndex_FollowsBuildOrder()
        {
            using (var context = NewContext(BuildNames))
            {
                var indexes = context.Engine.PropertyIndexes;
                Assert.IsNotNull(
                    indexes,
                    "An engine built with names has its table once it is " +
                    "built, before anything is processed.");
                for (var i = 0; i < BuildNames.Length; i++)
                {
                    Assert.IsTrue(
                        indexes.TryGetValue(BuildNames[i], out int index),
                        $"'{BuildNames[i]}' was given at build.");
                    Assert.AreEqual(
                        i,
                        index,
                        $"'{BuildNames[i]}' was name {i} at build.");
                }
                Assert.IsTrue(
                    indexes.TryGetValue("registeredcountry", out int lower),
                    "Names are matched without regard to case.");
                Assert.AreEqual(2, lower);
                Assert.IsFalse(
                    indexes.ContainsKey("NotAPropertyAnyDataFileHas"),
                    "Only names given at build have an index.");
            }
        }

        [TestMethod]
        public void PropertyIndex_NameGivenTwiceTakesFirstIndex()
        {
            var names = new[] { "RegisteredName", "Asn", "registeredname" };
            using (var context = NewContext(names))
            {
                Assert.AreEqual(
                    0,
                    context.Engine.PropertyIndexes["RegisteredName"],
                    "A name given twice takes its first index, as C does.");
                Assert.AreEqual(1, context.Engine.PropertyIndexes["Asn"]);
            }
        }

        [TestMethod]
        public void PropertyIndex_ReadsGoByIndex()
        {
            using (var context = NewContext(BuildNames))
            {
                AssertReadsGoByIndex(context);
            }
        }

        /// <summary>
        /// An engine built with no names takes the names of its first data
        /// file as its list, so it has a table once it is built.
        /// </summary>
        [TestMethod]
        public void PropertyIndex_NoNamesTakesFirstFileNames()
        {
            using (var context = NewContext(null))
            {
                var indexes = context.Engine.PropertyIndexes;
                Assert.IsNotNull(
                    indexes,
                    "An engine built with no names has its table once it " +
                    "is built, before anything is processed.");
                foreach (var property in context.Engine.Properties
                    .Where(p => p.Available))
                {
                    Assert.IsTrue(
                        indexes.ContainsKey(property.Name),
                        $"'{property.Name}' is in the first data file.");
                }
                CollectionAssert.AreEquivalent(
                    Enumerable.Range(0, indexes.Count).ToList(),
                    indexes.Values.ToList(),
                    "Each name has its own index, counting from 0.");
                var name = context.Read(d => d.RegisteredName);
                Assert.IsTrue(name.HasValue, name.NoValueMessage);
            }
        }

        [TestMethod]
        public void PropertyIndex_NoNamesReadsGoByIndex()
        {
            using (var context = NewContext(null))
            {
                AssertReadsGoByIndex(context);
            }
        }

        [TestMethod]
        public void PropertyIndex_NameAndTypedAgree()
        {
            using (var context = NewContext(BuildNames))
            {
                Assert.AreEqual(
                    context.Read(d => d.RegisteredName).Value,
                    context.Read(ByName("RegisteredName")).Value);
                Assert.AreEqual(
                    context.Read(d => d.RegisteredCountry).Value,
                    context.Read(ByName("RegisteredCountry")).Value);
            }
        }

        /// <summary>
        /// After a refresh to a data file with other properties, each index
        /// still names the property it named at build, so the values match
        /// those an engine built on the new file reads by name.
        /// </summary>
        [TestMethod]
        public void PropertyIndex_HoldsAcrossRefresh()
        {
            var asn = TestHelpers.Utils.GetFilePath(AsnDataFileName);
            using (var expected = NewContext(null, asn.FullName))
            using (var context = NewContext(BuildNames))
            {
                RefreshKeepsTable(context, asn);
                AssertReadAsNewFile(expected, context, BuildNames);
            }
        }

        /// <summary>
        /// An engine built with no names keeps the first data file's names
        /// across a refresh, so each index names the same property, and a
        /// property only the new file has gets no index and no value.
        /// </summary>
        [TestMethod]
        public void PropertyIndex_NoNamesHoldsAcrossRefresh()
        {
            var asn = TestHelpers.Utils.GetFilePath(AsnDataFileName);
            using (var expected = NewContext(null, asn.FullName))
            using (var context = NewContext(null))
            {
                var table = RefreshKeepsTable(context, asn);
                AssertReadAsNewFile(expected, context, table.Keys);
                foreach (var name in expected.Engine.Properties
                    .Select(p => p.Name)
                    .Where(n => table.ContainsKey(n) == false))
                {
                    Assert.IsFalse(
                        context.Values(name).HasValue,
                        $"'{name}' is only in the new file, so it has no " +
                        "index and no value.");
                }
            }
        }

        private static IReadOnlyDictionary<string, int> RefreshKeepsTable(
            Context context,
            FileInfo dataFile)
        {
            var table = context.Engine.PropertyIndexes;
            Assert.IsNotNull(table, "The engine has its table once built.");
            using (var stream = File.OpenRead(dataFile.FullName))
            {
                context.Engine.RefreshData(
                    context.Engine.DataFiles[0].Identifier, stream);
            }
            Assert.AreSame(
                table,
                context.Engine.PropertyIndexes,
                "A refresh keeps the table made at build.");
            return table;
        }

        /// <summary>
        /// Checks that each name reads after a refresh what an engine built
        /// on the new file reads by name, or no value if that file lacks it.
        /// </summary>
        private static void AssertReadAsNewFile(
            Context expected,
            Context context,
            IEnumerable<string> names)
        {
            var present = expected.Engine.Properties
                .Select(p => p.Name)
                .ToList();
            var compared = 0;
            foreach (var name in names)
            {
                var after = context.Values(name);
                if (present.Contains(name))
                {
                    var before = expected.Values(name);
                    Assert.IsTrue(before.HasValue, before.NoValueMessage);
                    Assert.IsTrue(after.HasValue, after.NoValueMessage);
                    CollectionAssert.AreEqual(
                        before.Value.ToList(),
                        after.Value.ToList(),
                        $"'{name}' reads the same property after the " +
                        "refresh.");
                    compared++;
                }
                else
                {
                    Assert.IsFalse(
                        after.HasValue,
                        $"'{name}' is not in the new file, so it has no " +
                        "value.");
                }
            }
            Assert.IsTrue(
                compared > 0,
                "The new file holds at least one of the names.");
        }

        /// <summary>
        /// Swapping two indexes in the table makes a read of one name give the
        /// other's value, which happens only if reads go by the index.
        /// </summary>
        private static void AssertReadsGoByIndex(Context context)
        {
            var name = context.Read(d => d.RegisteredName);
            var country = context.Read(d => d.RegisteredCountry);
            Assert.IsTrue(name.HasValue, name.NoValueMessage);
            Assert.IsTrue(country.HasValue, country.NoValueMessage);
            Assert.AreNotEqual(name.Value, country.Value);

            var table = context.Engine.PropertyIndexes;
            Assert.IsNotNull(table, "The engine has its table once built.");
            var swapped = table.ToDictionary(
                x => x.Key,
                x => x.Value,
                StringComparer.OrdinalIgnoreCase);
            swapped["RegisteredName"] = table["RegisteredCountry"];
            swapped["RegisteredCountry"] = table["RegisteredName"];
            context.Engine.PropertyIndexes = swapped;
            try
            {
                Assert.AreEqual(
                    country.Value,
                    context.Read(d => d.RegisteredName).Value,
                    "A typed read goes by the index in the table.");
                Assert.AreEqual(
                    name.Value,
                    context.Read(ByName("RegisteredCountry")).Value,
                    "A read by name goes by the index in the table.");
            }
            finally
            {
                context.Engine.PropertyIndexes = table;
            }
        }

        private static Func<IIpIntelligenceData, IAspectPropertyValue<string>>
            ByName(string name)
        {
            return d => (IAspectPropertyValue<string>)d[name];
        }

        private static Context NewContext(
            string[] properties,
            string dataFile = null)
        {
            var builder = new IpiOnPremiseEngineBuilder(_logger, null)
                .SetPerformanceProfile(PerformanceProfiles.LowMemory)
                .SetAutoUpdate(false)
                .SetDataFileSystemWatcher(false);
            if (properties != null)
            {
                builder.SetProperties(new List<string>(properties));
            }
            var engine = builder.Build(
                dataFile ?? TestHelpers.Utils.GetFilePath(
                    TestHelpers.Constants.IPI_DATA_FILE_NAME).FullName,
                false);
            return new Context(engine);
        }

        private sealed class Context : IDisposable
        {
            private readonly IPipeline _pipeline;

            public IpiOnPremiseEngine Engine { get; }

            public Context(IpiOnPremiseEngine engine)
            {
                Engine = engine;
                _pipeline = new PipelineBuilder(_logger)
                    .AddFlowElement(engine)
                    .Build();
            }

            public IAspectPropertyValue<string> Read(
                Func<IIpIntelligenceData, IAspectPropertyValue<string>> read)
            {
                using (var flowData = _pipeline.CreateFlowData())
                {
                    flowData.AddEvidence("query.client-ip", TestIp);
                    flowData.Process();
                    return read(flowData.Get<IIpIntelligenceData>());
                }
            }

            public IAspectPropertyValue<IReadOnlyList<string>> Values(
                string name)
            {
                using (var flowData = _pipeline.CreateFlowData())
                {
                    flowData.AddEvidence("query.client-ip", TestIp);
                    flowData.Process();
                    var data = (IIpDataOnPremise)flowData
                        .Get<IIpIntelligenceData>();
                    return data.GetValues(name);
                }
            }

            public void Dispose()
            {
                _pipeline.Dispose();
                Engine.Dispose();
            }
        }
    }
}
