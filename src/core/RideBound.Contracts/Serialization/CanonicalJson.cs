using System.Buffers;
using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using RideBound.Contracts.Protocol;

namespace RideBound.Contracts.Serialization;

public enum CanonicalJsonErrorCode
{
    MalformedJson,
    DuplicateProperty,
    InvalidUnicode,
    NullNotAllowed,
    NonIntegerNumber,
    IntegerOutOfRange,
}

public sealed class CanonicalJsonException : Exception
{
    public CanonicalJsonException(
        CanonicalJsonErrorCode code,
        string path,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        Path = path;
    }

    public CanonicalJsonErrorCode Code { get; }

    public string Path { get; }
}

public static class CanonicalJson
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
    };

    public static byte[] Serialize(ProtocolEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return Canonicalize(ProtocolEnvelopeCodec.Encode(envelope));
    }

    public static byte[] Canonicalize(ReadOnlySpan<byte> utf8Json)
    {
        ValidateRawUnicodeEscapes(utf8Json);

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(utf8Json.ToArray(), DocumentOptions);
        }
        catch (JsonException exception)
        {
            throw new CanonicalJsonException(
                CanonicalJsonErrorCode.MalformedJson,
                "$",
                $"Input is not valid JSON at byte {exception.BytePositionInLine}.",
                exception);
        }

        using (document)
        {
            var buffer = new ArrayBufferWriter<byte>();
            WriteElement(buffer, document.RootElement, "$");
            return buffer.WrittenSpan.ToArray();
        }
    }

    private static void WriteElement(
        ArrayBufferWriter<byte> writer,
        JsonElement element,
        string path)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                WriteObject(writer, element, path);
                break;
            case JsonValueKind.Array:
                WriteByte(writer, (byte)'[');

                var index = 0;

                foreach (var item in element.EnumerateArray())
                {
                    if (index > 0)
                    {
                        WriteByte(writer, (byte)',');
                    }

                    WriteElement(writer, item, $"{path}[{index}]");
                    index++;
                }

                WriteByte(writer, (byte)']');
                break;
            case JsonValueKind.String:
                var value = element.GetString()!;
                EnsureValidUnicode(value, path);
                WriteJsonString(writer, value);
                break;
            case JsonValueKind.Number:
                WriteInteger(writer, ReadCanonicalInteger(element, path));
                break;
            case JsonValueKind.True:
                WriteAscii(writer, "true"u8);
                break;
            case JsonValueKind.False:
                WriteAscii(writer, "false"u8);
                break;
            case JsonValueKind.Null:
                throw new CanonicalJsonException(
                    CanonicalJsonErrorCode.NullNotAllowed,
                    path,
                    $"Null is not permitted in canonical protocol JSON at '{path}'.");
            default:
                throw new CanonicalJsonException(
                    CanonicalJsonErrorCode.MalformedJson,
                    path,
                    $"Unsupported JSON token at '{path}'.");
        }
    }

    private static void WriteObject(
        ArrayBufferWriter<byte> writer,
        JsonElement element,
        string path)
    {
        var properties = new List<JsonProperty>();
        var propertyNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in element.EnumerateObject())
        {
            EnsureValidUnicode(property.Name, path);

            if (!propertyNames.Add(property.Name))
            {
                throw new CanonicalJsonException(
                    CanonicalJsonErrorCode.DuplicateProperty,
                    path,
                    $"Property '{property.Name}' appears more than once at '{path}'.");
            }

            properties.Add(property);
        }

        properties.Sort(
            static (left, right) =>
                StringComparer.Ordinal.Compare(left.Name, right.Name));

        WriteByte(writer, (byte)'{');

        for (var index = 0; index < properties.Count; index++)
        {
            if (index > 0)
            {
                WriteByte(writer, (byte)',');
            }

            var property = properties[index];
            WriteJsonString(writer, property.Name);
            WriteByte(writer, (byte)':');
            WriteElement(writer, property.Value, $"{path}.{property.Name}");
        }

        WriteByte(writer, (byte)'}');
    }

    private static long ReadCanonicalInteger(JsonElement element, string path)
    {
        var rawValue = element.GetRawText();

        if (rawValue.AsSpan().IndexOfAny('.', 'e', 'E') >= 0 || rawValue == "-0")
        {
            throw new CanonicalJsonException(
                CanonicalJsonErrorCode.NonIntegerNumber,
                path,
                $"Number at '{path}' is not a canonical integer.");
        }

        if (!element.TryGetInt64(out var value)
            || value is < ProtocolLimits.MinCanonicalInteger
                or > ProtocolLimits.MaxCanonicalInteger)
        {
            throw new CanonicalJsonException(
                CanonicalJsonErrorCode.IntegerOutOfRange,
                path,
                $"Integer at '{path}' is outside the canonical safe range.");
        }

        return value;
    }

    private static void EnsureValidUnicode(string value, string path)
    {
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];

            if (char.IsHighSurrogate(character))
            {
                if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                {
                    throw InvalidUnicode(path);
                }

                index++;
                continue;
            }

            if (char.IsLowSurrogate(character))
            {
                throw InvalidUnicode(path);
            }
        }
    }

    private static CanonicalJsonException InvalidUnicode(string path)
    {
        return new CanonicalJsonException(
            CanonicalJsonErrorCode.InvalidUnicode,
            path,
            $"String at '{path}' contains an unpaired UTF-16 surrogate.");
    }

    private static void WriteInteger(ArrayBufferWriter<byte> writer, long value)
    {
        var destination = writer.GetSpan(20);

        if (!Utf8Formatter.TryFormat(value, destination, out var bytesWritten))
        {
            throw new InvalidOperationException("Could not format a canonical integer.");
        }

        writer.Advance(bytesWritten);
    }

    private static void WriteJsonString(ArrayBufferWriter<byte> writer, string value)
    {
        WriteByte(writer, (byte)'"');

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];

            switch (character)
            {
                case '"':
                    WriteAscii(writer, "\\\""u8);
                    continue;
                case '\\':
                    WriteAscii(writer, "\\\\"u8);
                    continue;
                case '\b':
                    WriteAscii(writer, "\\b"u8);
                    continue;
                case '\t':
                    WriteAscii(writer, "\\t"u8);
                    continue;
                case '\n':
                    WriteAscii(writer, "\\n"u8);
                    continue;
                case '\f':
                    WriteAscii(writer, "\\f"u8);
                    continue;
                case '\r':
                    WriteAscii(writer, "\\r"u8);
                    continue;
            }

            if (character <= 0x1F)
            {
                WriteControlCharacter(writer, character);
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

            var destination = writer.GetSpan(4);
            var bytesWritten = rune.EncodeToUtf8(destination);
            writer.Advance(bytesWritten);
        }

        WriteByte(writer, (byte)'"');
    }

    private static void WriteControlCharacter(
        ArrayBufferWriter<byte> writer,
        char character)
    {
        const string HexDigits = "0123456789abcdef";

        Span<byte> escaped = stackalloc byte[6];
        escaped[0] = (byte)'\\';
        escaped[1] = (byte)'u';
        escaped[2] = (byte)'0';
        escaped[3] = (byte)'0';
        escaped[4] = (byte)HexDigits[(character >> 4) & 0xF];
        escaped[5] = (byte)HexDigits[character & 0xF];
        WriteAscii(writer, escaped);
    }

    private static void WriteByte(ArrayBufferWriter<byte> writer, byte value)
    {
        var destination = writer.GetSpan(1);
        destination[0] = value;
        writer.Advance(1);
    }

    private static void WriteAscii(
        ArrayBufferWriter<byte> writer,
        ReadOnlySpan<byte> value)
    {
        value.CopyTo(writer.GetSpan(value.Length));
        writer.Advance(value.Length);
    }

    private static void ValidateRawUnicodeEscapes(ReadOnlySpan<byte> utf8Json)
    {
        var reader = new Utf8JsonReader(
            utf8Json,
            new JsonReaderOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            });

        try
        {
            while (reader.Read())
            {
                if (reader.TokenType is not JsonTokenType.String
                    and not JsonTokenType.PropertyName)
                {
                    continue;
                }

                var rawValue = reader.HasValueSequence
                    ? reader.ValueSequence.ToArray()
                    : reader.ValueSpan.ToArray();

                ValidateEscapedSurrogates(rawValue);
            }
        }
        catch (JsonException)
        {
            // JsonDocument below reports malformed JSON with a stable contract error.
        }
    }

    private static void ValidateEscapedSurrogates(ReadOnlySpan<byte> rawValue)
    {
        for (var index = 0; index < rawValue.Length; index++)
        {
            if (rawValue[index] != (byte)'\\')
            {
                continue;
            }

            if (index + 1 >= rawValue.Length || rawValue[index + 1] != (byte)'u')
            {
                index++;
                continue;
            }

            if (!TryReadHexCodeUnit(rawValue[(index + 2)..], out var codeUnit))
            {
                continue;
            }

            if (codeUnit is >= 0xDC00 and <= 0xDFFF)
            {
                throw InvalidUnicode("$");
            }

            if (codeUnit is < 0xD800 or > 0xDBFF)
            {
                index += 5;
                continue;
            }

            var lowSurrogateStart = index + 6;

            if (lowSurrogateStart + 5 >= rawValue.Length
                || rawValue[lowSurrogateStart] != (byte)'\\'
                || rawValue[lowSurrogateStart + 1] != (byte)'u'
                || !TryReadHexCodeUnit(
                    rawValue[(lowSurrogateStart + 2)..],
                    out var lowSurrogate)
                || lowSurrogate is < 0xDC00 or > 0xDFFF)
            {
                throw InvalidUnicode("$");
            }

            index = lowSurrogateStart + 5;
        }
    }

    private static bool TryReadHexCodeUnit(
        ReadOnlySpan<byte> value,
        out int codeUnit)
    {
        codeUnit = 0;

        if (value.Length < 4)
        {
            return false;
        }

        for (var index = 0; index < 4; index++)
        {
            var digit = value[index] switch
            {
                >= (byte)'0' and <= (byte)'9' => value[index] - (byte)'0',
                >= (byte)'a' and <= (byte)'f' => value[index] - (byte)'a' + 10,
                >= (byte)'A' and <= (byte)'F' => value[index] - (byte)'A' + 10,
                _ => -1,
            };

            if (digit < 0)
            {
                return false;
            }

            codeUnit = (codeUnit << 4) | digit;
        }

        return true;
    }
}
