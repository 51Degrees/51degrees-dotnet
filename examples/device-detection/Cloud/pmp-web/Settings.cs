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

namespace FiftyOne.Examples.Cloud.PmpWeb
{
    /// <summary>
    /// The input data, read from the environment variable names every
    /// language's copy of this demo reads, each value under one name.
    /// <para>
    /// Neither value is ever written to the console or a log, because a
    /// resource key is not for publishing and an endpoint can name a host
    /// that is not public. Only the name of the variable each came from is.
    /// </para>
    /// </summary>
    public sealed class Settings
    {
        /// <summary>
        /// The resource key the pages and the pipeline use.
        /// </summary>
        public const string RESOURCE_KEY_VARIABLE = "_51DEGREES_RESOURCE_KEY";

        /// <summary>
        /// The cloud the pages load the PMP and the client script from,
        /// and the pipeline asks.
        /// </summary>
        public const string CLOUD_ENDPOINT_VARIABLE = "_51DEGREES_CLOUD_ENDPOINT";

        /// <summary>
        /// The cloud used when no endpoint variable is set.
        /// </summary>
        public const string DEFAULT_CLOUD_ENDPOINT =
            "https://cloud.51degrees.com";

        /// <summary>
        /// The path every endpoint the pages use sits under. An endpoint
        /// variable may be given with or without it, because the other
        /// repositories that read _51DEGREES_CLOUD_ENDPOINT give it with.
        /// </summary>
        private const string API_PATH = "/api/v4";

        private Settings(
            string resourceKey,
            string resourceKeyVariable,
            string cloudEndpoint,
            string cloudEndpointVariable)
        {
            ResourceKey = resourceKey;
            ResourceKeyVariable = resourceKeyVariable;
            CloudEndpoint = cloudEndpoint;
            CloudEndpointVariable = cloudEndpointVariable;
        }

        /// <summary>The resource key.</summary>
        public string ResourceKey { get; }

        /// <summary>The variable the resource key came from.</summary>
        public string ResourceKeyVariable { get; }

        /// <summary>
        /// The cloud's address with no trailing slash and without the API
        /// path, for example https://cloud.51degrees.com.
        /// </summary>
        public string CloudEndpoint { get; }

        /// <summary>
        /// Where the cloud endpoint came from, being the variable's name in
        /// quotes or a note saying the default is in use.
        /// </summary>
        public string CloudEndpointVariable { get; }

        /// <summary>
        /// What the demo writes when no resource key is set.
        /// </summary>
        public static string MissingKeyMessage =>
            $"No resource key is set. Set the environment variable " +
            $"'{RESOURCE_KEY_VARIABLE}' to a 51Degrees resource key " +
            "that includes the 51Did properties. A resource key can be " +
            "created at https://51degrees.com/configure?utm_source=code&utm_medium=example&utm_campaign=51degrees-dotnet&utm_content=examples-cloud-pmp-web-settings.cs&utm_term=resource-key-required";

        /// <summary>
        /// Reads both values from the environment.
        /// </summary>
        /// <returns>
        /// The settings, or null when no resource key is set.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// The endpoint is not an absolute http or https address.
        /// </exception>
        public static Settings? FromEnvironment()
        {
            var key = Read(RESOURCE_KEY_VARIABLE);
            if (key == null)
            {
                return null;
            }

            var endpoint = Read(CLOUD_ENDPOINT_VARIABLE);
            return new Settings(
                key,
                RESOURCE_KEY_VARIABLE,
                endpoint == null
                    ? DEFAULT_CLOUD_ENDPOINT
                    : CloudBase(endpoint, CLOUD_ENDPOINT_VARIABLE),
                endpoint == null
                    ? $"the default, {DEFAULT_CLOUD_ENDPOINT}"
                    : $"read from '{CLOUD_ENDPOINT_VARIABLE}'");
        }

        /// <summary>
        /// The address the PMP tag loads from, being the cloud, the API
        /// path, "pmp", and the resource key as the file name, because the
        /// PMP reads its key from the name of the script it was served as.
        /// </summary>
        /// <remarks>
        /// The key is escaped for a URL path segment here, and not written
        /// into the templates as a placeholder beside the rest of the
        /// address. A template placeholder is only ever encoded for HTML,
        /// which is not the same thing: a key holding '/', '?', '#' or '%'
        /// would still address the cloud, but a different part of it. The
        /// tag loads as a script, so the refusal that follows reaches no
        /// page code and the demo shows nothing at all.
        /// </remarks>
        private string PmpScriptUrl =>
            $"{CloudEndpoint}{API_PATH}/pmp/" +
            $"{Uri.EscapeDataString(ResourceKey)}.js";

        /// <summary>
        /// The address the cloud serves the client script from, which is
        /// <see cref="PmpScriptUrl"/> without the "pmp" segment, and is
        /// escaped for the same reason.
        /// </summary>
        private string CloudClientScriptUrl =>
            $"{CloudEndpoint}{API_PATH}/" +
            $"{Uri.EscapeDataString(ResourceKey)}.js";

        /// <summary>
        /// The placeholders a page under /cloud/ is filled with. The names
        /// are the ones in wwwroot/templates and README.md, and every
        /// language's copy of this demo fills the same ones.
        /// </summary>
        public IReadOnlyDictionary<string, string> CloudPlaceholders() =>
            new Dictionary<string, string>
            {
                ["PMP_SCRIPT_URL"] = PmpScriptUrl,
                ["CLIENT_SCRIPT_URL"] = CloudClientScriptUrl
            };

        /// <summary>
        /// The path the pipeline pages load the client script from, which
        /// the 51Degrees web integration answers on this demo's own origin.
        /// </summary>
        public const string PIPELINE_CLIENT_SCRIPT_PATH = "/51Degrees.core.js";

        /// <summary>
        /// The placeholders a page under /pipeline/ is filled with. The
        /// PMP still comes from the cloud, and only the client script
        /// changes, being the one this demo's pipeline serves.
        /// </summary>
        public IReadOnlyDictionary<string, string> PipelinePlaceholders() =>
            new Dictionary<string, string>
            {
                ["PMP_SCRIPT_URL"] = PmpScriptUrl,
                ["CLIENT_SCRIPT_URL"] = PIPELINE_CLIENT_SCRIPT_PATH
            };

        /// <summary>
        /// The two values the pipeline's cloud request engine is built
        /// with, as configuration keys under the element list in
        /// appsettings.json, so the pipeline reads the same environment
        /// variables as the pages and neither value is written to a file.
        /// The cloud request engine is the first element in that list.
        /// </summary>
        public IEnumerable<KeyValuePair<string, string?>>
            PipelineConfiguration() =>
            new Dictionary<string, string?>
            {
                ["PipelineOptions:Elements:0:BuildParameters:ResourceKey"] =
                    ResourceKey,
                ["PipelineOptions:Elements:0:BuildParameters:EndPoint"] =
                    $"{CloudEndpoint}{API_PATH}/"
            };

        /// <summary>
        /// The variable's value without surrounding spaces, or null when it
        /// is not set or is blank.
        /// </summary>
        private static string? Read(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        /// <summary>
        /// The cloud's address from an endpoint given with or without the
        /// API path and a trailing slash.
        /// </summary>
        private static string CloudBase(string value, string variable)
        {
            var endpoint = value.TrimEnd('/');
            if (endpoint.EndsWith(API_PATH, StringComparison.OrdinalIgnoreCase))
            {
                endpoint = endpoint
                    .Substring(0, endpoint.Length - API_PATH.Length)
                    .TrimEnd('/');
            }
            if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) == false ||
                (uri.Scheme != Uri.UriSchemeHttp &&
                 uri.Scheme != Uri.UriSchemeHttps))
            {
                // The value is not repeated, in case a key was put in the
                // wrong variable.
                throw new InvalidOperationException(
                    $"The environment variable '{variable}' must be an " +
                    "absolute http or https address, for example " +
                    $"'{DEFAULT_CLOUD_ENDPOINT}{API_PATH}/'.");
            }
            return endpoint;
        }
    }
}
