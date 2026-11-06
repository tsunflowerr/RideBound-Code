using RideBound.Domain.Common;
using RideBound.Domain.Routes;
using RideBound.Domain.Vehicles;

namespace RideBound.Algorithms.Candidates;

public sealed record OriginHoldTransformResult(
    RoutePlan Route,
    bool WasApplied,
    long RelocatedWaitMilliseconds,
    string ReasonCode);

/// <summary>
/// Moves waiting already present at the first mutable pickup to an executable
/// waypoint at the vehicle's current node. It never invents delay and does not
/// operate while the vehicle is on an edge or before an unexecuted frozen stop.
/// </summary>
public sealed class OriginHoldCandidateTransformer
{
    public OriginHoldTransformResult Transform(
        VehicleState vehicle,
        RoutePlan route,
        ForwardSlackProfile profile,
        string sourceCandidateId)
    {
        ArgumentNullException.ThrowIfNull(vehicle);
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrEmpty(sourceCandidateId);

        if (vehicle.Position is not NodePosition node)
        {
            return NotApplied(route, "EDGE_PROGRESS");
        }

        if (!vehicle.Route.HasExactFrozenPrefix(route)
            || route.ExecutedStopCount != route.FrozenPrefix.Count)
        {
            return NotApplied(route, "FROZEN_PREFIX_REMAINS");
        }

        var firstStop = route.MutableSuffix.FirstOrDefault();
        var firstTiming = profile.Stops.FirstOrDefault();

        if (firstStop is null
            || firstTiming is null
            || firstStop.StopId != firstTiming.StopId
            || firstStop.Kind != RouteStopKind.Pickup)
        {
            return NotApplied(route, "FIRST_STOP_NOT_PICKUP");
        }

        var wait = firstTiming.WaitingBeforeServiceMilliseconds;

        if (wait <= 0)
        {
            return NotApplied(route, "NO_RELOCATABLE_WAIT");
        }

        if (firstTiming.CertifiedDelayBeforeArrivalMilliseconds is long certified
            && wait > certified)
        {
            return NotApplied(route, "WAIT_EXCEEDS_CERTIFICATE");
        }

        PlanVersion version;
        var sameAsActive = ReferenceEquals(route, vehicle.Route)
            || vehicle.Route.IsSemanticallyEqual(route);

        try
        {
            version = sameAsActive ? route.Version.Next() : route.Version;
        }
        catch (OverflowException)
        {
            return NotApplied(route, "PLAN_VERSION_OVERFLOW");
        }

        var hold = new RouteStop(
            CandidateIdentity.CreateHoldStopId(sourceCandidateId),
            node.NodeId,
            RouteStopKind.Waypoint,
            null,
            new Duration(wait));
        var transformed = RoutePlan.Create(
            version,
            route.ExecutedStopCount,
            route.FrozenPrefix,
            new[] { hold }.Concat(route.MutableSuffix));

        return transformed.IsSuccess
            ? new OriginHoldTransformResult(
                transformed.Value!,
                true,
                wait,
                "WAIT_RELOCATED")
            : NotApplied(route, transformed.Failure!.Code);
    }

    private static OriginHoldTransformResult NotApplied(
        RoutePlan route,
        string reasonCode) =>
        new(route, false, 0, reasonCode);
}
