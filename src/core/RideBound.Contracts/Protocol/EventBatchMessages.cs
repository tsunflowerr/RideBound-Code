using System.Text.Json;

namespace RideBound.Contracts.Protocol;

public enum EventType
{
    RequestArrived,
    BookingConfirmed,
    OfferDeclined,
    RequestCancelledBeforeAcceptance,
    RequestCancelledAfterAcceptance,
    VehicleAdvanced,
    VehicleReachedStop,
    PassengerBoarded,
    PassengerAlighted,
    TravelTimesUpdated,
    TimerTick,
    IncidentOpened,
    IncidentResolved,
}

public sealed record ProtocolEvent(
    EventSequence EventSequence,
    EventType EventType,
    ProtocolEventPayload Payload);

public sealed record EventBatchPayload(IReadOnlyList<ProtocolEvent> Events);

public sealed record DecisionAppliedPayload(Sha256Hex DecisionHash);

public static class EventTypeVocabulary
{
    private static readonly IReadOnlyDictionary<string, EventType> ByWireValue =
        Enum.GetValues<EventType>().ToDictionary(
            ToProtocolValue,
            value => value,
            StringComparer.Ordinal);

    public static bool TryParse(string? value, out EventType eventType) =>
        ByWireValue.TryGetValue(value ?? string.Empty, out eventType);

    public static string ToProtocolValue(EventType eventType) =>
        eventType switch
        {
            EventType.RequestArrived => "requestArrived",
            EventType.BookingConfirmed => "bookingConfirmed",
            EventType.OfferDeclined => "offerDeclined",
            EventType.RequestCancelledBeforeAcceptance =>
                "requestCancelledBeforeAcceptance",
            EventType.RequestCancelledAfterAcceptance =>
                "requestCancelledAfterAcceptance",
            EventType.VehicleAdvanced => "vehicleAdvanced",
            EventType.VehicleReachedStop => "vehicleReachedStop",
            EventType.PassengerBoarded => "passengerBoarded",
            EventType.PassengerAlighted => "passengerAlighted",
            EventType.TravelTimesUpdated => "travelTimesUpdated",
            EventType.TimerTick => "timerTick",
            EventType.IncidentOpened => "incidentOpened",
            EventType.IncidentResolved => "incidentResolved",
            _ => throw new ArgumentOutOfRangeException(nameof(eventType)),
        };
}

public static class EventBatchPayloadCodec
{
    private static readonly IReadOnlySet<string> Fields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "events",
        };

    private static readonly IReadOnlySet<string> EventFields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "eventSeq",
            "eventType",
            "payload",
        };

    public static ProtocolPayloadDecodeResult<EventBatchPayload> Decode(
        JsonElement payload)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            payload,
            "$.payload",
            Fields);

        if (objectError is not null)
        {
            return ProtocolPayloadDecodeResult<EventBatchPayload>.Failure(objectError);
        }

        var eventsProperty = ProtocolPayloadReader.ReadRequiredProperty(
            payload,
            "$.payload",
            "events");

        if (!eventsProperty.IsSuccess)
        {
            return ProtocolPayloadDecodeResult<EventBatchPayload>.Failure(
                eventsProperty.Error!);
        }

        if (eventsProperty.Value.ValueKind != JsonValueKind.Array)
        {
            return Failure(
                ProtocolPayloadErrorCode.InvalidFieldType,
                "$.payload.events",
                "Field '$.payload.events' must be an array.");
        }

        var events = new List<ProtocolEvent>();
        var index = 0;

        foreach (var element in eventsProperty.Value.EnumerateArray())
        {
            var path = $"$.payload.events[{index}]";
            var eventObjectError = ProtocolPayloadReader.ValidateObject(
                element,
                path,
                EventFields);

            if (eventObjectError is not null)
            {
                return ProtocolPayloadDecodeResult<EventBatchPayload>.Failure(
                    eventObjectError);
            }

            var sequence = ProtocolPayloadReader.ReadRequiredInteger(
                element,
                path,
                "eventSeq",
                minimum: 1);
            var eventTypeText = ProtocolPayloadReader.ReadRequiredString(
                element,
                path,
                "eventType");
            var eventPayload = ProtocolPayloadReader.ReadRequiredProperty(
                element,
                path,
                "payload");
            var error = HelloPayloadCodec.FirstError(
                sequence.Error,
                eventTypeText.Error,
                eventPayload.Error);

            if (error is not null)
            {
                return ProtocolPayloadDecodeResult<EventBatchPayload>.Failure(error);
            }

            if (!EventSequence.TryCreate(sequence.Value, out var eventSequence))
            {
                return Failure(
                    ProtocolPayloadErrorCode.ValueOutOfRange,
                    $"{path}.eventSeq",
                    $"Field '{path}.eventSeq' is outside the event sequence range.");
            }

            if (!EventTypeVocabulary.TryParse(eventTypeText.Value, out var eventType))
            {
                return Failure(
                    ProtocolPayloadErrorCode.InvalidValue,
                    $"{path}.eventType",
                    $"Event type '{eventTypeText.Value}' is unknown for protocol v1.");
            }

            var typedPayload = ProtocolEventPayloadCodec.Decode(
                eventType,
                eventPayload.Value,
                $"{path}.payload");

            if (!typedPayload.IsSuccess)
            {
                return ProtocolPayloadDecodeResult<EventBatchPayload>.Failure(
                    typedPayload.Error!);
            }

            events.Add(new ProtocolEvent(eventSequence, eventType, typedPayload.Value!));
            index++;
        }

        if (events.Count == 0)
        {
            return Failure(
                ProtocolPayloadErrorCode.InvalidValue,
                "$.payload.events",
                "An event batch must contain at least one event.");
        }

        return ProtocolPayloadDecodeResult<EventBatchPayload>.Success(
            new EventBatchPayload(events));
    }

    public static byte[] Encode(EventBatchPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (payload.Events.Count == 0)
        {
            throw new ArgumentException(
                "An event batch must contain at least one event.",
                nameof(payload));
        }

        return ProtocolPayloadReader.Write(
            writer =>
            {
                writer.WriteStartObject();
                writer.WritePropertyName("events");
                writer.WriteStartArray();

                foreach (var protocolEvent in payload.Events)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber(
                        "eventSeq",
                        protocolEvent.EventSequence.Value);
                    writer.WriteString(
                        "eventType",
                        EventTypeVocabulary.ToProtocolValue(protocolEvent.EventType));
                    writer.WritePropertyName("payload");
                    ProtocolEventPayloadCodec.Write(
                        writer,
                        protocolEvent.EventType,
                        protocolEvent.Payload);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            });
    }

    private static ProtocolPayloadDecodeResult<EventBatchPayload> Failure(
        ProtocolPayloadErrorCode code,
        string field,
        string message) =>
        ProtocolPayloadDecodeResult<EventBatchPayload>.Failure(
            new ProtocolPayloadError(code, field, message));
}

internal static class ProtocolEventPayloadCodec
{
    private static readonly IReadOnlySet<string> RequestWrapperFields =
        Fields("request");

    private static readonly IReadOnlySet<string> RequestReferenceFields =
        Fields("requestId");

    private static readonly IReadOnlySet<string> VehicleWrapperFields =
        Fields("vehicle");

    private static readonly IReadOnlySet<string> VehicleReachedStopFields =
        Fields("vehicleId", "stopId", "planVersion", "position");

    private static readonly IReadOnlySet<string> PassengerFields =
        Fields("vehicleId", "requestId", "planVersion");

    private static readonly IReadOnlySet<string> TravelWrapperFields =
        Fields("snapshot");

    private static readonly IReadOnlySet<string> IncidentOpenedFields =
        Fields("incidentId", "reasonCode", "vehicleIds");

    private static readonly IReadOnlySet<string> IncidentResolvedFields =
        Fields("incidentId");

    private static readonly IReadOnlySet<string> EmptyFields =
        Fields();

    public static ProtocolPayloadDecodeResult<ProtocolEventPayload> Decode(
        EventType eventType,
        JsonElement payload,
        string path)
    {
        return eventType switch
        {
            EventType.RequestArrived => DecodeRequestArrived(payload, path),
            EventType.BookingConfirmed
                or EventType.OfferDeclined
                or EventType.RequestCancelledBeforeAcceptance
                or EventType.RequestCancelledAfterAcceptance =>
                DecodeRequestReference(payload, path),
            EventType.VehicleAdvanced => DecodeVehicleAdvanced(payload, path),
            EventType.VehicleReachedStop => DecodeVehicleReachedStop(payload, path),
            EventType.PassengerBoarded or EventType.PassengerAlighted =>
                DecodePassenger(payload, path),
            EventType.TravelTimesUpdated => DecodeTravelTimes(payload, path),
            EventType.TimerTick => DecodeTimerTick(payload, path),
            EventType.IncidentOpened => DecodeIncidentOpened(payload, path),
            EventType.IncidentResolved => DecodeIncidentResolved(payload, path),
            _ => Failure(
                ProtocolPayloadErrorCode.InvalidValue,
                path,
                "Event type has no protocol v1 payload codec."),
        };
    }

    public static void Write(
        Utf8JsonWriter writer,
        EventType eventType,
        ProtocolEventPayload payload)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(payload);

        switch (eventType, payload)
        {
            case (EventType.RequestArrived, RequestArrivedEventPayload arrived):
                writer.WriteStartObject();
                writer.WritePropertyName("request");
                OnlineContractCodec.WriteRequest(writer, arrived.Request);
                writer.WriteEndObject();
                break;
            case (
                EventType.BookingConfirmed
                    or EventType.OfferDeclined
                    or EventType.RequestCancelledBeforeAcceptance
                    or EventType.RequestCancelledAfterAcceptance,
                RequestReferenceEventPayload reference):
                RequireIdentifier(reference.RequestId, nameof(reference.RequestId));
                writer.WriteStartObject();
                writer.WriteString("requestId", reference.RequestId);
                writer.WriteEndObject();
                break;
            case (EventType.VehicleAdvanced, VehicleAdvancedEventPayload advanced):
                writer.WriteStartObject();
                writer.WritePropertyName("vehicle");
                OnlineContractCodec.WriteVehicle(writer, advanced.Vehicle);
                writer.WriteEndObject();
                break;
            case (
                EventType.VehicleReachedStop,
                VehicleReachedStopEventPayload reached):
                RequireIdentifier(reached.VehicleId, nameof(reached.VehicleId));
                RequireIdentifier(reached.StopId, nameof(reached.StopId));
                RequireCanonical(reached.PlanVersion, nameof(reached.PlanVersion));
                writer.WriteStartObject();
                writer.WriteString("vehicleId", reached.VehicleId);
                writer.WriteString("stopId", reached.StopId);
                writer.WriteNumber("planVersion", reached.PlanVersion);
                writer.WritePropertyName("position");
                OnlineContractCodec.WritePosition(writer, reached.Position);
                writer.WriteEndObject();
                break;
            case (
                EventType.PassengerBoarded or EventType.PassengerAlighted,
                PassengerEventPayload passenger):
                RequireIdentifier(passenger.VehicleId, nameof(passenger.VehicleId));
                RequireIdentifier(passenger.RequestId, nameof(passenger.RequestId));
                RequireCanonical(passenger.PlanVersion, nameof(passenger.PlanVersion));
                writer.WriteStartObject();
                writer.WriteString("vehicleId", passenger.VehicleId);
                writer.WriteString("requestId", passenger.RequestId);
                writer.WriteNumber("planVersion", passenger.PlanVersion);
                writer.WriteEndObject();
                break;
            case (
                EventType.TravelTimesUpdated,
                TravelTimesUpdatedEventPayload travel):
                writer.WriteStartObject();
                writer.WritePropertyName("snapshot");
                OnlineContractCodec.WriteTravelSnapshot(writer, travel.Snapshot);
                writer.WriteEndObject();
                break;
            case (EventType.TimerTick, TimerTickEventPayload):
                writer.WriteStartObject();
                writer.WriteEndObject();
                break;
            case (EventType.IncidentOpened, IncidentOpenedEventPayload incident):
                RequireIdentifier(incident.IncidentId, nameof(incident.IncidentId));
                RequireIdentifier(incident.ReasonCode, nameof(incident.ReasonCode));
                var vehicleIds = NormalizeSet(
                    incident.VehicleIds,
                    nameof(incident.VehicleIds));
                writer.WriteStartObject();
                writer.WriteString("incidentId", incident.IncidentId);
                writer.WriteString("reasonCode", incident.ReasonCode);
                writer.WritePropertyName("vehicleIds");
                writer.WriteStartArray();

                foreach (var vehicleId in vehicleIds)
                {
                    writer.WriteStringValue(vehicleId);
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
                break;
            case (
                EventType.IncidentResolved,
                IncidentResolvedEventPayload resolved):
                RequireIdentifier(resolved.IncidentId, nameof(resolved.IncidentId));
                writer.WriteStartObject();
                writer.WriteString("incidentId", resolved.IncidentId);
                writer.WriteEndObject();
                break;
            default:
                throw new ArgumentException(
                    $"Payload type '{payload.GetType().Name}' does not match event " +
                    $"type '{EventTypeVocabulary.ToProtocolValue(eventType)}'.",
                    nameof(payload));
        }
    }

    private static ProtocolPayloadDecodeResult<ProtocolEventPayload>
        DecodeRequestArrived(JsonElement payload, string path)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            payload,
            path,
            RequestWrapperFields);
        var requestElement = ProtocolPayloadReader.ReadRequiredProperty(
            payload,
            path,
            "request");
        var error = HelloPayloadCodec.FirstError(objectError, requestElement.Error);

        if (error is not null)
        {
            return ProtocolPayloadDecodeResult<ProtocolEventPayload>.Failure(error);
        }

        var request = OnlineContractCodec.ReadRequest(
            requestElement.Value,
            $"{path}.request");
        return From(request, value => new RequestArrivedEventPayload(value));
    }

    private static ProtocolPayloadDecodeResult<ProtocolEventPayload>
        DecodeRequestReference(JsonElement payload, string path)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            payload,
            path,
            RequestReferenceFields);
        var requestId = ProtocolPayloadReader.ReadRequiredString(
            payload,
            path,
            "requestId");
        var error = HelloPayloadCodec.FirstError(objectError, requestId.Error);

        return error is null
            ? Success(new RequestReferenceEventPayload(requestId.Value!))
            : ProtocolPayloadDecodeResult<ProtocolEventPayload>.Failure(error);
    }

    private static ProtocolPayloadDecodeResult<ProtocolEventPayload>
        DecodeVehicleAdvanced(JsonElement payload, string path)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            payload,
            path,
            VehicleWrapperFields);
        var vehicleElement = ProtocolPayloadReader.ReadRequiredProperty(
            payload,
            path,
            "vehicle");
        var error = HelloPayloadCodec.FirstError(objectError, vehicleElement.Error);

        if (error is not null)
        {
            return ProtocolPayloadDecodeResult<ProtocolEventPayload>.Failure(error);
        }

        var vehicle = OnlineContractCodec.ReadVehicle(
            vehicleElement.Value,
            $"{path}.vehicle");
        return From(vehicle, value => new VehicleAdvancedEventPayload(value));
    }

    private static ProtocolPayloadDecodeResult<ProtocolEventPayload>
        DecodeVehicleReachedStop(JsonElement payload, string path)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            payload,
            path,
            VehicleReachedStopFields);
        var vehicleId = ProtocolPayloadReader.ReadRequiredString(
            payload,
            path,
            "vehicleId");
        var stopId = ProtocolPayloadReader.ReadRequiredString(
            payload,
            path,
            "stopId");
        var planVersion = ProtocolPayloadReader.ReadRequiredInteger(
            payload,
            path,
            "planVersion",
            minimum: 0);
        var positionElement = ProtocolPayloadReader.ReadRequiredProperty(
            payload,
            path,
            "position");
        var error = HelloPayloadCodec.FirstError(
            objectError,
            vehicleId.Error,
            stopId.Error,
            planVersion.Error,
            positionElement.Error);

        if (error is not null)
        {
            return ProtocolPayloadDecodeResult<ProtocolEventPayload>.Failure(error);
        }

        var position = OnlineContractCodec.ReadPosition(
            positionElement.Value,
            $"{path}.position");

        if (!position.IsSuccess)
        {
            return ProtocolPayloadDecodeResult<ProtocolEventPayload>.Failure(
                position.Error!);
        }

        if (position.Value is not NodePositionContract node)
        {
            return Failure(
                ProtocolPayloadErrorCode.InvalidValue,
                $"{path}.position.kind",
                "A reached-stop observation must use a node position.");
        }

        return Success(
            new VehicleReachedStopEventPayload(
                vehicleId.Value!,
                stopId.Value!,
                planVersion.Value,
                node));
    }

    private static ProtocolPayloadDecodeResult<ProtocolEventPayload>
        DecodePassenger(JsonElement payload, string path)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            payload,
            path,
            PassengerFields);
        var vehicleId = ProtocolPayloadReader.ReadRequiredString(
            payload,
            path,
            "vehicleId");
        var requestId = ProtocolPayloadReader.ReadRequiredString(
            payload,
            path,
            "requestId");
        var planVersion = ProtocolPayloadReader.ReadRequiredInteger(
            payload,
            path,
            "planVersion",
            minimum: 0);
        var error = HelloPayloadCodec.FirstError(
            objectError,
            vehicleId.Error,
            requestId.Error,
            planVersion.Error);

        return error is null
            ? Success(
                new PassengerEventPayload(
                    vehicleId.Value!,
                    requestId.Value!,
                    planVersion.Value))
            : ProtocolPayloadDecodeResult<ProtocolEventPayload>.Failure(error);
    }

    private static ProtocolPayloadDecodeResult<ProtocolEventPayload>
        DecodeTravelTimes(JsonElement payload, string path)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            payload,
            path,
            TravelWrapperFields);
        var snapshotElement = ProtocolPayloadReader.ReadRequiredProperty(
            payload,
            path,
            "snapshot");
        var error = HelloPayloadCodec.FirstError(objectError, snapshotElement.Error);

        if (error is not null)
        {
            return ProtocolPayloadDecodeResult<ProtocolEventPayload>.Failure(error);
        }

        var snapshot = OnlineContractCodec.ReadTravelSnapshot(
            snapshotElement.Value,
            $"{path}.snapshot");
        return From(snapshot, value => new TravelTimesUpdatedEventPayload(value));
    }

    private static ProtocolPayloadDecodeResult<ProtocolEventPayload>
        DecodeTimerTick(JsonElement payload, string path)
    {
        var error = ProtocolPayloadReader.ValidateObject(payload, path, EmptyFields);
        return error is null
            ? Success(TimerTickEventPayload.Instance)
            : ProtocolPayloadDecodeResult<ProtocolEventPayload>.Failure(error);
    }

    private static ProtocolPayloadDecodeResult<ProtocolEventPayload>
        DecodeIncidentOpened(JsonElement payload, string path)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            payload,
            path,
            IncidentOpenedFields);
        var incidentId = ProtocolPayloadReader.ReadRequiredString(
            payload,
            path,
            "incidentId");
        var reasonCode = ProtocolPayloadReader.ReadRequiredString(
            payload,
            path,
            "reasonCode");
        var vehicles = ProtocolPayloadReader.ReadRequiredStringSet(
            payload,
            path,
            "vehicleIds",
            allowEmpty: false);
        var error = HelloPayloadCodec.FirstError(
            objectError,
            incidentId.Error,
            reasonCode.Error,
            vehicles.Error);

        return error is null
            ? Success(
                new IncidentOpenedEventPayload(
                    incidentId.Value!,
                    reasonCode.Value!,
                    vehicles.Value!))
            : ProtocolPayloadDecodeResult<ProtocolEventPayload>.Failure(error);
    }

    private static ProtocolPayloadDecodeResult<ProtocolEventPayload>
        DecodeIncidentResolved(JsonElement payload, string path)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            payload,
            path,
            IncidentResolvedFields);
        var incidentId = ProtocolPayloadReader.ReadRequiredString(
            payload,
            path,
            "incidentId");
        var error = HelloPayloadCodec.FirstError(objectError, incidentId.Error);

        return error is null
            ? Success(new IncidentResolvedEventPayload(incidentId.Value!))
            : ProtocolPayloadDecodeResult<ProtocolEventPayload>.Failure(error);
    }

    private static ProtocolPayloadDecodeResult<ProtocolEventPayload> From<T>(
        ProtocolValueReadResult<T> result,
        Func<T, ProtocolEventPayload> map)
    {
        return result.IsSuccess
            ? Success(map(result.Value!))
            : ProtocolPayloadDecodeResult<ProtocolEventPayload>.Failure(result.Error!);
    }

    private static ProtocolPayloadDecodeResult<ProtocolEventPayload> Success(
        ProtocolEventPayload payload) =>
        ProtocolPayloadDecodeResult<ProtocolEventPayload>.Success(payload);

    private static ProtocolPayloadDecodeResult<ProtocolEventPayload> Failure(
        ProtocolPayloadErrorCode code,
        string path,
        string message) =>
        ProtocolPayloadDecodeResult<ProtocolEventPayload>.Failure(
            new ProtocolPayloadError(code, path, message));

    private static IReadOnlySet<string> Fields(params string[] values) =>
        new HashSet<string>(values, StringComparer.Ordinal);

    private static void RequireIdentifier(string value, string parameterName)
    {
        if (!OpaqueIdentifier.IsValid(value))
        {
            throw new ArgumentException(
                "Identifier must contain 1 to 128 valid UTF-8 bytes.",
                parameterName);
        }
    }

    private static void RequireCanonical(long value, string parameterName)
    {
        if (value is < 0 or > ProtocolLimits.MaxCanonicalInteger)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static string[] NormalizeSet(
        IReadOnlyList<string> values,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values);
        var normalized = values.Order(StringComparer.Ordinal).ToArray();

        if (normalized.Distinct(StringComparer.Ordinal).Count() != normalized.Length)
        {
            throw new ArgumentException(
                "Semantic set contains a duplicate identifier.",
                parameterName);
        }

        foreach (var value in normalized)
        {
            RequireIdentifier(value, parameterName);
        }

        return normalized;
    }
}

public static class DecisionAppliedPayloadCodec
{
    private static readonly IReadOnlySet<string> Fields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "decisionHash",
        };

    public static ProtocolPayloadDecodeResult<DecisionAppliedPayload> Decode(
        JsonElement payload)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            payload,
            "$.payload",
            Fields);

        if (objectError is not null)
        {
            return ProtocolPayloadDecodeResult<DecisionAppliedPayload>.Failure(
                objectError);
        }

        var hashText = ProtocolPayloadReader.ReadRequiredString(
            payload,
            "$.payload",
            "decisionHash");

        if (!hashText.IsSuccess)
        {
            return ProtocolPayloadDecodeResult<DecisionAppliedPayload>.Failure(
                hashText.Error!);
        }

        if (!Sha256Hex.TryCreate(hashText.Value, out var hash))
        {
            return ProtocolPayloadDecodeResult<DecisionAppliedPayload>.Failure(
                new ProtocolPayloadError(
                    ProtocolPayloadErrorCode.InvalidValue,
                    "$.payload.decisionHash",
                    "decisionHash must be 64 lowercase hexadecimal characters."));
        }

        return ProtocolPayloadDecodeResult<DecisionAppliedPayload>.Success(
            new DecisionAppliedPayload(hash!));
    }

    public static byte[] Encode(DecisionAppliedPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return ProtocolPayloadReader.Write(
            writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("decisionHash", payload.DecisionHash.Value);
                writer.WriteEndObject();
            });
    }
}
