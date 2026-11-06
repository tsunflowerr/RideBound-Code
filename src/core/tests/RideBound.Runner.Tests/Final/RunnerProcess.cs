using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace RideBound.Runner.Tests.Final;

/// <summary>
/// Runs the Runner as a child process (<c>dotnet RideBound.Runner.dll</c>, the way the simulator adapter starts
/// it) and talks NDJSON to it: input lines are sent in order, and after every <c>eventBatch</c> the driver waits
/// for the Runner's answer. A decision is acknowledged with <c>decisionApplied</c> carrying its decisionHash, as
/// the adapter does; an error for that epoch gets no acknowledgement. Standard output is kept byte for byte.
/// </summary>
internal static class RunnerProcess
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    public static async Task<RunnerProcessResult> RunAsync(
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyList<string> inputLines)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory,
        };
        startInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "RideBound.Runner.dll"));

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // The final runs set none of the Runner's diagnostic environment switches; keep the child free of them.
        foreach (var name in startInfo.Environment.Keys
                     .Where(key => key.StartsWith("RIDEBOUND_", StringComparison.OrdinalIgnoreCase))
                     .ToArray())
        {
            startInfo.Environment.Remove(name);
        }

        using var timeout = new CancellationTokenSource(Timeout);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The Runner process did not start.");
        var standardOutput = new MemoryStream();
        var lines = Channel.CreateUnbounded<string>();
        var reading = ReadLinesAsync(process.StandardOutput.BaseStream, standardOutput, lines.Writer);
        var standardError = process.StandardError.ReadToEndAsync();

        try
        {
            var input = process.StandardInput.BaseStream;

            try
            {
                foreach (var line in inputLines)
                {
                    await WriteLineAsync(input, line, timeout.Token);

                    if (MessageType(line) == "eventBatch")
                    {
                        var applied = await AwaitDecisionAsync(lines.Reader, Epoch(line), timeout.Token);

                        if (applied is not null)
                        {
                            await WriteLineAsync(input, applied, timeout.Token);
                        }
                    }
                }

                process.StandardInput.Close();
            }
            catch (IOException)
            {
                // The Runner closed its input early; its exit code and output say why.
            }

            await reading.WaitAsync(timeout.Token);
            var errorText = await standardError.WaitAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            return new RunnerProcessResult(process.ExitCode, standardOutput.ToArray(), errorText);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                $"The Runner did not finish within {Timeout.TotalMinutes} minutes "
                + $"(arguments: {string.Join(' ', arguments)}).");
        }
    }

    public static string MessageType(string line)
    {
        using var document = JsonDocument.Parse(line);
        return document.RootElement.GetProperty("messageType").GetString()!;
    }

    private static long Epoch(string line)
    {
        using var document = JsonDocument.Parse(line);
        return document.RootElement.GetProperty("epochId").GetInt64();
    }

    private static async Task<string?> AwaitDecisionAsync(
        ChannelReader<string> lines,
        long epoch,
        CancellationToken cancellation)
    {
        while (await lines.WaitToReadAsync(cancellation))
        {
            while (lines.TryRead(out var line))
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                var type = root.GetProperty("messageType").GetString();

                if (type == "decision")
                {
                    return "{\"schemaVersion\":\"1.0.0\",\"messageType\":\"decisionApplied\","
                        + $"\"runId\":{JsonSerializer.Serialize(root.GetProperty("runId").GetString())},"
                        + $"\"scenarioId\":{JsonSerializer.Serialize(root.GetProperty("scenarioId").GetString())},"
                        + $"\"epochId\":{root.GetProperty("epochId").GetInt64()},"
                        + $"\"simTimeMs\":{root.GetProperty("simTimeMs").GetInt64()},"
                        + "\"payload\":{\"decisionHash\":\""
                        + root.GetProperty("payload").GetProperty("decisionHash").GetString()
                        + "\"}}";
                }

                if (type == "error"
                    && (!root.TryGetProperty("epochId", out var errorEpoch) || errorEpoch.GetInt64() == epoch))
                {
                    return null;
                }
            }
        }

        return null;
    }

    private static async Task WriteLineAsync(Stream input, string line, CancellationToken cancellation)
    {
        await input.WriteAsync(Encoding.UTF8.GetBytes(line + "\n"), cancellation);
        await input.FlushAsync(cancellation);
    }

    private static async Task ReadLinesAsync(Stream output, MemoryStream copy, ChannelWriter<string> lines)
    {
        var buffer = new byte[1 << 16];
        var current = new MemoryStream();
        int count;

        while ((count = await output.ReadAsync(buffer)) > 0)
        {
            copy.Write(buffer, 0, count);

            for (var index = 0; index < count; index++)
            {
                if (buffer[index] == (byte)'\n')
                {
                    lines.TryWrite(Encoding.UTF8.GetString(current.GetBuffer(), 0, (int)current.Length));
                    current.SetLength(0);
                }
                else
                {
                    current.WriteByte(buffer[index]);
                }
            }
        }

        if (current.Length > 0)
        {
            lines.TryWrite(Encoding.UTF8.GetString(current.GetBuffer(), 0, (int)current.Length));
        }

        lines.Complete();
    }
}

internal sealed record RunnerProcessResult(int ExitCode, byte[] StandardOutput, string StandardError)
{
    /// <summary>Standard output split at LF; the final LF ends the last line.</summary>
    public IReadOnlyList<string> Lines()
    {
        var text = Encoding.UTF8.GetString(StandardOutput);

        if (text.Length == 0)
        {
            return [];
        }

        return (text.EndsWith('\n') ? text[..^1] : text).Split('\n');
    }
}
