<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="Framework_Web.Default" %>

<%@ Import Namespace="FiftyOne.IpIntelligence" %>
<%@ Import Namespace="FiftyOne.IpIntelligence.Examples" %>
<%@ Import Namespace="FiftyOne.IpIntelligence.Engine.OnPremise.FlowElements" %>
<%@ Import Namespace="FiftyOne.Pipeline.Web.Framework.Providers" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>51Degrees Example</title>

    <link rel="stylesheet" href="Content/examples-main.min.css" />

    <%-- This JavaScript is dynamically generated based on the details of the detected device.
        Including a reference to it allows us to collect client side evidence
        (used for things such as Apple model detection) and access device detection
        results in client side code.
        Note that there doesn't need to be a physical file. The 51Degrees Pipeline will
        intercept the request and serve it automatically.
        --%>
    <script async src='51Degrees.core.js' type='text/javascript'></script>
</head>
<body>
    <div class="c-eg-page">
        <h2 class="c-eg-page__title">Web integration example</h2>

        <p class="c-eg-page__lead">
            This example demonstrates the use of the Pipeline API to perform IP intelligence within a
            simple ASP.NET web project.
        </p>

        <noscript>
            <div class="c-eg-alert">
                WARNING: JavaScript is disabled in your browser.
            </div>
        </noscript>

        <div id="content">
            <h3 class="c-eg-page__heading">Detection results</h3>
            <p class="c-eg-page__lead">
                The following values are determined by server-side IP intelligence
                on the first request.
            </p>
            <table class="c-eg-table">
                <thead class="c-eg-table__head">
                    <tr class="c-eg-table__row">
                        <th class="c-eg-table__cell">Key</th>
                        <th class="c-eg-table__cell">Value</th>
                    </tr>
                </thead>
                <tbody>
                <%
                    // Put the flow data and device data instances into local variables so we don't
                    // have to keep grabbing them.
                    var flowData = ((PipelineCapabilities)Request.Browser).FlowData;
                    var deviceData = flowData.Get<IIpIntelligenceData>();
                    // Get the engine that is used to perform IP intelligence.
                    var engine = flowData.Pipeline.GetElement<IpiOnPremiseEngine>();
                    // Note that below we are using some helper methods from the
                    // FiftyOne.IpIntelligence.Examples project (TryGetValue and GetHumanReadable)
                    // These are mostly intended to handle scenarios where IP intelligence does
                    // not have an answer.
                    // In a production scenario, you will probably want to handle these scenarios
                    // differently. Feel free to copy these helpers if they are useful though.
                %>
                <tr class="c-eg-table__row c-eg-table__row--alt"><td class="c-eg-table__cell c-eg-table__cell--key">Registered Name:</td><td class="c-eg-table__cell"> <%= deviceData.TryGetValue(d => d.RegisteredName.GetHumanReadable()) %></td></tr>
                </tbody>
            </table>

            <div id="evidence">
                <h3 class="c-eg-page__heading">Evidence used</h3>
                <p class="c-eg-legend">
                    Evidence was
                    <span class="c-eg-legend__swatch c-eg-legend__swatch--used">used</span>
                    /
                    <span class="c-eg-legend__swatch c-eg-legend__swatch--present">present</span>
                    for detection
                </p>
                <table class="c-eg-table">
                    <thead class="c-eg-table__head">
                        <tr class="c-eg-table__row">
                            <th class="c-eg-table__cell">Key</th>
                            <th class="c-eg-table__cell">Value</th>
                        </tr>
                    </thead>
                    <tbody>
                    <% foreach (var evidence in flowData.GetEvidence().AsDictionary()) { %>
                        <tr class="c-eg-table__row <%= engine.EvidenceKeyFilter.Include(evidence.Key) ? "c-eg-table__row--used" : "c-eg-table__row--present" %>">
                            <td class="c-eg-table__cell c-eg-table__cell--key"><%= evidence.Key %></td>
                            <td class="c-eg-table__cell"><%= evidence.Value %></td>
                        </tr>
                    <% } %>
                    </tbody>
                </table>
            </div>

            <div id="response-headers">
                <h3 class="c-eg-page__heading">Response headers</h3>
                <table class="c-eg-table">
                    <thead class="c-eg-table__head">
                        <tr class="c-eg-table__row">
                            <th class="c-eg-table__cell">Key</th>
                            <th class="c-eg-table__cell">Value</th>
                        </tr>
                    </thead>
                    <tbody>
                    <% foreach (var key in Response.Headers.AllKeys) { %>
                        <tr class="c-eg-table__row c-eg-table__row--present">
                            <td class="c-eg-table__cell c-eg-table__cell--key"><%= key %></td>
                            <td class="c-eg-table__cell"><%= string.Join(", ", Response.Headers.GetValues(key)) %></td>
                        </tr>
                    <% } %>
                    </tbody>
                </table>
            </div>
        </div>

        <% if (engine.DataSourceTier == "Lite") { %>
            <div class="c-eg-message">
              <p class="c-eg-message__text">Need more on-premise properties and features? <a href="https://51degrees.com/contact-us?utm_source=code&amp;utm_medium=example&amp;utm_campaign=ip-intelligence-dotnet-examples&amp;utm_content=examples-onpremise-framework-web-default.aspx&amp;utm_term=on-premise-properties">Contact us</a> to explore the options.</p>
              <a class="b-btn c-eg-message__cta" href="https://51degrees.com/contact-us?utm_source=code&amp;utm_medium=example&amp;utm_campaign=ip-intelligence-dotnet-examples&amp;utm_content=examples-onpremise-framework-web-default.aspx&amp;utm_term=on-premise-properties">Contact us</a>
            </div>
        <% } %>
    </div>

</body>
</html>
