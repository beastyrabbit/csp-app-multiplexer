using CspMultiplexer.Protocol;

namespace CspMultiplexer.Broker;

public sealed class CompanionServerPushEventArgs(CompanionFrame frame) : EventArgs
{
    public CompanionFrame Frame { get; } = frame;
}

public interface ICompanionUpstream : IAsyncDisposable
{
    event EventHandler<CompanionServerPushEventArgs>? ServerPushReceived;

    bool IsAuthenticated { get; }

    CompanionAuthResult Authentication { get; }

    Task<CompanionFrame> SendRawAsync(
        string command,
        ReadOnlyMemory<byte> rawDetail,
        ReadOnlyMemory<byte> binaryTail = default,
        CancellationToken cancellationToken = default);
}
