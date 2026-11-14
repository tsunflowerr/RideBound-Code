using System.Text.Json;

namespace RideBound.Runner.Configuration;

/// <summary>
/// JSON readers and the removed-feature pointer shared by the WP4 and the commitment configuration parsers.
/// Their error texts are what a refused configuration reports, so their wording must stay as it is.
/// </summary>
internal static class JsonConfig
{
    /// <summary>The pointer that every refused removed feature carries.</summary>
    public const string RemovedFeature =
        "removed; see commit ebca912 (branch research/final-real-runs-2026-11-06)";

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
