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

using FiftyOne.Pipeline.CloudRequestEngine.Data;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines;
using FiftyOne.Pipeline.Engines.Data;

namespace Examples.HumanConfidence.HumanConfidenceWeb;

/// <summary>
/// The 51Degrees Pipeline this demo runs, and the one place it reads Human
/// Confidence. The Pipeline asks the cloud and redeems a sealed result
/// itself, so this demo only reads the two Properties, HumanConfidence and
/// HumanConfidenceSource, and never works with a sealed result.
/// </summary>
public static class HumanConfidencePipeline
{
    /// <summary>
    /// The Pipeline's configuration, as a web integration reads it under
    /// PipelineOptions. The Cloud Request Engine asks the cloud with the
    /// Resource Key, and with the License Key where there is one, which
    /// stays on this server. The Derived Cloud Engine gives the two
    /// Properties, and the Browser Derived Cloud Engine gives the browser
    /// tests to the client script the Pipeline serves.
    /// </summary>
    /// <param name="resource">The Resource Key.</param>
    /// <param name="license">The License Key, or null.</param>
    /// <param name="api">The cloud's API base, ending /api/v4/.</param>
    public static Dictionary<string, string?> Configuration(
        string resource, string? license, string api)
    {
        var settings = new Dictionary<string, string?>
        {
            ["PipelineOptions:Elements:0:BuilderName"] = "CloudRequestEngine",
            ["PipelineOptions:Elements:0:BuildParameters:ResourceKey"] = resource,
            ["PipelineOptions:Elements:0:BuildParameters:EndPoint"] = api,
            ["PipelineOptions:Elements:1:BuilderName"] = "DerivedCloudEngine",
            ["PipelineOptions:Elements:2:BuilderName"] = "BrowserDerivedCloudEngine",
            ["PipelineOptions:ClientSideEvidenceEnabled"] = "true",
            // A slow or failed call to the cloud gives the page a message,
            // not an error page.
            ["PipelineOptions:BuildParameters:SuppressProcessExceptions"] = "true",
        };
        if (string.IsNullOrWhiteSpace(license) == false)
        {
            settings["PipelineOptions:Elements:0:BuildParameters:LicenseKey"] = license;
        }
        return settings;
    }

    /// <summary>
    /// Whether the key carries HumanConfidence, from the Derived Cloud
    /// Engine's property list, which the Pipeline fetched from the cloud's
    /// accessibleproperties when it was built. "yes" or "no", or "unknown"
    /// when the cloud couldn't be asked.
    /// </summary>
    public static string CarriesHumanConfidence(IPipeline pipeline)
    {
        try
        {
            var engine = pipeline.GetElement<DerivedCloudEngine>();
            return engine != null && engine.Properties.Any(p =>
                p.Name.Equals("HumanConfidence", StringComparison.OrdinalIgnoreCase))
                ? "yes" : "no";
        }
        catch (PipelineException)
        {
            return "unknown";
        }
    }

    /// <summary>
    /// The two Properties for the request the Flow Data was made for. Where
    /// the request carried a sealed result, the Pipeline has redeemed it.
    /// A Property with no value gives the reason in its place.
    /// </summary>
    public static HumanConfidenceAnswer Read(IFlowData data)
    {
        var derived = data.Get<IDerivedCloudData>();
        var (source, sourceReason) = Read(() => derived.HumanConfidenceSource);
        var (level, levelReason) = Read(() => derived.HumanConfidence);
        return new HumanConfidenceAnswer(source, level, sourceReason ?? levelReason);
    }

    private static (string? Value, string? Reason) Read(
        Func<IAspectPropertyValue<string>> read)
    {
        try
        {
            var value = read();
            return value.HasValue
                ? (value.Value, null)
                : (null, value.NoValueMessage);
        }
        catch (PropertyMissingException ex)
        {
            return (null, ex.Message);
        }
    }
}

/// <summary>
/// What the Pipeline gave for one request: the source and the level, and
/// why one of them has no value where that's so.
/// </summary>
public sealed record HumanConfidenceAnswer(
    string? Source, string? HumanConfidence, string? Reason);
