using System.Text.Json;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;

namespace RideBound.Runner.Tests.Final;

/// <summary>
/// The final experiment's Runner configuration,
/// driven through the real CLI process exactly like the simulator adapter drives it: every stdout byte equals the
/// recording made with the Runner at commit ebca912, the 17 checkpoints of each run included.
/// The inputs are machine independent: networkDirectory is relative and the child's working directory is Fixtures/.
/// </summary>
public sealed class FinalConfigGoldenTests
{
    public static TheoryData<string> Runs => FinalRuns.Names;

    [Theory]
    [MemberData(nameof(Runs))]
    public async Task Final_session_prints_the_stdout_recorded_at_ebca912(string name)
    {
        var run = FinalRuns.Get(name);

        var session = await FinalRuns.SessionAsync(run);

        Assert.Equal(string.Empty, session.StandardError);
        Assert.Equal(0, session.ExitCode);
        FinalRuns.AssertRecordedStdout(run, session.StandardOutput);
    }

    [Fact]
    public async Task An_early_boarding_session_fails_with_the_error_recorded_at_ebca912()
    {
        // Rider r-1 boards about 16 s before its promised pickup. An onboard rider keeps that promised pickup, and at
        // batch 11 its projected drop comes before it, so the vehicle loses every candidate, the kept route too. The
        // Runner answers INTERNAL_ERROR and fails the session; the checkpoint request after it is refused.
        var run = FinalRuns.EarlyBoarding;

        var session = await RunnerProcess.RunAsync(run.Arguments, FinalRuns.FixtureDirectory, FinalRuns.ScenarioLines(run));

        Assert.Equal(0, session.ExitCode);
        Assert.Equal(
            ["Protocol error: INTERNAL_ERROR", "Protocol error: INVALID_SESSION_STATE"],
            session.StandardError.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries));
        FinalRuns.AssertRecordedStdout(run, session.StandardOutput);
        var output = session.Lines();
        Assert.Equal(10, output.Count(line => RunnerProcess.MessageType(line) == "decision"));
        Assert.Equal(
            ((long?)11, "INTERNAL_ERROR", "failSession",
                "C1 rejected every generated candidate for this vehicle. No-op rejection: "
                + "Promise stops, ETA order and service order must be valid."),
            ReadError(output[^2]));
        Assert.Equal(
            ((long?)null, "INVALID_SESSION_STATE", "rejectMessage", "A failed session accepts only shutdown."),
            ReadError(output[^1]));
    }

    [Theory]
    [MemberData(nameof(Runs))]
    public void The_run_table_binds_each_wp4_file_to_its_commitment_file(string name)
    {
        var run = FinalRuns.Get(name);

        var binding = Wp4PolicyConfigurationBinding.Calculate(
            ContentHash(Path.Combine(FinalRuns.FixtureDirectory, "commitment", run.Commitment + ".json")),
            ContentHash(Path.Combine(FinalRuns.FixtureDirectory, "wp4", run.Wp4 + ".json")));

        Assert.Equal(run.Binding, binding.Value);
    }

    [Fact]
    public void Every_wp4_file_reads_the_shadow_network_through_a_relative_directory()
    {
        foreach (var path in Directory.GetFiles(Path.Combine(FinalRuns.FixtureDirectory, "wp4"), "*.json"))
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            var root = document.RootElement;
            Assert.Equal("network", root.GetProperty("shadowAllocation").GetProperty("networkDirectory").GetString());
            Assert.False(root.GetProperty("emitSolverExecutionEvidence").GetBoolean());
            Assert.Equal(
                path.EndsWith("light-v1.json", StringComparison.Ordinal),
                root.TryGetProperty("auditHash", out var audit) && audit.GetString() == "light-v1");
        }
    }

    private static Sha256Hex ContentHash(string path)
    {
        var canonical = CanonicalJson.Canonicalize(File.ReadAllBytes(path));
        Assert.True(Sha256Hex.TryCreate(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(canonical)), out var hash));
        return hash!;
    }

    /// <summary>Epoch (null when the line has none), code, disposition and message of an error line.</summary>
    private static (long? Epoch, string? Code, string? Disposition, string? Message) ReadError(string line)
    {
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;
        Assert.Equal("error", root.GetProperty("messageType").GetString());
        var payload = root.GetProperty("payload");
        return (
            root.TryGetProperty("epochId", out var epoch) ? (long?)epoch.GetInt64() : null,
            payload.GetProperty("code").GetString(),
            payload.GetProperty("disposition").GetString(),
            payload.GetProperty("message").GetString());
    }
}
