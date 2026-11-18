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

using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.CloudRequestEngine.Services;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.Pipeline.CloudRequestEngine.Tests
{
    /// <summary>
    /// Aspect data for <see cref="CloudResponseReader"/>.
    /// </summary>
    internal sealed class CloudResponseReaderData : AspectDataBase
    {
        public CloudResponseReaderData(
            IPipeline pipeline, IAspectEngine engine)
            : base(null, pipeline, engine,
                MissingPropertyServiceCloud.Instance)
        {
        }
    }

    /// <summary>
    /// A cloud aspect engine that reads the response for one product, as
    /// the product engines do. The response is a lazy property, so a
    /// failed request is recorded in the Flow Data's errors only when a
    /// reader such as this meets it while the Flow Data is processed.
    /// </summary>
    internal sealed class CloudResponseReader
        : CloudAspectEngineBase<CloudResponseReaderData>
    {
        private readonly string _product;

        public CloudResponseReader(string product = "device")
            : base(NullLogger<CloudResponseReader>.Instance, CreateData)
        {
            _product = product;
        }

        public override string ElementDataKey => _product;

        public override IEvidenceKeyFilter EvidenceKeyFilter { get; } =
            new EvidenceKeyFilterWhitelist(new List<string>());

        private static CloudResponseReaderData CreateData(
            IPipeline pipeline,
            FlowElementBase<CloudResponseReaderData,
                IAspectPropertyMetaData> engine)
            => new CloudResponseReaderData(pipeline, (IAspectEngine)engine);

        protected override void ProcessCloudEngine(
            IFlowData data, CloudResponseReaderData aspectData, string json)
        {
            var values = JObject.Parse(json)[_product]?
                .ToObject<Dictionary<string, object>>();
            if (values != null)
            {
                aspectData.PopulateFrom(
                    CreateAPVDictionary(values, Properties.ToList()));
            }
        }
    }
}
