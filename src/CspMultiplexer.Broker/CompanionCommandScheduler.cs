using CspMultiplexer.Protocol;

namespace CspMultiplexer.Broker;

internal sealed class CompanionCommandScheduler : IDisposable
{
    private static readonly HashSet<string> MutatingCommands = new(StringComparer.Ordinal)
    {
        "SetCurrentColor",
        "SetColorSelectionModel",
        "SetBrushSize",
        "SetAlpha",
        "DoGesture",
        "DoNavigator",
        "DoQuickAccess",
        "SetServerSelectedTabKind",
        "DoModeChange",
    };

    private readonly SemaphoreSlim mutationQueue = new(1, 1);
    private readonly SemaphoreSlim readConcurrency;

    public CompanionCommandScheduler(int maximumConcurrentReads)
    {
        if (maximumConcurrentReads is < 1 or > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumConcurrentReads));
        }

        readConcurrency = new SemaphoreSlim(maximumConcurrentReads, maximumConcurrentReads);
    }

    public async Task<CompanionFrame> ExecuteAsync(
        string command,
        Func<CancellationToken, Task<CompanionFrame>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(operation);
        var gate = MutatingCommands.Contains(command) ? mutationQueue : readConcurrency;
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await operation(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose()
    {
        mutationQueue.Dispose();
        readConcurrency.Dispose();
    }
}
