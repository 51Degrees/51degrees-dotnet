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
 
 using FiftyOne.Pipeline.Engines.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace FiftyOne.GeoLocation.Core.FlowElements
{
    /// <summary>
    /// A fluent builder that can create a location engine
    /// </summary>
    /// <seealso cref="ILocationEngineBuilder{TBuilder}"/>
    public interface ILocationEngineBuilder
    {
        /// <summary>
        /// Set the base URL for the remote location service.
        /// </summary>
        /// <param name="url">
        /// The base URL for the location service.
        /// </param>
        /// <returns>
        /// This builder.
        /// </returns>
#pragma warning disable CA1054 // Uri parameters should not be strings
        // Not correcting at this time.
        ILocationEngineBuilder SetUrl(string url);
#pragma warning restore CA1054 // Uri parameters should not be strings

        /// <summary>
        /// Set the lazy loading configuration for the engine.
        /// </summary>
        /// <param name="lazyLoadingConfig">
        /// The lazy loading configuration to use.
        /// </param>
        /// <returns>
        /// This builder.
        /// </returns>
        ILocationEngineBuilder SetLazyLoading(
            LazyLoadingConfiguration lazyLoadingConfig);
    }

    /// <summary>
    /// A fluent builder that can create a location engine
    /// </summary>
    /// <typeparam name="TBuilder">
    /// The type of the builder.
    /// This is used so that methods on the base class will return the 
    /// correct builder type, allowing derived builders to add their own
    /// methods without compromising user-experience.
    /// </typeparam>
    /// <seealso cref="ILocationEngineBuilder"/>
    public interface ILocationEngineBuilder<TBuilder> : ILocationEngineBuilder
        where TBuilder : ILocationEngineBuilder<TBuilder>
    {
        /// <inheritdoc/>
#pragma warning disable CA1054 // Uri parameters should not be strings
        // Not correcting at this time.
        new TBuilder SetUrl(string url);
#pragma warning restore CA1054 // Uri parameters should not be strings

        /// <inheritdoc/>
        new TBuilder SetLazyLoading(
            LazyLoadingConfiguration lazyLoadingConfig);
    }
}
