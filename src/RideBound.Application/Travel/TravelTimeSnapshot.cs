using System.Collections.Frozen;
using RideBound.Domain.Common;
using RideBound.Domain.Validation;

namespace RideBound.Application.Travel;

public readonly record struct TravelArc(NodeId FromNodeId, NodeId ToNodeId);

public sealed class TravelTimeSnapshot : ITravelTimeLookup
{
    private readonly FrozenDictionary<TravelArc, Duration> _travelTimes;

    private TravelTimeSnapshot(
        long version,
        string snapshotHash,
        IEnumerable<KeyValuePair<TravelArc, Duration>> travelTimes)
    {
        Version = version;
        SnapshotHash = snapshotHash;
        _travelTimes = travelTimes.ToFrozenDictionary();
    }

    private TravelTimeSnapshot(TravelTimeSnapshot source, TravelTimeForecast? forecast)
    {
        Version = source.Version;
        SnapshotHash = source.SnapshotHash;
        _travelTimes = source._travelTimes;
        Forecast = forecast;
    }

    public long Version { get; }

    public string SnapshotHash { get; }

    public IReadOnlyDictionary<TravelArc, Duration> TravelTimes => _travelTimes;

    /// <summary>
    /// Planning forecast attached by the Runner configuration (closed-loop forecast); null
    /// holds the snapshot flat. Not part of the snapshot's identity or canonical state.
    /// </summary>
    public TravelTimeForecast? Forecast { get; }

    /// <summary>The same snapshot (shared arcs) with the given planning forecast.</summary>
    public TravelTimeSnapshot WithForecast(TravelTimeForecast? forecast) =>
        new(this, forecast);

    public SimTime Arrive(SimTime evaluationTime, SimTime departure, Duration travelTime) =>
        Forecast is null
            ? departure + travelTime
            : Forecast.Arrive(evaluationTime, departure, travelTime);

    public static DomainResult<TravelTimeSnapshot> Create(
        long version,
        string snapshotHash,
        IEnumerable<KeyValuePair<TravelArc, Duration>> travelTimes)
    {
        ArgumentNullException.ThrowIfNull(travelTimes);

        if (version is < 1 or > DomainLimits.MaxCanonicalInteger)
        {
            return DomainResult<TravelTimeSnapshot>.Fail(
                TravelFailureCodes.InvalidSnapshot,
                "Travel snapshot version must be a positive canonical integer.",
                dimension: "version");
        }

        if (!IsLowerSha256(snapshotHash))
        {
            return DomainResult<TravelTimeSnapshot>.Fail(
                TravelFailureCodes.InvalidSnapshot,
                "Travel snapshot hash must be 64 lowercase hexadecimal characters.",
                dimension: "snapshotHash");
        }

        var arcs = travelTimes.ToArray();

        if (arcs.Length == 0)
        {
            return DomainResult<TravelTimeSnapshot>.Fail(
                TravelFailureCodes.InvalidSnapshot,
                "Travel snapshot must contain at least one directed arc.",
                dimension: "arcs");
        }

        if (arcs.Select(pair => pair.Key).Distinct().Count() != arcs.Length
            || arcs.Any(pair => pair.Key.FromNodeId == pair.Key.ToNodeId))
        {
            return DomainResult<TravelTimeSnapshot>.Fail(
                TravelFailureCodes.InvalidSnapshot,
                "Travel snapshot arcs must be unique with distinct endpoints.",
                dimension: "arcs");
        }

        return DomainResult<TravelTimeSnapshot>.Success(
            new TravelTimeSnapshot(version, snapshotHash, arcs));
    }

    public bool TryGetTravelTime(
        NodeId fromNodeId,
        NodeId toNodeId,
        out Duration travelTime)
    {
        if (fromNodeId == toNodeId)
        {
            travelTime = new Duration(0);
            return true;
        }

        return _travelTimes.TryGetValue(
            new TravelArc(fromNodeId, toNodeId),
            out travelTime);
    }

    private static bool IsLowerSha256(string? value) =>
        value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');
}

public static class TravelFailureCodes
{
    public const string InvalidSnapshot = "INVALID_TRAVEL_SNAPSHOT";
    public const string StaleSnapshot = "STALE_TRAVEL_SNAPSHOT";
}
