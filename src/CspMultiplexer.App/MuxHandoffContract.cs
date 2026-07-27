// ═══ CSP SUITE SHARED FILE ══════════════════════════════════════════════════
// Reconcile with tools/suite-sync.ps1 (spec §0.1). Tier 2.
//   Companion : src/CspPaletteCompanion.App/MuxHandoffContract.cs
//   Mux       : src/CspMultiplexer.App/MuxHandoffContract.cs
// ════════════════════════════════════════════════════════════════════════════
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

// ── SYNC-LOCAL BEGIN ──
namespace CspMultiplexer.App;
// ── SYNC-LOCAL END ──

/// <summary>Everything both apps must agree on about the Mux session handoff file.</summary>
internal static class MuxHandoffContract
{
    internal const int SchemaVersion = 1;
    internal const string DirectoryName = "CSP Suite";
    internal const string FileName = "mux-session.json";
    internal const string TempPrefix = ".mux-session.json.";
    internal const string TempSuffix = ".tmp";
    internal const string MuxProcessName = "CSP Mux";     // == <AssemblyName>, §5.8
    internal const int MaximumFileBytes = 4096;

    internal static readonly TimeSpan StartTimeTolerance = TimeSpan.FromSeconds(1);

    internal static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), DirectoryName);

    internal static string FilePath => Path.Combine(DirectoryPath, FileName);

    // camelCase is set explicitly on the writer while the reader is also case-insensitive:
    // no compiler enforces this contract across two repositories, so a casing drift on
    // either side must not be able to break it silently.
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };
}

/// <summary>
/// Every member is <c>required</c>, so System.Text.Json raises JsonException when a
/// property is missing — which the reader already maps to Malformed. Verified by
/// execution: <c>required</c> covers an OMITTED property but NOT an explicit
/// <c>"pairingUrl": null</c>, and it does NOT cover a document that is the JSON
/// literal <c>null</c> (Deserialize returns null in that case even for a required
/// record). Both remaining holes are closed explicitly by the reader.
/// </summary>
internal sealed record MuxSessionDocument
{
    [JsonRequired] public required int SchemaVersion { get; init; }

    [JsonRequired] public required string PairingUrl { get; init; }

    [JsonRequired] public required int ProcessId { get; init; }

    [JsonRequired] public required DateTime ProcessStartTimeUtc { get; init; }
}
