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
using System.Globalization;
using System.IO;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Web.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;

namespace FiftyOne.Pipeline.Web.Services
{
    /// <summary>
    /// A helper service that is used to add evidence from a web request 
    /// to a <see cref="IFlowData"/> instance.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/web-integration.md#populating-evidence">Specification</see>
    /// </summary>
    public class WebRequestEvidenceService : IWebRequestEvidenceService
    {
        /// <summary>
        /// Logger
        /// </summary>
        private ILogger<WebRequestEvidenceService> _logger;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">A logger</param>
        public WebRequestEvidenceService(
            ILogger<WebRequestEvidenceService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// The request's session, or null where session middleware has not
        /// given the request one. The session feature is read rather than
        /// HttpContext.Session, which throws when there is no session, and
        /// it is read for each request, because session middleware can run
        /// for some requests and not for others.
        /// </summary>
        /// <param name="httpRequest">
        /// The request to read the session of.
        /// </param>
        /// <returns>
        /// The session, or null.
        /// </returns>
        private static ISession GetSession(HttpRequest httpRequest) =>
            httpRequest.HttpContext?.Features?.Get<ISessionFeature>()?.Session;

        /// <summary>
        /// Use the specified <see cref="HttpRequest"/> to populated the 
        /// <see cref="IFlowData"/> with evidence.
        /// </summary>
        /// <param name="flowData">
        /// The <see cref="IFlowData"/> to populate.
        /// </param>
        /// <param name="httpRequest">
        /// The <see cref="HttpRequest"/> to pull values from.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if a required parameter is null.
        /// </exception>
        public void AddEvidenceFromRequest(IFlowData flowData, HttpRequest httpRequest)
        {
            if (httpRequest == null) throw new ArgumentNullException(nameof(httpRequest));
            if (flowData == null) throw new ArgumentNullException(nameof(flowData));

            AddEvidence(httpRequest, new Target(flowData));

            // The usage sharing element accepts no evidence and reads the
            // whole request from here instead, so this is added whatever
            // the pipeline accepts.
            flowData.AddEvidence(
                Core.Constants.EVIDENCE_HTTPREQUEST_KEY,
                new RequestView(this, httpRequest));
        }

        /// <summary>
        /// Add the values of the request to the target, being the evidence
        /// the pipeline accepts or every value for a
        /// <see cref="RequestView"/>.
        /// </summary>
        /// <param name="httpRequest">
        /// The <see cref="HttpRequest"/> to pull values from.
        /// </param>
        /// <param name="target">
        /// Where the values go.
        /// </param>
        private void AddEvidence(HttpRequest httpRequest, Target target)
        {
            try
            {
                foreach (var header in httpRequest.Headers)
                {
                    string evidenceKey = Core.Constants.EVIDENCE_HTTPHEADER_PREFIX +
                        Core.Constants.EVIDENCE_SEPERATOR + header.Key;
                    target.CheckAndAdd(evidenceKey, header.Value.ToString());
                }
                foreach (var cookie in httpRequest.Cookies)
                {
                    string evidenceKey = Core.Constants.EVIDENCE_COOKIE_PREFIX +
                        Core.Constants.EVIDENCE_SEPERATOR + cookie.Key;
                    target.CheckAndAdd(
                        evidenceKey,
                        cookie.Value == null ? "" :
                            cookie.Value.ToString(CultureInfo.InvariantCulture));
                }
                foreach (var queryValue in httpRequest.Query)
                {
                    string evidenceKey = Core.Constants.EVIDENCE_QUERY_PREFIX +
                        Core.Constants.EVIDENCE_SEPERATOR + queryValue.Key;
                    target.CheckAndAdd(evidenceKey, queryValue.Value.ToString());
                }
                // Add form parameters to the evidence.
                if (httpRequest.Method == Shared.Constants.METHOD_POST &&
                    FormContentType.IsForm(httpRequest.ContentType))
                {
                    try
                    {
                        foreach (var formValue in httpRequest.Form)
                        {
                            string evidenceKey = Core.Constants.EVIDENCE_QUERY_PREFIX +
                                Core.Constants.EVIDENCE_SEPERATOR + formValue.Key;
                            target.CheckAndAdd(evidenceKey, formValue.Value.ToString());
                        }
                    }
                    catch (Exception e) when (e is InvalidDataException || e is IOException)
                    {
                        // InvalidDataException - malformed form payload
                        //   (existing case).
                        // IOException        - client disconnected mid-body
                        //   or Kestrel aborted a very slow upload via the
                        //   MinRequestBodyDataRate guard. Kestrel surfaces
                        //   both as BadHttpRequestException, which inherits
                        //   from IOException, so we can catch it without
                        //   taking a Microsoft.AspNetCore.Server.Kestrel.Core
                        //   dependency (this library stays server-agnostic).
                        // Both are benign client-side conditions, not
                        // server-side faults, so they are logged at
                        // Information rather than escaping to the outer
                        // catch where they would be logged at Warning and
                        // surface as ExceptionTelemetry in App Insights.
                        // See https://github.com/51Degrees/pipeline-dotnet/issues/298
                        _logger.LogInformation(e,
                            Messages.MessageInvalidForm);
                    }
                }
                var session = GetSession(httpRequest);
                if (session != null)
                {
                    foreach (var sessionKey in session.Keys)
                    {
                        string evidenceKey = Core.Constants.EVIDENCE_SESSION_PREFIX +
                            Core.Constants.EVIDENCE_SEPERATOR + sessionKey;
                        target.CheckAndAdd(evidenceKey, session.GetString(sessionKey));
                    }
                    target.CheckAndAdd(Core.Constants.EVIDENCE_SESSION_KEY,
                        new AspCoreSession(session));
                }

                if (httpRequest.HttpContext.Connection?.RemoteIpAddress != null)
                {
                    target.CheckAndAdd(Core.Constants.EVIDENCE_CLIENTIP_KEY,
                        httpRequest.HttpContext.Connection.RemoteIpAddress.ToString());
                }

                AddRequestProtocolToEvidence(target, httpRequest);
                AddRequestLineToEvidence(target, httpRequest);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                _logger.LogWarning(ex, Messages.MessageEvidenceError);
            }
        }

        /// <summary>
        /// Where the values of a request go, being the evidence of a flow
        /// data for the keys its pipeline accepts, or every value into the
        /// dictionary of a <see cref="RequestView"/>.
        /// </summary>
        private readonly struct Target
        {
            private readonly IFlowData _flowData;
            private readonly IDictionary<string, object> _values;

            public Target(IFlowData flowData)
            {
                _flowData = flowData;
                _values = null;
            }

            public Target(IDictionary<string, object> values)
            {
                _flowData = null;
                _values = values;
            }

            /// <summary>
            /// True if a value for the key would be kept.
            /// </summary>
            public bool Wants(string key) =>
                _values != null || _flowData.EvidenceKeyFilter.Include(key);

            /// <summary>
            /// Keep the value if <see cref="Wants"/> the key.
            /// </summary>
            public void CheckAndAdd(string key, object value)
            {
                if (_values != null)
                {
                    _values[key] = value;
                }
                else if (_flowData.EvidenceKeyFilter.Include(key))
                {
                    _flowData.AddEvidence(key, value);
                }
            }
        }

        /// <summary>
        /// The request as evidence for the usage sharing element, read from
        /// the request the first time it is used.
        /// </summary>
        private sealed class RequestView : RequestEvidenceView
        {
            private readonly WebRequestEvidenceService _service;
            private readonly HttpRequest _request;

            public RequestView(
                WebRequestEvidenceService service,
                HttpRequest request)
            {
                _service = service;
                _request = request;
            }

            protected override void AddValues(
                IDictionary<string, object> values) =>
                _service.AddEvidence(_request, new Target(values));
        }

        /// <summary>
        /// Get the request protocol using .NET's Request object
        /// 'isHttps'. Fall back to non-standard headers.
        /// </summary>
        private static void AddRequestProtocolToEvidence(
            Target target,
            HttpRequest request)
        {
            string protocol = "https";
            if (request.IsHttps)
            {
                protocol = "https";
            }
            else if (request.Headers.ContainsKey("X-Origin-Proto"))
            {
                protocol = request.Headers["X-Origin-Proto"];
            }
            else if (request.Headers.ContainsKey("X-Forwarded-Proto"))
            {
                protocol = request.Headers["X-Forwarded-Proto"];
            }
            else
            {
                protocol = "http";
            }

            // Add protocol to the evidence.
            target.CheckAndAdd(Core.Constants.EVIDENCE_PROTOCOL, protocol);
        }

        /// <summary>
        /// Add the method, the path and the whole query string of the
        /// request to the evidence, exactly as the request carried them.
        /// </summary>
        /// <remarks>
        /// These are only added where an element in the pipeline has asked
        /// for them, and nothing is worked out at all where no element has,
        /// because this runs on every request.
        /// <para>
        /// The path and the query come from the raw request target rather
        /// than from 'Path' and 'QueryString'. The framework hands those
        /// two back with percent escapes decoded, so a request for
        /// '/a%2Fb' would arrive here as '/a/b', and these values are used
        /// to rebuild the text an HTTP message signature was made over,
        /// where one changed byte makes a valid signature read as invalid.
        /// The raw target is what the request line carried. Where it
        /// cannot be read, or where it is not in the ordinary form that
        /// starts with a slash, being the absolute form a proxy may send
        /// or the asterisk form of an OPTIONS request, the escaped forms
        /// of 'PathBase', 'Path' and 'QueryString' are used instead, which
        /// are as close to the original as the framework can give. The
        /// base is part of that reading because 'UsePathBase' takes it off
        /// 'Path' before the application sees it, whilst the request line
        /// carried it.
        /// </para>
        /// <para>
        /// All three are added together, with an empty query string where
        /// the request carried none, so that a missing key always means
        /// this integration did not supply the request line.
        /// </para>
        /// </remarks>
        private static void AddRequestLineToEvidence(
            Target target,
            HttpRequest httpRequest)
        {
            target.CheckAndAdd(
                Core.Constants.EVIDENCE_REQUEST_METHOD_KEY,
                httpRequest.Method ?? string.Empty);

            // The path and the query are split out of the request target
            // together, so the one reading serves both and is only done
            // where at least one of them is wanted.
            if (target.Wants(Core.Constants.EVIDENCE_REQUEST_PATH_KEY) ||
                target.Wants(Core.Constants.EVIDENCE_REQUEST_QUERY_KEY))
            {
                SplitRequestTarget(httpRequest, out var path, out var query);
                target.CheckAndAdd(
                    Core.Constants.EVIDENCE_REQUEST_PATH_KEY, path);
                target.CheckAndAdd(
                    Core.Constants.EVIDENCE_REQUEST_QUERY_KEY, query);
            }
        }

        /// <summary>
        /// Read the path and the query string as the request line carried
        /// them, without the leading question mark on the query.
        /// </summary>
        /// <param name="httpRequest">The request.</param>
        /// <param name="path">The path.</param>
        /// <param name="query">The query string.</param>
        private static void SplitRequestTarget(
            HttpRequest httpRequest,
            out string path,
            out string query)
        {
            var rawTarget = httpRequest.HttpContext?.Features?
                .Get<IHttpRequestFeature>()?.RawTarget;
            if (string.IsNullOrEmpty(rawTarget) == false &&
                rawTarget[0] == '/')
            {
                var mark = rawTarget.IndexOf('?');
                path = mark < 0 ? rawTarget : rawTarget.Substring(0, mark);
                query = mark < 0
                    ? string.Empty
                    : rawTarget.Substring(mark + 1);
                return;
            }
            // 'ToUriComponent' gives the escaped form, where 'Value' gives
            // the decoded one, so it is the closer of the two to what the
            // request line carried.
            //
            // 'PathBase' carries the part of the path the application is
            // mounted under, which 'UsePathBase' takes off 'Path' before
            // the application sees it. The request line carried the two
            // together, so the two are put back together here. Leaving the
            // base out would build a path shorter than the one that was
            // signed, and a signature covering '@path' or '@target-uri'
            // would then read as a mismatch, which says the agent was
            // lying, rather than as a value this integration could not
            // supply. 'ToUriComponent' gives an empty string where there
            // is no value, so an application mounted at the root is
            // unaffected.
            path = httpRequest.PathBase.ToUriComponent() +
                httpRequest.Path.ToUriComponent();
            query = httpRequest.QueryString.HasValue
                ? httpRequest.QueryString.ToUriComponent()
                : string.Empty;
            if (query.Length > 0 && query[0] == '?')
            {
                query = query.Substring(1);
            }
        }
    }
}
