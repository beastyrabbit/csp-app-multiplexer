using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace CspMultiplexer.Protocol;

public sealed record CompanionPairingInfo(
    IReadOnlyList<IPAddress> Addresses,
    ushort Port,
    string Password,
    string Generation);

public static partial class CompanionPairingCodec
{
    private const string ExpectedHost = "companion.clip-studio.com";
    private static readonly byte[] RemoteParameterKey = [0x74, 0xB2, 0x92, 0x5B, 0x4A, 0x21, 0xDA];

    public static string Encode(CompanionPairingInfo pairing, string locale = "en-us")
    {
        ArgumentNullException.ThrowIfNull(pairing);
        if (pairing.Addresses.Count == 0)
        {
            throw new ArgumentException("At least one pairing address is required.", nameof(pairing));
        }

        if (!Locale().IsMatch(locale))
        {
            throw new ArgumentException("Locale must be a two-letter language with an optional region.", nameof(locale));
        }

        if (pairing.Port == 0 ||
            string.IsNullOrEmpty(pairing.Password) ||
            string.IsNullOrEmpty(pairing.Generation))
        {
            throw new ArgumentException("Pairing port, password, and generation are required.", nameof(pairing));
        }

        foreach (var address in pairing.Addresses)
        {
            if (!IsPrivateOrLocal(address))
            {
                throw new InvalidOperationException($"Refusing to advertise non-private address {address}.");
            }
        }

        var plainText = string.Join(',', pairing.Addresses.Select(address => address.ToString())) + '\t' +
                        pairing.Port.ToString(CultureInfo.InvariantCulture) + '\t' +
                        pairing.Password + '\t' +
                        pairing.Generation;
        var bytes = Encoding.UTF8.GetBytes(plainText);
        XorInPlace(bytes, RemoteParameterKey);
        return $"https://{ExpectedHost}/rc/{locale}?s={Convert.ToHexString(bytes).ToLowerInvariant()}";
    }

    public static CompanionPairingInfo Decode(string pairingUrl, bool allowPublicEndpoints = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pairingUrl);

        if (!Uri.TryCreate(pairingUrl, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(uri.IdnHost, ExpectedHost, StringComparison.OrdinalIgnoreCase) ||
            !uri.IsDefaultPort ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            !PairingPath().IsMatch(uri.AbsolutePath))
        {
            throw new FormatException("The value is not an exact CLIP STUDIO companion pairing URL.");
        }

        var query = ParseQuery(uri.Query);
        if (query.Count != 1 || !query.TryGetValue("s", out var encoded) || string.IsNullOrEmpty(encoded))
        {
            throw new FormatException("The pairing URL must contain only one non-empty 's' parameter.");
        }

        byte[] obfuscated;
        try
        {
            obfuscated = Convert.FromHexString(encoded);
        }
        catch (FormatException ex)
        {
            throw new FormatException("The pairing parameter is not valid hexadecimal.", ex);
        }

        XorInPlace(obfuscated, RemoteParameterKey);
        var parts = Encoding.UTF8.GetString(obfuscated).Split('\t');
        if (parts.Length != 4)
        {
            throw new FormatException("The decoded pairing parameter must contain four fields.");
        }

        var addressStrings = parts[0].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (addressStrings.Length == 0)
        {
            throw new FormatException("The pairing parameter contains no endpoint addresses.");
        }

        var addresses = new List<IPAddress>(addressStrings.Length);
        foreach (var text in addressStrings)
        {
            if (!IPAddress.TryParse(text, out var address))
            {
                throw new FormatException($"'{text}' is not a valid IP address.");
            }

            if (!allowPublicEndpoints && !IsPrivateOrLocal(address))
            {
                throw new InvalidOperationException($"The pairing endpoint {address} is not a private or local address.");
            }

            addresses.Add(address);
        }

        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var port) ||
            port is < 1 or > ushort.MaxValue)
        {
            throw new FormatException("The pairing port must be between 1 and 65535.");
        }

        if (string.IsNullOrEmpty(parts[2]) || string.IsNullOrEmpty(parts[3]))
        {
            throw new FormatException("The pairing password and generation must not be empty.");
        }

        return new CompanionPairingInfo(addresses, (ushort)port, parts[2], parts[3]);
    }

    public static bool TryDecode(
        string pairingUrl,
        out CompanionPairingInfo? pairing,
        bool allowPublicEndpoints = false)
    {
        try
        {
            pairing = Decode(pairingUrl, allowPublicEndpoints);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException)
        {
            pairing = null;
            return false;
        }
    }

    public static bool IsPrivateOrLocal(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return bytes[0] == 10 ||
                   bytes[0] == 127 ||
                   (bytes[0] == 169 && bytes[1] == 254) ||
                   (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                   (bytes[0] == 192 && bytes[1] == 168);
        }

        return address.IsIPv6LinkLocal ||
               address.IsIPv6SiteLocal ||
               (bytes[0] & 0xFE) == 0xFC;
    }

    internal static void XorInPlace(Span<byte> value, ReadOnlySpan<byte> key)
    {
        for (var i = 0; i < value.Length; i++)
        {
            value[i] ^= key[i % key.Length];
        }
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            var name = Uri.UnescapeDataString(separator < 0 ? part : part[..separator]);
            var value = Uri.UnescapeDataString(separator < 0 ? string.Empty : part[(separator + 1)..]);
            if (!result.TryAdd(name, value))
            {
                throw new FormatException($"Duplicate query parameter '{name}'.");
            }
        }

        return result;
    }

    [GeneratedRegex("^/rc/[a-z]{2}(?:-[a-z]{2})?$", RegexOptions.CultureInvariant)]
    private static partial Regex PairingPath();

    [GeneratedRegex("^[a-z]{2}(?:-[a-z]{2})?$", RegexOptions.CultureInvariant)]
    private static partial Regex Locale();
}
