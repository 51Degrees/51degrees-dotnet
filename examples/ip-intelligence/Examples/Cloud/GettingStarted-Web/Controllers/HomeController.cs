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
using FiftyOne.IpIntelligence.Examples.Cloud.GettingStartedWeb.Model;
using Microsoft.Extensions.Logging;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Core.Data;

/// @example Cloud/GettingStarted-Web/Controllers/HomeController.cs
/// 
/// This example shows how to integrate 51Degrees Cloud IP Intelligence service
/// into an ASP.NET Core web application. The controller demonstrates processing 
/// visitor IP addresses to retrieve location and ISP information.
/// 
/// This example is available in full on [GitHub](https://github.com/51Degrees/ip-intelligence-dotnet-examples/blob/main/Examples/Cloud/GettingStarted-Web/Controllers/HomeController.cs). 
/// 
/// To run this example, you will eventually need to create a Resource Key, but for now you should use the GettingStarted-API example - just run it and point this example to its custom endpoint to simulate a custom hosted Cloud service. Resource Key is used as shorthand to store the particular set of properties you are interested in as well as any associated License Keys that entitle you to increased request limits and/or paid-for properties, but it is not yet available for IP Intelligence.
///
/// Required NuGet Dependencies:
/// - [FiftyOne.IpIntelligence](https://www.nuget.org/packages/FiftyOne.IpIntelligence/)
/// - [FiftyOne.Pipeline.Web](https://www.nuget.org/packages/FiftyOne.Pipeline.Web/)
/// - [Microsoft.AspNetCore.App](https://www.nuget.org/packages/Microsoft.AspNetCore.App/)
namespace FiftyOne.IpIntelligence.Examples.Cloud.GettingStartedWeb.Controllers
{
    public class HomeController : Controller
    {
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

        public IActionResult Index(string ipAddress = null)
        {
            // Get the visitor's IP address from the request
            var visitorIp = HttpContext.Connection.RemoteIpAddress?.ToString();

            // If no custom IP provided, use the visitor's IP
            var targetIp = !string.IsNullOrWhiteSpace(ipAddress) ? ipAddress : visitorIp;

            IFlowData flowData;

            if (!string.IsNullOrWhiteSpace(targetIp))
            {
                // Create new flow data with specified IP address (custom or visitor's)
                using (flowData = _pipeline.CreateFlowData())
                {
                    flowData.AddEvidence("server.client-ip", targetIp);
                    flowData.Process();

                    var model = new IndexModel(flowData, Response.Headers);
                    model.InputIpAddress = targetIp;
                    return View(model);
                }
            }
            else
            {
                // Fallback to default flow data if we can't determine IP
                flowData = _provider.GetFlowData();
                var model = new IndexModel(flowData, Response.Headers);
                model.InputIpAddress = visitorIp;
                return View(model);
            }
        }
    }
}
