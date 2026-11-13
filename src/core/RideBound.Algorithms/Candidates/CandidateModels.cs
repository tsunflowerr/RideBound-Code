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
    long? CertifiedForwardSlackMilliseconds = null,
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
    bool OmissionCountWasSaturated = false);

/// <summary>
/// G2 accounting for one donor vehicle with at least one eligible waiting rider. Kept apart
/// from <see cref="VehicleCandidateLoss"/>, whose meaning stays that of the ordinary search.
/// </summary>
public sealed record RelocationCandidateLoss(
    VehicleId DonorVehicleId,
    long EligibleRiderCount,
    long WorkUnits,
    long EvaluatedReceiverRouteCount,
    long FeasibleReceiverCount,
    long FeasibleDonorCount,
    long OmittedUnexpandedDonorPathCount,
    bool OmissionCountWasSaturated,
    long RidersWithoutFeasibleReceiver);

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
                && loss.OmittedFeasibleCandidateCountByCap == 0)
        && (RelocationLosses is null
            || RelocationLosses.All(loss => loss.OmittedUnexpandedDonorPathCount == 0));
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
        long maximumExplorationWorkUnits = 100_000,
        WaitingRiderRelocationGenerationOptions? waitingRiderRelocation = null)
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

        if (maximumExplorationWorkUnits is < 1
            or > DomainLimits.MaxCanonicalInteger)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumExplorationWorkUnits));
        }

        MaximumCandidatesPerVehicle = maximumCandidatesPerVehicle;
        MaximumNewRequestsPerVehicle = maximumNewRequestsPerVehicle;
        MaximumExplorationWorkUnits = maximumExplorationWorkUnits;
        WaitingRiderRelocation = waitingRiderRelocation;
    }

    public int MaximumCandidatesPerVehicle { get; }

    public int MaximumNewRequestsPerVehicle { get; }

    public long MaximumExplorationWorkUnits { get; }

    /// <summary>
    /// Null disables G2. Otherwise every eligible waiting rider seeds donor and receiver
    /// candidates after the ordinary per-vehicle search.
    /// </summary>
    public WaitingRiderRelocationGenerationOptions? WaitingRiderRelocation { get; }
}

/// <summary>
/// G2 generation bounds. Every eligible waiting rider of a donor is considered. A null target cap
/// means every other vehicle as a receiver; the k nearest targets are ranked by travel time from
/// the vehicle's schedule start node to the rider's origin, ties by ordinal vehicle id.
/// </summary>
public sealed record WaitingRiderRelocationGenerationOptions
{
    public WaitingRiderRelocationGenerationOptions(int? maximumTargetVehicles = null)
    {
        if (maximumTargetVehicles is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumTargetVehicles));
        }

        MaximumTargetVehicles = maximumTargetVehicles;
    }

    public int? MaximumTargetVehicles { get; }
}

public static class CandidateGenerationFailureCodes
{
    public const string ActiveRouteInfeasible = "ACTIVE_ROUTE_INFEASIBLE";
    public const string TravelSnapshotRequired = "TRAVEL_SNAPSHOT_REQUIRED";
    public const string PlanVersionOverflow = "PLAN_VERSION_OVERFLOW";
    public const string ScheduleEvaluationFailed = "SCHEDULE_EVALUATION_FAILED";
    public const string SlackProfileFailed = "SLACK_PROFILE_FAILED";
    public const string RequestBoundOmission = "REQUEST_BOUND_OMISSION";
    public const string WorkBoundOmission = "WORK_BOUND_OMISSION";
    public const string CandidateCapOmission = "CANDIDATE_CAP_OMISSION";
    public const string RelocationWorkBoundOmission =
        "RELOCATION_WORK_BOUND_OMISSION";
}
