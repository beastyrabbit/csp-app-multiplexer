using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using CspMultiplexer.Broker;
using CspMultiplexer.Protocol;

namespace CspMultiplexer.Broker.Tests;

public sealed class CompanionMultiplexerIntegrationTests
{
    [Fact]
    public async Task UpstreamDisconnect_IsSignaledAndDisposesCleanly()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var closeConnection = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () =>
        {
            using var serverClient = await listener.AcceptTcpClientAsync(timeout.Token);
            var stream = serverClient.GetStream();
            var authentication = await CompanionFrameCodec.ReadAsync(
                stream,
                cancellationToken: timeout.Token);
            var response = CompanionFrameCodec.EncodeRaw(
                CompanionFrameType.Success,
                "Authenticate",
                authentication.Serial,
                CompanionAuthCodec.CreateResultDetail(
                    "Unknown",
                    "G#1:2026.07",
                    isQuickAccessAvailable: true));
            await stream.WriteAsync(response, timeout.Token);
            await stream.FlushAsync(timeout.Token);
            await closeConnection.Task.WaitAsync(timeout.Token);
        }, timeout.Token);

        try
        {
            var pairing = new CompanionPairingInfo(
                [IPAddress.Loopback],
                checked((ushort)endpoint.Port),
                "invitation",
                "G#1:2026.07");
            await using var upstream = await UpstreamCompanionClient.ConnectAndAuthenticateAsync(
                pairing,
                timeout.Token);
            var disconnected = new TaskCompletionSource<Exception>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            upstream.Disconnected += (_, eventArgs) =>
                disconnected.TrySetResult(eventArgs.Exception);

            closeConnection.TrySetResult();
            var exception = await disconnected.Task.WaitAsync(timeout.Token);

            Assert.IsAssignableFrom<IOException>(exception);
            Assert.False(upstream.IsAuthenticated);
            await serverTask;
        }
        finally
        {
            closeConnection.TrySetResult();
            listener.Stop();
        }
    }

    [Fact]
    public async Task TwoQrClients_WithCollidingSerials_GetOnlyTheirOwnResponses()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var upstream = new FakeUpstream();
        await using var multiplexer = new CompanionMultiplexer(
            upstream,
            "G#1:2026.07");
        await multiplexer.StartAsync(timeout.Token);
        var invitation = CompanionPairingCodec.Decode(multiplexer.PairingUrl!);

        await using var first = await FakeDownstreamClient.ConnectAsync(
            invitation,
            "first-pw",
            timeout.Token);
        await using var second = await FakeDownstreamClient.ConnectAsync(
            invitation,
            "second-pw",
            timeout.Token);

        var firstResponseTask = first.SendAsync(7, 1, timeout.Token);
        var secondResponseTask = second.SendAsync(7, 2, timeout.Token);
        var responses = await Task.WhenAll(firstResponseTask, secondResponseTask);

        Assert.Equal((uint)7, responses[0].Serial);
        Assert.Equal((uint)7, responses[1].Serial);
        Assert.Equal(1, responses[0].Detail?.GetProperty("Echo").GetInt32());
        Assert.Equal(2, responses[1].Detail?.GetProperty("Echo").GetInt32());
        Assert.Equal(2, upstream.Requests.Count);
        Assert.Equal(2, multiplexer.AuthenticatedClientCount);
    }

    [Fact]
    public async Task HostPush_IsBroadcastWithIndependentDownstreamSerials()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var upstream = new FakeUpstream();
        await using var multiplexer = new CompanionMultiplexer(upstream, "G#1:2026.07");
        await multiplexer.StartAsync(timeout.Token);
        var invitation = CompanionPairingCodec.Decode(multiplexer.PairingUrl!);
        await using var first = await FakeDownstreamClient.ConnectAsync(
            invitation,
            "first-pw",
            timeout.Token);
        await using var second = await FakeDownstreamClient.ConnectAsync(
            invitation,
            "second-pw",
            timeout.Token);

        upstream.Push("SyncColorCircleUIState", """{"CurrentColorIndex":0}"""u8.ToArray());
        var pushes = await Task.WhenAll(
            first.ReadAsync(timeout.Token).AsTask(),
            second.ReadAsync(timeout.Token).AsTask());

        Assert.All(pushes, push =>
        {
            Assert.Equal(CompanionFrameType.Command, push.Type);
            Assert.Equal("SyncColorCircleUIState", push.Command);
            Assert.Equal(0, push.Detail?.GetProperty("CurrentColorIndex").GetInt32());
        });
    }

    [Fact]
    public async Task HostPushes_ArriveInOrderForEachDownstream()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var upstream = new FakeUpstream();
        await using var multiplexer = new CompanionMultiplexer(
            upstream,
            "G#1:2026.07",
            new CompanionMultiplexerOptions { MaximumPendingPushesPerClient = 32 });
        await multiplexer.StartAsync(timeout.Token);
        var invitation = CompanionPairingCodec.Decode(multiplexer.PairingUrl!);
        await using var client = await FakeDownstreamClient.ConnectAsync(
            invitation,
            "ordered-pushes",
            timeout.Token);

        for (var sequence = 0; sequence < 20; sequence++)
        {
            upstream.Push(
                "SyncColorCircleUIState",
                JsonSerializer.SerializeToUtf8Bytes(new { Sequence = sequence }));
        }

        for (var expected = 0; expected < 20; expected++)
        {
            var push = await client.ReadAsync(timeout.Token);
            Assert.Equal(expected, push.Detail?.GetProperty("Sequence").GetInt32());
        }
    }

    [Fact]
    public async Task ClientCanReconnectUsingMarkerAndOriginalRotatedPassword()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var upstream = new FakeUpstream();
        await using var multiplexer = new CompanionMultiplexer(upstream, "G#1:2026.07");
        await multiplexer.StartAsync(timeout.Token);
        var invitation = CompanionPairingCodec.Decode(multiplexer.PairingUrl!);

        await using (var initial = await FakeDownstreamClient.ConnectAsync(
                         invitation,
                         "stable-rotated-password",
                         timeout.Token))
        {
        }

        await using var reconnected = await FakeDownstreamClient.ReconnectAsync(
            invitation,
            "stable-rotated-password",
            timeout.Token);
        var response = await reconnected.SendAsync(4, 9, timeout.Token);

        Assert.Equal(9, response.Detail?.GetProperty("Echo").GetInt32());
    }

    [Fact]
    public async Task ReconnectCredentials_AreCappedAndOldestInactiveCredentialIsEvicted()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var upstream = new FakeUpstream();
        await using var multiplexer = new CompanionMultiplexer(
            upstream,
            "G#1:2026.07",
            new CompanionMultiplexerOptions { MaximumClients = 2 });
        await multiplexer.StartAsync(timeout.Token);
        var invitation = CompanionPairingCodec.Decode(multiplexer.PairingUrl!);

        var first = await FakeDownstreamClient.ConnectAsync(
            invitation,
            "oldest-credential",
            timeout.Token);
        await first.DisposeAsync();
        await WaitForClientCountAsync(multiplexer, 0, timeout.Token);

        var second = await FakeDownstreamClient.ConnectAsync(
            invitation,
            "newer-credential",
            timeout.Token);
        await second.DisposeAsync();
        await WaitForClientCountAsync(multiplexer, 0, timeout.Token);

        await using var third = await FakeDownstreamClient.ConnectAsync(
            invitation,
            "newest-credential",
            timeout.Token);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            FakeDownstreamClient.ReconnectAsync(
                invitation,
                "oldest-credential",
                timeout.Token));

        await using var reconnected = await FakeDownstreamClient.ReconnectAsync(
            invitation,
            "newer-credential",
            timeout.Token);
    }

    [Fact]
    public async Task DownstreamHeartbeat_IsTerminatedLocally()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var upstream = new FakeUpstream();
        await using var multiplexer = new CompanionMultiplexer(upstream, "G#1:2026.07");
        await multiplexer.StartAsync(timeout.Token);
        var invitation = CompanionPairingCodec.Decode(multiplexer.PairingUrl!);
        await using var client = await FakeDownstreamClient.ConnectAsync(
            invitation,
            "heartbeat-client",
            timeout.Token);

        var response = await client.SendHeartbeatAsync(17, timeout.Token);

        Assert.Equal(CompanionFrameType.Success, response.Type);
        Assert.Equal("TellHeartbeat", response.Command);
        Assert.Equal((uint)17, response.Serial);
        Assert.Empty(upstream.Requests);
    }

    [Fact]
    public async Task MutatingCommands_AreQueuedAcrossClients()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var upstream = new FakeUpstream();
        await using var multiplexer = new CompanionMultiplexer(upstream, "G#1:2026.07");
        await multiplexer.StartAsync(timeout.Token);
        var invitation = CompanionPairingCodec.Decode(multiplexer.PairingUrl!);
        await using var first = await FakeDownstreamClient.ConnectAsync(
            invitation,
            "queue-one",
            timeout.Token);
        await using var second = await FakeDownstreamClient.ConnectAsync(
            invitation,
            "queue-two",
            timeout.Token);

        await Task.WhenAll(
            first.SendAsync(3, 1, timeout.Token, "SetCurrentColor"),
            second.SendAsync(3, 2, timeout.Token, "SetCurrentColor"));

        Assert.Equal(1, upstream.MaximumConcurrentRequests);
    }

    [Fact]
    public async Task PrivateLanAddress_RequiresExplicitOptIn()
    {
        await using var upstream = new FakeUpstream();
        var address = IPAddress.Parse("192.168.50.20");

        Assert.Throws<InvalidOperationException>(() =>
            new CompanionMultiplexer(
                upstream,
                "G#1:2026.07",
                new CompanionMultiplexerOptions { ListenAddress = address }));

        await using var allowed = new CompanionMultiplexer(
            upstream,
            "G#1:2026.07",
            new CompanionMultiplexerOptions
            {
                ListenAddress = address,
                AllowLan = true,
            });
    }

    [Fact]
    public async Task PendingPushLimit_MustBeWithinSupportedRange()
    {
        await using var upstream = new FakeUpstream();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CompanionMultiplexer(
                upstream,
                "G#1:2026.07",
                new CompanionMultiplexerOptions { MaximumPendingPushesPerClient = 0 }));
    }

    private static async Task WaitForClientCountAsync(
        CompanionMultiplexer multiplexer,
        int expected,
        CancellationToken cancellationToken)
    {
        while (multiplexer.AuthenticatedClientCount != expected)
        {
            await Task.Delay(10, cancellationToken);
        }
    }

    private sealed class FakeUpstream : ICompanionUpstream
    {
        private int responseSerial;
        private int activeRequests;
        private int maximumConcurrentRequests;

        public event EventHandler<CompanionServerPushEventArgs>? ServerPushReceived;

        public event EventHandler<CompanionDisconnectedEventArgs>? Disconnected;

        public bool IsAuthenticated => true;

        public CompanionAuthResult Authentication { get; } =
            new(true, null, "G#1:2026.07", true);

        public ConcurrentQueue<int> Requests { get; } = new();

        public int MaximumConcurrentRequests => Volatile.Read(ref maximumConcurrentRequests);

        public async Task<CompanionFrame> SendRawAsync(
            string command,
            ReadOnlyMemory<byte> rawDetail,
            ReadOnlyMemory<byte> binaryTail = default,
            CancellationToken cancellationToken = default)
        {
            using var document = JsonDocument.Parse(rawDetail);
            var id = document.RootElement.GetProperty("ClientValue").GetInt32();
            Requests.Enqueue(id);
            var active = Interlocked.Increment(ref activeRequests);
            UpdateMaximum(ref maximumConcurrentRequests, active);
            try
            {
                await Task.Delay(id == 1 ? 60 : 5, cancellationToken);
                var responseDetail = JsonSerializer.SerializeToUtf8Bytes(new { Echo = id });
                using var responseDocument = JsonDocument.Parse(responseDetail);
                return new CompanionFrame(
                    CompanionFrameType.Success,
                    command,
                    unchecked((uint)Interlocked.Increment(ref responseSerial)),
                    responseDocument.RootElement.Clone(),
                    responseDetail,
                    []);
            }
            finally
            {
                Interlocked.Decrement(ref activeRequests);
            }
        }

        public void Push(string command, byte[] detail)
        {
            using var document = JsonDocument.Parse(detail);
            var frame = new CompanionFrame(
                CompanionFrameType.Command,
                command,
                88,
                document.RootElement.Clone(),
                detail,
                []);
            ServerPushReceived?.Invoke(this, new CompanionServerPushEventArgs(frame));
        }

        public void Disconnect(Exception exception) =>
            Disconnected?.Invoke(this, new CompanionDisconnectedEventArgs(exception));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static void UpdateMaximum(ref int target, int candidate)
        {
            var current = Volatile.Read(ref target);
            while (candidate > current)
            {
                var observed = Interlocked.CompareExchange(ref target, candidate, current);
                if (observed == current)
                {
                    return;
                }

                current = observed;
            }
        }
    }

    private sealed class FakeDownstreamClient : IAsyncDisposable
    {
        private readonly TcpClient tcpClient;
        private readonly Stream stream;

        private FakeDownstreamClient(TcpClient tcpClient)
        {
            this.tcpClient = tcpClient;
            stream = tcpClient.GetStream();
        }

        public static Task<FakeDownstreamClient> ConnectAsync(
            CompanionPairingInfo invitation,
            string rotatedPassword,
            CancellationToken cancellationToken) =>
            ConnectCoreAsync(
                invitation,
                invitation.Password,
                rotatedPassword,
                cancellationToken);

        public static Task<FakeDownstreamClient> ReconnectAsync(
            CompanionPairingInfo invitation,
            string rotatedPassword,
            CancellationToken cancellationToken) =>
            ConnectCoreAsync(
                invitation,
                CompanionAuthCodec.ReconnectionMarker,
                rotatedPassword,
                cancellationToken);

        public async Task<CompanionFrame> SendAsync(
            uint serial,
            int clientValue,
            CancellationToken cancellationToken,
            string command = "GetModifyKeyString")
        {
            var request = CompanionFrameCodec.Encode(
                CompanionFrameType.Command,
                command,
                serial,
                new { ClientValue = clientValue });
            await stream.WriteAsync(request, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            return await ReadAsync(cancellationToken);
        }

        public async Task<CompanionFrame> SendHeartbeatAsync(
            uint serial,
            CancellationToken cancellationToken)
        {
            var request = CompanionFrameCodec.Encode(
                CompanionFrameType.Command,
                "TellHeartbeat",
                serial);
            await stream.WriteAsync(request, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            return await ReadAsync(cancellationToken);
        }

        public ValueTask<CompanionFrame> ReadAsync(CancellationToken cancellationToken) =>
            CompanionFrameCodec.ReadAsync(stream, cancellationToken: cancellationToken);

        public ValueTask DisposeAsync()
        {
            tcpClient.Dispose();
            return ValueTask.CompletedTask;
        }

        private static async Task<FakeDownstreamClient> ConnectCoreAsync(
            CompanionPairingInfo invitation,
            string currentPassword,
            string rotatedPassword,
            CancellationToken cancellationToken)
        {
            var tcpClient = new TcpClient(AddressFamily.InterNetwork);
            await tcpClient.ConnectAsync(
                invitation.Addresses[0],
                invitation.Port,
                cancellationToken);
            var client = new FakeDownstreamClient(tcpClient);
            var authentication = CompanionFrameCodec.EncodeRaw(
                CompanionFrameType.Command,
                "Authenticate",
                0,
                CompanionAuthCodec.CreateAuthenticationDetail(
                    invitation.Generation,
                    currentPassword,
                    rotatedPassword));
            await client.stream.WriteAsync(authentication, cancellationToken);
            await client.stream.FlushAsync(cancellationToken);
            var response = await CompanionFrameCodec.ReadAsync(
                client.stream,
                cancellationToken: cancellationToken);
            var result = CompanionAuthCodec.ParseResult(response);
            if (!result.IsAuthenticated)
            {
                await client.DisposeAsync();
                throw new UnauthorizedAccessException(result.ErrorReason);
            }

            return client;
        }
    }
}
