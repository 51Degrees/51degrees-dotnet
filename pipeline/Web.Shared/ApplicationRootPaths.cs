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
using FiftyOne.Pipeline.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.Pipeline.Web.Shared
{
    /// <summary>
    /// Replaces a leading tilde (~) in the file system paths of a pipeline
    /// configuration with the root of the web application.
    /// </summary>
    internal static class ApplicationRootPaths
    {
        /// <summary>
        /// The build parameters that hold a file system path, matched
        /// without regard to case. They are an engine's data file and its
        /// temporary directory, the latter with or without the 'Set' that
        /// starts the name of its builder method.
        /// </summary>
        internal static readonly string[] PathParameters =
            new[] { "DataFile", "TempDirPath", "SetTempDirPath" };

        /// <summary>
        /// Replace each path parameter that starts with '~/' or '~\' in the
        /// elements of the options, including nested sub elements. Any other
        /// path is left as it is.
        /// </summary>
        /// <param name="options">
        /// Pipeline options to update in place.
        /// </param>
        /// <param name="resolve">
        /// Maps a path starting with '~/' or '~\' to a full path. An
        /// empty result is an error.
        /// </param>
        internal static void ResolvePaths(
            PipelineOptions options,
            Func<string, string> resolve)
        {
            if (options?.Elements != null)
            {
                ResolvePaths(options.Elements, resolve);
            }
        }

        private static void ResolvePaths(
            IEnumerable<ElementOptions> elements,
            Func<string, string> resolve)
        {
            foreach (var element in elements)
            {
                var parameters = element.BuildParameters;
                if (parameters != null)
                {
                    var keys = parameters.Keys.Where(k => PathParameters.Any(
                        n => string.Equals(
                            k,
                            n,
                            StringComparison.OrdinalIgnoreCase))).ToList();
                    foreach (var key in keys)
                    {
                        if (parameters[key] is string path &&
                            IsApplicationRootPath(path))
                        {
                            var resolved = resolve(path);
                            if (string.IsNullOrEmpty(resolved))
                            {
                                throw new PipelineConfigurationException(
                                    "The web application root could not " +
                                    $"be applied to '{path}', the value " +
                                    $"of '{key}'.");
                            }
                            parameters[key] = resolved;
                        }
                    }
                }
                if (element.SubElements != null)
                {
                    ResolvePaths(element.SubElements, resolve);
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
