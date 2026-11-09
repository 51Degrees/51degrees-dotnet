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

using FiftyOne.Pipeline.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace FiftyOne.Pipeline.Core.Services
{
    /// <summary>
    /// Masks license keys in text, so a log entry or an error message never
    /// holds one in full. A masked key shows only its last 4 characters.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/7cd9bc82797ea86c39dd90ed41232f3b0f167e00/pipeline-specification/features/logging.md#license-keys">Specification</see>
    /// </summary>
    public static class LicenseKeyMask
    {
        /// <summary>
        /// The number of characters at the end of a key that stay visible.
        /// </summary>
        public const int VisibleCharacters = 4;

        /// <summary>
        /// The character that replaces every other character of a key.
        /// </summary>
        public const char MaskCharacter = '*';

        private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

        // A query parameter that holds license keys, such as LicenseKeys=a|b.
        private static readonly Regex KeyParameter = new Regex(
            "(?<![A-Za-z0-9_])(?<name>license[_-]?keys?|licen[cs]e)=" +
            "(?<value>[^&\\s'\"#)<>]*)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            RegexTimeout);

        // The separators between several keys in one value. The brackets
        // make the separators part of the result of Split.
        private static readonly Regex KeySeparator = new Regex(
            "(\\||%7C|,|%2C|;|%3B)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            RegexTimeout);

        /// <summary>
        /// Mask a key. Every character before the last 4 is replaced. A
        /// key of 4 characters or fewer is replaced in full.
        /// </summary>
        /// <param name="key">The key to mask.</param>
        /// <returns>The masked key.</returns>
        public static string Mask(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return key;
            }
            if (key.Length <= VisibleCharacters)
            {
                return new string(MaskCharacter, key.Length);
            }
            return new string(MaskCharacter, key.Length - VisibleCharacters) +
                key.Substring(key.Length - VisibleCharacters);
        }

        /// <summary>
        /// Mask the license keys in some text. The text can be a URL, a
        /// message or the message of an exception. A key is masked where
        /// it is a value of a query parameter called LicenseKeys, License
        /// or Licence, and wherever one of the supplied keys appears.
        /// </summary>
        /// <param name="text">The text to mask.</param>
        /// <param name="knownKeys">
        /// Keys to mask wherever they appear, whether plain or encoded for
        /// a URL. Null or empty entries are ignored.
        /// </param>
        /// <returns>The text with each key masked.</returns>
        public static string MaskText(
            string text,
            IEnumerable<string> knownKeys = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            var result = text;
            if (knownKeys != null)
            {
                foreach (var key in knownKeys
                    .Where(k => string.IsNullOrEmpty(k) == false)
                    .Distinct()
                    .OrderByDescending(k => k.Length))
                {
                    var masked = Mask(key);
                    foreach (var form in new[]
                    {
                        key,
                        Uri.EscapeDataString(key),
                        WebUtility.UrlEncode(key),
                    }.Distinct())
                    {
                        result = result.Replace(form, masked);
                    }
                }
            }

            return KeyParameter.Replace(result, match =>
                match.Groups["name"].Value + "=" +
                MaskValue(match.Groups["value"].Value));
        }

        /// <summary>
        /// Mask the license keys in a URL.
        /// </summary>
        /// <param name="uri">The URL.</param>
        /// <param name="knownKeys">
        /// Keys to mask wherever they appear. See
        /// <see cref="MaskText(string, IEnumerable{string})"/>.
        /// </param>
        /// <returns>The URL as text with each key masked.</returns>
        public static string MaskUri(
            Uri uri,
            IEnumerable<string> knownKeys = null)
        {
            return uri == null ? null : MaskText(uri.AbsoluteUri, knownKeys);
        }

        /// <summary>
        /// Make an exception whose message, and the messages of the
        /// exceptions inside it, hold each license key only in masked form.
        /// A library often puts the URL it requested in its message, so this
        /// stands in for such an exception when it is logged or wrapped.
        /// The stand-in names the type of the original and keeps its stack
        /// trace as text.
        /// </summary>
        /// <param name="exception">The exception to stand in for.</param>
        /// <param name="knownKeys">
        /// Keys to mask wherever they appear. See
        /// <see cref="MaskText(string, IEnumerable{string})"/>.
        /// </param>
        /// <returns>The stand-in, or null if there is no exception.</returns>
        public static Exception MaskException(
            Exception exception,
            IEnumerable<string> knownKeys = null)
        {
            if (exception == null)
            {
                return null;
            }
            var keys = knownKeys?.ToList();
            return new MaskedException(
                exception.GetType().FullName,
                MaskText(exception.Message, keys),
                MaskText(exception.StackTrace, keys),
                MaskException(exception.InnerException, keys));
        }

        private static string MaskValue(string value)
        {
            var parts = KeySeparator.Split(value);
            for (int i = 0; i < parts.Length; i += 2)
            {
                parts[i] = Mask(parts[i]);
            }
            return string.Concat(parts);
        }
    }
}
