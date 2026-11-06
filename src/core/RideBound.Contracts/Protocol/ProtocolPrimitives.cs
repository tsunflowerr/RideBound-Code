using System.Globalization;
using System.Text;

namespace RideBound.Contracts.Protocol;

public static class ProtocolLimits
{
    public const long MaxCanonicalInteger = 9_007_199_254_740_991;

    public const long MinCanonicalInteger = -MaxCanonicalInteger;
}

public sealed record ProtocolVersion
{
    private ProtocolVersion(int major, int minor, int patch)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
    }

    public static ProtocolVersion Current { get; } = new(1, 0, 0);

    public int Major { get; }

    public int Minor { get; }

    public int Patch { get; }

    public static bool TryParse(string? value, out ProtocolVersion? version)
    {
        version = null;

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var parts = value.Split('.');

        if (parts.Length != 3
            || !TryParseComponent(parts[0], out var major)
            || !TryParseComponent(parts[1], out var minor)
            || !TryParseComponent(parts[2], out var patch))
        {
            return false;
        }

        version = new ProtocolVersion(major, minor, patch);
        return true;
    }

    public override string ToString()
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Major}.{Minor}.{Patch}");
    }

    private static bool TryParseComponent(string value, out int component)
    {
        component = default;

        if (value.Length == 0 || (value.Length > 1 && value[0] == '0'))
        {
            return false;
        }

        foreach (var character in value)
        {
            if (character is < '0' or > '9')
            {
                return false;
            }
        }

        return int.TryParse(
            value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out component);
    }
}

public sealed record ProtocolMessageType
{
    private static readonly IReadOnlyDictionary<string, ProtocolMessageType> KnownTypes =
        CreateKnownTypes();

    private ProtocolMessageType(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static bool TryParse(string? value, out ProtocolMessageType? messageType)
    {
        return KnownTypes.TryGetValue(value ?? string.Empty, out messageType);
    }

    public override string ToString() => Value;

    private static IReadOnlyDictionary<string, ProtocolMessageType> CreateKnownTypes()
    {
        var values = new[]
        {
            "hello",
            "helloAck",
            "initializeRun",
            "initialized",
            "checkpoint",
            "restore",
            "finalizeRun",
            "runSummary",
            "eventBatch",
            "decision",
            "decisionApplied",
            "shutdown",
            "error",
        };

        return values.ToDictionary(
            value => value,
            value => new ProtocolMessageType(value),
            StringComparer.Ordinal);
    }
}

public sealed record RunId
{
    private RunId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static bool TryCreate(string? value, out RunId? runId)
    {
        if (!OpaqueIdentifier.IsValid(value))
        {
            runId = null;
            return false;
        }

        runId = new RunId(value!);
        return true;
    }

    public override string ToString() => Value;
}

public sealed record ScenarioId
{
    private ScenarioId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static bool TryCreate(string? value, out ScenarioId? scenarioId)
    {
        if (!OpaqueIdentifier.IsValid(value))
        {
            scenarioId = null;
            return false;
        }

        scenarioId = new ScenarioId(value!);
        return true;
    }

    public override string ToString() => Value;
}

public readonly record struct EpochId
{
    private EpochId(long value)
    {
        Value = value;
    }

    public long Value { get; }

    public static bool TryCreate(long value, out EpochId epochId)
    {
        if (value is < 0 or > ProtocolLimits.MaxCanonicalInteger)
        {
            epochId = default;
            return false;
        }

        epochId = new EpochId(value);
        return true;
    }
}

public readonly record struct EventSequence
{
    private EventSequence(long value)
    {
        Value = value;
    }

    public long Value { get; }

    public static bool TryCreate(long value, out EventSequence eventSequence)
    {
        if (value is < 1 or > ProtocolLimits.MaxCanonicalInteger)
        {
            eventSequence = default;
            return false;
        }

        eventSequence = new EventSequence(value);
        return true;
    }
}

public readonly record struct SimulationTimeMilliseconds
{
    private SimulationTimeMilliseconds(long value)
    {
        Value = value;
    }

    public long Value { get; }

    public static bool TryCreate(long value, out SimulationTimeMilliseconds simulationTime)
    {
        if (value is < 0 or > ProtocolLimits.MaxCanonicalInteger)
        {
            simulationTime = default;
            return false;
        }

        simulationTime = new SimulationTimeMilliseconds(value);
        return true;
    }
}

public static class OpaqueIdentifier
{
    public static bool IsValid(string? value)
    {
        if (value is null
            || value.Length == 0
            || Encoding.UTF8.GetByteCount(value) > 128)
        {
            return false;
        }

        for (var index = 0; index < value.Length; index++)
        {
            if (char.IsHighSurrogate(value[index]))
            {
                if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                {
                    return false;
                }

                index++;
            }
            else if (char.IsLowSurrogate(value[index]))
            {
                return false;
            }
        }

        return true;
    }
}
