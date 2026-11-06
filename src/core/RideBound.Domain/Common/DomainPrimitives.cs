using System.Text;

namespace RideBound.Domain.Common;

public static class DomainLimits
{
    public const long MaxCanonicalInteger = 9_007_199_254_740_991;
}

public sealed record DomainFailure(
    string Code,
    string Message,
    string? EntityId = null,
    string? Dimension = null);

public sealed record DomainResult<T>
    where T : class
{
    private DomainResult(T? value, DomainFailure? failure)
    {
        Value = value;
        Failure = failure;
    }

    public bool IsSuccess => Value is not null;

    public T? Value { get; }

    public DomainFailure? Failure { get; }

    public static DomainResult<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new DomainResult<T>(value, null);
    }

    public static DomainResult<T> Fail(
        string code,
        string message,
        string? entityId = null,
        string? dimension = null) =>
        new(null, new DomainFailure(code, message, entityId, dimension));
}

public readonly record struct RunIdentifier
{
    public RunIdentifier(string value)
    {
        Value = DomainIdentifier.Require(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct ScenarioIdentifier
{
    public ScenarioIdentifier(string value)
    {
        Value = DomainIdentifier.Require(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct RequestId
{
    public RequestId(string value)
    {
        Value = DomainIdentifier.Require(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct VehicleId
{
    public VehicleId(string value)
    {
        Value = DomainIdentifier.Require(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct NodeId
{
    public NodeId(string value)
    {
        Value = DomainIdentifier.Require(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct StopId
{
    public StopId(string value)
    {
        Value = DomainIdentifier.Require(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct SimTime
{
    public SimTime(long milliseconds)
    {
        if (milliseconds is < 0 or > DomainLimits.MaxCanonicalInteger)
        {
            throw new ArgumentOutOfRangeException(nameof(milliseconds));
        }

        Milliseconds = milliseconds;
    }

    public long Milliseconds { get; }

    public static SimTime operator +(SimTime time, Duration duration)
    {
        var value = checked(time.Milliseconds + duration.Milliseconds);
        return new SimTime(value);
    }
}

public readonly record struct Duration
{
    public Duration(long milliseconds)
    {
        if (milliseconds is < 0 or > DomainLimits.MaxCanonicalInteger)
        {
            throw new ArgumentOutOfRangeException(nameof(milliseconds));
        }

        Milliseconds = milliseconds;
    }

    public long Milliseconds { get; }
}

public readonly record struct PlanVersion
{
    public PlanVersion(long value)
    {
        if (value is < 0 or > DomainLimits.MaxCanonicalInteger)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        Value = value;
    }

    public long Value { get; }

    public PlanVersion Next()
    {
        if (Value == DomainLimits.MaxCanonicalInteger)
        {
            throw new OverflowException("Plan version cannot advance.");
        }

        return new PlanVersion(Value + 1);
    }
}

public abstract record VehiclePosition;

public sealed record NodePosition : VehiclePosition
{
    public NodePosition(NodeId nodeId)
    {
        NodeId = nodeId;
    }

    public NodeId NodeId { get; }
}

public sealed record EdgeProgressPosition : VehiclePosition
{
    public EdgeProgressPosition(
        NodeId fromNodeId,
        NodeId toNodeId,
        string edgeId,
        long progressPermille)
    {
        if (fromNodeId == toNodeId)
        {
            throw new ArgumentException("Directed edge endpoints must be distinct.");
        }

        if (progressPermille is < 1 or > 999)
        {
            throw new ArgumentOutOfRangeException(nameof(progressPermille));
        }

        FromNodeId = fromNodeId;
        ToNodeId = toNodeId;
        EdgeId = DomainIdentifier.Require(edgeId, nameof(edgeId));
        ProgressPermille = progressPermille;
    }

    public NodeId FromNodeId { get; }

    public NodeId ToNodeId { get; }

    public string EdgeId { get; }

    public long ProgressPermille { get; }
}

internal static class DomainIdentifier
{
    public static string Require(string? value, string parameterName)
    {
        if (value is null
            || value.Length == 0
            || Encoding.UTF8.GetByteCount(value) > 128
            || ContainsInvalidSurrogate(value))
        {
            throw new ArgumentException(
                "Identifier must contain 1 to 128 valid UTF-8 bytes.",
                parameterName);
        }

        return value;
    }

    private static bool ContainsInvalidSurrogate(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (char.IsHighSurrogate(value[index]))
            {
                if (index + 1 >= value.Length
                    || !char.IsLowSurrogate(value[index + 1]))
                {
                    return true;
                }

                index++;
            }
            else if (char.IsLowSurrogate(value[index]))
            {
                return true;
            }
        }

        return false;
    }
}
