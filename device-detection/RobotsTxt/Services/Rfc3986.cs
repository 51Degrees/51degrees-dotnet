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

namespace FiftyOne.DeviceDetection.RobotsTxt.Services;

/// <summary>
/// Checks text against the URI grammar in RFC 3986 section 3 and
/// Appendix A, and gives a URI's scheme and host in lower case, as that
/// RFC asks of a program that writes a URI. Nothing else is rewritten.
/// </summary>
internal static class Rfc3986
{
    /// <summary>
    /// The URI with its scheme and host in lower case, as RFC 3986
    /// sections 3.1 and 3.2.2 ask of a program that writes a URI. A
    /// percent-encoding in the host keeps upper case hex digits, as
    /// section 3.2.2 also asks. The user information, port, path, query
    /// and fragment are kept as given, and text that is not a URI is
    /// returned unchanged.
    /// </summary>
    internal static string LowerCaseSchemeAndHost(string value)
    {
        if (!IsUri(value))
        {
            return value;
        }
        var chars = value.ToCharArray();
        var colon = SchemeEnd(value);
        for (var i = 0; i < colon; i++)
        {
            chars[i] = ToLower(chars[i]);
        }

        if (string.CompareOrdinal(value, colon + 1, "//", 0, 2) == 0)
        {
            var start = colon + 3;
            var end = start;
            while (end < value.Length && "/?#".IndexOf(value[end]) < 0)
            {
                end++;
            }
            var at = value.IndexOf('@', start, end - start);
            var hostStart = at < 0 ? start : at + 1;
            int hostEnd;
            if (hostStart < end && value[hostStart] == '[')
            {
                hostEnd = value.IndexOf(']', hostStart) + 1;
            }
            else
            {
                var port = value.IndexOf(':', hostStart, end - hostStart);
                hostEnd = port < 0 ? end : port;
            }
            for (var i = hostStart; i < hostEnd; i++)
            {
                if (chars[i] == '%')
                {
                    chars[i + 1] = ToUpper(chars[i + 1]);
                    chars[i + 2] = ToUpper(chars[i + 2]);
                    i += 2;
                }
                else
                {
                    chars[i] = ToLower(chars[i]);
                }
            }
        }
        return new string(chars);
    }

    /// <summary>
    /// True if the text matches
    /// <c>scheme ":" hier-part [ "?" query ] [ "#" fragment ]</c>.
    /// </summary>
    internal static bool IsUri(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }
        var colon = SchemeEnd(value);
        if (colon < 0)
        {
            return false;
        }

        var end = value.Length;
        string fragment = null;
        var hash = value.IndexOf('#', colon + 1);
        if (hash >= 0)
        {
            fragment = value.Substring(hash + 1);
            end = hash;
        }

        string query = null;
        var hierEnd = end;
        var question = value.IndexOf('?', colon + 1, end - colon - 1);
        if (question >= 0)
        {
            query = value.Substring(question + 1, end - question - 1);
            hierEnd = question;
        }

        return IsHierPart(value.Substring(colon + 1, hierEnd - colon - 1)) &&
            (query == null || Check(query, ":@/?")) &&
            (fragment == null || Check(fragment, ":@/?"));
    }

    private static int SchemeEnd(string value)
    {
        if (!IsAlpha(value[0]))
        {
            return -1;
        }
        for (var i = 1; i < value.Length; i++)
        {
            var c = value[i];
            if (c == ':')
            {
                return i;
            }
            if (!IsAlpha(c) && !IsDigit(c) && c != '+' && c != '-' && c != '.')
            {
                return -1;
            }
        }
        return -1;
    }

    private static bool IsHierPart(string hier)
    {
        if (hier.StartsWith("//", StringComparison.Ordinal))
        {
            var slash = hier.IndexOf('/', 2);
            var authority = slash < 0
                ? hier.Substring(2)
                : hier.Substring(2, slash - 2);
            var path = slash < 0 ? string.Empty : hier.Substring(slash);
            return IsAuthority(authority) && Check(path, ":@/");
        }
        return Check(hier, ":@/");
    }

    private static bool IsAuthority(string authority)
    {
        var hostPort = authority;
        var at = authority.IndexOf('@');
        if (at >= 0)
        {
            if (!Check(authority.Substring(0, at), ":"))
            {
                return false;
            }
            hostPort = authority.Substring(at + 1);
        }

        if (hostPort.StartsWith("[", StringComparison.Ordinal))
        {
            var close = hostPort.IndexOf(']');
            if (close < 0 || !IsIpLiteral(hostPort.Substring(1, close - 1)))
            {
                return false;
            }
            var after = hostPort.Substring(close + 1);
            return after.Length == 0 ||
                (after[0] == ':' && AllDigits(after.Substring(1)));
        }

        var colon = hostPort.IndexOf(':');
        var host = colon < 0 ? hostPort : hostPort.Substring(0, colon);
        return Check(host, string.Empty) &&
            (colon < 0 || AllDigits(hostPort.Substring(colon + 1)));
    }

    private static bool IsIpLiteral(string literal)
    {
        if (literal.Length > 0 && (literal[0] == 'v' || literal[0] == 'V'))
        {
            return IsIpVFuture(literal);
        }
        return IsIpV6(literal);
    }

    private static bool IsIpVFuture(string literal)
    {
        var i = 1;
        while (i < literal.Length && IsHex(literal[i]))
        {
            i++;
        }
        if (i == 1 || i >= literal.Length - 1 || literal[i] != '.')
        {
            return false;
        }
        for (i++; i < literal.Length; i++)
        {
            var c = literal[i];
            if (!IsUnreserved(c) && !IsSubDelim(c) && c != ':')
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsIpV6(string address)
    {
        var gap = address.IndexOf("::", StringComparison.Ordinal);
        if (gap >= 0 &&
            address.IndexOf("::", gap + 1, StringComparison.Ordinal) >= 0)
        {
            return false;
        }

        var groups = 0;
        if (gap < 0)
        {
            return CountGroups(address, true, ref groups) && groups == 8;
        }
        return CountGroups(address.Substring(0, gap), false, ref groups) &&
            CountGroups(address.Substring(gap + 2), true, ref groups) &&
            groups <= 7;
    }

    // Counts 16 bit groups, where a trailing IPv4 address counts as two.
    private static bool CountGroups(
        string part,
        bool allowIpV4,
        ref int groups)
    {
        if (part.Length == 0)
        {
            return true;
        }
        var items = part.Split(':');
        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            if (i == items.Length - 1 && allowIpV4 && item.Contains('.'))
            {
                if (!IsIpV4(item))
                {
                    return false;
                }
                groups += 2;
            }
            else
            {
                if (item.Length < 1 || item.Length > 4 || !AllHex(item))
                {
                    return false;
                }
                groups++;
            }
        }
        return true;
    }

    private static bool IsIpV4(string address)
    {
        var octets = address.Split('.');
        if (octets.Length != 4)
        {
            return false;
        }
        foreach (var octet in octets)
        {
            if (octet.Length < 1 || octet.Length > 3 || !AllDigits(octet) ||
                (octet.Length > 1 && octet[0] == '0') ||
                int.Parse(octet) > 255)
            {
                return false;
            }
        }
        return true;
    }

    // True if every character is unreserved, a sub-delimiter, one of the
    // extra characters, or a '%' followed by two hex digits.
    private static bool Check(string text, string extra)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '%')
            {
                if (i + 2 >= text.Length ||
                    !IsHex(text[i + 1]) ||
                    !IsHex(text[i + 2]))
                {
                    return false;
                }
                i += 2;
            }
            else if (!IsUnreserved(c) &&
                !IsSubDelim(c) &&
                extra.IndexOf(c) < 0)
            {
                return false;
            }
        }
        return true;
    }

    private static bool AllDigits(string text)
    {
        foreach (var c in text)
        {
            if (!IsDigit(c))
            {
                return false;
            }
        }
        return true;
    }

    private static bool AllHex(string text)
    {
        foreach (var c in text)
        {
            if (!IsHex(c))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsAlpha(char c) =>
        (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');

    private static char ToLower(char c) =>
        c >= 'A' && c <= 'Z' ? (char)(c + ('a' - 'A')) : c;

    private static char ToUpper(char c) =>
        c >= 'a' && c <= 'z' ? (char)(c - ('a' - 'A')) : c;

    private static bool IsDigit(char c) => c >= '0' && c <= '9';

    private static bool IsHex(char c) =>
        IsDigit(c) || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f');

    private static bool IsUnreserved(char c) =>
        IsAlpha(c) || IsDigit(c) || c == '-' || c == '.' || c == '_' ||
        c == '~';

    private static bool IsSubDelim(char c) =>
        "!$&'()*+,;=".IndexOf(c) >= 0;
}
