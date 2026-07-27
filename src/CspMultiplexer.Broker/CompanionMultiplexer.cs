using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using CspMultiplexer.Protocol;

namespace CspMultiplexer.Broker;

public sealed class CompanionClientCountChangedEventArgs(int authenticatedClientCount) : EventArgs
{
    public int AuthenticatedClientCount { get; } = authenticatedClientCount;
}

public sealed class CompanionMultiplexer : IAsyncDisposable
{
    private readonly ICompanionUpstream upstream;
    private readonly CompanionMultiplexerOptions options;
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private readonly ConcurrentDictionary<Guid, DownstreamSession> sessions = new();
    private readonly Dictionary<string, ReconnectCredential> reconnectCredentials = new(StringComparer.Ordinal);
    private readonly object credentialLock = new();
    private readonly CompanionCommandScheduler commandScheduler;
    private TcpListener? listener;
    private Task? acceptTask;
    private long credentialUseSequence;
    private int disposed;

    public CompanionMultiplexer(
        ICompanionUpstream upstream,
        string upstreamGeneration,
        CompanionMultiplexerOptions? options = null)
    {
        this.upstream = upstream ?? throw new ArgumentNullException(nameof(upstream));
        ArgumentException.ThrowIfNullOrWhiteSpace(upstreamGeneration);
        this.options = options ?? new CompanionMultiplexerOptions();
        if (!IPAddress.IsLoopback(this.options.ListenAddress) &&
            (!this.options.AllowLan ||
             !CompanionPairingCodec.IsPrivateOrLocal(this.options.ListenAddress) ||
             this.options.ListenAddress.Equals(IPAddress.Any) ||
             this.options.ListenAddress.Equals(IPAddress.IPv6Any)))
        {
            throw new InvalidOperationException(
                "LAN listening requires explicit opt-in and a specific private or local address.");
        }

        if (this.options.MaximumClients is < 1 or > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Maximum clients must be between 1 and 64.");
        }

        if (this.options.MaximumPendingPushesPerClient is < 1 or > 1024)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Maximum pending pushes per client must be between 1 and 1024.");
        }

        InvitationPassword = CompanionAuthCodec.CreateRandomPassword();
        Generation = upstream.Authentication.ServerSpecVersion ?? upstreamGeneration;
        commandScheduler = new CompanionCommandScheduler(this.options.MaximumConcurrentReads);
        this.upstream.ServerPushReceived += OnUpstreamServerPushReceived;
    }

    public event EventHandler<CompanionClientCountChangedEventArgs>? ClientCountChanged;

    public string InvitationPassword { get; }

    public string Generation { get; }

    public int AuthenticatedClientCount => sessions.Values.Count(session => session.IsAuthenticated);

    public IPEndPoint? LocalEndPoint => listener?.LocalEndpoint as IPEndPoint;

    public string? PairingUrl { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        if (listener is not null)
        {
            throw new InvalidOperationException("The multiplexer is already running.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        listener = new TcpListener(options.ListenAddress, options.Port);
        listener.Start(options.MaximumClients);
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        PairingUrl = CompanionPairingCodec.Encode(new CompanionPairingInfo(
            [options.ListenAddress],
            checked((ushort)endpoint.Port),
            InvitationPassword,
            Generation));
        acceptTask = Task.Run(() => AcceptLoopAsync(lifetimeCancellation.Token));
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        upstream.ServerPushReceived -= OnUpstreamServerPushReceived;
        await lifetimeCancellation.CancelAsync().ConfigureAwait(false);
        listener?.Stop();

        foreach (var session in sessions.Values)
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }

        if (acceptTask is not null)
        {
            try
            {
                await acceptTask.ConfigureAwait(false);
            }
            catch (Exception ex) when (
                ex is OperationCanceledException or SocketException or ObjectDisposedException)
            {
            }
        }

        lifetimeCancellation.Dispose();
        commandScheduler.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient tcpClient;
            try
            {
                tcpClient = await listener!.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (sessions.Count >= options.MaximumClients)
            {
                tcpClient.Dispose();
                continue;
            }

            var connectionId = Guid.NewGuid();
            var session = new DownstreamSession(
                connectionId,
                tcpClient,
                upstream,
                commandScheduler,
                Authenticate,
                options.MaximumFrameLength,
                options.MaximumPendingPushesPerClient,
                OnSessionAuthenticated,
                OnSessionClosed);
            if (!sessions.TryAdd(connectionId, session))
            {
                await session.DisposeAsync().ConfigureAwait(false);
                continue;
            }

            session.Start(cancellationToken);
        }
    }

    private DownstreamAuthenticationDecision Authenticate(
        Guid connectionId,
        CompanionAuthRequest request)
    {
        if (!string.Equals(request.Generation, Generation, StringComparison.Ordinal))
        {
            return DownstreamAuthenticationDecision.Reject("VersionMismatch");
        }

        lock (credentialLock)
        {
            if (string.Equals(request.CurrentPassword, InvitationPassword, StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(request.NewPassword) ||
                    reconnectCredentials.ContainsKey(request.NewPassword))
                {
                    return DownstreamAuthenticationDecision.Reject("PasswordMismatch");
                }

                if (reconnectCredentials.Count >= options.MaximumClients)
                {
                    string? oldestInactivePassword = null;
                    var oldestUse = long.MaxValue;
                    foreach (var (password, storedCredential) in reconnectCredentials)
                    {
                        if (!sessions.ContainsKey(storedCredential.ActiveConnectionId) &&
                            storedCredential.LastUsed < oldestUse)
                        {
                            oldestInactivePassword = password;
                            oldestUse = storedCredential.LastUsed;
                        }
                    }

                    if (oldestInactivePassword is null)
                    {
                        return DownstreamAuthenticationDecision.Reject("ServerUnready");
                    }

                    reconnectCredentials.Remove(oldestInactivePassword);
                }

                reconnectCredentials.Add(
                    request.NewPassword,
                    new ReconnectCredential(
                        connectionId,
                        connectionId,
                        ++credentialUseSequence));
                return DownstreamAuthenticationDecision.Accept(connectionId);
            }

            if (string.Equals(
                    request.CurrentPassword,
                    CompanionAuthCodec.ReconnectionMarker,
                    StringComparison.Ordinal) &&
                reconnectCredentials.TryGetValue(request.NewPassword, out var credential))
            {
                if (sessions.TryGetValue(credential.ActiveConnectionId, out var previousSession) &&
                    previousSession.ConnectionId != connectionId)
                {
                    previousSession.Disconnect();
                }

                credential.ActiveConnectionId = connectionId;
                credential.LastUsed = ++credentialUseSequence;
                return DownstreamAuthenticationDecision.Accept(credential.ClientId);
            }
        }

        return DownstreamAuthenticationDecision.Reject("PasswordMismatch");
    }

    private void OnSessionAuthenticated(DownstreamSession session) =>
        ClientCountChanged?.Invoke(
            this,
            new CompanionClientCountChangedEventArgs(AuthenticatedClientCount));

    private void OnSessionClosed(DownstreamSession session)
    {
        sessions.TryRemove(session.ConnectionId, out _);
        ClientCountChanged?.Invoke(
            this,
            new CompanionClientCountChangedEventArgs(AuthenticatedClientCount));
    }

    private void OnUpstreamServerPushReceived(
        object? sender,
        CompanionServerPushEventArgs eventArgs)
    {
        foreach (var session in sessions.Values.Where(value => value.IsAuthenticated))
        {
            session.QueueServerPush(eventArgs.Frame);
        }
    }

    private sealed class ReconnectCredential(
        Guid clientId,
        Guid activeConnectionId,
        long lastUsed)
    {
        public Guid ClientId { get; } = clientId;

        public Guid ActiveConnectionId { get; set; } = activeConnectionId;

        public long LastUsed { get; set; } = lastUsed;
    }

    private sealed record DownstreamAuthenticationDecision(
        bool IsAccepted,
        Guid ClientId,
        string ErrorReason)
    {
        public static DownstreamAuthenticationDecision Accept(Guid clientId) =>
            new(true, clientId, "Unknown");

        public static DownstreamAuthenticationDecision Reject(string reason) =>
            new(false, Guid.Empty, reason);
    }

    private sealed class DownstreamSession : IAsyncDisposable
    {
        private readonly TcpClient tcpClient;
        private readonly Stream stream;
        private readonly ICompanionUpstream upstream;
        private readonly CompanionCommandScheduler commandScheduler;
        private readonly Func<Guid, CompanionAuthRequest, DownstreamAuthenticationDecision> authenticate;
        private readonly int maximumFrameLength;
        private readonly Action<DownstreamSession> authenticated;
        private readonly Action<DownstreamSession> closed;
        private readonly CancellationTokenSource lifetimeCancellation = new();
        private readonly SemaphoreSlim writeLock = new(1, 1);
        private readonly Channel<CompanionFrame> serverPushes;
        private Task? runTask;
        private int nextPushSerial = -1;
        private int disposed;
        private int closeNotified;
        private int resourcesDisposed;
        private bool isAuthenticated;

        public DownstreamSession(
            Guid connectionId,
            TcpClient tcpClient,
            ICompanionUpstream upstream,
            CompanionCommandScheduler commandScheduler,
            Func<Guid, CompanionAuthRequest, DownstreamAuthenticationDecision> authenticate,
            int maximumFrameLength,
            int maximumPendingPushes,
            Action<DownstreamSession> authenticated,
            Action<DownstreamSession> closed)
        {
            ConnectionId = connectionId;
            this.tcpClient = tcpClient;
            stream = tcpClient.GetStream();
            this.upstream = upstream;
            this.commandScheduler = commandScheduler;
            this.authenticate = authenticate;
            this.maximumFrameLength = maximumFrameLength;
            serverPushes = Channel.CreateBounded<CompanionFrame>(
                new BoundedChannelOptions(maximumPendingPushes)
                {
                    SingleReader = true,
                    SingleWriter = true,
                    FullMode = BoundedChannelFullMode.Wait,
                    AllowSynchronousContinuations = false,
                });
            this.authenticated = authenticated;
            this.closed = closed;
        }

        public Guid ConnectionId { get; }

        public Guid ClientId { get; private set; }

        public bool IsAuthenticated => Volatile.Read(ref isAuthenticated);

        public void Start(CancellationToken serverCancellation)
        {
            runTask = RunSessionAsync(serverCancellation);
        }

        public void QueueServerPush(CompanionFrame frame)
        {
            if (!IsAuthenticated || Volatile.Read(ref disposed) != 0)
            {
                return;
            }

            if (!serverPushes.Writer.TryWrite(frame))
            {
                // A client that cannot consume a small bounded queue would otherwise retain
                // one task and one frame per host push for the rest of the session.
                Disconnect();
            }
        }

        public void Disconnect()
        {
            if (Volatile.Read(ref disposed) == 0)
            {
                lifetimeCancellation.Cancel();
                tcpClient.Dispose();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            Volatile.Write(ref isAuthenticated, false);
            serverPushes.Writer.TryComplete();
            await lifetimeCancellation.CancelAsync().ConfigureAwait(false);
            tcpClient.Dispose();
            await stream.DisposeAsync().ConfigureAwait(false);
            if (runTask is not null && Task.CurrentId != runTask.Id)
            {
                try
                {
                    await runTask.ConfigureAwait(false);
                }
                catch (Exception ex) when (
                    ex is OperationCanceledException or IOException or ObjectDisposedException)
                {
                }
            }

            DisposeResourcesOnce();
            NotifyClosedOnce();
        }

        private async Task RunSessionAsync(CancellationToken serverCancellation)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                lifetimeCancellation.Token,
                serverCancellation);
            var pushTask = SendServerPushesAsync(linked.Token);
            try
            {
                await RunAsync(linked.Token).ConfigureAwait(false);
            }
            finally
            {
                serverPushes.Writer.TryComplete();
                await linked.CancelAsync().ConfigureAwait(false);
                try
                {
                    await pushTask.ConfigureAwait(false);
                }
                catch (Exception ex) when (
                    ex is IOException or OperationCanceledException or ObjectDisposedException)
                {
                }

                Interlocked.Exchange(ref disposed, 1);
                DisposeResourcesOnce();
            }
        }

        private async Task SendServerPushesAsync(CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var frame in serverPushes.Reader.ReadAllAsync(cancellationToken)
                                   .ConfigureAwait(false))
                {
                    var serial = unchecked((uint)Interlocked.Increment(ref nextPushSerial));
                    var encoded = CompanionFrameCodec.EncodeRaw(
                        CompanionFrameType.Command,
                        frame.Command,
                        serial,
                        frame.RawDetail,
                        frame.BinaryTail);
                    await WriteAsync(encoded, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (
                ex is ArgumentException or IOException or
                    OperationCanceledException or ObjectDisposedException)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    Disconnect();
                }
            }
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (!await AuthenticateAsync(cancellationToken).ConfigureAwait(false))
                {
                    return;
                }

                while (!cancellationToken.IsCancellationRequested)
                {
                    var frame = await CompanionFrameCodec.ReadAsync(
                        stream,
                        maximumFrameLength,
                        cancellationToken).ConfigureAwait(false);
                    if (frame.Type == CompanionFrameType.Command)
                    {
                        await ForwardCommandAsync(frame, cancellationToken).ConfigureAwait(false);
                    }
                    // Success/Error frames are acknowledgements for independently broadcast pushes.
                }
            }
            catch (Exception ex) when (
                ex is IOException or EndOfStreamException or InvalidDataException or
                    OperationCanceledException or ObjectDisposedException)
            {
            }
            finally
            {
                Volatile.Write(ref isAuthenticated, false);
                tcpClient.Dispose();
                NotifyClosedOnce();
            }
        }

        private async Task<bool> AuthenticateAsync(CancellationToken cancellationToken)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeout.Token);
            var frame = await CompanionFrameCodec.ReadAsync(
                stream,
                maximumFrameLength,
                linked.Token).ConfigureAwait(false);

            CompanionAuthRequest request;
            try
            {
                request = CompanionAuthCodec.ParseRequest(frame);
            }
            catch (InvalidDataException)
            {
                await SendAuthResultAsync(frame, "PasswordMismatch", cancellationToken)
                    .ConfigureAwait(false);
                return false;
            }

            var decision = authenticate(ConnectionId, request);
            await SendAuthResultAsync(frame, decision.ErrorReason, cancellationToken)
                .ConfigureAwait(false);
            if (!decision.IsAccepted)
            {
                return false;
            }

            ClientId = decision.ClientId;
            Volatile.Write(ref isAuthenticated, true);
            authenticated(this);
            return true;
        }

        private async Task SendAuthResultAsync(
            CompanionFrame request,
            string errorReason,
            CancellationToken cancellationToken)
        {
            var detail = CompanionAuthCodec.CreateResultDetail(
                errorReason,
                upstream.Authentication.ServerSpecVersion ?? "G#1:2022.12",
                upstream.Authentication.IsQuickAccessAvailable);
            var encoded = CompanionFrameCodec.EncodeRaw(
                CompanionFrameType.Success,
                "Authenticate",
                request.Serial,
                detail);
            await WriteAsync(encoded, cancellationToken).ConfigureAwait(false);
        }

        private async Task ForwardCommandAsync(
            CompanionFrame request,
            CancellationToken cancellationToken)
        {
            if (request.Command == "Authenticate")
            {
                var rejected = CompanionFrameCodec.EncodeRaw(
                    CompanionFrameType.Error,
                    request.Command,
                    request.Serial,
                    []);
                await WriteAsync(rejected, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (request.Command == "TellHeartbeat")
            {
                // The broker owns one active upstream heartbeat. Forwarding every
                // client's passive heartbeat would multiply traffic and can put CSP
                // back into its idle state gate.
                var heartbeatResponse = CompanionFrameCodec.EncodeRaw(
                    CompanionFrameType.Success,
                    request.Command,
                    request.Serial,
                    []);
                await WriteAsync(heartbeatResponse, cancellationToken).ConfigureAwait(false);
                return;
            }

            CompanionFrame response;
            try
            {
                response = await commandScheduler.ExecuteAsync(
                    request.Command,
                    token => upstream.SendRawAsync(
                        request.Command,
                        request.RawDetail,
                        request.BinaryTail,
                        token),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (
                ex is IOException or InvalidOperationException or TimeoutException or
                    TaskCanceledException or OperationCanceledException)
            {
                var failed = CompanionFrameCodec.EncodeRaw(
                    CompanionFrameType.Error,
                    request.Command,
                    request.Serial,
                    []);
                await WriteAsync(failed, cancellationToken).ConfigureAwait(false);
                return;
            }

            var encoded = CompanionFrameCodec.EncodeRaw(
                response.Type,
                request.Command,
                request.Serial,
                response.RawDetail,
                response.BinaryTail);
            await WriteAsync(encoded, cancellationToken).ConfigureAwait(false);
        }

        private async Task WriteAsync(
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

        private void NotifyClosedOnce()
        {
            if (Interlocked.Exchange(ref closeNotified, 1) == 0)
            {
                closed(this);
            }
        }

        private void DisposeResourcesOnce()
        {
            if (Interlocked.Exchange(ref resourcesDisposed, 1) != 0)
            {
                return;
            }

            writeLock.Dispose();
            lifetimeCancellation.Dispose();
        }
    }
}
