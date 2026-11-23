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

using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace FiftyOne.IpIntelligence.Engine.OnPremise
{
    /// <summary>
    /// Reads the client IP address from an evidence value, only when all of
    /// the value is one address, optionally with a port. A list, a space or
    /// line ending before or after the address, a zone index and any other
    /// text make the value unread, so that no part of it is taken for an
    /// address the sender did not give.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/67ead6986f3118c68f0714325800b92d48ddc71f/ip-intelligence-specification/pipeline-elements/ip-intelligence-on-premise.md#reading-the-client-ip-value">Specification</see>
    /// </summary>
    internal static class ClientIpParser
    {
        /// <summary>
        /// Reads an IP address from a raw client-ip evidence value.
        /// </summary>
        /// <param name="rawValue">
        /// The evidence value. May be null, and may carry a port.
        /// </param>
        /// <param name="address">
        /// The address read from the value. An IPv4-mapped address is its
        /// IPv4 address.
        /// </param>
        /// <param name="addressText">
        /// The canonical textual form of <paramref name="address"/>, for
        /// passing to services that take a string, without any port or
        /// brackets.
        /// </param>
        /// <returns>True when an address was read.</returns>
        public static bool TryParse(
            string rawValue,
            out IPAddress address,
            out string addressText)
        {
            address = null;
            addressText = null;
            if (string.IsNullOrEmpty(rawValue) ||
                HasOnlyAddressCharacters(rawValue) == false)
            {
                return false;
            }
            var candidate = rawValue;

            // Brackets are excluded from the fast path below deliberately.
            // On some runtimes IPAddress.TryParse accepts the bracketed IPv6
            // form and silently discards any port on it, so "[2001:db8::1]:443"
            // would parse as an address while the text stayed bracketed and
            // ported. Anything bracketed is handled explicitly and reported
            // in canonical form.
            if (candidate[0] == '[')
            {
                return TryParseBracketed(candidate, out address, out addressText);
            }

            // A bare address. The text reported is the canonical form of the
            // address read, so an IPv4-mapped address reports its IPv4
            // address and a shortened IPv6 address reports one spelling.
            if (IsReadableAddressText(candidate) &&
                IPAddress.TryParse(candidate, out address))
            {
                address = ToLookupAddress(address);
                addressText = address.ToString();
                return true;
            }

            // Falls here when an IPv4 address carries a port, which
            // IPAddress.TryParse rejects. An unbracketed IPv6 address keeps
            // its colons: without brackets a trailing ":443" is
            // indistinguishable from part of the address, so no port
            // splitting is attempted for IPv6.
            var portSeparatorIndex = candidate.LastIndexOf(':');
            if (portSeparatorIndex > 0 &&
                IsValidPort(candidate.Substring(portSeparatorIndex + 1)))
            {
                var addressPart = candidate.Substring(0, portSeparatorIndex);
                if (IsReadableAddressText(addressPart) &&
                    IPAddress.TryParse(addressPart, out address) &&
                    address.AddressFamily == AddressFamily.InterNetwork)
                {
                    addressText = address.ToString();
                    return true;
                }
            }

            address = null;
            return false;
        }

        /// <summary>
        /// Parses "[address]" or "[address]:port" where the bracketed
        /// address is IPv6.
        /// </summary>
        private static bool TryParseBracketed(
            string candidate,
            out IPAddress address,
            out string addressText)
        {
            address = null;
            addressText = null;

            var closingBracketIndex = candidate.IndexOf(']');
            if (closingBracketIndex < 2)
            {
                return false;
            }

            var afterBracket = candidate.Substring(closingBracketIndex + 1);
            if (afterBracket.Length > 0 &&
                (afterBracket[0] != ':' ||
                IsValidPort(afterBracket.Substring(1)) == false))
            {
                return false;
            }

            var bracketedAddress = candidate.Substring(1, closingBracketIndex - 1);
            if (IsReadableAddressText(bracketedAddress) &&
                IPAddress.TryParse(bracketedAddress, out address) &&
                address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                address = ToLookupAddress(address);
                addressText = address.ToString();
                return true;
            }

            address = null;
            return false;
        }

        /// <summary>
        /// Returns the address a lookup uses, which is the IPv4 address for
        /// an IPv4-mapped address and otherwise the address itself.
        /// MapToIPv4 takes the last four bytes of any IPv6 address, so
        /// it is only called on a mapped one. The
        /// <see href="https://github.com/51Degrees/specifications/blob/67ead6986f3118c68f0714325800b92d48ddc71f/ip-intelligence-specification/pipeline-elements/ip-intelligence-on-premise.md#address-forms">specification</see>
        /// defines which forms are mapped.
        /// </summary>
        private static IPAddress ToLookupAddress(IPAddress address)
        {
            return address.IsIPv4MappedToIPv6
                ? address.MapToIPv4()
                : address;
        }

        /// <summary>
        /// True if every character can be part of an address with a port,
        /// being a hexadecimal digit, '.', ':', '[' or ']'. A list, a space,
        /// a tab, a line ending, a zone index's '%' and a prefix length's '/'
        /// all fail this check.
        /// </summary>
        private static bool HasOnlyAddressCharacters(string value)
        {
            foreach (var character in value)
            {
                var allowed =
                    (character >= '0' && character <= '9') ||
                    (character >= 'a' && character <= 'f') ||
                    (character >= 'A' && character <= 'F') ||
                    character == '.' || character == ':' ||
                    character == '[' || character == ']';
                if (allowed == false)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// True when the text is a plain dotted-quad IPv4 address, or an IPv6
        /// value whose embedded IPv4 part, where it has one, is a dotted quad
        /// as well. RFC 4291 section 2.2 puts an IPv4 address only in the last
        /// 32 bits of an IPv6 address, so the text after the last ':' is the
        /// only part that can be dotted.
        /// </summary>
        private static bool IsReadableAddressText(string text)
        {
            var lastColonIndex = text.LastIndexOf(':');
            if (lastColonIndex >= 0)
            {
                var embeddedIpV4 = text.Substring(lastColonIndex + 1);
                return embeddedIpV4.IndexOf('.') < 0 ||
                    IsDottedQuad(embeddedIpV4);
            }
            return IsDottedQuad(text);
        }

        /// <summary>
        /// True when the text is four parts of one to three decimal digits
        /// with no leading zero, which is RFC 3986's IPv4address built from
        /// dec-octet. .NET additionally accepts inet_aton IPv4 forms - a bare
        /// number ("3232235521" -> 192.168.0.1), fewer than four parts
        /// ("1.2.3"), and octal octets ("012.1.2.3" -> 10.1.2.3, which the
        /// native engine's atoi reads as 12.1.2.3) - and it reads a leading
        /// zero in the last part of an embedded IPv4 address
        /// ("::ffff:1.2.3.04" -> "::ffff:1.2.3.4"). None of those are a client
        /// IP: accepting them lets numeric junk count as a valid address and
        /// outrank a real one, and resolves a different address than the
        /// native lookup.
        /// </summary>
        private static bool IsDottedQuad(string text)
        {
            var octets = text.Split('.');
            if (octets.Length != 4)
            {
                return false;
            }

            foreach (var octet in octets)
            {
                if (octet.Length == 0 || octet.Length > 3 ||
                    (octet.Length > 1 && octet[0] == '0'))
                {
                    return false;
                }
                foreach (var character in octet)
                {
                    if (character < '0' || character > '9')
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool IsValidPort(string portText)
        {
            // NumberStyles.None rejects signs and whitespace, so only a
            // plain digit run within ushort range passes.
            return portText.Length > 0 &&
                ushort.TryParse(
                    portText,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out _);
        }
    }
}
