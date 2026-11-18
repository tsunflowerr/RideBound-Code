// Reference code for the tests only; the Runner does not use it.

using RideBound.Algorithms.Candidates;
using RideBound.Application.Commitments;
using RideBound.Application.State;
using RideBound.Domain.Common;
using RideBound.Domain.Runs;
using RideBound.Domain.Validation;

namespace RideBound.Algorithms.Commitments;

public sealed class CommitmentCandidateFilter
{
    private readonly OnlineState _beforeEventState;
    private readonly ICommitmentPolicyProvider _policies;
    private readonly IStopDistanceLookup _stopDistances;
    private readonly string _publicationScope;
    private readonly long _sourceEventSequence;
    private readonly CommitmentDecisionValidator _validator;

    public CommitmentCandidateFilter(
        OnlineState beforeEventState,
        ICommitmentPolicyProvider policies,
        IStopDistanceLookup stopDistances,
        string publicationScope,
        long sourceEventSequence,
        CommitmentDecisionValidator? validator = null)
    {
        _beforeEventState = beforeEventState
            ?? throw new ArgumentNullException(nameof(beforeEventState));
        _policies = policies
            ?? throw new ArgumentNullException(nameof(policies));
        _stopDistances = stopDistances
            ?? throw new ArgumentNullException(nameof(stopDistances));
        _publicationScope = publicationScope;
        _sourceEventSequence = sourceEventSequence;
        _validator = validator ?? new CommitmentDecisionValidator();
    }

    public IReadOnlyList<VehicleCandidateSet> Filter(
        OnlineState reducedState,
        IReadOnlyList<VehicleCandidateSet> candidateSets)
    {
        var result = new List<VehicleCandidateSet>(candidateSets.Count);

        foreach (var set in candidateSets.OrderBy(
                     value => value.VehicleId.Value,
                     StringComparer.Ordinal))
        {
            var retained = new List<InsertionCandidate>();
            var pruned = set.PrunedCandidates.ToList();

            foreach (var candidate in set.Candidates)
            {
                var updated = CandidateStateApplicator.Apply(
                    reducedState.Run,
                    candidate);

                if (!updated.IsSuccess)
                {
                    pruned.Add(
                        new CandidatePruneWitness(
                            candidate.CandidateId,
                            set.VehicleId,
                            candidate.NewRequestIds,
                            updated.Failure!.Code,
                            updated.Failure.Message));
                    continue;
                }

                var candidateState = reducedState with { Run = updated.Value! };
                var validation = _validator.Validate(
                    new CommitmentValidationContext(
                        _beforeEventState,
                        reducedState,
                        candidateState,
                        _policies,
                        _stopDistances,
                        _publicationScope,
                        _sourceEventSequence,
                        ScopedVehicleId: set.VehicleId));

                if (validation.IsValid)
                {
                    retained.Add(candidate);
                    continue;
                }

                var witness = validation.Witnesses[0];
                pruned.Add(
                    new CandidatePruneWitness(
                        candidate.CandidateId,
                        set.VehicleId,
                        candidate.NewRequestIds,
                        witness.Code,
                        witness.Message,
                        CommitmentWitnesses: validation.Witnesses));
            }

            result.Add(
                new VehicleCandidateSet(
                    set.VehicleId,
                    retained.AsReadOnly(),
                    pruned
                        .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
                        .ToArray(),
                    set.WasTruncated,
                    set.Loss));
        }

        return result.AsReadOnly();
    }

}
