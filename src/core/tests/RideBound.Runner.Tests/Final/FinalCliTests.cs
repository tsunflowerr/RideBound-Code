namespace RideBound.Runner.Tests.Final;

/// <summary>
/// T2 of the refactor safety net: the command line of the Runner. It accepts exactly the form the final runs used
/// (FinalRun.Arguments), with or without the trailing line limit. Every other form exits 64 without reading stdin;
/// the forms the Runner accepted at ebca912 (the online and conformance modes, no arguments, and the shorter
/// commitment forms) name the commit where they still run.
/// </summary>
public sealed class FinalCliTests
{
    private const string Hello =
        """{"schemaVersion":"1.0.0","messageType":"hello","payload":{"adapterId":"wp6-tiny-ndjson","adapterVersion":"1.0.0","supportedSchemaVersions":["1.0.0"],"positionModel":"directedEdgeProgress","capabilities":["oldPlanProjection","cancellations","exactEventOrdering","dynamicTravelTimes"],"maxFleetSize":5000,"maxRequestCount":100000}}""";

    private const string Shutdown = """{"schemaVersion":"1.0.0","messageType":"shutdown","payload":{}}""";

    private const string Usage =
        "Usage: RideBound.Runner --mode commitment --policy-config <path> --wp4-config <path> "
        + "--solver-seed-source manifest-master-seed [--maximum-line-bytes <1048576..1073741824>]";

    private const string RemovedForms =
        "--mode online, --mode conformance, no arguments, and --mode commitment without --wp4-config or "
        + "--solver-seed-source: removed; see commit ebca912 (branch research/final-real-runs-2026-11-06)";

    private const string LineLimit = "--maximum-line-bytes must be an integer from 1048576 through 1073741824.";

    [Fact]
    public async Task The_final_argument_vector_with_a_one_gibibyte_line_limit_is_accepted()
    {
        var run = FinalRuns.Get("FS-G2-SHRC71-CL.light-v1");
        Assert.Equal("1073741824", run.Arguments[^1]);

        var result = await RunnerProcess.RunAsync(run.Arguments, FinalRuns.FixtureDirectory, [Hello, Shutdown]);

        AssertHelloAckOnly(result, "final form");
    }

    [Fact]
    public async Task The_final_form_without_the_line_limit_flag_prints_the_same_bytes()
    {
        var run = FinalRuns.Get("FS-G2-SHRC71-CL.light-v1");
        var session = await FinalRuns.SessionAsync(run);

        var result = await RunnerProcess.RunAsync(
            run.Arguments.Take(8).ToArray(),
            FinalRuns.FixtureDirectory,
            FinalRuns.ScenarioLines(run));

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        Assert.Equal(FinalRuns.Sha256(session.StandardOutput), FinalRuns.Sha256(result.StandardOutput));
    }

    /// <summary>The 6-argument form (no manifest seed) printed the final form's bytes at ebca912, because the
    /// configured randomSeed 7 equals the manifest's masterSeed 7; it is refused now, whatever the input.</summary>
    [Fact]
    public async Task The_six_argument_commitment_form_is_rejected_with_exit_64()
    {
        var run = FinalRuns.Get("FS-G2-SHRC71-CL.light-v1");

        var result = await RunnerProcess.RunAsync(
            run.Arguments.Take(6).ToArray(),
            FinalRuns.FixtureDirectory,
            FinalRuns.ScenarioLines(run));

        AssertRemovedForm(result, "commitment, 6 arguments, final scenario");
    }

    /// <summary>The forms the Runner accepted at ebca912 besides the final one.</summary>
    [Theory]
    [InlineData("no arguments (online rolling-cost mode)", "")]
    [InlineData("online", "--mode online")]
    [InlineData("conformance", "--mode conformance")]
    [InlineData("commitment, 4 arguments", "--mode commitment --policy-config commitment/SHRC71.json")]
    [InlineData("commitment, 6 arguments", "--mode commitment --policy-config commitment/SHRC71.json --wp4-config wp4/G2-CL-light-v1.json")]
    [InlineData("line limit alone", "--maximum-line-bytes 1048576")]
    [InlineData("online with a line limit", "--mode online --maximum-line-bytes 1073741824")]
    [InlineData("commitment, 4 arguments with a line limit", "--mode commitment --policy-config commitment/SHHZ71.json --maximum-line-bytes 1073741824")]
    [InlineData("commitment, 6 arguments with a line limit", "--mode commitment --policy-config commitment/INS-SHHZ.json --wp4-config wp4/INS-N-light-v1.json --maximum-line-bytes 2097152")]
    public async Task Removed_cli_forms_are_rejected_with_exit_64(string form, string arguments)
    {
        var result = await RunnerProcess.RunAsync(Split(arguments), FinalRuns.FixtureDirectory, [Hello, Shutdown]);

        AssertRemovedForm(result, form);
    }

    [Theory]
    [InlineData("unknown mode", "--mode replay", Usage)]
    [InlineData("mode without a value", "--mode", Usage)]
    [InlineData("seed source other than the manifest", "--mode commitment --policy-config commitment/SHRC71.json --wp4-config wp4/G2-CL-light-v1.json --solver-seed-source configuration", Usage)]
    [InlineData("wp4 file without the commitment form", "--mode online --wp4-config wp4/G2-CL-light-v1.json", Usage)]
    [InlineData("line limit before the mode", "--maximum-line-bytes 1073741824 --mode online", Usage)]
    [InlineData("line limit one byte below 1 MiB", "--mode online --maximum-line-bytes 1048575", LineLimit)]
    [InlineData("line limit one byte above 1 GiB", "--mode online --maximum-line-bytes 1073741825", LineLimit)]
    [InlineData("line limit that is not an integer", "--mode online --maximum-line-bytes 1e9", LineLimit)]
    [InlineData("missing commitment file", "--mode commitment --policy-config commitment/missing.json --wp4-config wp4/G2-CL-light-v1.json --solver-seed-source manifest-master-seed", "Invalid commitment policy configuration: ")]
    [InlineData("missing wp4 file", "--mode commitment --policy-config commitment/SHRC71.json --wp4-config wp4/missing.json --solver-seed-source manifest-master-seed", "Invalid commitment policy configuration: ")]
    public async Task Cli_forms_are_rejected_with_exit_64(string form, string arguments, string message)
    {
        var result = await RunnerProcess.RunAsync(Split(arguments), FinalRuns.FixtureDirectory, [Hello, Shutdown]);

        Assert.Equal(64, result.ExitCode);
        Assert.Empty(result.StandardOutput);
        Assert.True(
            result.StandardError.StartsWith(message, StringComparison.Ordinal),
            $"{form}: stderr was {result.StandardError}");
    }

    private static string[] Split(string arguments) =>
        arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static void AssertHelloAckOnly(RunnerProcessResult result, string form)
    {
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        var lines = result.Lines();
        Assert.True(
            lines.Count == 1 && RunnerProcess.MessageType(lines[0]) == "helloAck",
            $"{form}: expected one helloAck line, got {lines.Count} lines");
    }

    private static void AssertRemovedForm(RunnerProcessResult result, string form)
    {
        Assert.True(result.ExitCode == 64, $"{form}: exit code {result.ExitCode}");
        Assert.Empty(result.StandardOutput);
        Assert.Equal(Usage + Environment.NewLine + RemovedForms + Environment.NewLine, result.StandardError);
    }
}
