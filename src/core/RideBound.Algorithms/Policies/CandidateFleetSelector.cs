using RideBound.Algorithms.Candidates;
using RideBound.Domain.Common;

namespace RideBound.Algorithms.Policies;

public sealed class CandidateFleetSelector
{
    public FleetSelectionResult Select(
        IReadOnlyList<VehicleCandidateSet> vehicleCandidateSets)
    {
        ArgumentNullException.ThrowIfNull(vehicleCandidateSets);
        var orderedSets = vehicleCandidateSets
            .OrderBy(value => value.VehicleId.Value, StringComparer.Ordinal)
            .ToArray();

        var missing = orderedSets.FirstOrDefault(value => value.Candidates.Count == 0);

        if (missing is not null)
        {
            return FleetSelectionResult.Failure(
                new RollingCostWitness(
                    RollingCostFailureCodes.NoVehiclePlan,
                    "Every vehicle requires at least one feasible plan, including no-op.",
                    missing.VehicleId));
        }

        FleetSelection? best = null;
        RollingCostWitness? overflow = null;
        Enumerate(
            index: 0,
            orderedSets,
            [],
            new HashSet<RequestId>(),
            acceptedCount: 0,
            operationalCost: 0,
            ref best,
            ref overflow);

        if (best is not null)
        {
            return FleetSelectionResult.Success(best);
        }

        return FleetSelectionResult.Failure(
            overflow
            ?? new RollingCostWitness(
                RollingCostFailureCodes.NoVehiclePlan,
                "No globally consistent fleet candidate set exists."));
    }

    private static void Enumerate(
        int index,
        IReadOnlyList<VehicleCandidateSet> vehicleCandidateSets,
        IReadOnlyList<SelectedVehiclePlan> selected,
        IReadOnlySet<RequestId> assignedRequests,
        int acceptedCount,
        long operationalCost,
        ref FleetSelection? best,
        ref RollingCostWitness? overflow)
    {
        if (index == vehicleCandidateSets.Count)
        {
            var candidate = new FleetSelection(
                selected.ToArray(),
                acceptedCount,
                operationalCost);

            if (best is null || IsBetter(candidate, best))
            {
                best = candidate;
            }

            return;
        }

        var set = vehicleCandidateSets[index];

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

            var nextRequests = assignedRequests
                .Concat(candidate.NewRequestIds)
                .ToHashSet();
            Enumerate(
                index + 1,
                vehicleCandidateSets,
                selected.Append(
                    new SelectedVehiclePlan(set.VehicleId, candidate))
                    .ToArray(),
                nextRequests,
                acceptedCount + candidate.NewRequestIds.Count,
                nextCost,
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
