using System.Diagnostics;
using System.Globalization;

namespace RideBound.Runner.Protocol;

/// <summary>
/// Measurement only (wf9 hashing study): when RIDEBOUND_HASH_TIMING names a file, one CSV
/// line per eventBatch records how long each phase took. Off by default; it never changes
/// any protocol byte.
/// </summary>
internal struct HashTiming
{
    private static readonly StreamWriter? Output = Open();
    private long _last;
    private readonly long _start;
    private readonly double[]? _phases;

    private HashTiming(long now)
    {
        _start = now;
        _last = now;
        _phases = new double[5];
    }

    public enum Phase
    {
        Batch = 0,
        Decide = 1,
        StateWrite = 2,
        StateHash = 3,
        DecisionHash = 4,
    }

    public static HashTiming Start() =>
        Output is null ? default : new HashTiming(Stopwatch.GetTimestamp());

    public void Lap(Phase phase)
    {
        if (_phases is null)
        {
            return;
        }

        var now = Stopwatch.GetTimestamp();
        _phases[(int)phase] += Stopwatch.GetElapsedTime(_last, now).TotalMilliseconds;
        _last = now;
    }

    public readonly void End(long epoch, long batchBytes, long stagedBytes, long committedBytes)
    {
        if (_phases is null || Output is null)
        {
            return;
        }

        var total = Stopwatch.GetElapsedTime(_start).TotalMilliseconds;
        var line = string.Create(
            CultureInfo.InvariantCulture,
            $"{epoch},{batchBytes},{stagedBytes},{committedBytes},{_phases[0]:F3},{_phases[1]:F3},{_phases[2]:F3},{_phases[3]:F3},{_phases[4]:F3},{total:F3},{GC.GetTotalMemory(false)}");

        lock (Output)
        {
            Output.WriteLine(line);
        }
    }

    private static StreamWriter? Open()
    {
        var path = Environment.GetEnvironmentVariable("RIDEBOUND_HASH_TIMING");

        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var writer = new StreamWriter(path, append: false) { AutoFlush = true };
        writer.WriteLine(
            "epoch,batchBytes,stagedStateBytes,committedStateBytes,batchMs,decideMs,stateWriteMs,stateHashMs,decisionHashMs,totalMs,gcBytes");
        return writer;
    }
}
