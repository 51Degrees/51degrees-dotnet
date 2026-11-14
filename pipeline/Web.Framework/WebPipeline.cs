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

using FiftyOne.Pipeline.Core.Configuration;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Core.Services;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using FiftyOne.Pipeline.JavaScriptBuilder.FlowElement;
using FiftyOne.Pipeline.JsonBuilder.FlowElement;
using FiftyOne.Pipeline.Web.Framework.Configuration;
using FiftyOne.Pipeline.Web.Framework.Providers;
using FiftyOne.Pipeline.Web.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.Hosting;

namespace FiftyOne.Pipeline.Web.Framework
{
    /// <summary>
    /// Singleton class which contains an IPipeline build from a configuration
    /// file, and some extra options which are available in a web environment.
    /// </summary>
    public class WebPipeline
    {
        private static WebPipeline _instance = null;

        /// <summary>
        /// Whether or not client-side properties are enabled. If they are,
        /// then a 51Degrees.core.js will be served by the server.
        /// </summary>
        public bool ClientSideEvidenceEnabled => _options.ClientSideEvidenceEnabled;

        /// <summary>
        /// Whether or not set header properties are enabled.
        /// </summary>
        public bool SetHeaderPropertiesEnabled => _options.UseSetHeaderProperties;

        /// <summary>
        /// The path of the request that is answered with the client-side
        /// JavaScript.
        /// </summary>
        public string JavaScriptPath => _options.JavaScriptPath;

        /// <summary>
        /// The path of the request that is answered with the client-side
        /// JSON.
        /// </summary>
        public string JsonPath => _options.JsonPath;

        /// <summary>
        /// Extra pipeline options which only apply to an implementation in a
        /// web server.
        /// </summary>
        private readonly PipelineWebIntegrationOptions _options;

        /// <summary>
        /// Lock used when constructing a pipeline.
        /// </summary>
        private static readonly object _lock = new object();

        /// <summary>
        /// The single IPipeline instance for a web server.
        /// </summary>
        public IPipeline Pipeline { get; private set; }

        /// <summary>
        /// Get the only instance of WebPipeline. If an instance does not
        /// already exist, one is created.
        /// </summary>
        /// <returns>
        /// The single instance of WebPipeline
        /// </returns>
        public static WebPipeline GetInstance()
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new WebPipeline();
                    }
                }
            }
            return _instance;
        }

        /// <summary>
        /// Private constructor used to construct the only instance which will
        /// exist of this class.
        /// </summary>
        private WebPipeline()
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddPipelineConfig()
                .Build();
            _options = new PipelineWebIntegrationOptions();
            var section = config.GetRequiredSection("PipelineOptions");
            section.Bind(_options, (o) => { o.ErrorOnUnknownConfiguration = true; });

            if (_options == null ||
                _options.Elements == null)
            {
                throw new PipelineConfigurationException(Messages.ExceptionNoConfiguration);
            }

            // Add the sequence element.
            var sequenceConfig = _options.Elements.Where(e =>
                e.BuilderName.IndexOf(nameof(SequenceElement),
                    StringComparison.OrdinalIgnoreCase) >= 0);
            if (sequenceConfig.Any() == false)
            {
                if (_options.SequenceElementEnabled)
                {
                    // The sequence element is not included so add it.
                    // Make sure it's added as the first element.
                    _options.Elements.Insert(0, new ElementOptions()
                    {
                        BuilderName = nameof(SequenceElement)
                    });
                }
                else if (ClientSideEvidenceEnabled)
                {
                    // The JSON builder fails without a sequence number.
                    throw new PipelineConfigurationException(
                        Messages.ExceptionNoSequenceElement);
                }
            }

            if (ClientSideEvidenceEnabled)
            {
                // Client-side evidence is enabled so make sure the 
                // JsonBuilderElement and JavaScriptBundlerElement has been 
                // included.
                var jsonConfig = _options.Elements.Where(e =>
                    e.BuilderName.StartsWith(nameof(JsonBuilderElement),
                        StringComparison.OrdinalIgnoreCase));
                var javascriptConfig = _options.Elements.Where(e =>
                    e.BuilderName.StartsWith(nameof(JavaScriptBuilderElement),
                        StringComparison.OrdinalIgnoreCase));

                var jsIndex = javascriptConfig.Any() ?
                    _options.Elements.IndexOf(javascriptConfig.First()) : -1;

                if (jsonConfig.Any() == false)
                {
                    // The json builder is not included so add it.
                    var newElementOptions = new ElementOptions()
                    {
                        BuilderName = nameof(JsonBuilderElement)
                    };
                    if (jsIndex > -1)
                    {
                        // There is already a javascript builder element
                        // so insert the json builder before it.
                        _options.Elements.Insert(jsIndex, newElementOptions);
                    }
                    else
                    {
                        _options.Elements.Add(newElementOptions);
                    }
                }

                if (jsIndex == -1)
                {
                    // The javascript builder is not included so add it.
                    var javaScriptOptions = new ElementOptions()
                    {
                        BuilderName = nameof(JavaScriptBuilderElement)
                    };
                    // The script must call back to the path that is
                    // intercepted.
                    if (JsonPath !=
                        PipelineWebIntegrationOptions.DefaultJsonPath)
                    {
                        javaScriptOptions.BuildParameters["Endpoint"] =
                            JsonPath;
                    }
                    _options.Elements.Add(javaScriptOptions);
                }
            }

            // Add the set headers
            var setHeadersConfig = _options.Elements.Where(e =>
                e.BuilderName.IndexOf(nameof(SetHeadersElement),
                    StringComparison.OrdinalIgnoreCase) >= 0);
            if (setHeadersConfig.Any() == false &&
                _options.SetHeadersElementEnabled)
            {
                // The set headers element is not included, so add it.
                // Make sure it's added as the last element.
                _options.Elements.Add(new ElementOptions()
                {
                    BuilderName = nameof(SetHeadersElement)
                });
            }

            ResolveApplicationRootPaths(_options, HostingEnvironment.MapPath);

            // Set up common services.
            var loggerFactory = new LoggerFactory();
            var httpClient = new System.Net.Http.HttpClient();
            var updateService = new DataUpdateService(
                loggerFactory.CreateLogger<DataUpdateService>(),
                httpClient);
            var services = new FiftyOneServiceProvider();
            // Add data update and missing property services.
            services.AddService(loggerFactory);
            services.AddService(httpClient);
            services.AddService(updateService);
            services.AddService(MissingPropertyServiceOnPremise.Instance);

            Pipeline = new PipelineBuilder(
                loggerFactory,
                services)
                .BuildFromConfiguration(_options);
        }


        /// <summary>
        /// Replace each path parameter that starts with '~/' or '~\' with
        /// the path returned by <paramref name="mapPath"/>. Any other path
        /// is left as it is.
        /// </summary>
        /// <param name="options">
        /// Pipeline options to update in place.
        /// </param>
        /// <param name="mapPath">
        /// Maps a virtual path to a physical one, as
        /// <see cref="HostingEnvironment.MapPath(string)"/> does.
        /// </param>
        internal static void ResolveApplicationRootPaths(
            PipelineOptions options,
            Func<string, string> mapPath)
        {
            ApplicationRootPaths.ResolvePaths(
                options,
                p => mapPath(p.Replace('\\', '/')));
        }

        /// <summary>
        /// Populate a FlowData's evidence from the web request, and process
        /// using the Pipeline.
        /// </summary>
        /// <param name="request">
        /// Request containing the evidence to process
        /// </param>
        /// <returns>
        /// A processed FlowData
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if a required parameter is null.
        /// </exception>
        /// <exception cref="AggregateException">
        /// Thrown if an error that should throw occurred during processing,
        /// unless underlying <see ref="IPipeline.SuppressProcessExceptions"/> is true.
        /// </exception>
        public static IFlowData Process(HttpRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var clientDisconnectedToken = CancellationToken.None;
            try
            {
                clientDisconnectedToken = request.RequestContext.HttpContext.Response.ClientDisconnectedToken;
            }
            catch (PlatformNotSupportedException)
            {
                // Only available on IIS 7.5+ in integrated mode.
            }

            // Create a new FlowData instance.
            var flowData = GetInstance().Pipeline.CreateFlowData(clientDisconnectedToken);

            try
            {
                AddEvidenceFromRequest(flowData, request);

                // Process the evidence and return the result
                flowData.Process();

                if (GetInstance().SetHeaderPropertiesEnabled &&
                    request.RequestContext.HttpContext.ApplicationInstance != null)
                {
                    // Set HTTP headers in the response.
                    SetHeadersProvider.GetInstance().SetHeaders(flowData,
                        request.RequestContext.HttpContext.ApplicationInstance.Context);
                }
            } 
            catch (Exception ex)
            {
                var shouldSuppress 
                    // Suppress all ?
                    = GetInstance().Pipeline.SuppressProcessExceptions
                    // thrown by `EvidenceKeyFilter` ?
                    || ex is PipelineTemporarilyUnavailableException
                    // thrown by `Process` ?
                    || (ex is AggregateException 
                    && ex.InnerException is PipelineTemporarilyUnavailableException);
                if (!shouldSuppress)
                {
                    Exception ex2 = null;
                    try
                    {
                        flowData.Dispose();
                    }
                    catch (Exception ex3)
                    {
                        ex2 = ex3;
                    }
                    if (ex2 is null)
                    {
                        if (ex is AggregateException) { throw; }
                        throw new AggregateException(ex);
                    }
                    throw new AggregateException(new Exception[] { ex, ex2 });
                }
            }

            return flowData;
        }

        /// <summary>
        /// Add the evidence the pipeline accepts from the request, then the
        /// request itself for the usage sharing element, which accepts no
        /// evidence and reads the whole request instead, so the request is
        /// added whatever the pipeline accepts.
        /// </summary>
        /// <param name="flowData">The flow data to add the evidence to.</param>
        /// <param name="request">The request to read.</param>
        /// <exception cref="AggregateException">
        /// Thrown if any value could not be added, after every value has
        /// been tried and each error added to the flow data.
        /// </exception>
        /// <remarks>
        /// An error reading the flow data's evidence key filter is added to
        /// the flow data and thrown as it is.
        /// </remarks>
        internal static void AddEvidenceFromRequest(
            IFlowData flowData,
            HttpRequest request)
        {
            EvidenceFiller filler;
            try
            {
                filler = new EvidenceFiller(flowData);
            }
            catch (Exception ex)
            {
                flowData.AddError(ex, null);
                throw;
            }

            Fill(ref filler, request);

            flowData.AddEvidence(
                Core.Constants.EVIDENCE_HTTPREQUEST_KEY,
                new RequestView(request));

            if (filler.Errors is IList<Exception> errors)
            {
                foreach (var error in errors)
                {
                    flowData.AddError(error, null);
                }
                throw new AggregateException(errors);
            }
        }

        /// <summary>
        /// The start of the evidence key of each request header.
        /// </summary>
        private const string HeaderPrefix =
            Core.Constants.EVIDENCE_HTTPHEADER_PREFIX +
            Core.Constants.EVIDENCE_SEPERATOR;

        /// <summary>
        /// The start of the evidence key of each cookie.
        /// </summary>
        private const string CookiePrefix =
            Core.Constants.EVIDENCE_COOKIE_PREFIX +
            Core.Constants.EVIDENCE_SEPERATOR;

        /// <summary>
        /// The start of the evidence key of each query string or form
        /// parameter.
        /// </summary>
        private const string QueryPrefix =
            Core.Constants.EVIDENCE_QUERY_PREFIX +
            Core.Constants.EVIDENCE_SEPERATOR;

        /// <summary>
        /// Add the values of the request to the filler, being the evidence
        /// the pipeline accepts or every value for a
        /// <see cref="RequestView"/>.
        /// </summary>
        /// <param name="filler">Where the values go.</param>
        /// <param name="request">The request to read.</param>
        private static void Fill(ref EvidenceFiller filler, HttpRequest request)
        {
            filler.CheckAndAddAll(HeaderPrefix, request.Headers, (headers, k) => headers[k]);
            filler.CheckAndAddAll(CookiePrefix, request.Cookies, (cookies, k) => cookies[k].Value);
            filler.CheckAndAddParameters(QueryPrefix, request.QueryString);
            if (request.RequestContext.HttpContext.Session is HttpSessionStateBase session)
            {
                filler.AddSessionToEvidence(session);
            }

            // Add form parameters to the evidence.
            if (request.HttpMethod == Shared.Constants.METHOD_POST &&
                FormContentType.IsForm(request.ContentType))
            {
                filler.CheckAndAddParameters(QueryPrefix, request.Form);
            }

            // A request handed over with no client address has no client
            // IP to add.
            var clientIp = request.UserHostAddress;
            if (string.IsNullOrEmpty(clientIp) == false)
            {
                filler.CheckAndAdd(Core.Constants.EVIDENCE_CLIENTIP_KEY, clientIp);
            }
            filler.AddRequestProtocolToEvidence(request);
            filler.AddRequestLineToEvidence(request);
        }

        /// <summary>
        /// The request as evidence for the usage sharing element, read from
        /// the request the first time it is used.
        /// </summary>
        private sealed class RequestView : RequestEvidenceView
        {
            private readonly HttpRequest _request;

            public RequestView(HttpRequest request)
            {
                _request = request;
            }

            protected override void AddValues(
                IDictionary<string, object> values)
            {
                var filler = new EvidenceFiller(values);
                Fill(ref filler, _request);
            }
        }

        /// <summary>
        /// A convenience wrapper around IFlowData.
        /// Reduces amount of calls to `IFlowData.EvidenceKeyFilter`
        /// </summary>
        public struct EvidenceFiller
        {
            private readonly IFlowData _flowData;
            private readonly IEvidenceKeyFilter _evidenceKeyFilter;
            private readonly IDictionary<string, object> _values;
            private IList<Exception> _errors;

            /// <summary>
            /// A collection of exceptions thrown while trying to set data.
            /// </summary>
            public IList<Exception> Errors => _errors;

            /// <param name="flowData">
            /// The <see cref="IFlowData"/> to add the evidence to.
            /// </param>
            /// <exception cref="PipelineException">
            /// thrown if <see cref="IFlowData.EvidenceKeyFilter"/> is null
            /// or re-thrown from <see cref="IFlowData.EvidenceKeyFilter"/> itself
            /// </exception>
            public EvidenceFiller(IFlowData flowData)
            {
                _flowData = flowData;
                _evidenceKeyFilter = flowData.EvidenceKeyFilter;
                if (_evidenceKeyFilter is null)
                {
                    throw new PipelineException($"Failed to retrieve {nameof(flowData.EvidenceKeyFilter)} from {nameof(flowData)}");
                }
                _values = null;
                _errors = null;
            }

            /// <summary>
            /// A filler that keeps every value in the dictionary, for a
            /// <see cref="RequestView"/>.
            /// </summary>
            /// <param name="values">The dictionary to add values to.</param>
            internal EvidenceFiller(IDictionary<string, object> values)
            {
                _flowData = null;
                _evidenceKeyFilter = null;
                _values = values;
                _errors = null;
            }

            /// <summary>
            /// A filler that keeps every value in the dictionary.
            /// </summary>
            /// <param name="values">The dictionary to add values to.</param>
            /// <returns>The filler.</returns>
            public static EvidenceFiller ForValues(
                IDictionary<string, object> values) =>
                new EvidenceFiller(values);

            /// <summary>
            /// True if a value for the key would be kept.
            /// </summary>
            private bool Wants(string key) =>
                _values != null || _evidenceKeyFilter.Include(key);

            /// <summary>
            /// Check if the given key is needed by the given flowdata.
            /// If it is then add it as evidence.
            /// </summary>
            /// <param name="key">
            /// The evidence key
            /// </param>
            /// <param name="value">
            /// The evidence value
            /// </param>
            public void CheckAndAdd(string key, object value)
            {
                try
                {
                    if (_values != null)
                    {
                        _values[key] = value;
                    }
                    else if (_evidenceKeyFilter.Include(key))
                    {
                        _flowData.AddEvidence(key, value);
                    }
                }
                catch (Exception ex)
                {
                    if (_errors is null)
                    {
                        _errors = new List<Exception>();
                    }
                    _errors.Add(ex);
                }
            }

            /// <summary>
            /// Add the session object and each session value, named with
            /// the 'session' prefix so the usage sharing rule for session
            /// values applies to them.
            /// </summary>
            /// <param name="session">The session to read.</param>
            public void AddSessionToEvidence(HttpSessionStateBase session)
            {
                const string prefix =
                    Core.Constants.EVIDENCE_SESSION_PREFIX +
                    Core.Constants.EVIDENCE_SEPERATOR;
                CheckAndAdd(
                    Core.Constants.EVIDENCE_SESSION_KEY,
                    new AspFrameworkSession(session));
                CheckAndAddAll(prefix, session.Keys, (k) => session[k]);
            }

            /// <summary>
            /// Convenience loop wrapper for enumerable of keys
            /// </summary>
            public void CheckAndAddAll(string keyPrefix, System.Collections.IEnumerable keys, Func<string, object> valueGetter)
            {
                foreach (string k in keys)
                {
                    CheckAndAdd(keyPrefix + k, valueGetter(k));
                }
            }

            /// <summary>
            /// Convenience loop wrapper for NameObjectCollectionBase derivatives
            /// </summary>
            public void CheckAndAddAll<T>(string keyPrefix, T nameObjectCollection, Func<T, string, object> valueGetter) where T : NameObjectCollectionBase
                => CheckAndAddAll(keyPrefix, nameObjectCollection.Keys, (k) => valueGetter(nameObjectCollection, k));

            /// <summary>
            /// Add each parameter of a query string or a form. System.Web
            /// holds a parameter written with no '=' under a null name,
            /// with the parameter's text as its value, and each of those is
            /// added as a name with an empty value, as the URL standard
            /// parses it and as ASP.NET Core gives it.
            /// </summary>
            /// <param name="keyPrefix">The start of each evidence key.</param>
            /// <param name="parameters">The parameters to add.</param>
            internal void CheckAndAddParameters(
                string keyPrefix,
                NameValueCollection parameters)
            {
                foreach (string name in parameters.Keys)
                {
                    if (name == null)
                    {
                        foreach (var bare in
                            parameters.GetValues(name) ?? new string[0])
                        {
                            CheckAndAdd(keyPrefix + bare, string.Empty);
                        }
                    }
                    else
                    {
                        CheckAndAdd(keyPrefix + name, parameters[name]);
                    }
                }
            }

            /// <summary>
            /// Get the request protocol using .NET's Request object
            /// 'isHttps'. Fall back to non-standard headers.
            /// </summary>
            public void AddRequestProtocolToEvidence(HttpRequest request)
            {
                string protocol;
                if (request.IsSecureConnection)
                {
                    protocol = "https";
                }
                else if (request.Headers.AllKeys.Contains("X-Origin-Proto"))
                {
                    protocol = request.Headers["X-Origin-Proto"];
                }
                else if (request.Headers.AllKeys.Contains("X-Forwarded-Proto"))
                {
                    protocol = request.Headers["X-Forwarded-Proto"];
                }
                else
                {
                    protocol = "http";
                }

                // Add protocol to the evidence.
                CheckAndAdd(Core.Constants.EVIDENCE_PROTOCOL, protocol);
            }

            /// <summary>
            /// Add the method, the path and the whole query string of the
            /// request to the evidence, exactly as the request carried
            /// them.
            /// </summary>
            /// <remarks>
            /// These are only added where an element in the pipeline has
            /// asked for them, and nothing is worked out at all where no
            /// element has, because this runs on every request.
            /// <para>
            /// The path and the query come from 'RawUrl' rather than from
            /// 'Url'. A 'Uri' canonicalises what it holds, taking out dot
            /// segments and decoding escapes, so a request for '/a%2Fb'
            /// would arrive here as '/a/b', and these values are used to
            /// rebuild the text an HTTP message signature was made over,
            /// where one changed byte makes a valid signature read as
            /// invalid. 'RawUrl' is what the request line carried. Where it
            /// cannot be read, or is not in the ordinary form that starts
            /// with a slash, the canonical form is used instead as the
            /// closest the framework can give.
            /// </para>
            /// <para>
            /// All three are added together, with an empty query string
            /// where the request carried none, so that a missing key always
            /// means this integration did not supply the request line.
            /// </para>
            /// </remarks>
            public void AddRequestLineToEvidence(HttpRequest request)
            {
                if (Wants(Core.Constants.EVIDENCE_REQUEST_METHOD_KEY))
                {
                    CheckAndAdd(
                        Core.Constants.EVIDENCE_REQUEST_METHOD_KEY,
                        request.HttpMethod ?? string.Empty);
                }

                // The path and the query are split out of the request
                // target together, so the one reading serves both and is
                // only done where at least one of them is wanted.
                if (Wants(Core.Constants.EVIDENCE_REQUEST_PATH_KEY) ||
                    Wants(Core.Constants.EVIDENCE_REQUEST_QUERY_KEY))
                {
                    SplitRequestTarget(request, out var path, out var query);

                    if (Wants(Core.Constants.EVIDENCE_REQUEST_PATH_KEY))
                    {
                        CheckAndAdd(
                            Core.Constants.EVIDENCE_REQUEST_PATH_KEY, path);
                    }

                    if (Wants(Core.Constants.EVIDENCE_REQUEST_QUERY_KEY))
                    {
                        CheckAndAdd(
                            Core.Constants.EVIDENCE_REQUEST_QUERY_KEY, query);
                    }
                }
            }

            /// <summary>
            /// Read the path and the query string as the request line
            /// carried them, without the leading question mark on the
            /// query.
            /// </summary>
            /// <param name="request">The request.</param>
            /// <param name="path">The path.</param>
            /// <param name="query">The query string.</param>
            private static void SplitRequestTarget(
                HttpRequest request,
                out string path,
                out string query)
            {
                var rawUrl = request.RawUrl;
                if (string.IsNullOrEmpty(rawUrl) == false &&
                    rawUrl[0] == '/')
                {
                    var mark = rawUrl.IndexOf('?');
                    path = mark < 0 ? rawUrl : rawUrl.Substring(0, mark);
                    query = mark < 0
                        ? string.Empty
                        : rawUrl.Substring(mark + 1);
                    return;
                }
                var url = request.Url;
                path = url == null ? string.Empty : url.AbsolutePath;
                query = url == null ? string.Empty : url.Query;
                if (query.Length > 0 && query[0] == '?')
                {
                    query = query.Substring(1);
                }
            }
        }
    }
}
