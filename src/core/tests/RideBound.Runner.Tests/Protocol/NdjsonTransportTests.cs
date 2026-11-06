using System.Text;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Runner.Protocol;

namespace RideBound.Runner.Tests.Protocol;

public sealed class NdjsonTransportTests
{
    [Fact]
    public async Task Reader_accepts_lf_and_crlf_without_changing_json()
    {
        var input = new MemoryStream(
            Encoding.UTF8.GetBytes("{}\n{\"a\":1}\r\n"));
        var reader = new NdjsonReader(input);

        var first = await reader.ReadAsync();
        var second = await reader.ReadAsync();
        var end = await reader.ReadAsync();

        Assert.Equal("{}", Encoding.UTF8.GetString(first.Utf8Json!));
        Assert.Equal("{\"a\":1}", Encoding.UTF8.GetString(second.Utf8Json!));
        Assert.Equal(NdjsonReadKind.EndOfStream, end.Kind);
    }

    [Fact]
    public async Task Reader_reports_incomplete_eof_instead_of_accepting_unframed_json()
    {
        var reader = new NdjsonReader(
            new MemoryStream(Encoding.UTF8.GetBytes("{}")));

        var result = await reader.ReadAsync();

        Assert.Equal(NdjsonReadKind.Error, result.Kind);
        Assert.Equal("INCOMPLETE_FRAME_EOF", result.ErrorCode);
    }

    [Fact]
    public async Task Reader_rejects_malformed_utf8_and_can_continue()
    {
        var bytes = new byte[]
        {
            0xff,
            (byte)'\n',
            (byte)'{',
            (byte)'}',
            (byte)'\n',
        };
        var reader = new NdjsonReader(new MemoryStream(bytes));

        var invalid = await reader.ReadAsync();
        var valid = await reader.ReadAsync();

        Assert.Equal("MALFORMED_UTF8", invalid.ErrorCode);
        Assert.Equal("{}", Encoding.UTF8.GetString(valid.Utf8Json!));
    }

    [Fact]
    public async Task Reader_discards_whole_oversize_line_before_continuing()
    {
        var reader = new NdjsonReader(
            new MemoryStream(Encoding.UTF8.GetBytes("12345\n{}\n")),
            maximumLineBytes: 4);

        var oversized = await reader.ReadAsync();
        var valid = await reader.ReadAsync();

        Assert.Equal("MESSAGE_TOO_LARGE", oversized.ErrorCode);
        Assert.Equal("{}", Encoding.UTF8.GetString(valid.Utf8Json!));
    }

    [Fact]
    public async Task Reader_and_writer_propagate_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var reader = new NdjsonReader(new BlockingReadStream());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await reader.ReadAsync(cancellation.Token));
    }

    [Fact]
    public async Task Writer_emits_canonical_json_one_lf_and_flushes()
    {
        ProtocolMessageType.TryParse("shutdown", out var messageType);
        using var payloadDocument = System.Text.Json.JsonDocument.Parse("{}");
        var envelope = new ProtocolEnvelope(
            ProtocolVersion.Current,
            messageType!,
            payloadDocument.RootElement.Clone());
        var output = new FlushTrackingStream();
        var writer = new NdjsonWriter(output);

        await writer.WriteAsync(envelope);

        var bytes = output.ToArray();
        Assert.Equal((byte)'\n', bytes[^1]);
        Assert.NotEqual((byte)'\n', bytes[^2]);
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.Equal(1, output.FlushCount);
        Assert.Equal(
            CanonicalJson.Serialize(envelope),
            bytes[..^1]);
    }

    private sealed class BlockingReadStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromCanceled<int>(cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }

    private sealed class FlushTrackingStream : MemoryStream
    {
        public int FlushCount { get; private set; }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            FlushCount++;
            return Task.CompletedTask;
        }
    }
}
