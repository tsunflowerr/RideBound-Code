using System.Diagnostics;
using System.Text;
using System.Text.Json;
using RideBound.Contracts.Tests.Fixtures;
using RideBound.Runner.Protocol;

namespace RideBound.Runner.Tests.Protocol;

public sealed class RunnerEndToEndTests
{
    [Fact]
    public async Task Published_transcript_replays_twice_with_exact_output_and_hash()
    {
        var transcript = FixtureLoader.ReadUtf8(
            "runner/full-tiny-transcript.input.ndjson");
        var expectedOutput = NormalizeLf(
            FixtureLoader.ReadUtf8(
                "runner/full-tiny-transcript.expected.ndjson"));
        var expectedFinalHash = FixtureLoader.ReadUtf8(
                "runner/full-tiny-transcript.expected-final-hash.txt")
            .Trim();

        var first = await RunInMemory(transcript);
        var second = await RunInMemory(transcript);

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(first.StandardOutput, second.StandardOutput);
        Assert.Equal(expectedOutput, first.StandardOutput);
        Assert.Equal(string.Empty, first.StandardError);
        var lines = first.StandardOutput.Split(
            '\n',
            StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(4, lines.Length);
        Assert.All(lines, line => Assert.True(IsJsonObject(line)));
        Assert.Equal(
            ["helloAck", "initialized", "decision", "decision"],
            lines.Select(ReadMessageType));
        Assert.Equal(lines[2], lines[3]);
        Assert.Equal(expectedFinalHash, ReadDecisionHash(lines[^1]));
    }

    [Fact]
    public async Task Published_transcript_replays_twice_through_clean_processes()
    {
        var transcript = FixtureLoader.ReadUtf8(
            "runner/full-tiny-transcript.input.ndjson");
        var expectedOutput = NormalizeLf(
            FixtureLoader.ReadUtf8(
                "runner/full-tiny-transcript.expected.ndjson"));

        var first = await RunExecutable(transcript);
        var second = await RunExecutable(transcript);

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(0, second.ExitCode);
        Assert.Equal(string.Empty, first.StandardError);
        Assert.Equal(string.Empty, second.StandardError);
        Assert.Equal(expectedOutput, first.StandardOutput);
        Assert.Equal(first.StandardOutput, second.StandardOutput);
    }

    [Fact]
    public async Task Tampered_event_changes_decision_hash_and_fails_golden_comparison()
    {
        var transcript = FixtureLoader.ReadUtf8(
            "runner/full-tiny-transcript.input.ndjson");
        var tampered = transcript.Replace(
            "\"eventType\":\"timerTick\",\"payload\":{}",
            "\"eventType\":\"incidentResolved\",\"payload\":{\"incidentId\":\"tampered\"}",
            StringComparison.Ordinal);

        var original = await RunInMemory(transcript);
        var changed = await RunInMemory(tampered);
        var originalLines = original.StandardOutput.Split(
            '\n',
            StringSplitOptions.RemoveEmptyEntries);
        var changedLines = changed.StandardOutput.Split(
            '\n',
            StringSplitOptions.RemoveEmptyEntries);

        Assert.NotEqual(original.StandardOutput, changed.StandardOutput);
        Assert.NotEqual(
            ReadDecisionHash(originalLines[^1]),
            ReadDecisionHash(changedLines[^1]));
    }

    [Fact]
    public async Task Executable_process_keeps_diagnostics_off_stdout()
    {
        var assembly = typeof(RunnerHost).Assembly.Location;
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(assembly);
        startInfo.ArgumentList.Add("--mode");
        startInfo.ArgumentList.Add("conformance");
        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        await process.StandardInput.WriteAsync(
            CompactFixture("hello/valid-hello.json"));
        await process.StandardInput.WriteLineAsync();
        await process.StandardInput.WriteLineAsync(
            """{"schemaVersion":"1.0.0","messageType":"shutdown","payload":{}}""");
        process.StandardInput.Close();
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.Equal(0, process.ExitCode);
        Assert.Equal(string.Empty, stderr);
        var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        Assert.Equal("helloAck", ReadMessageType(lines[0]));
    }

    [Fact]
    public async Task Host_returns_nonzero_for_incomplete_eof_without_fake_response()
    {
        var input = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        var output = new MemoryStream();
        var diagnostics = new StringWriter();

        var exitCode = await RunnerHost.RunAsync(input, output, diagnostics);

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
            CompactFixture("hello/valid-hello.json"),
            """{"schemaVersion":"1.0.0","messageType":"shutdown","payload":{}}""") + '\n';

        var result = await RunInMemory(inputText);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("MALFORMED_JSON", result.StandardError);
        var lines = result.StandardOutput.Split(
            '\n',
            StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Equal("error", ReadMessageType(lines[0]));
        Assert.Equal("MALFORMED_JSON", ReadErrorCode(lines[0]));
        Assert.Equal("helloAck", ReadMessageType(lines[1]));
    }

    private static async Task<RunResult> RunInMemory(string transcript)
    {
        var input = new MemoryStream(Encoding.UTF8.GetBytes(transcript));
        var output = new MemoryStream();
        var diagnostics = new StringWriter();

        var exitCode = await RunnerHost.RunAsync(input, output, diagnostics);

        return new RunResult(
            exitCode,
            Encoding.UTF8.GetString(output.ToArray()),
            diagnostics.ToString());
    }

    private static async Task<RunResult> RunExecutable(string transcript)
    {
        var assembly = typeof(RunnerHost).Assembly.Location;
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(assembly);
        startInfo.ArgumentList.Add("--mode");
        startInfo.ArgumentList.Add("conformance");

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        await process.StandardInput.WriteAsync(transcript);
        process.StandardInput.Close();
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new RunResult(
            process.ExitCode,
            NormalizeLf(stdout),
            NormalizeLf(stderr));
    }

    private static string CompactFixture(string path) =>
        Compact(FixtureLoader.ReadUtf8(path));

    private static string Compact(string json) =>
        Encoding.UTF8.GetString(
            RideBound.Contracts.Serialization.CanonicalJson.Canonicalize(
                Encoding.UTF8.GetBytes(json)));

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

    private static string NormalizeLf(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);

    private sealed record RunResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
