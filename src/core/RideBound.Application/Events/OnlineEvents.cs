using RideBound.Application.Travel;
using RideBound.Domain.Common;
using RideBound.Domain.Incidents;
using RideBound.Domain.Requests;
using RideBound.Domain.Vehicles;

namespace RideBound.Application.Events;

public abstract record OnlineEvent(long EventSequence, SimTime SimulationTime);

public sealed record RequestArrived(
    long EventSequence,
    SimTime SimulationTime,
    RideRequest Request) : OnlineEvent(EventSequence, SimulationTime);

public sealed record BookingConfirmed(
    long EventSequence,
    SimTime SimulationTime,
    RequestId RequestId) : OnlineEvent(EventSequence, SimulationTime);

public sealed record OfferDeclined(
    long EventSequence,
    SimTime SimulationTime,
    RequestId RequestId) : OnlineEvent(EventSequence, SimulationTime);

public sealed record RequestCancelledBeforeAcceptance(
    long EventSequence,
    SimTime SimulationTime,
    RequestId RequestId) : OnlineEvent(EventSequence, SimulationTime);

public sealed record RequestCancelledAfterAcceptance(
    long EventSequence,
    SimTime SimulationTime,
    RequestId RequestId) : OnlineEvent(EventSequence, SimulationTime);

public sealed record VehicleAdvanced(
    long EventSequence,
    SimTime SimulationTime,
    VehicleState Observation) : OnlineEvent(EventSequence, SimulationTime);

public sealed record VehicleReachedStop(
    long EventSequence,
    SimTime SimulationTime,
    VehicleId VehicleId,
    StopId StopId,
    PlanVersion PlanVersion,
    NodePosition Position) : OnlineEvent(EventSequence, SimulationTime);

public sealed record PassengerBoarded(
    long EventSequence,
    SimTime SimulationTime,
    VehicleId VehicleId,
    RequestId RequestId,
    PlanVersion PlanVersion) : OnlineEvent(EventSequence, SimulationTime);

public sealed record PassengerAlighted(
    long EventSequence,
    SimTime SimulationTime,
    VehicleId VehicleId,
    RequestId RequestId,
    PlanVersion PlanVersion) : OnlineEvent(EventSequence, SimulationTime);

public sealed record TravelTimesUpdated(
    long EventSequence,
    SimTime SimulationTime,
    TravelTimeSnapshot Snapshot) : OnlineEvent(EventSequence, SimulationTime);

public sealed record TimerTick(long EventSequence, SimTime SimulationTime)
    : OnlineEvent(EventSequence, SimulationTime);

public sealed record IncidentOpened(
    long EventSequence,
    SimTime SimulationTime,
    IncidentId IncidentId,
    string ReasonCode,
    IReadOnlyList<VehicleId> VehicleIds)
    : OnlineEvent(EventSequence, SimulationTime);

public sealed record IncidentResolved(
    long EventSequence,
    SimTime SimulationTime,
    IncidentId IncidentId)
    : OnlineEvent(EventSequence, SimulationTime);

public sealed record InternalEventBatch(
    RunIdentifier RunId,
    ScenarioIdentifier ScenarioId,
    long Epoch,
    SimTime SimulationTime,
    IReadOnlyList<OnlineEvent> Events);
