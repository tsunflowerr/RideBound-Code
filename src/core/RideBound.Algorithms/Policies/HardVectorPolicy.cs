using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Commitments;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Validation;

namespace RideBound.Algorithms.Policies;

public sealed class HardVectorFleetSelector
{
    public FleetSelectionResult Select(
        IReadOnlyList<VehicleCandidateSet> vehicleCandidateSets,
        IReadOnlyDictionary<string, HardVectorCandidateAssessment> assessments)
    {
        ArgumentNullException.ThrowIfNull(vehicleCandidateSets);
        ArgumentNullException.ThrowIfNull(assessments);
        var orderedSets = vehicleCandidateSets
            .OrderBy(value => value.VehicleId.Value, StringComparer.Ordinal)
            .ToArray();
        var missing = orderedSets.FirstOrDefault(
            set => set.Candidates.Count == 0
                || set.Candidates.Any(
                    candidate => !assessments.ContainsKey(candidate.CandidateId)));

        if (missing is not null)
        {
            return FleetSelectionResult.Failure(
                new RollingCostWitness(
                    RollingCostFailureCodes.CommitmentAssessmentFailed,
                    "Every feasible candidate requires a C1 hard-vector assessment.",
                    missing.VehicleId));
        }

        var hardTreatmentActive = assessments.Values.Any(
            value => value.HasApplicableHardLimit);
        FleetSelection? best = null;
        RollingCostWitness? overflow = null;
        Enumerate(
            0,
            orderedSets,
            assessments,
            hardTreatmentActive,
            [],
            new HashSet<RequestId>(),
            0,
            0,
            0,
            CommitmentVector.Zero,
            ref best,
            ref overflow);

        return best is not null
            ? FleetSelectionResult.Success(best)
            : FleetSelectionResult.Failure(
                overflow
                ?? new RollingCostWitness(
                    RollingCostFailureCodes.NoVehiclePlan,
                    "No globally consistent C1 fleet candidate set exists."));
    }

    private static void Enumerate(
        int index,
        IReadOnlyList<VehicleCandidateSet> sets,
        IReadOnlyDictionary<string, HardVectorCandidateAssessment> assessments,
        bool hardTreatmentActive,
        IReadOnlyList<SelectedVehiclePlan> selected,
        IReadOnlySet<RequestId> assignedRequests,
        int acceptedCount,
        long operationalCost,
        long worstUtilization,
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
                worstUtilization);

            if (best is null || IsBetter(fleet, best, hardTreatmentActive))
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
                overflow ??= new RollingCostWitness(
                    RollingCostFailureCodes.OperationalCostOverflow,
                    "Fleet operational cost exceeded the integer range.",
                    candidate.VehicleId,
                    CandidateId: candidate.CandidateId,
                    Dimension: "operationalCost");
                continue;
            }

            var assessment = assessments[candidate.CandidateId];
            var nextRevision = revision.Add(
                assessment.DecisionInducedRevision);

            if (!nextRevision.IsSuccess)
            {
                overflow ??= new RollingCostWitness(
                    RollingCostFailureCodes.CommitmentAssessmentFailed,
                    nextRevision.Failure!.Message,
                    candidate.VehicleId,
                    CandidateId: candidate.CandidateId,
                    Dimension: nextRevision.Failure.Dimension);
                continue;
            }

            Enumerate(
                index + 1,
                sets,
                assessments,
                hardTreatmentActive,
                selected.Append(
                    new SelectedVehiclePlan(set.VehicleId, candidate)).ToArray(),
                assignedRequests.Concat(candidate.NewRequestIds).ToHashSet(),
                acceptedCount + candidate.NewRequestIds.Count,
                nextCost,
                Math.Max(
                    worstUtilization,
                    assessment.WorstHardUtilizationPartsPerMillion),
                nextRevision.Value!,
                ref best,
                ref overflow);
        }
    }

    private static bool IsBetter(
        FleetSelection candidate,
        FleetSelection current,
        bool hardTreatmentActive)
    {
        if (candidate.AcceptedRequestCount != current.AcceptedRequestCount)
        {
            return candidate.AcceptedRequestCount > current.AcceptedRequestCount;
        }

        if (hardTreatmentActive)
        {
            if (candidate.WorstHardUtilizationPartsPerMillion
                != current.WorstHardUtilizationPartsPerMillion)
            {
                return candidate.WorstHardUtilizationPartsPerMillion
                    < current.WorstHardUtilizationPartsPerMillion;
            }

            foreach (var dimension in CommitmentDimensionVocabulary.Ordered)
            {
                var candidateValue = candidate.DecisionInducedRevision!.Get(dimension);
                var currentValue = current.DecisionInducedRevision!.Get(dimension);

                if (candidateValue != currentValue)
                {
                    return candidateValue < currentValue;
                }
            }
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
}

public sealed class RideBoundHardVectorPolicy
{
    private readonly InsertionCandidateGenerator _generator;
    private readonly HardVectorCandidateAssessor _assessor;
    private readonly HardVectorFleetSelector _selector;
    private readonly PhysicalPlanValidator _physicalValidator;

    public RideBoundHardVectorPolicy(
        InsertionCandidateGenerator? generator = null,
        HardVectorCandidateAssessor? assessor = null,
        HardVectorFleetSelector? selector = null,
        PhysicalPlanValidator? physicalValidator = null)
    {
        _generator = generator ?? new InsertionCandidateGenerator();
        _assessor = assessor ?? new HardVectorCandidateAssessor();
        _selector = selector ?? new HardVectorFleetSelector();
        _physicalValidator = physicalValidator ?? new PhysicalPlanValidator();
    }

    public string PolicyId => RidePoolingPolicyRegistry.RideBoundHardVector;

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
            generated.VehicleCandidates!);

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
