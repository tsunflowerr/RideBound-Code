using RideBound.Domain.Common;

namespace RideBound.Domain.Validation;

public interface ITravelTimeLookup
{
    bool TryGetTravelTime(
        NodeId fromNodeId,
        NodeId toNodeId,
        out Duration travelTime);

    /// <summary>
    /// Arrival at the end of a leg that takes <paramref name="travelTime"/> under this lookup,
    /// started at <paramref name="departure"/>, for a plan evaluated at
    /// <paramref name="evaluationTime"/>. The default holds the snapshot flat:
    /// departure + travelTime. A planning forecast (<see cref="TravelTimeForecast"/>)
    /// integrates the leg over its time-of-day profile instead.
    /// </summary>
    SimTime Arrive(
        SimTime evaluationTime,
        SimTime departure,
        Duration travelTime) =>
        departure + travelTime;
}
