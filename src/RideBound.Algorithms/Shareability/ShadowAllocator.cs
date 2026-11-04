using RideBound.Application.Scheduling;
using RideBound.Application.State;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;

namespace RideBound.Algorithms.Shareability;

/// <summary>Per-cell inputs of Shadow Control (the emulator's makeCellCtx, frozen thresholds).</summary>
/// <param name="Network">Full free-flow network.</param>
/// <param name="Kappa">Scale F(window start) of the cell (ms per free-flow second / 1000).</param>
/// <param name="Factors">Bin factors f_b of the cell profile (held after the last).</param>
/// <param name="PriorOrigin">Preceding-hour Via counts per zone (zone-index order), origins.</param>
/// <param name="PriorDestination">The same for destinations.</param>
/// <param name="Phi">sampled / eligible of the cell.</param>
/// <param name="Sampled">Sampled requests of the cell (prior fallback).</param>
/// <param name="Alpha">Smoothing alpha of the zone rates.</param>
/// <param name="Threshold">theta: a score at or below it makes the rider guaranteed.</param>
/// <param name="NetworkNodes">Run node identifier -> network node index, for every node the run can name. The
/// identifiers are the simulator adapter's opaque ones, so the map is part of the cell's input (ADR-089 GĐ4 gate,
/// 2026-11-04: the identifiers are not the network's own node numbers).</param>
public sealed record ShadowAllocationInputs(
    ShareabilityNetwork Network,
    double Kappa,
    IReadOnlyList<double> Factors,
    double PickupWindowMilliseconds,
    double StrideMilliseconds,
    double IntervalMilliseconds,
    double Alpha,
    double Phi,
    double Sampled,
    IReadOnlyList<double> PriorOrigin,
    IReadOnlyList<double> PriorDestination,
    double Threshold,
    long BinMilliseconds,
    IReadOnlyDictionary<string, int> NetworkNodes);

/// <summary>
/// ADR-089 GĐ4: Shadow Control classes for the riders whose first promise was published in a
/// decision, from the staged state of that decision (the comparison emulator, engine17.js:742-776):
/// the score context is the assigned vehicle's start, current arc, load and capacity and its route
/// with the projected service times T and headrooms h (pickup: 0 once promised, else latest - T;
/// drop: max(0, pickup + max ride - T), capped for a co-rider promised earlier by its remaining
/// allowance max(0, bSH - sum|c|)). Zone rates count every request revealed so far; their causal
/// weights use completed intervals only, as the emulator's full-cell table does.
/// </summary>
public sealed class ShadowAllocator
{
    private readonly ShadowAllocationInputs _inputs;
    private readonly RouteScheduleProjector _projector;
    private readonly int[] _identity;

    public ShadowAllocator(ShadowAllocationInputs inputs, RouteScheduleProjector? projector = null)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        _inputs = inputs;
        _projector = projector ?? new RouteScheduleProjector();
        _identity = Enumerable.Range(0, inputs.Network.NodeCount).ToArray();
    }

    /// <summary>Last scores, for audit and tests (request id -> score).</summary>
    public IReadOnlyDictionary<RequestId, ShareabilityScore> LastScores { get; private set; } =
        new Dictionary<RequestId, ShareabilityScore>();

    public OnlineState Allocate(
        OnlineState state,
        IReadOnlyCollection<RequestId> opened,
        SimTime evaluationTime)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(opened);

        if (opened.Count == 0)
        {
            return state;
        }

        var travelTimes = state.TravelTimes
            ?? throw new InvalidOperationException("Shadow scoring needs a travel snapshot.");
        var rates = Rates(state);
        var scorer = new ShareabilityScorer(
            _inputs.Network,
            new ShareabilityCell(
                _inputs.Kappa,
                _inputs.Factors,
                _identity,
                _inputs.PickupWindowMilliseconds,
                _inputs.StrideMilliseconds,
                rates,
                Oracle: false));
        var ledger = state.Commitments;
        var openedSet = opened.ToHashSet();
        var scores = new Dictionary<RequestId, ShareabilityScore>();

        foreach (var requestId in opened.OrderBy(value => value.Value, StringComparer.Ordinal))
        {
            var request = state.Run.Requests[requestId];
            var vehicle = state.Run.Vehicles[
                request.AssignedVehicleId
                ?? throw new InvalidOperationException("A promised rider has a vehicle.")];
            var route = vehicle.Route.RemainingStops.ToArray();
            var schedule = _projector.Project(state.Run, vehicle, vehicle.Route, travelTimes, evaluationTime);

            if (!schedule.IsSuccess)
            {
                throw new InvalidOperationException(
                    "Shadow scoring could not project the promised route: " + schedule.Failure!.Message);
            }

            var projected = schedule.Schedule!.Stops;
            var pickupService = new Dictionary<RequestId, SimTime>();

            for (var index = 0; index < route.Length; index++)
            {
                if (route[index].Kind == RouteStopKind.Pickup)
                {
                    pickupService[route[index].RequestId!.Value] = projected[index].ServiceStartTime;
                }
            }

            var stops = new ShareabilityStop[route.Length];
            int pickupIndex = -1, dropIndex = -1;

            for (var index = 0; index < route.Length; index++)
            {
                var stop = route[index];
                var riderId = stop.RequestId
                    ?? throw new InvalidOperationException("Shadow scoring does not handle waypoints.");
                var rider = state.Run.Requests[riderId];
                var time = projected[index].ServiceStartTime.Milliseconds;
                var promised = ledger.Histories.ContainsKey(riderId);
                double headroom;

                if (stop.Kind == RouteStopKind.Pickup)
                {
                    headroom = promised ? 0 : Math.Max(0, rider.LatestPickup.Milliseconds - time);
                    pickupIndex = riderId == requestId ? index : pickupIndex;
                }
                else
                {
                    var pickup = rider.Lifecycle == RequestLifecycle.Onboard
                        ? rider.ActualPickupTime
                            ?? throw new InvalidOperationException("An onboard rider has a pickup time.")
                        : pickupService[riderId];
                    double physical = Math.Max(
                        0,
                        pickup.Milliseconds + rider.MaxRideTime.Milliseconds - time);
                    headroom = !promised || openedSet.Contains(riderId)
                        ? physical
                        : Math.Min(physical, Math.Max(0, Allowance(ledger, rider) - Spent(ledger, riderId)));
                    dropIndex = riderId == requestId ? index : dropIndex;
                }

                stops[index] = new ShareabilityStop(
                    NetworkNode(stop.NodeId),
                    stop.Kind == RouteStopKind.Pickup,
                    time,
                    headroom,
                    checked((int)rider.PartySize));
            }

            var (startNode, startTime, arc) = Start(vehicle.Position, travelTimes, evaluationTime);
            var score = scorer.Score(new ShareabilityContext(
                evaluationTime.Milliseconds,
                checked((int)(evaluationTime.Milliseconds / _inputs.BinMilliseconds)),
                startNode,
                startTime,
                arc,
                checked((int)vehicle.OccupiedSeats),
                checked((int)vehicle.Capacity),
                stops,
                pickupIndex,
                dropIndex,
                Dwell: 0));
            scores[requestId] = score;
            ledger = ledger.WithShadowAllocation(requestId, ShadowAllocation.From(score.Score, _inputs.Threshold));
        }

        LastScores = scores;
        return state with { Commitments = ledger };
    }

    private ZoneDemandRates Rates(OnlineState state)
    {
        var requests = state.Run.Requests.Values.ToArray();
        return ZoneDemandRates.Create(
            _inputs.Network,
            requests.Select(value => (double)value.ArrivalTime.Milliseconds).ToArray(),
            requests.Select(value => _inputs.Network.ZoneOf(NetworkNode(value.OriginNodeId))).ToArray(),
            requests.Select(value => _inputs.Network.ZoneOf(NetworkNode(value.DestinationNodeId))).ToArray(),
            _inputs.PriorOrigin,
            _inputs.PriorDestination,
            _inputs.Phi,
            _inputs.Alpha,
            _inputs.Sampled,
            _inputs.IntervalMilliseconds);
    }

    // bSH = round(aSH * slack) of a co-rider promised in an earlier decision.
    private static long Allowance(CommitmentLedger ledger, RideRequest rider)
    {
        var allocation = ledger.ShadowAllocationOf(rider.Id)
            ?? throw new InvalidOperationException("A promised rider has a shadow class.");
        return allocation.Class == ShadowAllocationClass.Guaranteed
            ? 0
            : CommitmentPolicy.SlackProportionalLimit(
                ledger.Histories[rider.Id].Entries[0].PublishedPromise.Projection,
                rider.MaxRideTime,
                1000);
    }

    // sum |c| so far: the decision-induced drop ETA charge accumulated in the ledger.
    private static long Spent(CommitmentLedger ledger, RequestId requestId) =>
        ledger.Histories[requestId].Current.BudgetAfter.DropEtaTotalMs;

    private (int Node, double Time, ShareabilityArc? Arc) Start(
        VehiclePosition position,
        Domain.Validation.ITravelTimeLookup travelTimes,
        SimTime evaluationTime)
    {
        switch (position)
        {
            case NodePosition node:
                return (NetworkNode(node.NodeId), evaluationTime.Milliseconds, null);
            case EdgeProgressPosition edge:
                // RouteScheduleProjector's start: the rest of the edge, rounded up, as a driven leg.
                if (!travelTimes.TryGetTravelTime(edge.FromNodeId, edge.ToNodeId, out var full))
                {
                    throw new InvalidOperationException("The current edge is not in the snapshot.");
                }

                var product = checked(full.Milliseconds * (1000 - edge.ProgressPermille));
                var remaining = product / 1000 + (product % 1000 == 0 ? 0 : 1);
                var time = travelTimes.Arrive(evaluationTime, evaluationTime, new Duration(remaining));
                return (
                    NetworkNode(edge.ToNodeId),
                    time.Milliseconds,
                    new ShareabilityArc(NetworkNode(edge.FromNodeId), checked((int)edge.ProgressPermille)));
            default:
                throw new InvalidOperationException("Vehicle position type is unsupported.");
        }
    }

    internal int NetworkNode(NodeId node) =>
        _inputs.NetworkNodes.TryGetValue(node.Value, out var index)
            ? index
            : throw new InvalidOperationException($"Node '{node.Value}' is not in shadowAllocation.networkNodes.");
}
