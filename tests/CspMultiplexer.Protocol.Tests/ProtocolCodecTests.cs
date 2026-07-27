using System.Net;
using CspMultiplexer.Protocol;

namespace CspMultiplexer.Protocol.Tests;

public sealed class ProtocolCodecTests
{
    [Fact]
    public void PairingEncodeDecode_RoundTripsProxyInvitation()
    {
        var pairing = new CompanionPairingInfo(
            [IPAddress.Loopback],
            43123,
            "proxy-password",
            "G#1:2026.07");

        var url = CompanionPairingCodec.Encode(pairing);
        var decoded = CompanionPairingCodec.Decode(url);

        Assert.Equal(pairing.Addresses, decoded.Addresses);
        Assert.Equal(pairing.Port, decoded.Port);
        Assert.Equal(pairing.Password, decoded.Password);
        Assert.Equal(pairing.Generation, decoded.Generation);
        Assert.StartsWith("https://companion.clip-studio.com/rc/en-us?s=", url);
    }

    [Fact]
    public void AuthenticationRequest_RoundTripsAllThreeFields()
    {
        var detail = CompanionAuthCodec.CreateAuthenticationDetail(
            "G#1:2026.07",
            CompanionAuthCodec.ReconnectionMarker,
            "rotated!");
        var encoded = CompanionFrameCodec.EncodeRaw(
            CompanionFrameType.Command,
            "Authenticate",
            12,
            detail);

        Assert.True(CompanionFrameCodec.TryDecode(encoded, out var frame, out _));
        var request = CompanionAuthCodec.ParseRequest(frame!);

        Assert.Equal("G#1:2026.07", request.Generation);
        Assert.Equal(CompanionAuthCodec.ReconnectionMarker, request.CurrentPassword);
        Assert.Equal("rotated!", request.NewPassword);
    }

    [Fact]
    public void FrameCodec_PreservesDetailAndBinaryTail()
    {
        var detail = """{"Operation":"ReadPreviewBlock"}"""u8.ToArray();
        var tail = "AQIDBA=="u8.ToArray();
        var encoded = CompanionFrameCodec.EncodeRaw(
            CompanionFrameType.Success,
            "PreviewWebtoonFromClient",
            uint.MaxValue,
            detail,
            tail);

        Assert.True(CompanionFrameCodec.TryDecode(encoded, out var frame, out var consumed));
        Assert.Equal(encoded.Length, consumed);
        Assert.Equal(detail, frame!.RawDetail);
        Assert.Equal(tail, frame.BinaryTail);
    }
}
