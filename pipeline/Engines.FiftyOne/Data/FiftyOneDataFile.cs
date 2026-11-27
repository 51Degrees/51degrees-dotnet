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

using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using System;
using System.IO;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Data
{
    /// <summary>
    /// Class that stores meta-data relating to a data file associated
    /// with a 51Degrees engine.
    /// </summary>
    public class FiftyOneDataFile : AspectEngineDataFile, IFiftyOneDataFile
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="engineType"></param>
        public FiftyOneDataFile(Type engineType) : 
            base(engineType)
        {
        }

        private string _dataDownloadTypeFromEngine;
        private string _dataDownloadType;
        /// <summary>
        /// Get the type name to send when checking for data file updates.
        /// </summary>
        /// <remarks>
        /// In general, this value should be pulled from the Engine, which
        /// will have read it from the data file.
        /// However, in some cases, we want to know the type name before
        /// the engine is created. (e.g. when UpdateOnStartup is set)
        /// This is why the value can also be set.
        /// </remarks>
        public string DataDownloadType
        {
            get
            {
                if(_dataDownloadTypeFromEngine == null &&
                    Engine != null)
                {
                    _dataDownloadTypeFromEngine = (Engine as IFiftyOneAspectEngine)
                        .GetDataDownloadType(Identifier);
                }
                return _dataDownloadTypeFromEngine ?? _dataDownloadType;
            }
            set
            {
                _dataDownloadType = value;
            }
        }

        /// <summary>
        /// The start of the 'Type' of a Device Detection data file.
        /// </summary>
        private const string DeviceDetectionType = "Hash";

        /// <summary>
        /// The start of the product name of a Device Detection data file.
        /// </summary>
        private const string DeviceDetectionPrefix = "V4";

        /// <summary>
        /// The start of the 'Type' of an IP Intelligence data file.
        /// </summary>
        private const string IpIntelligenceType = "IPI";

        /// <summary>
        /// The start of the product name of an IP Intelligence data file.
        /// </summary>
        private const string IpIntelligencePrefix = "IPIV4";

        /// <summary>
        /// Reads the product name, such as 'V4TAC', from the header of the
        /// data file at the path given, without loading the file. The engine
        /// builder sets it, so that the product is known before the engine
        /// has loaded a data file, as for an update on start-up. Null when
        /// the engine has no way to read it.
        /// </summary>
        public Func<string, string> ProductFromFile { get; set; }

        /// <summary>
        /// The name the distributor gives the product of the data file. The
        /// name is a prefix for the kind of data file, 'V4' for Device
        /// Detection and 'IPIV4' for IP Intelligence, followed by the
        /// product value held in the data file. A value that already starts
        /// with the prefix is the product name as it is. For example,
        /// 'V4TAC' or 'IPIV4Enterprise'.
        /// </summary>
        /// <remarks>
        /// The product value comes from the engine once it has loaded the
        /// data file. Before that, the product name comes from the header of
        /// the file at <see cref="AspectEngineDataFile.DataFilePath"/>,
        /// through <see cref="ProductFromFile"/>, and already starts with the
        /// prefix. No product name is known, and the value is null, when
        /// neither gives a value, when the kind of data file has no prefix,
        /// or when the value is the prefix alone.
        /// </remarks>
        public override string DataUpdateProduct
        {
            get
            {
                // An engine that gives the product value of its data file
                // apart from its tier is asked for that value.
                var value = Engine == null ?
                    ReadProductFromFile() :
                    (Engine as IDataFileProductEngine)
                        ?.GetDataFileProduct(Identifier) ??
                    Engine.DataSourceTier;
                if (string.IsNullOrEmpty(value))
                {
                    return null;
                }

                var type = DataDownloadType;
                string prefix = null;
                if (type != null && type.StartsWith(
                    IpIntelligenceType, StringComparison.OrdinalIgnoreCase))
                {
                    prefix = IpIntelligencePrefix;
                }
                else if (type != null && type.StartsWith(
                    DeviceDetectionType, StringComparison.OrdinalIgnoreCase))
                {
                    prefix = DeviceDetectionPrefix;
                }
                if (prefix == null ||
                    string.Equals(
                        value, prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return value.StartsWith(
                    prefix, StringComparison.OrdinalIgnoreCase) ?
                    value : prefix + value;
            }
        }

        /// <summary>
        /// The product value in the header of the data file at
        /// <see cref="AspectEngineDataFile.DataFilePath"/>, or null when
        /// there is no file there, no way to read it, or its header cannot
        /// be read.
        /// </summary>
        private string ReadProductFromFile()
        {
            if (ProductFromFile == null ||
                string.IsNullOrEmpty(DataFilePath) ||
                File.Exists(DataFilePath) == false)
            {
                return null;
            }
            try
            {
                return ProductFromFile(DataFilePath);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // The native code reports a header it cannot read with an
            // exception, and a product that cannot be read is not known.
            catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                return null;
            }
        }
    }
}
