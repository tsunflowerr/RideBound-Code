namespace RideBound.Runner.Protocol;

/// <summary>
/// A response written to the output stream part by part instead of being built as one
/// envelope. The final checkpoint of a Via-sized job is one line of several hundred MiB, and
/// building it as an envelope would hold five to seven copies of it and run out of memory. The parts are
/// exactly the canonical bytes <see cref="NdjsonWriter"/> writes for the equivalent envelope.
/// </summary>
public sealed class StreamedResponse
{
    private readonly IReadOnlyList<ReadOnlyMemory<byte>> _parts;

    public StreamedResponse(IReadOnlyList<ReadOnlyMemory<byte>> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        _parts = parts;
    }

    public async ValueTask WriteToAsync(Stream stream, CancellationToken cancellationToken)
    {
        foreach (var part in _parts)
        {
            await stream.WriteAsync(part, cancellationToken).ConfigureAwait(false);
        }
    }
}
