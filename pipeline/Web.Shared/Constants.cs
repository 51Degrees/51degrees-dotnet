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

using System.Runtime.CompilerServices;

// The two web integrations implement IRequestMethodAdapter.
[assembly: InternalsVisibleTo("FiftyOne.Pipeline.Web, PublicKey=0024000004800000940000000602000000240000525341310004000001000100c3a631e6634ea697e19c6d2fedc285bdc7f0447a5583e1ac3c5ed3502b7633e691f899a265c42a5611122a23fd2bc882e4e412384a5d4183271782416cf016b06e6648273d44896e95ce482bb8b13054ba6a6f41d393c3f3f2e5780d620e50cb67c248882e4427bf007b7c77fdd65f832c7f4a3fef9dc18e39f792d1a37cc980")]
[assembly: InternalsVisibleTo("FiftyOne.Pipeline.Web.Framework, PublicKey=0024000004800000940000000602000000240000525341310004000001000100c3a631e6634ea697e19c6d2fedc285bdc7f0447a5583e1ac3c5ed3502b7633e691f899a265c42a5611122a23fd2bc882e4e412384a5d4183271782416cf016b06e6648273d44896e95ce482bb8b13054ba6a6f41d393c3f3f2e5780d620e50cb67c248882e4427bf007b7c77fdd65f832c7f4a3fef9dc18e39f792d1a37cc980")]

namespace FiftyOne.Pipeline.Web.Shared
{
    /// <summary>
    /// Static class containing various constants that are used by the 
    /// Pipeline web integration and/or are helpful to callers.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming",
        "CA1707:Identifiers should not contain underscores",
        Justification = "51Degrees coding style is for constant names " +
            "to be all-caps with an underscore to separate words.")]
    public static class Constants
    {
        /// <summary>
        /// The Content type that indicates the body contains a 
        /// URL encoded form.
        /// </summary>
        public static readonly string[] CONTENT_TYPE_FORM = 
        {
            "application/x-www-form-urlencoded",
            "multipart/form-data"
        };

        /// <summary>
        /// The HTTP method indicating this request was a POST.
        /// </summary>
        public const string METHOD_POST = "POST";
    }
}
