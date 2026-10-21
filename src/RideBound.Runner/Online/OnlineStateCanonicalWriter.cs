using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using RideBound.Application.State;
using RideBound.Application.Travel;
using RideBound.Contracts.Protocol;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Incidents;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Vehicles;

namespace RideBound.Runner.Online;

/// <summary>
/// Per-entity canonical fragments of immutable domain objects, keyed by object identity.
/// Every cached type is deeply immutable (get-only members over frozen collections or
/// Array.AsReadOnly copies), so an object that is still referenced has the same canonical
/// bytes forever. Entries disappear with their objects (ConditionalWeakTable).
/// </summary>
public sealed class OnlineStateCanonicalCache
{
    /// <summary>
    /// False (default): ledger entries are separate segments, cache memory ~1x the canonical
    /// ledger. True: one concatenated fragment per rider history as well (~2x memory, fewer
    /// segments).
    /// </summary>
    public bool CacheHistories { get; init; }

    internal ConditionalWeakTable<RideRequest, byte[]> Requests { get; } = new();

    internal ConditionalWeakTable<VehicleState, byte[]> Vehicles { get; } = new();

    internal ConditionalWeakTable<TravelTimeSnapshot, byte[]> TravelTimes { get; } = new();

    internal ConditionalWeakTable<CommitmentLedgerEntry, byte[]> LedgerEntries { get; } = new();

    internal ConditionalWeakTable<RiderCommitmentHistory, byte[]> LedgerHistories { get; } = new();

    internal ConditionalWeakTable<OperationalIncident, byte[]> Incidents { get; } = new();

    internal ConditionalWeakTable<CommitmentBreachRecord, byte[]> Breaches { get; } = new();

    public long FragmentMisses { get; internal set; }

    public long FragmentHits { get; internal set; }
}

/// <summary>
/// Direct writer of the canonical online state (OnlineStateHash.v1). It produces exactly the
/// bytes of the reference path OnlineStateCanonicalizer.CanonicalizeReference
/// (Utf8JsonWriter + CanonicalJson.Canonicalize), with member names in ordinal order as
/// CanonicalJson sorts them. With a cache, unchanged entities are reused by reference.
/// </summary>
public static class OnlineStateCanonicalWriter
{
    private static readonly byte[] DomainPrefix =
        "RideBound.OnlineStateHash.v1\0"u8.ToArray();

    private static readonly byte[] FrameTag = "canonicalOnlineState"u8.ToArray();

    public static CanonicalSegments Write(OnlineState state, OnlineStateCanonicalCache? cache)
    {
        ArgumentNullException.ThrowIfNull(state);
        var builder = new CanonicalSegmentBuilder();
        var glue = builder.Glue;
        var root = new CanonicalObject(glue);
        root.Member("appliedEpoch"u8).Integer(state.Run.AppliedEpoch);

        root.Member("commitmentLedger"u8).Byte((byte)'[');
        var firstHistory = true;

        foreach (var history in state.Commitments.Histories.Values.OrderBy(
                     value => value.RequestId.Value,
                     StringComparer.Ordinal))
        {
            if (!firstHistory)
            {
                glue.Byte((byte)',');
            }

            firstHistory = false;

            if (cache is { CacheHistories: true })
            {
                builder.Fragment(HistoryFragment(history, cache));
                continue;
            }

            // Default: one segment per ledger entry (memory ~1x the canonical ledger bytes).
            var item = new CanonicalObject(glue);
            item.Member("entries"u8).Byte((byte)'[');

            for (var index = 0; index < history.Entries.Count; index++)
            {
                if (index > 0)
                {
                    glue.Byte((byte)',');
                }

                builder.Fragment(Cached(cache?.LedgerEntries, history.Entries[index], cache, WriteLedgerEntry));
            }

            glue.Byte((byte)']');
            item.Member("requestId"u8).String(history.RequestId.Value);
            item.End();
        }

        glue.Byte((byte)']');
        root.Member("expectedInitialTravelTimeSnapshotHash"u8)
            .String(state.ExpectedInitialTravelTimeSnapshotHash);
        root.Member("incidentLedger"u8);
        WriteIncidentLedger(builder, state.Incidents, cache);
        root.Member("nextEventSeq"u8).Integer(state.NextEventSequence);

        if (state.PlanPool.Version != 0)
        {
            root.Member("planPool"u8);
            WritePlanPool(glue, state.PlanPool);
        }

        root.Member("requests"u8).Byte((byte)'[');
        var first = true;

        foreach (var request in state.Run.Requests.Values.OrderBy(
                     value => value.Id.Value,
                     StringComparer.Ordinal))
        {
            if (!first)
            {
                glue.Byte((byte)',');
            }

            first = false;
            builder.Fragment(Cached(cache?.Requests, request, cache, WriteRequest));
        }

        glue.Byte((byte)']');
        root.Member("runId"u8).String(state.Run.Id.Value);
        root.Member("scenarioId"u8).String(state.Run.ScenarioId.Value);
        root.Member("simulationTimeMs"u8).Integer(state.Run.SimulationTime.Milliseconds);

        if (state.TravelTimes is not null)
        {
            root.Member("travelTimes"u8);
            builder.Fragment(Cached(cache?.TravelTimes, state.TravelTimes, cache, WriteTravelTimes));
        }

        root.Member("vehicles"u8).Byte((byte)'[');
        first = true;

        foreach (var vehicle in state.Run.Vehicles.Values.OrderBy(
                     value => value.Id.Value,
                     StringComparer.Ordinal))
        {
            if (!first)
            {
                glue.Byte((byte)',');
            }

            first = false;
            builder.Fragment(Cached(cache?.Vehicles, vehicle, cache, WriteVehicle));
        }

        glue.Byte((byte)']');
        root.End();
        return builder.Build();
    }

    /// <summary>OnlineStateCanonicalizer.CalculateHash over already built canonical bytes.</summary>
    public static Sha256Hex HashV1(CanonicalSegments canonical)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(DomainPrefix);
        Span<byte> header = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16BigEndian(header, (ushort)FrameTag.Length);
        hash.AppendData(header);
        hash.AppendData(FrameTag);
        Span<byte> length = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(length, (ulong)canonical.Length);
        hash.AppendData(length);
        canonical.AppendTo(hash);
        Sha256Hex.TryCreate(Convert.ToHexStringLower(hash.GetHashAndReset()), out var result);
        return result!;
    }

    private static byte[] Cached<T>(
        ConditionalWeakTable<T, byte[]>? table,
        T value,
        OnlineStateCanonicalCache? cache,
        Action<CanonicalBuffer, T> write)
        where T : class
    {
        if (table is not null && table.TryGetValue(value, out var cached))
        {
            cache!.FragmentHits++;
            return cached;
        }

        var buffer = new CanonicalBuffer(256);
        write(buffer, value);
        var fragment = buffer.ToArray();

        if (table is not null)
        {
            cache!.FragmentMisses++;
            table.AddOrUpdate(value, fragment);
        }

        return fragment;
    }

    private static byte[] HistoryFragment(
        RiderCommitmentHistory history,
        OnlineStateCanonicalCache? cache)
    {
        if (cache is not null && cache.LedgerHistories.TryGetValue(history, out var cached))
        {
            cache.FragmentHits++;
            return cached;
        }

        var entries = new byte[history.Entries.Count][];
        var size = 64 + Encoding.UTF8.GetMaxByteCount(history.RequestId.Value.Length);

        for (var index = 0; index < entries.Length; index++)
        {
            entries[index] = Cached(cache?.LedgerEntries, history.Entries[index], cache, WriteLedgerEntry);
            size += entries[index].Length + 1;
        }

        var buffer = new CanonicalBuffer(size);
        var item = new CanonicalObject(buffer);
        item.Member("entries"u8).Byte((byte)'[');

        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                buffer.Byte((byte)',');
            }

            buffer.Raw(entries[index]);
        }

        buffer.Byte((byte)']');
        item.Member("requestId"u8).String(history.RequestId.Value);
        item.End();
        var fragment = buffer.ToArray();

        if (cache is not null)
        {
            cache.FragmentMisses++;
            cache.LedgerHistories.AddOrUpdate(history, fragment);
        }

        return fragment;
    }

    // OnlineStateCanonicalizer.WriteRequest (OnlineStateCanonicalizer.cs:142-180), ordinal member order.
    internal static void WriteRequest(CanonicalBuffer buffer, RideRequest request)
    {
        var item = new CanonicalObject(buffer);

        if (request.ActualPickupTime is SimTime pickupTime)
        {
            item.Member("actualPickupTimeMs"u8).Integer(pickupTime.Milliseconds);
        }

        item.Member("arrivalTimeMs"u8).Integer(request.ArrivalTime.Milliseconds);

        if (request.AssignedVehicleId is VehicleId vehicleId)
        {
            item.Member("assignedVehicleId"u8).String(vehicleId.Value);
        }

        item.Member("commitmentPolicyId"u8).String(request.CommitmentPolicyId);
        item.Member("destinationNodeId"u8).String(request.DestinationNodeId.Value);
        item.Member("earliestPickupMs"u8).Integer(request.EarliestPickup.Milliseconds);
        item.Member("latestPickupMs"u8).Integer(request.LatestPickup.Milliseconds);
        item.Member("lifecycle"u8).String(ToProtocolValue(request.Lifecycle));
        item.Member("maxRideTimeMs"u8).Integer(request.MaxRideTime.Milliseconds);
        item.Member("originNodeId"u8).String(request.OriginNodeId.Value);
        item.Member("partySize"u8).Integer(request.PartySize);
        item.Member("requestId"u8).String(request.Id.Value);
        item.Member("serviceClass"u8).String(request.ServiceClass);
        item.End();
    }

    // OnlineStateCanonicalizer.cs:54-78.
    internal static void WriteVehicle(CanonicalBuffer buffer, VehicleState vehicle)
    {
        var item = new CanonicalObject(buffer);
        WriteRequestSet(item.Member("acceptedRequestIds"u8), vehicle.AcceptedRequestIds);
        item.Member("capacity"u8).Integer(vehicle.Capacity);
        item.Member("lastObservedEpoch"u8).Integer(vehicle.LastObservedEpoch);
        item.Member("occupiedSeats"u8).Integer(vehicle.OccupiedSeats);
        WriteRequestSet(item.Member("onboardRequestIds"u8), vehicle.OnboardRequestIds);
        WritePosition(item.Member("position"u8), vehicle.Position);
        WriteRoute(item.Member("route"u8), vehicle.Route);
        item.Member("vehicleId"u8).String(vehicle.Id.Value);
        item.End();
    }

    // OnlineStateCanonicalizer.cs:81-112 (arcs sorted by from, then to node id, ordinal).
    internal static void WriteTravelTimes(CanonicalBuffer buffer, TravelTimeSnapshot travel)
    {
        var item = new CanonicalObject(buffer);
        item.Member("arcs"u8).Byte((byte)'[');
        var first = true;

        foreach (var arc in travel.TravelTimes.OrderBy(
                     value => value.Key.FromNodeId.Value,
                     StringComparer.Ordinal)
                 .ThenBy(
                     value => value.Key.ToNodeId.Value,
                     StringComparer.Ordinal))
        {
            if (!first)
            {
                buffer.Byte((byte)',');
            }

            first = false;
            var value = new CanonicalObject(buffer);
            value.Member("fromNodeId"u8).String(arc.Key.FromNodeId.Value);
            value.Member("toNodeId"u8).String(arc.Key.ToNodeId.Value);
            value.Member("travelTimeMs"u8).Integer(arc.Value.Milliseconds);
            value.End();
        }

        buffer.Byte((byte)']');
        item.Member("snapshotHash"u8).String(travel.SnapshotHash);
        item.Member("version"u8).Integer(travel.Version);
        item.End();
    }

    // OnlineStateCanonicalizer.WritePosition (182-208).
    private static void WritePosition(CanonicalBuffer buffer, VehiclePosition position)
    {
        var item = new CanonicalObject(buffer);

        switch (position)
        {
            case NodePosition node:
                item.Member("kind"u8).String("node");
                item.Member("nodeId"u8).String(node.NodeId.Value);
                break;
            case EdgeProgressPosition edge:
                item.Member("edgeId"u8).String(edge.EdgeId);
                item.Member("fromNodeId"u8).String(edge.FromNodeId.Value);
                item.Member("kind"u8).String("edgeProgress");
                item.Member("progressPermille"u8).Integer(edge.ProgressPermille);
                item.Member("toNodeId"u8).String(edge.ToNodeId.Value);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(position));
        }

        item.End();
    }

    // OnlineStateCanonicalizer.WriteRequestSet (210-226).
    private static void WriteRequestSet(CanonicalBuffer buffer, IEnumerable<RequestId> values)
    {
        buffer.Byte((byte)'[');
        var first = true;

        foreach (var requestId in values.OrderBy(value => value.Value, StringComparer.Ordinal))
        {
            if (!first)
            {
                buffer.Byte((byte)',');
            }

            first = false;
            buffer.String(requestId.Value);
        }

        buffer.Byte((byte)']');
    }

    // OnlineStateCanonicalizer.WriteRoute / WriteStops (228-276).
    private static void WriteRoute(CanonicalBuffer buffer, RoutePlan route)
    {
        var item = new CanonicalObject(buffer);
        item.Member("executedStopCount"u8).Integer(route.ExecutedStopCount);
        WriteStops(item.Member("frozenPrefix"u8), route.FrozenPrefix);
        WriteStops(item.Member("mutableSuffix"u8), route.MutableSuffix);
        item.Member("planVersion"u8).Integer(route.Version.Value);
        item.End();
    }

    private static void WriteStops(CanonicalBuffer buffer, IEnumerable<RouteStop> stops)
    {
        buffer.Byte((byte)'[');
        var first = true;

        foreach (var stop in stops)
        {
            if (!first)
            {
                buffer.Byte((byte)',');
            }

            first = false;
            var item = new CanonicalObject(buffer);
            item.Member("kind"u8).String(StopKind(stop.Kind));
            item.Member("nodeId"u8).String(stop.NodeId.Value);

            if (stop.RequestId is RequestId requestId)
            {
                item.Member("requestId"u8).String(requestId.Value);
            }

            item.Member("serviceDurationMs"u8).Integer(stop.ServiceDuration.Milliseconds);
            item.Member("stopId"u8).String(stop.StopId.Value);
            item.End();
        }

        buffer.Byte((byte)']');
    }

    // OnlineStateCanonicalizer.WritePlanPool (278-316). Not cached: absent unless multiple-plan mode.
    internal static void WritePlanPool(CanonicalBuffer buffer, VersionedPlanPool pool)
    {
        var item = new CanonicalObject(buffer);
        item.Member("distinguishedPlanId"u8).String(pool.DistinguishedPlanId);
        item.Member("plans"u8).Byte((byte)'[');
        var first = true;

        foreach (var plan in pool.Plans.OrderBy(value => value.PlanId, StringComparer.Ordinal))
        {
            if (!first)
            {
                buffer.Byte((byte)',');
            }

            first = false;
            var planItem = new CanonicalObject(buffer);
            planItem.Member("planId"u8).String(plan.PlanId);
            planItem.Member("sourceEpoch"u8).Integer(plan.SourceEpoch);
            planItem.Member("vehiclePlans"u8).Byte((byte)'[');
            var firstVehicle = true;

            foreach (var vehicle in plan.VehiclePlans.OrderBy(
                         value => value.VehicleId.Value,
                         StringComparer.Ordinal))
            {
                if (!firstVehicle)
                {
                    buffer.Byte((byte)',');
                }

                firstVehicle = false;
                var vehicleItem = new CanonicalObject(buffer);
                WriteRoute(vehicleItem.Member("route"u8), vehicle.Route);
                vehicleItem.Member("vehicleId"u8).String(vehicle.VehicleId.Value);
                vehicleItem.End();
            }

            buffer.Byte((byte)']');
            planItem.End();
        }

        buffer.Byte((byte)']');
        item.Member("sourceEpoch"u8).Integer(pool.SourceEpoch);
        item.Member("version"u8).Integer(pool.Version);
        item.End();
    }

    // OnlineStateCanonicalizer.WriteCommitmentLedger entry body (349-396).
    internal static void WriteLedgerEntry(CanonicalBuffer buffer, CommitmentLedgerEntry entry)
    {
        var item = new CanonicalObject(buffer);
        WriteVector(item.Member("budgetAfter"u8), entry.BudgetAfter);

        if (entry.BudgetBasis is CommitmentBudgetBasis budgetBasis)
        {
            item.Member("budgetBasis"u8).String(
                budgetBasis == CommitmentBudgetBasis.DecisionInduced
                    ? "decisionInduced"
                    : "customerVisible");
        }

        WriteVector(item.Member("budgetBefore"u8), entry.BudgetBefore);
        WriteDeltas(item.Member("deltas"u8), entry.Deltas);
        WritePromiseProjection(item.Member("exogenousProjection"u8), entry.ExogenousProjection);
        item.Member("kind"u8).String(
            entry.Kind == CommitmentLedgerEntryKind.InitialPromise
                ? "initialPromise"
                : "revision");

        if (entry.PreviousPromise is not null)
        {
            WritePublishedPromise(item.Member("previousPromise"u8), entry.PreviousPromise);
        }

        item.Member("publicationId"u8).String(entry.PublicationId);
        WritePublishedPromise(item.Member("publishedPromise"u8), entry.PublishedPromise);
        item.Member("reasonCode"u8).String(entry.ReasonCode);
        item.Member("sourceEventSeq"u8).Integer(entry.SourceEventSequence);
        item.End();
    }

    private static void WriteDeltas(CanonicalBuffer buffer, ThreeWayPromiseDelta deltas)
    {
        var item = new CanonicalObject(buffer);
        WriteVector(item.Member("decisionInduced"u8), deltas.DecisionInduced);
        WriteVector(item.Member("exogenous"u8), deltas.Exogenous);
        WriteVector(item.Member("visible"u8), deltas.Visible);
        item.End();
    }

    // OnlineStateCanonicalizer.WriteIncidentLedger (406-553).
    private static void WriteIncidentLedger(
        CanonicalSegmentBuilder builder,
        OperationalIncidentLedger ledger,
        OnlineStateCanonicalCache? cache)
    {
        var glue = builder.Glue;
        var item = new CanonicalObject(glue);
        item.Member("breaches"u8).Byte((byte)'[');
        var first = true;

        foreach (var breach in ledger.Breaches)
        {
            if (!first)
            {
                glue.Byte((byte)',');
            }

            first = false;
            builder.Fragment(Cached(cache?.Breaches, breach, cache, WriteBreach));
        }

        glue.Byte((byte)']');
        item.Member("incidents"u8).Byte((byte)'[');
        first = true;

        foreach (var incident in ledger.Incidents.Values.OrderBy(
                     value => value.Id.Value,
                     StringComparer.Ordinal))
        {
            if (!first)
            {
                glue.Byte((byte)',');
            }

            first = false;
            builder.Fragment(Cached(cache?.Incidents, incident, cache, WriteIncident));
        }

        glue.Byte((byte)']');

        if (ledger.LatePickups.Count != 0)
        {
            item.Member("latePickups"u8).Byte((byte)'[');
            first = true;

            foreach (var late in ledger.LatePickups)
            {
                if (!first)
                {
                    glue.Byte((byte)',');
                }

                first = false;
                var lateItem = new CanonicalObject(glue);
                lateItem.Member("actualPickupMs"u8).Integer(late.ActualPickup.Milliseconds);
                lateItem.Member("latestPickupMs"u8).Integer(late.LatestPickup.Milliseconds);
                lateItem.Member("recordedEpoch"u8).Integer(late.RecordedEpoch);
                lateItem.Member("requestId"u8).String(late.RequestId.Value);
                lateItem.Member("sourceEventSeq"u8).Integer(late.SourceEventSequence);
                lateItem.Member("vehicleId"u8).String(late.VehicleId.Value);
                lateItem.End();
            }

            glue.Byte((byte)']');
        }

        item.End();
    }

    internal static void WriteIncident(CanonicalBuffer buffer, OperationalIncident incident)
    {
        var item = new CanonicalObject(buffer);
        WriteIdentifiers(
            item.Member("affectedRequestIds"u8),
            incident.AffectedRequestIds.Select(value => value.Value));
        WriteIdentifiers(
            item.Member("affectedVehicleIds"u8),
            incident.AffectedVehicleIds.Select(value => value.Value));
        item.Member("incidentId"u8).String(incident.Id.Value);
        item.Member("openedAtMs"u8).Integer(incident.OpenedAt.Milliseconds);
        item.Member("openedEventSeq"u8).Integer(incident.OpenedEventSequence);
        item.Member("reasonCode"u8).String(incident.ReasonCode);

        if (incident.ResolvedEventSequence is long resolvedSequence)
        {
            item.Member("resolvedAtMs"u8).Integer(incident.ResolvedAt!.Value.Milliseconds);
            item.Member("resolvedEventSeq"u8).Integer(resolvedSequence);
        }

        item.End();
    }

    internal static void WriteBreach(CanonicalBuffer buffer, CommitmentBreachRecord breach)
    {
        var item = new CanonicalObject(buffer);
        WriteVector(item.Member("attemptedBudgetAfter"u8), breach.AttemptedBudgetAfter);
        item.Member("breachId"u8).String(breach.BreachId);
        WriteVector(item.Member("budgetBefore"u8), breach.BudgetBefore);
        WriteDeltas(item.Member("deltas"u8), breach.Deltas);
        WritePromiseProjection(item.Member("exogenousProjection"u8), breach.ExogenousProjection);

        switch (breach.Kind)
        {
            case CommitmentBreachKind.OperationalIncident:
                item.Member("incidentId"u8).String(breach.IncidentId!.Value.Value);
                break;
            case CommitmentBreachKind.ExogenousServiceQuality:
                item.Member("kind"u8).String("exogenousServiceQuality");
                break;
            case CommitmentBreachKind.ForcedReference:
                item.Member("kind"u8).String("forcedReference");
                break;
            case CommitmentBreachKind.ForcedNoWorse:
                item.Member("kind"u8).String("forcedNoWorse");
                break;
            default:
                throw new InvalidOperationException("Unknown commitment breach kind.");
        }

        item.Member("normalOperation"u8).Boolean(breach.NormalOperation);
        WritePublishedPromise(item.Member("previousPromise"u8), breach.PreviousPromise);
        item.Member("recordedAtMs"u8).Integer(breach.RecordedAt.Milliseconds);
        item.Member("recordedEpoch"u8).Integer(breach.RecordedEpoch);
        item.Member("requestId"u8).String(breach.RequestId.Value);
        WritePromiseProjection(item.Member("safetyProjection"u8), breach.SafetyProjection);

        if (breach.Kind == CommitmentBreachKind.ExogenousServiceQuality)
        {
            var witnesses = item.Member("serviceQualityWitnesses"u8);
            witnesses.Byte((byte)'[');
            var first = true;

            foreach (var witness in breach.ServiceQualityWitnesses)
            {
                if (!first)
                {
                    witnesses.Byte((byte)',');
                }

                first = false;
                var witnessItem = new CanonicalObject(witnesses);
                witnessItem.Member("code"u8).String(witness.Code);
                witnessItem.Member("contractualMilliseconds"u8).Integer(witness.ContractualMilliseconds);
                witnessItem.Member("dimension"u8).String(witness.Dimension);
                witnessItem.Member("exogenousMilliseconds"u8).Integer(witness.ExogenousMilliseconds);
                witnessItem.Member("requestId"u8).String(witness.RequestId.Value);
                witnessItem.End();
            }

            witnesses.Byte((byte)']');
        }

        item.Member("sourceEventSeq"u8).Integer(breach.SourceEventSequence);
        WriteIdentifiers(item.Member("witnessCodes"u8), breach.WitnessCodes);
        item.End();
    }

    // OnlineStateCanonicalizer.WritePublishedPromise (555-566).
    private static void WritePublishedPromise(CanonicalBuffer buffer, PublishedPromise promise)
    {
        var item = new CanonicalObject(buffer);
        WritePromiseProjection(item.Member("projection"u8), promise.Projection);
        item.Member("publishedAtMs"u8).Integer(promise.PublishedAt.Milliseconds);
        item.Member("publishedEpoch"u8).Integer(promise.PublishedEpoch);
        item.Member("version"u8).Integer(promise.Version.Value);
        item.End();
    }

    // OnlineStateCanonicalizer.WritePromiseProjection (568-608).
    private static void WritePromiseProjection(CanonicalBuffer buffer, PromiseProjection promise)
    {
        var item = new CanonicalObject(buffer);
        item.Member("dropEtaMs"u8).Integer(promise.DropEta.Milliseconds);
        item.Member("dropNodeId"u8).String(promise.DropNodeId.Value);
        item.Member("dropStopId"u8).String(promise.DropStopId.Value);
        item.Member("pickupEtaMs"u8).Integer(promise.PickupEta.Milliseconds);
        item.Member("pickupNodeId"u8).String(promise.PickupNodeId.Value);
        item.Member("pickupStopId"u8).String(promise.PickupStopId.Value);
        item.Member("requestId"u8).String(promise.RequestId.Value);
        var order = item.Member("serviceOrder"u8);
        order.Byte((byte)'[');
        var first = true;

        foreach (var token in promise.ServiceOrder)
        {
            if (!first)
            {
                order.Byte((byte)',');
            }

            first = false;
            var tokenItem = new CanonicalObject(order);
            tokenItem.Member("kind"u8).String(StopKind(token.Kind));

            if (token.RequestId is RequestId requestId)
            {
                tokenItem.Member("requestId"u8).String(requestId.Value);
            }

            tokenItem.Member("stopId"u8).String(token.StopId.Value);
            tokenItem.End();
        }

        order.Byte((byte)']');
        item.Member("vehicleId"u8).String(promise.VehicleId.Value);
        item.End();
    }

    // OnlineStateCanonicalizer.WriteVector (610-638).
    private static void WriteVector(CanonicalBuffer buffer, CommitmentVector value)
    {
        var item = new CanonicalObject(buffer);
        item.Member("dropEtaTotalMs"u8).Integer(value.DropEtaTotalMs);
        item.Member("dropStopRelocationMm"u8).Integer(value.DropStopRelocationMm);
        item.Member("dropStopSwitchCount"u8).Integer(value.DropStopSwitchCount);
        item.Member("incumbentOrderInversionCount"u8).Integer(value.IncumbentOrderInversionCount);
        item.Member("materialEtaRevisionCount"u8).Integer(value.MaterialEtaRevisionCount);
        item.Member("pickupEtaTotalMs"u8).Integer(value.PickupEtaTotalMs);
        item.Member("pickupStopRelocationMm"u8).Integer(value.PickupStopRelocationMm);
        item.Member("pickupStopSwitchCount"u8).Integer(value.PickupStopSwitchCount);
        item.Member("prePickupInsertedStopCount"u8).Integer(value.PrePickupInsertedStopCount);
        item.Member("vehicleSwitchCount"u8).Integer(value.VehicleSwitchCount);
        item.End();
    }

    // OnlineStateCanonicalizer.WriteIdentifiers (640-654).
    private static void WriteIdentifiers(CanonicalBuffer buffer, IEnumerable<string> values)
    {
        buffer.Byte((byte)'[');
        var first = true;

        foreach (var value in values.Order(StringComparer.Ordinal))
        {
            if (!first)
            {
                buffer.Byte((byte)',');
            }

            first = false;
            buffer.String(value);
        }

        buffer.Byte((byte)']');
    }

    private static string StopKind(RideBound.Domain.Routes.RouteStopKind kind) =>
        kind switch
        {
            RideBound.Domain.Routes.RouteStopKind.Waypoint => "waypoint",
            RideBound.Domain.Routes.RouteStopKind.Pickup => "pickup",
            RideBound.Domain.Routes.RouteStopKind.DropOff => "dropOff",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    private static string ToProtocolValue(RequestLifecycle lifecycle) =>
        lifecycle switch
        {
            RequestLifecycle.Pending => "pending",
            RequestLifecycle.Accepted => "accepted",
            RequestLifecycle.WaitingPickup => "waitingPickup",
            RequestLifecycle.Onboard => "onboard",
            RequestLifecycle.Completed => "completed",
            RequestLifecycle.Rejected => "rejected",
            RequestLifecycle.CancelledBeforeAcceptance => "cancelledBeforeAcceptance",
            RequestLifecycle.CancelledAfterAcceptance => "cancelledAfterAcceptance",
            _ => throw new ArgumentOutOfRangeException(nameof(lifecycle)),
        };
}
