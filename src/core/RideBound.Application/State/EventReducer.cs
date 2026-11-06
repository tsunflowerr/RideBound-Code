using RideBound.Application.Events;
using RideBound.Application.Travel;
using RideBound.Domain.Common;
using RideBound.Domain.Incidents;
using RideBound.Domain.Requests;
using RideBound.Domain.Runs;
using RideBound.Domain.Vehicles;

namespace RideBound.Application.State;

public sealed class EventReducer
{
    private readonly bool _recordLatePickups;

    /// <param name="recordLatePickups">
    /// ADR-076 (exploratory, off by default). When true, a passenger observed boarding
    /// after the latest pickup is accepted and recorded as an <see cref="ObservedLatePickup"/>
    /// instead of failing the batch; the accepted window is unchanged.
    /// </param>
    public EventReducer(bool recordLatePickups = false)
    {
        _recordLatePickups = recordLatePickups;
    }

    public EventReductionResult Reduce(
        OnlineState state,
        InternalEventBatch batch)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(batch);

        var structuralFailure = ValidateBatch(state, batch);

        if (structuralFailure is not null)
        {
            return EventReductionResult.Failure(structuralFailure);
        }

        var run = state.Run;
        var travelTimes = state.TravelTimes;
        var incidents = state.Incidents;

        for (var index = 0; index < batch.Events.Count; index++)
        {
            var onlineEvent = batch.Events[index];
            var applied = ApplyEvent(
                run,
                travelTimes,
                incidents,
                state.ExpectedInitialTravelTimeSnapshotHash,
                batch.Epoch,
                onlineEvent,
                _recordLatePickups);

            if (applied.Failure is not null)
            {
                return EventReductionResult.Failure(
                    ToWitness(applied.Failure, index, onlineEvent.EventSequence));
            }

            run = applied.Run!;
            travelTimes = applied.TravelTimes;
            incidents = applied.Incidents!;
        }

        if (travelTimes is null)
        {
            return EventReductionResult.Failure(
                new EventReductionWitness(
                    EventReductionFailureCodes.TravelSnapshotRequired,
                    "The first proposed online state requires a travel snapshot."));
        }

        if (run.Vehicles.Count == 0)
        {
            return EventReductionResult.Failure(
                new EventReductionWitness(
                    EventReductionFailureCodes.VehicleBootstrapRequired,
                    "The first proposed online state requires at least one vehicle."));
        }

        var advanced = run.AdvanceEpoch(batch.Epoch, batch.SimulationTime);

        if (!advanced.IsSuccess)
        {
            return EventReductionResult.Failure(
                ToWitness(advanced.Failure!));
        }

        var nextSequence = batch.Events[^1].EventSequence + 1;
        return EventReductionResult.Success(
            new OnlineState(
                advanced.Value!,
                travelTimes,
                nextSequence,
                state.ExpectedInitialTravelTimeSnapshotHash,
                state.Commitments,
                incidents)
            {
                PlanPool = state.PlanPool,
            });
    }

    private static EventReductionWitness? ValidateBatch(
        OnlineState state,
        InternalEventBatch batch)
    {
        if (batch.RunId != state.Run.Id
            || batch.ScenarioId != state.Run.ScenarioId)
        {
            return new EventReductionWitness(
                EventReductionFailureCodes.IdentityMismatch,
                "Internal event batch identity does not match the run.");
        }

        if (batch.Epoch is < 1 or > DomainLimits.MaxCanonicalInteger
            || state.Run.AppliedEpoch >= DomainLimits.MaxCanonicalInteger
            || batch.Epoch != state.Run.AppliedEpoch + 1)
        {
            return new EventReductionWitness(
                EventReductionFailureCodes.InvalidEpoch,
                "Internal event batch epoch must advance by one.",
                Dimension: "epoch");
        }

        if (batch.SimulationTime.Milliseconds
            < state.Run.SimulationTime.Milliseconds)
        {
            return new EventReductionWitness(
                EventReductionFailureCodes.InvalidEventTime,
                "Batch simulation time cannot move backwards.",
                Dimension: "simTimeMs");
        }

        if (batch.Events.Count == 0)
        {
            return new EventReductionWitness(
                EventReductionFailureCodes.EmptyBatch,
                "Internal event batch cannot be empty.");
        }

        if (state.NextEventSequence is < 1 or > DomainLimits.MaxCanonicalInteger)
        {
            return new EventReductionWitness(
                EventReductionFailureCodes.InvalidEventSequence,
                "The next internal event sequence is outside the canonical range.",
                Dimension: "eventSeq");
        }

        var expectedSequence = state.NextEventSequence;

        for (var index = 0; index < batch.Events.Count; index++)
        {
            var onlineEvent = batch.Events[index];

            if (onlineEvent.EventSequence != expectedSequence)
            {
                return new EventReductionWitness(
                    EventReductionFailureCodes.InvalidEventSequence,
                    "Internal event sequence must be globally consecutive.",
                    index,
                    onlineEvent.EventSequence,
                    Dimension: "eventSeq");
            }

            if (onlineEvent.EventSequence == DomainLimits.MaxCanonicalInteger)
            {
                return new EventReductionWitness(
                    EventReductionFailureCodes.InvalidEventSequence,
                    "The event sequence exhausts the canonical integer range.",
                    index,
                    onlineEvent.EventSequence,
                    Dimension: "eventSeq");
            }

            if (onlineEvent.SimulationTime != batch.SimulationTime)
            {
                return new EventReductionWitness(
                    EventReductionFailureCodes.InvalidEventTime,
                    "Every internal event must use the batch simulation time.",
                    index,
                    onlineEvent.EventSequence,
                    Dimension: "simTimeMs");
            }

            expectedSequence++;
        }

        return null;
    }

    private static EventApplyResult ApplyEvent(
        RideBoundRun run,
        TravelTimeSnapshot? travelTimes,
        OperationalIncidentLedger incidents,
        string expectedInitialTravelTimeSnapshotHash,
        long epoch,
        OnlineEvent onlineEvent,
        bool recordLatePickups)
    {
        DomainResult<RideBoundRun>? runResult = onlineEvent switch
        {
            RequestArrived arrived => run.AddRequest(arrived.Request),
            BookingConfirmed confirmed =>
                run.ConfirmWaitingPickup(confirmed.RequestId),
            OfferDeclined declined when run.Requests.TryGetValue(
                    declined.RequestId,
                    out var offered)
                && offered.Lifecycle == RequestLifecycle.Accepted =>
                run.CancelAfterAcceptance(declined.RequestId),
            OfferDeclined declined => run.RejectRequest(declined.RequestId),
            RequestCancelledBeforeAcceptance cancelled =>
                run.CancelBeforeAcceptance(cancelled.RequestId),
            RequestCancelledAfterAcceptance cancelled =>
                run.CancelAfterAcceptance(cancelled.RequestId),
            VehicleAdvanced advanced when run.Vehicles.ContainsKey(
                advanced.Observation.Id) =>
                run.ObserveVehicle(advanced.Observation),
            VehicleAdvanced advanced when epoch == 1 =>
                BootstrapVehicle(run, advanced.Observation),
            VehicleAdvanced advanced =>
                DomainResult<RideBoundRun>.Fail(
                    RunFailureCodes.UnknownVehicle,
                    "Unknown vehicle cannot be introduced after bootstrap.",
                    advanced.Observation.Id.Value,
                    "vehicleId"),
            VehicleReachedStop reached =>
                run.ReachStop(
                    reached.VehicleId,
                    reached.StopId,
                    reached.PlanVersion,
                    reached.Position,
                    epoch),
            PassengerBoarded boarded =>
                run.Board(
                    boarded.VehicleId,
                    boarded.RequestId,
                    boarded.PlanVersion,
                    boarded.SimulationTime,
                    recordLatePickups),
            PassengerAlighted alighted =>
                run.Alight(
                    alighted.VehicleId,
                    alighted.RequestId,
                    alighted.PlanVersion),
            TimerTick => DomainResult<RideBoundRun>.Success(run),
            TravelTimesUpdated => null,
            IncidentOpened => null,
            IncidentResolved => null,
            _ => DomainResult<RideBoundRun>.Fail(
                "UNSUPPORTED_INTERNAL_EVENT",
                $"Internal event '{onlineEvent.GetType().Name}' is unsupported."),
        };

        if (runResult is not null && !runResult.IsSuccess)
        {
            return EventApplyResult.Fail(runResult.Failure!);
        }

        // The boarding already succeeded, so the rider was waiting; its accepted window
        // before the event is the contract the lateness is measured against.
        if (recordLatePickups
            && onlineEvent is PassengerBoarded late
            && run.Requests.TryGetValue(late.RequestId, out var waiting)
            && late.SimulationTime.Milliseconds > waiting.LatestPickup.Milliseconds)
        {
            var recorded = incidents.RecordLatePickup(
                new ObservedLatePickup(
                    late.RequestId,
                    late.VehicleId,
                    waiting.LatestPickup,
                    late.SimulationTime,
                    late.EventSequence,
                    epoch));

            if (!recorded.IsSuccess)
            {
                return EventApplyResult.Fail(recorded.Failure!);
            }

            incidents = recorded.Ledger!;
        }

        if (onlineEvent is TravelTimesUpdated travel)
        {
            if (travelTimes is null
                && !string.Equals(
                    travel.Snapshot.SnapshotHash,
                    expectedInitialTravelTimeSnapshotHash,
                    StringComparison.Ordinal))
            {
                return EventApplyResult.Fail(
                    new DomainFailure(
                        EventReductionFailureCodes.TravelSnapshotIdentityMismatch,
                        "Initial travel snapshot hash does not match the run manifest.",
                        Dimension: "snapshotHash"));
            }

            if (travelTimes is not null
                && travel.Snapshot.Version != travelTimes.Version + 1)
            {
                return EventApplyResult.Fail(
                    new DomainFailure(
                        TravelFailureCodes.StaleSnapshot,
                        "Travel snapshot version must advance by exactly one.",
                        Dimension: "version"));
            }

            travelTimes = travel.Snapshot;
        }

        IncidentLedgerResult? incidentResult = onlineEvent switch
        {
            IncidentOpened opened => OpenIncident(run, incidents, opened),
            IncidentResolved resolved => incidents.Resolve(
                resolved.IncidentId,
                resolved.EventSequence,
                resolved.SimulationTime),
            _ => null,
        };

        if (incidentResult is not null && !incidentResult.IsSuccess)
        {
            return EventApplyResult.Fail(incidentResult.Failure!);
        }

        return EventApplyResult.Success(
            runResult?.Value ?? run,
            travelTimes,
            incidentResult?.Ledger ?? incidents);
    }

    private static IncidentLedgerResult OpenIncident(
        RideBoundRun run,
        OperationalIncidentLedger incidents,
        IncidentOpened opened)
    {
        var unknown = opened.VehicleIds.FirstOrDefault(
            vehicleId => !run.Vehicles.ContainsKey(vehicleId));

        if (unknown != default)
        {
            return IncidentLedgerResult.Fail(
                IncidentFailureCodes.UnknownIncidentVehicle,
                "An incident cannot reference an unknown vehicle.",
                unknown.Value,
                "vehicleId");
        }

        var affectedRiders = opened.VehicleIds
            .SelectMany(vehicleId => run.Vehicles[vehicleId].AcceptedRequestIds)
            .Distinct()
            .OrderBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();

        return incidents.Open(
            opened.IncidentId,
            opened.ReasonCode,
            opened.VehicleIds,
            affectedRiders,
            opened.EventSequence,
            opened.SimulationTime);
    }

    private static DomainResult<RideBoundRun> BootstrapVehicle(
        RideBoundRun run,
        VehicleState observation)
    {
        if (observation.OccupiedSeats != 0
            || observation.OnboardRequestIds.Count != 0
            || observation.AcceptedRequestIds.Count != 0
            || observation.Route.RemainingStops.Any(
                stop => stop.RequestId is not null))
        {
            return DomainResult<RideBoundRun>.Fail(
                RunFailureCodes.VehicleRiderMismatch,
                "A genesis vehicle observation cannot preload riders or " +
                "request-owned route stops before a decision accepts them.",
                observation.Id.Value,
                "acceptedRequestIds");
        }

        return run.BootstrapVehicle(observation);
    }

    private static EventReductionWitness ToWitness(
        DomainFailure failure,
        int? index = null,
        long? sequence = null) =>
        new(
            failure.Code,
            failure.Message,
            index,
            sequence,
            failure.EntityId,
            failure.Dimension);

    private sealed record EventApplyResult(
        RideBoundRun? Run,
        TravelTimeSnapshot? TravelTimes,
        OperationalIncidentLedger? Incidents,
        DomainFailure? Failure)
    {
        public static EventApplyResult Success(
            RideBoundRun run,
            TravelTimeSnapshot? travelTimes,
            OperationalIncidentLedger incidents) =>
            new(run, travelTimes, incidents, null);

        public static EventApplyResult Fail(DomainFailure failure) =>
            new(null, null, null, failure);
    }
}
