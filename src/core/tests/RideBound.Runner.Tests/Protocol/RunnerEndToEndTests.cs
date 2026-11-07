using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RideBound.Runner.Tests.Final;

namespace RideBound.Runner.Tests.Protocol;

/// <summary>
/// The Runner host end to end on the final experiment's configuration (the T1 session FS-G2-SHRC71-CL.light-v1 of
/// <see cref="FinalRuns"/>): in memory through <c>RunnerHost</c>, and as a child process with the final runs'
/// argument vector. Recorded output: Fixtures/expected/FS-G2-SHRC71-CL.light-v1.txt (Runner at commit ebca912).
/// </summary>
public sealed class RunnerEndToEndTests
{
    private const string Shutdown = """{"schemaVersion":"1.0.0","messageType":"shutdown","payload":{}}""";

    private static readonly FinalRun Run = FinalRuns.Get("FS-G2-SHRC71-CL.light-v1");

    /// <summary>
    /// The adapter's side of the session written out in advance: every decisionApplied carries the decision hash
    /// recorded for that epoch, and the first batch is sent twice (a retry before its acknowledgement), so the
    /// Runner repeats its first decision once.
    /// </summary>
    [Fact]
    public async Task Final_transcript_replays_twice_in_memory_with_the_recorded_output_and_hash()
    {
        var transcript = RecordedTranscript();

        var first = await RunInMemory(transcript);
        var second = await RunInMemory(transcript);

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(FinalRuns.Sha256(first.StandardOutput), FinalRuns.Sha256(second.StandardOutput));
        Assert.Equal(string.Empty, first.StandardError);
        var lines = Lines(first.StandardOutput);
        Assert.Equal(FinalRuns.RecordedLines(Run).Count + 1, lines.Count);
        Assert.All(lines, line => Assert.True(IsJsonObject(line)));
        Assert.Equal(
            ["helloAck", "initialized", "checkpoint", "decision", "decision"],
            lines.Take(5).Select(ReadMessageType));
        Assert.Equal(lines[3], lines[4]);
        FinalRuns.AssertRecordedStdout(Run, Bytes(lines.Where((_, index) => index != 4)));
        var decisions = lines.Where(line => ReadMessageType(line) == "decision").ToArray();
        Assert.Equal(
            ReadDecisionHash(RecordedDecisions()[^1]),
            ReadDecisionHash(decisions[^1]));
    }

    [Fact]
    public async Task Final_transcript_replays_twice_through_clean_processes()
    {
        var scenario = FinalRuns.ScenarioLines(Run);

        var first = await RunnerProcess.RunAsync(Run.Arguments, FinalRuns.FixtureDirectory, scenario);
        var second = await RunnerProcess.RunAsync(Run.Arguments, FinalRuns.FixtureDirectory, scenario);

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(0, second.ExitCode);
        Assert.Equal(string.Empty, first.StandardError);
        Assert.Equal(string.Empty, second.StandardError);
        FinalRuns.AssertRecordedStdout(Run, first.StandardOutput);
        Assert.Equal(FinalRuns.Sha256(first.StandardOutput), FinalRuns.Sha256(second.StandardOutput));
    }

    [Fact]
    public async Task Tampered_event_changes_decision_hash_and_fails_golden_comparison()
    {
        var scenario = FinalRuns.ScenarioLines(Run);
        var firstBatch = scenario.First(line => ReadMessageType(line) == "eventBatch");
        var tamperedBatch = JsonNode.Parse(firstBatch)!;
        tamperedBatch["payload"]!["events"]![0]!["payload"]!["snapshot"]!["arcs"]![0]!["travelTimeMs"] = 3001;
        string Transcript(string batch) => string.Join('\n', scenario[0], scenario[1], batch, Shutdown) + '\n';

        var original = await RunInMemory(Transcript(firstBatch));
        var changed = await RunInMemory(Transcript(tamperedBatch.ToJsonString()));
        var originalLines = Lines(original.StandardOutput);
        var changedLines = Lines(changed.StandardOutput);

        Assert.Equal(RecordedDecisions()[0], originalLines[^1]);
        Assert.NotEqual(RecordedDecisions()[0], changedLines[^1]);
        Assert.NotEqual(original.StandardOutput, changed.StandardOutput);
        Assert.NotEqual(
            ReadDecisionHash(originalLines[^1]),
            ReadDecisionHash(changedLines[^1]));
    }

    [Fact]
    public async Task Executable_process_keeps_diagnostics_off_stdout()
    {
        var hello = FinalRuns.ScenarioLines(Run)[0];

        var result = await RunnerProcess.RunAsync(Run.Arguments, FinalRuns.FixtureDirectory, [hello, Shutdown]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        var lines = result.Lines();
        Assert.Single(lines);
        Assert.Equal("helloAck", ReadMessageType(lines[0]));
    }

    [Fact]
    public async Task Host_returns_nonzero_for_incomplete_eof_without_fake_response()
    {
        var input = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        var output = new MemoryStream();
        var diagnostics = new StringWriter();

        var exitCode = await FinalRuns.RunHostAsync(Run, input, output, diagnostics);

        Assert.Equal(2, exitCode);
        Assert.Empty(output.ToArray());
        Assert.Contains("line-feed", diagnostics.ToString());
    }

    [Fact]
    public async Task Malformed_json_is_rejected_and_next_valid_message_still_runs()
    {
        var inputText = string.Join(
            '\n',
            "{not-json}",
            FinalRuns.ScenarioLines(Run)[0],
            Shutdown) + '\n';

        var result = await RunInMemory(inputText);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("MALFORMED_JSON", result.StandardError);
        var lines = Lines(result.StandardOutput);
        Assert.Equal(2, lines.Count);
        Assert.Equal("error", ReadMessageType(lines[0]));
        Assert.Equal("MALFORMED_JSON", ReadErrorCode(lines[0]));
        Assert.Equal("helloAck", ReadMessageType(lines[1]));
    }

    /// <summary>The scenario with a decisionApplied after every eventBatch, built from the recorded decisions the
    /// way the adapter builds it from the Runner's, and with the first batch sent a second time before its
    /// decisionApplied (a retry).</summary>
    private static string RecordedTranscript()
    {
        var decisions = RecordedDecisions();
        var transcript = new StringBuilder();
        var batches = 0;

        foreach (var line in FinalRuns.ScenarioLines(Run))
        {
            transcript.Append(line).Append('\n');

            if (ReadMessageType(line) != "eventBatch")
            {
                continue;
            }

            if (batches == 0)
            {
                transcript.Append(line).Append('\n');
            }

            transcript.Append(DecisionApplied(decisions[batches++])).Append('\n');
        }

        Assert.Equal(decisions.Count, batches);
        return transcript.ToString();
    }

    private static string DecisionApplied(string decision)
    {
        using var document = JsonDocument.Parse(decision);
        var root = document.RootElement;
        return new JsonObject
        {
            ["schemaVersion"] = "1.0.0",
            ["messageType"] = "decisionApplied",
            ["runId"] = root.GetProperty("runId").GetString(),
            ["scenarioId"] = root.GetProperty("scenarioId").GetString(),
            ["epochId"] = root.GetProperty("epochId").GetInt64(),
            ["simTimeMs"] = root.GetProperty("simTimeMs").GetInt64(),
            ["payload"] = new JsonObject
            {
                ["decisionHash"] = root.GetProperty("payload").GetProperty("decisionHash").GetString(),
            },
        }.ToJsonString();
    }

    /// <summary>The decision lines of the T1 golden, which keeps this run's decisions verbatim.</summary>
    private static IReadOnlyList<string> RecordedDecisions() =>
        FinalRuns.RecordedLines(Run)
            .Where(line => line.StartsWith('{') && ReadMessageType(line) == "decision")
            .ToArray();

    private static async Task<RunResult> RunInMemory(string transcript)
    {
        var input = new MemoryStream(Encoding.UTF8.GetBytes(transcript));
        var output = new MemoryStream();
        var diagnostics = new StringWriter();

        var exitCode = await FinalRuns.RunHostAsync(Run, input, output, diagnostics);

        return new RunResult(exitCode, output.ToArray(), diagnostics.ToString());
    }

    private static IReadOnlyList<string> Lines(byte[] standardOutput) =>
        new RunnerProcessResult(0, standardOutput, string.Empty).Lines();

    private static byte[] Bytes(IEnumerable<string> lines) =>
        Encoding.UTF8.GetBytes(string.Concat(lines.Select(line => line + "\n")));

    private static bool IsJsonObject(string line)
    {
        using var document = JsonDocument.Parse(line);
        return document.RootElement.ValueKind == JsonValueKind.Object;
    }

    private static string ReadMessageType(string line)
    {
        using var document = JsonDocument.Parse(line);
        return document.RootElement.GetProperty("messageType").GetString()!;
    }

    private static string ReadDecisionHash(string line)
    {
        using var document = JsonDocument.Parse(line);
        return document.RootElement
            .GetProperty("payload")
            .GetProperty("decisionHash")
            .GetString()!;
    }

    private static string ReadErrorCode(string line)
    {
        using var document = JsonDocument.Parse(line);
        return document.RootElement
            .GetProperty("payload")
            .GetProperty("code")
            .GetString()!;
    }

    private sealed record RunResult(
        int ExitCode,
        byte[] StandardOutput,
        string StandardError);
}
