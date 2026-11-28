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
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.CloudRequestEngine.FlowElements
{
    /// <summary>
    /// Builder for the <see cref="BrowserDerivedCloudEngine"/>, which is
    /// added after a <see cref="CloudRequestEngine"/>, through the fluent
    /// or the configured Pipeline builders.
    /// </summary>
    public class BrowserDerivedCloudEngineBuilder
        : CloudAspectEngineBuilderBase<BrowserDerivedCloudEngineBuilder, BrowserDerivedCloudEngine>
    {
        private readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="loggerFactory">
        /// Logger factory used by the engine and the Element Data it
        /// creates.
        /// </param>
        public BrowserDerivedCloudEngineBuilder(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        /// <summary>
        /// Build a new <see cref="BrowserDerivedCloudEngine"/>.
        /// </summary>
        /// <returns>The engine.</returns>
        public BrowserDerivedCloudEngine Build()
        {
            return BuildEngine();
        }

        /// <summary>
        /// Create a new <see cref="BrowserDerivedCloudEngine"/>. The
        /// properties are not passed to the engine, as the cloud service
        /// already answers for the credential in use.
        /// </summary>
        /// <param name="properties">Unused.</param>
        /// <returns>The engine.</returns>
        protected override BrowserDerivedCloudEngine NewEngine(
            List<string> properties)
        {
            return new BrowserDerivedCloudEngine(
                _loggerFactory.CreateLogger<BrowserDerivedCloudEngine>(),
                CreateData);
        }

        private IBrowserDerivedCloudData CreateData(
            IPipeline pipeline,
            FlowElementBase<IBrowserDerivedCloudData, IAspectPropertyMetaData> engine)
        {
            return new BrowserDerivedCloudData(
                _loggerFactory.CreateLogger<BrowserDerivedCloudData>(),
                pipeline,
                engine as IAspectEngine);
        }
    }
}
