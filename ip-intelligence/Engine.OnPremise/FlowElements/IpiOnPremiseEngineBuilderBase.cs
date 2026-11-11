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

using FiftyOne.IpIntelligence.Engine.OnPremise.Data;
using FiftyOne.IpIntelligence.Engine.OnPremise.Interop;
using FiftyOne.IpIntelligence.Shared.FlowElements;
using FiftyOne.Pipeline.Core.Attributes;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines;
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.FiftyOne.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using FiftyOne.IpIntelligence.Engine.OnPremise.Wrappers;

namespace FiftyOne.IpIntelligence.Engine.OnPremise.FlowElements
{
    /// <summary>
    /// Builder for the <see cref="IpiOnPremiseEngine"/>. All options
    /// for the engine should be set here.
    /// </summary>
    public abstract class IpiOnPremiseEngineBuilderBase<TEngine>
       : OnPremiseIpiEngineBuilderBase<IpiOnPremiseEngineBuilderBase<TEngine>, TEngine>
        where TEngine : IpiOnPremiseEngine
    {
        #region Private Properties

        private readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// The settings for the native configuration. Each engine gets a new
        /// configuration with these applied, which it owns and frees when it
        /// is disposed, so a builder can build more than one engine. The
        /// first is the Balanced performance profile, the default the
        /// specification gives, so that a profile the caller sets is applied
        /// after it and replaces it.
        /// </summary>
        private readonly List<Action<IConfigSwigWrapper>> _configSettings =
            new List<Action<IConfigSwigWrapper>>()
            {
                c => c.setBalanced()
            };

        #endregion

        internal ISwigFactory SwigFactory { get; set; } = new SwigFactory();

        #region Constructor

        /// <summary>
        /// Construct a new instance of the builder.
        /// </summary>
        /// <param name="loggerFactory">
        /// Factory used to create loggers for the engine
        /// </param>
        public IpiOnPremiseEngineBuilderBase(
            ILoggerFactory loggerFactory)
            : this(loggerFactory, null)
        {
        }

        /// <summary>
        /// Construct a new instance of the builder.
        /// </summary>
        /// <param name="loggerFactory">
        /// Factory used to create loggers for the engine
        /// </param>
        /// <param name="dataUpdateService">
        /// Data update service used to keep the engine's data up to date.
        /// </param>
        public IpiOnPremiseEngineBuilderBase(
            ILoggerFactory loggerFactory,
            IDataUpdateService dataUpdateService)
            : base(dataUpdateService)
        {
            _loggerFactory = loggerFactory;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Set whether or not an existing temp file should be used if one is
        /// found in the temp directory.
        /// </summary>
        /// <param name="reuse">True if an existing file should be used</param>
        /// <returns>This builder</returns>
        public IpiOnPremiseEngineBuilderBase<TEngine> SetReuseTempFile(bool reuse)
        {
            _configSettings.Add(c => c.setReuseTempFile(reuse));
            return this;
        }

        /// <summary>
        /// Set the performance profile to use when constructing the data set.
        /// </summary>
        /// <param name="profileName">Name of the profile to use</param>
        /// <returns>This builder</returns>
        [DefaultValue("Balanced")]
        public IpiOnPremiseEngineBuilderBase<TEngine> SetPerformanceProfile(
            string profileName)
        {
            PerformanceProfiles profile;
            if (Enum.TryParse<PerformanceProfiles>(
                profileName,
                out profile))
            {
                return SetPerformanceProfile(profile);
            }
            else
            {
                var available = Enum.GetNames(typeof(PerformanceProfiles))
                    .Select(i => "'" + i + "'");
                throw new ArgumentException(
                    $"'{profileName}' is not a valid performance profile. " +
                    $"Available profiles are {string.Join(", ", available)}.");
            }
        }

        /// <summary>
        /// Set the performance profile to use when constructing the data set.
        /// </summary>
        /// <param name="profile">Profile to use</param>
        /// <returns>This builder</returns>
        public override IpiOnPremiseEngineBuilderBase<TEngine> SetPerformanceProfile(
            PerformanceProfiles profile)
        {
            switch (profile)
            {
                case PerformanceProfiles.LowMemory:
                    _configSettings.Add(c => c.setLowMemory());
                    break;
                case PerformanceProfiles.Balanced:
                    _configSettings.Add(c => c.setBalanced());
                    break;
                case PerformanceProfiles.BalancedTemp:
                    _configSettings.Add(c => c.setBalancedTemp());
                    break;
                case PerformanceProfiles.HighPerformance:
                    _configSettings.Add(c => c.setHighPerformance());
                    break;
                case PerformanceProfiles.MaxPerformance:
                    _configSettings.Add(c => c.setMaxPerformance());
                    break;
                default:
                    throw new ArgumentException(
                        $"The performance profile '{profile}' is not valid " +
                        $"for a IpiOnPremiseEngine.",
                        nameof(profile));
            }
            return this;
        }

        /// <summary>
        /// Set the expected number of operations that use the engine at the same
        /// time. The native code uses it for every collection read from the data
        /// file.
        /// First, it sets the number of file handles in the pool, which is the
        /// concurrency up to a fixed maximum of 64, because a process can only have
        /// so many files open. An operation that has to read from the file when
        /// every handle is in use waits until another operation releases one.
        /// Second, it decides how each cache is split into parts, each with its own
        /// lock, which avoids excessive locking. An item in use cannot be removed
        /// from a cache, and any operation can need any part, so every part holds
        /// at least the concurrency in items. The number of parts is the
        /// concurrency, or fewer where needed so that every part holds that many
        /// items, and a cache that holds fewer items than the concurrency in total
        /// is made large enough to hold that many. No value stops the engine
        /// building.
        /// The concurrency has no effect when every collection is held in memory,
        /// for example with the MaxPerformance profile.
        /// </summary>
        /// <param name="concurrency">
        /// The expected number of operations that use the engine at
        /// the same time.
        /// </param>
        /// <returns>This builder</returns>
        public override IpiOnPremiseEngineBuilderBase<TEngine> SetConcurrency(
            ushort concurrency)
        {
            _configSettings.Add(c => c.setConcurrency(concurrency));
            return this;
        }

        #endregion

        #region Protected Overrides
        /// <summary>
        /// Called by the 'BuildEngine' method to handle
        /// creation of the engine instance.
        /// </summary>
        /// <param name="properties"></param>
        /// <returns>
        /// An <see cref="IAspectEngine"/>.
        /// </returns>
        protected override TEngine NewEngine(
            List<string> properties)
        {
            if (DataFiles.Count != 1)
            {
                throw new PipelineConfigurationException(
                    "This builder requires one and only one configured file " +
                    $"but it has {DataFileConfigs.Count}");
            }
            var dataFile = DataFiles.First();
            // We remove the data file configuration from the list.
            // This is because the on-premise engine builder base class 
            // adds all the data file configs after engine creation.  
            // However, the IP intelligence data files are supplied 
            // directly to the constructor.
            // Consequently, we remove it here to stop it from being added 
            // again by the base class.
            DataFiles.Remove(dataFile);

            // Create the swig configuration object, which the engine owns.
            var config = SwigFactory.CreateConfig();
            IRequiredPropertiesConfigSwigWrapper propertyConfig = null;
            TEngine engine = null;
            try
            {
                foreach (var setting in _configSettings)
                {
                    setting(config);
                }
                if (dataFile.Configuration.CreateTempCopy && String.IsNullOrEmpty(TempDir) == false)
                {
                    using (var tempDirs = new VectorStringSwig())
                    {
                        tempDirs.Add(TempDir);
                        config.setTempDirectories(tempDirs);
                    }
                    config.setUseTempFile(true);
                }

                // Create swig property configuration object.
                using (var vProperties = new VectorStringSwig(properties))
                {
                    propertyConfig = SwigFactory.CreateRequiredProperties(vProperties);
                }

                // Create a new instance of the required engine type.
                engine = CreateEngine(
                    _loggerFactory,
                    CreateAspectData,
                    TempDir);

                // Then set the instances of properties that remain internal to
                // the package.
                engine.SwigFactory = SwigFactory;
                engine.PropertiesConfigSwig = propertyConfig;
                engine.Config = config;
                // A property's index is the place of its name in this list,
                // so only an engine built with names reads by index. The
                // native code gives an engine built with no names no
                // indexes, so that engine reads by name.
                engine.BuiltWithPropertyNames =
                    properties != null && properties.Count > 0;

                // This must be last as the call results in RefreshData being
                // triggered which requires the prior three properties to be set.
                engine.AddDataFile(dataFile);
                return engine;
            }
            catch
            {
                // No caller receives an engine that failed to load, so free
                // it, or the native configuration if there is no engine yet.
                if (engine != null)
                {
                    engine.Dispose();
                }
                else
                {
                    propertyConfig?.Dispose();
                    config.Dispose();
                }
                throw;
            }
        }

        /// <summary>
        /// Not supported, because this engine cannot have a results cache.
        /// </summary>
        /// <param name="cacheConfig">Not used.</param>
        /// <exception cref="NotSupportedException">Always thrown.</exception>
        [CodeConfigOnly]
        public override IpiOnPremiseEngineBuilderBase<TEngine> SetCache(
            CacheConfiguration cacheConfig)
        {
            throw new NotSupportedException(Messages.ExceptionSetCache);
        }

        /// <summary>
        /// Not supported, because this engine cannot have a results cache.
        /// </summary>
        /// <param name="cacheSize">Not used.</param>
        /// <exception cref="NotSupportedException">Always thrown.</exception>
        [DefaultValue("Not supported")]
        public override IpiOnPremiseEngineBuilderBase<TEngine> SetCacheSize(
            int cacheSize)
        {
            throw new NotSupportedException(Messages.ExceptionSetCache);
        }

        /// <summary>
        /// Get the default value for the 'Type' parameter that is passed
        /// to the 51Degrees Distributor service when checking for updated
        /// data files.
        /// </summary>
        protected override string DefaultDataDownloadType => "IPIV41";
        #endregion

        #region Abstract Methods
        /// <summary>
        /// Overridden in the implementing class to create a new instance of
        /// TEngine with the constructor parameters provided.
        /// </summary>
        /// <param name="loggerFactory"></param>
        /// <param name="deviceDataFactory"></param>
        /// <param name="tempDataFilePath"></param>
        /// <returns></returns>
        protected abstract TEngine CreateEngine(
            ILoggerFactory loggerFactory,
            Func<IPipeline, FlowElementBase<
                IIpDataOnPremise,
                IFiftyOneAspectPropertyMetaData>,
                IIpDataOnPremise> deviceDataFactory,
            string tempDataFilePath);
        #endregion


        private IIpDataOnPremise CreateAspectData(IPipeline pipeline,
            FlowElementBase<IIpDataOnPremise, IFiftyOneAspectPropertyMetaData> engine)
        {
            // On-premise engines use the base MissingPropertyService by design;
            // MissingPropertyServiceCloud is only for cloud engines.
            // Instance is marked obsolete to make the split between
            // MissingPropertyService and MissingPropertyServiceCloud
            // a conscious, compiler-caught decision.
#pragma warning disable CS0618
            return new IpDataOnPremise(
                _loggerFactory.CreateLogger<IpDataOnPremise>(),
                pipeline,
                engine as IpiOnPremiseEngine,
                MissingPropertyService.Instance);
#pragma warning restore CS0618
        }
    }
}
