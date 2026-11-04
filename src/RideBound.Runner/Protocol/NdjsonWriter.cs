using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;

namespace RideBound.Runner.Protocol;

public sealed class NdjsonWriter
{
    private static readonly byte[] LineFeed = [(byte)'\n'];

    private readonly Stream _stream;

    public NdjsonWriter(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanWrite)
        {
            throw new ArgumentException("NDJSON stream must be writable.", nameof(stream));
        }

        _stream = stream;
    }

    public async ValueTask WriteAsync(
        ProtocolEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var canonical = CanonicalJson.Serialize(envelope);
        await _stream.WriteAsync(canonical, cancellationToken).ConfigureAwait(false);
        await _stream.WriteAsync(LineFeed, cancellationToken).ConfigureAwait(false);
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>ADR-089: writes a response that is already canonical bytes, part by part.</summary>
    public async ValueTask WriteAsync(
        StreamedResponse response,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        await response.WriteToAsync(_stream, cancellationToken).ConfigureAwait(false);
        await _stream.WriteAsync(LineFeed, cancellationToken).ConfigureAwait(false);
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
