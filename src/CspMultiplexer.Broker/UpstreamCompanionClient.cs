using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using CspMultiplexer.Protocol;

namespace CspMultiplexer.Broker;

public sealed class UpstreamCompanionClient : ICompanionUpstream
{
    private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(15);
    private readonly Stream stream;
    private readonly TcpClient tcpClient;
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private readonly ConcurrentDictionary<uint, TaskCompletionSource<CompanionFrame>> pending = new();
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private Task? receiveTask;
    private Task? heartbeatTask;
    private int nextSerial = -1;
    private int disposed;
    private int disconnected;
    private bool isAuthenticated;

    private UpstreamCompanionClient(TcpClient tcpClient)
    {
        this.tcpClient = tcpClient;
        stream = tcpClient.GetStream();
        Authentication = new CompanionAuthResult(false, "ServerUnready", null, false);
    }

    public event EventHandler<CompanionServerPushEventArgs>? ServerPushReceived;

    public event EventHandler<CompanionDisconnectedEventArgs>? Disconnected;

    public bool IsAuthenticated => Volatile.Read(ref isAuthenticated);

    public CompanionAuthResult Authentication { get; private set; }

    public EndPoint? RemoteEndPoint => tcpClient.Client.RemoteEndPoint;

    public static async Task<UpstreamCompanionClient> ConnectAndAuthenticateAsync(
        CompanionPairingInfo pairing,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pairing);
        Exception? lastError = null;

        foreach (var address in pairing.Addresses)
        {
            var tcpClient = new TcpClient(address.AddressFamily);
            UpstreamCompanionClient? client = null;
            try
            {
                await tcpClient.ConnectAsync(address, pairing.Port, cancellationToken).ConfigureAwait(false);
                client = new UpstreamCompanionClient(tcpClient);
                client.StartReceiveLoop();
                await client.AuthenticateAsync(pairing, cancellationToken).ConfigureAwait(false);
                return client;
            }
            catch (Exception ex) when (ex is SocketException or IOException)
            {
                lastError = ex;
                await DisposeFailedConnectionAsync(client, tcpClient).ConfigureAwait(false);
            }
            catch
            {
                await DisposeFailedConnectionAsync(client, tcpClient).ConfigureAwait(false);
                throw;
            }
        }

        throw new IOException("Could not connect to any CLIP STUDIO companion endpoint.", lastError);
    }

    public static Task<UpstreamCompanionClient> ConnectAndAuthenticateAsync(
        string pairingUrl,
        CancellationToken cancellationToken = default) =>
        ConnectAndAuthenticateAsync(CompanionPairingCodec.Decode(pairingUrl), cancellationToken);

    public async Task<CompanionFrame> SendRawAsync(
        string command,
        ReadOnlyMemory<byte> rawDetail,
        ReadOnlyMemory<byte> binaryTail = default,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (!IsAuthenticated && command != "Authenticate")
        {
            throw new InvalidOperationException("The upstream CSP connection is not authenticated.");
        }

        var serial = unchecked((uint)Interlocked.Increment(ref nextSerial));
        var completion = new TaskCompletionSource<CompanionFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!pending.TryAdd(serial, completion))
        {
            throw new InvalidOperationException($"An upstream request with serial {serial} is already pending.");
        }

        try
        {
            var encoded = CompanionFrameCodec.EncodeRaw(
                CompanionFrameType.Command,
                command,
                serial,
                rawDetail.Span,
                binaryTail.Span);
            await WriteFrameAsync(encoded, cancellationToken).ConfigureAwait(false);

            using var timeout = new CancellationTokenSource(DefaultRequestTimeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                lifetimeCancellation.Token,
                timeout.Token);
            return await completion.Task.WaitAsync(linked.Token).ConfigureAwait(false);
        }
        finally
        {
            pending.TryRemove(serial, out _);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        Volatile.Write(ref isAuthenticated, false);
        await lifetimeCancellation.CancelAsync().ConfigureAwait(false);
        tcpClient.Dispose();
        await stream.DisposeAsync().ConfigureAwait(false);

        var closed = new ObjectDisposedException(nameof(UpstreamCompanionClient));
        foreach (var completion in pending.Values)
        {
            completion.TrySetException(closed);
        }

        await IgnoreExpectedShutdownAsync(receiveTask).ConfigureAwait(false);
        await IgnoreExpectedShutdownAsync(heartbeatTask).ConfigureAwait(false);
        writeLock.Dispose();
        lifetimeCancellation.Dispose();
    }

    private async Task AuthenticateAsync(
        CompanionPairingInfo pairing,
        CancellationToken cancellationToken)
    {
        var rotatedPassword = CompanionAuthCodec.CreateRandomPassword();
        var detail = CompanionAuthCodec.CreateAuthenticationDetail(
            pairing.Generation,
            pairing.Password,
            rotatedPassword);
        var response = await SendRawAsync("Authenticate", detail, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        Authentication = CompanionAuthCodec.ParseResult(response);
        if (!Authentication.IsAuthenticated)
        {
            throw new UnauthorizedAccessException(
                $"CLIP STUDIO rejected companion authentication: {Authentication.ErrorReason ?? "unknown reason"}.");
        }

        Volatile.Write(ref isAuthenticated, true);
        heartbeatTask = Task.Run(() => HeartbeatLoopAsync(lifetimeCancellation.Token));
    }

    private void StartReceiveLoop() =>
        receiveTask = Task.Run(() => ReceiveLoopAsync(lifetimeCancellation.Token));

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var frame = await CompanionFrameCodec.ReadAsync(
                    stream,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                if (frame.Type == CompanionFrameType.Command)
                {
                    await AcknowledgeServerPushAsync(frame, cancellationToken).ConfigureAwait(false);
                    ServerPushReceived?.Invoke(this, new CompanionServerPushEventArgs(frame));
                }
                else if (pending.TryGetValue(frame.Serial, out var completion))
                {
                    completion.TrySetResult(frame);
                }
            }
        }
        catch (Exception ex) when (
            ex is IOException or EndOfStreamException or OperationCanceledException or ObjectDisposedException)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                MarkDisconnected(ex);
            }
        }
    }

    private async Task AcknowledgeServerPushAsync(
        CompanionFrame frame,
        CancellationToken cancellationToken)
    {
        var acknowledgement = CompanionFrameCodec.EncodeRaw(
            CompanionFrameType.Success,
            frame.Command,
            frame.Serial,
            []);
        await WriteFrameAsync(acknowledgement, cancellationToken).ConfigureAwait(false);
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                // Active heartbeats keep CSP's state synchronization out of its idle gate.
                var detail = """{"IdleTimerResetRequested":true}"""u8.ToArray();
                await SendRawAsync("TellHeartbeat", detail, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (
            ex is IOException or EndOfStreamException or SocketException or
                InvalidOperationException or TimeoutException or
                TaskCanceledException or OperationCanceledException)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                MarkDisconnected(ex);
            }
        }
    }

    private async Task WriteFrameAsync(
        ReadOnlyMemory<byte> frame,
        CancellationToken cancellationToken)
    {
        await writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            writeLock.Release();
        }
    }

    private void MarkDisconnected(Exception exception)
    {
        if (Interlocked.Exchange(ref disconnected, 1) != 0)
        {
            return;
        }

        Volatile.Write(ref isAuthenticated, false);
        lifetimeCancellation.Cancel();
        foreach (var completion in pending.Values)
        {
            completion.TrySetException(exception);
        }

        if (Volatile.Read(ref disposed) == 0)
        {
            Disconnected?.Invoke(this, new CompanionDisconnectedEventArgs(exception));
        }
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

    private static async Task IgnoreExpectedShutdownAsync(Task? task)
    {
        if (task is null)
        {
            return;
        }

        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception ex) when (
            ex is OperationCanceledException or IOException or InvalidOperationException or
                ObjectDisposedException or SocketException)
        {
        }
    }

    private static async Task DisposeFailedConnectionAsync(
        UpstreamCompanionClient? client,
        TcpClient tcpClient)
    {
        if (client is null)
        {
            tcpClient.Dispose();
            return;
        }

        try
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (
            ex is OperationCanceledException or IOException or InvalidOperationException or
                ObjectDisposedException or SocketException)
        {
        }
    }
}
