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

namespace FiftyOne.Pipeline.Web.Shared.Adapters
{
    /// <summary>
    /// Gives the method of a request. An <see cref="IRequestAdapter"/> that
    /// also implements this lets the client-side endpoints answer a matching
    /// If-None-Match header with 304 only to GET and HEAD, and with 412 to
    /// any other method, as
    /// <see href="https://www.rfc-editor.org/rfc/rfc9110#section-13.1.2">RFC
    /// 9110 section 13.1.2</see> requires. A request whose adapter does not
    /// implement it is treated as a GET.
    /// </summary>
    public interface IRequestMethodAdapter
    {
        /// <summary>
        /// The request method, such as GET or POST, as the request gives it.
        /// </summary>
        string Method { get; }
    }
}
