using System.Text.Json;

namespace RideBound.Contracts.Protocol;

public abstract record PositionContract;

public sealed record NodePositionContract(string NodeId) : PositionContract;

public sealed record EdgeProgressPositionContract(
    string FromNodeId,
    string ToNodeId,
    string EdgeId,
    long ProgressPermille) : PositionContract;

public enum RouteStopKind
{
    Waypoint,
    Pickup,
    DropOff,
}

public static class RouteStopKindVocabulary
{
    public static bool TryParse(string? value, out RouteStopKind kind)
    {
        kind = value switch
        {
            "waypoint" => RouteStopKind.Waypoint,
            "pickup" => RouteStopKind.Pickup,
            "dropOff" => RouteStopKind.DropOff,
            _ => default,
        };

        return value is "waypoint" or "pickup" or "dropOff";
    }

    public static string ToProtocolValue(RouteStopKind kind) =>
        kind switch
        {
            RouteStopKind.Waypoint => "waypoint",
            RouteStopKind.Pickup => "pickup",
            RouteStopKind.DropOff => "dropOff",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
}

public sealed record RouteStopContract(
    string StopId,
    string NodeId,
    RouteStopKind Kind,
    string? RequestId,
    long ServiceDurationMs);

public sealed record RoutePlanContract(
    long PlanVersion,
    long ExecutedStopCount,
    IReadOnlyList<RouteStopContract> FrozenPrefix,
    IReadOnlyList<RouteStopContract> MutableSuffix);

public sealed record RequestContract(
    string RequestId,
    long ArrivalTimeMs,
    string OriginNodeId,
    string DestinationNodeId,
    long EarliestPickupMs,
    long LatestPickupMs,
    long MaxRideTimeMs,
    long PartySize,
    string ServiceClass,
    string CommitmentPolicyId);

public sealed record VehicleSnapshotContract(
    string VehicleId,
    long Capacity,
    long OccupiedSeats,
    PositionContract Position,
    IReadOnlyList<string> OnboardRequestIds,
    IReadOnlyList<string> AcceptedRequestIds,
    RoutePlanContract Route);

public sealed record TravelArcContract(
    string FromNodeId,
    string ToNodeId,
    long TravelTimeMs);

public sealed record TravelTimeSnapshotContract(
    long Version,
    Sha256Hex SnapshotHash,
    IReadOnlyList<TravelArcContract> Arcs);

public abstract record ProtocolEventPayload;

public sealed record RequestArrivedEventPayload(RequestContract Request)
    : ProtocolEventPayload;

public sealed record RequestReferenceEventPayload(string RequestId)
    : ProtocolEventPayload;

public sealed record VehicleAdvancedEventPayload(VehicleSnapshotContract Vehicle)
    : ProtocolEventPayload;

public sealed record VehicleReachedStopEventPayload(
    string VehicleId,
    string StopId,
    long PlanVersion,
    NodePositionContract Position) : ProtocolEventPayload;

public sealed record PassengerEventPayload(
    string VehicleId,
    string RequestId,
    long PlanVersion) : ProtocolEventPayload;

public sealed record TravelTimesUpdatedEventPayload(
    TravelTimeSnapshotContract Snapshot) : ProtocolEventPayload;

public sealed record TimerTickEventPayload : ProtocolEventPayload
{
    public static TimerTickEventPayload Instance { get; } = new();
}

public sealed record IncidentOpenedEventPayload(
    string IncidentId,
    string ReasonCode,
    IReadOnlyList<string> VehicleIds) : ProtocolEventPayload;

public sealed record IncidentResolvedEventPayload(string IncidentId)
    : ProtocolEventPayload;

internal static class OnlineContractCodec
{
    private static readonly IReadOnlySet<string> NodePositionFields =
        Fields("kind", "nodeId");

    private static readonly IReadOnlySet<string> EdgePositionFields =
        Fields(
            "kind",
            "fromNodeId",
            "toNodeId",
            "edgeId",
            "progressPermille");

    private static readonly IReadOnlySet<string> RequestFields =
        Fields(
            "requestId",
            "arrivalTimeMs",
            "originNodeId",
            "destinationNodeId",
            "earliestPickupMs",
            "latestPickupMs",
            "maxRideTimeMs",
            "partySize",
            "serviceClass",
            "commitmentPolicyId");

    private static readonly IReadOnlySet<string> RouteStopFields =
        Fields(
            "stopId",
            "nodeId",
            "kind",
            "requestId",
            "serviceDurationMs");

    private static readonly IReadOnlySet<string> RoutePlanFields =
        Fields(
            "planVersion",
            "executedStopCount",
            "frozenPrefix",
            "mutableSuffix");

    private static readonly IReadOnlySet<string> VehicleFields =
        Fields(
            "vehicleId",
            "capacity",
            "occupiedSeats",
            "position",
            "onboardRequestIds",
            "acceptedRequestIds",
            "route");

    private static readonly IReadOnlySet<string> TravelArcFields =
        Fields("fromNodeId", "toNodeId", "travelTimeMs");

    private static readonly IReadOnlySet<string> TravelSnapshotFields =
        Fields("version", "snapshotHash", "arcs");

    public static ProtocolValueReadResult<PositionContract> ReadPosition(
        JsonElement value,
        string path)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            return WrongType<PositionContract>(path, "an object");
        }

        var kind = ProtocolPayloadReader.ReadRequiredString(
            value,
            path,
            "kind",
            requireOpaqueValue: false);

        if (!kind.IsSuccess)
        {
            return Failure<PositionContract>(kind.Error!);
        }

        if (kind.Value == "node")
        {
            var objectError = ProtocolPayloadReader.ValidateObject(
                value,
                path,
                NodePositionFields);
            var nodeId = ProtocolPayloadReader.ReadRequiredString(
                value,
                path,
                "nodeId");
            var error = HelloPayloadCodec.FirstError(objectError, nodeId.Error);

            return error is null
                ? Success<PositionContract>(new NodePositionContract(nodeId.Value!))
                : Failure<PositionContract>(error);
        }

        if (kind.Value == "edgeProgress")
        {
            var objectError = ProtocolPayloadReader.ValidateObject(
                value,
                path,
                EdgePositionFields);
            var from = ProtocolPayloadReader.ReadRequiredString(
                value,
                path,
                "fromNodeId");
            var to = ProtocolPayloadReader.ReadRequiredString(
                value,
                path,
                "toNodeId");
            var edge = ProtocolPayloadReader.ReadRequiredString(
                value,
                path,
                "edgeId");
            var progress = ProtocolPayloadReader.ReadRequiredInteger(
                value,
                path,
                "progressPermille",
                minimum: 1,
                maximum: 999);
            var error = HelloPayloadCodec.FirstError(
                objectError,
                from.Error,
                to.Error,
                edge.Error,
                progress.Error);

            return error is null
                ? Success<PositionContract>(
                    new EdgeProgressPositionContract(
                        from.Value!,
                        to.Value!,
                        edge.Value!,
                        progress.Value))
                : Failure<PositionContract>(error);
        }

        return Invalid<PositionContract>(
            $"{path}.kind",
            "Position kind must be 'node' or 'edgeProgress'.");
    }

    public static void WritePosition(Utf8JsonWriter writer, PositionContract position)
    {
        ArgumentNullException.ThrowIfNull(position);
        writer.WriteStartObject();

        switch (position)
        {
            case NodePositionContract node:
                RequireIdentifier(node.NodeId, nameof(node.NodeId));
                writer.WriteString("kind", "node");
                writer.WriteString("nodeId", node.NodeId);
                break;
            case EdgeProgressPositionContract edge:
                RequireIdentifier(edge.FromNodeId, nameof(edge.FromNodeId));
                RequireIdentifier(edge.ToNodeId, nameof(edge.ToNodeId));
                RequireIdentifier(edge.EdgeId, nameof(edge.EdgeId));
                RequireRange(
                    edge.ProgressPermille,
                    1,
                    999,
                    nameof(edge.ProgressPermille));
                writer.WriteString("kind", "edgeProgress");
                writer.WriteString("fromNodeId", edge.FromNodeId);
                writer.WriteString("toNodeId", edge.ToNodeId);
                writer.WriteString("edgeId", edge.EdgeId);
                writer.WriteNumber("progressPermille", edge.ProgressPermille);
                break;
            default:
                throw new ArgumentException(
                    $"Unsupported position type '{position.GetType().Name}'.",
                    nameof(position));
        }

        writer.WriteEndObject();
    }

    public static ProtocolValueReadResult<RequestContract> ReadRequest(
        JsonElement value,
        string path)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            value,
            path,
            RequestFields);
        var requestId = ProtocolPayloadReader.ReadRequiredString(
            value,
            path,
            "requestId");
        var arrival = ProtocolPayloadReader.ReadRequiredInteger(
            value,
            path,
            "arrivalTimeMs",
            minimum: 0);
        var origin = ProtocolPayloadReader.ReadRequiredString(
            value,
            path,
            "originNodeId");
        var destination = ProtocolPayloadReader.ReadRequiredString(
            value,
            path,
            "destinationNodeId");
        var earliest = ProtocolPayloadReader.ReadRequiredInteger(
            value,
            path,
            "earliestPickupMs",
            minimum: 0);
        var latest = ProtocolPayloadReader.ReadRequiredInteger(
            value,
            path,
            "latestPickupMs",
            minimum: 0);
        var maxRide = ProtocolPayloadReader.ReadRequiredInteger(
            value,
            path,
            "maxRideTimeMs",
            minimum: 1);
        var partySize = ProtocolPayloadReader.ReadRequiredInteger(
            value,
            path,
            "partySize",
            minimum: 1);
        var serviceClass = ProtocolPayloadReader.ReadRequiredString(
            value,
            path,
            "serviceClass");
        var commitmentPolicy = ProtocolPayloadReader.ReadRequiredString(
            value,
            path,
            "commitmentPolicyId");
        var error = HelloPayloadCodec.FirstError(
            objectError,
            requestId.Error,
            arrival.Error,
            origin.Error,
            destination.Error,
            earliest.Error,
            latest.Error,
            maxRide.Error,
            partySize.Error,
            serviceClass.Error,
            commitmentPolicy.Error);

        if (error is not null)
        {
            return Failure<RequestContract>(error);
        }

        if (arrival.Value > earliest.Value || earliest.Value > latest.Value)
        {
            return Invalid<RequestContract>(
                path,
                "Request times must satisfy arrivalTimeMs <= earliestPickupMs <= latestPickupMs.");
        }

        if (string.Equals(origin.Value, destination.Value, StringComparison.Ordinal))
        {
            return Invalid<RequestContract>(
                $"{path}.destinationNodeId",
                "Origin and destination must be distinct.");
        }

        return Success(
            new RequestContract(
                requestId.Value!,
                arrival.Value,
                origin.Value!,
                destination.Value!,
                earliest.Value,
                latest.Value,
                maxRide.Value,
                partySize.Value,
                serviceClass.Value!,
                commitmentPolicy.Value!));
    }

    public static void WriteRequest(Utf8JsonWriter writer, RequestContract request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireIdentifier(request.RequestId, nameof(request.RequestId));
        RequireIdentifier(request.OriginNodeId, nameof(request.OriginNodeId));
        RequireIdentifier(
            request.DestinationNodeId,
            nameof(request.DestinationNodeId));
        RequireIdentifier(request.ServiceClass, nameof(request.ServiceClass));
        RequireIdentifier(
            request.CommitmentPolicyId,
            nameof(request.CommitmentPolicyId));
        RequireRange(
            request.ArrivalTimeMs,
            0,
            ProtocolLimits.MaxCanonicalInteger,
            nameof(request.ArrivalTimeMs));
        RequireRange(
            request.EarliestPickupMs,
            request.ArrivalTimeMs,
            ProtocolLimits.MaxCanonicalInteger,
            nameof(request.EarliestPickupMs));
        RequireRange(
            request.LatestPickupMs,
            request.EarliestPickupMs,
            ProtocolLimits.MaxCanonicalInteger,
            nameof(request.LatestPickupMs));
        RequireRange(
            request.MaxRideTimeMs,
            1,
            ProtocolLimits.MaxCanonicalInteger,
            nameof(request.MaxRideTimeMs));
        RequireRange(
            request.PartySize,
            1,
            ProtocolLimits.MaxCanonicalInteger,
            nameof(request.PartySize));

        if (string.Equals(
                request.OriginNodeId,
                request.DestinationNodeId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Origin and destination must be distinct.",
                nameof(request));
        }

        writer.WriteStartObject();
        writer.WriteString("requestId", request.RequestId);
        writer.WriteNumber("arrivalTimeMs", request.ArrivalTimeMs);
        writer.WriteString("originNodeId", request.OriginNodeId);
        writer.WriteString("destinationNodeId", request.DestinationNodeId);
        writer.WriteNumber("earliestPickupMs", request.EarliestPickupMs);
        writer.WriteNumber("latestPickupMs", request.LatestPickupMs);
        writer.WriteNumber("maxRideTimeMs", request.MaxRideTimeMs);
        writer.WriteNumber("partySize", request.PartySize);
        writer.WriteString("serviceClass", request.ServiceClass);
        writer.WriteString("commitmentPolicyId", request.CommitmentPolicyId);
        writer.WriteEndObject();
    }

    public static ProtocolValueReadResult<RoutePlanContract> ReadRoutePlan(
        JsonElement value,
        string path)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            value,
            path,
            RoutePlanFields);
        var version = ProtocolPayloadReader.ReadRequiredInteger(
            value,
            path,
            "planVersion",
            minimum: 0);
        var executed = ProtocolPayloadReader.ReadRequiredInteger(
            value,
            path,
            "executedStopCount",
            minimum: 0);
        var frozen = ReadRouteStops(value, path, "frozenPrefix");
        var mutable = ReadRouteStops(value, path, "mutableSuffix");
        var error = HelloPayloadCodec.FirstError(
            objectError,
            version.Error,
            executed.Error,
            frozen.Error,
            mutable.Error);

        if (error is not null)
        {
            return Failure<RoutePlanContract>(error);
        }

        if (executed.Value > frozen.Value!.Count)
        {
            return Invalid<RoutePlanContract>(
                $"{path}.executedStopCount",
                "executedStopCount cannot exceed frozenPrefix length.");
        }

        var duplicateStop = frozen.Value.Concat(mutable.Value!)
            .GroupBy(stop => stop.StopId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateStop is not null)
        {
            return Invalid<RoutePlanContract>(
                path,
                $"Route stop ID '{duplicateStop.Key}' is duplicated.");
        }

        return Success(
            new RoutePlanContract(
                version.Value,
                executed.Value,
                frozen.Value,
                mutable.Value!));
    }

    public static void WriteRoutePlan(Utf8JsonWriter writer, RoutePlanContract route)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(route.FrozenPrefix);
        ArgumentNullException.ThrowIfNull(route.MutableSuffix);
        RequireRange(
            route.PlanVersion,
            0,
            ProtocolLimits.MaxCanonicalInteger,
            nameof(route.PlanVersion));
        RequireRange(
            route.ExecutedStopCount,
            0,
            route.FrozenPrefix.Count,
            nameof(route.ExecutedStopCount));

        var allStops = route.FrozenPrefix.Concat(route.MutableSuffix).ToArray();

        if (allStops.Select(stop => stop.StopId).Distinct(StringComparer.Ordinal).Count()
            != allStops.Length)
        {
            throw new ArgumentException(
                "Route stop IDs must be unique.",
                nameof(route));
        }

        writer.WriteStartObject();
        writer.WriteNumber("planVersion", route.PlanVersion);
        writer.WriteNumber("executedStopCount", route.ExecutedStopCount);
        writer.WritePropertyName("frozenPrefix");
        WriteRouteStops(writer, route.FrozenPrefix);
        writer.WritePropertyName("mutableSuffix");
        WriteRouteStops(writer, route.MutableSuffix);
        writer.WriteEndObject();
    }

    public static ProtocolValueReadResult<VehicleSnapshotContract> ReadVehicle(
        JsonElement value,
        string path)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            value,
            path,
            VehicleFields);
        var vehicleId = ProtocolPayloadReader.ReadRequiredString(
            value,
            path,
            "vehicleId");
        var capacity = ProtocolPayloadReader.ReadRequiredInteger(
            value,
            path,
            "capacity",
            minimum: 1);
        var occupied = ProtocolPayloadReader.ReadRequiredInteger(
            value,
            path,
            "occupiedSeats",
            minimum: 0);
        var positionElement = ProtocolPayloadReader.ReadRequiredProperty(
            value,
            path,
            "position");
        var onboard = ProtocolPayloadReader.ReadRequiredStringSet(
            value,
            path,
            "onboardRequestIds",
            allowEmpty: true);
        var accepted = ProtocolPayloadReader.ReadRequiredStringSet(
            value,
            path,
            "acceptedRequestIds",
            allowEmpty: true);
        var routeElement = ProtocolPayloadReader.ReadRequiredProperty(
            value,
            path,
            "route");
        var error = HelloPayloadCodec.FirstError(
            objectError,
            vehicleId.Error,
            capacity.Error,
            occupied.Error,
            positionElement.Error,
            onboard.Error,
            accepted.Error,
            routeElement.Error);

        if (error is not null)
        {
            return Failure<VehicleSnapshotContract>(error);
        }

        var position = ReadPosition(positionElement.Value, $"{path}.position");
        var route = ReadRoutePlan(routeElement.Value, $"{path}.route");
        error = HelloPayloadCodec.FirstError(position.Error, route.Error);

        if (error is not null)
        {
            return Failure<VehicleSnapshotContract>(error);
        }

        if (occupied.Value > capacity.Value)
        {
            return Invalid<VehicleSnapshotContract>(
                $"{path}.occupiedSeats",
                "occupiedSeats cannot exceed capacity.");
        }

        if (onboard.Value!.Except(accepted.Value!, StringComparer.Ordinal).Any())
        {
            return Invalid<VehicleSnapshotContract>(
                $"{path}.acceptedRequestIds",
                "acceptedRequestIds must contain every onboard request.");
        }

        return Success(
            new VehicleSnapshotContract(
                vehicleId.Value!,
                capacity.Value,
                occupied.Value,
                position.Value!,
                onboard.Value!,
                accepted.Value!,
                route.Value!));
    }

    public static void WriteVehicle(
        Utf8JsonWriter writer,
        VehicleSnapshotContract vehicle)
    {
        ArgumentNullException.ThrowIfNull(vehicle);
        RequireIdentifier(vehicle.VehicleId, nameof(vehicle.VehicleId));
        RequireRange(
            vehicle.Capacity,
            1,
            ProtocolLimits.MaxCanonicalInteger,
            nameof(vehicle.Capacity));
        RequireRange(
            vehicle.OccupiedSeats,
            0,
            vehicle.Capacity,
            nameof(vehicle.OccupiedSeats));
        var onboard = NormalizeSet(
            vehicle.OnboardRequestIds,
            nameof(vehicle.OnboardRequestIds));
        var accepted = NormalizeSet(
            vehicle.AcceptedRequestIds,
            nameof(vehicle.AcceptedRequestIds));

        if (onboard.Except(accepted, StringComparer.Ordinal).Any())
        {
            throw new ArgumentException(
                "acceptedRequestIds must contain every onboard request.",
                nameof(vehicle));
        }

        writer.WriteStartObject();
        writer.WriteString("vehicleId", vehicle.VehicleId);
        writer.WriteNumber("capacity", vehicle.Capacity);
        writer.WriteNumber("occupiedSeats", vehicle.OccupiedSeats);
        writer.WritePropertyName("position");
        WritePosition(writer, vehicle.Position);
        WriteStringArray(writer, "onboardRequestIds", onboard);
        WriteStringArray(writer, "acceptedRequestIds", accepted);
        writer.WritePropertyName("route");
        WriteRoutePlan(writer, vehicle.Route);
        writer.WriteEndObject();
    }

    public static ProtocolValueReadResult<TravelTimeSnapshotContract>
        ReadTravelSnapshot(JsonElement value, string path)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            value,
            path,
            TravelSnapshotFields);
        var version = ProtocolPayloadReader.ReadRequiredInteger(
            value,
            path,
            "version",
            minimum: 1);
        var hashText = ProtocolPayloadReader.ReadRequiredString(
            value,
            path,
            "snapshotHash",
            requireOpaqueValue: false);
        var arcsProperty = ProtocolPayloadReader.ReadRequiredProperty(
            value,
            path,
            "arcs");
        var error = HelloPayloadCodec.FirstError(
            objectError,
            version.Error,
            hashText.Error,
            arcsProperty.Error);

        if (error is not null)
        {
            return Failure<TravelTimeSnapshotContract>(error);
        }

        if (!Sha256Hex.TryCreate(hashText.Value, out var hash))
        {
            return Invalid<TravelTimeSnapshotContract>(
                $"{path}.snapshotHash",
                "snapshotHash must be 64 lowercase hexadecimal characters.");
        }

        if (arcsProperty.Value.ValueKind != JsonValueKind.Array)
        {
            return WrongType<TravelTimeSnapshotContract>(
                $"{path}.arcs",
                "an array");
        }

        var arcs = new List<TravelArcContract>();
        var index = 0;

        foreach (var arcElement in arcsProperty.Value.EnumerateArray())
        {
            var arcPath = $"{path}.arcs[{index}]";
            var arcObjectError = ProtocolPayloadReader.ValidateObject(
                arcElement,
                arcPath,
                TravelArcFields);
            var from = ProtocolPayloadReader.ReadRequiredString(
                arcElement,
                arcPath,
                "fromNodeId");
            var to = ProtocolPayloadReader.ReadRequiredString(
                arcElement,
                arcPath,
                "toNodeId");
            var travelTime = ProtocolPayloadReader.ReadRequiredInteger(
                arcElement,
                arcPath,
                "travelTimeMs",
                minimum: 0);
            error = HelloPayloadCodec.FirstError(
                arcObjectError,
                from.Error,
                to.Error,
                travelTime.Error);

            if (error is not null)
            {
                return Failure<TravelTimeSnapshotContract>(error);
            }

            if (string.Equals(from.Value, to.Value, StringComparison.Ordinal))
            {
                return Invalid<TravelTimeSnapshotContract>(
                    arcPath,
                    "Travel arc endpoints must be distinct.");
            }

            arcs.Add(
                new TravelArcContract(
                    from.Value!,
                    to.Value!,
                    travelTime.Value));
            index++;
        }

        if (arcs.Count == 0)
        {
            return Invalid<TravelTimeSnapshotContract>(
                $"{path}.arcs",
                "A travel-time snapshot must contain at least one arc.");
        }

        var duplicate = arcs
            .GroupBy(
                arc => (arc.FromNodeId, arc.ToNodeId),
                EqualityComparer<(string, string)>.Default)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            return Invalid<TravelTimeSnapshotContract>(
                $"{path}.arcs",
                $"Directed arc '{duplicate.Key.Item1}->{duplicate.Key.Item2}' is duplicated.");
        }

        arcs.Sort(CompareArcs);
        return Success(
            new TravelTimeSnapshotContract(version.Value, hash!, arcs));
    }

    public static void WriteTravelSnapshot(
        Utf8JsonWriter writer,
        TravelTimeSnapshotContract snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(snapshot.Arcs);
        RequireRange(
            snapshot.Version,
            1,
            ProtocolLimits.MaxCanonicalInteger,
            nameof(snapshot.Version));

        if (snapshot.Arcs.Count == 0)
        {
            throw new ArgumentException(
                "A travel-time snapshot must contain at least one arc.",
                nameof(snapshot));
        }

        var arcs = snapshot.Arcs.Order(TravelArcComparer.Instance).ToArray();

        if (arcs
            .Select(arc => (arc.FromNodeId, arc.ToNodeId))
            .Distinct()
            .Count() != arcs.Length)
        {
            throw new ArgumentException(
                "Directed travel arcs must be unique.",
                nameof(snapshot));
        }

        writer.WriteStartObject();
        writer.WriteNumber("version", snapshot.Version);
        writer.WriteString("snapshotHash", snapshot.SnapshotHash.Value);
        writer.WritePropertyName("arcs");
        writer.WriteStartArray();

        foreach (var arc in arcs)
        {
            RequireIdentifier(arc.FromNodeId, nameof(arc.FromNodeId));
            RequireIdentifier(arc.ToNodeId, nameof(arc.ToNodeId));
            RequireRange(
                arc.TravelTimeMs,
                0,
                ProtocolLimits.MaxCanonicalInteger,
                nameof(arc.TravelTimeMs));

            if (string.Equals(
                    arc.FromNodeId,
                    arc.ToNodeId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Travel arc endpoints must be distinct.",
                    nameof(snapshot));
            }

            writer.WriteStartObject();
            writer.WriteString("fromNodeId", arc.FromNodeId);
            writer.WriteString("toNodeId", arc.ToNodeId);
            writer.WriteNumber("travelTimeMs", arc.TravelTimeMs);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static ProtocolValueReadResult<IReadOnlyList<RouteStopContract>>
        ReadRouteStops(JsonElement root, string path, string field)
    {
        var property = ProtocolPayloadReader.ReadRequiredProperty(root, path, field);

        if (!property.IsSuccess)
        {
            return Failure<IReadOnlyList<RouteStopContract>>(property.Error!);
        }

        var fieldPath = $"{path}.{field}";

        if (property.Value.ValueKind != JsonValueKind.Array)
        {
            return WrongType<IReadOnlyList<RouteStopContract>>(
                fieldPath,
                "an array");
        }

        var stops = new List<RouteStopContract>();
        var index = 0;

        foreach (var element in property.Value.EnumerateArray())
        {
            var stopPath = $"{fieldPath}[{index}]";
            var objectError = ProtocolPayloadReader.ValidateObject(
                element,
                stopPath,
                RouteStopFields);
            var stopId = ProtocolPayloadReader.ReadRequiredString(
                element,
                stopPath,
                "stopId");
            var nodeId = ProtocolPayloadReader.ReadRequiredString(
                element,
                stopPath,
                "nodeId");
            var kindText = ProtocolPayloadReader.ReadRequiredString(
                element,
                stopPath,
                "kind",
                requireOpaqueValue: false);
            var requestId = ProtocolPayloadReader.ReadOptionalString(
                element,
                stopPath,
                "requestId");
            var service = ProtocolPayloadReader.ReadRequiredInteger(
                element,
                stopPath,
                "serviceDurationMs",
                minimum: 0);
            var error = HelloPayloadCodec.FirstError(
                objectError,
                stopId.Error,
                nodeId.Error,
                kindText.Error,
                requestId.Error,
                service.Error);

            if (error is not null)
            {
                return Failure<IReadOnlyList<RouteStopContract>>(error);
            }

            if (!RouteStopKindVocabulary.TryParse(kindText.Value, out var kind))
            {
                return Invalid<IReadOnlyList<RouteStopContract>>(
                    $"{stopPath}.kind",
                    "Route stop kind is unknown.");
            }

            if (kind == RouteStopKind.Waypoint && requestId.Value is not null
                || kind != RouteStopKind.Waypoint && requestId.Value is null)
            {
                return Invalid<IReadOnlyList<RouteStopContract>>(
                    $"{stopPath}.requestId",
                    "Waypoint stops omit requestId; pickup/dropOff stops require it.");
            }

            stops.Add(
                new RouteStopContract(
                    stopId.Value!,
                    nodeId.Value!,
                    kind,
                    requestId.Value,
                    service.Value));
            index++;
        }

        return Success<IReadOnlyList<RouteStopContract>>(stops);
    }

    private static void WriteRouteStops(
        Utf8JsonWriter writer,
        IReadOnlyList<RouteStopContract> stops)
    {
        writer.WriteStartArray();

        foreach (var stop in stops)
        {
            RequireIdentifier(stop.StopId, nameof(stop.StopId));
            RequireIdentifier(stop.NodeId, nameof(stop.NodeId));
            RequireRange(
                stop.ServiceDurationMs,
                0,
                ProtocolLimits.MaxCanonicalInteger,
                nameof(stop.ServiceDurationMs));

            if (stop.Kind == RouteStopKind.Waypoint && stop.RequestId is not null
                || stop.Kind != RouteStopKind.Waypoint && stop.RequestId is null)
            {
                throw new ArgumentException(
                    "Waypoint stops omit requestId; pickup/dropOff stops require it.",
                    nameof(stops));
            }

            writer.WriteStartObject();
            writer.WriteString("stopId", stop.StopId);
            writer.WriteString("nodeId", stop.NodeId);
            writer.WriteString(
                "kind",
                RouteStopKindVocabulary.ToProtocolValue(stop.Kind));

            if (stop.RequestId is not null)
            {
                RequireIdentifier(stop.RequestId, nameof(stop.RequestId));
                writer.WriteString("requestId", stop.RequestId);
            }

            writer.WriteNumber("serviceDurationMs", stop.ServiceDurationMs);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
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

    private static void WriteStringArray(
        Utf8JsonWriter writer,
        string propertyName,
        IReadOnlyList<string> values)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();

        foreach (var value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }

    private static int CompareArcs(TravelArcContract left, TravelArcContract right)
    {
        var from = string.CompareOrdinal(left.FromNodeId, right.FromNodeId);
        return from != 0
            ? from
            : string.CompareOrdinal(left.ToNodeId, right.ToNodeId);
    }

    private static IReadOnlySet<string> Fields(params string[] values) =>
        new HashSet<string>(values, StringComparer.Ordinal);

    private static ProtocolValueReadResult<T> Success<T>(T value) =>
        ProtocolValueReadResult<T>.Success(value);

    private static ProtocolValueReadResult<T> Failure<T>(
        ProtocolPayloadError error) =>
        ProtocolValueReadResult<T>.Failure(error);

    private static ProtocolValueReadResult<T> Invalid<T>(
        string path,
        string message) =>
        Failure<T>(
            new ProtocolPayloadError(
                ProtocolPayloadErrorCode.InvalidValue,
                path,
                message));

    private static ProtocolValueReadResult<T> WrongType<T>(
        string path,
        string expected) =>
        Failure<T>(
            new ProtocolPayloadError(
                ProtocolPayloadErrorCode.InvalidFieldType,
                path,
                $"Field '{path}' must be {expected}."));

    private static void RequireIdentifier(string value, string parameterName)
    {
        if (!OpaqueIdentifier.IsValid(value))
        {
            throw new ArgumentException(
                "Identifier must contain 1 to 128 valid UTF-8 bytes.",
                parameterName);
        }
    }

    private static void RequireRange(
        long value,
        long minimum,
        long maximum,
        string parameterName)
    {
        if (value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"Value must be in [{minimum}, {maximum}].");
        }
    }

    private sealed class TravelArcComparer : IComparer<TravelArcContract>
    {
        public static TravelArcComparer Instance { get; } = new();

        public int Compare(TravelArcContract? x, TravelArcContract? y)
        {
            ArgumentNullException.ThrowIfNull(x);
            ArgumentNullException.ThrowIfNull(y);
            return CompareArcs(x, y);
        }
    }
}
