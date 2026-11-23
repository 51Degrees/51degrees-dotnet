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
using FiftyOne.IpIntelligence.Engine.OnPremise.Interop;
using FiftyOne.IpIntelligence.Engine.OnPremise.Wrappers;
using FiftyOne.Pipeline.Engines;
using FiftyOne.Pipeline.Engines.Caching;
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.FiftyOne.Data;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FiftyOne.IpIntelligence.OnPremise.Tests.Core.FlowElements
{
    [TestClass]
    public class EngineAndBuilderTests
    {
        IpiOnPremiseEngineBuilder _builder;
        SwigFactory _factory;

        /// <summary>
        /// Setup the builder to create a mock swig engine that will mostly 
        /// return empty values.
        /// </summary>
        [TestInitialize]
        public void Init()
        {
            _builder = new IpiOnPremiseEngineBuilder(new LoggerFactory());

            // Setup values returned by IMetaDataSwigWrapper
            var properties = new Mock<IPropertyCollectionSwigWrapper>();
            properties.Setup(c => c.GetEnumerator()).Returns(new List<IFiftyOneAspectPropertyMetaData>().GetEnumerator());
            var components = new Mock<IComponentCollectionSwigWrapper>();
            components.Setup(c => c.GetEnumerator()).Returns(new List<IComponentMetaData>().GetEnumerator());

            // Setup values returned by IEngineSwigWrapper
            var swigMetaData = new Mock<IMetaDataSwigWrapper>();
            swigMetaData.Setup(d => d.getProperties(It.IsAny<IpiOnPremiseEngine>())).Returns(properties.Object);
            swigMetaData.Setup(d => d.getComponents(It.IsAny<IpiOnPremiseEngine>())).Returns(components.Object);
            var date = new Mock<IDateSwigWrapper>();
            date.Setup(d => d.getYear()).Returns(2019);
            date.Setup(d => d.getMonth()).Returns(12);
            date.Setup(d => d.getDay()).Returns(10);

            // Setup swig engine wrapper
            var swigEngine = new Mock<IEngineSwigWrapper>();
            // The engine frees the keys it is given, so each call gets new
            // ones.
            swigEngine.Setup(e => e.getKeys()).Returns(() => new VectorStringSwig());
            swigEngine.Setup(e => e.getRequiredProperties())
                .Returns(() => new VectorStringSwig());
            swigEngine.Setup(e => e.getMetaData()).Returns(swigMetaData.Object);
            swigEngine.Setup(e => e.getPublishedTime()).Returns(date.Object);
            swigEngine.Setup(e => e.getUpdateAvailableTime()).Returns(date.Object);

            // Setup factory to create our mock engine rather than a real one
            var factory = new SwigFactory();
            factory.EngineFromData = (a, b, c, d) => { return swigEngine.Object; };

            // Assign the factory to the builder
            _factory = factory;
            _builder.SwigFactory = factory;
            _builder.SetAutoUpdate(false)
                .SetDataFileSystemWatcher(false);
        }

        /// <summary>
        /// Create a new engine and check that the data file associated 
        /// with the engine has had its 'Engine' property set correctly.
        /// </summary>
        [TestMethod]
        public void EngineBuilder_CheckDataFileEngineSet()
        {
            using var engine = _builder.Build(new MemoryStream());
            Assert.AreEqual(engine, engine.DataFiles.ElementAt(0).Engine);
        }

        /// <summary>
        /// An engine built without a performance profile uses Balanced, as
        /// the specification says, rather than the native configuration's
        /// own default, which loads everything into memory.
        /// </summary>
        [TestMethod]
        public void EngineBuilder_NoProfile_UsesBalanced()
        {
            using var balanced = new ConfigIpiSwig();
            balanced.setBalanced();

            Assert.AreEqual(Describe(balanced), BuildAndDescribeConfig());
        }

        /// <summary>
        /// A performance profile the caller sets still decides, so
        /// MaxPerformance loads everything into memory.
        /// </summary>
        [TestMethod]
        public void EngineBuilder_MaxPerformance_UsesInMemory()
        {
            using var balanced = new ConfigIpiSwig();
            balanced.setBalanced();
            using var inMemory = new ConfigIpiSwig();
            inMemory.setMaxPerformance();
            Assert.AreNotEqual(Describe(balanced), Describe(inMemory),
                "Balanced and MaxPerformance must differ for this test to " +
                "mean anything.");

            _builder.SetPerformanceProfile(PerformanceProfiles.MaxPerformance);

            Assert.AreEqual(Describe(inMemory), BuildAndDescribeConfig());
        }

        /// <summary>
        /// Build an engine and describe the native configuration the
        /// builder gave it.
        /// </summary>
        private string BuildAndDescribeConfig()
        {
            string described = null;
            var create = _factory.EngineFromData;
            _factory.EngineFromData = (data, size, config, properties) =>
            {
                described = Describe(config.Object);
                return create(data, size, config, properties);
            };
            using (_builder.Build(new MemoryStream()))
            {
            }
            Assert.IsNotNull(described, "The engine was not created.");
            return described;
        }

        /// <summary>
        /// The number of entries loaded and the cache capacity of each
        /// collection, which are what a performance profile sets.
        /// </summary>
        private static string Describe(ConfigIpiSwig config)
        {
            var collections = new[]
            {
                ("strings", config.getStrings()),
                ("components", config.getComponents()),
                ("maps", config.getMaps()),
                ("properties", config.getProperties()),
                ("values", config.getValues()),
                ("profiles", config.getProfiles()),
                ("graphs", config.getGraphs()),
                ("profileGroups", config.getProfileGroups()),
                ("profileOffsets", config.getProfileOffsets()),
                ("propertyTypes", config.getPropertyTypes()),
                ("graph", config.getGraph()),
            };
            return string.Join("; ", collections.Select(c =>
                $"{c.Item1} loaded {c.Item2.getLoaded()} " +
                $"capacity {c.Item2.getCapacity()}"));
        }

        /// <summary>
        /// The builder refuses a results cache size.
        /// </summary>
        [TestMethod]
        public void EngineBuilder_CacheSizeDisabled()
        {
            Assert.Throws<NotSupportedException>(() =>
            {
                _ = _builder.SetCacheSize(100);
            });
        }

        /// <summary>
        /// The builder refuses a results cache.
        /// </summary>
        [TestMethod]
        public void EngineBuilder_CacheDisabled()
        {
            Assert.Throws<NotSupportedException>(() =>
            {
                _ = _builder.SetCache(new CacheConfiguration());
            });
        }

        /// <summary>
        /// The engine refuses a results cache set on it directly.
        /// </summary>
        [TestMethod]
        public void Engine_CacheDisabled()
        {
            using var engine = _builder.Build(new MemoryStream());
            Assert.Throws<NotSupportedException>(() =>
            {
                engine.SetCache(new DefaultFlowCache(new CacheConfiguration()));
            });
        }
    }
}
