using System.Text;
using RideBound.Runner.Protocol;

namespace RideBound.Runner.Tests.Protocol;

/// <summary>
/// ADR-089: the buffered reader must return exactly what the archived byte-at-a-time reader
/// returned (kind, bytes, error code and message) for any byte stream, read size and line limit.
/// </summary>
public sealed class NdjsonReaderIdentityTests
{
    [Fact]
    public async Task Buffered_reader_matches_the_archived_reader_on_random_streams()
    {
        var compared = 0;

        for (var seed = 1; seed <= 3000; seed++)
        {
            var random = new Random(seed);
            var input = RandomInput(random);
            var limit = random.Next(1, 40);
            var archived = await ReadAll(new ArchivedReader(new ChoppyStream(input, new Random(seed)), limit));
            var buffered = await ReadAll(new NdjsonReader(new ChoppyStream(input, new Random(seed + 1)), limit));

            Assert.Equal(archived.Count, buffered.Count);

            for (var index = 0; index < archived.Count; index++)
            {
                Assert.Equal(archived[index].Kind, buffered[index].Kind);
                Assert.Equal(archived[index].ErrorCode, buffered[index].ErrorCode);
                Assert.Equal(archived[index].ErrorMessage, buffered[index].ErrorMessage);
                Assert.Equal(archived[index].Utf8Json, buffered[index].Utf8Json);
                compared++;
            }
        }

        Assert.True(compared > 10_000, $"only {compared} results compared");
    }

    [Fact]
    public async Task A_line_longer_than_one_buffer_is_read_whole()
    {
        var payload = Encoding.UTF8.GetBytes("{\"x\":\"" + new string('é', 200_000) + "\"}");
        var reader = new NdjsonReader(
            new MemoryStream([.. payload, (byte)'\r', (byte)'\n']),
            maximumLineBytes: 1_048_576);

        var result = await reader.ReadAsync();

        Assert.Equal(NdjsonReadKind.Message, result.Kind);
        Assert.Equal(payload, result.Utf8Json);
        Assert.Equal(NdjsonReadKind.EndOfStream, (await reader.ReadAsync()).Kind);
    }

    private static byte[] RandomInput(Random random)
    {
        // Short lines over a small alphabet that includes CR, LF, multi-byte UTF-8, and bytes that
        // break UTF-8 (lone continuation, truncated lead, overlong, surrogate, beyond U+10FFFF).
        byte[][] pieces =
        [
            "a"u8.ToArray(), "{}"u8.ToArray(), "\r"u8.ToArray(), "\n"u8.ToArray(), "\r\n"u8.ToArray(),
            "é"u8.ToArray(), "中"u8.ToArray(), "😀"u8.ToArray(), [0x80], [0xC3], [0xC0, 0xAF],
            [0xED, 0xA0, 0x80], [0xF4, 0x90, 0x80, 0x80], [0xEF, 0xBB, 0xBF], [0x00],
        ];
        var bytes = new List<byte>();
        var count = random.Next(0, 60);

        for (var index = 0; index < count; index++)
        {
            bytes.AddRange(pieces[random.Next(pieces.Length)]);
        }

        return [.. bytes];
    }

    private static async Task<List<NdjsonReadResult>> ReadAll(NdjsonReader reader)
    {
        var results = new List<NdjsonReadResult>();

        while (true)
        {
            var result = await reader.ReadAsync();
            results.Add(result);

            if (result.Kind == NdjsonReadKind.EndOfStream || result.ErrorCode == "INCOMPLETE_FRAME_EOF")
            {
                return results;
            }
        }
    }

    private static async Task<List<NdjsonReadResult>> ReadAll(ArchivedReader reader)
    {
        var results = new List<NdjsonReadResult>();

        while (true)
        {
            var result = await reader.ReadAsync();
            results.Add(result);

            if (result.Kind == NdjsonReadKind.EndOfStream || result.ErrorCode == "INCOMPLETE_FRAME_EOF")
            {
                return results;
            }
        }
    }

    /// <summary>A stream that returns between 1 and 7 bytes per read.</summary>
    private sealed class ChoppyStream(byte[] data, Random random) : Stream
    {
        private int _position;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => data.Length;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var take = Math.Min(Math.Min(count, random.Next(1, 8)), data.Length - _position);
            Array.Copy(data, _position, buffer, offset, take);
            _position += take;
            return take;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>NdjsonReader.ReadAsync at 59c4ee8, verbatim apart from the class name.</summary>
    private sealed class ArchivedReader(Stream stream, int maximumLineBytes)
    {
        private static readonly UTF8Encoding StrictUtf8 = new(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

        private readonly byte[] _readBuffer = new byte[8192];
        private int _readOffset;
        private int _readCount;

        public async ValueTask<NdjsonReadResult> ReadAsync(
            CancellationToken cancellationToken = default)
        {
            var line = new List<byte>();
            var tooLarge = false;

            while (true)
            {
                var next = await ReadByteAsync(cancellationToken).ConfigureAwait(false);

                if (next is null)
                {
                    if (line.Count == 0 && !tooLarge)
                    {
                        return new NdjsonReadResult(NdjsonReadKind.EndOfStream);
                    }

                    return new NdjsonReadResult(
                        NdjsonReadKind.Error,
                        ErrorCode: "INCOMPLETE_FRAME_EOF",
                        ErrorMessage: "Input ended before the NDJSON line-feed delimiter.");
                }

                if (next.Value == (byte)'\n')
                {
                    if (tooLarge)
                    {
                        return new NdjsonReadResult(
                            NdjsonReadKind.Error,
                            ErrorCode: "MESSAGE_TOO_LARGE",
                            ErrorMessage: $"NDJSON message exceeds {maximumLineBytes} bytes.");
                    }

                    if (line.Count > 0 && line[^1] == (byte)'\r')
                    {
                        line.RemoveAt(line.Count - 1);
                    }

                    var bytes = line.ToArray();

                    try
                    {
                        _ = StrictUtf8.GetString(bytes);
                    }
                    catch (DecoderFallbackException)
                    {
                        return new NdjsonReadResult(
                            NdjsonReadKind.Error,
                            ErrorCode: "MALFORMED_UTF8",
                            ErrorMessage: "NDJSON message is not valid UTF-8.");
                    }

                    return new NdjsonReadResult(NdjsonReadKind.Message, bytes);
                }

                if (!tooLarge)
                {
                    if (line.Count == maximumLineBytes)
                    {
                        tooLarge = true;
                        line.Clear();
                    }
                    else
                    {
                        line.Add(next.Value);
                    }
                }
            }
        }

        private async ValueTask<byte?> ReadByteAsync(CancellationToken cancellationToken)
        {
            if (_readOffset >= _readCount)
            {
                _readCount = await stream.ReadAsync(
                    _readBuffer.AsMemory(),
                    cancellationToken).ConfigureAwait(false);
                _readOffset = 0;

                if (_readCount == 0)
                {
                    return null;
                }
            }

            return _readBuffer[_readOffset++];
        }
    }
}
