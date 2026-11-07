using RideBound.Runner.Configuration;
using RideBound.Runner.Protocol;

// The Runner accepts exactly one command line, the one the final runs used:
//   --mode commitment --policy-config <commitment file> --wp4-config <WP4 file>
//   --solver-seed-source manifest-master-seed [--maximum-line-bytes <1048576..1073741824>]
// Every other command line exits with code 64.
const string Usage =
    "Usage: RideBound.Runner --mode commitment --policy-config <path> --wp4-config <path> "
    + "--solver-seed-source manifest-master-seed [--maximum-line-bytes <1048576..1073741824>]";
const string RemovedForms =
    "--mode online, --mode conformance, no arguments, and --mode commitment without --wp4-config or "
    + "--solver-seed-source: removed; see commit ebca912 (branch research/final-real-runs-2026-11-06)";

var runnerArguments = args;
var maximumLineBytes = NdjsonReader.DefaultMaximumLineBytes;

if (runnerArguments.Length >= 2 && runnerArguments[^2] == "--maximum-line-bytes")
{
    if (!int.TryParse(
            runnerArguments[^1],
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out maximumLineBytes)
        || maximumLineBytes < NdjsonReader.DefaultMaximumLineBytes
        || maximumLineBytes > NdjsonReader.MaximumConfigurableLineBytes)
    {
        await Console.Error.WriteLineAsync(
            "--maximum-line-bytes must be an integer from 1048576 through 1073741824.");
        return 64;
    }

    runnerArguments = runnerArguments[..^2];
}

if (runnerArguments is not
    [
        "--mode", "commitment",
        "--policy-config", var policyPath,
        "--wp4-config", var wp4Path,
        "--solver-seed-source", "manifest-master-seed"
    ])
{
    await Console.Error.WriteLineAsync(Usage);
    await Console.Error.WriteLineAsync(RemovedForms);
    return 64;
}

CommitmentPolicyConfiguration commitmentConfiguration;
Wp4RunnerConfiguration wp4Configuration;

try
{
    commitmentConfiguration = CommitmentPolicyConfiguration.Decode(
        await File.ReadAllBytesAsync(policyPath));
    wp4Configuration = Wp4RunnerConfiguration.Decode(
        await File.ReadAllBytesAsync(wp4Path),
        commitmentConfiguration);
}
catch (Exception error) when (
    error is IOException
        or UnauthorizedAccessException
        or InvalidDataException
        or ArgumentException
        or InvalidOperationException
        or System.Text.Json.JsonException
        or RideBound.Contracts.Serialization.CanonicalJsonException)
{
    await Console.Error.WriteLineAsync(
        $"Invalid commitment policy configuration: {error.Message}");
    return 64;
}

return await RunnerHost.RunAsync(
    Console.OpenStandardInput(),
    Console.OpenStandardOutput(),
    Console.Error,
    commitmentPolicies: commitmentConfiguration,
    stopDistances: commitmentConfiguration,
    commitmentPolicyConfigurationHash: wp4Configuration.BindToCommitmentConfiguration(
        commitmentConfiguration.ContentHash),
    wp4Configuration: wp4Configuration,
    useManifestSolverSeed: true,
    maximumLineBytes: maximumLineBytes);
