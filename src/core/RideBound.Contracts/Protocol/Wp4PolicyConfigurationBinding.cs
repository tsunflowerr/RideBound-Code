using System.Security.Cryptography;
using System.Text;

namespace RideBound.Contracts.Protocol;

/// <summary>
/// Defines the exact Runner-visible identity of the combined WP3 commitment and
/// WP4 policy configuration: the domain-separated digest carried by
/// initializeRun.
/// </summary>
public static class Wp4PolicyConfigurationBinding
{
    private static readonly byte[] BindingDomain =
        "RideBound.Wp4RunnerConfigurationBinding.v1\0"u8.ToArray();

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
}
