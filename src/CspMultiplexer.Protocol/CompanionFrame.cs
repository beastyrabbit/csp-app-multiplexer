using System.Text.Json;

namespace CspMultiplexer.Protocol;

public enum CompanionFrameType : byte
{
    Command = 0x01,
    Success = 0x06,
    Error = 0x15,
}

public sealed record CompanionFrame(
    CompanionFrameType Type,
    string Command,
    uint Serial,
    JsonElement? Detail,
    byte[] RawDetail,
    byte[] BinaryTail)
{
    public T? DeserializeDetail<T>() =>
        RawDetail.Length == 0 ? default : JsonSerializer.Deserialize<T>(RawDetail);
}
