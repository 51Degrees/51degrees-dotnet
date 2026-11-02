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

using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Engines;
using FiftyOne.Pipeline.Engines.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.IpIntelligence.Examples
{
    public static class DataExtensions
    {
        /// <summary>
        /// Execute the specified function on the supplied <see cref="IElementData"/> instance.
        /// If a <see cref="PropertyMissingException"/> occurs then the resulting string will
        /// contain 'Unknown' + the message from the exception.
        /// </summary>
        /// <typeparam name="TData"></typeparam>
        /// <param name="data"></param>
        /// <param name="function"></param>
        /// <returns></returns>
        public static string TryGetValue<TData>(this TData data, Func<TData, string> function)
            where TData : IElementData
            => TryGetValue(data, function, ex => $"Unknown ({ex.Message})");
        
        /// <summary>
        /// Execute the specified function on the supplied <see cref="IElementData"/> instance.
        /// If a <see cref="PropertyMissingException"/> occurs then the resulting string will
        /// contain 'Unknown' + the message from the exception.
        /// </summary>
        /// <typeparam name="TData"></typeparam>
        /// <param name="data"></param>
        /// <param name="function"></param>
        /// <returns></returns>
        public static IReadOnlyList<string> TryGetValue<TData>(this TData data, Func<TData, IReadOnlyList<string>> function)
            where TData : IElementData
            => TryGetValue(data, function, ex => new[]{$"Unknown ({ex.Message})"});
        
        private static TResult TryGetValue<TData, TResult>(
            this TData data,
            Func<TData, TResult> function,
            Func<PropertyMissingException, TResult> fallbackFactory)
            where TData : IElementData
        {
            try
            {
                return function(data);
            }
            catch (PropertyMissingException pex)
            {
                return fallbackFactory(pex);
            }
        }

        /// <summary>
        /// Get a human-readable version of the specified <see cref="IAspectPropertyValue"/>.
        /// If no value has be set, the result will be 'Unknown' + the 
        /// <see cref="IAspectPropertyValue.NoValueMessage"/>.
        /// </summary>
        /// <param name="apv"></param>
        /// <returns></returns>
        public static string GetHumanReadable<T>(this IAspectPropertyValue<T> apv)
        {
            return apv.HasValue ? apv.Value.ToString() : $"Unknown ({apv.NoValueMessage})";
        }
        /// <inheritdoc cref="GetHumanReadable{T}(IAspectPropertyValue{T})"/>
        public static string GetHumanReadable<T>(this IAspectPropertyValue<IReadOnlyList<IWeightedValue<T>>> apv)
        {
            if (!apv.HasValue)
                return $"Unknown ({apv.NoValueMessage})";
            
            var values = apv.Value.Select(x => 
                Math.Abs(x.Weighting() - 1.0f) < 0.0001f ? x.Value.ToString() : $"({x.Value} @ {x.Weighting():F4})");
            return string.Join(", ", values);
        }
        /// <summary>
        /// Get a human-readable version of the specified <see cref="IAspectPropertyValue"/>.
        /// If no value has be set, the result will be 'Unknown' + the 
        /// <see cref="IAspectPropertyValue.NoValueMessage"/>.
        /// </summary>
        /// <param name="apv"></param>
        /// <returns></returns>
        public static IReadOnlyList<string> GetHumanReadableList<T>(this IAspectPropertyValue<IReadOnlyList<T>> apv)
        {
            return apv.HasValue
                ? apv.Value.Select(x => x.ToString()).ToList()
                : (IReadOnlyList<string>)new[] { $"Unknown ({apv.NoValueMessage})" };
        }
        /// <inheritdoc cref="GetHumanReadableList{T}(IAspectPropertyValue{IReadOnlyList{T}})"/>
        public static IReadOnlyList<string> GetHumanReadableList<T>(this IAspectPropertyValue<IReadOnlyList<IWeightedValue<T>>> apv)
        {
            return apv.HasValue
                ? apv.Value.Select(x => $"{x.Value} ({x.Weighting() * 100}%)").ToList()
                : (IReadOnlyList<string>)new[] { $"Unknown ({apv.NoValueMessage})" };
        }
    }
}
