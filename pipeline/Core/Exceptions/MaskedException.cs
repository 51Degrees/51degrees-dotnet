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

namespace FiftyOne.Pipeline.Core.Exceptions
{
    /// <summary>
    /// Stands in for an exception whose message held a license key, and
    /// holds the message with each key masked.
    /// See <see cref="Services.LicenseKeyMask.MaskException"/>.
    /// </summary>
    public sealed class MaskedException : Exception
    {
        /// <summary>
        /// The full name of the type of the exception this stands in for.
        /// </summary>
        public string OriginalTypeName { get; }

        /// <summary>
        /// The stack trace of the exception this stands in for.
        /// </summary>
        public string OriginalStackTrace { get; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="originalTypeName">
        /// The full name of the type of the original exception.
        /// </param>
        /// <param name="maskedMessage">
        /// The message of the original with each license key masked.
        /// </param>
        /// <param name="originalStackTrace">
        /// The stack trace of the original, with each license key masked.
        /// </param>
        /// <param name="inner">
        /// The masked stand-in for the inner exception.
        /// </param>
        public MaskedException(
            string originalTypeName,
            string maskedMessage,
            string originalStackTrace,
            Exception inner)
            : base(originalTypeName + ": " + maskedMessage, inner)
        {
            OriginalTypeName = originalTypeName;
            OriginalStackTrace = originalStackTrace;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return base.ToString() + Environment.NewLine +
                OriginalStackTrace;
        }
    }
}
