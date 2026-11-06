using RideBound.Application.State;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Vehicles;

namespace RideBound.Algorithms.Candidates;

/// <summary>
/// One waiting rider that may leave <see cref="Donor"/>: its own pickup and drop stops, and the
/// donor's mutable suffix without them (the root of every donor candidate).
/// </summary>
public sealed record WaitingRiderRelocationSeed(
    RideRequest Request,
    VehicleState Donor,
    RouteStop Pickup,
    RouteStop DropOff,
    IReadOnlyList<RouteStop> DonorSuffix);

public sealed record WaitingRiderRelocationEligibility(
    IReadOnlyList<WaitingRiderRelocationSeed> Considered,
    IReadOnlyList<RequestId> EligibleRequestIds,
    IReadOnlyList<RequestId> OmittedRequestIds);

/// <summary>
/// Builds the G2 neighbourhood (waiting-rider relocation). It only builds routes; validation,
/// work accounting and identities stay in <see cref="InsertionCandidateGenerator"/>.
/// <list type="bullet">
/// <item>Eligible riders: confirmed waiting riders of the donor whose pickup and drop are both in
/// its mutable suffix, by pickup index then ordinal id (the B4 template, waiting only).</item>
/// <item>Receivers: every other vehicle, or the k nearest; each takes the rider's own two stops at
/// every precedence-preserving position of its mutable suffix and no new request.</item>
/// </list>
/// </summary>
public sealed class WaitingRiderRelocationBuilder
{
    public WaitingRiderRelocationEligibility Eligible(
        OnlineState state,
        VehicleState donor,
        int? maximumRiders)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(donor);

        var eligible = state.Run.Requests.Values
            .Where(
                request => request.Lifecycle == RequestLifecycle.WaitingPickup
                    && request.AssignedVehicleId == donor.Id
                    && donor.AcceptedRequestIds.Contains(request.Id)
                    && !donor.OnboardRequestIds.Contains(request.Id)
                    && WaitingIncumbentRepairSeedBuilder.HasExactMutablePair(
                        donor.Route,
                        request.Id))
            .Select(
                request => (
                    Request: request,
                    PickupIndex: IndexOf(donor.Route.MutableSuffix, request.Id, RouteStopKind.Pickup)))
            .OrderBy(value => value.PickupIndex)
            .ThenBy(value => value.Request.Id.Value, StringComparer.Ordinal)
            .Select(value => value.Request)
            .ToArray();
        var considered = maximumRiders is int cap ? eligible.Take(cap).ToArray() : eligible;
        var omitted = eligible.Skip(considered.Length).ToArray();
        var suffix = donor.Route.MutableSuffix;

        return new WaitingRiderRelocationEligibility(
            considered
                .Select(
                    request => new WaitingRiderRelocationSeed(
                        request,
                        donor,
                        suffix[IndexOf(suffix, request.Id, RouteStopKind.Pickup)],
                        suffix[IndexOf(suffix, request.Id, RouteStopKind.DropOff)],
                        Array.AsReadOnly(
                            suffix.Where(stop => stop.RequestId != request.Id).ToArray())))
                .ToArray(),
            eligible.Select(request => request.Id).ToArray(),
            omitted.Select(request => request.Id).ToArray());
    }

    /// <summary>
    /// Receiver vehicles in ordinal order. With a cap below the fleet size, the k nearest by
    /// travel time from the vehicle's schedule start node (its node, or the end of its current
    /// edge, as the schedule projector starts) to the rider's origin; ties by ordinal id; a
    /// vehicle without a travel arc ranks last.
    /// </summary>
    public IReadOnlyList<VehicleState> Targets(
        OnlineState state,
        VehicleState donor,
        RideRequest request,
        int? maximumTargets)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(donor);
        ArgumentNullException.ThrowIfNull(request);

        var others = state.Run.Vehicles.Values
            .Where(vehicle => vehicle.Id != donor.Id)
            .OrderBy(vehicle => vehicle.Id.Value, StringComparer.Ordinal)
            .ToArray();

        if (maximumTargets is not int cap || others.Length <= cap)
        {
            return others;
        }

        return others
            .Select(vehicle => (Vehicle: vehicle, Travel: TravelToOrigin(state, vehicle, request)))
            .OrderBy(value => value.Travel)
            .ThenBy(value => value.Vehicle.Id.Value, StringComparer.Ordinal)
            .Take(cap)
            .Select(value => value.Vehicle)
            .OrderBy(vehicle => vehicle.Id.Value, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>Every (pickup, drop) insertion of the rider's own stops into the receiver's suffix.</summary>
    public static IEnumerable<RoutePlan> ReceiverRoutes(
        VehicleState receiver,
        RouteStop pickup,
        RouteStop dropOff)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        var baseStops = receiver.Route.MutableSuffix;

        for (var pickupIndex = 0; pickupIndex <= baseStops.Count; pickupIndex++)
        {
            for (var dropIndex = pickupIndex + 1; dropIndex <= baseStops.Count + 1; dropIndex++)
            {
                var withPair = new RouteStop[baseStops.Count + 2];
                var source = 0;

                for (var index = 0; index < withPair.Length; index++)
                {
                    withPair[index] = index == pickupIndex
                        ? pickup
                        : index == dropIndex
                            ? dropOff
                            : baseStops[source++];
                }

                var route = receiver.Route.ReplaceMutableSuffix(withPair);

                if (route.IsSuccess)
                {
                    yield return route.Value!;
                }
            }
        }
    }

    private static long TravelToOrigin(OnlineState state, VehicleState vehicle, RideRequest request)
    {
        NodeId? start = vehicle.Position switch
        {
            NodePosition node => node.NodeId,
            EdgeProgressPosition edge => edge.ToNodeId,
            _ => null,
        };

        return start is NodeId from
            && state.TravelTimes!.TryGetTravelTime(from, request.OriginNodeId, out var travel)
                ? travel.Milliseconds
                : long.MaxValue;
    }

    private static int IndexOf(IReadOnlyList<RouteStop> suffix, RequestId requestId, RouteStopKind kind)
    {
        for (var index = 0; index < suffix.Count; index++)
        {
            if (suffix[index].RequestId == requestId && suffix[index].Kind == kind)
            {
                return index;
            }
        }

        throw new InvalidOperationException("An eligible rider must have both stops in the mutable suffix.");
    }
}
