using RideBound.Domain.Common;

namespace RideBound.Domain.Commitments;

public sealed record CommitmentVector
{
    public CommitmentVector(
        long pickupEtaTotalMs,
        long dropEtaTotalMs,
        long materialEtaRevisionCount,
        long vehicleSwitchCount,
        long pickupStopRelocationMm,
        long pickupStopSwitchCount,
        long dropStopRelocationMm,
        long dropStopSwitchCount,
        long incumbentOrderInversionCount,
        long prePickupInsertedStopCount)
    {
        var values = new[]
        {
            pickupEtaTotalMs,
            dropEtaTotalMs,
            materialEtaRevisionCount,
            vehicleSwitchCount,
            pickupStopRelocationMm,
            pickupStopSwitchCount,
            dropStopRelocationMm,
            dropStopSwitchCount,
            incumbentOrderInversionCount,
            prePickupInsertedStopCount,
        };

        if (values.Any(value => value is < 0 or > DomainLimits.MaxCanonicalInteger))
        {
            throw new ArgumentOutOfRangeException(
                nameof(pickupEtaTotalMs),
                "Commitment vector values must be canonical non-negative integers.");
        }

        PickupEtaTotalMs = pickupEtaTotalMs;
        DropEtaTotalMs = dropEtaTotalMs;
        MaterialEtaRevisionCount = materialEtaRevisionCount;
        VehicleSwitchCount = vehicleSwitchCount;
        PickupStopRelocationMm = pickupStopRelocationMm;
        PickupStopSwitchCount = pickupStopSwitchCount;
        DropStopRelocationMm = dropStopRelocationMm;
        DropStopSwitchCount = dropStopSwitchCount;
        IncumbentOrderInversionCount = incumbentOrderInversionCount;
        PrePickupInsertedStopCount = prePickupInsertedStopCount;
    }

    public long PickupEtaTotalMs { get; }

    public long DropEtaTotalMs { get; }

    public long MaterialEtaRevisionCount { get; }

    public long VehicleSwitchCount { get; }

    public long PickupStopRelocationMm { get; }

    public long PickupStopSwitchCount { get; }

    public long DropStopRelocationMm { get; }

    public long DropStopSwitchCount { get; }

    public long IncumbentOrderInversionCount { get; }

    public long PrePickupInsertedStopCount { get; }

    public static CommitmentVector Zero { get; } =
        new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public long Get(CommitmentDimension dimension) =>
        dimension switch
        {
            CommitmentDimension.PickupEtaTotalMs => PickupEtaTotalMs,
            CommitmentDimension.DropEtaTotalMs => DropEtaTotalMs,
            CommitmentDimension.MaterialEtaRevisionCount =>
                MaterialEtaRevisionCount,
            CommitmentDimension.VehicleSwitchCount => VehicleSwitchCount,
            CommitmentDimension.PickupStopRelocationMm =>
                PickupStopRelocationMm,
            CommitmentDimension.PickupStopSwitchCount =>
                PickupStopSwitchCount,
            CommitmentDimension.DropStopRelocationMm =>
                DropStopRelocationMm,
            CommitmentDimension.DropStopSwitchCount => DropStopSwitchCount,
            CommitmentDimension.IncumbentOrderInversionCount =>
                IncumbentOrderInversionCount,
            CommitmentDimension.PrePickupInsertedStopCount =>
                PrePickupInsertedStopCount,
            _ => throw new ArgumentOutOfRangeException(nameof(dimension)),
        };

    public DomainResult<CommitmentVector> Add(CommitmentVector delta)
    {
        ArgumentNullException.ThrowIfNull(delta);
        var values = new long[CommitmentDimensionVocabulary.Ordered.Count];

        for (var index = 0; index < values.Length; index++)
        {
            var dimension = CommitmentDimensionVocabulary.Ordered[index];
            var before = Get(dimension);
            var addition = delta.Get(dimension);

            if (before > DomainLimits.MaxCanonicalInteger - addition)
            {
                return DomainResult<CommitmentVector>.Fail(
                    CommitmentFailureCodes.VectorOverflow,
                    "Commitment vector addition exceeds the canonical integer range.",
                    dimension: CommitmentDimensionVocabulary.ToProtocolValue(
                        dimension));
            }

            values[index] = before + addition;
        }

        return DomainResult<CommitmentVector>.Success(
            new CommitmentVector(
                values[0],
                values[1],
                values[2],
                values[3],
                values[4],
                values[5],
                values[6],
                values[7],
                values[8],
                values[9]));
    }
}

public static class CommitmentFailureCodes
{
    public const string VectorOverflow = "COMMITMENT_VECTOR_OVERFLOW";
    public const string InvalidPromise = "INVALID_PROMISE";
    public const string PromiseProjectionFailed = "PROMISE_PROJECTION_FAILED";
    public const string StopDistanceRequired = "STOP_DISTANCE_REQUIRED";
    public const string LedgerConflict = "COMMITMENT_LEDGER_CONFLICT";
    public const string BudgetExceeded = "COMMITMENT_BUDGET_EXCEEDED";
    public const string PhaseLock = "COMMITMENT_PHASE_LOCK";

    /// <summary>
    /// The candidate moves the drop ETA outside the deadline measured from the
    /// rider's initial promise. Unlike a phase lock, which compares with the
    /// exogenous projection, a deadline can be tripped by exogenous drift alone.
    /// </summary>
    public const string DeadlineExceeded = "COMMITMENT_DEADLINE_EXCEEDED";

    /// <summary>
    /// A treatment assessment rejected every generated candidate for one
    /// vehicle, including the safety no-op. This is fail-closed: the run stops
    /// with an explicit witness instead of handing the solver a vehicle that has
    /// no selectable option.
    /// </summary>
    public const string VehicleHasNoFeasibleCandidate =
        "C1_VEHICLE_HAS_NO_FEASIBLE_CANDIDATE";

    /// <summary>
    /// A treatment assessment rejected the safety no-op of a vehicle while other
    /// candidates survived. Reported only to a caller whose selection model
    /// requires exactly one no-op per vehicle, where this state cannot be solved;
    /// it replaces a later generic mapping error. The no-op may have been rejected
    /// at any validation stage, so the cited rejection, not this code, names the
    /// cause.
    /// </summary>
    public const string SafetyNoOpRejected = "COMMITMENT_SAFETY_NOOP_REJECTED";
}
