using RideBound.Algorithms.Candidates;
using RideBound.Application.Optimization;
using RideBound.Application.State;
using RideBound.Domain.Common;

namespace RideBound.Algorithms.Policies;

public enum RequestDecisionOutcome
{
    Accepted,
    Rejected,
    Deferred,
}

public sealed record RequestDecisionAction(
    RequestId RequestId,
    RequestDecisionOutcome Outcome,
    string ReasonCode,
    VehicleId? VehicleId = null,
    string? CandidateId = null);

public sealed record SelectedVehiclePlan(
    VehicleId VehicleId,
    InsertionCandidate Candidate);

public sealed record FleetSelection(
    IReadOnlyList<SelectedVehiclePlan> VehiclePlans,
    int AcceptedRequestCount,
    long OperationalCost);

public sealed record FleetSelectionResult
{
    private FleetSelectionResult(
        FleetSelection? selection,
        RollingCostWitness? witness)
    {
        Selection = selection;
        Witness = witness;
    }

    public bool IsSuccess => Selection is not null;

    public FleetSelection? Selection { get; }

    public RollingCostWitness? Witness { get; }

    public static FleetSelectionResult Success(FleetSelection selection) =>
        new(selection, null);

    public static FleetSelectionResult Failure(RollingCostWitness witness) =>
        new(null, witness);
}

/// <param name="ForcedReferenceVehicles">
/// Vehicles whose gate-rejected safety no-op was kept by forced-reference recovery.
/// Null unless recovery was enabled and triggered; the Runner passes it to its own
/// validation so that the kept route is recorded as a typed breach.
/// </param>
public sealed record DispatchDecision(
    OnlineState ProposedState,
    IReadOnlyList<SelectedVehiclePlan> VehiclePlans,
    IReadOnlyList<RequestDecisionAction> RequestActions,
    IReadOnlyList<CandidatePruneWitness> PrunedCandidates,
    int AcceptedRequestCount,
    long OperationalCost,
    CandidateSelectionExecutionResult? SelectionExecution = null,
    CandidateGenerationDiagnostics? GenerationDiagnostics = null,
    IReadOnlySet<VehicleId>? ForcedReferenceVehicles = null);

public sealed record DispatchDecisionResult
{
    private DispatchDecisionResult(
        DispatchDecision? decision,
        RollingCostWitness? witness)
    {
        Decision = decision;
        Witness = witness;
    }

    public bool IsSuccess => Decision is not null;

    public DispatchDecision? Decision { get; }

    public RollingCostWitness? Witness { get; }

    public static DispatchDecisionResult Success(
        DispatchDecision decision) =>
        new(decision, null);

    public static DispatchDecisionResult Failure(
        RollingCostWitness witness) =>
        new(null, witness);
}

public sealed record RollingCostWitness(
    string Code,
    string Message,
    VehicleId? VehicleId = null,
    RequestId? RequestId = null,
    string? CandidateId = null,
    string? Dimension = null);

public static class DecisionFailureCodes
{
    public const string CandidateGenerationFailed =
        "CANDIDATE_GENERATION_FAILED";
    public const string NoVehiclePlan = "NO_VEHICLE_PLAN";
    public const string OperationalCostOverflow = "OPERATIONAL_COST_OVERFLOW";
    public const string SelectedCandidateInvalid = "SELECTED_CANDIDATE_INVALID";
    public const string DecisionApplyFailed = "DECISION_APPLY_FAILED";
    public const string CommitmentAssessmentFailed =
        "COMMITMENT_ASSESSMENT_FAILED";
}

public static class RollingCostReasonCodes
{
    public const string Accepted = "ACCEPTED";
    public const string NoFeasibleInsertion = "NO_FEASIBLE_INSERTION";
    public const string FleetSelectionConflict = "FLEET_SELECTION_CONFLICT";
}
