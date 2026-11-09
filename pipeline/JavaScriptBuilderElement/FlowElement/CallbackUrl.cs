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
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FiftyOne.Pipeline.JavaScriptBuilder.FlowElement
{
    /// <summary>
    /// The callback URL the client script posts to for updated JSON. The
    /// protocol and host usually come from the request, and the script holds
    /// the URL in a single-quoted string, so the URL is checked before it is
    /// written into the script. A URL that is not valid, including one whose
    /// protocol is not http or https, is refused. In anything after the host
    /// and port, and in the endpoint, the characters a URL cannot hold or
    /// that could end the string or the script are percent-encoded.
    /// The rules are applied to the text given, and not to the result of
    /// parsing it, because <see cref="Uri"/> accepts hosts the rules refuse
    /// and rewrites others, such as 127.1.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/pipeline-elements/javascript-builder.md#callback-url">Specification</see>
    /// </summary>
    internal sealed class CallbackUrl
    {
        /// <summary>
        /// The largest port number.
        /// </summary>
        private const int MaxPort = 65535;

        /// <summary>
        /// The most digits a port can have once its leading zeros are
        /// dropped.
        /// </summary>
        private const int MaxPortDigits = 5;

        /// <summary>
        /// The printable ASCII characters that are percent-encoded, besides
        /// the control characters, the space and everything outside ASCII.
        /// </summary>
        private const string Encoded = "\"'<>\\^`{|}";

        private const string HexDigits = "0123456789ABCDEF";

        /// <summary>
        /// The schemes the script can send its request with. It uses the URL
        /// only with fetch and XMLHttpRequest, which reach a server with no
        /// other scheme.
        /// </summary>
        private static readonly string[] Schemes = { "http", "https" };

        /// <summary>
        /// The characters the host value cannot hold. A '?' or '#' would
        /// make the endpoint part of the query or fragment, and an '@' means
        /// user information, which fetch refuses and XMLHttpRequest sends.
        /// </summary>
        private static readonly char[] NotInHost = { '?', '#', '@' };

        /// <summary>
        /// A label of a host name, being ASCII letters, digits, '-' and '_'.
        /// </summary>
        private static readonly Regex Label = new Regex(
            @"\A[A-Za-z0-9_-]+\z", RegexOptions.CultureInvariant);

        /// <summary>
        /// A port as it follows the host, being ':' and any number of digits.
        /// </summary>
        private static readonly Regex Port = new Regex(
            @"\A:[0-9]*\z", RegexOptions.CultureInvariant);

        /// <summary>
        /// One group of an IPv6 address.
        /// </summary>
        private static readonly Regex HexGroup = new Regex(
            @"\A[0-9A-Fa-f]{1,4}\z", RegexOptions.CultureInvariant);

        /// <summary>
        /// An IPv4 address as RFC 3986 section 3.2.2 defines one, being four
        /// numbers from 0 to 255 with no leading zeros.
        /// </summary>
        private static readonly Regex Ipv4 = new Regex(
            @"\A(25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9]?[0-9])" +
            @"(\.(25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9]?[0-9])){3}\z",
            RegexOptions.CultureInvariant);

        /// <summary>
        /// Whether the URL can be written into the script.
        /// </summary>
        internal bool Valid { get; }

        /// <summary>
        /// The URL to write into the script when it is valid, and otherwise
        /// the URL as it was built, for the log, with <c>***</c> in place of
        /// everything in the host value before its last '@'.
        /// </summary>
        internal string Url { get; }

        private CallbackUrl(bool valid, string url)
        {
            Valid = valid;
            Url = url;
        }

        /// <summary>
        /// Builds the callback URL from its parts and checks it. One slash
        /// separates the host and the endpoint, being added when neither has
        /// one and removed when both do. The protocol and the host are
        /// written in lower case and the rest as given, apart from the
        /// percent-encoding.
        /// </summary>
        /// <param name="protocol">
        /// The protocol, http or https in any case.
        /// </param>
        /// <param name="host">
        /// The host, which can end with a port and a path.
        /// </param>
        /// <param name="endpoint">
        /// The endpoint.
        /// </param>
        /// <returns>
        /// The callback URL, or null when the protocol, host or endpoint is
        /// not set.
        /// </returns>
        internal static CallbackUrl Of(
            string protocol,
            string host,
            string endpoint)
        {
            if (string.IsNullOrWhiteSpace(protocol) ||
                string.IsNullOrWhiteSpace(host) ||
                string.IsNullOrWhiteSpace(endpoint))
            {
                return null;
            }
            var hostSlash = host[host.Length - 1] == '/';
            var endpointSlash = endpoint[0] == '/';
            if (hostSlash == false && endpointSlash == false)
            {
                endpoint = "/" + endpoint;
            }
            else if (hostSlash && endpointSlash)
            {
                endpoint = endpoint.Substring(1);
            }
            var end = AuthorityEnd(host);
            var authority = IsScheme(protocol) &&
                host.IndexOfAny(NotInHost) < 0
                ? CheckedHostPort(host.Substring(0, end))
                : null;
            if (authority == null)
            {
                return new CallbackUrl(
                    false,
                    protocol + "://" + WithoutUserInformation(host) +
                        endpoint);
            }
            return new CallbackUrl(
                true,
                AsciiLowerCase(protocol) + "://" + authority +
                    Encode(host.Substring(end)) + Encode(endpoint));
        }

        /// <summary>
        /// The host value with everything before its last '@' replaced by
        /// <c>***</c>. The last '@' of the whole value is used, and not that
        /// of the authority, because a password can hold a '/', '?' or '#'.
        /// </summary>
        private static string WithoutUserInformation(string host)
        {
            var at = host.LastIndexOf('@');
            return at < 0 ? host : "***" + host.Substring(at);
        }

        /// <summary>
        /// Whether the protocol is http or https, ignoring the case of its
        /// ASCII letters only. <see cref="StringComparison.OrdinalIgnoreCase"/>
        /// also matches a letter outside ASCII to an ASCII one, such as the
        /// long s (U+017F) to 's', so it is not used.
        /// </summary>
        /// <param name="protocol">
        /// The protocol.
        /// </param>
        /// <returns>
        /// True if the protocol is http or https.
        /// </returns>
        internal static bool IsScheme(string protocol)
        {
            if (protocol == null)
            {
                return false;
            }
            var lower = AsciiLowerCase(protocol);
            foreach (var scheme in Schemes)
            {
                if (string.Equals(scheme, lower, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The text with the ASCII letters A to Z in lower case and every
        /// other character as it is.
        /// </summary>
        private static string AsciiLowerCase(string text)
        {
            var lower = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                lower.Append(c >= 'A' && c <= 'Z' ? (char)(c + ('a' - 'A')) : c);
            }
            return lower.ToString();
        }

        /// <summary>
        /// Where the authority ends, which is at the first '/' of the host,
        /// or at its end.
        /// </summary>
        private static int AuthorityEnd(string host)
        {
            var end = host.IndexOf('/');
            return end < 0 ? host.Length : end;
        }

        /// <summary>
        /// The host and optional port in lower case, or null when they are
        /// not valid. The host is an IPv6 address in brackets, or a name of
        /// labels separated by dots with a dot allowed at the end, which
        /// must be an IPv4 address when its last label is a number. The port
        /// is ':' followed by digits up to 65535, or by nothing.
        /// </summary>
        private static string CheckedHostPort(string hostPort)
        {
            string host;
            string port;
            if (hostPort.Length > 0 && hostPort[0] == '[')
            {
                var close = hostPort.IndexOf(']');
                if (close < 0)
                {
                    return null;
                }
                var address = hostPort.Substring(1, close - 1);
                if (IsIpv6(address) == false)
                {
                    return null;
                }
                host = hostPort.Substring(0, close + 1);
                port = hostPort.Substring(close + 1);
            }
            else
            {
                var colon = hostPort.IndexOf(':');
                var end = colon < 0 ? hostPort.Length : colon;
                host = hostPort.Substring(0, end);
                port = hostPort.Substring(end);
                var name = host.Length > 0 && host[host.Length - 1] == '.'
                    ? host.Substring(0, host.Length - 1)
                    : host;
                var labels = name.Split('.');
                foreach (var label in labels)
                {
                    if (Label.IsMatch(label) == false)
                    {
                        return null;
                    }
                }
                if (IsNumber(labels[labels.Length - 1]) &&
                    Ipv4.IsMatch(host) == false)
                {
                    return null;
                }
            }
            if (port.Length > 0 && IsPort(port) == false)
            {
                return null;
            }
            return AsciiLowerCase(host) + port;
        }

        /// <summary>
        /// Whether a label is only decimal digits, or is "0x" or "0X"
        /// followed by hexadecimal digits, which the URL Standard reads as
        /// the last part of an IPv4 address.
        /// </summary>
        private static bool IsNumber(string label)
        {
            var hex = label.Length >= 2 && label[0] == '0' &&
                (label[1] == 'x' || label[1] == 'X');
            for (var i = hex ? 2 : 0; i < label.Length; i++)
            {
                var c = label[i];
                if ((c >= '0' && c <= '9') == false &&
                    (hex && ((c >= 'a' && c <= 'f') ||
                        (c >= 'A' && c <= 'F'))) == false)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Whether the text is ':' followed by digits whose value is no more
        /// than 65535, or by nothing.
        /// </summary>
        private static bool IsPort(string text)
        {
            if (Port.IsMatch(text) == false)
            {
                return false;
            }
            var digits = text.Substring(1).TrimStart('0');
            return digits.Length <= MaxPortDigits &&
                (digits.Length == 0 ||
                    int.Parse(digits, NumberStyles.None,
                        CultureInfo.InvariantCulture) <= MaxPort);
        }

        /// <summary>
        /// Whether the text is an IPv6 address as RFC 3986 section 3.2.2
        /// defines one. That is eight groups of one to four hexadecimal
        /// digits separated by ':', where one '::' can stand for one or more
        /// groups of zeros and the last two groups can be written as an IPv4
        /// address.
        /// </summary>
        private static bool IsIpv6(string text)
        {
            var gap = text.IndexOf("::", StringComparison.Ordinal);
            if (gap < 0)
            {
                return Groups(text, true) == 8;
            }
            if (text.IndexOf("::", gap + 1, StringComparison.Ordinal) >= 0)
            {
                return false;
            }
            var head = Groups(text.Substring(0, gap), false);
            var tail = Groups(text.Substring(gap + 2), true);
            return head >= 0 && tail >= 0 && head + tail <= 7;
        }

        /// <summary>
        /// The number of 16 bit groups in a run of groups separated by ':',
        /// or -1 when the run is not valid. An empty run has none, and an
        /// IPv4 address at the end of the run, where one is allowed, counts
        /// as two.
        /// </summary>
        private static int Groups(string run, bool ipv4AtEnd)
        {
            if (run.Length == 0)
            {
                return 0;
            }
            var parts = run.Split(':');
            var count = 0;
            for (var i = 0; i < parts.Length; i++)
            {
                if (HexGroup.IsMatch(parts[i]))
                {
                    count++;
                }
                else if (ipv4AtEnd && i == parts.Length - 1 &&
                    Ipv4.IsMatch(parts[i]))
                {
                    count += 2;
                }
                else
                {
                    return -1;
                }
            }
            return count;
        }

        /// <summary>
        /// The text with each byte of its UTF-8 encoding percent-encoded
        /// where the byte is a control character, a space, outside ASCII or
        /// one of <see cref="Encoded"/>, and with a '%' encoded where two
        /// hexadecimal digits do not follow it. A lone surrogate has no UTF-8
        /// encoding, and <see cref="Encoding.UTF8"/> writes it as U+FFFD, as
        /// a browser does.
        /// </summary>
        private static string Encode(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            var output = new StringBuilder(bytes.Length);
            for (var i = 0; i < bytes.Length; i++)
            {
                var b = bytes[i];
                var keep = b == '%'
                    ? i + 2 < bytes.Length &&
                        IsHexDigit(bytes[i + 1]) && IsHexDigit(bytes[i + 2])
                    : b > ' ' && b < 0x7F && Encoded.IndexOf((char)b) < 0;
                if (keep)
                {
                    output.Append((char)b);
                }
                else
                {
                    output.Append('%')
                        .Append(HexDigits[b >> 4])
                        .Append(HexDigits[b & 0x0F]);
                }
            }
            return output.ToString();
        }

        private static bool IsHexDigit(byte b)
        {
            return (b >= '0' && b <= '9') ||
                (b >= 'A' && b <= 'F') ||
                (b >= 'a' && b <= 'f');
        }
    }
}
