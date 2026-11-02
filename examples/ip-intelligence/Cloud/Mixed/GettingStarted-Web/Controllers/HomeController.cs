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

using Microsoft.AspNetCore.Mvc;
using FiftyOne.Pipeline.Web.Services;
using FiftyOne.IpIntelligence.Examples.Mixed.Cloud.GettingStartedWeb.Model;
using Microsoft.Extensions.Logging;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.DeviceDetection;
using FiftyOne.IpIntelligence.Translation.Data;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using System.Text;

namespace FiftyOne.IpIntelligence.Examples.Mixed.Cloud.GettingStartedWeb.Controllers
{
    public class HomeController : Controller
    {
        private IFlowDataProvider _provider;
        private ILogger<HomeController> _logger;
        private IPipeline _pipeline;

        // The controller has a dependency on IFlowDataProvider. This is used to access the 
        // IFlowData that contains both Device Detection and IP Intelligence results 
        // for the current HTTP request.
        public HomeController(IFlowDataProvider provider, ILogger<HomeController> logger, IPipeline pipeline)
        {
            _provider = provider;
            _logger = logger;
            _pipeline = pipeline;
        }

        public IActionResult Index()
        {
            // Get the flow data from the provider. This contains detection results
            // based on the evidence from the current request (User-Agent, client hints, etc.)
            // The 51Degrees middleware automatically adds query parameters with "query." prefix
            var flowData = _provider.GetFlowData();
            
            // Create the model to pass to the view
            var model = new IndexModel();
            
            // Get Device Detection results from the flow data
            model.Device = flowData.Get<IDeviceData>();
            
            // Get IP Intelligence results from the flow data
            model.IpData = flowData.Get<IIpIntelligenceData>();

            // Get the translated country lists from the cloud countries
            // translation engine. These power the country dropdown in the view.
            model.Countries = flowData.Get<ICountriesTranslationData>();

            // Check if a custom IP was provided via query parameter
            var clientIp = Request.Query["client-ip"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(clientIp))
            {
                model.UserInputIp = clientIp;
                model.IpMessage = $"Showing location data for: {clientIp}";
            }
            else
            {
                // Use the visitor's own IP address
                var visitorIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                model.UserInputIp = visitorIp;
                model.IpMessage = "Showing location data for your IP address";
            }
            
            // Note: JavaScript for client-side evidence gathering is handled 
            // by the FiftyOneJS view component in the view
            
            // Build device message. Each property is read defensively because a
            // resource key may not grant every property - accessing one that is
            // not in the key throws PropertyMissingException.
            if (model.Device != null)
            {
                var deviceType = SafeGet(() => model.Device.DeviceType.HasValue ? model.Device.DeviceType.Value : "Unknown");
                var browserName = SafeGet(() => model.Device.BrowserName.HasValue ? model.Device.BrowserName.Value : "Unknown");
                var platformName = SafeGet(() => model.Device.PlatformName.HasValue ? model.Device.PlatformName.Value : "Unknown");

                model.DeviceMessage = $"You are using {browserName} on {platformName} ({deviceType})";
            }
            
            // Collect evidence for display
            model.Evidence = new List<EvidenceModel>();
            foreach (var evidence in flowData.GetEvidence().AsDictionary())
            {
                model.Evidence.Add(new EvidenceModel
                {
                    Key = evidence.Key,
                    Value = evidence.Value?.ToString() ?? "null"
                });
            }
            
            // Set response headers for client hints
            if (Response != null && Response.Headers != null)
            {
                SetResponseHeaders(flowData, Response.Headers);
            }
            
            return View(model);
        }
        
        /// <summary>
        /// Evaluate a property accessor, returning "Unknown" if the property is
        /// not included in the resource key (which throws
        /// PropertyMissingException) or otherwise cannot be read.
        /// </summary>
        private static string SafeGet(System.Func<string> get)
        {
            try { return get(); }
            catch { return "Unknown"; }
        }

        private void SetResponseHeaders(IFlowData flowData, IHeaderDictionary headers)
        {
            // Add Accept-CH header for client hints
            var acceptCH = new StringBuilder();
            
            // Check for device detection properties that require client hints
            var device = flowData.Get<IDeviceData>();
            if (device != null)
            {
                // Add standard client hints
                acceptCH.Append("Sec-CH-UA-Model,Sec-CH-UA-Platform,Sec-CH-UA-Platform-Version,");
                acceptCH.Append("Sec-CH-UA-Mobile,Sec-CH-UA-Full-Version-List,Sec-CH-UA-Arch");
                
                headers["Accept-CH"] = acceptCH.ToString().TrimEnd(',');
            }
        }
    }
}