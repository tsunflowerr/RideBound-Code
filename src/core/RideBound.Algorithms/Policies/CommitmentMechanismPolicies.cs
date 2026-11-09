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
    internal static DispatchDecisionResult Finish(
        CommitmentMechanismContext context,
        IReadOnlyList<VehicleCandidateSet> candidates,
        FleetSelectionResult selected,
        PhysicalPlanValidator physicalValidator)
    {
        if (!selected.IsSuccess)
        {
            return DispatchDecisionResult.Failure(selected.Witness!);
        }

        var invalid = SelectionApplier.ValidateSelection(
            context.ReducedState,
            selected.Selection!,
            physicalValidator);

        return invalid is null
            ? SelectionApplier.ApplySelection(
                context.ReducedState,
                selected.Selection!,
                candidates)
            : DispatchDecisionResult.Failure(invalid);
    }

    internal static DispatchDecisionResult GenerationFailure(
        CandidateGenerationWitness witness) =>
        DispatchDecisionResult.Failure(
            new RollingCostWitness(
                DecisionFailureCodes.CandidateGenerationFailed,
                witness.Message,
                witness.VehicleId,
                witness.RequestId,
                Dimension: witness.Dimension));
}
