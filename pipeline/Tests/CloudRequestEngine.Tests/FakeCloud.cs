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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.CloudRequestEngine.Tests;

/// <summary>
/// Stands in for the cloud service. Each of its three endpoints answers
/// with what a test sets, and the requests each receives are counted.
/// </summary>
internal sealed class FakeCloud : HttpMessageHandler
{
    /// <summary>
    /// The accessible properties answered unless a test sets others.
    /// </summary>
    public const string AccessiblePropertiesBody =
        "{\"Products\": {\"device\": {\"DataTier\": \"tier\", " +
        "\"Properties\": [{\"Name\": \"value\", \"Type\": \"String\", " +
        "\"Category\": \"Device\"}]}}}";

    /// <summary>
    /// The data answered unless a test sets other data.
    /// </summary>
    public const string DataBody = "{\"device\": {\"value\": \"1\"}}";

    /// <summary>
    /// The evidence keys answered unless a test sets others.
    /// </summary>
    public const string EvidenceKeysBody = "[\"query.user-agent\"]";

    /// <summary>
    /// What one endpoint answers.
    /// </summary>
    public sealed class Answer
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        public string Body { get; set; }

        /// <summary>
        /// When true the endpoint does not answer, and the request waits
        /// until the client gives up on it.
        /// </summary>
        public bool Hang { get; set; }
    }

    private readonly Dictionary<string, Answer> _answers;

    private readonly ConcurrentDictionary<string, int> _requests =
        new ConcurrentDictionary<string, int>();

    private readonly ConcurrentQueue<string> _forms =
        new ConcurrentQueue<string>();

    public FakeCloud()
    {
        _answers = new Dictionary<string, Answer>()
        {
            [Constants.EVIDENCE_KEYS_FILENAME] =
                new Answer() { Body = EvidenceKeysBody },
            [Constants.PROPERTIES_FILENAME] =
                new Answer() { Body = AccessiblePropertiesBody },
            [Constants.DATA_FILENAME] = new Answer() { Body = DataBody },
        };
    }

    /// <summary>
    /// The answer of the endpoint with the file name given, being one of
    /// <see cref="Constants.EVIDENCE_KEYS_FILENAME"/>,
    /// <see cref="Constants.PROPERTIES_FILENAME"/> and
    /// <see cref="Constants.DATA_FILENAME"/>.
    /// </summary>
    public Answer For(string endpoint) => _answers[endpoint];

    /// <summary>
    /// The number of requests the endpoint has received.
    /// </summary>
    public int Requests(string endpoint) =>
        _requests.TryGetValue(endpoint, out var count) ? count : 0;

    /// <summary>
    /// The form body of each request the data endpoint received, as
    /// field names and decoded values.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<KeyValuePair<string, string>>> Forms =>
        _forms.Select(Parse).ToList();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var endpoint = _answers.Keys.Single(name =>
            request.RequestUri.AbsolutePath.EndsWith(
                name, StringComparison.OrdinalIgnoreCase));
        _requests.AddOrUpdate(endpoint, 1, (_, count) => count + 1);
        if (endpoint == Constants.DATA_FILENAME)
        {
            _forms.Enqueue(await request.Content.ReadAsStringAsync(
                cancellationToken));
        }

        var answer = _answers[endpoint];
        if (answer.Hang)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        return new HttpResponseMessage(answer.Status)
        {
            Content = new StringContent(answer.Body ?? string.Empty),
        };
    }

    private static IReadOnlyList<KeyValuePair<string, string>> Parse(
        string form) =>
        form.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(field => field.Split('=', 2))
            .Select(parts => new KeyValuePair<string, string>(
                WebUtility.UrlDecode(parts[0]),
                WebUtility.UrlDecode(parts.Length > 1 ? parts[1] : "")))
            .ToList();
}
