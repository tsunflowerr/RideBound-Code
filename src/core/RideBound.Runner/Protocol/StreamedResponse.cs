using RideBound.Contracts.Protocol;

namespace RideBound.Runner.Protocol;

/// <summary>
/// ADR-089: a response written to the output stream part by part instead of being built as one
/// envelope. The final checkpoint of a Via-sized job is one line of several hundred MiB, and
/// building it as an envelope held five to seven copies of it and ran out of memory. The parts are
/// exactly the canonical bytes <see cref="NdjsonWriter"/> writes for the equivalent envelope.
/// </summary>
public sealed class StreamedResponse
{
    private readonly IReadOnlyList<ReadOnlyMemory<byte>> _parts;
    private ProtocolEnvelope? _envelope;

    public StreamedResponse(string messageType, IReadOnlyList<ReadOnlyMemory<byte>> parts)
    {
        ArgumentException.ThrowIfNullOrEmpty(messageType);
        ArgumentNullException.ThrowIfNull(parts);
        MessageType = messageType;
        _parts = parts;
    }

    public string MessageType { get; }

    public async ValueTask WriteToAsync(Stream stream, CancellationToken cancellationToken)
    {
        foreach (var part in _parts)
        {
            await stream.WriteAsync(part, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The response as an envelope, decoded from the streamed bytes. It copies the whole message,
    /// so the host never calls it; it serves callers that inspect the response in memory.
    /// </summary>
    public ProtocolEnvelope ToEnvelope()
    {
        if (_envelope is not null)
        {
            return _envelope;
        }

        var bytes = new byte[_parts.Sum(part => (long)part.Length)];
        var offset = 0;

        foreach (var part in _parts)
        {
            part.Span.CopyTo(bytes.AsSpan(offset));
            offset += part.Length;
        }

        var decoded = ProtocolEnvelopeCodec.Decode(bytes);
        _envelope = decoded.Envelope
            ?? throw new InvalidOperationException("A streamed response is not a protocol envelope.");
        return _envelope;
    }
}
