using RideBound.Application.Events;
using RideBound.Application.Travel;
using RideBound.Contracts.Protocol;
using RideBound.Domain.Common;
using RideBound.Domain.Incidents;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Vehicles;
using ContractRouteStopKind = RideBound.Contracts.Protocol.RouteStopKind;
using DomainRouteStopKind = RideBound.Domain.Routes.RouteStopKind;

namespace RideBound.Runner.Online;

public sealed record OnlineEventMappingWitness(
    string Code,
    string Message,
    int? EventIndex = null,
    long? EventSequence = null,
    string? Field = null);

public sealed record OnlineEventMappingResult
{
    private OnlineEventMappingResult(
        InternalEventBatch? batch,
        OnlineEventMappingWitness? witness)
    {
        Batch = batch;
        Witness = witness;
    }

    public bool IsSuccess => Batch is not null;

    public InternalEventBatch? Batch { get; }

    public OnlineEventMappingWitness? Witness { get; }

    public static OnlineEventMappingResult Success(InternalEventBatch batch) =>
        new(batch, null);

    public static OnlineEventMappingResult Failure(
        OnlineEventMappingWitness witness) =>
        new(null, witness);
}

public sealed class OnlineEventMapper
{
    public OnlineEventMappingResult Map(ProtocolEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (envelope.MessageType.Value != "eventBatch"
            || envelope.RunId is null
            || envelope.ScenarioId is null
            || envelope.EpochId is null
            || envelope.SimTime is null)
        {
            return Failure(
                "INVALID_EVENT_BATCH_ENVELOPE",
                "Online mapper requires a complete eventBatch envelope.");
        }

        var payload = EventBatchPayloadCodec.Decode(envelope.Payload);

        if (!payload.IsSuccess)
        {
            return Failure(
                "INVALID_EVENT_PAYLOAD",
                payload.Error!.Message,
                field: payload.Error.Field);
        }

        var events = new List<OnlineEvent>(payload.Value!.Events.Count);
        var simTime = new SimTime(envelope.SimTime.Value.Value);
        var epoch = envelope.EpochId.Value.Value;

        for (var index = 0; index < payload.Value.Events.Count; index++)
        {
            var protocolEvent = payload.Value.Events[index];
            var mapped = MapEvent(protocolEvent, simTime, epoch);

            if (mapped.Event is null)
            {
                return Failure(
                    mapped.Witness!.Code,
                    mapped.Witness.Message,
                    index,
                    protocolEvent.EventSequence.Value,
                    mapped.Witness.Field);
            }

            events.Add(mapped.Event);
        }

        return OnlineEventMappingResult.Success(
            new InternalEventBatch(
                new RunIdentifier(envelope.RunId.Value),
                new ScenarioIdentifier(envelope.ScenarioId.Value),
                epoch,
                simTime,
                events));
    }

    private static EventMappingResult MapEvent(
        ProtocolEvent protocolEvent,
        SimTime simTime,
        long epoch)
    {
        try
        {
            var sequence = protocolEvent.EventSequence.Value;

            return protocolEvent.Payload switch
            {
                RequestArrivedEventPayload arrived =>
                    MapRequest(sequence, simTime, arrived.Request),
                RequestReferenceEventPayload reference =>
                    MapRequestReference(
                        protocolEvent.EventType,
                        sequence,
                        simTime,
                        reference),
                VehicleAdvancedEventPayload advanced =>
                    MapVehicle(sequence, simTime, epoch, advanced.Vehicle),
                VehicleReachedStopEventPayload reached =>
                    EventMappingResult.Success(
                        new VehicleReachedStop(
                            sequence,
                            simTime,
                            new VehicleId(reached.VehicleId),
                            new StopId(reached.StopId),
                            new PlanVersion(reached.PlanVersion),
                            new NodePosition(
                                new NodeId(reached.Position.NodeId)))),
                PassengerEventPayload passenger =>
                    MapPassenger(
                        protocolEvent.EventType,
                        sequence,
                        simTime,
                        passenger),
                TravelTimesUpdatedEventPayload travel =>
                    MapTravel(sequence, simTime, travel.Snapshot),
                TimerTickEventPayload =>
                    EventMappingResult.Success(new TimerTick(sequence, simTime)),
                IncidentOpenedEventPayload opened =>
                    EventMappingResult.Success(
                        new IncidentOpened(
                            sequence,
                            simTime,
                            new IncidentId(opened.IncidentId),
                            opened.ReasonCode,
                            opened.VehicleIds
                                .Select(value => new VehicleId(value))
                                .OrderBy(value => value.Value, StringComparer.Ordinal)
                                .ToArray())),
                IncidentResolvedEventPayload resolved =>
                    EventMappingResult.Success(
                        new IncidentResolved(
                            sequence,
                            simTime,
                            new IncidentId(resolved.IncidentId))),
                _ => EventMappingResult.Failure(
                    "UNSUPPORTED_EVENT_TYPE",
                    $"Payload '{protocolEvent.Payload.GetType().Name}' is unsupported."),
            };
        }
        catch (ArgumentException error)
        {
            return EventMappingResult.Failure(
                "CONTRACT_DOMAIN_MAPPING_FAILED",
                error.Message);
        }
        catch (OverflowException error)
        {
            return EventMappingResult.Failure(
                "CONTRACT_DOMAIN_MAPPING_FAILED",
                error.Message);
        }
    }

    private static EventMappingResult MapRequest(
        long sequence,
        SimTime simTime,
        RequestContract contract)
    {
        var request = RideRequest.CreatePending(
            new RequestId(contract.RequestId),
            new SimTime(contract.ArrivalTimeMs),
            new NodeId(contract.OriginNodeId),
            new NodeId(contract.DestinationNodeId),
            new SimTime(contract.EarliestPickupMs),
            new SimTime(contract.LatestPickupMs),
            new Duration(contract.MaxRideTimeMs),
            contract.PartySize,
            contract.ServiceClass,
            contract.CommitmentPolicyId);

        return request.IsSuccess
            ? EventMappingResult.Success(
                new RequestArrived(sequence, simTime, request.Value!))
            : EventMappingResult.Failure(
                request.Failure!.Code,
                request.Failure.Message);
    }

    private static EventMappingResult MapRequestReference(
        EventType eventType,
        long sequence,
        SimTime simTime,
        RequestReferenceEventPayload reference)
    {
        var requestId = new RequestId(reference.RequestId);
        OnlineEvent? mapped = eventType switch
        {
            EventType.BookingConfirmed =>
                new BookingConfirmed(sequence, simTime, requestId),
            EventType.OfferDeclined =>
                new OfferDeclined(sequence, simTime, requestId),
            EventType.RequestCancelledBeforeAcceptance =>
                new RequestCancelledBeforeAcceptance(
                    sequence,
                    simTime,
                    requestId),
            EventType.RequestCancelledAfterAcceptance =>
                new RequestCancelledAfterAcceptance(
                    sequence,
                    simTime,
                    requestId),
            _ => null,
        };

        return mapped is not null
            ? EventMappingResult.Success(mapped)
            : EventMappingResult.Failure(
                "EVENT_PAYLOAD_TYPE_MISMATCH",
                "Request reference payload does not match the event type.");
    }

    private static EventMappingResult MapVehicle(
        long sequence,
        SimTime simTime,
        long epoch,
        VehicleSnapshotContract contract)
    {
        var route = MapRoute(contract.Route);

        if (!route.IsSuccess)
        {
            return EventMappingResult.Failure(
                route.Failure!.Code,
                route.Failure.Message);
        }

        var vehicle = VehicleState.Create(
            new VehicleId(contract.VehicleId),
            contract.Capacity,
            contract.OccupiedSeats,
            MapPosition(contract.Position),
            contract.OnboardRequestIds.Select(value => new RequestId(value)),
            contract.AcceptedRequestIds.Select(value => new RequestId(value)),
            route.Value!,
            epoch);

        return vehicle.IsSuccess
            ? EventMappingResult.Success(
                new VehicleAdvanced(sequence, simTime, vehicle.Value!))
            : EventMappingResult.Failure(
                vehicle.Failure!.Code,
                vehicle.Failure.Message);
    }

    private static EventMappingResult MapPassenger(
        EventType eventType,
        long sequence,
        SimTime simTime,
        PassengerEventPayload passenger)
    {
        var vehicleId = new VehicleId(passenger.VehicleId);
        var requestId = new RequestId(passenger.RequestId);
        var planVersion = new PlanVersion(passenger.PlanVersion);

        return eventType switch
        {
            EventType.PassengerBoarded => EventMappingResult.Success(
                new PassengerBoarded(
                    sequence,
                    simTime,
                    vehicleId,
                    requestId,
                    planVersion)),
            EventType.PassengerAlighted => EventMappingResult.Success(
                new PassengerAlighted(
                    sequence,
                    simTime,
                    vehicleId,
                    requestId,
                    planVersion)),
            _ => EventMappingResult.Failure(
                "EVENT_PAYLOAD_TYPE_MISMATCH",
                "Passenger payload does not match the event type."),
        };
    }

    private static EventMappingResult MapTravel(
        long sequence,
        SimTime simTime,
        TravelTimeSnapshotContract contract)
    {
        var snapshot = TravelTimeSnapshot.Create(
            contract.Version,
            contract.SnapshotHash.Value,
            contract.Arcs.Select(
                arc => new KeyValuePair<TravelArc, Duration>(
                    new TravelArc(
                        new NodeId(arc.FromNodeId),
                        new NodeId(arc.ToNodeId)),
                    new Duration(arc.TravelTimeMs))));

        return snapshot.IsSuccess
            ? EventMappingResult.Success(
                new TravelTimesUpdated(sequence, simTime, snapshot.Value!))
            : EventMappingResult.Failure(
                snapshot.Failure!.Code,
                snapshot.Failure.Message);
    }

    private static DomainResult<RoutePlan> MapRoute(RoutePlanContract contract)
    {
        return RoutePlan.Create(
            new PlanVersion(contract.PlanVersion),
            contract.ExecutedStopCount,
            contract.FrozenPrefix.Select(MapStop),
            contract.MutableSuffix.Select(MapStop));
    }

    private static RouteStop MapStop(RouteStopContract contract)
    {
        var kind = contract.Kind switch
        {
            ContractRouteStopKind.Waypoint => DomainRouteStopKind.Waypoint,
            ContractRouteStopKind.Pickup => DomainRouteStopKind.Pickup,
            ContractRouteStopKind.DropOff => DomainRouteStopKind.DropOff,
            _ => throw new ArgumentOutOfRangeException(nameof(contract)),
        };

        return new RouteStop(
            new StopId(contract.StopId),
            new NodeId(contract.NodeId),
            kind,
            contract.RequestId is null ? null : new RequestId(contract.RequestId),
            new Duration(contract.ServiceDurationMs));
    }

    private static VehiclePosition MapPosition(PositionContract contract) =>
        contract switch
        {
            NodePositionContract node =>
                new NodePosition(new NodeId(node.NodeId)),
            EdgeProgressPositionContract edge =>
                new EdgeProgressPosition(
                    new NodeId(edge.FromNodeId),
                    new NodeId(edge.ToNodeId),
                    edge.EdgeId,
                    edge.ProgressPermille),
            _ => throw new ArgumentOutOfRangeException(nameof(contract)),
        };

    private static OnlineEventMappingResult Failure(
        string code,
        string message,
        int? index = null,
        long? sequence = null,
        string? field = null) =>
        OnlineEventMappingResult.Failure(
            new OnlineEventMappingWitness(
                code,
                message,
                index,
                sequence,
                field));

    private sealed record EventMappingResult(
        OnlineEvent? Event,
        OnlineEventMappingWitness? Witness)
    {
        public static EventMappingResult Success(OnlineEvent onlineEvent) =>
            new(onlineEvent, null);

        public static EventMappingResult Failure(string code, string message) =>
            new(null, new OnlineEventMappingWitness(code, message));
    }
}
