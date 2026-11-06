using System.Text.Json;
using RideBound.Algorithms.Policies;
using RideBound.Application.Commitments;
using RideBound.Application.State;
using RideBound.Contracts.Protocol;
using RideBound.Domain.Commitments;
using RideBound.Domain.Routes;
using ContractRouteStopKind = RideBound.Contracts.Protocol.RouteStopKind;
using DomainRouteStopKind = RideBound.Domain.Routes.RouteStopKind;

namespace RideBound.Runner.Online;

public static class OnlineDecisionActionMapper
{
    public static IReadOnlyList<JsonElement> Map(
        RollingCostDecision decision,
        OnlineState stateBeforeEvents,
        bool includeEventInducedPlanUpdates)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(stateBeforeEvents);
        var actions = new List<JsonElement>();

        foreach (var action in decision.RequestActions.OrderBy(
                     value => value.RequestId.Value,
                     StringComparer.Ordinal))
        {
            actions.Add(MapRequestAction(action));
        }

        foreach (var plan in decision.VehiclePlans
                     .Where(
                         value => !value.Candidate.IsNoOp
                             || includeEventInducedPlanUpdates
                             && (!stateBeforeEvents.Run.Vehicles.TryGetValue(
                                     value.VehicleId,
                                     out var priorVehicle)
                                 || !RoutesEqual(
                                     priorVehicle.Route,
                                     value.Candidate.Route)))
                     .OrderBy(
                         value => value.VehicleId.Value,
                         StringComparer.Ordinal))
        {
            actions.Add(
                OnlineDecisionActionCodec.Encode(
                    new OnlineDecisionAction(
                        DecisionType.VehiclePlanUpdated,
                        new VehiclePlanUpdatedActionPayload(
                            plan.VehicleId.Value,
                            plan.Candidate.CandidateId,
                            MapRoute(plan.Candidate.Route)))));
        }

        return actions.AsReadOnly();
    }

    private static bool RoutesEqual(RoutePlan left, RoutePlan right) =>
        left.Version == right.Version
        && left.ExecutedStopCount == right.ExecutedStopCount
        && left.FrozenPrefix.SequenceEqual(right.FrozenPrefix)
        && left.MutableSuffix.SequenceEqual(right.MutableSuffix);

    public static IReadOnlyList<JsonElement> MapPublications(
        IEnumerable<PromisePublication> publications)
    {
        ArgumentNullException.ThrowIfNull(publications);
        return publications
            .OrderBy(value => value.PublicationId, StringComparer.Ordinal)
            .Select(
                publication => OnlineDecisionActionCodec.Encode(
                    new OnlineDecisionAction(
                        DecisionType.PromisePublished,
                        MapPublication(publication))))
            .ToArray();
    }

    private static PromisePublishedActionPayload MapPublication(
        PromisePublication publication)
    {
        var entry = publication.Entry;
        return new PromisePublishedActionPayload(
            publication.PublicationId,
            entry.PublishedPromise.Version.Value,
            entry.ReasonCode,
            entry.SourceEventSequence,
            MapPromise(entry.PublishedPromise.Projection),
            MapVector(entry.Deltas.Exogenous),
            MapVector(entry.Deltas.DecisionInduced),
            MapVector(entry.Deltas.Visible),
            MapVector(entry.BudgetBefore),
            MapVector(entry.BudgetAfter));
    }

    private static PromiseProjectionContract MapPromise(
        PromiseProjection promise) =>
        new(
            promise.RequestId.Value,
            promise.VehicleId.Value,
            promise.PickupStopId.Value,
            promise.PickupNodeId.Value,
            promise.DropStopId.Value,
            promise.DropNodeId.Value,
            promise.PickupEta.Milliseconds,
            promise.DropEta.Milliseconds,
            promise.ServiceOrder.Select(
                token => new PromiseServiceTokenContract(
                    token.StopId.Value,
                    token.RequestId?.Value,
                    token.Kind switch
                    {
                        DomainRouteStopKind.Waypoint =>
                            ContractRouteStopKind.Waypoint,
                        DomainRouteStopKind.Pickup =>
                            ContractRouteStopKind.Pickup,
                        DomainRouteStopKind.DropOff =>
                            ContractRouteStopKind.DropOff,
                        _ => throw new ArgumentOutOfRangeException(nameof(token)),
                    })).ToArray());

    private static CommitmentVectorContract MapVector(CommitmentVector value) =>
        new(
            value.PickupEtaTotalMs,
            value.DropEtaTotalMs,
            value.MaterialEtaRevisionCount,
            value.VehicleSwitchCount,
            value.PickupStopRelocationMm,
            value.PickupStopSwitchCount,
            value.DropStopRelocationMm,
            value.DropStopSwitchCount,
            value.IncumbentOrderInversionCount,
            value.PrePickupInsertedStopCount);

    private static JsonElement MapRequestAction(RequestDecisionAction action)
    {
        return action.Outcome switch
        {
            RequestDecisionOutcome.Accepted =>
                OnlineDecisionActionCodec.Encode(
                    new OnlineDecisionAction(
                        DecisionType.RequestAccepted,
                        new RequestAcceptedActionPayload(
                            action.RequestId.Value,
                            action.VehicleId!.Value.Value,
                            action.CandidateId!))),
            RequestDecisionOutcome.Rejected =>
                OnlineDecisionActionCodec.Encode(
                    new OnlineDecisionAction(
                        DecisionType.RequestRejected,
                        new RequestOutcomeActionPayload(
                            action.RequestId.Value,
                            action.ReasonCode))),
            RequestDecisionOutcome.Deferred =>
                OnlineDecisionActionCodec.Encode(
                    new OnlineDecisionAction(
                        DecisionType.RequestDeferred,
                        new RequestOutcomeActionPayload(
                            action.RequestId.Value,
                            action.ReasonCode))),
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
    }

    private static RoutePlanContract MapRoute(RoutePlan route) =>
        new(
            route.Version.Value,
            route.ExecutedStopCount,
            route.FrozenPrefix.Select(MapStop).ToArray(),
            route.MutableSuffix.Select(MapStop).ToArray());

    private static RouteStopContract MapStop(RouteStop stop) =>
        new(
            stop.StopId.Value,
            stop.NodeId.Value,
            stop.Kind switch
            {
                DomainRouteStopKind.Waypoint => ContractRouteStopKind.Waypoint,
                DomainRouteStopKind.Pickup => ContractRouteStopKind.Pickup,
                DomainRouteStopKind.DropOff => ContractRouteStopKind.DropOff,
                _ => throw new ArgumentOutOfRangeException(nameof(stop)),
            },
            stop.RequestId?.Value,
            stop.ServiceDuration.Milliseconds);
}
