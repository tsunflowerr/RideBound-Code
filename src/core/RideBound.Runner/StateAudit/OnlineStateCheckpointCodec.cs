using System.Text.Json;
using RideBound.Application.State;
using RideBound.Application.Travel;
using RideBound.Contracts.Serialization;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Incidents;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Validation;
using RideBound.Domain.Vehicles;

namespace RideBound.Runner.StateAudit;

public sealed record OnlineStateCheckpointReadResult(
    OnlineState? State,
    string? Error)
{
    public bool IsSuccess => State is not null;

    public static OnlineStateCheckpointReadResult Success(OnlineState state) =>
        new(state, null);

    public static OnlineStateCheckpointReadResult Failure(string error) =>
        new(null, error);
}

public static class OnlineStateCheckpointCodec
{
    /// <param name="shadowAllocation">
    /// Whether the run uses Shadow Control. Only then is a `shadowAllocations` member read;
    /// otherwise it stays unknown and the canonical round trip rejects it, as before Shadow existed.
    /// </param>
    public static OnlineStateCheckpointReadResult Decode(JsonElement element, bool shadowAllocation = false)
    {
        try
        {
            var runId = new RunIdentifier(Text(element, "runId"));
            var scenarioId = new ScenarioIdentifier(Text(element, "scenarioId"));
            var appliedEpoch = Integer(element, "appliedEpoch");
            var simulationTime = new SimTime(Integer(element, "simulationTimeMs"));

            // ADR-076: only a rider with a recorded late pickup may rehydrate a pickup after
            // its latest pickup; ValidateRelations then matches every record to its rider.
            var latePickupRequests = LatePickupRequestIds(
                element.GetProperty("incidentLedger"));
            var requests = element.GetProperty("requests")
                .EnumerateArray()
                .Select(value => ReadRequest(value, latePickupRequests))
                .ToArray();
            var vehicles = element.GetProperty("vehicles")
                .EnumerateArray()
                .Select(ReadVehicle)
                .ToArray();
            var run = RideBoundRun.Rehydrate(
                runId,
                scenarioId,
                appliedEpoch,
                simulationTime,
                requests,
                vehicles);

            if (!run.IsSuccess)
            {
                return OnlineStateCheckpointReadResult.Failure(
                    run.Failure!.Message);
            }

            TravelTimeSnapshot? travel = null;

            if (element.TryGetProperty("travelTimes", out var travelElement))
            {
                var created = TravelTimeSnapshot.Create(
                    Integer(travelElement, "version"),
                    Text(travelElement, "snapshotHash"),
                    travelElement.GetProperty("arcs")
                        .EnumerateArray()
                        .Select(
                            arc => new KeyValuePair<TravelArc, Duration>(
                                new TravelArc(
                                    new NodeId(Text(arc, "fromNodeId")),
                                    new NodeId(Text(arc, "toNodeId"))),
                                new Duration(Integer(arc, "travelTimeMs")))));

                if (!created.IsSuccess)
                {
                    return OnlineStateCheckpointReadResult.Failure(
                        created.Failure!.Message);
                }

                travel = created.Value;
            }

            var commitments = ReadCommitmentLedger(
                element.GetProperty("commitmentLedger"));

            // ADR-089 GĐ4: present only under Shadow Control; the canonical round trip below
            // rejects any other shape.
            if (shadowAllocation && element.TryGetProperty("shadowAllocations", out var shadowElement))
            {
                foreach (var item in shadowElement.EnumerateArray())
                {
                    commitments = commitments.WithShadowAllocation(
                        new RequestId(Text(item, "requestId")),
                        new ShadowAllocation(
                            OnlineStateCanonicalWriter.ShadowClassFrom(Text(item, "class")),
                            Text(item, "score")));
                }
            }

            var incidents = ReadIncidentLedger(
                element.GetProperty("incidentLedger"));
            var nextEventSequence = Integer(element, "nextEventSeq");
            var expectedInitialHash = Text(
                element,
                "expectedInitialTravelTimeSnapshotHash");
            var relationError = ValidateRelations(
                run.Value!,
                travel,
                expectedInitialHash,
                commitments,
                incidents,
                nextEventSequence);

            if (relationError is not null)
            {
                return OnlineStateCheckpointReadResult.Failure(relationError);
            }

            var state = new OnlineState(
                run.Value!,
                travel,
                nextEventSequence,
                expectedInitialHash,
                commitments,
                incidents);
            var inputCanonical = CanonicalJson.Canonicalize(
                JsonSerializer.SerializeToUtf8Bytes(element));
            var rebuiltCanonical = OnlineStateCanonicalizer.Canonicalize(state);

            return inputCanonical.SequenceEqual(rebuiltCanonical)
                ? OnlineStateCheckpointReadResult.Success(state)
                : OnlineStateCheckpointReadResult.Failure(
                    "Checkpoint online state is not an exact canonical domain state.");
        }
        catch (Exception error) when (
            error is ArgumentException
                or InvalidOperationException
                or KeyNotFoundException
                or OverflowException
                or JsonException)
        {
            return OnlineStateCheckpointReadResult.Failure(error.Message);
        }
    }

    private static IReadOnlySet<RequestId> LatePickupRequestIds(JsonElement incidentLedger) =>
        incidentLedger.TryGetProperty("latePickups", out var latePickups)
            ? latePickups.EnumerateArray()
                .Select(value => new RequestId(Text(value, "requestId")))
                .ToHashSet()
            : new HashSet<RequestId>();

    private static RideRequest ReadRequest(
        JsonElement element,
        IReadOnlySet<RequestId> latePickupRequests)
    {
        var lifecycle = Text(element, "lifecycle") switch
        {
            "pending" => RequestLifecycle.Pending,
            "accepted" => RequestLifecycle.Accepted,
            "waitingPickup" => RequestLifecycle.WaitingPickup,
            "onboard" => RequestLifecycle.Onboard,
            "completed" => RequestLifecycle.Completed,
            "rejected" => RequestLifecycle.Rejected,
            "cancelledBeforeAcceptance" =>
                RequestLifecycle.CancelledBeforeAcceptance,
            "cancelledAfterAcceptance" =>
                RequestLifecycle.CancelledAfterAcceptance,
            _ => throw new InvalidOperationException("Unknown request lifecycle."),
        };
        var requestId = new RequestId(Text(element, "requestId"));
        var request = RideRequest.Rehydrate(
            requestId,
            new SimTime(Integer(element, "arrivalTimeMs")),
            new NodeId(Text(element, "originNodeId")),
            new NodeId(Text(element, "destinationNodeId")),
            new SimTime(Integer(element, "earliestPickupMs")),
            new SimTime(Integer(element, "latestPickupMs")),
            new Duration(Integer(element, "maxRideTimeMs")),
            Integer(element, "partySize"),
            Text(element, "serviceClass"),
            Text(element, "commitmentPolicyId"),
            lifecycle,
            element.TryGetProperty("assignedVehicleId", out var vehicle)
                ? new VehicleId(vehicle.GetString()!)
                : null,
            element.TryGetProperty("actualPickupTimeMs", out var pickup)
                ? new SimTime(pickup.GetInt64())
                : null,
            latePickupRequests.Contains(requestId));
        return request.IsSuccess
            ? request.Value!
            : throw new InvalidOperationException(request.Failure!.Message);
    }

    private static VehicleState ReadVehicle(JsonElement element)
    {
        var vehicle = VehicleState.Create(
            new VehicleId(Text(element, "vehicleId")),
            Integer(element, "capacity"),
            Integer(element, "occupiedSeats"),
            ReadPosition(element.GetProperty("position")),
            ReadRequestIds(element.GetProperty("onboardRequestIds")),
            ReadRequestIds(element.GetProperty("acceptedRequestIds")),
            ReadRoute(element.GetProperty("route")),
            Integer(element, "lastObservedEpoch"));
        return vehicle.IsSuccess
            ? vehicle.Value!
            : throw new InvalidOperationException(vehicle.Failure!.Message);
    }

    private static VehiclePosition ReadPosition(JsonElement element) =>
        Text(element, "kind") switch
        {
            "node" => new NodePosition(new NodeId(Text(element, "nodeId"))),
            "edgeProgress" => new EdgeProgressPosition(
                new NodeId(Text(element, "fromNodeId")),
                new NodeId(Text(element, "toNodeId")),
                Text(element, "edgeId"),
                Integer(element, "progressPermille")),
            _ => throw new InvalidOperationException("Unknown vehicle position."),
        };

    private static RoutePlan ReadRoute(JsonElement element)
    {
        var route = RoutePlan.Create(
            new PlanVersion(Integer(element, "planVersion")),
            Integer(element, "executedStopCount"),
            element.GetProperty("frozenPrefix").EnumerateArray().Select(ReadStop),
            element.GetProperty("mutableSuffix").EnumerateArray().Select(ReadStop));
        return route.IsSuccess
            ? route.Value!
            : throw new InvalidOperationException(route.Failure!.Message);
    }

    private static RouteStop ReadStop(JsonElement element) =>
        new(
            new StopId(Text(element, "stopId")),
            new NodeId(Text(element, "nodeId")),
            Text(element, "kind") switch
            {
                "waypoint" => RouteStopKind.Waypoint,
                "pickup" => RouteStopKind.Pickup,
                "dropOff" => RouteStopKind.DropOff,
                _ => throw new InvalidOperationException("Unknown route stop kind."),
            },
            element.TryGetProperty("requestId", out var request)
                ? new RequestId(request.GetString()!)
                : null,
            new Duration(Integer(element, "serviceDurationMs")));

    private static CommitmentLedger ReadCommitmentLedger(JsonElement element)
    {
        var ledger = CommitmentLedger.Empty;

        foreach (var history in element.EnumerateArray())
        {
            foreach (var entry in history.GetProperty("entries").EnumerateArray())
            {
                var kind = Text(entry, "kind");
                var published = ReadPublishedPromise(
                    entry.GetProperty("publishedPromise"));
                var publicationId = Text(entry, "publicationId");
                var reason = Text(entry, "reasonCode");
                var source = Integer(entry, "sourceEventSeq");

                if (kind == "initialPromise")
                {
                    var opened = ledger.OpenInitial(
                        publicationId,
                        published.Projection,
                        published.PublishedEpoch,
                        published.PublishedAt,
                        reason,
                        source);
                    ledger = opened.IsSuccess
                        ? opened.Ledger!
                        : throw new InvalidOperationException(opened.Failure!.Message);
                    continue;
                }

                if (kind != "revision")
                {
                    throw new InvalidOperationException("Unknown ledger entry kind.");
                }

                var requestId = published.Projection.RequestId;
                var current = ledger.Histories[requestId].Current;
                var basis = Text(entry, "budgetBasis") switch
                {
                    "decisionInduced" => CommitmentBudgetBasis.DecisionInduced,
                    "customerVisible" => CommitmentBudgetBasis.CustomerVisible,
                    _ => throw new InvalidOperationException("Unknown budget basis."),
                };
                var appended = ledger.AppendRevision(
                    publicationId,
                    requestId,
                    current.PublishedPromise.Version,
                    ReadPromiseProjection(entry.GetProperty("exogenousProjection")),
                    published.Projection,
                    ReadDeltas(entry.GetProperty("deltas")),
                    basis,
                    published.PublishedEpoch,
                    published.PublishedAt,
                    reason,
                    source);
                ledger = appended.IsSuccess
                    ? appended.Ledger!
                    : throw new InvalidOperationException(appended.Failure!.Message);
            }
        }

        return ledger;
    }

    private static OperationalIncidentLedger ReadIncidentLedger(
        JsonElement element)
    {
        var ledger = OperationalIncidentLedger.Empty;
        var resolutions = new List<(IncidentId Id, long EventSeq, SimTime At)>();

        foreach (var value in element.GetProperty("incidents").EnumerateArray())
        {
            var id = new IncidentId(Text(value, "incidentId"));
            var opened = ledger.Open(
                id,
                Text(value, "reasonCode"),
                value.GetProperty("affectedVehicleIds")
                    .EnumerateArray()
                    .Select(item => new VehicleId(item.GetString()!)),
                value.GetProperty("affectedRequestIds")
                    .EnumerateArray()
                    .Select(item => new RequestId(item.GetString()!)),
                Integer(value, "openedEventSeq"),
                new SimTime(Integer(value, "openedAtMs")));
            ledger = opened.IsSuccess
                ? opened.Ledger!
                : throw new InvalidOperationException(opened.Failure!.Message);

            if (value.TryGetProperty("resolvedEventSeq", out var resolved))
            {
                resolutions.Add(
                    (id,
                        resolved.GetInt64(),
                        new SimTime(Integer(value, "resolvedAtMs"))));
            }
        }

        foreach (var value in element.GetProperty("breaches").EnumerateArray())
        {
            var requestId = new RequestId(Text(value, "requestId"));
            var previousPromise = ReadPublishedPromise(
                value.GetProperty("previousPromise"));
            var exogenousProjection = ReadPromiseProjection(
                value.GetProperty("exogenousProjection"));
            var deltas = ReadDeltas(value.GetProperty("deltas"));
            var budgetBefore = ReadVector(value.GetProperty("budgetBefore"));
            CommitmentBreachRecord breach;

            if (value.TryGetProperty("kind", out var kind)
                && kind.GetString() == "forcedReference")
            {
                // The kept-route projection is stored as the safety projection; the factory
                // rejects a record whose two projections differ.
                breach = CommitmentBreachRecord.CreateForcedReference(
                    Text(value, "breachId"),
                    requestId,
                    previousPromise,
                    exogenousProjection,
                    ReadPromiseProjection(value.GetProperty("safetyProjection")),
                    deltas,
                    budgetBefore,
                    ReadVector(value.GetProperty("attemptedBudgetAfter")),
                    value.GetProperty("witnessCodes")
                        .EnumerateArray()
                        .Select(item => item.GetString()!),
                    Integer(value, "sourceEventSeq"),
                    Integer(value, "recordedEpoch"),
                    new SimTime(Integer(value, "recordedAtMs")));
            }
            else if (value.TryGetProperty("kind", out kind)
                && kind.GetString() == "forcedNoWorse")
            {
                // The changed route's projection is stored as the safety projection; the factory
                // rejects a record whose two projections are equal or on different vehicles.
                breach = CommitmentBreachRecord.CreateForcedNoWorse(
                    Text(value, "breachId"),
                    requestId,
                    previousPromise,
                    exogenousProjection,
                    ReadPromiseProjection(value.GetProperty("safetyProjection")),
                    deltas,
                    budgetBefore,
                    ReadVector(value.GetProperty("attemptedBudgetAfter")),
                    value.GetProperty("witnessCodes")
                        .EnumerateArray()
                        .Select(item => item.GetString()!),
                    Integer(value, "sourceEventSeq"),
                    Integer(value, "recordedEpoch"),
                    new SimTime(Integer(value, "recordedAtMs")));
            }
            else if (value.TryGetProperty("kind", out kind))
            {
                if (kind.GetString() != "exogenousServiceQuality")
                {
                    throw new InvalidOperationException(
                        "Unknown commitment breach kind.");
                }

                breach = CommitmentBreachRecord.CreateExogenousServiceQuality(
                    Text(value, "breachId"),
                    requestId,
                    previousPromise,
                    exogenousProjection,
                    ReadPromiseProjection(value.GetProperty("safetyProjection")),
                    deltas,
                    budgetBefore,
                    ReadVector(value.GetProperty("attemptedBudgetAfter")),
                    value.GetProperty("witnessCodes")
                        .EnumerateArray()
                        .Select(item => item.GetString()!),
                    value.GetProperty("serviceQualityWitnesses")
                        .EnumerateArray()
                        .Select(
                            item => new ServiceQualityBreach(
                                new RequestId(Text(item, "requestId")),
                                Text(item, "code"),
                                Text(item, "dimension"),
                                Integer(item, "contractualMilliseconds"),
                                Integer(item, "exogenousMilliseconds"))),
                    Integer(value, "sourceEventSeq"),
                    Integer(value, "recordedEpoch"),
                    new SimTime(Integer(value, "recordedAtMs")));
            }
            else
            {
                breach = new CommitmentBreachRecord(
                    Text(value, "breachId"),
                    new IncidentId(Text(value, "incidentId")),
                    requestId,
                    previousPromise,
                    exogenousProjection,
                    ReadPromiseProjection(value.GetProperty("safetyProjection")),
                    deltas,
                    budgetBefore,
                    ReadVector(value.GetProperty("attemptedBudgetAfter")),
                    value.GetProperty("witnessCodes")
                        .EnumerateArray()
                        .Select(item => item.GetString()!),
                    Integer(value, "sourceEventSeq"),
                    Integer(value, "recordedEpoch"),
                    new SimTime(Integer(value, "recordedAtMs")));
            }

            var appended = ledger.AppendBreach(breach);
            ledger = appended.IsSuccess
                ? appended.Ledger!
                : throw new InvalidOperationException(appended.Failure!.Message);
        }

        foreach (var resolution in resolutions)
        {
            var closed = ledger.Resolve(
                resolution.Id,
                resolution.EventSeq,
                resolution.At);
            ledger = closed.IsSuccess
                ? closed.Ledger!
                : throw new InvalidOperationException(closed.Failure!.Message);
        }

        if (element.TryGetProperty("latePickups", out var latePickups))
        {
            foreach (var value in latePickups.EnumerateArray())
            {
                var recorded = ledger.RecordLatePickup(
                    new ObservedLatePickup(
                        new RequestId(Text(value, "requestId")),
                        new VehicleId(Text(value, "vehicleId")),
                        new SimTime(Integer(value, "latestPickupMs")),
                        new SimTime(Integer(value, "actualPickupMs")),
                        Integer(value, "sourceEventSeq"),
                        Integer(value, "recordedEpoch")));
                ledger = recorded.IsSuccess
                    ? recorded.Ledger!
                    : throw new InvalidOperationException(recorded.Failure!.Message);
            }
        }

        return ledger;
    }

    private static string? ValidateRelations(
        RideBoundRun run,
        TravelTimeSnapshot? travel,
        string expectedInitialTravelHash,
        CommitmentLedger commitments,
        OperationalIncidentLedger incidents,
        long nextEventSequence)
    {
        if (nextEventSequence is < 1 or > DomainLimits.MaxCanonicalInteger)
        {
            return "Checkpoint next event sequence is outside the canonical range.";
        }

        if (!IsLowerSha256(expectedInitialTravelHash)
            || travel is { Version: 1 }
                && !string.Equals(
                    travel.SnapshotHash,
                    expectedInitialTravelHash,
                    StringComparison.Ordinal))
        {
            return "Checkpoint initial travel snapshot identity is invalid.";
        }

        if (run.AppliedEpoch == 0)
        {
            if (run.SimulationTime.Milliseconds != 0
                || nextEventSequence != 1
                || travel is not null
                || run.Requests.Count != 0
                || run.Vehicles.Count != 0
                || commitments.Histories.Count != 0
                || incidents.Incidents.Count != 0
                || incidents.Breaches.Count != 0
                || incidents.LatePickups.Count != 0)
            {
                return "A genesis checkpoint must be the exact empty initialized state.";
            }
        }
        else if (travel is null || run.Vehicles.Count == 0)
        {
            return "A post-genesis checkpoint requires travel state and a vehicle.";
        }

        // ADR-076: each late-pickup record names a boarded rider with exactly that pickup, and
        // a rider may only have boarded after its latest pickup when such a record exists.
        foreach (var late in incidents.LatePickups)
        {
            if (!run.Requests.TryGetValue(late.RequestId, out var rider)
                || rider.Lifecycle is not (RequestLifecycle.Onboard
                    or RequestLifecycle.Completed)
                || rider.AssignedVehicleId != late.VehicleId
                || rider.ActualPickupTime != late.ActualPickup
                || rider.LatestPickup != late.LatestPickup
                || late.SourceEventSequence >= nextEventSequence
                || late.RecordedEpoch > run.AppliedEpoch
                || late.ActualPickup.Milliseconds > run.SimulationTime.Milliseconds)
            {
                return "Checkpoint late pickup does not match its boarded rider.";
            }
        }

        if (run.Requests.Values.Any(
                rider => rider.ActualPickupTime is SimTime pickup
                    && pickup.Milliseconds > rider.LatestPickup.Milliseconds
                    && incidents.LatePickups.All(late => late.RequestId != rider.Id)))
        {
            return "Checkpoint rider boarded after its latest pickup without a record.";
        }

        foreach (var history in commitments.Histories.Values)
        {
            if (!run.Requests.TryGetValue(history.RequestId, out var request)
                || request.Lifecycle is RequestLifecycle.Pending
                    or RequestLifecycle.Rejected
                    or RequestLifecycle.CancelledBeforeAcceptance)
            {
                return "Checkpoint commitment history has no previously accepted request.";
            }

            foreach (var entry in history.Entries)
            {
                if (entry.PublishedPromise.PublishedEpoch > run.AppliedEpoch
                    || entry.PublishedPromise.PublishedAt.Milliseconds
                        > run.SimulationTime.Milliseconds
                    || entry.SourceEventSequence >= nextEventSequence
                    || !ProjectionReferencesKnownEntities(
                        run,
                        entry.PublishedPromise.Projection)
                    || !ProjectionReferencesKnownEntities(
                        run,
                        entry.ExogenousProjection)
                    || entry.PreviousPromise is not null
                        && !ProjectionReferencesKnownEntities(
                            run,
                            entry.PreviousPromise.Projection))
                {
                    return "Checkpoint commitment entry crosses its run/event boundary.";
                }
            }
        }

        foreach (var incident in incidents.Incidents.Values)
        {
            if (incident.OpenedEventSequence >= nextEventSequence
                || incident.OpenedAt.Milliseconds
                    > run.SimulationTime.Milliseconds
                || incident.ResolvedEventSequence is long resolved
                    && resolved >= nextEventSequence
                || incident.ResolvedAt is SimTime resolvedAt
                    && resolvedAt.Milliseconds > run.SimulationTime.Milliseconds
                || incident.AffectedVehicleIds.Any(
                    value => !run.Vehicles.ContainsKey(value))
                || incident.AffectedRequestIds.Any(
                    value => !run.Requests.ContainsKey(value)))
            {
                return "Checkpoint incident crosses its run/event boundary.";
            }
        }

        foreach (var breach in incidents.Breaches)
        {
            commitments.Histories.TryGetValue(
                breach.RequestId,
                out var breachHistory);
            var matchingPromise = breachHistory?.Entries.FirstOrDefault(
                entry => SamePublishedPromise(
                        entry.PublishedPromise,
                        breach.PreviousPromise)
                    && entry.BudgetAfter == breach.BudgetBefore);

            if (breach.SourceEventSequence >= nextEventSequence
                || breach.RecordedEpoch > run.AppliedEpoch
                || breach.RecordedAt.Milliseconds
                    > run.SimulationTime.Milliseconds
                || matchingPromise is null
                || breach.RecordedEpoch
                    < matchingPromise.PublishedPromise.PublishedEpoch
                || breach.RecordedAt.Milliseconds
                    < matchingPromise.PublishedPromise.PublishedAt.Milliseconds
                || !ProjectionReferencesKnownEntities(
                    run,
                    breach.PreviousPromise.Projection)
                || !ProjectionReferencesKnownEntities(
                    run,
                    breach.ExogenousProjection)
                || !ProjectionReferencesKnownEntities(
                    run,
                    breach.SafetyProjection))
            {
                return "Checkpoint breach crosses its promise/run boundary.";
            }
        }

        return null;
    }

    private static bool IsLowerSha256(string value) =>
        value.Length == 64
        && value.All(
            character => character is >= '0' and <= '9'
                or >= 'a' and <= 'f');

    private static bool SamePublishedPromise(
        PublishedPromise left,
        PublishedPromise right) =>
        left.Version == right.Version
        && left.PublishedEpoch == right.PublishedEpoch
        && left.PublishedAt == right.PublishedAt
        && SameProjection(left.Projection, right.Projection);

    private static bool SameProjection(
        PromiseProjection left,
        PromiseProjection right) =>
        left.RequestId == right.RequestId
        && left.VehicleId == right.VehicleId
        && left.PickupStopId == right.PickupStopId
        && left.PickupNodeId == right.PickupNodeId
        && left.DropStopId == right.DropStopId
        && left.DropNodeId == right.DropNodeId
        && left.PickupEta == right.PickupEta
        && left.DropEta == right.DropEta
        && left.ServiceOrder.SequenceEqual(right.ServiceOrder);

    private static bool ProjectionReferencesKnownEntities(
        RideBoundRun run,
        PromiseProjection projection) =>
        run.Requests.ContainsKey(projection.RequestId)
        && run.Vehicles.ContainsKey(projection.VehicleId)
        && projection.ServiceOrder.All(
            token => token.RequestId is not RequestId requestId
                || run.Requests.ContainsKey(requestId));

    private static PublishedPromise ReadPublishedPromise(JsonElement element) =>
        new(
            new PromiseVersion(Integer(element, "version")),
            Integer(element, "publishedEpoch"),
            new SimTime(Integer(element, "publishedAtMs")),
            ReadPromiseProjection(element.GetProperty("projection")));

    private static PromiseProjection ReadPromiseProjection(JsonElement element) =>
        new(
            new RequestId(Text(element, "requestId")),
            new VehicleId(Text(element, "vehicleId")),
            new StopId(Text(element, "pickupStopId")),
            new NodeId(Text(element, "pickupNodeId")),
            new StopId(Text(element, "dropStopId")),
            new NodeId(Text(element, "dropNodeId")),
            new SimTime(Integer(element, "pickupEtaMs")),
            new SimTime(Integer(element, "dropEtaMs")),
            element.GetProperty("serviceOrder")
                .EnumerateArray()
                .Select(
                    token => new PromiseServiceToken(
                        new StopId(Text(token, "stopId")),
                        token.TryGetProperty("requestId", out var request)
                            ? new RequestId(request.GetString()!)
                            : null,
                        Text(token, "kind") switch
                        {
                            "waypoint" => RouteStopKind.Waypoint,
                            "pickup" => RouteStopKind.Pickup,
                            "dropOff" => RouteStopKind.DropOff,
                            _ => throw new InvalidOperationException(
                                "Unknown promise token kind."),
                        })));

    private static ThreeWayPromiseDelta ReadDeltas(JsonElement element) =>
        new(
            ReadVector(element.GetProperty("exogenous")),
            ReadVector(element.GetProperty("decisionInduced")),
            ReadVector(element.GetProperty("visible")));

    private static CommitmentVector ReadVector(JsonElement element) =>
        new(
            Integer(element, "pickupEtaTotalMs"),
            Integer(element, "dropEtaTotalMs"),
            Integer(element, "materialEtaRevisionCount"),
            Integer(element, "vehicleSwitchCount"),
            Integer(element, "pickupStopRelocationMm"),
            Integer(element, "pickupStopSwitchCount"),
            Integer(element, "dropStopRelocationMm"),
            Integer(element, "dropStopSwitchCount"),
            Integer(element, "incumbentOrderInversionCount"),
            Integer(element, "prePickupInsertedStopCount"));

    private static IEnumerable<RequestId> ReadRequestIds(JsonElement array) =>
        array.EnumerateArray().Select(value => new RequestId(value.GetString()!));

    private static string Text(JsonElement element, string property) =>
        element.GetProperty(property).GetString()
        ?? throw new InvalidOperationException($"'{property}' must be a string.");

    private static long Integer(JsonElement element, string property) =>
        element.GetProperty(property).GetInt64();
}
