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
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.CloudRequestEngine.Data
{
    /// <summary>
    /// The <see cref="IElementData"/> class for 
    /// <see cref="FlowElements.CloudRequestEngine"/>.
    /// </summary>
    /// <remarks>
    /// The JSON response is a lazy property. The engine starts the HTTP
    /// request while it processes and returns without waiting. Every
    /// reader of the response waits for that one request, and every reader
    /// gets its result or its failure. The request is sent once.
    /// </remarks>
    public class CloudRequestData : AspectDataBase
    {
        private const string JSON_RESPONSE_KEY = "json-response";
        private const string PROCESS_STARTED_KEY = "process-started";

        /// <summary>
        /// The task that sends the request and gives the response, or
        /// null when the response was set directly.
        /// </summary>
        private Task<string> _response;

        /// <summary>
        /// Set to 1 by the first reader that records a failed request in
        /// the Flow Data's errors.
        /// </summary>
        private int _failureRecorded;

        /// <summary>
        /// The raw JSON response returned by the 51Degrees cloud service.
        /// Reading it waits for the request to finish.
        /// </summary>
        /// <exception cref="CloudRequestException">
        /// Thrown, to every reader, if the request failed.
        /// </exception>
        public string JsonResponse
        {
            get { return base[JSON_RESPONSE_KEY]?.ToString(); }
            set
            {
                Volatile.Write(ref _response, null);
                base[JSON_RESPONSE_KEY] = value;
            }
        }

        /// <summary>
        /// The raw JSON response if it has arrived, or null. Unlike
        /// <see cref="JsonResponse"/>, this never waits.
        /// </summary>
        internal string JsonResponseIfSet
        {
            get
            {
                var response = Volatile.Read(ref _response);
                if (response != null)
                {
                    return response.Status == TaskStatus.RanToCompletion
                        ? response.Result
                        : null;
                }
                return base.TryGetValue(JSON_RESPONSE_KEY, out object value)
                    ? value?.ToString()
                    : null;
            }
        }

        /// <summary>
        /// The task that gives the response, or null.
        /// </summary>
        internal Task<string> ResponseTask => Volatile.Read(ref _response);

        /// <summary>
        /// Flag to confirm that the CloudRequestEngine has started processing.
        /// </summary>
        public bool? ProcessStarted
        {
            get { return base[PROCESS_STARTED_KEY] as bool?; }
            set { base[PROCESS_STARTED_KEY] = value; }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">
        /// The logger for this instance
        /// </param>
        /// <param name="pipeline">
        /// The pipeline this instance was created by.
        /// </param>
        /// <param name="engine">
        /// The engine that created this instance.
        /// </param>
        public CloudRequestData(
            ILogger<AspectDataBase> logger,
            IPipeline pipeline,
            IAspectEngine engine) : base(logger, pipeline, engine)
        {
        }

        /// <summary>
        /// Set the task that gives the response. Called by the engine
        /// while it processes.
        /// </summary>
        internal void SetResponseTask(Task<string> response)
        {
            Volatile.Write(ref _response, response);
        }

        /// <summary>
        /// True for the first caller only, so that a failed request is
        /// recorded once in the Flow Data's errors.
        /// </summary>
        internal bool ClaimFailureRecord()
        {
            return Interlocked.Exchange(ref _failureRecorded, 1) == 0;
        }

        /// <summary>
        /// Wait for the response and return it.
        /// </summary>
        private static string WaitForResponse(Task<string> response)
        {
            // GetResult throws the request's own exception, the same
            // instance to every reader.
            return response.GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        protected override bool TryGetValue<T>(string key, out T value)
        {
            var response = Volatile.Read(ref _response);
            if (response != null &&
                string.Equals(key, JSON_RESPONSE_KEY,
                    StringComparison.OrdinalIgnoreCase))
            {
                value = (T)(object)WaitForResponse(response);
                return true;
            }
            return base.TryGetValue(key, out value);
        }

        /// <inheritdoc/>
        public override IReadOnlyDictionary<string, object> AsDictionary()
        {
            var values = base.AsDictionary();
            var response = Volatile.Read(ref _response);
            if (response == null)
            {
                return values;
            }
            var result = new Dictionary<string, object>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var entry in values)
            {
                result[entry.Key] = entry.Value;
            }
            result[JSON_RESPONSE_KEY] = WaitForResponse(response);
            return result;
        }
    }
}
