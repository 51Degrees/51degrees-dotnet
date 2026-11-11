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
using FiftyOne.IpIntelligence.Shared.Data;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace FiftyOne.IpIntelligence.Engine.OnPremise.Data
{
    /// <summary>
    /// The IP Intelligence data of one flow data. Its native results hold
    /// the data set in memory until they are freed, so the flow data
    /// disposes it.
    /// </summary>
    internal class IpDataOnPremise : IpDataBaseOnPremise<ResultsIpiSwig>, IIpDataOnPremise, IDisposable
    {
        #region Constructor

        /// <summary>
        /// Construct a new instance of the wrapper.
        /// </summary>
        /// <param name="logger">
        /// The logger instance to use.
        /// </param>
        /// <param name="pipeline">
        /// The Pipeline that created this data instance.
        /// </param>
        /// <param name="engine">
        /// The engine that create this data instance.
        /// </param>
        /// <param name="missingPropertyService">
        /// The <see cref="IMissingPropertyService"/> to use if a requested
        /// property does not exist.
        /// </param>
        internal IpDataOnPremise(
            ILogger<AspectDataBase> logger,
            IPipeline pipeline,
            IpiOnPremiseEngine engine,
            IMissingPropertyService missingPropertyService)
            : base(logger, pipeline, engine, missingPropertyService)
        {
            _engine = engine;
            _asDict = new Lazy<IReadOnlyDictionary<string, object>>(AsStrippedDictionary);
        }

        #endregion

        #region Internal Methods

        private FiftyOne.Pipeline.Engines.Data.AspectPropertyValue<System.Net.IPAddress> _echoIp =
            new FiftyOne.Pipeline.Engines.Data.AspectPropertyValue<System.Net.IPAddress>();
        private FiftyOne.Pipeline.Engines.Data.AspectPropertyValue<System.Net.IPAddress> _echoIpV6 =
            new FiftyOne.Pipeline.Engines.Data.AspectPropertyValue<System.Net.IPAddress>();

        private readonly Lazy<IReadOnlyDictionary<string, object>> _asDict;

        /// <summary>
        /// The engine that made this data, which holds the index of each
        /// property name it was built with.
        /// </summary>
        private readonly IpiOnPremiseEngine _engine;

        /// <summary>
        /// The reason reported for both echo properties when the request did
        /// carry a client IP but none of the supplied values could be read as
        /// an address.
        /// </summary>
        internal const string InvalidIpEvidenceMessage =
            "The IP supplied as evidence was not valid.";

        /// <summary>
        /// Set the echo IP values captured from request evidence.
        /// Called by IpiOnPremiseEngine.ProcessEngine.
        /// </summary>
        /// <param name="ipv4">Parsed IPv4 address, or null if none.</param>
        /// <param name="ipv6">Parsed IPv6 address, or null if none.</param>
        /// <param name="clientIpSupplied">
        /// True when the request carried a non-blank client IP value,
        /// whatever its content. It separates the two reasons an echo
        /// property can have no value: nothing was supplied, or what was
        /// supplied could not be read as an IP address. Reporting the second
        /// as the first sends the caller looking for a header that is in fact
        /// present - see issue #332.
        /// </param>
        internal void SetEchoIp(
            System.Net.IPAddress ipv4,
            System.Net.IPAddress ipv6,
            bool clientIpSupplied)
        {
            _echoIp = new FiftyOne.Pipeline.Engines.Data.AspectPropertyValue<System.Net.IPAddress>();
            _echoIpV6 = new FiftyOne.Pipeline.Engines.Data.AspectPropertyValue<System.Net.IPAddress>();

            // Only when NEITHER family resolved does supplied evidence mean
            // the supplied evidence was bad. If one family resolved, the
            // other is genuinely absent from the request rather than
            // invalid, and "was not valid" would misreport it.
            var suppliedButInvalid =
                clientIpSupplied && ipv4 == null && ipv6 == null;

            if (ipv4 != null)
            {
                _echoIp.Value = ipv4;
            }
            else
            {
                _echoIp.NoValueMessage = suppliedButInvalid
                    ? InvalidIpEvidenceMessage
                    : "IPv4 was not supplied as evidence.";
            }

            if (ipv6 != null)
            {
                _echoIpV6.Value = ipv6;
            }
            else
            {
                _echoIpV6.NoValueMessage = suppliedButInvalid
                    ? InvalidIpEvidenceMessage
                    : "IPv6 was not supplied as evidence.";
            }
        }

        internal void SetResults(ResultsIpiSwig results)
        {
            Results.AddResult(results);
        }

        /// <summary>
        /// Frees the native results. The results' finalizers free them if
        /// this is never called.
        /// </summary>
        /// <param name="disposing">
        /// True when called from <see cref="Dispose()"/>.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Results.Dispose();
            }
        }

        /// <summary>
        /// Frees the native results.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Drops the Ip and IpV6 entries when the matching address is not in
        /// evidence. Direct .NET accessors are unchanged.
        /// </summary>
        public override IReadOnlyDictionary<string, object> AsDictionary()
            => _asDict.Value;

        private IReadOnlyDictionary<string, object> AsStrippedDictionary()
        {
            return base.AsDictionary()
                .Where(x =>
                {
                    if (string.Compare(x.Key, "ip", StringComparison.InvariantCultureIgnoreCase) == 0)
                    {
                        return _echoIp.HasValue;
                    }
                    if (string.Compare(x.Key, "ipv6", StringComparison.InvariantCultureIgnoreCase) == 0)
                    {
                        return _echoIpV6.HasValue;
                    }
                    return true;
                }).ToDictionary(
                    kv => kv.Key, kv => kv.Value,
                    StringComparer.InvariantCultureIgnoreCase);
            
        }
        #endregion

        #region Private Methods

        /// <summary>
        /// Gets the results to read a property from, with the property's
        /// required property index when the name is in the engine's table,
        /// or -1 to read it by name.
        /// </summary>
        private ResultsIpiSwig GetResults(string propertyName, out int index)
        {
            var indexes = _engine?.PropertyIndexes;
            if (indexes != null &&
                indexes.TryGetValue(propertyName, out index))
            {
                var list = Results.ResultsList;
                if (list.Count == 1)
                {
                    return list[0];
                }
            }
            index = -1;
            return GetResultsContainingProperty(propertyName);
        }

        private ResultsIpiSwig GetResultsContainingProperty(string propertyName)
        {
            foreach (var results in Results.ResultsList)
            {
                if (results.containsProperty(propertyName))
                {
                    return results;
                }
            }
            return null;
        }

        #endregion

        #region Protected Methods

        protected override bool PropertyIsAvailable(string propertyName)
        {
            if (string.Equals(propertyName, "Ip", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(propertyName, "IpV6", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return Results.ResultsList
                .Any(r => r.containsProperty(propertyName));
        }

        protected override IAspectPropertyValue<IReadOnlyList<IWeightedValue<bool>>> GetValuesAsWeightedBoolList(string propertyName)
        {
            var result = new AspectPropertyValue<IReadOnlyList<IWeightedValue<bool>>>();
            var results = GetResults(propertyName, out var index);

            if (results != null)
            {
                using (var value = index < 0 ?
                    results.getValuesAsWeightedBoolList(propertyName) :
                    results.getValuesAsWeightedBoolList(index))
                {
                    if (value.hasValue())
                    {
                        using (var list = value.getValue())
                        {
                            result.Value = new WeightedBoolListSwigWrapper(list);
                        }
                    }
                    else
                    {
                        result.NoValueMessage = value.getNoValueMessage();
                    }
                }
            }
            return result;
        }

        protected override IAspectPropertyValue<IReadOnlyList<IWeightedValue<double>>> GetValuesAsWeightedDoubleList(string propertyName)
        {
            var result = new AspectPropertyValue<IReadOnlyList<IWeightedValue<double>>>();
            var results = GetResults(propertyName, out var index);

            if (results != null)
            {
                using (var value = index < 0 ?
                    results.getValuesAsWeightedDoubleList(propertyName) :
                    results.getValuesAsWeightedDoubleList(index))
                {
                    if (value.hasValue())
                    {
                        using (var list = value.getValue())
                        {
                            result.Value = new WeightedDoubleListSwigWrapper(list);
                        }
                    }
                    else
                    {
                        result.NoValueMessage = value.getNoValueMessage();
                    }
                }
            }
            return result;
        }

        protected override IAspectPropertyValue<IReadOnlyList<IWeightedValue<float>>> GetValuesAsWeightedFloatList(string propertyName)
        {
            var result = new AspectPropertyValue<IReadOnlyList<IWeightedValue<float>>>();
            var results = GetResults(propertyName, out var index);

            if (results != null)
            {
                using (var value = index < 0 ?
                    results.getValuesAsWeightedDoubleList(propertyName) :
                    results.getValuesAsWeightedDoubleList(index))
                {
                    if (value.hasValue())
                    {
                        using (var list = value.getValue())
                        {
                            result.Value = new List<IWeightedValue<float>>(new WeightedDoubleListSwigWrapper(list)
                                .Select(x => new WeightedValue<float>(x.RawWeighting, (float)x.Value)));
                        }
                    }
                    else
                    {
                        result.NoValueMessage = value.getNoValueMessage();
                    }
                }
            }
            return result;
        }

        protected override IAspectPropertyValue<IReadOnlyList<IWeightedValue<int>>> GetValuesAsWeightedIntegerList(string propertyName)
        {
            var result = new AspectPropertyValue<IReadOnlyList<IWeightedValue<int>>>();
            var results = GetResults(propertyName, out var index);

            if (results != null)
            {
                using (var value = index < 0 ?
                    results.getValuesAsWeightedIntegerList(propertyName) :
                    results.getValuesAsWeightedIntegerList(index))
                {
                    if (value.hasValue())
                    {
                        using (var list = value.getValue())
                        {
                            result.Value = new WeightedIntListSwigWrapper(list);
                        }
                    }
                    else
                    {
                        result.NoValueMessage = value.getNoValueMessage();
                    }
                }
            }
            return result;
        }

        public override IAspectPropertyValue<IReadOnlyList<string>> GetValues(string propertyName)
        {
            var result = new AspectPropertyValue<IReadOnlyList<string>>();
            var results = GetResults(propertyName, out var index);

            if (results != null)
            {
                using (var value = index < 0 ?
                    results.getValues(propertyName) :
                    results.getValues(index))
                {
                    if (value.hasValue())
                    {
                        using (var vector = value.getValue())
                        {
                            result.Value = vector.ToList();
                        }
                    }
                    else
                    {
                        result.NoValueMessage = value.getNoValueMessage();
                    }
                }
            }
            return result;
        }

        protected override IAspectPropertyValue<string> GetValueAsString(string propertyName)
        {
            var result = new AspectPropertyValue<string>();
            var results = GetResults(propertyName, out var index);

            if (results != null)
            {
                using (var value = index < 0 ?
                    results.getValueAsUTF8String(propertyName) :
                    results.getValueAsUTF8String(index))
                {
                    if (value.hasValue())
                    {
                        using (var utf8value = value.getValue())
                        {
                            result.Value = utf8value.ToUTF8String();
                        }
                    }
                    else
                    {
                        result.NoValueMessage = value.getNoValueMessage();
                    }
                }
            }
            return result;
        }

        protected override IAspectPropertyValue<WktString> GetValueAsWktString(string propertyName)
        {
            var result = new AspectPropertyValue<WktString>();
            var results = GetResults(propertyName, out var index);

            if (results != null)
            {
                using (var value = index < 0 ?
                    results.getValueAsString(propertyName) :
                    results.getValueAsString(index))
                {
                    if (value.hasValue())
                    {
                        result.Value = new WktString(value.getValue());
                    }
                    else
                    {
                        result.NoValueMessage = value.getNoValueMessage();
                    }
                }
            }
            return result;
        }

        protected override IAspectPropertyValue<int> GetValueAsInteger(string propertyName)
        {
            var result = new AspectPropertyValue<int>();
            var results = GetResults(propertyName, out var index);

            if (results != null)
            {
                using (var value = index < 0 ?
                    results.getValueAsInteger(propertyName) :
                    results.getValueAsInteger(index))
                {
                    if (value.hasValue())
                    {
                        result.Value = value.getValue();
                    }
                    else
                    {
                        result.NoValueMessage = value.getNoValueMessage();
                    }
                }
            }
            return result;
        }

        protected override IAspectPropertyValue<bool> GetValueAsBool(string propertyName)
        { 
            var result = new AspectPropertyValue<bool>();
            var results = GetResults(propertyName, out var index);

            if (results != null)
            {
                using (var value = index < 0 ?
                    results.getValueAsBool(propertyName) :
                    results.getValueAsBool(index))
                {
                    if (value.hasValue())
                    {
                        result.Value = value.getValue();
                    }
                    else
                    {
                        result.NoValueMessage = value.getNoValueMessage();
                    }
                }
            }
            return result;
        }

        protected override IAspectPropertyValue<double> GetValueAsDouble(string propertyName)
        {
            var result = new AspectPropertyValue<double>();
            var results = GetResults(propertyName, out var index);

            if (results != null)
            {
                using (var value = index < 0 ?
                    results.getValueAsDouble(propertyName) :
                    results.getValueAsDouble(index))
                {
                    if (value.hasValue())
                    {
                        result.Value = value.getValue();
                    }
                    else
                    {
                        result.NoValueMessage = value.getNoValueMessage();
                    }
                }
            }
            return result;
        }

        protected override IAspectPropertyValue<float> GetValueAsFloat(string propertyName)
        {
            var result = new AspectPropertyValue<float>();
            var results = GetResults(propertyName, out var index);

            if (results != null)
            {
                using (var value = index < 0 ?
                    results.getValueAsDouble(propertyName) :
                    results.getValueAsDouble(index))
                {
                    if (value.hasValue())
                    {
                        result.Value = (float)value.getValue();
                    }
                    else
                    {
                        result.NoValueMessage = value.getNoValueMessage();
                    }
                }
            }
            return result;
        }

        protected override IAspectPropertyValue<IPAddress> GetValueAsIp(string propertyName)
        {
            if (string.Equals(propertyName, "Ip", System.StringComparison.OrdinalIgnoreCase))
            {
                return _echoIp;
            }
            if (string.Equals(propertyName, "IpV6", System.StringComparison.OrdinalIgnoreCase))
            {
                return _echoIpV6;
            }

            var result = new AspectPropertyValue<IPAddress>();
            var results = GetResults(propertyName, out var index);

            if (results != null)
            {
                using (var value = index < 0 ?
                    results.getValueAsString(propertyName) :
                    results.getValueAsString(index))
                {
                    if (value.hasValue())
                    {
                        result.Value = IPAddress.Parse(value.getValue());
                    }
                    else
                    {
                        result.NoValueMessage = value.getNoValueMessage();
                    }
                }
            }
            return result;
        }

        protected override IAspectPropertyValue<IReadOnlyList<IWeightedValue<string>>> GetValuesAsWeightedStringList(string propertyName)
        {
            var result = new AspectPropertyValue<IReadOnlyList<IWeightedValue<string>>>();
            var results = GetResults(propertyName, out var index);

            if (results != null)
            {
                using (var value = index < 0 ?
                    results.getValuesAsWeightedUTF8StringList(propertyName) :
                    results.getValuesAsWeightedUTF8StringList(index))
                {
                    if (value.hasValue())
                    {
                        using (var list = value.getValue())
                        {
                            result.Value = new WeightedUTF8StringListSwigWrapper(list);
                        }
                    }
                    else
                    {
                        result.NoValueMessage = value.getNoValueMessage();
                    }
                }
            }
            return result;
        }

        protected override IAspectPropertyValue<IReadOnlyList<IWeightedValue<IPAddress>>> GetValuesAsWeightedIPList(string propertyName)
        {
            var result = new AspectPropertyValue<IReadOnlyList<IWeightedValue<IPAddress>>>();
            var results = GetResults(propertyName, out var index);

            if (results != null)
            {
                using (var value = index < 0 ?
                    results.getValuesAsWeightedStringList(propertyName) :
                    results.getValuesAsWeightedStringList(index))
                {
                    if (value.hasValue())
                    {
                        using (var list = value.getValue())
                        {
                            result.Value = new List<IWeightedValue<IPAddress>>(new WeightedStringListSwigWrapper(list)
                                .Select(x => new WeightedValue<IPAddress>(x.RawWeighting, IPAddress.Parse(x.Value))));
                        }
                    }
                    else
                    {
                        result.NoValueMessage = value.getNoValueMessage();
                    }
                }
            }
            return result;
        }

        protected override IAspectPropertyValue<IReadOnlyList<IWeightedValue<string>>> GetValuesAsWeightedWKTStringList(
            string propertyName, byte decimalPlaces)
        {
            var result = new AspectPropertyValue<IReadOnlyList<IWeightedValue<string>>>();
            var results = GetResults(propertyName, out var index);

            if (results != null)
            {
                using (var value = index < 0 ?
                    results.getValuesAsWeightedWKTStringList(propertyName, decimalPlaces) :
                    results.getValuesAsWeightedWKTStringList(index, decimalPlaces))
                {
                    if (value.hasValue())
                    {
                        using (var list = value.getValue())
                        {
                            result.Value = new WeightedStringListSwigWrapper(list);
                        }
                    }
                    else
                    {
                        result.NoValueMessage = value.getNoValueMessage();
                    }
                }
            }
            return result;
        }

        #endregion
    }
}
