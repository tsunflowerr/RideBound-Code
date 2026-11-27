using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Commitments;

namespace RideBound.Algorithms.Policies;

/// <summary>
/// The two decision steps that the policy classes share: apply a fleet selection the caller has already
/// validated, and turn a candidate generation failure into a decision failure. The class keeps the name of the
/// rolling-penalty policy (B2), which owned these steps and was removed.
/// </summary>
public static class RevisionPenaltyPolicy
{
    internal static DispatchDecisionResult Finish(
        CommitmentMechanismContext context,
        IReadOnlyList<VehicleCandidateSet> candidates,
        FleetSelectionResult selected)
    {
        if (!selected.IsSuccess)
        {
            return DispatchDecisionResult.Failure(selected.Witness!);
        }

        return SelectionApplier.ApplySelection(
            context.ReducedState,
            selected.Selection!,
            candidates);
    }

    internal static DispatchDecisionResult GenerationFailure(
        CandidateGenerationWitness witness) =>
        DispatchDecisionResult.Failure(
            new RollingCostWitness(
                DecisionFailureCodes.CandidateGenerationFailed,
                witness.Message));
}
