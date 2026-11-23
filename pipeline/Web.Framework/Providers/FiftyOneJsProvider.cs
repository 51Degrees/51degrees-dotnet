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
using FiftyOne.Pipeline.Web.Framework.Adapters;
using FiftyOne.Pipeline.Web.Shared.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Web;

namespace FiftyOne.Pipeline.Web.Framework.Providers
{
    /// <summary>
    /// The provider that serves the JavaScript when requested.
    /// </summary>
    internal class FiftyOneJsProvider
    {
        private enum ContentType
        {
            JavaScript,
            Json
        }

        /// <summary>
        /// The single instance of the provider.
        /// </summary>
        private static FiftyOneJsProvider _instance = null;

        /// <summary>
        /// Lock used when constructing the instance.
        /// </summary>
        private static readonly object _lock = new object();

        private static IClientsidePropertyService _clientsidePropertyService;

        /// <summary>
        /// Get the single instance of the provider. If one does not yet
        /// exist, it is constructed.
        /// </summary>
        /// <returns></returns>
        public static FiftyOneJsProvider GetInstance()
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new FiftyOneJsProvider();
                    }
                }
            }
            return _instance;
        }

        /// <summary>
        /// Create a new FiftyOneJsProvider
        /// </summary>
        public FiftyOneJsProvider()
        {
            using (var loggerFactory = new LoggerFactory())
            {
                var logger = loggerFactory.CreateLogger<ClientsidePropertyService>();
                _clientsidePropertyService = new ClientsidePropertyService(
                    WebPipeline.GetInstance().Pipeline, logger);
            }
        }

        /// <summary>
        /// Add the JavaScript from the flow data object to the HttpResponse
        /// </summary>
        /// <param name="context">
        /// The HttpContext containing the HttpResponse to add the 
        /// JavaScript to.
        /// </param>
        public void ServeJavascript(HttpContext context)
        {
            if (ServeNotModified(
                context,
                WebPipeline.GetInstance().Pipeline,
                _clientsidePropertyService))
            {
                return;
            }
            _clientsidePropertyService.ServeJavascript(
                new ContextAdapter(context), 
                GetFlowData(context));
        }

        /// <summary>
        /// Add the JSON from the flow data object to the HttpResponse
        /// </summary>
        /// <param name="context">
        /// The HttpContext containing the HttpResponse to add the 
        /// JSON to.
        /// </param>
        public void ServeJson(HttpContext context)
        {
            if (ServeNotModified(
                context,
                WebPipeline.GetInstance().Pipeline,
                _clientsidePropertyService))
            {
                return;
            }
            _clientsidePropertyService.ServeJson(
                new ContextAdapter(context), 
                GetFlowData(context));
        }

        /// <summary>
        /// The key of the item that marks a request answered by
        /// <see cref="ServeNotModified"/>.
        /// </summary>
        internal const string AnsweredWithoutProcessingKey =
            "FiftyOne.Pipeline.Web.Framework.AnsweredWithoutProcessing";

        /// <summary>
        /// Answers a request whose If-None-Match matches the ETag of the
        /// evidence it supplies with a 304 or a 412, without processing it,
        /// because the web integration specification says the endpoint
        /// "MUST NOT process a request whose 'If-None-Match' header
        /// matches". False, with nothing written, when the request has no
        /// If-None-Match, it does not match, or its evidence cannot be read,
        /// and the request is then processed and served.
        /// </summary>
        internal static bool ServeNotModified(
            HttpContext context,
            IPipeline pipeline,
            IClientsidePropertyService service)
        {
            var clientside = service as ClientsidePropertyService;
            if (clientside == null ||
                string.IsNullOrWhiteSpace(
                    context.Request.Headers["If-None-Match"]))
            {
                return false;
            }
            bool answered;
            using (var flowData = pipeline.CreateFlowData())
            {
                try
                {
                    WebPipeline.AddEvidenceFromRequest(
                        flowData, context.Request);
                }
                catch (Exception)
                {
                    // Processing meets the same failure and reports it.
                    return false;
                }
                answered = clientside.ServeNotModified(
                    new ContextAdapter(context), flowData);
            }
            if (answered)
            {
                context.Items[AnsweredWithoutProcessingKey] = true;
            }
            return answered;
        }

        /// <summary>
        /// True when <see cref="ServeNotModified"/> answered the request, so
        /// the request was not processed and its browser capabilities must
        /// not be read, as reading them processes it.
        /// </summary>
        internal static bool AnsweredWithoutProcessing(HttpContext context) =>
            context.Items.Contains(AnsweredWithoutProcessingKey);

        private static IFlowData GetFlowData(HttpContext context)
        {
            PipelineCapabilities caps = context.Request.Browser as PipelineCapabilities;
            return caps.FlowData;
        }
    }
}
