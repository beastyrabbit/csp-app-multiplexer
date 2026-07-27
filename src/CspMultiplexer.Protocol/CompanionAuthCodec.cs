using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CspMultiplexer.Protocol;

public sealed record CompanionAuthRequest(
    string Generation,
    string CurrentPassword,
    string NewPassword);

public sealed record CompanionAuthResult(
    bool IsAuthenticated,
    string? ErrorReason,
    string? ServerSpecVersion,
    bool IsQuickAccessAvailable);

public static class CompanionAuthCodec
{
    public const string ReconnectionMarker = "{{(([[reconnection request marker]]))}}\r\n";
    private static readonly byte[] AuthenticationKey = [0xB6, 0xD5, 0x92, 0xC4, 0xA7, 0x83, 0xE1];

    public static string Encrypt(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var bytes = Encoding.UTF8.GetBytes(value);
        CompanionPairingCodec.XorInPlace(bytes, AuthenticationKey);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string Decrypt(string encryptedHex)
    {
        ArgumentNullException.ThrowIfNull(encryptedHex);
        var bytes = Convert.FromHexString(encryptedHex);
        CompanionPairingCodec.XorInPlace(bytes, AuthenticationKey);
        return Encoding.UTF8.GetString(bytes);
    }

    public static string CreateRandomPassword()
    {
        Span<byte> bytes = stackalloc byte[6];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=');
    }

    public static byte[] CreateAuthenticationDetail(string generation, string currentPassword, string newPassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(generation);
        ArgumentNullException.ThrowIfNull(currentPassword);
        ArgumentNullException.ThrowIfNull(newPassword);
        return JsonSerializer.SerializeToUtf8Bytes(
            new[] { generation, Encrypt(currentPassword), Encrypt(newPassword) });
    }

    public static CompanionAuthRequest ParseRequest(CompanionFrame frame)
    {
        if (frame.Command != "Authenticate" ||
            frame.Type != CompanionFrameType.Command ||
            frame.Detail is not { ValueKind: JsonValueKind.Array } detail)
        {
            throw new InvalidDataException("Expected an Authenticate command with an array detail.");
        }

        var fields = detail.EnumerateArray().ToArray();
        if (fields.Length != 3 || fields.Any(field => field.ValueKind != JsonValueKind.String))
        {
            throw new InvalidDataException("Authenticate detail must contain exactly three strings.");
        }

        try
        {
            return new CompanionAuthRequest(
                fields[0].GetString()!,
                Decrypt(fields[1].GetString()!),
                Decrypt(fields[2].GetString()!));
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException("Authenticate contains an invalid encrypted token.", ex);
        }
    }

    public static byte[] CreateResultDetail(
        string errorReason,
        string serverSpecVersion,
        bool isQuickAccessAvailable) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            AuthErrorReason = errorReason,
            RemoteCommandSpecVersionOfServer = serverSpecVersion,
            IsQuickAccessAvailable = isQuickAccessAvailable,
        });

    public static CompanionAuthResult ParseResult(CompanionFrame frame)
    {
        if (frame.RawDetail.Length == 0)
        {
            return new CompanionAuthResult(
                frame.Type != CompanionFrameType.Error,
                frame.Type == CompanionFrameType.Error ? "EmptyResponse" : null,
                null,
                false);
        }

        if (frame.Detail is { ValueKind: JsonValueKind.Object } detail)
        {
            var reason = detail.TryGetProperty("AuthErrorReason", out var reasonValue)
                ? reasonValue.GetString()
                : null;
            var version = detail.TryGetProperty("RemoteCommandSpecVersionOfServer", out var versionValue)
                ? versionValue.GetString()
                : null;
            var quickAccess = detail.TryGetProperty("IsQuickAccessAvailable", out var quickAccessValue) &&
                              quickAccessValue.ValueKind is JsonValueKind.True;
            var failed = reason is "VersionMismatch" or "PasswordMismatch" or "ServerUnready";
            return new CompanionAuthResult(!failed, failed ? reason : null, version, quickAccess);
        }

        return new CompanionAuthResult(true, null, null, false);
    }
}
