using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;

namespace RideBound.Runner.Tests.Final;

/// <summary>
/// Safety net for the refactor (TARGET-DESIGN phase S, step S3). The final experiment's Runner configuration,
/// driven through the real CLI process exactly like the simulator adapter drives it:
/// <list type="bullet">
/// <item>T1: every stdout byte equals the recording made with the Runner at commit ebca912;</item>
/// <item>T3: a restore from any checkpoint of that session reproduces the rest of it byte for byte.</item>
/// </list>
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

    [Theory]
    [MemberData(nameof(Runs))]
    public async Task Restore_from_every_checkpoint_reproduces_the_rest_of_the_session(string name)
    {
        var run = FinalRuns.Get(name);
        var session = await FinalRuns.SessionAsync(run);
        Assert.Equal(0, session.ExitCode);
        var output = session.Lines();
        var scenario = FinalRuns.ScenarioLines(run);
        var checkpoints = IndexesOf(output, "checkpoint");
        var checkpointRequests = IndexesOf(scenario, "checkpoint");
        Assert.Equal(17, checkpoints.Count);
        Assert.Equal(checkpointRequests.Count, checkpoints.Count);
        string runId, scenarioId;

        using (var initialize = JsonDocument.Parse(scenario[1]))
        {
            runId = initialize.RootElement.GetProperty("runId").GetString()!;
            scenarioId = initialize.RootElement.GetProperty("scenarioId").GetString()!;
        }

        var failures = new ConcurrentBag<string>();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, checkpoints.Count),
            new ParallelOptions { MaxDegreeOfParallelism = 4 },
            async (k, _) =>
            {
                string payload, checkpointHash;

                using (var checkpoint = JsonDocument.Parse(output[checkpoints[k]]))
                {
                    var element = checkpoint.RootElement.GetProperty("payload");
                    payload = element.GetRawText();
                    checkpointHash = element.GetProperty("checkpointHash").GetString()!;
                }

                var restore = "{\"schemaVersion\":\"1.0.0\",\"messageType\":\"restore\","
                    + $"\"runId\":{JsonSerializer.Serialize(runId)},\"scenarioId\":{JsonSerializer.Serialize(scenarioId)},"
                    + $"\"payload\":{payload}}}";
                var input = new List<string> { scenario[0], scenario[1], restore };
                input.AddRange(scenario.Skip(checkpointRequests[k] + 1));

                var restored = await RunnerProcess.RunAsync(run.Arguments, FinalRuns.FixtureDirectory, input);
                var lines = restored.Lines();
                var expectedSuffix = output.Skip(checkpoints[k] + 1).ToArray();

                if (restored.ExitCode != 0 || restored.StandardError.Length != 0)
                {
                    failures.Add($"restore@{k}: exit {restored.ExitCode}, stderr {restored.StandardError}");
                }
                else if (lines.Count != 3 + expectedSuffix.Length
                         || lines[0] != output[0]
                         || lines[1] != output[1])
                {
                    failures.Add($"restore@{k}: {lines.Count} lines, expected {3 + expectedSuffix.Length} with the session's helloAck and initialized");
                }
                else if (!IsRestoreAcknowledgement(lines[2], checkpointHash))
                {
                    failures.Add($"restore@{k}: unexpected acknowledgement {lines[2]}");
                }
                else
                {
                    for (var index = 0; index < expectedSuffix.Length; index++)
                    {
                        if (!string.Equals(lines[3 + index], expectedSuffix[index], StringComparison.Ordinal))
                        {
                            failures.Add(
                                $"restore@{k}: line {3 + index} differs from session line {checkpoints[k] + 1 + index} "
                                + $"({FinalRuns.Digest(lines[3 + index])} vs {FinalRuns.Digest(expectedSuffix[index])})");
                            break;
                        }
                    }
                }
            });

        Assert.Empty(failures.OrderBy(value => value, StringComparer.Ordinal));
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

    private static List<int> IndexesOf(IReadOnlyList<string> lines, string messageType) =>
        Enumerable.Range(0, lines.Count)
            .Where(index => RunnerProcess.MessageType(lines[index]) == messageType)
            .ToList();

    private static bool IsRestoreAcknowledgement(string line, string checkpointHash)
    {
        using var document = JsonDocument.Parse(Encoding.UTF8.GetBytes(line));
        var root = document.RootElement;
        var payload = root.GetProperty("payload");
        return root.GetProperty("messageType").GetString() == "restore"
            && payload.GetProperty("status").GetString() == "restored"
            && payload.GetProperty("checkpointHash").GetString() == checkpointHash;
    }
}
