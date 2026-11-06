using System.Buffers;
using System.Text.Json;

namespace RideBound.Contracts.Protocol;

public enum ProtocolPayloadErrorCode
{
    DuplicateField,
    UnknownField,
    MissingRequiredField,
    InvalidFieldType,
    InvalidValue,
    ValueOutOfRange,
}

public sealed record ProtocolPayloadError(
    ProtocolPayloadErrorCode Code,
    string Field,
    string Message);

public sealed record ProtocolPayloadDecodeResult<T>
    where T : class
{
    private ProtocolPayloadDecodeResult(T? value, ProtocolPayloadError? error)
    {
        Value = value;
        Error = error;
    }

    public bool IsSuccess => Value is not null;

    public T? Value { get; }

    public ProtocolPayloadError? Error { get; }

    internal static ProtocolPayloadDecodeResult<T> Success(T value) => new(value, null);

    internal static ProtocolPayloadDecodeResult<T> Failure(ProtocolPayloadError error) =>
        new(null, error);
}

internal readonly record struct ProtocolValueReadResult<T>(
    T? Value,
    ProtocolPayloadError? Error)
{
    public bool IsSuccess => Error is null;

    public static ProtocolValueReadResult<T> Success(T value) => new(value, null);

    public static ProtocolValueReadResult<T> Failure(ProtocolPayloadError error) =>
        new(default, error);
}

internal static class ProtocolPayloadReader
{
    public static ProtocolPayloadError? ValidateObject(
        JsonElement element,
        string path,
        IReadOnlySet<string> allowedFields)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return new ProtocolPayloadError(
                ProtocolPayloadErrorCode.InvalidFieldType,
                path,
                $"Field '{path}' must be an object.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in element.EnumerateObject())
        {
            var fieldPath = Join(path, property.Name);

            if (!seen.Add(property.Name))
            {
                return new ProtocolPayloadError(
                    ProtocolPayloadErrorCode.DuplicateField,
                    fieldPath,
                    $"Field '{fieldPath}' appears more than once.");
            }

            if (!allowedFields.Contains(property.Name))
            {
                return new ProtocolPayloadError(
                    ProtocolPayloadErrorCode.UnknownField,
                    fieldPath,
                    $"Field '{fieldPath}' is not defined in protocol v1.");
            }
        }

        return null;
    }

    public static ProtocolValueReadResult<JsonElement> ReadRequiredProperty(
        JsonElement element,
        string path,
        string field)
    {
        var fieldPath = Join(path, field);

        if (!element.TryGetProperty(field, out var value))
        {
            return ProtocolValueReadResult<JsonElement>.Failure(
                new ProtocolPayloadError(
                    ProtocolPayloadErrorCode.MissingRequiredField,
                    fieldPath,
                    $"Required field '{fieldPath}' is missing."));
        }

        return ProtocolValueReadResult<JsonElement>.Success(value);
    }

    public static ProtocolValueReadResult<string> ReadRequiredString(
        JsonElement element,
        string path,
        string field,
        bool requireOpaqueValue = true)
    {
        var property = ReadRequiredProperty(element, path, field);

        if (!property.IsSuccess)
        {
            return ProtocolValueReadResult<string>.Failure(property.Error!);
        }

        var fieldPath = Join(path, field);

        if (property.Value.ValueKind != JsonValueKind.String)
        {
            return WrongType<string>(fieldPath, "a string");
        }

        var value = property.Value.GetString()!;

        if (requireOpaqueValue && !OpaqueIdentifier.IsValid(value))
        {
            return ProtocolValueReadResult<string>.Failure(
                new ProtocolPayloadError(
                    ProtocolPayloadErrorCode.InvalidValue,
                    fieldPath,
                    $"Field '{fieldPath}' must contain 1 to 128 valid UTF-8 bytes."));
        }

        return ProtocolValueReadResult<string>.Success(value);
    }

    public static ProtocolValueReadResult<string?> ReadOptionalString(
        JsonElement element,
        string path,
        string field)
    {
        if (!element.TryGetProperty(field, out var property))
        {
            return ProtocolValueReadResult<string?>.Success(null);
        }

        var fieldPath = Join(path, field);

        if (property.ValueKind != JsonValueKind.String)
        {
            return WrongType<string?>(fieldPath, "a string");
        }

        var value = property.GetString()!;

        if (!OpaqueIdentifier.IsValid(value))
        {
            return ProtocolValueReadResult<string?>.Failure(
                new ProtocolPayloadError(
                    ProtocolPayloadErrorCode.InvalidValue,
                    fieldPath,
                    $"Field '{fieldPath}' must contain 1 to 128 valid UTF-8 bytes."));
        }

        return ProtocolValueReadResult<string?>.Success(value);
    }

    public static ProtocolValueReadResult<long> ReadRequiredInteger(
        JsonElement element,
        string path,
        string field,
        long minimum,
        long maximum = ProtocolLimits.MaxCanonicalInteger)
    {
        var property = ReadRequiredProperty(element, path, field);

        if (!property.IsSuccess)
        {
            return ProtocolValueReadResult<long>.Failure(property.Error!);
        }

        var fieldPath = Join(path, field);

        if (property.Value.ValueKind != JsonValueKind.Number)
        {
            return WrongType<long>(fieldPath, "an integer");
        }

        var raw = property.Value.GetRawText();

        if (!property.Value.TryGetInt64(out var value)
            || raw.AsSpan().IndexOfAny('.', 'e', 'E') >= 0
            || raw == "-0"
            || value < minimum
            || value > maximum)
        {
            return ProtocolValueReadResult<long>.Failure(
                new ProtocolPayloadError(
                    ProtocolPayloadErrorCode.ValueOutOfRange,
                    fieldPath,
                    $"Field '{fieldPath}' is outside its canonical integer range."));
        }

        return ProtocolValueReadResult<long>.Success(value);
    }

    public static ProtocolValueReadResult<string[]> ReadRequiredStringSet(
        JsonElement element,
        string path,
        string field,
        bool allowEmpty)
    {
        var property = ReadRequiredProperty(element, path, field);

        if (!property.IsSuccess)
        {
            return ProtocolValueReadResult<string[]>.Failure(property.Error!);
        }

        var fieldPath = Join(path, field);

        if (property.Value.ValueKind != JsonValueKind.Array)
        {
            return WrongType<string[]>(fieldPath, "an array");
        }

        var values = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;

        foreach (var item in property.Value.EnumerateArray())
        {
            var itemPath = $"{fieldPath}[{index}]";

            if (item.ValueKind != JsonValueKind.String)
            {
                return WrongType<string[]>(itemPath, "a string");
            }

            var value = item.GetString()!;

            if (!OpaqueIdentifier.IsValid(value))
            {
                return ProtocolValueReadResult<string[]>.Failure(
                    new ProtocolPayloadError(
                        ProtocolPayloadErrorCode.InvalidValue,
                        itemPath,
                        $"Field '{itemPath}' must contain 1 to 128 valid UTF-8 bytes."));
            }

            if (!seen.Add(value))
            {
                return ProtocolValueReadResult<string[]>.Failure(
                    new ProtocolPayloadError(
                        ProtocolPayloadErrorCode.InvalidValue,
                        itemPath,
                        $"Set field '{fieldPath}' contains duplicate value '{value}'."));
            }

            values.Add(value);
            index++;
        }

        if (!allowEmpty && values.Count == 0)
        {
            return ProtocolValueReadResult<string[]>.Failure(
                new ProtocolPayloadError(
                    ProtocolPayloadErrorCode.InvalidValue,
                    fieldPath,
                    $"Set field '{fieldPath}' must not be empty."));
        }

        values.Sort(StringComparer.Ordinal);
        return ProtocolValueReadResult<string[]>.Success(values.ToArray());
    }

    public static byte[] Write(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            write(writer);
        }

        return buffer.WrittenSpan.ToArray();
    }

    public static string Join(string path, string field) =>
        path == "$" ? $"$.{field}" : $"{path}.{field}";

    private static ProtocolValueReadResult<T> WrongType<T>(
        string fieldPath,
        string expected)
    {
        return ProtocolValueReadResult<T>.Failure(
            new ProtocolPayloadError(
                ProtocolPayloadErrorCode.InvalidFieldType,
                fieldPath,
                $"Field '{fieldPath}' must be {expected}."));
    }
}
