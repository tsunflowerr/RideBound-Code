using System.Text.Unicode;

namespace RideBound.Runner.Protocol;

public enum NdjsonReadKind
{
    Message,
    EndOfStream,
    Error,
}

public sealed record NdjsonReadResult(
    NdjsonReadKind Kind,
    byte[]? Utf8Json = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

/// <remarks>
/// ADR-089: a line is read a buffer at a time (the delimiter found with a vectorized search) and
/// checked with <see cref="Utf8.IsValid(ReadOnlySpan{byte})"/>, instead of one awaited byte at a
/// time and a strict UTF-16 decode of the whole line. Results are the same: a line counts every
/// byte before the line feed (a trailing carriage return included) against the limit, an oversize
/// line is discarded up to its line feed, and only well-formed UTF-8 is accepted. A 125 MiB travel
/// snapshot no longer costs about 600 MB of temporary memory.
/// </remarks>
public sealed class NdjsonReader
{
    public const int DefaultMaximumLineBytes = 1_048_576;

    /// <summary>
    /// ADR-089: the largest --maximum-line-bytes (was 64 MiB). A travelTimesUpdated snapshot of a
    /// full 512-request Via cell (824–858 nodes, about 700,000 arcs) is about 125 MiB.
    /// </summary>
    public const int MaximumConfigurableLineBytes = 1_073_741_824;

    private const int ChunkBytes = 65_536;

    private readonly Stream _stream;
    private readonly int _maximumLineBytes;
    private readonly byte[] _readBuffer = new byte[ChunkBytes];
    private int _readOffset;
    private int _readCount;

    public NdjsonReader(
        Stream stream,
        int maximumLineBytes = DefaultMaximumLineBytes)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
        {
            throw new ArgumentException("NDJSON stream must be readable.", nameof(stream));
        }

        if (maximumLineBytes < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumLineBytes));
        }

        _stream = stream;
        _maximumLineBytes = maximumLineBytes;
    }

    public async ValueTask<NdjsonReadResult> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        var chunks = new List<byte[]>();
        long length = 0;
        var tooLarge = false;

        while (true)
        {
            if (_readOffset >= _readCount)
            {
                _readCount = await _stream.ReadAsync(
                    _readBuffer.AsMemory(),
                    cancellationToken).ConfigureAwait(false);
                _readOffset = 0;

                if (_readCount == 0)
                {
                    if (length == 0 && !tooLarge)
                    {
                        return new NdjsonReadResult(NdjsonReadKind.EndOfStream);
                    }

                    return new NdjsonReadResult(
                        NdjsonReadKind.Error,
                        ErrorCode: "INCOMPLETE_FRAME_EOF",
                        ErrorMessage: "Input ended before the NDJSON line-feed delimiter.");
                }
            }

            var available = _readBuffer.AsSpan(_readOffset, _readCount - _readOffset);
            var lineFeed = available.IndexOf((byte)'\n');
            var content = lineFeed < 0 ? available : available[..lineFeed];

            if (!tooLarge)
            {
                if (length + content.Length > _maximumLineBytes)
                {
                    tooLarge = true;
                    chunks.Clear();
                    length = 0;
                }
                else if (!content.IsEmpty)
                {
                    chunks.Add(content.ToArray());
                    length += content.Length;
                }
            }

            _readOffset += content.Length;

            if (lineFeed < 0)
            {
                continue;
            }

            _readOffset++;

            if (tooLarge)
            {
                return new NdjsonReadResult(
                    NdjsonReadKind.Error,
                    ErrorCode: "MESSAGE_TOO_LARGE",
                    ErrorMessage: $"NDJSON message exceeds {_maximumLineBytes} bytes.");
            }

            var bytes = Join(chunks, length);

            if (bytes.Length > 0 && bytes[^1] == (byte)'\r')
            {
                bytes = bytes[..^1];
            }

            if (!Utf8.IsValid(bytes))
            {
                return new NdjsonReadResult(
                    NdjsonReadKind.Error,
                    ErrorCode: "MALFORMED_UTF8",
                    ErrorMessage: "NDJSON message is not valid UTF-8.");
            }

            return new NdjsonReadResult(NdjsonReadKind.Message, bytes);
        }
    }

    private static byte[] Join(List<byte[]> chunks, long length)
    {
        if (chunks.Count == 1)
        {
            return chunks[0];
        }

        var result = GC.AllocateUninitializedArray<byte>(checked((int)length));
        var offset = 0;

        foreach (var chunk in chunks)
        {
            chunk.CopyTo(result, offset);
            offset += chunk.Length;
        }

        return result;
    }
}
