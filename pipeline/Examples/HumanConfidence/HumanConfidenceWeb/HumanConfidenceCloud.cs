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

using System.Net;
using System.Text.Json;

namespace Examples.HumanConfidence.HumanConfidenceWeb;

/// <summary>
/// The calls this demo makes to the cloud. It passes the evidence on as it
/// is and reads what the cloud gives back. It never works a level out,
/// because the level is the cloud's to give.
/// </summary>
public sealed class HumanConfidenceCloud
{
    private readonly HttpClient _http;
    private readonly string _resource;
    private readonly string? _license;
    private HashSet<string>? _headerNames;

    /// <summary>
    /// The API base, ending in one slash, so every URL is the base plus
    /// its path.
    /// </summary>
    public string Api { get; }

    public HumanConfidenceCloud(
        HttpClient http, string api, string resource, string? license)
    {
        _http = http;
        Api = api.Trim().TrimEnd('/') + "/";
        _resource = resource;
        _license = license;
    }

    /// <summary>
    /// Whether the key carries HumanConfidence, asked of the cloud's
    /// accessibleproperties. "yes" or "no" from the cloud's list, or
    /// "unknown" when the cloud couldn't be asked.
    /// </summary>
    public async Task<string> CarriesHumanConfidenceAsync()
    {
        try
        {
            using var document = JsonDocument.Parse(await _http.GetStringAsync(
                Api + "accessibleproperties?resource=" + _resource));
            var names = document.RootElement.TryGetProperty("Products", out var products)
                ? products.EnumerateObject()
                    .Where(p => p.Value.TryGetProperty("Properties", out _))
                    .SelectMany(p => p.Value.GetProperty("Properties").EnumerateArray())
                    .Select(p => Read(p, "Name"))
                : [];
            return names.Contains("HumanConfidence", StringComparer.OrdinalIgnoreCase)
                ? "yes" : "no";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return "unknown";
        }
    }

    /// <summary>
    /// The request headers the cloud reads, in lower case, from its own
    /// list, fetched once.
    /// </summary>
    public async Task<HashSet<string>> HeaderNamesAsync()
    {
        if (_headerNames == null)
        {
            var keys = JsonSerializer.Deserialize<string[]>(
                await _http.GetStringAsync(Api + "evidencekeys")) ?? [];
            _headerNames = keys
                .Where(k => k.StartsWith("header.", StringComparison.OrdinalIgnoreCase))
                .Select(k => k[7..].ToLowerInvariant())
                .ToHashSet();
        }
        return _headerNames;
    }

    /// <summary>
    /// The evidence for a passive call: the visitor's request headers the
    /// cloud reads, with their values as they came, and the visitor's
    /// address. Only the cloud's headers are passed on, so headers such as
    /// Authorization stay here. The address is set last, so no header can
    /// replace it.
    /// </summary>
    public async Task<Dictionary<string, string>> EvidenceAsync(
        IEnumerable<KeyValuePair<string, string>> headers, IPAddress? address)
    {
        var names = await HeaderNamesAsync();
        var evidence = new Dictionary<string, string>();
        foreach (var (name, value) in headers)
        {
            if (names.Contains(name.ToLowerInvariant()))
            {
                evidence[name.ToLowerInvariant()] = value;
            }
        }
        if (address != null)
        {
            evidence["client-ip"] = (address.IsIPv4MappedToIPv6
                ? address.MapToIPv4() : address).ToString();
        }
        return evidence;
    }

    /// <summary>
    /// Passive: posts the evidence to the key's JSON endpoint and reads the
    /// source and the level from the answer.
    /// </summary>
    public async Task<PassiveAnswer> PassiveAsync(Dictionary<string, string> evidence)
    {
        try
        {
            using var answer = await _http.PostAsync(
                Api + _resource + ".json", new FormUrlEncodedContent(evidence));
            var body = await answer.Content.ReadAsStringAsync();
            if (answer.IsSuccessStatusCode == false)
            {
                return new PassiveAnswer(null, null,
                    $"HTTP {(int)answer.StatusCode}: {Trim(body)}");
            }
            using var json = JsonDocument.Parse(body);
            var derived = json.RootElement.TryGetProperty("derived", out var d) ? d : default;
            return new PassiveAnswer(
                Read(derived, "humanconfidencesource"),
                Read(derived, "humanconfidence"),
                null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new PassiveAnswer(null, null, ex.Message);
        }
    }

    /// <summary>
    /// Redeem: posts the sealed value, with the License Key where there is
    /// one, to hc/redeem, and returns the cloud's answer as it gave it.
    /// </summary>
    public async Task<RedeemAnswer> RedeemAsync(string value)
    {
        var fields = new Dictionary<string, string> { ["value"] = value };
        if (string.IsNullOrWhiteSpace(_license) == false)
        {
            fields["license"] = _license;
        }
        try
        {
            using var answer = await _http.PostAsync(
                Api + "hc/redeem", new FormUrlEncodedContent(fields));
            var body = await answer.Content.ReadAsStringAsync();
            if (answer.StatusCode == HttpStatusCode.NotFound)
            {
                return new RedeemAnswer((int)answer.StatusCode, body,
                    $"The service at {Api} doesn't redeem Human Confidence.");
            }
            return new RedeemAnswer((int)answer.StatusCode, body, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new RedeemAnswer(502, "", ex.Message);
        }
    }

    private static string? Read(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    private static string Trim(string text) => text.Length > 200 ? text[..200] : text;
}

/// <summary>
/// What a passive call gave: the source and the level as the cloud wrote
/// them, or the error.
/// </summary>
public sealed record PassiveAnswer(string? Source, string? HumanConfidence, string? Error);

/// <summary>
/// What a redeem gave: the cloud's status and body as they came, or the
/// error.
/// </summary>
public sealed record RedeemAnswer(int StatusCode, string Body, string? Error);
