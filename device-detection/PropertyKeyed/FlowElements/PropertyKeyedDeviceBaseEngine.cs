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
using FiftyOne.DeviceDetection.PropertyKeyed.Services;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.FiftyOne.Data;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.DeviceDetection.PropertyKeyed.FlowElements
{
    /// <summary>
    /// A generic device detection engine that uses property-keyed 
    /// lookups against a <see cref="DeviceDetectionHashEngine"/>.
    /// Can be configured to validate input and use any property as the key.
    /// </summary>
    /// <remarks>
    /// Build the Hash engine in code and name the key property with
    /// <c>SetIndexedProperties</c> on its builder, for example
    /// <c>SetIndexedProperties("NativeModel", "TAC")</c>. Without the index
    /// each lookup reads every profile in the data file, and a warning is
    /// logged once when the Pipeline is built.
    /// </remarks>
    public abstract class PropertyKeyedDeviceBaseEngine :
        PropertyKeyedEngine<IMultiDeviceData, IDeviceData>
    {
        private readonly IDataSetService _dataSetService;
        private readonly ILogger<MultiDeviceData> _loggerMultiDd;
        private readonly string _keyProperty;
        private readonly string _elementDataKeyValue;
        private readonly object _dataSetLock = new object();
        private volatile bool _isDataSetInitialized;

        /// <summary>
        /// The Hash engine whose native lookup finds the profiles.
        /// </summary>
        private DeviceDetectionHashEngine _hashEngine;

        /// <inheritdoc/>
        public override string ElementDataKey => _elementDataKeyValue;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="loggerFactory">
        /// The factory used to create loggers.
        /// </param>
        /// <param name="indexedProperties">
        /// The properties to be indexed.
        /// </param>
        /// <param name="keyProperty">
        /// The property name to use for lookups (e.g. "TAC", "NativeModel").
        /// </param>
        /// <param name="elementDataKey">
        /// The unique key for this engine's data in flow data.
        /// </param>
        public PropertyKeyedDeviceBaseEngine(
            ILoggerFactory loggerFactory,
            IReadOnlyList<string> indexedProperties,
            string keyProperty,
            string elementDataKey) : base(
                loggerFactory,
                indexedProperties)
        {
            if (string.IsNullOrEmpty(keyProperty))
            {
                throw new ArgumentException(
                    "keyProperty is required and cannot be null or empty.",
                    nameof(keyProperty));
            }
            if (string.IsNullOrEmpty(elementDataKey))
            {
                throw new ArgumentException(
                    "elementDataKey is required and cannot be null or empty.",
                    nameof(elementDataKey));
            }

            _loggerMultiDd = loggerFactory.CreateLogger<MultiDeviceData>();
            _keyProperty = keyProperty;
            _elementDataKeyValue = elementDataKey;
            _dataSetService = new DataSetService(
                new PropertyValueQueryService(
                    indexedProperties,
                    loggerFactory),
                loggerFactory.CreateLogger<DataSetService>());
        }

        /// <inheritdoc/>
        protected override string GetKeyPropertyName() => _keyProperty;

        /// <summary>
        /// Creates the element data for each request.
        /// </summary>
        /// <param name="pipeline">The pipeline instance.</param>
        /// <returns>A new MultiDeviceData instance.</returns>
        protected override IMultiDeviceData CreateElementData(
            IPipeline pipeline)
        {
            EnsureDataSetInitialized(pipeline);
            
// On-premise engines legitimately use the base MissingPropertyService singleton.
// It's marked obsolete to make the split between MissingPropertyService and
// MissingPropertyServiceCloud a conscious, compiler-caught decision.
#pragma warning disable CS0618
            return new MultiDeviceData(
                _loggerMultiDd,
                pipeline,
                this,
                Pipeline.Engines.Services.MissingPropertyService.Instance,
                DataSet);
#pragma warning restore CS0618
        }

        /// <summary>
        /// Called when pipeline is added. Attempts early initialization.
        /// </summary>
        /// <param name="pipeline">The pipeline being added.</param>
        public override void AddPipeline(IPipeline pipeline)
        {
            if (_isDataSetInitialized == true)
            {
                throw new Exception("Data set already initialized.");
            }
            base.AddPipeline(pipeline);
            EnsureDataSetInitialized(pipeline);
        }

        /// <summary>
        /// Ensures that the DataSet is initialized.
        /// This is a fallback in case AddPipeline wasn't called.
        /// </summary>
        /// <param name="pipeline">
        /// The pipeline to query for the hash engine.
        /// </param>
        private void EnsureDataSetInitialized(IPipeline pipeline)
        {
            if (_isDataSetInitialized == false)
            {
                lock (_dataSetLock)
                {
                    if (_isDataSetInitialized == false)
                    {
                        _hashEngine = pipeline.GetDeviceDetectionHashEngine();
                        WarnIfNotIndexed();
                        DataSet = _dataSetService.BuildDataSet(
                            this,
                            pipeline);
                        _isDataSetInitialized = true;
                    }
                }
            }
        }

        /// <summary>
        /// Logs one warning, when the Pipeline is built, if the Hash engine
        /// does not index the property this engine is keyed on. Such a
        /// lookup still works but reads every profile, and the
        /// specification only recommends the index, so the configuration
        /// is allowed rather than refused.
        /// </summary>
        private void WarnIfNotIndexed()
        {
            if (_hashEngine.IndexedProperties.Contains(
                _keyProperty, StringComparer.Ordinal) == false)
            {
                Logger.LogWarning(
                    Messages.PropertyNotIndexed,
                    GetType().Name,
                    _keyProperty);
            }
        }

        /// <summary>
        /// Get the profiles from the Hash engine's native lookup, for every
        /// property. It matches the value exactly as the data file holds
        /// it and gives each profile once, in ascending profile id. It uses
        /// the data set's own index for a property the Hash engine's builder
        /// named with SetIndexedProperties, which is built when the data set
        /// loads and again when it refreshes, and reads every profile for
        /// any other, which gives the same profiles more slowly. Keeping an
        /// index here instead would match differently from every other
        /// language.
        /// </summary>
        protected override IEnumerable<uint> GetProfileIds(
            PropertyKeyedIndex propertyIndex,
            string value)
        {
            if (propertyIndex == null)
            {
                throw new ArgumentNullException(nameof(propertyIndex));
            }
            return _hashEngine.GetProfileIdsForPropertyValue(
                propertyIndex.MetaData.Name,
                value);
        }

        /// <inheritdoc/>
        protected override void ProcessProfileMatch(
            IFlowData data,
            IMultiDeviceData aspectData,
            uint profileId)
        {
            aspectData.AddProfileId(profileId);
        }
    }
}
