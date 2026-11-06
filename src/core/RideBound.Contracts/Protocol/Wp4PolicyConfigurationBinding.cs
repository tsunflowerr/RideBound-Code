using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RideBound.Contracts.Serialization;

namespace RideBound.Contracts.Protocol;

/// <summary>
/// Defines the exact Runner-visible identity of the combined WP3 commitment and
/// WP4 policy configuration. The canonical document is provenance; the
/// domain-separated digest is the value carried by initializeRun.
/// </summary>
public static class Wp4PolicyConfigurationBinding
{
    public const string BindingId = "ridebound-wp4-policy-binding-v1";

    public const string SchemaVersion = "1.0.0";

    private static readonly byte[] BindingDomain =
        "RideBound.Wp4RunnerConfigurationBinding.v1\0"u8.ToArray();

    private static readonly IReadOnlySet<string> DocumentFields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "bindingId",
            "commitmentConfigurationSha256",
            "schemaVersion",
            "wp4ConfigurationSha256",
        };

    public static Sha256Hex Calculate(
        Sha256Hex commitmentConfigurationSha256,
        Sha256Hex wp4ConfigurationSha256)
    {
        ArgumentNullException.ThrowIfNull(commitmentConfigurationSha256);
        ArgumentNullException.ThrowIfNull(wp4ConfigurationSha256);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(BindingDomain);
        hash.AppendData(Convert.FromHexString(commitmentConfigurationSha256.Value));
        hash.AppendData(Convert.FromHexString(wp4ConfigurationSha256.Value));
        _ = Sha256Hex.TryCreate(
            Convert.ToHexStringLower(hash.GetHashAndReset()),
            out var result);
        return result!;
    }

    public static byte[] CreateCanonicalDocument(
        Sha256Hex commitmentConfigurationSha256,
        Sha256Hex wp4ConfigurationSha256)
    {
        ArgumentNullException.ThrowIfNull(commitmentConfigurationSha256);
        ArgumentNullException.ThrowIfNull(wp4ConfigurationSha256);
        return CanonicalJson.Canonicalize(
            JsonSerializer.SerializeToUtf8Bytes(
                new
                {
                    bindingId = BindingId,
                    commitmentConfigurationSha256 =
                        commitmentConfigurationSha256.Value,
                    schemaVersion = SchemaVersion,
                    wp4ConfigurationSha256 = wp4ConfigurationSha256.Value,
                }));
    }

    public static Sha256Hex DecodeExactAndCalculate(
        ReadOnlySpan<byte> canonicalDocument)
    {
        byte[] canonical;

        try
        {
            canonical = CanonicalJson.Canonicalize(canonicalDocument);
        }
        catch (CanonicalJsonException exception)
        {
            throw new InvalidDataException(
                "WP4 policy binding document is not canonicalizable.",
                exception);
        }

        if (!canonicalDocument.SequenceEqual(canonical))
        {
            throw new InvalidDataException(
                "WP4 policy binding document must use exact canonical JSON bytes.");
        }

        using var document = JsonDocument.Parse(canonical);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(
                "WP4 policy binding document must be an object.");
        }

        var names = root.EnumerateObject().Select(value => value.Name).ToArray();

        if (names.Length != DocumentFields.Count
            || names.Distinct(StringComparer.Ordinal).Count() != names.Length
            || names.Any(value => !DocumentFields.Contains(value)))
        {
            throw new InvalidDataException(
                "WP4 policy binding document fields are not exact.");
        }

        if (Text(root, "schemaVersion") != SchemaVersion
            || Text(root, "bindingId") != BindingId
            || !Sha256Hex.TryCreate(
                Text(root, "commitmentConfigurationSha256"),
                out var commitment)
            || !Sha256Hex.TryCreate(
                Text(root, "wp4ConfigurationSha256"),
                out var wp4))
        {
            throw new InvalidDataException(
                "WP4 policy binding document values are invalid.");
        }

        return Calculate(commitment!, wp4!);
    }

    private static string Text(JsonElement root, string name)
    {
        var value = root.GetProperty(name);
        return value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw new InvalidDataException(
                $"WP4 policy binding field '{name}' must be a string.");
    }
}
