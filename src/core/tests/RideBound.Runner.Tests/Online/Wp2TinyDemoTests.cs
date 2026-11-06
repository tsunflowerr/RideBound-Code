using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RideBound.Runner.Protocol;

namespace RideBound.Runner.Tests.Online;

public sealed class Wp2TinyDemoTests
{
    [Fact]
    public async Task Published_demo_replays_twice_with_exact_actions_and_hash_chain()
    {
        var transcript = ReadScenario("online-demo.input.ndjson");
        var expected = NormalizeLf(
            ReadScenario("online-demo.expected.ndjson"));
        var expectedFinalHash =
            ReadScenario("online-demo.expected-final-hash.txt").Trim();

        var first = await RunInMemory(transcript);
        var second = await RunInMemory(transcript);

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(string.Empty, first.StandardError);
        Assert.Equal(expected, first.StandardOutput);
        Assert.Equal(first.StandardOutput, second.StandardOutput);

        var decisions = ReadDecisions(first.StandardOutput);
        Assert.Equal(4, decisions.Length);
        Assert.Equal(
            ["requestAccepted", "vehiclePlanUpdated"],
            ReadActionTypes(decisions[0]));
        Assert.Empty(ReadActionTypes(decisions[1]));
        Assert.Equal(["requestRejected"], ReadActionTypes(decisions[2]));
        Assert.Equal(
            "CAPACITY",
            decisions[2].RootElement.GetProperty("payload")
                .GetProperty("actions")[0]
                .GetProperty("payload")
                .GetProperty("reasonCode")
                .GetString());
        Assert.Empty(ReadActionTypes(decisions[3]));

        var previous = new string('0', 64);

        foreach (var decision in decisions)
        {
            var payload = decision.RootElement.GetProperty("payload");
            Assert.Equal(
                previous,
                payload.GetProperty("previousDecisionHash").GetString());
            previous = payload.GetProperty("decisionHash").GetString()!;
            Assert.Equal(
                "notProduced",
                payload.GetProperty("certificate")
                    .GetProperty("status")
                    .GetString());
        }

        Assert.Equal(expectedFinalHash, previous);
    }

    [Fact]
    public async Task Published_demo_replays_twice_through_clean_processes()
    {
        var transcript = ReadScenario("online-demo.input.ndjson");
        var expected = NormalizeLf(
            ReadScenario("online-demo.expected.ndjson"));

        var first = await RunExecutable(transcript);
        var second = await RunExecutable(transcript);

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(0, second.ExitCode);
        Assert.Equal(string.Empty, first.StandardError);
        Assert.Equal(string.Empty, second.StandardError);
        Assert.Equal(expected, first.StandardOutput);
        Assert.Equal(first.StandardOutput, second.StandardOutput);
    }

    [Fact]
    public async Task Tampered_request_changes_the_affected_decision_hash()
    {
        var lines = ReadScenario("online-demo.input.ndjson")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var prefix = string.Join('\n', lines.Take(3)) + '\n';
        var tampered = prefix.Replace(
            "\"partySize\":1",
            "\"partySize\":4",
            StringComparison.Ordinal);

        var original = await RunInMemory(prefix);
        var changed = await RunInMemory(tampered);
        var originalDecision = ReadDecisions(original.StandardOutput).Single();
        var changedDecision = ReadDecisions(changed.StandardOutput).Single();

        Assert.NotEqual(
            ReadHash(originalDecision),
            ReadHash(changedDecision));
        Assert.NotEqual(
            originalDecision.RootElement.GetProperty("payload")
                .GetProperty("stateAfterHash")
                .GetString(),
            changedDecision.RootElement.GetProperty("payload")
                .GetProperty("stateAfterHash")
                .GetString());
    }

    private static async Task<RunResult> RunInMemory(string transcript)
    {
        var input = new MemoryStream(Encoding.UTF8.GetBytes(transcript));
        var output = new MemoryStream();
        var diagnostics = new StringWriter();
        var exitCode = await RunnerHost.RunAsync(
            input,
            output,
            diagnostics,
            executionMode: RunnerExecutionMode.OnlineRollingCost);

        return new RunResult(
            exitCode,
            NormalizeLf(Encoding.UTF8.GetString(output.ToArray())),
            NormalizeLf(diagnostics.ToString()));
    }

    private static async Task<RunResult> RunExecutable(string transcript)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(typeof(RunnerHost).Assembly.Location);
        startInfo.ArgumentList.Add("--mode");
        startInfo.ArgumentList.Add("online");

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

    private static JsonDocument[] ReadDecisions(string output) =>
        output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonDocument.Parse(line))
            .Where(
                document =>
                    document.RootElement.GetProperty("messageType").GetString()
                    == "decision")
            .ToArray();

    private static string[] ReadActionTypes(JsonDocument decision) =>
        decision.RootElement.GetProperty("payload")
            .GetProperty("actions")
            .EnumerateArray()
            .Select(action => action.GetProperty("decisionType").GetString()!)
            .ToArray();

    private static string ReadHash(JsonDocument decision) =>
        decision.RootElement.GetProperty("payload")
            .GetProperty("decisionHash")
            .GetString()!;

    private static string ReadScenario(string fileName)
    {
        var repositoryRoot = typeof(Wp2TinyDemoTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "RideBoundRepositoryRoot")
            .Value!;
        return File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "benchmarks",
                "scenarios",
                "wp2-tiny",
                fileName),
            new UTF8Encoding(false, true));
    }

    private static string NormalizeLf(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);

    private sealed record RunResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
