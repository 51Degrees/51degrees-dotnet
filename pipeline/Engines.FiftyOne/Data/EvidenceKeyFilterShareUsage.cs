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
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.Engines.FiftyOne.Trackers;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Data
{
    /// <summary>
    /// Decides which of the evidence the <see cref="ShareUsageElement"/>
    /// finds is shared.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/d79bbe3b2aef86273570742f6b660f29bfe72816/pipeline-specification/pipeline-elements/usage-sharing-element.md#shared-evidence">Specification</see>
    /// </summary>
    /// <remarks>
    /// This filter is not the element's
    /// <see cref="ShareUsageBase.EvidenceKeyFilter"/>, which accepts no
    /// evidence. It is generally inclusive, so as an element's filter it
    /// would put far more evidence into a pipeline than the elements need,
    /// and every cache key and ETag would depend on it.
    /// </remarks>
    public class EvidenceKeyFilterShareUsage : IEvidenceKeyFilter
    {
        /// <summary>
        /// If true then the asp.net session cookie will be included in
        /// the filter.
        /// </summary>
        /// <remarks>
        /// The session cookie is used by the <see cref="ShareUsageTracker"/> 
        /// but we do not actually want to share it.
        /// </remarks>
        private bool _includeSession;

        /// <summary>
        /// If true then all evidence will always be shared.
        /// </summary>
        private bool _shareAll = false;

        /// <summary>
        /// The cookie name being used to store the asp.net session id.
        /// </summary>
        private string _aspSessionCookieName;

        /// <summary>
        /// The content of HTTP headers in this array will not be included in 
        /// the request information sent to 51degrees.
        /// Any header names added here are hard-coded to be blocked
        /// regardless of the settings passed to the constructor. 'cookie' is
        /// the header that carries every cookie, including the ones this
        /// filter leaves out, and is the only header the specification
        /// blocks by default.
        /// </summary>
        private HashSet<string> _blockedHttpHeaders = new HashSet<string>()
        {
            "cookie"
        };

        /// <summary>
        /// The Pipeline whose Flow Elements decide which query string and
        /// form parameters, besides those starting 51D_, are shared. Null
        /// until the usage sharing element joins a Pipeline, and while it is
        /// null only the parameters starting 51D_ are shared.
        /// </summary>
        private volatile IPipeline _pipeline;

        /// <summary>
        /// Evidence keys that are never shared, regardless of the share-all
        /// setting. They are matched on a whole-segment boundary by
        /// <see cref="Include"/>, so both the bare key and any prefixed form
        /// ('id.email', 'query.id.email', 'header.id.email') are excluded
        /// while an unrelated key such as 'query.valid.email' is not.
        /// </summary>
        /// <remarks>
        /// 'id.email' and 'id.salt' carry caller-supplied secrets, being the
        /// raw email address and the salt used to derive hashed-email
        /// identifiers, which must not leave the server.
        /// <para>
        /// 'request-query' is the whole query string of the request, added
        /// as 'server.request-query' by the web integrations so that a
        /// signature made over the request line can be checked. It is held
        /// back here because it carries every parameter together, including
        /// those the rule for parameters in <see cref="Include"/> withholds,
        /// so sharing it would undo that rule.
        /// </para>
        /// <para>
        /// 'request-path' is held back for a different reason, being what it
        /// does to usage sharing itself rather than what it carries.
        /// <see cref="EvidenceKeyFilterShareUsageTracker"/> derives
        /// from this class, so every key shared is also part of the key the
        /// tracker de-duplicates on. Sharing the path makes each address a
        /// visitor opens look like a different session, so one visitor
        /// moving through thirty pages sends thirty records where the
        /// tracker is meant to send one. The path is also the part of a URL
        /// most likely to carry a name or an identifier a site has put in
        /// it.
        /// </para>
        /// <para>
        /// 'header.cookie' is the Cookie header, which carries every cookie
        /// including the ones the cookie rule leaves out. The cookies
        /// starting 51D_ are shared as their own 'cookie.' keys.
        /// </para>
        /// <para>
        /// 'humanconfidence' is a sealed Human Confidence result and
        /// 'humanconfidencepage' the page id it is sent back with, which
        /// only the server that served the page may use.
        /// </para>
        /// <para>
        /// An element that needs either value still receives it in evidence,
        /// because this filter governs only what usage sharing sends, not
        /// what a pipeline collects.
        /// </para>
        /// </remarks>
        private static readonly string[] _neverSharedEvidenceKeys = new[]
        {
            "id.email",
            "id.salt",
            "request-query",
            "request-path",
            "header.cookie",
            // A sealed Human Confidence result, and the page id it is sent
            // back with, are for the server that served the page alone.
            "humanconfidence",
            "humanconfidencepage",
        };

        /// <summary>
        /// The prefix of every cookie's evidence key, with its separator.
        /// </summary>
        private static readonly string _cookiePrefix =
            Core.Constants.EVIDENCE_COOKIE_PREFIX +
            Core.Constants.EVIDENCE_SEPERATOR;

        /// <summary>
        /// The prefix of every query string and form parameter's evidence
        /// key, with its separator.
        /// </summary>
        private static readonly string _queryPrefix =
            Core.Constants.EVIDENCE_QUERY_PREFIX +
            Core.Constants.EVIDENCE_SEPERATOR;

        /// <summary>
        /// Constructor
        /// Using this constructor will create a filter that allows all
        /// evidence. I.e. All evidence will be shared, except for the
        /// hard-coded exceptions that <see cref="Include"/> never shares
        /// regardless of this setting, such as caller-supplied personal data
        /// and every cookie whose name does not start 51D_.
        /// </summary>
        public EvidenceKeyFilterShareUsage()
        {
            _shareAll = true;
        }

        /// <summary>
        /// Creates a filter that allows all evidence but what
        /// <see cref="Include"/> never shares, for a filter that derives
        /// from this one and keys on the asp.net session cookie.
        /// </summary>
        /// <param name="includeSession">
        /// If true then a filter that derives from this one keys on the
        /// session cookie. This filter does not share it either way.
        /// </param>
        /// <param name="aspSessionCookieName">
        /// The name of the cookie that contains the asp.net session id.
        /// </param>
        protected EvidenceKeyFilterShareUsage(
            bool includeSession,
            string aspSessionCookieName)
        {
            _shareAll = true;
            _includeSession = includeSession;
            _aspSessionCookieName = aspSessionCookieName ?? string.Empty;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="blockedHttpHeaders">
        /// A list of the names of the HTTP headers that share usage should
        /// not send to 51Degrees.
        /// </param>
        /// <param name="includedQueryStringParams">
        /// Deprecated and has no effect. A query string or form parameter
        /// is shared only when its name starts 51D_ or a Flow Element in
        /// the Pipeline accepts it.
        /// </param>
        /// <param name="includeSession">
        /// If true then the repeat tracker's filter, which derives from
        /// this one, keys on the asp.net session cookie. This filter does
        /// not share the cookie either way, because only cookies starting
        /// 51D_ are shared.
        /// </param>
        /// <param name="aspSessionCookieName">
        /// The name of the cookie that contains the asp.net session id.
        /// </param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization",
            "CA1308:Normalize strings to uppercase",
            Justification = "Pipeline specification requires keys to be " +
                "all lower-case")]
        public EvidenceKeyFilterShareUsage(
            List<string> blockedHttpHeaders,
            List<string> includedQueryStringParams,
            bool includeSession,
            string aspSessionCookieName)
        {
            if(blockedHttpHeaders == null)
            {
                throw new ArgumentNullException(nameof(blockedHttpHeaders));
            }
            if (aspSessionCookieName == null)
            {
                throw new ArgumentNullException(nameof(aspSessionCookieName));
            }

            _includeSession = includeSession;
            _aspSessionCookieName = aspSessionCookieName.ToLowerInvariant();
            foreach (var header in blockedHttpHeaders)
            {
                var lowerHeader = header.ToLowerInvariant();
                if (!_blockedHttpHeaders.Contains(lowerHeader))
                {
                    _blockedHttpHeaders.Add(lowerHeader);
                }
            }
        }

        /// <summary>
        /// Sets the Pipeline whose Flow Elements decide which query string
        /// and form parameters, besides those starting 51D_, are shared.
        /// </summary>
        /// <param name="pipeline">
        /// The Pipeline the usage sharing element has joined.
        /// </param>
        internal void UsePipeline(IPipeline pipeline)
        {
            _pipeline = pipeline;
        }

        /// <summary>
        /// True if a query string or form parameter is shared, being when
        /// its name starts 51D_ or a Flow Element in the Pipeline accepts
        /// it, whatever the other settings.
        /// </summary>
        /// <param name="key">A key with the query prefix.</param>
        /// <returns>True if the parameter is shared.</returns>
        private bool IsSharedParameter(string key)
        {
            if (key.Substring(_queryPrefix.Length).StartsWith(
                    Engines.Constants.FIFTYONE_COOKIE_PREFIX,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            var used = _pipeline?.EvidenceKeyFilter;
            return used != null && used.Include(key);
        }

        /// <summary>
        /// True if the key is the asp.net session cookie and the filter
        /// was given the setting to include the session.
        /// </summary>
        /// <param name="key">The evidence key.</param>
        /// <returns>True for the session cookie when it is included.</returns>
        protected bool IsIncludedSessionCookie(string key)
        {
            return _includeSession &&
                key.Equals(
                    Core.Constants.EVIDENCE_COOKIE_PREFIX +
                        Core.Constants.EVIDENCE_SEPERATOR +
                        _aspSessionCookieName,
                    StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Check if the specified evidence key is included by this filter.
        /// </summary>
        /// <param name="key">
        /// The key to check
        /// </param>
        /// <returns>
        /// True if the key is included and false if not.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied key is null
        /// </exception>
        public virtual bool Include(string key)
        {
            if(key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            // The web request itself is never shared, only the values read
            // from it.
            if (ShareUsageEvidence.IsRequestKey(key))
            {
                return false;
            }

            // The keys listed in _neverSharedEvidenceKeys must never leave
            // the server, even when every other piece of evidence is
            // shared. Matched on a whole-segment boundary - the bare key or
            // a separator-prefixed form - so 'query.valid.email' is not
            // mistaken for the 'id.email' secret. Checked before the
            // share-all short-circuit below so it applies in every mode.
            if (Array.Exists(_neverSharedEvidenceKeys, neverShared =>
                key.Equals(neverShared, StringComparison.OrdinalIgnoreCase)
                || key.EndsWith(Core.Constants.EVIDENCE_SEPERATOR + neverShared,
                    StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            // A cookie is shared only when its name starts 51D_, whatever
            // the settings, so a cookie a site sets for itself, such as its
            // session cookie, is never shared.
            if (key.StartsWith(_cookiePrefix, StringComparison.OrdinalIgnoreCase) &&
                key.Substring(_cookiePrefix.Length).StartsWith(
                    Engines.Constants.FIFTYONE_COOKIE_PREFIX,
                    StringComparison.OrdinalIgnoreCase) == false)
            {
                return false;
            }

            // A query string or form parameter follows the same rule, and is
            // also shared when the Pipeline uses it, whatever the settings.
            if (key.StartsWith(_queryPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return IsSharedParameter(key);
            }

            bool result = _shareAll;

            if (_shareAll == false)
            {
                int firstSeperator = key.IndexOf(Core.Constants.EVIDENCE_SEPERATOR,
                    StringComparison.OrdinalIgnoreCase);
                if (firstSeperator > 0)
                {
                    string firstPart = key.Remove(firstSeperator);
                    string lastPart = key.Substring(firstSeperator + 1);

                    if (string.Equals(firstPart, Core.Constants.EVIDENCE_HTTPHEADER_PREFIX,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        // Add the header to the list if the header name does not 
                        // appear in the list of blocked headers.
                        result = _blockedHttpHeaders
#pragma warning disable CA1308 // Normalize strings to uppercase
                            // Pipeline specification is for keys to be lower-case.
                            .Contains(lastPart.ToLowerInvariant()) == false;
#pragma warning restore CA1308 // Normalize strings to uppercase
                    }
                    else if (string.Equals(firstPart, Core.Constants.EVIDENCE_SESSION_PREFIX,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        // Only add session values that start with the 51Degrees
                        // cookie prefix.
                        result = lastPart.StartsWith(Engines.Constants.FIFTYONE_COOKIE_PREFIX,
                            StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        // Add anything that is not a header or session
                        // variable. A cookie that gets here starts 51D_.
                        result = true;
                    }
                }
                else
                {
                    result = true;
                }
            }

            return result;
        }

        /// <summary>
        /// Return the 'order of precedence of the specified evidence
        /// key.
        /// Lower values indicate evidence where the value is more likely
        /// to provide differentiation between requests. This is used by 
        /// features such as Caching to improve performance when checking
        /// if one set of evidence matches another.
        /// </summary>
        /// <param name="key">
        /// The key to get the order for.
        /// </param>
        /// <returns>
        /// The order, where lower values indicate a higher order of 
        /// precedence. 
        /// </returns>
        public int? Order(string key)
        {
            return 100;
        }
    }
}
