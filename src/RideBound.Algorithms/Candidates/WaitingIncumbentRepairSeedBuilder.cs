using RideBound.Application.State;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Vehicles;

namespace RideBound.Algorithms.Candidates;

public sealed record WaitingIncumbentRepairSeed(
    RequestId RequestId,
    RoutePlan Route);

public sealed record WaitingIncumbentRepairSeedResult(
    IReadOnlyList<WaitingIncumbentRepairSeed> Seeds,
    IReadOnlyList<RequestId> EligibleRequestIds,
    IReadOnlyList<RequestId> ConsideredRequestIds,
    IReadOnlyList<RequestId> OmittedRequestIds);

/// <summary>
/// Builds a single-pair same-vehicle repair neighborhood. Each seed removes one
/// eligible waiting incumbent's complete mutable pickup/drop pair and reinserts
/// that exact pair at precedence-preserving positions. It never combines repair
/// moves, changes assignment, touches an onboard rider, or edits frozen stops.
/// </summary>
public sealed class WaitingIncumbentRepairSeedBuilder
{
    public WaitingIncumbentRepairSeedResult Build(
        OnlineState state,
        VehicleState vehicle,
        int maximumRequestsConsidered)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(vehicle);

        if (maximumRequestsConsidered < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRequestsConsidered));
        }

        var eligible = state.Run.Requests.Values
            .Where(
                request => request.Lifecycle is RequestLifecycle.Accepted
                        or RequestLifecycle.WaitingPickup
                    && request.AssignedVehicleId == vehicle.Id
                    && vehicle.AcceptedRequestIds.Contains(request.Id)
                    && !vehicle.OnboardRequestIds.Contains(request.Id)
                    && HasExactMutablePair(vehicle.Route, request.Id))
            .Select(
                request => new
                {
                    Request = request,
                    PickupIndex = vehicle.Route.MutableSuffix
                        .Select((stop, index) => (stop, index))
                        .Single(
                            pair => pair.stop.RequestId == request.Id
                                && pair.stop.Kind == RouteStopKind.Pickup)
                        .index,
                })
            .OrderBy(value => value.PickupIndex)
            .ThenBy(value => value.Request.Id.Value, StringComparer.Ordinal)
            .Select(value => value.Request)
            .ToArray();
        var considered = eligible.Take(maximumRequestsConsidered).ToArray();
        var omitted = eligible.Skip(maximumRequestsConsidered).ToArray();
        var seeds = new Dictionary<string, WaitingIncumbentRepairSeed>(
            StringComparer.Ordinal);

        foreach (var request in considered)
        {
            var pickup = vehicle.Route.MutableSuffix.Single(
                stop => stop.RequestId == request.Id
                    && stop.Kind == RouteStopKind.Pickup);
            var dropOff = vehicle.Route.MutableSuffix.Single(
                stop => stop.RequestId == request.Id
                    && stop.Kind == RouteStopKind.DropOff);
            var withoutPair = vehicle.Route.MutableSuffix
                .Where(stop => stop.RequestId != request.Id)
                .ToArray();

            for (var pickupIndex = 0;
                 pickupIndex <= withoutPair.Length;
                 pickupIndex++)
            {
                var withPickup = withoutPair.ToList();
                withPickup.Insert(pickupIndex, pickup);

                for (var dropIndex = pickupIndex + 1;
                     dropIndex <= withPickup.Count;
                     dropIndex++)
                {
                    var repairedSuffix = withPickup.ToList();
                    repairedSuffix.Insert(dropIndex, dropOff);

                    if (repairedSuffix.SequenceEqual(vehicle.Route.MutableSuffix))
                    {
                        continue;
                    }

                    var repaired = vehicle.Route.ReplaceMutableSuffix(
                        repairedSuffix);

                    if (!repaired.IsSuccess)
                    {
                        continue;
                    }

                    var fingerprint = CandidateIdentity.CreateRouteFingerprint(
                        repaired.Value!);
                    seeds.TryAdd(
                        fingerprint,
                        new WaitingIncumbentRepairSeed(
                            request.Id,
                            repaired.Value!));
                }
            }
        }

        return new WaitingIncumbentRepairSeedResult(
            seeds.Values
                .OrderBy(seed => seed.RequestId.Value, StringComparer.Ordinal)
                .ThenBy(
                    seed => CandidateIdentity.CreateRouteFingerprint(seed.Route),
                    StringComparer.Ordinal)
                .ToArray(),
            eligible.Select(request => request.Id).ToArray(),
            considered.Select(request => request.Id).ToArray(),
            omitted.Select(request => request.Id).ToArray());
    }

    internal static bool HasExactMutablePair(
        RoutePlan route,
        RequestId requestId)
    {
        if (route.FrozenPrefix
            .Skip(route.ExecutedStopCount)
            .Any(stop => stop.RequestId == requestId))
        {
            return false;
        }

        var pickupCount = route.MutableSuffix.Count(
            stop => stop.RequestId == requestId
                && stop.Kind == RouteStopKind.Pickup);
        var dropCount = route.MutableSuffix.Count(
            stop => stop.RequestId == requestId
                && stop.Kind == RouteStopKind.DropOff);
        return pickupCount == 1 && dropCount == 1;
    }
}
