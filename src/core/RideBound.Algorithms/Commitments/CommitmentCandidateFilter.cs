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
    private readonly InitialPromiseTrigger _initialPromiseTrigger;
    private readonly bool _collectAllWitnesses;
    private readonly CommitmentDecisionValidator _validator;

    public CommitmentCandidateFilter(
        OnlineState beforeEventState,
        ICommitmentPolicyProvider policies,
        IStopDistanceLookup stopDistances,
        string publicationScope,
        long sourceEventSequence,
        CommitmentDecisionValidator? validator = null,
        InitialPromiseTrigger initialPromiseTrigger =
            InitialPromiseTrigger.InitialAcceptance,
        bool collectAllWitnesses = false)
    {
        _beforeEventState = beforeEventState
            ?? throw new ArgumentNullException(nameof(beforeEventState));
        _policies = policies
            ?? throw new ArgumentNullException(nameof(policies));
        _stopDistances = stopDistances
            ?? throw new ArgumentNullException(nameof(stopDistances));
        _publicationScope = publicationScope;
        _sourceEventSequence = sourceEventSequence;
        _initialPromiseTrigger = initialPromiseTrigger;
        _collectAllWitnesses = collectAllWitnesses;
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
                        ScopedVehicleId: set.VehicleId,
                        InitialPromiseTrigger: _initialPromiseTrigger,
                        CollectAllCommitmentWitnesses: _collectAllWitnesses));

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

internal static class CandidateStateApplicator
{
    public const string RelocationRequiresPair = "RELOCATION_REQUIRES_PAIR";

    /// <summary>
    /// Applies one ordinary candidate. A G2 candidate is half of a move and fails closed here;
    /// it is applied only with its partner through <see cref="ApplyRelocationPair"/>.
    /// </summary>
    public static DomainResult<RideBoundRun> Apply(
        RideBoundRun reducedRun,
        InsertionCandidate candidate)
    {
        if (candidate.RelocationRole is not null)
        {
            return DomainResult<RideBoundRun>.Fail(
                RelocationRequiresPair,
                "A waiting-rider relocation candidate is applied only with its partner.",
                candidate.CandidateId,
                "relocationRole");
        }

        var updated = candidate.IsNoOp
            ? DomainResult<RideBoundRun>.Success(reducedRun)
            : reducedRun.UpdateVehicleRoute(
                candidate.VehicleId,
                candidate.Route);

        if (!updated.IsSuccess)
        {
            return updated;
        }

        var run = updated.Value!;

        foreach (var requestId in candidate.NewRequestIds.OrderBy(
                     value => value.Value,
                     StringComparer.Ordinal))
        {
            var accepted = run.AcceptRequest(requestId, candidate.VehicleId);

            if (!accepted.IsSuccess)
            {
                return accepted;
            }

            run = accepted.Value!;
        }

        return DomainResult<RideBoundRun>.Success(run);
    }

    /// <summary>
    /// The pair state of one waiting-rider move: the donor route, then the receiver route, then
    /// the atomic reassignment, then the donor's new requests in ordinal order (the apply order of
    /// a selected move).
    /// </summary>
    public static DomainResult<RideBoundRun> ApplyRelocationPair(
        RideBoundRun reducedRun,
        InsertionCandidate donor,
        InsertionCandidate receiver)
    {
        if (donor.RelocationRole != RelocationRole.Donor
            || receiver.RelocationRole != RelocationRole.Receiver
            || donor.RelocatedRequestId is not RequestId requestId
            || receiver.RelocatedRequestId != requestId
            || donor.VehicleId == receiver.VehicleId
            || receiver.NewRequestIds.Count != 0)
        {
            return DomainResult<RideBoundRun>.Fail(
                RelocationRequiresPair,
                "A relocation pair needs one donor and one receiver of the same rider on two vehicles.",
                donor.CandidateId,
                "relocationRole");
        }

        var run = reducedRun.UpdateVehicleRoute(donor.VehicleId, donor.Route);
        run = run.IsSuccess
            ? run.Value!.UpdateVehicleRoute(receiver.VehicleId, receiver.Route)
            : run;
        run = run.IsSuccess
            ? run.Value!.ReassignWaitingRequest(requestId, receiver.VehicleId)
            : run;

        foreach (var newRequestId in donor.NewRequestIds.OrderBy(
                     value => value.Value,
                     StringComparer.Ordinal))
        {
            run = run.IsSuccess
                ? run.Value!.AcceptRequest(newRequestId, donor.VehicleId)
                : run;
        }

        return run;
    }
}
