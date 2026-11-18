using RideBound.Algorithms.Candidates;
using RideBound.Domain.Common;
using RideBound.Domain.Runs;

namespace RideBound.Algorithms.Commitments;

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
