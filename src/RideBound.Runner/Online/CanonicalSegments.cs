using System.Security.Cryptography;

namespace RideBound.Runner.Online;

/// <summary>
/// The canonical bytes of one value as an ordered list of immutable byte ranges.
/// Cached fragments are shared by reference, so building the list costs O(number of
/// fragments), not O(bytes); hashing or copying the list visits every byte once.
/// </summary>
public sealed class CanonicalSegments
{
    private readonly List<ArraySegment<byte>> _parts;

    internal CanonicalSegments(List<ArraySegment<byte>> parts, long length)
    {
        _parts = parts;
        Length = length;
    }

    public long Length { get; }

    public int PartCount => _parts.Count;

    public IReadOnlyList<ArraySegment<byte>> Parts => _parts;

    public void AppendTo(IncrementalHash hash)
    {
        foreach (var part in _parts)
        {
            hash.AppendData(part.AsSpan());
        }
    }

    public void CopyTo(Span<byte> destination)
    {
        var offset = 0;

        foreach (var part in _parts)
        {
            part.AsSpan().CopyTo(destination[offset..]);
            offset += part.Count;
        }
    }

    public byte[] ToArray()
    {
        var result = GC.AllocateUninitializedArray<byte>(checked((int)Length));
        CopyTo(result);
        return result;
    }

    public bool ContentEquals(ReadOnlySpan<byte> other)
    {
        if (other.Length != Length)
        {
            return false;
        }

        var offset = 0;

        foreach (var part in _parts)
        {
            if (!part.AsSpan().SequenceEqual(other.Slice(offset, part.Count)))
            {
                return false;
            }

            offset += part.Count;
        }

        return true;
    }
}

/// <summary>
/// Collects glue bytes and cached fragments in output order. Glue is written into one
/// growable buffer; the glue ranges are turned into segments only when the build ends,
/// after the buffer can no longer move.
/// </summary>
internal sealed class CanonicalSegmentBuilder
{
    private readonly List<(byte[]? Fragment, int Start, int Length)> _pending = [];
    private int _glueStart;
    private long _length;

    public CanonicalSegmentBuilder(int glueCapacity = 4096)
    {
        Glue = new CanonicalBuffer(glueCapacity);
    }

    public CanonicalBuffer Glue { get; }

    public void Fragment(byte[] fragment)
    {
        FlushGlue();

        if (fragment.Length != 0)
        {
            _pending.Add((fragment, 0, fragment.Length));
            _length += fragment.Length;
        }
    }

    public CanonicalSegments Build()
    {
        FlushGlue();
        var glue = Glue.Array;
        var parts = new List<ArraySegment<byte>>(_pending.Count);

        foreach (var (fragment, start, length) in _pending)
        {
            parts.Add(new ArraySegment<byte>(fragment ?? glue, start, length));
        }

        return new CanonicalSegments(parts, _length);
    }

    private void FlushGlue()
    {
        var end = Glue.Length;

        if (end > _glueStart)
        {
            _pending.Add((null, _glueStart, end - _glueStart));
            _length += end - _glueStart;
            _glueStart = end;
        }
    }
}
