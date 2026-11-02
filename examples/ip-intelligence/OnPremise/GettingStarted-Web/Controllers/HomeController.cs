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
using FiftyOne.IpIntelligence.Examples.OnPremise.GettingStartedWeb.Model;
using Microsoft.Extensions.Logging;
using FiftyOne.Pipeline.Core.FlowElements;

namespace FiftyOne.IpIntelligence.Examples.OnPremise.GettingStartedWeb.Controllers
{
    public class HomeController : Controller
    {
        private static bool _checkedDataFile = false;

        private IFlowDataProvider _provider;
        private ILogger<HomeController> _logger;
        private IPipeline _pipeline;

        // The controller has a dependency on IFlowDataProvider. This is used to access the 
        // IFlowData that contains the IP Intelligence results for the current HTTP request.
        public HomeController(IFlowDataProvider provider, ILogger<HomeController> logger, IPipeline pipeline)
        {
            _provider = provider;
            _logger = logger;
            _pipeline = pipeline;
        }

        public IActionResult Index()
        {
            // Log warnings if the data file is too old or the 'Lite' file is being used.
            if(_checkedDataFile == false)
            {
                ExampleUtils.CheckDataFile(_provider.GetFlowData().Pipeline, _logger);
                _checkedDataFile = true;
            }

            // Get the flow data from the provider. This contains IP Intelligence results
            // based on the evidence from the current request, including both query.client-ip 
            // and server.client-ip evidence automatically added by the middleware.
            var flowData = _provider.GetFlowData();
            
            // Check if a custom IP was provided via query parameter
            var clientIp = Request.Query["client-ip"].ToString();
            string targetIp;
            if (!string.IsNullOrWhiteSpace(clientIp))
            {
                targetIp = clientIp;
            }
            else
            {
                // Use the visitor's own IP address
                targetIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            }

            var model = new IndexModel(flowData, Response.Headers);
            model.InputIpAddress = targetIp;
            return View(model);
        }
    }
}
