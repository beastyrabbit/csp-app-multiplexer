using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using CspMultiplexer.Protocol;

namespace CspMultiplexer.App;

// Positional records tolerate missing trailing properties on deserialisation, so a
// settings.json written before the window position existed loads unchanged.
internal sealed record AppPreferences(
    string ListenAddress,
    bool HideQrAfterFirstConnection = false,
    double? WindowLeft = null,
    double? WindowTop = null,
    bool RunInTray = true,
    bool TrayHintShown = false)
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CSP App Multiplexer");

    public static string SettingsPath { get; } = Path.Combine(SettingsDirectory, "settings.json");

    public static AppPreferences Load()
    {
        try
        {
            return File.Exists(SettingsPath)
                ? JsonSerializer.Deserialize<AppPreferences>(File.ReadAllText(SettingsPath)) ??
                  new AppPreferences(IPAddress.Loopback.ToString())
                : new AppPreferences(IPAddress.Loopback.ToString());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppPreferences(IPAddress.Loopback.ToString());
        }
    }

    /// <summary>
    /// Writes the file. Returns null on success, or a one-line failure for the settings
    /// notice — save-on-change runs from synchronous void handlers, where a thrown
    /// <see cref="IOException"/> would be unhandled and take the process down.
    /// </summary>
    public static string? Save(AppPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            File.WriteAllText(
                SettingsPath,
                JsonSerializer.Serialize(preferences, new JsonSerializerOptions { WriteIndented = true }));
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return $"Settings could not be saved: {ex.Message}";
        }
    }

    /// <summary>
    /// Parses the saved address without touching the network stack.
    /// </summary>
    /// <remarks>
    /// Deliberately does not verify the address against the live adapters. Validation walks
    /// every interface, which a stalled VPN adapter can block for seconds, and this runs in
    /// the window constructor — before the window is on screen. The adapter walk belongs to
    /// the async load in <c>Window_Loaded</c>, which already surfaces a saved-but-absent
    /// address as a disabled picker entry rather than silently reverting to loopback.
    /// </remarks>
    public IPAddress ParseAddress() =>
        IPAddress.TryParse(ListenAddress, out var requested) ? requested : IPAddress.Loopback;
}

internal sealed record NetworkChoice(string Label, IPAddress Address);

internal static class NetworkDiscovery
{
    public static IReadOnlyList<NetworkChoice> GetChoices()
    {
        var choices = new List<NetworkChoice>
        {
            new("This computer only · 127.0.0.1", IPAddress.Loopback),
        };

        var seen = new HashSet<IPAddress>();
        foreach (var network in NetworkInterface.GetAllNetworkInterfaces()
                     .Where(value => value.OperationalStatus == OperationalStatus.Up &&
                                     value.NetworkInterfaceType is not NetworkInterfaceType.Loopback))
        {
            foreach (var address in network.GetIPProperties().UnicastAddresses
                         .Select(value => value.Address)
                         .Where(value => value.AddressFamily == AddressFamily.InterNetwork &&
                                         !IPAddress.IsLoopback(value) &&
                                         CompanionPairingCodec.IsPrivateOrLocal(value)))
            {
                if (seen.Add(address))
                {
                    choices.Add(new NetworkChoice($"{address} · {network.Name}", address));
                }
            }
        }

        return choices;
    }
}
