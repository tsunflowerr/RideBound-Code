using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RideBound.Application.State;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Incidents;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;

namespace RideBound.Runner.Online;

public static class OnlineStateCanonicalizer
{
    private static readonly byte[] DomainPrefix =
        "RideBound.OnlineStateHash.v1\0"u8.ToArray();

    /// <summary>
    /// When RIDEBOUND_CANONICAL_SELFCHECK=1, every Canonicalize/CalculateHash call also runs
    /// the reference path and throws on any byte difference (used to validate the direct
    /// writer on the whole test suite).
    /// </summary>
    private static readonly bool SelfCheck = string.Equals(
        Environment.GetEnvironmentVariable("RIDEBOUND_CANONICAL_SELFCHECK"),
        "1",
        StringComparison.Ordinal);

    public static byte[] Canonicalize(OnlineState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var canonical = OnlineStateCanonicalWriter.Write(state, null);
        Verify(state, canonical);
        return canonical.ToArray();
    }

    public static Sha256Hex CalculateHash(OnlineState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var canonical = OnlineStateCanonicalWriter.Write(state, null);
        Verify(state, canonical);
        return OnlineStateCanonicalWriter.HashV1(canonical);
    }

    /// <summary>Throws when the reference canonicalization differs (self-check mode only).</summary>
    public static void Verify(OnlineState state, CanonicalSegments canonical)
    {
        if (!SelfCheck)
        {
            return;
        }

        var equal = canonical.ContentEquals(CanonicalizeReference(state));
        var log = Environment.GetEnvironmentVariable("RIDEBOUND_CANONICAL_SELFCHECK_LOG");

        if (!string.IsNullOrEmpty(log))
        {
            lock (DomainPrefix)
            {
                File.AppendAllText(
                    log,
                    $"{(equal ? "equal" : "DIFFERENT")} {canonical.Length} {state.Run.Requests.Count} {state.Commitments.Histories.Count} {state.Incidents.Breaches.Count} {state.Incidents.Incidents.Count} {state.Incidents.LatePickups.Count} {state.PlanPool.Version}\n");
            }
        }

        if (!equal)
        {
            throw new InvalidOperationException(
                "Direct canonical writer differs from the reference canonicalization.");
        }
    }

    /// <summary>The original v1 path: Utf8JsonWriter followed by CanonicalJson.Canonicalize.</summary>
    public static byte[] CanonicalizeReference(OnlineState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("runId", state.Run.Id.Value);
            writer.WriteString("scenarioId", state.Run.ScenarioId.Value);
            writer.WriteNumber("appliedEpoch", state.Run.AppliedEpoch);
            writer.WriteNumber(
                "simulationTimeMs",
                state.Run.SimulationTime.Milliseconds);
            writer.WriteNumber("nextEventSeq", state.NextEventSequence);
            writer.WriteString(
                "expectedInitialTravelTimeSnapshotHash",
                state.ExpectedInitialTravelTimeSnapshotHash);
            writer.WritePropertyName("requests");
            writer.WriteStartArray();

            foreach (var request in state.Run.Requests.Values.OrderBy(
                         value => value.Id.Value,
                         StringComparer.Ordinal))
            {
                WriteRequest(writer, request);
            }

            writer.WriteEndArray();
            writer.WritePropertyName("vehicles");
            writer.WriteStartArray();

            foreach (var vehicle in state.Run.Vehicles.Values.OrderBy(
                         value => value.Id.Value,
                         StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("vehicleId", vehicle.Id.Value);
                writer.WriteNumber("capacity", vehicle.Capacity);
                writer.WriteNumber("occupiedSeats", vehicle.OccupiedSeats);
                writer.WritePropertyName("position");
                WritePosition(writer, vehicle.Position);
                WriteRequestSet(
                    writer,
                    "onboardRequestIds",
                    vehicle.OnboardRequestIds);
                WriteRequestSet(
                    writer,
                    "acceptedRequestIds",
                    vehicle.AcceptedRequestIds);
                writer.WritePropertyName("route");
                WriteRoute(writer, vehicle.Route);
                writer.WriteNumber(
                    "lastObservedEpoch",
                    vehicle.LastObservedEpoch);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            if (state.TravelTimes is not null)
            {
                writer.WritePropertyName("travelTimes");
                writer.WriteStartObject();
                writer.WriteNumber("version", state.TravelTimes.Version);
                writer.WriteString(
                    "snapshotHash",
                    state.TravelTimes.SnapshotHash);
                writer.WritePropertyName("arcs");
                writer.WriteStartArray();

                foreach (var arc in state.TravelTimes.TravelTimes.OrderBy(
                             value => value.Key.FromNodeId.Value,
                             StringComparer.Ordinal)
                         .ThenBy(
                             value => value.Key.ToNodeId.Value,
                             StringComparer.Ordinal))
                {
                    writer.WriteStartObject();
                    writer.WriteString(
                        "fromNodeId",
                        arc.Key.FromNodeId.Value);
                    writer.WriteString("toNodeId", arc.Key.ToNodeId.Value);
                    writer.WriteNumber(
                        "travelTimeMs",
                        arc.Value.Milliseconds);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            if (state.PlanPool.Version != 0)
            {
                writer.WritePropertyName("planPool");
                WritePlanPool(writer, state.PlanPool);
            }

            writer.WritePropertyName("commitmentLedger");
            WriteCommitmentLedger(writer, state.Commitments);
            writer.WritePropertyName("incidentLedger");
            WriteIncidentLedger(writer, state.Incidents);

            writer.WriteEndObject();
        }

        return CanonicalJson.Canonicalize(buffer.WrittenSpan);
    }

    /// <summary>The original v1 hash path (reference for differential tests).</summary>
    public static Sha256Hex CalculateHashReference(OnlineState state)
    {
        var canonical = CanonicalizeReference(state);
        var buffer = new ArrayBufferWriter<byte>();
        buffer.Write(DomainPrefix);
        WriteFrame(buffer, "canonicalOnlineState", canonical);
        var text = Convert.ToHexStringLower(SHA256.HashData(buffer.WrittenSpan));
        Sha256Hex.TryCreate(text, out var hash);
        return hash!;
    }

    private static void WriteRequest(Utf8JsonWriter writer, RideRequest request)
    {
        writer.WriteStartObject();
        writer.WriteString("requestId", request.Id.Value);
        writer.WriteNumber("arrivalTimeMs", request.ArrivalTime.Milliseconds);
        writer.WriteString("originNodeId", request.OriginNodeId.Value);
        writer.WriteString(
            "destinationNodeId",
            request.DestinationNodeId.Value);
        writer.WriteNumber(
            "earliestPickupMs",
            request.EarliestPickup.Milliseconds);
        writer.WriteNumber(
            "latestPickupMs",
            request.LatestPickup.Milliseconds);
        writer.WriteNumber(
            "maxRideTimeMs",
            request.MaxRideTime.Milliseconds);
        writer.WriteNumber("partySize", request.PartySize);
        writer.WriteString("serviceClass", request.ServiceClass);
        writer.WriteString(
            "commitmentPolicyId",
            request.CommitmentPolicyId);
        writer.WriteString("lifecycle", ToProtocolValue(request.Lifecycle));

        if (request.AssignedVehicleId is VehicleId vehicleId)
        {
            writer.WriteString("assignedVehicleId", vehicleId.Value);
        }

        if (request.ActualPickupTime is SimTime pickupTime)
        {
            writer.WriteNumber(
                "actualPickupTimeMs",
                pickupTime.Milliseconds);
        }

        writer.WriteEndObject();
    }

    private static void WritePosition(
        Utf8JsonWriter writer,
        VehiclePosition position)
    {
        writer.WriteStartObject();

        switch (position)
        {
            case NodePosition node:
                writer.WriteString("kind", "node");
                writer.WriteString("nodeId", node.NodeId.Value);
                break;
            case EdgeProgressPosition edge:
                writer.WriteString("kind", "edgeProgress");
                writer.WriteString("fromNodeId", edge.FromNodeId.Value);
                writer.WriteString("toNodeId", edge.ToNodeId.Value);
                writer.WriteString("edgeId", edge.EdgeId);
                writer.WriteNumber(
                    "progressPermille",
                    edge.ProgressPermille);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(position));
        }

        writer.WriteEndObject();
    }

    private static void WriteRequestSet(
        Utf8JsonWriter writer,
        string propertyName,
        IEnumerable<RequestId> values)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();

        foreach (var requestId in values.OrderBy(
                     value => value.Value,
                     StringComparer.Ordinal))
        {
            writer.WriteStringValue(requestId.Value);
        }

        writer.WriteEndArray();
    }

    private static void WriteRoute(Utf8JsonWriter writer, RoutePlan route)
    {
        writer.WriteStartObject();
        writer.WriteNumber("planVersion", route.Version.Value);
        writer.WriteNumber("executedStopCount", route.ExecutedStopCount);
        writer.WritePropertyName("frozenPrefix");
        WriteStops(writer, route.FrozenPrefix);
        writer.WritePropertyName("mutableSuffix");
        WriteStops(writer, route.MutableSuffix);
        writer.WriteEndObject();
    }

    private static void WriteStops(
        Utf8JsonWriter writer,
        IEnumerable<RouteStop> stops)
    {
        writer.WriteStartArray();

        foreach (var stop in stops)
        {
            writer.WriteStartObject();
            writer.WriteString("stopId", stop.StopId.Value);
            writer.WriteString("nodeId", stop.NodeId.Value);
            writer.WriteString(
                "kind",
                stop.Kind switch
                {
                    RideBound.Domain.Routes.RouteStopKind.Waypoint =>
                        "waypoint",
                    RideBound.Domain.Routes.RouteStopKind.Pickup =>
                        "pickup",
                    RideBound.Domain.Routes.RouteStopKind.DropOff =>
                        "dropOff",
                    _ => throw new ArgumentOutOfRangeException(nameof(stop)),
                });

            if (stop.RequestId is RequestId requestId)
            {
                writer.WriteString("requestId", requestId.Value);
            }

            writer.WriteNumber(
                "serviceDurationMs",
                stop.ServiceDuration.Milliseconds);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WritePlanPool(
        Utf8JsonWriter writer,
        VersionedPlanPool pool)
    {
        writer.WriteStartObject();
        writer.WriteNumber("version", pool.Version);
        writer.WriteNumber("sourceEpoch", pool.SourceEpoch);
        writer.WriteString("distinguishedPlanId", pool.DistinguishedPlanId);
        writer.WritePropertyName("plans");
        writer.WriteStartArray();

        foreach (var plan in pool.Plans.OrderBy(
                     value => value.PlanId,
                     StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("planId", plan.PlanId);
            writer.WriteNumber("sourceEpoch", plan.SourceEpoch);
            writer.WritePropertyName("vehiclePlans");
            writer.WriteStartArray();

            foreach (var vehicle in plan.VehiclePlans.OrderBy(
                         value => value.VehicleId.Value,
                         StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("vehicleId", vehicle.VehicleId.Value);
                writer.WritePropertyName("route");
                WriteRoute(writer, vehicle.Route);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static string ToProtocolValue(RequestLifecycle lifecycle) =>
        lifecycle switch
        {
            RequestLifecycle.Pending => "pending",
            RequestLifecycle.Accepted => "accepted",
            RequestLifecycle.WaitingPickup => "waitingPickup",
            RequestLifecycle.Onboard => "onboard",
            RequestLifecycle.Completed => "completed",
            RequestLifecycle.Rejected => "rejected",
            RequestLifecycle.CancelledBeforeAcceptance =>
                "cancelledBeforeAcceptance",
            RequestLifecycle.CancelledAfterAcceptance =>
                "cancelledAfterAcceptance",
            _ => throw new ArgumentOutOfRangeException(nameof(lifecycle)),
        };

    private static void WriteCommitmentLedger(
        Utf8JsonWriter writer,
        CommitmentLedger ledger)
    {
        writer.WriteStartArray();

        foreach (var history in ledger.Histories.Values.OrderBy(
                     value => value.RequestId.Value,
                     StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("requestId", history.RequestId.Value);
            writer.WritePropertyName("entries");
            writer.WriteStartArray();

            foreach (var entry in history.Entries)
            {
                writer.WriteStartObject();
                writer.WriteString("publicationId", entry.PublicationId);
                writer.WriteString(
                    "kind",
                    entry.Kind == CommitmentLedgerEntryKind.InitialPromise
                        ? "initialPromise"
                        : "revision");
                writer.WritePropertyName("publishedPromise");
                WritePublishedPromise(writer, entry.PublishedPromise);

                if (entry.PreviousPromise is not null)
                {
                    writer.WritePropertyName("previousPromise");
                    WritePublishedPromise(writer, entry.PreviousPromise);
                }

                writer.WritePropertyName("exogenousProjection");
                WritePromiseProjection(writer, entry.ExogenousProjection);
                writer.WritePropertyName("deltas");
                writer.WriteStartObject();
                writer.WritePropertyName("exogenous");
                WriteVector(writer, entry.Deltas.Exogenous);
                writer.WritePropertyName("decisionInduced");
                WriteVector(writer, entry.Deltas.DecisionInduced);
                writer.WritePropertyName("visible");
                WriteVector(writer, entry.Deltas.Visible);
                writer.WriteEndObject();
                writer.WritePropertyName("budgetBefore");
                WriteVector(writer, entry.BudgetBefore);
                writer.WritePropertyName("budgetAfter");
                WriteVector(writer, entry.BudgetAfter);

                if (entry.BudgetBasis is CommitmentBudgetBasis budgetBasis)
                {
                    writer.WriteString(
                        "budgetBasis",
                        budgetBasis == CommitmentBudgetBasis.DecisionInduced
                            ? "decisionInduced"
                            : "customerVisible");
                }

                writer.WriteString("reasonCode", entry.ReasonCode);
                writer.WriteNumber(
                    "sourceEventSeq",
                    entry.SourceEventSequence);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteIncidentLedger(
        Utf8JsonWriter writer,
        OperationalIncidentLedger ledger)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("incidents");
        writer.WriteStartArray();

        foreach (var incident in ledger.Incidents.Values.OrderBy(
                     value => value.Id.Value,
                     StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("incidentId", incident.Id.Value);
            writer.WriteString("reasonCode", incident.ReasonCode);
            WriteIdentifiers(
                writer,
                "affectedVehicleIds",
                incident.AffectedVehicleIds.Select(value => value.Value));
            WriteIdentifiers(
                writer,
                "affectedRequestIds",
                incident.AffectedRequestIds.Select(value => value.Value));
            writer.WriteNumber(
                "openedEventSeq",
                incident.OpenedEventSequence);
            writer.WriteNumber(
                "openedAtMs",
                incident.OpenedAt.Milliseconds);

            if (incident.ResolvedEventSequence is long resolvedSequence)
            {
                writer.WriteNumber("resolvedEventSeq", resolvedSequence);
                writer.WriteNumber(
                    "resolvedAtMs",
                    incident.ResolvedAt!.Value.Milliseconds);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WritePropertyName("breaches");
        writer.WriteStartArray();

        foreach (var breach in ledger.Breaches)
        {
            writer.WriteStartObject();
            writer.WriteString("breachId", breach.BreachId);

            // Exhaustive by kind: an operational record writes its incident, every other kind
            // writes its own name. A new kind must never fall into another kind's encoding.
            switch (breach.Kind)
            {
                case CommitmentBreachKind.OperationalIncident:
                    writer.WriteString("incidentId", breach.IncidentId!.Value.Value);
                    break;
                case CommitmentBreachKind.ExogenousServiceQuality:
                    writer.WriteString("kind", "exogenousServiceQuality");
                    break;
                case CommitmentBreachKind.ForcedReference:
                    writer.WriteString("kind", "forcedReference");
                    break;
                case CommitmentBreachKind.ForcedNoWorse:
                    writer.WriteString("kind", "forcedNoWorse");
                    break;
                default:
                    throw new InvalidOperationException("Unknown commitment breach kind.");
            }

            writer.WriteString("requestId", breach.RequestId.Value);
            writer.WritePropertyName("previousPromise");
            WritePublishedPromise(writer, breach.PreviousPromise);
            writer.WritePropertyName("exogenousProjection");
            WritePromiseProjection(writer, breach.ExogenousProjection);
            writer.WritePropertyName("safetyProjection");
            WritePromiseProjection(writer, breach.SafetyProjection);
            writer.WritePropertyName("deltas");
            writer.WriteStartObject();
            writer.WritePropertyName("exogenous");
            WriteVector(writer, breach.Deltas.Exogenous);
            writer.WritePropertyName("decisionInduced");
            WriteVector(writer, breach.Deltas.DecisionInduced);
            writer.WritePropertyName("visible");
            WriteVector(writer, breach.Deltas.Visible);
            writer.WriteEndObject();
            writer.WritePropertyName("budgetBefore");
            WriteVector(writer, breach.BudgetBefore);
            writer.WritePropertyName("attemptedBudgetAfter");
            WriteVector(writer, breach.AttemptedBudgetAfter);
            WriteIdentifiers(writer, "witnessCodes", breach.WitnessCodes);

            if (breach.Kind == CommitmentBreachKind.ExogenousServiceQuality)
            {
                writer.WritePropertyName("serviceQualityWitnesses");
                writer.WriteStartArray();

                foreach (var witness in breach.ServiceQualityWitnesses)
                {
                    writer.WriteStartObject();
                    writer.WriteString("requestId", witness.RequestId.Value);
                    writer.WriteString("code", witness.Code);
                    writer.WriteString("dimension", witness.Dimension);
                    writer.WriteNumber(
                        "contractualMilliseconds",
                        witness.ContractualMilliseconds);
                    writer.WriteNumber(
                        "exogenousMilliseconds",
                        witness.ExogenousMilliseconds);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            writer.WriteNumber("sourceEventSeq", breach.SourceEventSequence);
            writer.WriteNumber("recordedEpoch", breach.RecordedEpoch);
            writer.WriteNumber("recordedAtMs", breach.RecordedAt.Milliseconds);
            writer.WriteBoolean("normalOperation", breach.NormalOperation);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        // ADR-076: written only when non-empty, so a state without a late boarding keeps
        // its canonical bytes and hash.
        if (ledger.LatePickups.Count != 0)
        {
            writer.WritePropertyName("latePickups");
            writer.WriteStartArray();

            foreach (var late in ledger.LatePickups)
            {
                writer.WriteStartObject();
                writer.WriteString("requestId", late.RequestId.Value);
                writer.WriteString("vehicleId", late.VehicleId.Value);
                writer.WriteNumber("latestPickupMs", late.LatestPickup.Milliseconds);
                writer.WriteNumber("actualPickupMs", late.ActualPickup.Milliseconds);
                writer.WriteNumber("sourceEventSeq", late.SourceEventSequence);
                writer.WriteNumber("recordedEpoch", late.RecordedEpoch);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static void WritePublishedPromise(
        Utf8JsonWriter writer,
        PublishedPromise promise)
    {
        writer.WriteStartObject();
        writer.WriteNumber("version", promise.Version.Value);
        writer.WriteNumber("publishedEpoch", promise.PublishedEpoch);
        writer.WriteNumber("publishedAtMs", promise.PublishedAt.Milliseconds);
        writer.WritePropertyName("projection");
        WritePromiseProjection(writer, promise.Projection);
        writer.WriteEndObject();
    }

    private static void WritePromiseProjection(
        Utf8JsonWriter writer,
        PromiseProjection promise)
    {
        writer.WriteStartObject();
        writer.WriteString("requestId", promise.RequestId.Value);
        writer.WriteString("vehicleId", promise.VehicleId.Value);
        writer.WriteString("pickupStopId", promise.PickupStopId.Value);
        writer.WriteString("pickupNodeId", promise.PickupNodeId.Value);
        writer.WriteString("dropStopId", promise.DropStopId.Value);
        writer.WriteString("dropNodeId", promise.DropNodeId.Value);
        writer.WriteNumber("pickupEtaMs", promise.PickupEta.Milliseconds);
        writer.WriteNumber("dropEtaMs", promise.DropEta.Milliseconds);
        writer.WritePropertyName("serviceOrder");
        writer.WriteStartArray();

        foreach (var token in promise.ServiceOrder)
        {
            writer.WriteStartObject();
            writer.WriteString("stopId", token.StopId.Value);

            if (token.RequestId is RequestId requestId)
            {
                writer.WriteString("requestId", requestId.Value);
            }

            writer.WriteString(
                "kind",
                token.Kind switch
                {
                    RideBound.Domain.Routes.RouteStopKind.Waypoint => "waypoint",
                    RideBound.Domain.Routes.RouteStopKind.Pickup => "pickup",
                    RideBound.Domain.Routes.RouteStopKind.DropOff => "dropOff",
                    _ => throw new ArgumentOutOfRangeException(nameof(token)),
                });
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteVector(
        Utf8JsonWriter writer,
        CommitmentVector value)
    {
        writer.WriteStartObject();
        writer.WriteNumber("pickupEtaTotalMs", value.PickupEtaTotalMs);
        writer.WriteNumber("dropEtaTotalMs", value.DropEtaTotalMs);
        writer.WriteNumber(
            "materialEtaRevisionCount",
            value.MaterialEtaRevisionCount);
        writer.WriteNumber("vehicleSwitchCount", value.VehicleSwitchCount);
        writer.WriteNumber(
            "pickupStopRelocationMm",
            value.PickupStopRelocationMm);
        writer.WriteNumber(
            "pickupStopSwitchCount",
            value.PickupStopSwitchCount);
        writer.WriteNumber(
            "dropStopRelocationMm",
            value.DropStopRelocationMm);
        writer.WriteNumber("dropStopSwitchCount", value.DropStopSwitchCount);
        writer.WriteNumber(
            "incumbentOrderInversionCount",
            value.IncumbentOrderInversionCount);
        writer.WriteNumber(
            "prePickupInsertedStopCount",
            value.PrePickupInsertedStopCount);
        writer.WriteEndObject();
    }

    private static void WriteIdentifiers(
        Utf8JsonWriter writer,
        string propertyName,
        IEnumerable<string> values)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();

        foreach (var value in values.Order(StringComparer.Ordinal))
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }

    private static void WriteFrame(
        IBufferWriter<byte> writer,
        string tag,
        ReadOnlySpan<byte> value)
    {
        var tagBytes = Encoding.UTF8.GetBytes(tag);
        var header = writer.GetSpan(sizeof(ushort));
        BinaryPrimitives.WriteUInt16BigEndian(
            header,
            checked((ushort)tagBytes.Length));
        writer.Advance(sizeof(ushort));
        writer.Write(tagBytes);
        header = writer.GetSpan(sizeof(ulong));
        BinaryPrimitives.WriteUInt64BigEndian(header, (ulong)value.Length);
        writer.Advance(sizeof(ulong));
        writer.Write(value);
    }
}
