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

namespace FiftyOne.Pipeline.Web.Shared
{
    /// <summary>
    /// Decides whether a request's content type is one whose form fields
    /// the web integrations add to the evidence.
    /// </summary>
    internal static class FormContentType
    {
        /// <summary>
        /// True when the media type of a Content-Type header value is one
        /// of <see cref="Constants.CONTENT_TYPE_FORM"/>. The media type is
        /// the part before any parameters, which start at a semicolon, and
        /// its type and subtype are compared ignoring case, as RFC 9110
        /// section 8.3.1 defines them.
        /// </summary>
        /// <param name="contentType">
        /// The Content-Type header value, or null where there is none.
        /// </param>
        /// <returns>
        /// True if the form fields of the request should be read.
        /// </returns>
        internal static bool IsForm(string contentType)
        {
            if (contentType == null)
            {
                return false;
            }
            var end = contentType.IndexOf(';');
            var mediaType = (end < 0 ?
                contentType :
                contentType.Substring(0, end)).Trim();
            foreach (var form in Constants.CONTENT_TYPE_FORM)
            {
                if (string.Equals(
                    mediaType, form, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
