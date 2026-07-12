using System.Net;
using System.Net.Sockets;

namespace AndreGoepel.UrlShortener.Services;

/// <summary>
/// Validates and normalises a user-supplied destination URL before it is shortened. Because
/// creation is public and anonymous, this is the first line of abuse defence: only http/https
/// is allowed, and targets pointing at loopback / private / link-local addresses are rejected
/// so the shortener can't be used to hand out links into internal networks.
/// </summary>
public sealed class UrlValidator
{
    public const int MaxUrlLength = 2048;

    public bool TryValidate(string? input, out string normalized, out string? error)
    {
        normalized = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Please enter a URL.";
            return false;
        }

        input = input.Trim();
        if (input.Length > MaxUrlLength)
        {
            error = $"That URL is too long (maximum {MaxUrlLength} characters).";
            return false;
        }

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            error = "That does not look like a valid absolute URL.";
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            error = "Only http and https URLs are allowed.";
            return false;
        }

        if (IsDisallowedHost(uri))
        {
            error = "URLs pointing to local or private addresses are not allowed.";
            return false;
        }

        normalized = uri.AbsoluteUri;
        return true;
    }

    private static bool IsDisallowedHost(Uri uri)
    {
        if (
            uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6
            && IPAddress.TryParse(uri.Host, out var ip)
        )
        {
            return IsPrivate(ip);
        }

        var host = uri.Host;
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPrivate(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return b[0] == 10
                || (b[0] == 172 && b[1] is >= 16 and <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 169 && b[1] == 254); // link-local
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return ip.IsIPv6LinkLocal
                || ip.IsIPv6SiteLocal
                || ip.Equals(IPAddress.IPv6Loopback)
                || IsUniqueLocal(ip);
        }

        return false;
    }

    // fc00::/7 — IPv6 unique local addresses.
    private static bool IsUniqueLocal(IPAddress ip) => (ip.GetAddressBytes()[0] & 0xFE) == 0xFC;
}
