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

using System.Collections.Generic;

using Microsoft.Extensions.Primitives;
using FiftyOne.Pipeline.Core.FlowElements;
using System.Linq;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.JavaScriptBuilder.FlowElement;
using System.Globalization;
using FiftyOne.Pipeline.JsonBuilder.FlowElement;
using System.Text;
using FiftyOne.Pipeline.Web.Shared.Adapters;
using Microsoft.Extensions.Logging;
using FiftyOne.Pipeline.JavaScriptBuilder.Data;
using FiftyOne.Pipeline.JsonBuilder.Data;

namespace FiftyOne.Pipeline.Web.Shared.Services
{
    /// <summary>
    /// Class that provides functionality for the 'Client side Overrides'
    /// feature.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/web-integration.md#client-side-features">Specification</see>
    /// </summary>
    public class ClientsidePropertyService : IClientsidePropertyService
    {
        /// <summary>
        /// Pipeline
        /// </summary>
        private readonly IPipeline _pipeline;

        private readonly ILogger<ClientsidePropertyService> _logger;

        /// <summary>
        /// A list of all the HTTP headers that are requested evidence
        /// for elements that populate JavaScript properties 
        /// </summary>
        private StringValues? _headersAffectingJavaScript = null;
        private readonly object _headersAffectingJavaScriptLock = new object();

        private StringValues HeadersAffectingJavaScript
        {
            get
            {
                if (_headersAffectingJavaScript.HasValue)
                {
                    return _headersAffectingJavaScript.Value;
                }

                lock (_headersAffectingJavaScriptLock)
                {
                    if (_headersAffectingJavaScript.HasValue)
                    {
                        return _headersAffectingJavaScript.Value;
                    }

                    CollectHeadersAffectingJavaScript(out StringValues newHeaders, out bool gotExceptions);

                    if (!gotExceptions)
                    {
                        _headersAffectingJavaScript = newHeaders;
                    }
                    return newHeaders;
                }
            }
        }

        private enum ContentType
        {
            JavaScript,
            Json
        }

        /// <summary>
        /// The cache control values that will be set for the JavaScript and
        /// JSON.
        /// </summary>
        private readonly StringValues _cacheControl = new StringValues(
            new string[] {
                "private",
                "max-age=1800",
            });

        /// <summary>
        /// The cache control value for a response to a request whose
        /// processing recorded errors. A browser may keep the response but
        /// must ask again before using it, and the response carries no ETag,
        /// so the next request gets a full response rather than a 304.
        /// </summary>
        private const string CacheControlNotCached = "private, no-cache";

        /// <summary>
        /// The prefix that marks an entity tag as weak. The ETag stands for
        /// the evidence rather than the content, which can change for the
        /// same evidence when a data file is refreshed, so it is a weak
        /// validator (RFC 9110 sections 8.8.1 and 8.8.3).
        /// </summary>
        private const string WeakETagPrefix = "W/";

        /// <summary>
        /// The 64 bit FNV-1a offset basis and prime. The ETag uses a hash
        /// with no per-process seed so that the same evidence gives the same
        /// ETag in every process.
        /// </summary>
        private const ulong FnvOffsetBasis = 14695981039346656037;
        private const ulong FnvPrime = 1099511628211;

        /// <summary>
        /// Create a new ClientsidePropertyService
        /// </summary>
        /// <param name="pipeline"></param>
        /// <param name="logger"></param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if a required parameter is null.
        /// </exception>
        public ClientsidePropertyService(
            IPipeline pipeline,
            ILogger<ClientsidePropertyService> logger)
        {
            if (pipeline == null) throw new ArgumentNullException(nameof(pipeline));

            _pipeline = pipeline;
            _logger = logger;
        }

        private void CollectHeadersAffectingJavaScript(out StringValues headers, out bool gotExceptions)
        {
            var headersAffectingJavaScript = new List<string>();
            gotExceptions = false;

            foreach (var flowElement in _pipeline.FlowElements)
            {
                IEvidenceKeyFilter filter;
                try
                {
                    filter = flowElement.EvidenceKeyFilter;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to get {nameof(flowElement.EvidenceKeyFilter)} from {{flowElementType}}", flowElement.GetType().Name);
                    gotExceptions = true;
                    continue;
                }

                // If the filter is a white list or derived type then
                // get all HTTP header evidence keys from white list
                // and add them to the headers that could affect the 
                // generated JavaScript.
                var inclusionList = filter as EvidenceKeyFilterWhitelist;
                if (inclusionList != null)
                {
                    headersAffectingJavaScript.AddRange(inclusionList.Whitelist
                        .Where(entry => entry.Key.StartsWith(
                            Core.Constants.EVIDENCE_HTTPHEADER_PREFIX + 
                            Core.Constants.EVIDENCE_SEPERATOR, 
                            StringComparison.OrdinalIgnoreCase) &&
                            // Exclude any header names that contain
                            // control characters.
                            entry.Key.Any(c => char.IsControl(c)) == false)
                        .Select(entry => entry.Key.Substring(entry.Key.IndexOf(
                            Core.Constants.EVIDENCE_SEPERATOR, 
                            StringComparison.OrdinalIgnoreCase) + 1))
                        // Only include headers that are not already in
                        // the list.
                        .Where(entry => headersAffectingJavaScript.Contains(
                            entry, StringComparer.OrdinalIgnoreCase) == false)
                        .Distinct(StringComparer.OrdinalIgnoreCase));
                }
            }
            headers = new StringValues(headersAffectingJavaScript.ToArray());
        }

        /// <summary>
        /// Add the JavaScript from the flow data object to the HttpResponse
        /// </summary>
        /// <param name="context">
        /// An <see cref="IContextAdapter"/> representing the HttpResponse 
        /// to add the JavaScript to.
        /// </param>
        /// <param name="flowData">
        /// The flow data to get the JavaScript from.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if a required parameter is null.
        /// </exception>
        public void ServeJavascript(IContextAdapter context, IFlowData flowData)
        {
            ServeContent(context, flowData, ContentType.JavaScript);
        }

        /// <summary>
        /// Add the JSON from the flow data object to the HttpResponse
        /// </summary>
        /// <param name="context">
        /// An <see cref="IContextAdapter"/> representing the HttpResponse 
        /// to add the JSON to.
        /// </param>
        /// <param name="flowData">
        /// The flow data to get the JSON from.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if a required parameter is null.
        /// </exception>
        public void ServeJson(IContextAdapter context, IFlowData flowData)
        { 
            ServeContent(context, flowData, ContentType.Json);
        }

        private void ServeContent(IContextAdapter context, IFlowData flowData, ContentType contentType)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (flowData == null) throw new ArgumentNullException(nameof(flowData));

            context.Response.Clear();
            context.Response.ClearHeaders();

            // A response to a request whose processing recorded errors is
            // not cached and carries no ETag.
            bool hadFailures = flowData.Errors?.Count > 0;

            IEvidenceKeyFilter pipelineEvidenceKeyFilter = null;
            try
            {
                pipelineEvidenceKeyFilter = _pipeline.EvidenceKeyFilter;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to get {nameof(_pipeline.EvidenceKeyFilter)} from {nameof(_pipeline)}");
                hadFailures = true;
            }

            string eTag = null;
            if (pipelineEvidenceKeyFilter != null) {
                eTag = GetETag(flowData, pipelineEvidenceKeyFilter);
            }

            bool matched = IfNoneMatchMatches(
                context.Request.GetHeaderValue("If-None-Match"), eTag);

            if (matched && IsGetOrHead(context.Request) == false)
            {
                // A matching validator fails the precondition of any method
                // other than GET or HEAD (RFC 9110 section 13.1.2).
                context.Response.StatusCode = 412;
                SetAllowOriginHeader(context);
            }
            else if (matched)
            {
                // The response hasn't changed so respond with a 304, which
                // carries the caching headers the 200 would carry (RFC 9110
                // section 15.4.5).
                if (HasBuilderData(flowData, contentType) == false)
                {
                    hadFailures = true;
                }
                context.Response.StatusCode = 304;
                SetCachingHeaders(context, eTag, !hadFailures);
            }
            else
            {
                // Otherwise, return the requested content to the client.
                string content = null;
                Func<IJsonBuilderElementData> GetJsonData = () =>
                {
                    var jsonElement = flowData.Pipeline.GetElement<JsonBuilderElement>();
                    if (jsonElement == null)
                    {
                        throw new PipelineConfigurationException(
                            Messages.ExceptionNoJsonBuilder);
                    }
                    IJsonBuilderElementData jsonData = null;
                    try
                    {
                        jsonData = flowData.GetFromElement(jsonElement);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to get data from {flowElementType}", jsonElement.GetType().Name);
                        jsonData = jsonElement.GetFallbackResponse(flowData);
                        hadFailures = true;
                    }
                    return jsonData;
                };

                Func<IJavaScriptBuilderElementData> GetJsData = () =>
                {
                    var jsElement = flowData.Pipeline.GetElement<JavaScriptBuilderElement>();
                    if (jsElement == null)
                    {
                        throw new PipelineConfigurationException(
                            Messages.ExceptionNoJavaScriptBuilder);
                    }
                    IJavaScriptBuilderElementData jsData;
                    try
                    {
                        jsData = flowData.GetFromElement(jsElement);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to get data from {flowElementType}", jsElement.GetType().Name);
                        jsData = jsElement.GetFallbackResponse(flowData, GetJsonData());
                        hadFailures = true;
                    }
                    return jsData;
                };

                switch (contentType)
                {
                    case ContentType.JavaScript:
                        content = GetJsData()?.JavaScript;
                        break;
                    case ContentType.Json:
                        content = GetJsonData()?.Json;
                        break;
                    default:
                        break;
                }

                int length = 0;
                if (content != null && content.Length > 0)
                {
                    // GetByteCount computes the length without allocating
                    // the byte array that GetBytes would create.
                    length = Encoding.UTF8.GetByteCount(content);
                }

                context.Response.StatusCode = 200;
                SetHeaders(context, 
                    eTag,
                    length,
                    contentType == ContentType.JavaScript ? "x-javascript" : "json",
                    !hadFailures);

                context.Response.Write(content);
            }

        }

        /// <summary>
        /// True when the flow data holds the data of the builder whose output
        /// the endpoint serves, so a 200 would not have to fall back.
        /// </summary>
        private bool HasBuilderData(IFlowData flowData, ContentType contentType)
        {
            IFlowElement element = null;
            try
            {
                if (contentType == ContentType.JavaScript)
                {
                    var jsElement = flowData.Pipeline
                        .GetElement<JavaScriptBuilderElement>();
                    element = jsElement;
                    if (jsElement != null)
                    {
                        flowData.GetFromElement(jsElement);
                    }
                }
                else
                {
                    var jsonElement = flowData.Pipeline
                        .GetElement<JsonBuilderElement>();
                    element = jsonElement;
                    if (jsonElement != null)
                    {
                        flowData.GetFromElement(jsonElement);
                    }
                }
                return element != null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex,
                    "Failed to get data from {flowElementType}",
                    element?.GetType().Name);
                return false;
            }
        }

        /// <summary>
        /// The ETag for the evidence the filter takes from the request,
        /// being a weak entity tag holding a 64 bit FNV-1a hash of each
        /// evidence name, in lower case, and value, in the order of the
        /// names. The hash has no per-process seed, unlike
        /// <see cref="string.GetHashCode()"/>, so the same evidence gives the
        /// same ETag in every process running the same build.
        /// </summary>
        private static string GetETag(
            IFlowData flowData,
            IEvidenceKeyFilter filter)
        {
            var entries = new List<KeyValuePair<string, object>>();
            foreach (var entry in GetSuppliedEvidence(flowData))
            {
                if (filter.Include(entry.Key))
                {
                    entries.Add(entry);
                }
            }
            entries.Sort((a, b) =>
            {
                var result = StringComparer.OrdinalIgnoreCase.Compare(
                    a.Key, b.Key);
                return result != 0 ?
                    result :
                    StringComparer.Ordinal.Compare(a.Key, b.Key);
            });

            var hash = FnvOffsetBasis;
            foreach (var entry in entries)
            {
                hash = AddText(hash, entry.Key, true);
                hash = AddText(hash, GetValueText(entry.Value), false);
            }
            return WeakETagPrefix + "\"" +
                hash.ToString("x16", CultureInfo.InvariantCulture) + "\"";
        }

        /// <summary>
        /// The evidence the request supplied. A Flow Element can add or
        /// replace evidence while processing, as the Sequence Element does
        /// with a new session id, so the evidence after processing differs
        /// on every request and a revalidation would never match.
        /// </summary>
        private static IReadOnlyDictionary<string, object> GetSuppliedEvidence(
            IFlowData flowData)
        {
            return flowData is FlowData supplied ?
                supplied.GetSuppliedEvidence() :
                flowData.GetEvidence().AsDictionary();
        }

        /// <summary>
        /// The text an evidence value is hashed as. Numbers and other
        /// formattable values use the invariant culture so the text is the
        /// same in every process, and any other value uses its ToString.
        /// </summary>
        private static string GetValueText(object value)
        {
            switch (value)
            {
                case null:
                    return null;
                case string text:
                    return text;
                case IFormattable formattable:
                    return formattable.ToString(
                        null, CultureInfo.InvariantCulture);
                default:
                    return value.ToString();
            }
        }

        /// <summary>
        /// Add the length of the text, then each UTF-16 code unit low byte
        /// first, to the hash. The length keeps ("ab", "c") apart from
        /// ("a", "bc"), and -1 stands for null.
        /// </summary>
        private static ulong AddText(ulong hash, string text, bool lowerCase)
        {
            var length = text == null ? -1 : text.Length;
            for (var i = 0; i < 4; i++)
            {
                hash = AddByte(hash, (byte)(length >> (i * 8)));
            }
            if (text != null)
            {
                foreach (var c in text)
                {
                    var unit = lowerCase ? char.ToLowerInvariant(c) : c;
                    hash = AddByte(hash, (byte)unit);
                    hash = AddByte(hash, (byte)(unit >> 8));
                }
            }
            return hash;
        }

        private static ulong AddByte(ulong hash, byte value)
        {
            return unchecked((hash ^ value) * FnvPrime);
        }

        /// <summary>
        /// True when the If-None-Match header matches the ETag. The field is
        /// either "*" or a comma separated list of entity tags, and a list
        /// matches when one tag's quoted part equals the ETag's, weak or not
        /// (RFC 9110 sections 8.8.3.2 and 13.1.2). A field that is neither
        /// is not a valid If-None-Match, so the condition is true and the
        /// response is the full one (section 13.1.2, and section 13.2.2
        /// step 3).
        /// </summary>
        private static bool IfNoneMatchMatches(string ifNoneMatch, string eTag)
        {
            if (string.IsNullOrWhiteSpace(ifNoneMatch))
            {
                return false;
            }
            var field = ifNoneMatch.Trim(' ', '	');
            if (field == "*")
            {
                // The condition is false while the origin has a current
                // representation, whether or not an ETag can be made.
                return true;
            }
            if (eTag == null)
            {
                return false;
            }
            var opaqueTag = eTag.Substring(WeakETagPrefix.Length);
            var matched = false;
            var needComma = false;
            var position = 0;
            while (true)
            {
                while (position < field.Length &&
                    (field[position] == ' ' || field[position] == '	'))
                {
                    position++;
                }
                if (position == field.Length)
                {
                    return matched;
                }
                if (field[position] == ',')
                {
                    position++;
                    needComma = false;
                    continue;
                }
                if (needComma ||
                    TryReadEntityTag(field, ref position, out var tag) == false)
                {
                    return false;
                }
                matched = matched ||
                    string.Equals(tag, opaqueTag, StringComparison.Ordinal);
                needComma = true;
            }
        }

        /// <summary>
        /// Reads the entity tag at the position, being an optional "W/" and
        /// a quoted string of etagc characters (RFC 9110 section 8.8.3),
        /// and moves the position past it. The tag returned is the quoted
        /// part without "W/".
        /// </summary>
        private static bool TryReadEntityTag(
            string field,
            ref int position,
            out string tag)
        {
            tag = null;
            var start = position;
            if (string.CompareOrdinal(
                field, start, WeakETagPrefix, 0, WeakETagPrefix.Length) == 0)
            {
                start += WeakETagPrefix.Length;
            }
            if (start >= field.Length || field[start] != '"')
            {
                return false;
            }
            for (var i = start + 1; i < field.Length; i++)
            {
                var c = field[i];
                if (c == '"')
                {
                    tag = field.Substring(start, i + 1 - start);
                    position = i + 1;
                    return true;
                }
                // etagc is %x21, %x23-7E or obs-text (%x80-FF).
                if (c != 0x21 && (c < 0x23 || c == 0x7f || c > 0xFF))
                {
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// True when the request is a GET or a HEAD, or its adapter does not
        /// give the method. Method names are case-sensitive (RFC 9110
        /// section 9.1).
        /// </summary>
        private static bool IsGetOrHead(IRequestAdapter request)
        {
            var method = (request as IRequestMethodAdapter)?.Method;
            return string.IsNullOrEmpty(method) ||
                method == "GET" ||
                method == "HEAD";
        }

        /// <summary>
        /// Set the content headers and the caching headers on a 200
        /// response.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="eTag"></param>
        /// <param name="contentLength"></param>
        /// <param name="contentType"></param>
        /// <param name="shouldCache"></param>
        private void SetHeaders(
            IContextAdapter context,
            string eTag,
            int contentLength,
            string contentType,
            bool shouldCache)
        {
            try
            {
                context.Response.SetHeader("Content-Type",
                    $"application/{contentType}");
                context.Response.SetHeader("Content-Length",
                    contentLength.ToString(CultureInfo.InvariantCulture));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, Messages.MessageJavaScriptCachingError);
            }
            SetCachingHeaders(context, eTag, shouldCache);
        }

        /// <summary>
        /// Set the headers that a 200 and a 304 to the same request both
        /// carry, being Cache-Control, Vary, ETag and
        /// Access-Control-Allow-Origin. A response that is not cached carries
        /// no ETag, so a browser holding it has nothing to revalidate with.
        /// </summary>
        private void SetCachingHeaders(
            IContextAdapter context,
            string eTag,
            bool shouldCache)
        {
            try
            {
                context.Response.SetHeader("Cache-Control", shouldCache ?
                    _cacheControl.ToString() :
                    CacheControlNotCached);
                var headersAffectingJavaScript = HeadersAffectingJavaScript;
                if (string.IsNullOrEmpty(headersAffectingJavaScript.ToString()) == false)
                {
                    context.Response.SetHeader("Vary", headersAffectingJavaScript);
                }
                if (shouldCache && !string.IsNullOrEmpty(eTag))
                {
                    context.Response.SetHeader("ETag", eTag);
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, Messages.MessageJavaScriptCachingError);
            }
            SetAllowOriginHeader(context);
        }

        /// <summary>
        /// Set the Access-Control-Allow-Origin header when the request has
        /// an Origin, so a script on another origin can read the response.
        /// </summary>
        private void SetAllowOriginHeader(IContextAdapter context)
        {
            try
            {
                var origin = GetAllowOrigin(context.Request);
                if (origin != null)
                {
                    context.Response.SetHeader("Access-Control-Allow-Origin", origin);
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, Messages.MessageJavaScriptCachingError);
            }
        }

        /// <summary>
        /// Returns the value for the Access-Control-Allow-Origin response header by inspecting the Origin header
        /// of the request. See https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Origin for details associated
        /// with the Origin header.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        private string GetAllowOrigin(IRequestAdapter request)
        {
            var value = request.GetHeaderValue("Origin");
            if (String.IsNullOrEmpty(value) == false && "null".Equals(value) == false)
            {
                return value;
            }
            return null;
        }
    }
}
