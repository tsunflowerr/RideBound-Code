using RideBound.Application.State;
using RideBound.Application.Travel;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Vehicles;

namespace RideBound.Algorithms.Tests.Oracle;

internal sealed record OracleCandidate(
    VehicleId VehicleId,
    string SemanticKey,
    IReadOnlyList<RequestId> NewRequestIds,
    long OperationalCost);

internal sealed record OracleSelection(
    IReadOnlyList<OracleCandidate> Candidates,
    int AcceptedRequestCount,
    long OperationalCost);

internal static class ExactSmallOracle
{
    public static IReadOnlyDictionary<VehicleId, IReadOnlyList<OracleCandidate>>
        Generate(OnlineState state)
    {
        var pending = state.Run.Requests.Values
            .Where(value => value.Lifecycle == RequestLifecycle.Pending)
            .OrderBy(value => value.Id.Value, StringComparer.Ordinal)
            .ToArray();
        var result =
            new Dictionary<VehicleId, IReadOnlyList<OracleCandidate>>();

        foreach (var vehicle in state.Run.Vehicles.Values.OrderBy(
                     value => value.Id.Value,
                     StringComparer.Ordinal))
        {
            var byKey = new Dictionary<string, OracleCandidate>(
                StringComparer.Ordinal);
            var subsetCount = 1 << pending.Length;

            for (var mask = 0; mask < subsetCount; mask++)
            {
                var selected = pending
                    .Where((_, index) => (mask & (1 << index)) != 0)
                    .ToArray();
                var tokens = CreateTokens(vehicle, selected);

                foreach (var permutation in Permutations(tokens))
                {
                    if (!PreservesConstraints(permutation))
                    {
                        continue;
                    }

                    var evaluation = Evaluate(
                        state,
                        vehicle,
                        permutation);

                    if (evaluation is null)
                    {
                        continue;
                    }

                    var key = SemanticKey(permutation);
                    byKey.TryAdd(
                        key,
                        new OracleCandidate(
                            vehicle.Id,
                            key,
                            selected
                                .Select(value => value.Id)
                                .OrderBy(
                                    value => value.Value,
                                    StringComparer.Ordinal)
                                .ToArray(),
                            evaluation.Value));
                }
            }

            result.Add(
                vehicle.Id,
                byKey.Values
                    .OrderBy(value => value.SemanticKey, StringComparer.Ordinal)
                    .ToArray());
        }

        return result;
    }

    public static OracleSelection Select(
        IReadOnlyDictionary<VehicleId, IReadOnlyList<OracleCandidate>> candidates,
        IReadOnlyDictionary<(VehicleId VehicleId, string SemanticKey), string>
            productionCandidateIds)
    {
        var ordered = candidates
            .OrderBy(value => value.Key.Value, StringComparer.Ordinal)
            .ToArray();
        OracleSelection? best = null;
        EnumerateSelections(
            index: 0,
            ordered,
            productionCandidateIds,
            [],
            new HashSet<RequestId>(),
            0,
            0,
            ref best);
        return best
            ?? throw new InvalidOperationException(
                "Oracle found no fleet selection.");
    }

    private static void EnumerateSelections(
        int index,
        IReadOnlyList<
            KeyValuePair<VehicleId, IReadOnlyList<OracleCandidate>>> candidates,
        IReadOnlyDictionary<(VehicleId VehicleId, string SemanticKey), string>
            productionCandidateIds,
        IReadOnlyList<OracleCandidate> selected,
        IReadOnlySet<RequestId> assigned,
        int acceptedCount,
        long cost,
        ref OracleSelection? best)
    {
        if (index == candidates.Count)
        {
            var current = new OracleSelection(
                selected.ToArray(),
                acceptedCount,
                cost);

            if (best is null
                || IsBetter(current, best, productionCandidateIds))
            {
                best = current;
            }

            return;
        }

        foreach (var candidate in candidates[index].Value)
        {
            if (candidate.NewRequestIds.Any(assigned.Contains))
            {
                continue;
            }

            EnumerateSelections(
                index + 1,
                candidates,
                productionCandidateIds,
                selected.Append(candidate).ToArray(),
                assigned.Concat(candidate.NewRequestIds).ToHashSet(),
                acceptedCount + candidate.NewRequestIds.Count,
                checked(cost + candidate.OperationalCost),
                ref best);
        }
    }

    private static bool IsBetter(
        OracleSelection candidate,
        OracleSelection current,
        IReadOnlyDictionary<(VehicleId VehicleId, string SemanticKey), string>
            productionCandidateIds)
    {
        if (candidate.AcceptedRequestCount != current.AcceptedRequestCount)
        {
            return candidate.AcceptedRequestCount > current.AcceptedRequestCount;
        }

        if (candidate.OperationalCost != current.OperationalCost)
        {
            return candidate.OperationalCost < current.OperationalCost;
        }

        for (var index = 0; index < candidate.Candidates.Count; index++)
        {
            var left = productionCandidateIds[
                (
                    candidate.Candidates[index].VehicleId,
                    candidate.Candidates[index].SemanticKey)];
            var right = productionCandidateIds[
                (
                    current.Candidates[index].VehicleId,
                    current.Candidates[index].SemanticKey)];
            var comparison = StringComparer.Ordinal.Compare(left, right);

            if (comparison != 0)
            {
                return comparison < 0;
            }
        }

        return false;
    }

    private static IReadOnlyList<OracleToken> CreateTokens(
        VehicleState vehicle,
        IReadOnlyList<RideRequest> requests)
    {
        var tokens = vehicle.Route.MutableSuffix
            .Select(
                (stop, index) => new OracleToken(
                    $"existing:{index}",
                    stop,
                    ExistingIndex: index,
                    NewRequestId: null))
            .ToList();

        foreach (var request in requests)
        {
            tokens.Add(
                new OracleToken(
                    $"pickup:{request.Id.Value}",
                    new RouteStop(
                        new StopId($"oracle-p-{request.Id.Value}"),
                        request.OriginNodeId,
                        RouteStopKind.Pickup,
                        request.Id,
                        new Duration(0)),
                    ExistingIndex: null,
                    request.Id));
            tokens.Add(
                new OracleToken(
                    $"drop:{request.Id.Value}",
                    new RouteStop(
                        new StopId($"oracle-d-{request.Id.Value}"),
                        request.DestinationNodeId,
                        RouteStopKind.DropOff,
                        request.Id,
                        new Duration(0)),
                    ExistingIndex: null,
                    request.Id));
        }

        return tokens;
    }

    private static bool PreservesConstraints(
        IReadOnlyList<OracleToken> permutation)
    {
        var existingOrder = permutation
            .Where(value => value.ExistingIndex is not null)
            .Select(value => value.ExistingIndex!.Value)
            .ToArray();

        if (!existingOrder.SequenceEqual(existingOrder.Order()))
        {
            return false;
        }

        foreach (var requestId in permutation
                     .Where(value => value.NewRequestId is not null)
                     .Select(value => value.NewRequestId!.Value)
                     .Distinct())
        {
            var pickup = permutation.FindIndex(
                value => value.Stop.Kind == RouteStopKind.Pickup
                    && value.NewRequestId == requestId);
            var drop = permutation.FindIndex(
                value => value.Stop.Kind == RouteStopKind.DropOff
                    && value.NewRequestId == requestId);

            if (pickup < 0 || drop <= pickup)
            {
                return false;
            }
        }

        return true;
    }

    private static long? Evaluate(
        OnlineState state,
        VehicleState vehicle,
        IReadOnlyList<OracleToken> permutation)
    {
        if (state.TravelTimes is null)
        {
            return null;
        }

        var time = state.Run.SimulationTime;
        NodeId currentNode;

        if (vehicle.Position is NodePosition node)
        {
            currentNode = node.NodeId;
        }
        else if (vehicle.Position is EdgeProgressPosition edge)
        {
            if (!state.TravelTimes.TryGetTravelTime(
                    edge.FromNodeId,
                    edge.ToNodeId,
                    out var fullEdge))
            {
                return null;
            }

            var remaining = DivideRoundUp(
                checked(
                    fullEdge.Milliseconds
                    * (1000 - edge.ProgressPermille)),
                1000);
            time = TryAdd(time, new Duration(remaining));
            currentNode = edge.ToNodeId;
        }
        else
        {
            return null;
        }

        var load = vehicle.OccupiedSeats;
        var onboard = vehicle.OnboardRequestIds.ToHashSet();
        var picked = new HashSet<RequestId>();
        var dropped = new HashSet<RequestId>();
        var pickupTimes = new Dictionary<RequestId, SimTime>();

        foreach (var token in permutation)
        {
            var stop = token.Stop;

            if (!state.TravelTimes.TryGetTravelTime(
                    currentNode,
                    stop.NodeId,
                    out var travel))
            {
                return null;
            }

            time = TryAdd(time, travel);

            if (stop.Kind != RouteStopKind.Waypoint)
            {
                if (stop.RequestId is not RequestId requestId
                    || !state.Run.Requests.TryGetValue(requestId, out var request))
                {
                    return null;
                }

                if (request.IsAcceptedActive
                    && request.AssignedVehicleId != vehicle.Id)
                {
                    return null;
                }

                if (stop.Kind == RouteStopKind.Pickup)
                {
                    if (onboard.Contains(requestId)
                        || !picked.Add(requestId)
                        || time.Milliseconds > request.LatestPickup.Milliseconds)
                    {
                        return null;
                    }

                    if (time.Milliseconds < request.EarliestPickup.Milliseconds)
                    {
                        time = request.EarliestPickup;
                    }

                    load = checked(load + request.PartySize);

                    if (load > vehicle.Capacity)
                    {
                        return null;
                    }

                    pickupTimes.Add(requestId, time);
                }
                else
                {
                    if (!dropped.Add(requestId)
                        || !onboard.Contains(requestId)
                            && !picked.Contains(requestId))
                    {
                        return null;
                    }

                    var pickupTime = onboard.Contains(requestId)
                        ? request.ActualPickupTime
                        : pickupTimes.GetValueOrDefault(requestId);

                    if (pickupTime is null
                        || time.Milliseconds - pickupTime.Value.Milliseconds
                            > request.MaxRideTime.Milliseconds)
                    {
                        return null;
                    }

                    load -= request.PartySize;

                    if (load < 0)
                    {
                        return null;
                    }
                }
            }

            time = TryAdd(time, stop.ServiceDuration);
            currentNode = stop.NodeId;
        }

        if (vehicle.OnboardRequestIds.Any(value => !dropped.Contains(value)))
        {
            return null;
        }

        return time.Milliseconds - state.Run.SimulationTime.Milliseconds;
    }

    private static SimTime TryAdd(SimTime time, Duration duration)
    {
        try
        {
            return time + duration;
        }
        catch (Exception error) when (
            error is OverflowException or ArgumentOutOfRangeException)
        {
            throw new InvalidOperationException(
                "Generated exact-small time exceeded its published bound.",
                error);
        }
    }

    private static string SemanticKey(
        IReadOnlyList<OracleToken> permutation) =>
        string.Join(
            "|",
            permutation.Select(
                value => value.NewRequestId is RequestId requestId
                    ? $"{(value.Stop.Kind == RouteStopKind.Pickup ? "P" : "D")}:" +
                        requestId.Value
                    : $"E:{value.Stop.StopId.Value}"));

    private static IEnumerable<IReadOnlyList<OracleToken>> Permutations(
        IReadOnlyList<OracleToken> values)
    {
        var used = new bool[values.Count];
        var current = new OracleToken[values.Count];

        return Enumerate(0);

        IEnumerable<IReadOnlyList<OracleToken>> Enumerate(int depth)
        {
            if (depth == values.Count)
            {
                yield return current.ToArray();
                yield break;
            }

            for (var index = 0; index < values.Count; index++)
            {
                if (used[index])
                {
                    continue;
                }

                used[index] = true;
                current[depth] = values[index];

                foreach (var permutation in Enumerate(depth + 1))
                {
                    yield return permutation;
                }

                used[index] = false;
            }
        }
    }

    private static int FindIndex(
        this IReadOnlyList<OracleToken> values,
        Func<OracleToken, bool> predicate)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (predicate(values[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static long DivideRoundUp(long value, long divisor) =>
        value / divisor + (value % divisor == 0 ? 0 : 1);

    private sealed record OracleToken(
        string TokenId,
        RouteStop Stop,
        int? ExistingIndex,
        RequestId? NewRequestId);
}
