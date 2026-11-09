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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace FiftyOne.Pipeline.Web.Shared
{
    /// <summary>
    /// A read only view of a web request, keyed as its values would be as
    /// evidence, for example 'header.user-agent', which a web integration
    /// adds to evidence as
    /// <see cref="Core.Constants.EVIDENCE_HTTPREQUEST_KEY"/>.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/web-integration.md#populating-evidence">Specification</see>
    /// </summary>
    /// <remarks>
    /// The values are read from the request the first time the view is
    /// used, so a request whose view nothing reads costs only this object.
    /// The view has to be read while the request is being handled.
    /// </remarks>
    public abstract class RequestEvidenceView :
        IReadOnlyDictionary<string, object>
    {
        private Dictionary<string, object> _values;

        /// <summary>
        /// Add every value of the request, keyed as it would be as evidence.
        /// A later value for a key replaces an earlier one, as it does in
        /// evidence.
        /// </summary>
        /// <param name="values">The dictionary to add the values to.</param>
        protected abstract void AddValues(IDictionary<string, object> values);

        /// <summary>
        /// The values, read from the request on first use.
        /// </summary>
        private Dictionary<string, object> Read()
        {
            var values = Volatile.Read(ref _values);
            if (values == null)
            {
                values = new Dictionary<string, object>(
                    StringComparer.OrdinalIgnoreCase);
                AddValues(values);
                values = Interlocked.CompareExchange(
                    ref _values, values, null) ?? values;
            }
            return values;
        }

        /// <inheritdoc/>
        public object this[string key] => Read()[key];

        /// <inheritdoc/>
        public IEnumerable<string> Keys => Read().Keys;

        /// <inheritdoc/>
        public IEnumerable<object> Values => Read().Values;

        /// <inheritdoc/>
        public int Count => Read().Count;

        /// <inheritdoc/>
        public bool ContainsKey(string key) => Read().ContainsKey(key);

        /// <inheritdoc/>
        public bool TryGetValue(string key, out object value) =>
            Read().TryGetValue(key, out value);

        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() =>
            Read().GetEnumerator();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
