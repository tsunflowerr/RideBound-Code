using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Commitments;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;

namespace RideBound.Algorithms.Policies;

/// <summary>
/// B2 exact selector. It never turns revision magnitude into a hard gate:
/// accepted count wins first, then material revisions, the stable ten-dimension
/// revision vector, operational cost, and finally the candidate-ID vector.
/// </summary>
public sealed class RevisionPenaltyFleetSelector
{
    public FleetSelectionResult Select(
        IReadOnlyList<VehicleCandidateSet> vehicleCandidateSets,
        IReadOnlyDictionary<string, CandidateCommitmentAssessment> assessments)
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
                    "Every candidate requires a B2 revision assessment.",
                    missing.VehicleId));
        }

        FleetSelection? best = null;
        RollingCostWitness? overflow = null;
        Enumerate(
            index: 0,
            orderedSets,
            assessments,
            [],
            new HashSet<RequestId>(),
            acceptedCount: 0,
            operationalCost: 0,
            CommitmentVector.Zero,
            ref best,
            ref overflow);

        return best is not null
            ? FleetSelectionResult.Success(best)
            : FleetSelectionResult.Failure(
                overflow
                ?? new RollingCostWitness(
                    RollingCostFailureCodes.NoVehiclePlan,
                    "No globally consistent B2 fleet candidate set exists."));
    }

    private static void Enumerate(
        int index,
        IReadOnlyList<VehicleCandidateSet> sets,
        IReadOnlyDictionary<string, CandidateCommitmentAssessment> assessments,
        IReadOnlyList<SelectedVehiclePlan> selected,
        IReadOnlySet<RequestId> assignedRequests,
        int acceptedCount,
        long operationalCost,
        CommitmentVector revision,
        ref FleetSelection? best,
        ref RollingCostWitness? overflow)
    {
        if (index == sets.Count)
        {
            var candidate = new FleetSelection(
                selected.ToArray(),
                acceptedCount,
                operationalCost,
                revision);

            if (best is null || IsBetter(candidate, best))
            {
                best = candidate;
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

            var nextRevision = revision.Add(
                assessments[candidate.CandidateId].DecisionInducedRevision);

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
                selected.Append(
                    new SelectedVehiclePlan(set.VehicleId, candidate)).ToArray(),
                assignedRequests.Concat(candidate.NewRequestIds).ToHashSet(),
                acceptedCount + candidate.NewRequestIds.Count,
                nextCost,
                nextRevision.Value!,
                ref best,
                ref overflow);
        }
    }

    private static bool IsBetter(FleetSelection candidate, FleetSelection current)
    {
        if (candidate.AcceptedRequestCount != current.AcceptedRequestCount)
        {
            return candidate.AcceptedRequestCount > current.AcceptedRequestCount;
        }

        var candidateRevision = candidate.DecisionInducedRevision!;
        var currentRevision = current.DecisionInducedRevision!;

        if (candidateRevision.MaterialEtaRevisionCount
            != currentRevision.MaterialEtaRevisionCount)
        {
            return candidateRevision.MaterialEtaRevisionCount
                < currentRevision.MaterialEtaRevisionCount;
        }

        foreach (var dimension in CommitmentDimensionVocabulary.Ordered)
        {
            if (candidateRevision.Get(dimension) != currentRevision.Get(dimension))
            {
                return candidateRevision.Get(dimension)
                    < currentRevision.Get(dimension);
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
