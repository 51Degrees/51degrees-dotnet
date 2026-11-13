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

namespace Examples.Did.CreatorContextWeb;

/// <summary>
/// Reads a setting from the environment for the example.
/// </summary>
public static class EnvironmentSetting
{
    /// <summary>
    /// Get an environment variable. When it is not set its old name is
    /// read, and a value read from the old name is reported on the error
    /// stream as deprecated.
    /// </summary>
    /// <param name="name">The variable's name.</param>
    /// <param name="oldName">The name the variable had before.</param>
    /// <returns>
    /// The value, or what <paramref name="name"/> holds when the old name
    /// is not set either.
    /// </returns>
    public static string? Read(string name, string oldName)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value) == false)
        {
            return value;
        }
        var oldValue = Environment.GetEnvironmentVariable(oldName);
        if (string.IsNullOrWhiteSpace(oldValue))
        {
            return value;
        }
        Console.Error.WriteLine(
            $"The environment variable {oldName} is deprecated and {name} "
            + $"replaces it. Set {name} to the same value instead.");
        return oldValue;
    }
}
