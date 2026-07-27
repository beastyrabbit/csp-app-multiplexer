using System.Net;

namespace CspMultiplexer.Broker;

public sealed record CompanionMultiplexerOptions
{
    public IPAddress ListenAddress { get; init; } = IPAddress.Loopback;

    public bool AllowLan { get; init; }

    public ushort Port { get; init; }

    public int MaximumClients { get; init; } = 8;

    public int MaximumConcurrentReads { get; init; } = 4;

    public int MaximumPendingPushesPerClient { get; init; } = 64;

    public int MaximumFrameLength { get; init; } = 32 * 1024 * 1024;
}
