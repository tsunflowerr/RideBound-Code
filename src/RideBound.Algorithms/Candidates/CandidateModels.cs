using RideBound.Application.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Routes;
using RideBound.Domain.Validation;

namespace RideBound.Algorithms.Candidates;

public sealed record ScheduledStop(
    StopId StopId,
    SimTime ArrivalTime,
    SimTime ServiceStartTime,
    SimTime DepartureTime);

public sealed record CandidateSchedule(
    IReadOnlyList<ScheduledStop> Stops,
    long OperationalCost);

public enum CandidateScheduleStrategy
{
    EarliestFeasible,
    OriginHoldRelocatedWait,
}

public enum CandidateRetentionStrategy
{
    LegacyAcceptedCountCostSlack,
    ServiceSetStabilityPortfolioV1,
}

/// <summary>
/// The side of a waiting-rider relocation (G2) a candidate stands for: the donor route
/// drops the rider, the receiver route carries it. Only a donor and a receiver of the
/// same rider together form a move.
/// </summary>
public enum RelocationRole
{
    Donor,
    Receiver,
}

/// <param name="RelocatedRequestId">
/// The waiting rider a G2 candidate moves; null for every ordinary candidate.
/// </param>
/// <param name="RelocationRole">
/// Donor or receiver of that move; null exactly when <paramref name="RelocatedRequestId"/> is.
/// </param>
public sealed record InsertionCandidate(
    string CandidateId,
    VehicleId VehicleId,
    RoutePlan Route,
    IReadOnlyList<RequestId> NewRequestIds,
    CandidateSchedule Schedule,
    bool IsNoOp,
    CandidateScheduleStrategy ScheduleStrategy =
        CandidateScheduleStrategy.EarliestFeasible,
    long RelocatedWaitMilliseconds = 0,
    long? CertifiedForwardSlackMilliseconds = null,
    RequestId? RepairedIncumbentRequestId = null,
    RequestId? RelocatedRequestId = null,
    RelocationRole? RelocationRole = null);

public sealed record CandidatePruneWitness(
    string CandidateId,
    VehicleId VehicleId,
    IReadOnlyList<RequestId> NewRequestIds,
    string Code,
    string Message,
    PhysicalViolationWitness? PhysicalWitness = null,
    IReadOnlyList<CommitmentValidationWitness>? CommitmentWitnesses = null);

public sealed record VehicleCandidateSet(
    VehicleId VehicleId,
    IReadOnlyList<InsertionCandidate> Candidates,
    IReadOnlyList<CandidatePruneWitness> PrunedCandidates,
    bool WasTruncated,
    VehicleCandidateLoss? Loss = null);

public sealed record CandidateOmissionWitness(
    string Code,
    long Count,
    string StableDigest,
    string Message,
    VehicleId? VehicleId = null,
    IReadOnlyList<RequestId>? RequestIds = null,
    bool CountWasSaturated = false);

public sealed record VehicleCandidateLoss(
    long ExplorationWorkUnits,
    long EvaluatedCandidatePathCount,
    long UniqueFeasibleCandidateCountBeforeCap,
    long RetainedCandidateCount,
    long PhysicallyOrSchedulePrunedCount,
    long OmittedUnexpandedCandidatePathCount,
    long OmittedFeasibleCandidateCountByCap,
    bool WorkBudgetExhausted,
    bool CandidateCapApplied,
    bool OmissionCountWasSaturated = false,
    long EligibleRepairRequestCount = 0,
    long ConsideredRepairRequestCount = 0,
    long OmittedRepairRequestCount = 0,
    VehicleId? VehicleId = null);

/// <summary>
/// G2 accounting for one donor vehicle with at least one eligible waiting rider. Kept apart
/// from <see cref="VehicleCandidateLoss"/>, whose meaning stays that of the ordinary search.
/// </summary>
public sealed record RelocationCandidateLoss(
    VehicleId DonorVehicleId,
    long EligibleRiderCount,
    long ConsideredRiderCount,
    long OmittedRiderCount,
    long WorkUnits,
    long EvaluatedReceiverRouteCount,
    long FeasibleReceiverCount,
    long EvaluatedDonorPathCount,
    long FeasibleDonorCount,
    long OmittedUnexpandedDonorPathCount,
    bool OmissionCountWasSaturated,
    long RidersWithoutFeasibleReceiver,
    long RidersWithInfeasibleDonorRoute);

/// <summary>
/// A service-quality deadline the vehicle's unchanged active route can no longer
/// meet under the current travel snapshot (ADR-045). It is attributed to traffic,
/// not to the decision about to be taken, and it never removes the safety no-op.
/// <see cref="ExogenousMilliseconds"/> is the exact no-op observation. ADR-047
/// keeps every changed candidate strictly contractual; only the unchanged
/// safety route may use this observation as its relaxed bound.
/// </summary>
public sealed record ExogenousServiceQualityBreach(
    VehicleId VehicleId,
    RequestId RequestId,
    string Code,
    string Dimension,
    long ContractualMilliseconds,
    long ExogenousMilliseconds);

public sealed record CandidateGenerationDiagnostics(
    long TotalPendingRequestCount,
    long ConsideredRequestCount,
    long OmittedRequestCount,
    IReadOnlyList<VehicleCandidateLoss> VehicleLosses,
    IReadOnlyList<CandidateOmissionWitness> Omissions,
    /// <summary>
    /// Exogenous service-quality breaches observed this epoch, ordered by
    /// vehicle, then request, then dimension. These are diagnostic: they never
    /// prune a candidate and never fail the epoch.
    /// </summary>
    IReadOnlyList<ExogenousServiceQualityBreach> ExogenousServiceQualityBreaches,
    /// <summary>
    /// G2 accounting by donor vehicle; null when waiting-rider relocation is off.
    /// </summary>
    IReadOnlyList<RelocationCandidateLoss>? RelocationLosses = null)
{

    public bool IsComplete => OmittedRequestCount == 0
        && VehicleLosses.All(
            loss => loss.OmittedUnexpandedCandidatePathCount == 0
                && loss.OmittedFeasibleCandidateCountByCap == 0
                && loss.OmittedRepairRequestCount == 0)
        && (RelocationLosses is null
            || RelocationLosses.All(
                loss => loss.OmittedRiderCount == 0
                    && loss.OmittedUnexpandedDonorPathCount == 0));
}

public sealed record CandidateGenerationResult
{
    private CandidateGenerationResult(
        IReadOnlyList<VehicleCandidateSet>? vehicleCandidates,
        CandidateGenerationDiagnostics? diagnostics,
        CandidateGenerationWitness? witness)
    {
        VehicleCandidates = vehicleCandidates;
        Diagnostics = diagnostics;
        Witness = witness;
    }

    public bool IsSuccess => VehicleCandidates is not null;

    public IReadOnlyList<VehicleCandidateSet>? VehicleCandidates { get; }

    public CandidateGenerationDiagnostics? Diagnostics { get; }

    public CandidateGenerationWitness? Witness { get; }

    public static CandidateGenerationResult Success(
        IReadOnlyList<VehicleCandidateSet> vehicleCandidates,
        CandidateGenerationDiagnostics diagnostics) =>
        new(vehicleCandidates, diagnostics, null);

    public static CandidateGenerationResult Failure(
        CandidateGenerationWitness witness) =>
        new(null, null, witness);
}

public sealed record CandidateGenerationWitness(
    string Code,
    string Message,
    VehicleId? VehicleId = null,
    RequestId? RequestId = null,
    string? Dimension = null);

public sealed record CandidateGenerationOptions
{
    public CandidateGenerationOptions(
        int maximumCandidatesPerVehicle,
        int maximumNewRequestsPerVehicle,
        bool exactSmallMode,
        CandidateScheduleStrategy scheduleStrategy =
            CandidateScheduleStrategy.EarliestFeasible,
        long maximumExplorationWorkUnits = 100_000,
        int maximumRepairRequestsConsideredPerVehicle = 0,
        CandidateRetentionStrategy retentionStrategy =
            CandidateRetentionStrategy.LegacyAcceptedCountCostSlack,
        WaitingRiderRelocationGenerationOptions? waitingRiderRelocation = null,
        long stopServiceDurationMs = 0)
    {
        if (maximumCandidatesPerVehicle < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCandidatesPerVehicle));
        }

        if (maximumNewRequestsPerVehicle < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumNewRequestsPerVehicle));
        }

        if (!Enum.IsDefined(scheduleStrategy))
        {
            throw new ArgumentOutOfRangeException(nameof(scheduleStrategy));
        }

        if (maximumExplorationWorkUnits is < 1
            or > DomainLimits.MaxCanonicalInteger)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumExplorationWorkUnits));
        }

        if (maximumRepairRequestsConsideredPerVehicle < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumRepairRequestsConsideredPerVehicle));
        }

        if (!Enum.IsDefined(retentionStrategy))
        {
            throw new ArgumentOutOfRangeException(nameof(retentionStrategy));
        }

        // G2 candidates use the earliest-feasible schedule only; an origin-hold transform
        // would change their routes and identities on one side of the move.
        if (waitingRiderRelocation is not null
            && scheduleStrategy != CandidateScheduleStrategy.EarliestFeasible)
        {
            throw new ArgumentException(
                "Waiting-rider relocation requires the earliest-feasible schedule strategy.",
                nameof(waitingRiderRelocation));
        }

        MaximumCandidatesPerVehicle = maximumCandidatesPerVehicle;
        MaximumNewRequestsPerVehicle = maximumNewRequestsPerVehicle;
        ExactSmallMode = exactSmallMode;
        ScheduleStrategy = scheduleStrategy;
        MaximumExplorationWorkUnits = maximumExplorationWorkUnits;
        MaximumRepairRequestsConsideredPerVehicle =
            maximumRepairRequestsConsideredPerVehicle;
        if (stopServiceDurationMs is < 0 or > DomainLimits.MaxCanonicalInteger)
        {
            throw new ArgumentOutOfRangeException(nameof(stopServiceDurationMs));
        }

        RetentionStrategy = retentionStrategy;
        WaitingRiderRelocation = waitingRiderRelocation;
        StopServiceDuration = new Duration(stopServiceDurationMs);
    }

    public int MaximumCandidatesPerVehicle { get; }

    public int MaximumNewRequestsPerVehicle { get; }

    public bool ExactSmallMode { get; }

    public CandidateScheduleStrategy ScheduleStrategy { get; }

    public long MaximumExplorationWorkUnits { get; }

    /// <summary>
    /// Zero disables the B4 neighborhood. A positive value bounds how many
    /// eligible waiting incumbents may each seed one atomic pair-repair move.
    /// </summary>
    public int MaximumRepairRequestsConsideredPerVehicle { get; }

    public CandidateRetentionStrategy RetentionStrategy { get; }

    /// <summary>
    /// Null disables G2. Otherwise every eligible waiting rider (up to the cap) seeds donor
    /// and receiver candidates after the ordinary per-vehicle search.
    /// </summary>
    public WaitingRiderRelocationGenerationOptions? WaitingRiderRelocation { get; }

    /// <summary>
    /// Service duration of every pickup and drop stop the generator inserts (dwell). Zero, the
    /// default, keeps every earlier candidate, route and identity unchanged; a positive value lets
    /// the Runner plan a non-zero boarding time (bridge step 1.3, opt-in).
    /// </summary>
    public Duration StopServiceDuration { get; }

    public CandidateGenerationOptions WithRepairRequestCap(int maximumRequests) =>
        new(
            MaximumCandidatesPerVehicle,
            MaximumNewRequestsPerVehicle,
            ExactSmallMode,
            ScheduleStrategy,
            MaximumExplorationWorkUnits,
            maximumRequests,
            RetentionStrategy,
            WaitingRiderRelocation,
            StopServiceDuration.Milliseconds);

    public static CandidateGenerationOptions ExactSmall { get; } =
        new(
            maximumCandidatesPerVehicle: 100_000,
            maximumNewRequestsPerVehicle: 2,
            exactSmallMode: true);
}

/// <summary>
/// G2 generation bounds. A null cap means every eligible waiting rider of a donor, and every
/// other vehicle as a receiver; the k nearest targets are ranked by travel time from the
/// vehicle's schedule start node to the rider's origin, ties by ordinal vehicle id.
/// </summary>
public sealed record WaitingRiderRelocationGenerationOptions
{
    public WaitingRiderRelocationGenerationOptions(
        int? maximumRidersPerVehicle = null,
        int? maximumTargetVehicles = null)
    {
        if (maximumRidersPerVehicle is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRidersPerVehicle));
        }

        if (maximumTargetVehicles is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumTargetVehicles));
        }

        MaximumRidersPerVehicle = maximumRidersPerVehicle;
        MaximumTargetVehicles = maximumTargetVehicles;
    }

    public int? MaximumRidersPerVehicle { get; }

    public int? MaximumTargetVehicles { get; }
}

public static class CandidateGenerationFailureCodes
{
    public const string ActiveRouteInfeasible = "ACTIVE_ROUTE_INFEASIBLE";
    public const string TravelSnapshotRequired = "TRAVEL_SNAPSHOT_REQUIRED";
    public const string ExactSmallRequestBoundExceeded =
        "EXACT_SMALL_REQUEST_BOUND_EXCEEDED";
    public const string ExactSmallCandidateCapExceeded =
        "EXACT_SMALL_CANDIDATE_CAP_EXCEEDED";
    public const string ExactSmallWorkCapExceeded =
        "EXACT_SMALL_WORK_CAP_EXCEEDED";
    public const string PlanVersionOverflow = "PLAN_VERSION_OVERFLOW";
    public const string ScheduleEvaluationFailed = "SCHEDULE_EVALUATION_FAILED";
    public const string SlackProfileFailed = "SLACK_PROFILE_FAILED";
    public const string ScheduleStrategyEquivalenceFailed =
        "SCHEDULE_STRATEGY_EQUIVALENCE_FAILED";
    public const string RequestBoundOmission = "REQUEST_BOUND_OMISSION";
    public const string WorkBoundOmission = "WORK_BOUND_OMISSION";
    public const string CandidateCapOmission = "CANDIDATE_CAP_OMISSION";
    public const string RepairRequestBoundOmission =
        "REPAIR_REQUEST_BOUND_OMISSION";
    public const string ExactSmallRepairRequestBoundExceeded =
        "EXACT_SMALL_REPAIR_REQUEST_BOUND_EXCEEDED";
    public const string RelocationRiderBoundOmission =
        "RELOCATION_RIDER_BOUND_OMISSION";
    public const string RelocationWorkBoundOmission =
        "RELOCATION_WORK_BOUND_OMISSION";
    public const string ExactSmallRelocationRiderBoundExceeded =
        "EXACT_SMALL_RELOCATION_RIDER_BOUND_EXCEEDED";
    public const string ExactSmallRelocationWorkCapExceeded =
        "EXACT_SMALL_RELOCATION_WORK_CAP_EXCEEDED";
}
