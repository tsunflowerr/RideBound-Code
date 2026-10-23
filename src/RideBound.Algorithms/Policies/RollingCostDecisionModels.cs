using RideBound.Algorithms.Candidates;
using RideBound.Application.Optimization;
using RideBound.Application.State;
using RideBound.Domain.Commitments;
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
    long OperationalCost,
    CommitmentVector? DecisionInducedRevision = null,
    long? WorstHardUtilizationPartsPerMillion = null,
    CommitmentVector? WarningExcess = null);

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
public sealed record RollingCostDecision(
    OnlineState ProposedState,
    IReadOnlyList<SelectedVehiclePlan> VehiclePlans,
    IReadOnlyList<RequestDecisionAction> RequestActions,
    IReadOnlyList<CandidatePruneWitness> PrunedCandidates,
    int AcceptedRequestCount,
    long OperationalCost,
    CommitmentVector? DecisionInducedRevision = null,
    long? WorstHardUtilizationPartsPerMillion = null,
    CommitmentVector? WarningExcess = null,
    CandidateSelectionExecutionResult? SelectionExecution = null,
    CandidateGenerationDiagnostics? GenerationDiagnostics = null,
    CandidatePortfolioEvidenceSnapshot? CandidatePortfolioEvidence = null,
    IReadOnlySet<VehicleId>? ForcedReferenceVehicles = null);

public sealed class CandidatePortfolioEvidenceSnapshot
{
    private CandidatePortfolioEvidenceSnapshot(
        SolverBackedObjectiveProfile objectiveProfile,
        IReadOnlyList<VehicleCandidateSet> generatedPhysicalCandidateSets,
        IReadOnlyList<VehicleCandidateSet> policyEligibleCandidateSets,
        CandidateSelectionProblem selectionProblem,
        IReadOnlyList<string> selectedCandidateIds)
    {
        ObjectiveProfile = objectiveProfile;
        GeneratedPhysicalCandidateSets = generatedPhysicalCandidateSets;
        PolicyEligibleCandidateSets = policyEligibleCandidateSets;
        SelectionProblem = selectionProblem;
        SelectedCandidateIds = selectedCandidateIds;
    }

    public SolverBackedObjectiveProfile ObjectiveProfile { get; }

    public IReadOnlyList<VehicleCandidateSet> GeneratedPhysicalCandidateSets
    {
        get;
    }

    public IReadOnlyList<VehicleCandidateSet> PolicyEligibleCandidateSets
    {
        get;
    }

    public CandidateSelectionProblem SelectionProblem { get; }

    public IReadOnlyList<string> SelectedCandidateIds { get; }

    public static DomainResult<CandidatePortfolioEvidenceSnapshot> Create(
        SolverBackedObjectiveProfile objectiveProfile,
        IReadOnlyList<VehicleCandidateSet> generatedPhysicalCandidateSets,
        IReadOnlyList<VehicleCandidateSet> policyEligibleCandidateSets,
        CandidateSelectionProblem selectionProblem,
        IReadOnlyList<string> selectedCandidateIds)
    {
        ArgumentNullException.ThrowIfNull(generatedPhysicalCandidateSets);
        ArgumentNullException.ThrowIfNull(policyEligibleCandidateSets);
        ArgumentNullException.ThrowIfNull(selectionProblem);
        ArgumentNullException.ThrowIfNull(selectedCandidateIds);

        if (!Enum.IsDefined(objectiveProfile))
        {
            return Failure("Objective profile is unknown.", "objectiveProfile");
        }

        var generatedSets = generatedPhysicalCandidateSets
            .OrderBy(value => value.VehicleId.Value, StringComparer.Ordinal)
            .ToArray();
        var eligibleSets = policyEligibleCandidateSets
            .OrderBy(value => value.VehicleId.Value, StringComparer.Ordinal)
            .ToArray();

        if (generatedSets.Length == 0
            || generatedSets.Select(value => value.VehicleId).Distinct().Count()
                != generatedSets.Length
            || eligibleSets.Select(value => value.VehicleId).Distinct().Count()
                != eligibleSets.Length
            || !eligibleSets.Select(value => value.VehicleId)
                .SequenceEqual(generatedSets.Select(value => value.VehicleId))
            || generatedSets.Any(
                set => set.Candidates.Any(
                    candidate => candidate.VehicleId != set.VehicleId))
            || eligibleSets.Any(
                set => set.Candidates.Any(
                    candidate => candidate.VehicleId != set.VehicleId)))
        {
            return Failure(
                "Generated and eligible portfolios need the same unique vehicles.",
                "vehicleId");
        }

        var generated = generatedSets
            .SelectMany(value => value.Candidates)
            .ToArray();
        var eligible = eligibleSets
            .SelectMany(value => value.Candidates)
            .ToArray();
        var generatedById = generated
            .GroupBy(value => value.CandidateId, StringComparer.Ordinal)
            .ToDictionary(
                value => value.Key,
                value => value.ToArray(),
                StringComparer.Ordinal);
        var eligibleById = eligible
            .GroupBy(value => value.CandidateId, StringComparer.Ordinal)
            .ToDictionary(
                value => value.Key,
                value => value.ToArray(),
                StringComparer.Ordinal);

        if (generated.Length == 0
            || generatedById.Any(value => value.Value.Length != 1)
            || eligibleById.Any(value => value.Value.Length != 1)
            || eligibleById.Any(
                value => !generatedById.TryGetValue(value.Key, out var source)
                    || !ReferenceEquals(source[0], value.Value[0])))
        {
            return Failure(
                "Eligible candidates must be an identity-preserving subset of "
                + "the unique generated portfolio.",
                "candidateId");
        }

        var problemOptions = selectionProblem.Options.ToDictionary(
            value => value.OptionId,
            StringComparer.Ordinal);

        if (problemOptions.Count != selectionProblem.Options.Count
            || !problemOptions.Keys.ToHashSet(StringComparer.Ordinal)
                .SetEquals(eligibleById.Keys))
        {
            return Failure(
                "Selection problem options must equal the eligible portfolio.",
                "optionId");
        }

        foreach (var (candidateId, values) in eligibleById)
        {
            var candidate = values[0];
            var option = problemOptions[candidateId];

            if (option.VehicleId != candidate.VehicleId
                || option.IsNoOp != candidate.IsNoOp
                || option.RelocationOut != (candidate.RelocationRole == RelocationRole.Donor
                    ? candidate.RelocatedRequestId
                    : null)
                || option.RelocationIn != (candidate.RelocationRole == RelocationRole.Receiver
                    ? candidate.RelocatedRequestId
                    : null)
                || !option.RequestIds.SequenceEqual(
                    candidate.NewRequestIds.OrderBy(
                        value => value.Value,
                        StringComparer.Ordinal)))
            {
                return Failure(
                    "Selection option identity differs from its candidate.",
                    "optionId",
                    candidateId);
            }
        }

        var selected = CandidateSelectionSolution.Create(
            selectionProblem,
            selectedCandidateIds);

        if (!selected.IsSuccess)
        {
            return Failure(
                "Selected candidate IDs must form an exact feasible solution.",
                "selectedCandidateIds");
        }

        var candidateCopies = generated.ToDictionary(
            value => value.CandidateId,
            CopyCandidate,
            StringComparer.Ordinal);
        var capturedGeneratedSets = generatedSets
            .Select(value => CopySet(value, candidateCopies))
            .ToArray();
        var capturedEligibleSets = eligibleSets
            .Select(value => CopySet(value, candidateCopies))
            .ToArray();

        return DomainResult<CandidatePortfolioEvidenceSnapshot>.Success(
            new CandidatePortfolioEvidenceSnapshot(
                objectiveProfile,
                Array.AsReadOnly(capturedGeneratedSets),
                Array.AsReadOnly(capturedEligibleSets),
                selectionProblem,
                selected.Value!.SelectedOptionIds));
    }

    private static VehicleCandidateSet CopySet(
        VehicleCandidateSet source,
        IReadOnlyDictionary<string, InsertionCandidate> candidates) =>
        new(
            source.VehicleId,
            Array.AsReadOnly(
                source.Candidates
                    .Select(value => candidates[value.CandidateId])
                    .ToArray()),
            Array.Empty<CandidatePruneWitness>(),
            source.WasTruncated,
            source.Loss);

    private static InsertionCandidate CopyCandidate(InsertionCandidate source) =>
        source with
        {
            NewRequestIds = Array.AsReadOnly(source.NewRequestIds.ToArray()),
            Schedule = source.Schedule with
            {
                Stops = Array.AsReadOnly(source.Schedule.Stops.ToArray()),
            },
        };

    private static DomainResult<CandidatePortfolioEvidenceSnapshot> Failure(
        string message,
        string dimension,
        string? entityId = null) =>
        DomainResult<CandidatePortfolioEvidenceSnapshot>.Fail(
            RollingCostFailureCodes.CandidatePortfolioEvidenceInvalid,
            message,
            entityId,
            dimension);
}

public sealed record RollingCostDecisionResult
{
    private RollingCostDecisionResult(
        RollingCostDecision? decision,
        RollingCostWitness? witness)
    {
        Decision = decision;
        Witness = witness;
    }

    public bool IsSuccess => Decision is not null;

    public RollingCostDecision? Decision { get; }

    public RollingCostWitness? Witness { get; }

    public static RollingCostDecisionResult Success(
        RollingCostDecision decision) =>
        new(decision, null);

    public static RollingCostDecisionResult Failure(
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

public static class RollingCostFailureCodes
{
    public const string CandidateGenerationFailed =
        "CANDIDATE_GENERATION_FAILED";
    public const string NoVehiclePlan = "NO_VEHICLE_PLAN";
    public const string OperationalCostOverflow = "OPERATIONAL_COST_OVERFLOW";
    public const string SelectedCandidateInvalid = "SELECTED_CANDIDATE_INVALID";
    public const string DecisionApplyFailed = "DECISION_APPLY_FAILED";
    public const string CommitmentAssessmentFailed =
        "COMMITMENT_ASSESSMENT_FAILED";
    public const string CandidatePortfolioEvidenceInvalid =
        "CANDIDATE_PORTFOLIO_EVIDENCE_INVALID";
}

public static class RollingCostReasonCodes
{
    public const string Accepted = "ACCEPTED";
    public const string NoFeasibleInsertion = "NO_FEASIBLE_INSERTION";
    public const string FleetSelectionConflict = "FLEET_SELECTION_CONFLICT";
}
