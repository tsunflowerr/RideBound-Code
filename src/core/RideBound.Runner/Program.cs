using RideBound.Runner.Configuration;
using RideBound.Runner.Protocol;

var executionMode = RunnerExecutionMode.OnlineRollingCost;
CommitmentPolicyConfiguration? commitmentConfiguration = null;
Wp4RunnerConfiguration? wp4Configuration = null;
var useManifestSolverSeed = false;
var maximumLineBytes = NdjsonReader.DefaultMaximumLineBytes;
var runnerArguments = args;

if (runnerArguments.Length >= 2
    && string.Equals(
        runnerArguments[^2],
        "--maximum-line-bytes",
        StringComparison.Ordinal))
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

if (runnerArguments.Length > 0)
{
    var isSimpleMode = runnerArguments.Length == 2
        && runnerArguments[1] is "online" or "conformance";
    var isCommitmentMode = runnerArguments.Length is 4 or 6 or 8
        && runnerArguments[1] == "commitment"
        && string.Equals(runnerArguments[2], "--policy-config", StringComparison.Ordinal)
        && (runnerArguments.Length == 4
            || string.Equals(runnerArguments[4], "--wp4-config", StringComparison.Ordinal))
        && (runnerArguments.Length != 8
            || string.Equals(
                runnerArguments[6],
                "--solver-seed-source",
                StringComparison.Ordinal)
                && string.Equals(
                    runnerArguments[7],
                    "manifest-master-seed",
                    StringComparison.Ordinal));

    if (!isSimpleMode && !isCommitmentMode
        || !string.Equals(runnerArguments[0], "--mode", StringComparison.Ordinal)
        || runnerArguments[1] is not ("online" or "conformance" or "commitment"))
    {
        await Console.Error.WriteLineAsync(
            "Usage: RideBound.Runner [--mode online|conformance] | " +
            "--mode commitment --policy-config <path> " +
            "[--wp4-config <path> " +
            "[--solver-seed-source manifest-master-seed]] " +
            "[--maximum-line-bytes <1048576..1073741824>]");
        return 64;
    }

    executionMode = runnerArguments[1] switch
    {
        "conformance" => RunnerExecutionMode.StructuralConformance,
        "commitment" => RunnerExecutionMode.OnlineCommitment,
        _ => RunnerExecutionMode.OnlineRollingCost,
    };

    if (executionMode == RunnerExecutionMode.OnlineCommitment)
    {
        try
        {
            commitmentConfiguration = CommitmentPolicyConfiguration.Decode(
                await File.ReadAllBytesAsync(runnerArguments[3]));

            if (runnerArguments.Length is 6 or 8)
            {
                wp4Configuration = Wp4RunnerConfiguration.Decode(
                    await File.ReadAllBytesAsync(runnerArguments[5]),
                    commitmentConfiguration);
                useManifestSolverSeed = runnerArguments.Length == 8;
            }
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
    }
}

return await RunnerHost.RunAsync(
    Console.OpenStandardInput(),
    Console.OpenStandardOutput(),
    Console.Error,
    maximumLineBytes: maximumLineBytes,
    executionMode: executionMode,
    commitmentPolicies: commitmentConfiguration,
    stopDistances: commitmentConfiguration,
    commitmentPolicyConfigurationHash: commitmentConfiguration is null
        ? null
        : wp4Configuration?.BindToCommitmentConfiguration(
            commitmentConfiguration.ContentHash)
            ?? commitmentConfiguration.ContentHash,
    wp4Configuration: wp4Configuration,
    useManifestSolverSeed: useManifestSolverSeed);
