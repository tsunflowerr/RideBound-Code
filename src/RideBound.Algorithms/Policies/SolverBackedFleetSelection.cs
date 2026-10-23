using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Commitments;
using RideBound.Application.Optimization;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;

namespace RideBound.Algorithms.Policies;

public enum SolverBackedObjectiveProfile
{
    RollingCost,
    RevisionPenalty,
    HardVector,
    SoftHardHybrid,
}

/// <summary>
/// ADR-078 (exploratory, off by default). Order of the C1 ranking levels after the
/// accepted-request count. Cost-first skips the worst-hard-utilization and revision levels;
/// gates, locks and recovery act before ranking and are unchanged.
/// </summary>
public enum SolverBackedSelectionObjectiveOrder
{
    StabilityFirst,
    CostFirst,
}

public interface IFleetSelectionValidator
{
    CandidateSelectionValidationResult Validate(FleetSelection selection);
}

public sealed record SolverBackedFleetSelection(
    FleetSelection Selection,
    CandidateSelectionExecutionResult Execution,
    CandidateSelectionProblem Problem);

public sealed record SolverBackedFleetSelectionResult
{
    private SolverBackedFleetSelectionResult(
        SolverBackedFleetSelection? selection,
        RollingCostWitness? witness)
    {
        Selection = selection;
        Witness = witness;
    }

    public bool IsSuccess => Selection is not null;

    public SolverBackedFleetSelection? Selection { get; }

    public RollingCostWitness? Witness { get; }

    public static SolverBackedFleetSelectionResult Success(
        SolverBackedFleetSelection selection) => new(selection, null);

    public static SolverBackedFleetSelectionResult Failure(
        RollingCostWitness witness) => new(null, witness);
}

public static class SolverBackedSelectionFailureCodes
{
    public const string ModelMappingFailed = "SOLVER_MODEL_MAPPING_FAILED";
    public const string SolverDidNotProduceSelection =
        "SOLVER_DID_NOT_PRODUCE_SELECTION";
}

/// <summary>
/// Maps the exact policy objective hierarchy onto the portable assignment model.
/// Stable candidate-ID tie-breaking is represented as one ordered rank objective
/// per canonical vehicle, avoiding a weighted scalar or solver enumeration order.
/// </summary>
public sealed class SolverBackedFleetSelector
{
    private readonly ICandidateSelectionSolver _solver;

    public SolverBackedFleetSelector(ICandidateSelectionSolver solver)
    {
        _solver = solver ?? throw new ArgumentNullException(nameof(solver));
    }

    public SolverBackedFleetSelectionResult Select(
        IReadOnlyList<VehicleCandidateSet> candidateSets,
        SolverBackedObjectiveProfile objectiveProfile,
        DeterministicCandidateSelectionExecutionBudget executionBudget,
        CandidateSelectionPreSolveAccounting preSolveAccounting,
        IFleetSelectionValidator validator,
        IReadOnlyDictionary<string, CandidateCommitmentAssessment>?
            revisionAssessments = null,
        IReadOnlyDictionary<string, HardVectorCandidateAssessment>?
            hardAssessments = null,
        SolverBackedSelectionObjectiveOrder objectiveOrder =
            SolverBackedSelectionObjectiveOrder.StabilityFirst)
    {
        ArgumentNullException.ThrowIfNull(candidateSets);
        ArgumentNullException.ThrowIfNull(executionBudget);
        ArgumentNullException.ThrowIfNull(preSolveAccounting);
        ArgumentNullException.ThrowIfNull(validator);

        if (!Enum.IsDefined(objectiveProfile))
        {
            throw new ArgumentOutOfRangeException(nameof(objectiveProfile));
        }

        if (!Enum.IsDefined(objectiveOrder))
        {
            throw new ArgumentOutOfRangeException(nameof(objectiveOrder));
        }

        if (objectiveOrder == SolverBackedSelectionObjectiveOrder.CostFirst
            && objectiveProfile != SolverBackedObjectiveProfile.HardVector)
        {
            throw new ArgumentException(
                "The cost-first objective order is only defined for the C1 hard-vector profile.",
                nameof(objectiveOrder));
        }

        var mapping = SelectionModelMapping.Create(
            candidateSets,
            objectiveProfile,
            revisionAssessments,
            hardAssessments,
            objectiveOrder);

        if (mapping.Witness is not null)
        {
            return SolverBackedFleetSelectionResult.Failure(mapping.Witness);
        }

        var solutionValidator = new MappedSolutionValidator(
            mapping,
            validator);
        var executed = new SafeCandidateSelectionExecutor(
            _solver,
            solutionValidator).Execute(
                mapping.Problem!,
                executionBudget,
                preSolveAccounting);

        if (executed.SolveResult.Solution is null)
        {
            var rejection = executed.Diagnostics.ValidationWitnesses.LastOrDefault();
            return SolverBackedFleetSelectionResult.Failure(
                new RollingCostWitness(
                    SolverBackedSelectionFailureCodes.SolverDidNotProduceSelection,
                    rejection is null
                        ? executed.SolveResult.Message
                            ?? "The solver and validated fallback portfolio produced no selection."
                        : $"Fallback validation failed: {rejection.ReasonCode}: {rejection.Message}",
                    Dimension: rejection?.ReasonCode
                        ?? executed.SolveResult.ReasonCode));
        }

        var selected = mapping.Map(executed.SolveResult.Solution);

        return selected.Selection is not null
            ? SolverBackedFleetSelectionResult.Success(
                new SolverBackedFleetSelection(
                    selected.Selection,
                    executed,
                    mapping.Problem!))
            : SolverBackedFleetSelectionResult.Failure(selected.Witness!);
    }

    private sealed class MappedSolutionValidator(
        SelectionModelMapping mapping,
        IFleetSelectionValidator validator) : ICandidateSelectionSolutionValidator
    {
        public CandidateSelectionValidationResult Validate(
            CandidateSelectionProblem problem,
            CandidateSelectionSolution solution)
        {
            if (!ReferenceEquals(problem, mapping.Problem))
            {
                return CandidateSelectionValidationResult.Invalid(
                    SolverBackedSelectionFailureCodes.ModelMappingFailed,
                    "The semantic validator received a solution for a different model instance.");
            }

            var selected = mapping.Map(solution);
            return selected.Selection is null
                ? CandidateSelectionValidationResult.Invalid(
                    selected.Witness!.Code,
                    selected.Witness.Message)
                : validator.Validate(selected.Selection);
        }
    }

    private sealed class SelectionModelMapping
    {
        private readonly IReadOnlyDictionary<string, InsertionCandidate> _candidates;
        private readonly SolverBackedObjectiveProfile _profile;
        private readonly IReadOnlyDictionary<string, CandidateCommitmentAssessment>?
            _revisionAssessments;
        private readonly IReadOnlyDictionary<string, HardVectorCandidateAssessment>?
            _hardAssessments;
        private readonly bool _hardTreatmentActive;
        private readonly bool _warningTreatmentActive;

        private SelectionModelMapping(
            CandidateSelectionProblem? problem,
            IReadOnlyDictionary<string, InsertionCandidate> candidates,
            SolverBackedObjectiveProfile profile,
            IReadOnlyDictionary<string, CandidateCommitmentAssessment>?
                revisionAssessments,
            IReadOnlyDictionary<string, HardVectorCandidateAssessment>?
                hardAssessments,
            bool hardTreatmentActive,
            bool warningTreatmentActive,
            RollingCostWitness? witness)
        {
            Problem = problem;
            _candidates = candidates;
            _profile = profile;
            _revisionAssessments = revisionAssessments;
            _hardAssessments = hardAssessments;
            _hardTreatmentActive = hardTreatmentActive;
            _warningTreatmentActive = warningTreatmentActive;
            Witness = witness;
        }

        public CandidateSelectionProblem? Problem { get; }

        public RollingCostWitness? Witness { get; }

        public static SelectionModelMapping Create(
            IReadOnlyList<VehicleCandidateSet> candidateSets,
            SolverBackedObjectiveProfile profile,
            IReadOnlyDictionary<string, CandidateCommitmentAssessment>?
                revisionAssessments,
            IReadOnlyDictionary<string, HardVectorCandidateAssessment>?
                hardAssessments,
            SolverBackedSelectionObjectiveOrder objectiveOrder)
        {
            var orderedSets = candidateSets
                .OrderBy(value => value.VehicleId.Value, StringComparer.Ordinal)
                .ToArray();
            var allCandidates = orderedSets
                .SelectMany(value => value.Candidates)
                .ToArray();
            var candidateMap = allCandidates
                .GroupBy(value => value.CandidateId, StringComparer.Ordinal)
                .Where(group => group.Count() == 1)
                .ToDictionary(
                    group => group.Key,
                    group => group.Single(),
                    StringComparer.Ordinal);

            if (orderedSets.Any(set => set.Candidates.Count == 0))
            {
                return Failure(
                    candidateMap,
                    profile,
                    revisionAssessments,
                    hardAssessments,
                    "Every vehicle needs at least one feasible candidate.");
            }

            if (candidateMap.Count != allCandidates.Length)
            {
                return Failure(
                    candidateMap,
                    profile,
                    revisionAssessments,
                    hardAssessments,
                    "Candidate IDs must be globally unique across the fleet.");
            }

            if (profile == SolverBackedObjectiveProfile.RevisionPenalty
                && (revisionAssessments is null
                    || allCandidates.Any(
                        candidate => !revisionAssessments.ContainsKey(
                            candidate.CandidateId))))
            {
                return Failure(
                    candidateMap,
                    profile,
                    revisionAssessments,
                    hardAssessments,
                    "Every B2 candidate needs a revision assessment.");
            }

            if (profile is SolverBackedObjectiveProfile.HardVector
                    or SolverBackedObjectiveProfile.SoftHardHybrid
                && (hardAssessments is null
                    || allCandidates.Any(
                        candidate => !hardAssessments.ContainsKey(
                            candidate.CandidateId))))
            {
                return Failure(
                    candidateMap,
                    profile,
                    revisionAssessments,
                    hardAssessments,
                    "Every C1/C2 candidate needs a hard-vector assessment.");
            }

            var hardTreatmentActive = hardAssessments?.Values.Any(
                value => value.HasApplicableHardLimit) == true;
            var warningTreatmentActive =
                profile == SolverBackedObjectiveProfile.SoftHardHybrid
                && hardAssessments!.Values.Any(value => value.HasApplicableWarning);
            var forcedTreatmentActive = profile
                    is SolverBackedObjectiveProfile.HardVector
                    or SolverBackedObjectiveProfile.SoftHardHybrid
                && allCandidates.Any(
                    candidate => hardAssessments![candidate.CandidateId].IsForcedReference);
            var objectives = new List<ObjectiveMapping>();

            if (forcedTreatmentActive)
            {
                // A forced option keeps a route that a commitment gate rejected, which
                // breaches an existing promise. It ranks before accepting new riders, so
                // it is selected only for a vehicle with no alternative that avoids it.
                objectives.Add(
                    MinSum(
                        "forced-reference-count",
                        candidate => hardAssessments![candidate.CandidateId]
                            .IsForcedReference
                                ? 1
                                : 0));
            }

            objectives.Add(
                new(
                    new CandidateSelectionObjectiveLevel(
                        "accepted-request-count",
                        CandidateSelectionObjectiveSense.Maximize,
                        CandidateSelectionObjectiveAggregation.Sum),
                    candidate => candidate.NewRequestIds.Count));

            switch (profile)
            {
                case SolverBackedObjectiveProfile.RevisionPenalty:
                    objectives.Add(
                        MinSum(
                            "material-revision-count",
                            candidate => revisionAssessments![candidate.CandidateId]
                                .DecisionInducedRevision.MaterialEtaRevisionCount));
                    AddRevisionObjectives(
                        objectives,
                        candidate => revisionAssessments![candidate.CandidateId]
                            .DecisionInducedRevision,
                        "revision");
                    break;
                // Cost-first (ADR-078) matches no case here, so operational cost follows the
                // accepted-request count directly; the forced level above is unchanged.
                case SolverBackedObjectiveProfile.HardVector
                    when hardTreatmentActive
                        && objectiveOrder
                            == SolverBackedSelectionObjectiveOrder.StabilityFirst:
                    objectives.Add(
                        MinMaximum(
                            "worst-hard-utilization-ppm",
                            candidate => hardAssessments![candidate.CandidateId]
                                .WorstHardUtilizationPartsPerMillion));
                    AddRevisionObjectives(
                        objectives,
                        candidate => hardAssessments![candidate.CandidateId]
                            .DecisionInducedRevision,
                        "revision");
                    break;
                case SolverBackedObjectiveProfile.SoftHardHybrid
                    when warningTreatmentActive:
                    objectives.Add(
                        MinMaximum(
                            "worst-hard-utilization-ppm",
                            candidate => hardAssessments![candidate.CandidateId]
                                .WorstHardUtilizationPartsPerMillion));
                    AddRevisionObjectives(
                        objectives,
                        candidate => hardAssessments![candidate.CandidateId]
                            .WarningExcess!,
                        "warning-excess");
                    AddRevisionObjectives(
                        objectives,
                        candidate => hardAssessments![candidate.CandidateId]
                            .DecisionInducedRevision,
                        "revision");
                    break;
                case SolverBackedObjectiveProfile.SoftHardHybrid
                    when hardTreatmentActive:
                    objectives.Add(
                        MinMaximum(
                            "worst-hard-utilization-ppm",
                            candidate => hardAssessments![candidate.CandidateId]
                                .WorstHardUtilizationPartsPerMillion));
                    AddRevisionObjectives(
                        objectives,
                        candidate => hardAssessments![candidate.CandidateId]
                            .DecisionInducedRevision,
                        "revision");
                    break;
            }

            objectives.Add(
                MinSum(
                    "operational-cost",
                    candidate => candidate.Schedule.OperationalCost));

            // G2 "prefer-keep" (emulator engine.js 584-596): among equal cost, fewer relocation
            // candidates win before the identity tie-break. Added only when a G2 option exists,
            // so every other decision keeps the archived hierarchy.
            if (allCandidates.Any(candidate => candidate.RelocationRole is not null))
            {
                objectives.Add(
                    MinSum(
                        "relocation-candidate-count",
                        candidate => candidate.RelocationRole is null ? 0 : 1));
            }

            foreach (var set in orderedSets)
            {
                var ranks = set.Candidates
                    .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
                    .Select((candidate, rank) => (candidate.CandidateId, rank))
                    .ToDictionary(
                        value => value.CandidateId,
                        value => (long)value.rank,
                        StringComparer.Ordinal);
                objectives.Add(
                    MinSum(
                        $"candidate-id-rank:{set.VehicleId.Value}",
                        candidate => candidate.VehicleId == set.VehicleId
                            ? ranks[candidate.CandidateId]
                            : 0));
            }

            var problem = CandidateSelectionProblem.Create(
                orderedSets.Select(value => value.VehicleId),
                allCandidates
                    .SelectMany(value => value.NewRequestIds)
                    .Distinct(),
                objectives.Select(value => value.Level),
                allCandidates.Select(
                    candidate => new CandidateSelectionOption(
                        candidate.CandidateId,
                        candidate.VehicleId,
                        candidate.NewRequestIds,
                        objectives.Select(
                            objective => objective.Contribution(candidate)).ToArray(),
                        candidate.IsNoOp,
                        candidate.RelocationRole == RelocationRole.Donor
                            ? candidate.RelocatedRequestId
                            : null,
                        candidate.RelocationRole == RelocationRole.Receiver
                            ? candidate.RelocatedRequestId
                            : null)));

            return problem.IsSuccess
                ? new SelectionModelMapping(
                    problem.Value,
                    candidateMap,
                    profile,
                    revisionAssessments,
                    hardAssessments,
                    hardTreatmentActive,
                    warningTreatmentActive,
                    null)
                : Failure(
                    candidateMap,
                    profile,
                    revisionAssessments,
                    hardAssessments,
                    problem.Failure!.Message);
        }

        public MappedSelectionResult Map(CandidateSelectionSolution solution)
        {
            var selected = solution.SelectedOptionIds
                .Select(optionId => _candidates[optionId])
                .OrderBy(value => value.VehicleId.Value, StringComparer.Ordinal)
                .ToArray();

            try
            {
                var accepted = checked(
                    selected.Sum(candidate => candidate.NewRequestIds.Count));
                var cost = selected.Aggregate(
                    0L,
                    (total, candidate) => checked(
                        total + candidate.Schedule.OperationalCost));
                CommitmentVector? revision = null;
                CommitmentVector? warning = null;
                long? worst = null;

                if (_profile == SolverBackedObjectiveProfile.RevisionPenalty)
                {
                    revision = SumVector(
                        selected,
                        candidate => _revisionAssessments![candidate.CandidateId]
                            .DecisionInducedRevision);
                }
                else if (_profile is SolverBackedObjectiveProfile.HardVector
                             or SolverBackedObjectiveProfile.SoftHardHybrid
                    && _hardTreatmentActive)
                {
                    revision = SumVector(
                        selected,
                        candidate => _hardAssessments![candidate.CandidateId]
                            .DecisionInducedRevision);
                    worst = selected.Max(
                        candidate => _hardAssessments![candidate.CandidateId]
                            .WorstHardUtilizationPartsPerMillion);

                    if (_warningTreatmentActive)
                    {
                        warning = SumVector(
                            selected,
                            candidate => _hardAssessments![candidate.CandidateId]
                                .WarningExcess!);
                    }
                }

                return MappedSelectionResult.Success(
                    new FleetSelection(
                        selected.Select(
                            candidate => new SelectedVehiclePlan(
                                candidate.VehicleId,
                                candidate)).ToArray(),
                        accepted,
                        cost,
                        revision,
                        worst,
                        warning));
            }
            catch (OverflowException)
            {
                return MappedSelectionResult.Failure(
                    new RollingCostWitness(
                        SolverBackedSelectionFailureCodes.ModelMappingFailed,
                        "Mapped fleet selection exceeded the canonical integer range.",
                        Dimension: "selectionAggregation"));
            }
        }

        private static CommitmentVector SumVector(
            IReadOnlyList<InsertionCandidate> selected,
            Func<InsertionCandidate, CommitmentVector> vector)
        {
            var sum = CommitmentVector.Zero;

            foreach (var candidate in selected)
            {
                var added = sum.Add(vector(candidate));

                if (!added.IsSuccess)
                {
                    throw new OverflowException(added.Failure!.Message);
                }

                sum = added.Value!;
            }

            return sum;
        }

        private static void AddRevisionObjectives(
            ICollection<ObjectiveMapping> objectives,
            Func<InsertionCandidate, CommitmentVector> vector,
            string prefix)
        {
            foreach (var dimension in CommitmentDimensionVocabulary.Ordered)
            {
                objectives.Add(
                    MinSum(
                        $"{prefix}:{CommitmentDimensionVocabulary.ToProtocolValue(dimension)}",
                        candidate => vector(candidate).Get(dimension)));
            }
        }

        private static ObjectiveMapping MinSum(
            string name,
            Func<InsertionCandidate, long> contribution) =>
            new(
                new CandidateSelectionObjectiveLevel(
                    name,
                    CandidateSelectionObjectiveSense.Minimize,
                    CandidateSelectionObjectiveAggregation.Sum),
                contribution);

        private static ObjectiveMapping MinMaximum(
            string name,
            Func<InsertionCandidate, long> contribution) =>
            new(
                new CandidateSelectionObjectiveLevel(
                    name,
                    CandidateSelectionObjectiveSense.Minimize,
                    CandidateSelectionObjectiveAggregation.Maximum),
                contribution);

        private static SelectionModelMapping Failure(
            IReadOnlyDictionary<string, InsertionCandidate> candidates,
            SolverBackedObjectiveProfile profile,
            IReadOnlyDictionary<string, CandidateCommitmentAssessment>?
                revisionAssessments,
            IReadOnlyDictionary<string, HardVectorCandidateAssessment>?
                hardAssessments,
            string message) =>
            new(
                null,
                candidates,
                profile,
                revisionAssessments,
                hardAssessments,
                false,
                false,
                new RollingCostWitness(
                    SolverBackedSelectionFailureCodes.ModelMappingFailed,
                    message,
                    Dimension: "candidateSelectionModel"));

        private sealed record ObjectiveMapping(
            CandidateSelectionObjectiveLevel Level,
            Func<InsertionCandidate, long> Contribution);
    }

    private sealed record MappedSelectionResult(
        FleetSelection? Selection,
        RollingCostWitness? Witness)
    {
        public static MappedSelectionResult Success(FleetSelection selection) =>
            new(selection, null);

        public static MappedSelectionResult Failure(RollingCostWitness witness) =>
            new(null, witness);
    }
}
