using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Commitments;
using RideBound.Domain.Validation;

namespace RideBound.Algorithms.Policies;

/// <summary>
/// The two decision steps that the policy classes share: validate and apply a fleet selection, and turn a
/// candidate generation failure into a decision failure. The class keeps the name of the rolling-penalty policy
/// (B2), which owned these steps and was removed.
/// </summary>
public static class RevisionPenaltyPolicy
{
    internal static RollingCostDecisionResult Finish(
        CommitmentMechanismContext context,
        IReadOnlyList<VehicleCandidateSet> candidates,
        FleetSelectionResult selected,
        PhysicalPlanValidator physicalValidator)
    {
        if (!selected.IsSuccess)
        {
            return RollingCostDecisionResult.Failure(selected.Witness!);
        }

        var invalid = RollingCostPolicy.ValidateSelection(
            context.ReducedState,
            selected.Selection!,
            physicalValidator);

        return invalid is null
            ? RollingCostPolicy.ApplySelection(
                context.ReducedState,
                selected.Selection!,
                candidates)
            : RollingCostDecisionResult.Failure(invalid);
    }

    internal static RollingCostDecisionResult GenerationFailure(
        CandidateGenerationWitness witness) =>
        RollingCostDecisionResult.Failure(
            new RollingCostWitness(
                RollingCostFailureCodes.CandidateGenerationFailed,
                witness.Message,
                witness.VehicleId,
                witness.RequestId,
                Dimension: witness.Dimension));
}
