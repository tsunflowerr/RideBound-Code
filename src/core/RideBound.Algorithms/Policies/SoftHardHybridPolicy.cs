using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Commitments;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Validation;

namespace RideBound.Algorithms.Policies;

public sealed class SoftHardHybridFleetSelector
{
    private readonly HardVectorFleetSelector _hardSelector;

    public SoftHardHybridFleetSelector(HardVectorFleetSelector? hardSelector = null)
    {
        _hardSelector = hardSelector ?? new HardVectorFleetSelector();
    }

    public FleetSelectionResult Select(
        IReadOnlyList<VehicleCandidateSet> vehicleCandidateSets,
        IReadOnlyDictionary<string, HardVectorCandidateAssessment> assessments)
    {
        ArgumentNullException.ThrowIfNull(vehicleCandidateSets);
        ArgumentNullException.ThrowIfNull(assessments);

        if (!assessments.Values.Any(value => value.HasApplicableWarning))
        {
            return _hardSelector.Select(vehicleCandidateSets, assessments);
        }

        var orderedSets = vehicleCandidateSets
            .OrderBy(value => value.VehicleId.Value, StringComparer.Ordinal)
            .ToArray();
        var missing = orderedSets.FirstOrDefault(
            set => set.Candidates.Count == 0
                || set.Candidates.Any(
                    candidate => !assessments.TryGetValue(
                            candidate.CandidateId,
                            out var assessment)
                        || assessment.WarningExcess is null));

        if (missing is not null)
        {
            return FleetSelectionResult.Failure(
                new RollingCostWitness(
                    RollingCostFailureCodes.CommitmentAssessmentFailed,
                    "Every feasible candidate requires a C2 warning assessment.",
                    missing.VehicleId));
        }

        FleetSelection? best = null;
        RollingCostWitness? overflow = null;
        Enumerate(
            0,
            orderedSets,
            assessments,
            [],
            new HashSet<RequestId>(),
            0,
            0,
            0,
            CommitmentVector.Zero,
            CommitmentVector.Zero,
            ref best,
            ref overflow);

        return best is not null
            ? FleetSelectionResult.Success(best)
            : FleetSelectionResult.Failure(
                overflow
                ?? new RollingCostWitness(
                    RollingCostFailureCodes.NoVehiclePlan,
                    "No globally consistent C2 fleet candidate set exists."));
    }

    private static void Enumerate(
        int index,
        IReadOnlyList<VehicleCandidateSet> sets,
        IReadOnlyDictionary<string, HardVectorCandidateAssessment> assessments,
        IReadOnlyList<SelectedVehiclePlan> selected,
        IReadOnlySet<RequestId> assignedRequests,
        int acceptedCount,
        long operationalCost,
        long worstUtilization,
        CommitmentVector warningExcess,
        CommitmentVector revision,
        ref FleetSelection? best,
        ref RollingCostWitness? overflow)
    {
        if (index == sets.Count)
        {
            var fleet = new FleetSelection(
                selected.ToArray(),
                acceptedCount,
                operationalCost,
                revision,
                worstUtilization,
                warningExcess);

            if (best is null || IsBetter(fleet, best))
            {
                best = fleet;
            }

            return;
        }

        var set = sets[index];

        foreach (var candidate in set.Candidates.OrderBy(
                     value => value.CandidateId,
                     StringComparer.Ordinal))
        {
            if (candidate.NewRequestIds.Any(assignedRequests.Contains))
            {
                continue;
            }

            var assessment = assessments[candidate.CandidateId];
            long nextCost;

            try
            {
                nextCost = checked(
                    operationalCost + candidate.Schedule.OperationalCost);

                if (nextCost > DomainLimits.MaxCanonicalInteger)
                {
                    throw new OverflowException();
                }
            }
            catch (OverflowException)
            {
                overflow ??= OverflowWitness(candidate, "operationalCost");
                continue;
            }

            var nextWarning = warningExcess.Add(assessment.WarningExcess!);
            var nextRevision = revision.Add(assessment.DecisionInducedRevision);

            if (!nextWarning.IsSuccess || !nextRevision.IsSuccess)
            {
                var failure = nextWarning.Failure ?? nextRevision.Failure!;
                overflow ??= new RollingCostWitness(
                    RollingCostFailureCodes.CommitmentAssessmentFailed,
                    failure.Message,
                    candidate.VehicleId,
                    CandidateId: candidate.CandidateId,
                    Dimension: failure.Dimension);
                continue;
            }

            Enumerate(
                index + 1,
                sets,
                assessments,
                selected.Append(
                    new SelectedVehiclePlan(set.VehicleId, candidate)).ToArray(),
                assignedRequests.Concat(candidate.NewRequestIds).ToHashSet(),
                acceptedCount + candidate.NewRequestIds.Count,
                nextCost,
                Math.Max(
                    worstUtilization,
                    assessment.WorstHardUtilizationPartsPerMillion),
                nextWarning.Value!,
                nextRevision.Value!,
                ref best,
                ref overflow);
        }
    }

    private static bool IsBetter(
        FleetSelection candidate,
        FleetSelection current)
    {
        if (candidate.AcceptedRequestCount != current.AcceptedRequestCount)
        {
            return candidate.AcceptedRequestCount > current.AcceptedRequestCount;
        }

        if (candidate.WorstHardUtilizationPartsPerMillion
            != current.WorstHardUtilizationPartsPerMillion)
        {
            return candidate.WorstHardUtilizationPartsPerMillion
                < current.WorstHardUtilizationPartsPerMillion;
        }

        var warningComparison = CompareVector(
            candidate.WarningExcess!,
            current.WarningExcess!);

        if (warningComparison != 0)
        {
            return warningComparison < 0;
        }

        var revisionComparison = CompareVector(
            candidate.DecisionInducedRevision!,
            current.DecisionInducedRevision!);

        if (revisionComparison != 0)
        {
            return revisionComparison < 0;
        }

        if (candidate.OperationalCost != current.OperationalCost)
        {
            return candidate.OperationalCost < current.OperationalCost;
        }

        for (var index = 0; index < candidate.VehiclePlans.Count; index++)
        {
            var comparison = StringComparer.Ordinal.Compare(
                candidate.VehiclePlans[index].Candidate.CandidateId,
                current.VehiclePlans[index].Candidate.CandidateId);

            if (comparison != 0)
            {
                return comparison < 0;
            }
        }

        return false;
    }

    private static int CompareVector(
        CommitmentVector left,
        CommitmentVector right)
    {
        foreach (var dimension in CommitmentDimensionVocabulary.Ordered)
        {
            var comparison = left.Get(dimension).CompareTo(right.Get(dimension));

            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }

    private static RollingCostWitness OverflowWitness(
        InsertionCandidate candidate,
        string dimension) =>
        new(
            RollingCostFailureCodes.OperationalCostOverflow,
            "Fleet operational cost exceeded the integer range.",
            candidate.VehicleId,
            CandidateId: candidate.CandidateId,
            Dimension: dimension);
}

public sealed class CommitSoftHardHybridPolicy
{
    private readonly ICommitmentWarningProfileProvider _warningProfiles;
    private readonly InsertionCandidateGenerator _generator;
    private readonly HardVectorCandidateAssessor _assessor;
    private readonly SoftHardHybridFleetSelector _selector;
    private readonly PhysicalPlanValidator _physicalValidator;

    public CommitSoftHardHybridPolicy(
        ICommitmentWarningProfileProvider warningProfiles,
        InsertionCandidateGenerator? generator = null,
        HardVectorCandidateAssessor? assessor = null,
        SoftHardHybridFleetSelector? selector = null,
        PhysicalPlanValidator? physicalValidator = null)
    {
        _warningProfiles = warningProfiles
            ?? throw new ArgumentNullException(nameof(warningProfiles));
        _generator = generator ?? new InsertionCandidateGenerator();
        _assessor = assessor ?? new HardVectorCandidateAssessor();
        _selector = selector ?? new SoftHardHybridFleetSelector();
        _physicalValidator = physicalValidator ?? new PhysicalPlanValidator();
    }

    public string PolicyId => RidePoolingPolicyRegistry.CommitSoftHardHybrid;

    public RollingCostDecisionResult Decide(
        CommitmentMechanismContext context,
        CandidateGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);
        var generated = _generator.Generate(context.ReducedState, options);

        if (!generated.IsSuccess)
        {
            return RevisionPenaltyPolicy.GenerationFailure(generated.Witness!);
        }

        var assessed = _assessor.AssessAndFilter(
            context,
            generated.VehicleCandidates!,
            _warningProfiles);

        if (!assessed.IsSuccess)
        {
            var witness = assessed.Witness!;
            return RollingCostDecisionResult.Failure(
                new RollingCostWitness(
                    RollingCostFailureCodes.CommitmentAssessmentFailed,
                    witness.Message,
                    witness.VehicleId,
                    witness.RequestId,
                    witness.CandidateId,
                    witness.Dimension));
        }

        var batch = assessed.Batch!;
        var selected = _selector.Select(
            batch.FeasibleCandidateSets,
            batch.Assessments);
        return RevisionPenaltyPolicy.Finish(
            context,
            batch.FeasibleCandidateSets,
            selected,
            _physicalValidator);
    }
}
