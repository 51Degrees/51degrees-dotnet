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

using FiftyOne.Pipeline.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.Pipeline.Web.Shared
{
    /// <summary>
    /// Replaces a leading tilde (~) in the data file paths of a pipeline
    /// configuration with the root of the web application.
    /// </summary>
    internal static class ApplicationRootPaths
    {
        /// <summary>
        /// The build parameter that holds an engine's data file path.
        /// </summary>
        internal const string DataFileParameter = "DataFile";

        /// <summary>
        /// Replace each data file path that starts with '~/' or '~\' in the
        /// elements of the options, including nested sub elements. Any other
        /// path is left as it is.
        /// </summary>
        /// <param name="options">
        /// Pipeline options to update in place.
        /// </param>
        /// <param name="resolve">
        /// Maps a path starting with '~/' or '~\' to a full path.
        /// </param>
        internal static void ResolveDataFiles(
            PipelineOptions options,
            Func<string, string> resolve)
        {
            if (options?.Elements != null)
            {
                ResolveDataFiles(options.Elements, resolve);
            }
        }

        private static void ResolveDataFiles(
            IEnumerable<ElementOptions> elements,
            Func<string, string> resolve)
        {
            foreach (var element in elements)
            {
                var parameters = element.BuildParameters;
                if (parameters != null)
                {
                    var keys = parameters.Keys.Where(k => string.Equals(
                        k,
                        DataFileParameter,
                        StringComparison.OrdinalIgnoreCase)).ToList();
                    foreach (var key in keys)
                    {
                        if (parameters[key] is string path &&
                            IsApplicationRootPath(path))
                        {
                            parameters[key] = resolve(path);
                        }
                    }
                }
                if (element.SubElements != null)
                {
                    ResolveDataFiles(element.SubElements, resolve);
                }
            }
        }

        /// <summary>
        /// True if the path starts with '~/' or '~\'.
        /// </summary>
        internal static bool IsApplicationRootPath(string path)
        {
            return path != null &&
                path.Length > 1 &&
                path[0] == '~' &&
                (path[1] == '/' || path[1] == '\\');
        }

        /// <summary>
        /// Combine a root directory with the part of a '~/' or '~\' path that
        /// follows the tilde, using the separator of the current platform.
        /// </summary>
        internal static string CombineWithRoot(string root, string path)
        {
            var relative = path.Substring(2)
                .Replace('/', System.IO.Path.DirectorySeparatorChar)
                .Replace('\\', System.IO.Path.DirectorySeparatorChar);
            return System.IO.Path.Combine(root, relative);
        }
    }
}
