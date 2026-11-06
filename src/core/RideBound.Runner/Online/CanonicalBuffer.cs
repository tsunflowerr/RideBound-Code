using System.Buffers.Text;
using System.Text;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;

namespace RideBound.Runner.Online;

/// <summary>
/// Writes canonical JSON v1 bytes directly (docs/06 section 10.1; CanonicalJson.cs).
/// The output is byte-identical to <c>CanonicalJson.Canonicalize</c> applied to the same
/// value: members are written in ordinal name order (enforced at run time), strings use
/// exactly CanonicalJson.WriteJsonString escaping, integers use Utf8Formatter inside the
/// canonical safe range, and null is rejected. Property names must be plain ASCII names
/// that need no escaping.
/// </summary>
internal sealed class CanonicalBuffer
{
    private byte[] _buffer;
    private int _length;

    public CanonicalBuffer(int capacity = 4096)
    {
        _buffer = new byte[Math.Max(capacity, 64)];
    }

    public int Length => _length;

    public byte[] Array => _buffer;

    public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _length);

    public void Reset() => _length = 0;

    public byte[] ToArray() => _buffer.AsSpan(0, _length).ToArray();

    public void Raw(ReadOnlySpan<byte> value)
    {
        Ensure(value.Length);
        value.CopyTo(_buffer.AsSpan(_length));
        _length += value.Length;
    }

    public void Byte(byte value)
    {
        Ensure(1);
        _buffer[_length++] = value;
    }

    public void Integer(long value)
    {
        if (value is < ProtocolLimits.MinCanonicalInteger
            or > ProtocolLimits.MaxCanonicalInteger)
        {
            throw new CanonicalJsonException(
                CanonicalJsonErrorCode.IntegerOutOfRange,
                "$",
                "Integer is outside the canonical safe range.");
        }

        Ensure(20);

        if (!Utf8Formatter.TryFormat(value, _buffer.AsSpan(_length), out var written))
        {
            throw new InvalidOperationException("Could not format a canonical integer.");
        }

        _length += written;
    }

    public void Boolean(bool value) => Raw(value ? "true"u8 : "false"u8);

    public void String(string? value)
    {
        if (value is null)
        {
            throw new CanonicalJsonException(
                CanonicalJsonErrorCode.NullNotAllowed,
                "$",
                "Null is not permitted in canonical protocol JSON.");
        }

        // Fast path: printable ASCII without '"' or '\' is copied byte for byte, which is
        // exactly what CanonicalJson.WriteJsonString emits for such a string.
        var plain = true;

        foreach (var character in value)
        {
            if (character < 0x20 || character > 0x7E || character == '"' || character == '\\')
            {
                plain = false;
                break;
            }
        }

        Ensure(value.Length + 2);
        _buffer[_length++] = (byte)'"';

        if (plain)
        {
            for (var index = 0; index < value.Length; index++)
            {
                _buffer[_length + index] = (byte)value[index];
            }

            _length += value.Length;
        }
        else
        {
            WriteEscaped(value);
        }

        Byte((byte)'"');
    }

    // Mirrors CanonicalJson.EnsureValidUnicode + CanonicalJson.WriteJsonString (CanonicalJson.cs:203-303).
    private void WriteEscaped(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];

            if (char.IsHighSurrogate(character))
            {
                if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                {
                    throw InvalidUnicode();
                }
            }
            else if (char.IsLowSurrogate(character)
                && (index == 0 || !char.IsHighSurrogate(value[index - 1])))
            {
                throw InvalidUnicode();
            }
        }

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];

            switch (character)
            {
                case '"':
                    Raw("\\\""u8);
                    continue;
                case '\\':
                    Raw("\\\\"u8);
                    continue;
                case '\b':
                    Raw("\\b"u8);
                    continue;
                case '\t':
                    Raw("\\t"u8);
                    continue;
                case '\n':
                    Raw("\\n"u8);
                    continue;
                case '\f':
                    Raw("\\f"u8);
                    continue;
                case '\r':
                    Raw("\\r"u8);
                    continue;
            }

            if (character <= 0x1F)
            {
                const string HexDigits = "0123456789abcdef";
                Ensure(6);
                _buffer[_length++] = (byte)'\\';
                _buffer[_length++] = (byte)'u';
                _buffer[_length++] = (byte)'0';
                _buffer[_length++] = (byte)'0';
                _buffer[_length++] = (byte)HexDigits[(character >> 4) & 0xF];
                _buffer[_length++] = (byte)HexDigits[character & 0xF];
                continue;
            }

            Rune rune;

            if (char.IsHighSurrogate(character))
            {
                _ = Rune.TryCreate(character, value[++index], out rune);
            }
            else
            {
                _ = Rune.TryCreate(character, out rune);
            }

            Ensure(4);
            _length += rune.EncodeToUtf8(_buffer.AsSpan(_length));
        }
    }

    private static CanonicalJsonException InvalidUnicode() =>
        new(
            CanonicalJsonErrorCode.InvalidUnicode,
            "$",
            "String contains an unpaired UTF-16 surrogate.");

    private void Ensure(int additional)
    {
        if (_length + additional <= _buffer.Length)
        {
            return;
        }

        var size = Math.Max(_buffer.Length * 2, _length + additional);
        System.Array.Resize(ref _buffer, size);
    }
}

/// <summary>
/// Writes one canonical object: members must be added in strictly increasing ordinal
/// name order (the order CanonicalJson.WriteObject sorts to); a wrong order throws, so a
/// coding error can never silently produce non-canonical bytes.
/// </summary>
internal ref struct CanonicalObject
{
    private readonly CanonicalBuffer _buffer;
    private ReadOnlySpan<byte> _lastName;
    private bool _first;

    public CanonicalObject(CanonicalBuffer buffer)
    {
        _buffer = buffer;
        _lastName = default;
        _first = true;
        buffer.Byte((byte)'{');
    }

    public CanonicalBuffer Member(ReadOnlySpan<byte> name)
    {
        if (_first)
        {
            _first = false;
        }
        else
        {
            if (name.SequenceCompareTo(_lastName) <= 0)
            {
                throw new InvalidOperationException(
                    $"Canonical member '{Encoding.ASCII.GetString(name)}' is out of ordinal order.");
            }

            _buffer.Byte((byte)',');
        }

        _lastName = name;
        _buffer.Byte((byte)'"');
        _buffer.Raw(name);
        _buffer.Byte((byte)'"');
        _buffer.Byte((byte)':');
        return _buffer;
    }

    public void End() => _buffer.Byte((byte)'}');
}
