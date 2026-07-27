using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace CspMultiplexer.Protocol;

public static class CompanionFrameCodec
{
    public const int DefaultMaximumFrameLength = 32 * 1024 * 1024;
    private static readonly byte[] ParameterSeparator = [0x1E, (byte)'$'];
    private static readonly byte[] Prefix = Encoding.ASCII.GetBytes("$tcp_remote_command_protocol_version=1.0");
    private static readonly ConditionalWeakTable<Stream, BufferedFrameReader> StreamReaders = new();

    public static byte[] Encode(
        CompanionFrameType type,
        string command,
        uint serial,
        object? detail = null,
        ReadOnlySpan<byte> binaryTail = default)
    {
        byte[] rawDetail = detail switch
        {
            null => [],
            byte[] bytes => bytes,
            JsonElement element => JsonSerializer.SerializeToUtf8Bytes(element),
            _ => JsonSerializer.SerializeToUtf8Bytes(detail),
        };

        return EncodeRaw(type, command, serial, rawDetail, binaryTail);
    }

    public static byte[] EncodeRaw(
        CompanionFrameType type,
        string command,
        uint serial,
        ReadOnlySpan<byte> rawDetail,
        ReadOnlySpan<byte> binaryTail = default)
    {
        ValidateType(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        if (command.Any(character => character < 0x20 || character > 0x7E || character is '$' or '='))
        {
            throw new ArgumentException("Command names must contain only safe printable ASCII.", nameof(command));
        }

        ValidateDetail(rawDetail);
        var writer = new ArrayBufferWriter<byte>(96 + rawDetail.Length + binaryTail.Length);
        Write(writer, [(byte)type]);
        Write(writer, Prefix);
        Write(writer, ParameterSeparator);
        Write(writer, Encoding.ASCII.GetBytes("command=" + command));
        Write(writer, ParameterSeparator);
        Write(writer, Encoding.ASCII.GetBytes("serial=" + serial.ToString(CultureInfo.InvariantCulture)));
        Write(writer, ParameterSeparator);
        Write(writer, "detail="u8);
        Write(writer, rawDetail);
        if (!binaryTail.IsEmpty)
        {
            Write(writer, [0x0B]);
            Write(writer, binaryTail);
        }

        Write(writer, [0x1E, 0x00]);
        return writer.WrittenSpan.ToArray();
    }

    public static bool TryDecode(
        ReadOnlySpan<byte> buffer,
        out CompanionFrame? frame,
        out int bytesConsumed,
        int maximumFrameLength = DefaultMaximumFrameLength)
    {
        ValidateMaximumLength(maximumFrameLength);
        var terminator = buffer.IndexOf((byte)0);
        if (terminator < 0)
        {
            if (buffer.Length > maximumFrameLength)
            {
                throw new InvalidDataException($"Frame exceeds the {maximumFrameLength}-byte limit.");
            }

            frame = null;
            bytesConsumed = 0;
            return false;
        }

        var length = terminator + 1;
        if (length > maximumFrameLength)
        {
            throw new InvalidDataException($"Frame exceeds the {maximumFrameLength}-byte limit.");
        }

        frame = DecodeComplete(buffer[..length]);
        bytesConsumed = length;
        return true;
    }

    public static async ValueTask<CompanionFrame> ReadAsync(
        Stream stream,
        int maximumFrameLength = DefaultMaximumFrameLength,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ValidateMaximumLength(maximumFrameLength);
        var reader = StreamReaders.GetValue(stream, static value => new BufferedFrameReader(value));
        return await reader.ReadAsync(maximumFrameLength, cancellationToken).ConfigureAwait(false);
    }

    private static CompanionFrame DecodeComplete(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 4 || frame[^1] != 0 || frame[^2] != 0x1E)
        {
            throw new InvalidDataException("Frame is missing its record separator or null terminator.");
        }

        var type = (CompanionFrameType)frame[0];
        ValidateType(type);
        if (frame[1] != (byte)'$')
        {
            throw new InvalidDataException("Frame is missing the leading '$'.");
        }

        var body = frame[1..^2];
        var fields = Split(body, ParameterSeparator, 4);
        if (fields.Count != 4 ||
            !fields[0].Span.SequenceEqual(Prefix) ||
            !fields[1].Span.StartsWith("command="u8) ||
            !fields[2].Span.StartsWith("serial="u8) ||
            !fields[3].Span.StartsWith("detail="u8))
        {
            throw new InvalidDataException("Frame headers do not match companion protocol version 1.0.");
        }

        var command = Encoding.ASCII.GetString(fields[1].Span["command="u8.Length..]);
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new InvalidDataException("Frame command is empty.");
        }

        if (!uint.TryParse(
                Encoding.ASCII.GetString(fields[2].Span["serial="u8.Length..]),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var serial))
        {
            throw new InvalidDataException("Frame serial is not an unsigned 32-bit integer.");
        }

        var detailAndTail = fields[3].Span["detail="u8.Length..];
        var binarySeparator = detailAndTail.IndexOf((byte)0x0B);
        var rawDetail = (binarySeparator < 0 ? detailAndTail : detailAndTail[..binarySeparator]).ToArray();
        var tail = binarySeparator < 0 ? [] : detailAndTail[(binarySeparator + 1)..].ToArray();
        JsonElement? detail = null;
        if (rawDetail.Length > 0)
        {
            try
            {
                using var document = JsonDocument.Parse(rawDetail);
                detail = document.RootElement.Clone();
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Frame detail is not valid JSON.", ex);
            }
        }

        return new CompanionFrame(type, command, serial, detail, rawDetail, tail);
    }

    private static List<ReadOnlyMemory<byte>> Split(ReadOnlySpan<byte> value, ReadOnlySpan<byte> separator, int count)
    {
        var copy = value.ToArray();
        var fields = new List<ReadOnlyMemory<byte>>(count);
        var start = 0;
        for (var i = 1; i < count; i++)
        {
            var offset = copy.AsSpan(start).IndexOf(separator);
            if (offset < 0)
            {
                return fields;
            }

            fields.Add(copy.AsMemory(start, offset));
            start += offset + separator.Length;
        }

        fields.Add(copy.AsMemory(start));
        return fields;
    }

    private static void ValidateDetail(ReadOnlySpan<byte> detail)
    {
        foreach (var value in detail)
        {
            if (value <= 0x06 || value is >= 0x0E and <= 0x1F || value == 0x7F)
            {
                throw new ArgumentException("Detail contains a control byte reserved by the wire protocol.", nameof(detail));
            }
        }
    }

    private static void ValidateType(CompanionFrameType type)
    {
        if (type is not CompanionFrameType.Command and not CompanionFrameType.Success and not CompanionFrameType.Error)
        {
            throw new InvalidDataException($"Unknown companion frame type 0x{(byte)type:X2}.");
        }
    }

    private static void ValidateMaximumLength(int maximumFrameLength)
    {
        if (maximumFrameLength < 64)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumFrameLength));
        }
    }

    private static void Write(ArrayBufferWriter<byte> writer, ReadOnlySpan<byte> value)
    {
        var destination = writer.GetSpan(value.Length);
        value.CopyTo(destination);
        writer.Advance(value.Length);
    }

    private sealed class BufferedFrameReader(Stream stream)
    {
        private const int InitialBufferLength = 64 * 1024;
        private readonly SemaphoreSlim readLock = new(1, 1);
        private byte[] buffer = new byte[InitialBufferLength];
        private int bufferedLength;

        public async ValueTask<CompanionFrame> ReadAsync(
            int maximumFrameLength,
            CancellationToken cancellationToken)
        {
            await readLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                while (true)
                {
                    if (TryDecode(
                            buffer.AsSpan(0, bufferedLength),
                            out var frame,
                            out var consumed,
                            maximumFrameLength))
                    {
                        buffer.AsSpan(consumed, bufferedLength - consumed).CopyTo(buffer);
                        bufferedLength -= consumed;
                        return frame!;
                    }

                    EnsureWritableCapacity(maximumFrameLength);
                    var read = await stream.ReadAsync(
                        buffer.AsMemory(bufferedLength, buffer.Length - bufferedLength),
                        cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        throw new EndOfStreamException(
                            "The companion connection closed before a complete frame arrived.");
                    }

                    bufferedLength += read;
                }
            }
            finally
            {
                readLock.Release();
            }
        }

        private void EnsureWritableCapacity(int maximumFrameLength)
        {
            if (bufferedLength < buffer.Length)
            {
                return;
            }

            if (bufferedLength >= maximumFrameLength)
            {
                throw new InvalidDataException($"Frame exceeds the {maximumFrameLength}-byte limit.");
            }

            var newLength = Math.Min(maximumFrameLength, checked(buffer.Length * 2));
            Array.Resize(ref buffer, newLength);
        }
    }
}
