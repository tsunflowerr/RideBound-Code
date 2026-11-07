using RideBound.Application.Travel;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Incidents;
using RideBound.Domain.Runs;

namespace RideBound.Application.State;

public sealed record OnlineState(
    RideBoundRun Run,
    TravelTimeSnapshot? TravelTimes,
    long NextEventSequence,
    string ExpectedInitialTravelTimeSnapshotHash,
    CommitmentLedger Commitments,
    OperationalIncidentLedger Incidents)
{
    public OnlineState(
        RideBoundRun run,
        TravelTimeSnapshot? travelTimes,
        long nextEventSequence,
        string expectedInitialTravelTimeSnapshotHash,
        CommitmentLedger commitments)
        : this(
            run,
            travelTimes,
            nextEventSequence,
            expectedInitialTravelTimeSnapshotHash,
            commitments,
            OperationalIncidentLedger.Empty)
    {
    }

    public static OnlineState Create(
        RideBoundRun run,
        string expectedInitialTravelTimeSnapshotHash)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (expectedInitialTravelTimeSnapshotHash is not { Length: 64 }
            || !expectedInitialTravelTimeSnapshotHash.All(
                character => character is >= '0' and <= '9'
                    or >= 'a' and <= 'f'))
        {
            throw new ArgumentException(
                "Expected travel snapshot hash must be lowercase SHA-256.",
                nameof(expectedInitialTravelTimeSnapshotHash));
        }

        return new OnlineState(
            run,
            null,
            1,
            expectedInitialTravelTimeSnapshotHash,
            CommitmentLedger.Empty,
            OperationalIncidentLedger.Empty);
    }
}

public sealed record EventReductionWitness(
    string Code,
    string Message,
    int? EventIndex = null,
    long? EventSequence = null,
    string? EntityId = null,
    string? Dimension = null);

public sealed record EventReductionResult
{
    private EventReductionResult(
        OnlineState? proposedState,
        EventReductionWitness? witness)
    {
        ProposedState = proposedState;
        Witness = witness;
    }

    public bool IsSuccess => ProposedState is not null;

    public OnlineState? ProposedState { get; }

    public EventReductionWitness? Witness { get; }

    public static EventReductionResult Success(OnlineState state) =>
        new(state, null);

    public static EventReductionResult Failure(EventReductionWitness witness) =>
        new(null, witness);
}

public static class EventReductionFailureCodes
{
    public const string IdentityMismatch = "IDENTITY_MISMATCH";
    public const string InvalidEpoch = "INVALID_EPOCH";
    public const string InvalidEventSequence = "INVALID_EVENT_SEQUENCE";
    public const string InvalidEventTime = "INVALID_EVENT_TIME";
    public const string EmptyBatch = "EMPTY_EVENT_BATCH";
    public const string TravelSnapshotRequired = "TRAVEL_SNAPSHOT_REQUIRED";
    public const string TravelSnapshotIdentityMismatch =
        "TRAVEL_SNAPSHOT_IDENTITY_MISMATCH";
    public const string VehicleBootstrapRequired = "VEHICLE_BOOTSTRAP_REQUIRED";
    public const string PendingTransitionExists = "PENDING_TRANSITION_EXISTS";
    public const string NoPendingTransition = "NO_PENDING_TRANSITION";
    public const string AcknowledgementMismatch = "ACKNOWLEDGEMENT_MISMATCH";
    public const string DecisionStateMismatch = "DECISION_STATE_MISMATCH";
}
