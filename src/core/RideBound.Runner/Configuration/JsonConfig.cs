using System.Text.Json;

namespace RideBound.Runner.Configuration;

/// <summary>
/// JSON readers shared by the WP4 and the commitment configuration parsers.
/// Their error texts are what a refused configuration reports, so their wording must stay as it is.
/// </summary>
internal static class JsonConfig
{
    /// <summary>
    /// Requires the string field <paramref name="name"/> to hold <paramref name="value"/>. The final files set such a
    /// key to the one value the Runner supports; any other value, or no key, is refused.
    /// </summary>
    public static void RequireValue(JsonElement element, string name, string value, string path)
    {
        if (!element.TryGetProperty(name, out var field)
            || field.ValueKind != JsonValueKind.String
            || field.GetString() != value)
        {
            throw new InvalidDataException($"'{path}.{name}' must be '{value}'.");
        }
    }

    /// <summary>A set of field names compared ordinally.</summary>
    public static IReadOnlySet<string> Fields(params string[] names) =>
        new HashSet<string>(names, StringComparer.Ordinal);

    /// <summary>
    /// Requires an object with no field outside <paramref name="allowed"/> and every field of
    /// <paramref name="required"/>. An unknown field is reported before a missing one.
    /// </summary>
    public static void RequireObject(
        JsonElement element,
        IReadOnlySet<string> allowed,
        IReadOnlySet<string> required,
        string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"'{path}' must be an object.");
        }

        var names = element.EnumerateObject()
            .Select(value => value.Name)
            .ToArray();
        var unknown = names.FirstOrDefault(value => !allowed.Contains(value));
        var missing = required.FirstOrDefault(value => !names.Contains(value));

        if (unknown is not null || missing is not null)
        {
            throw new InvalidDataException(
                unknown is not null
                    ? $"Unknown field '{unknown}' at '{path}'."
                    : $"Missing field '{missing}' at '{path}'.");
        }
    }

    /// <summary>Reads a required string field that is not empty or blank.</summary>
    public static string Text(JsonElement element, string propertyName)
    {
        var value = element.GetProperty(propertyName);

        if (value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidDataException(
                $"'{propertyName}' must be a non-empty string.");
        }

        return value.GetString()!;
    }

    /// <summary>Reads an optional boolean field; a missing field reads as false.</summary>
    public static bool OptionalBoolean(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
            ? Boolean(value, name)
            : false;

    /// <summary>Reads a boolean value; <paramref name="name"/> is the field name used in the error text.</summary>
    public static bool Boolean(JsonElement element, string name) =>
        element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new InvalidDataException($"'{name}' must be a boolean."),
        };
}
