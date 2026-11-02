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
using System.Web.UI;

/// @example OnPremise/Framework-Web/Default.aspx.cs
/// 
/// This example shows how to use 51Degrees On-premise IP Intelligence to determine location and network details from IP addresses in a .NET Framework web application.
/// 
/// You will learn:
/// 
/// 1. How to configure a Pipeline that uses 51Degrees On-premise IP Intelligence in a .NET Framework website
/// 2. How the PipelineModule automatically processes requests and makes results available  
/// 3. How to retrieve IP Intelligence results in your web pages
/// 
/// The source code for this example is available in full on [GitHub](https://github.com/51Degrees/ip-intelligence-dotnet-examples/tree/master/Examples/OnPremise/Framework-Web).
/// 
/// This example requires an enterprise IP Intelligence data file (.ipi). 
/// To obtain an enterprise data file for testing, please [contact us](https://51degrees.com/contact-us?utm_source=code&amp;utm_medium=example&amp;utm_campaign=ip-intelligence-dotnet-examples&amp;utm_content=examples-onpremise-framework-web-default.aspx.cs&amp;utm_term=header).
/// 
/// Required NuGet Dependencies: 
/// - [FiftyOne.IpIntelligence](https://www.nuget.org/packages/FiftyOne.IpIntelligence/)
/// - [FiftyOne.Pipeline.Web](https://www.nuget.org/packages/FiftyOne.Pipeline.Web/)
/// 
/// ## Overview
/// 
/// The FiftyOne.Pipeline.Web package includes an IHttpModule implementation called 
/// 'PipelineModule'. This is inserted into the start of the ASP.NET processing pipeline so
/// that it can intercept the request and perform IP Intelligence.
/// 
/// The module replaces the default `HttpCapabilitiesBase.BrowserCapabilitiesProvider` with a 
/// 51Degrees version.
/// This means that when values are requested (e.g. Request.Browser.IsMobileDevice), we can 
/// intercept that request and supply a result from our IP Intelligence.
/// 
/// If you require access to properties and features that are not available through 
/// `BrowserCapabilitiesProvider`, you will need to cast browser capabilities to our 
/// `PipelineCapabilities` class in order to access the `IFlowData` instance directly:
/// 
/// ```
/// var flowData = ((PipelineCapabilities)Request.Browser).FlowData;
/// var ipiData = flowData.Get<IIpIntelligenceData>();
/// ```
/// 
/// The 'IPipeline' instance that is being used can also be accessed through the static 
/// `WebPipeline` class:
/// 
/// ```
/// var ipiPipeline = WebPipeline.GetInstance();
/// ```
/// 
/// ## Configuration
/// 
/// By default, a 51Degrees.json file is used to supply the Pipeline configuration.
/// For more details about the available options, check the relevant builder classes. 
/// For example, the CloudRequestEngineBuilder. The methods available on the builder are 
/// the same as those that will be available in the configuration file. 
/// 
/// Note that you will need to update the configuration with the complete path to a data file. 
/// The free 'lite' file is included with this repository under the path `ip-intelligence-data`
/// 
/// Alternatively, you can obtain a [license key](https://51degrees.com/pricing?utm_source=code&amp;utm_medium=example&amp;utm_campaign=ip-intelligence-dotnet-examples&amp;utm_content=examples-onpremise-framework-web-default.aspx.cs&amp;utm_term=header) which can be used 
/// to download a data file from our 
/// [Distributor](https://51degrees.com/documentation/_info__distributor.html?utm_source=code&amp;utm_medium=example&amp;utm_campaign=ip-intelligence-dotnet-examples&amp;utm_content=examples-onpremise-framework-web-default.aspx.cs&amp;utm_term=header) service.
/// 
/// @include OnPremise/Framework-Web/App_Data/51Degrees.json
/// 
/// ### Web.config
/// 
/// The 51Degrees API mostly targets .NET Standard. This means you may get an error like:
/// 
/// ```
/// CS0012: The type 'System.Object' is defined in an assembly that is not referenced. You must add 
/// a reference to assembly 'netstandard, Version=2.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'.
/// ```
/// 
/// In this case, you will need to add a section similar to the following to your web.config:
/// 
/// ```
/// <compilation debug="true" targetFramework="4.7.2">
///   <assemblies>
///     <add assembly="netstandard, Version=2.0.0.0, Culture=neutral, 
///           PublicKeyToken=cc7b13ffcd2ddd51"/>
///   </assemblies>
/// </compilation>
/// ```
/// 
/// Another common problem is an error loading a specific version of a library because your
/// project is using a newer version. For example, this message is appearing because our API only 
/// requires version 11 of the Newtonsoft library, but the sample project is using version 13:
/// 
/// ```
/// Could not load file or assembly 'Newtonsoft.Json, Version=11.0.0.0, Culture=neutral, PublicKeyToken=30ad4fe6b2a6aeed' or one of its dependencies. The located assembly's manifest definition does not match the assembly reference. (Exception from HRESULT: 0x80131040)
/// ```
/// 
/// Fixing this requires a binding redirect to tell the runtime to use the installed version, 
/// rather than the requested version.
/// For example, to direct it to use version 13 of the Newtonsoft library regardless of the 
/// requested version, we would add:
/// 
/// ```
/// <runtime>
///   <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
///     <dependentAssembly>
///       <assemblyIdentity name="Newtonsoft.Json" publicKeyToken="30ad4fe6b2a6aeed" />
///       <bindingRedirect oldVersion="0.0.0.0-13.0.0.0" newVersion="13.0.0.0" />
///     </dependentAssembly>
///   </assemblyBinding>
/// </runtime>
/// ```
/// 
/// ## Load Assemblies
/// 
/// Any builders that are specified in configuration must also have their assemblies loaded into
/// the AppDomain. This is handled in Global.asax:
/// 
/// @include OnPremise/Framework-Web/Global.asax.cs
/// 
/// ## Results
/// 
/// This example includes a simple demonstration page that shows how to access different values 
/// from the results.
/// 
/// @include OnPremise/Framework-Web/Default.aspx
namespace Framework_Web
{
    public partial class Default : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {

        }
    }
}