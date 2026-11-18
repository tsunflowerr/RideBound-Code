// Reference code for the tests only; the Runner does not use it.

using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Commitments;
using RideBound.Application.State;
using RideBound.Domain.Validation;

namespace RideBound.Algorithms.Policies;

/// <summary>
/// Generates the candidates, keeps those the optional commitment filter keeps, picks one per vehicle
/// with <see cref="CandidateFleetSelector"/>, then checks and applies the pick with the production
/// <see cref="SelectionApplier"/>.
/// </summary>
public sealed class ReferenceSelectionPolicy
{
    private readonly InsertionCandidateGenerator _generator;
    private readonly CandidateFleetSelector _selector;
    private readonly PhysicalPlanValidator _validator;

    public ReferenceSelectionPolicy(
        InsertionCandidateGenerator? generator = null,
        CandidateFleetSelector? selector = null,
        PhysicalPlanValidator? validator = null)
    {
        _generator = generator ?? new InsertionCandidateGenerator();
        _selector = selector ?? new CandidateFleetSelector();
        _validator = validator ?? new PhysicalPlanValidator();
    }

    public DispatchDecisionResult Decide(
        OnlineState state,
        CandidateGenerationOptions options,
        CommitmentCandidateFilter? commitmentFilter = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(options);

        var generated = _generator.Generate(state, options);

        if (!generated.IsSuccess)
        {
            return DispatchDecisionResult.Failure(
                new RollingCostWitness(
                    DecisionFailureCodes.CandidateGenerationFailed,
                    generated.Witness!.Message,
                    generated.Witness.VehicleId,
                    generated.Witness.RequestId,
                    Dimension: generated.Witness.Dimension));
        }

        var candidates = commitmentFilter is null
            ? generated.VehicleCandidates!
            : commitmentFilter.Filter(state, generated.VehicleCandidates!);
        var selection = _selector.Select(candidates);

        if (!selection.IsSuccess)
        {
            return DispatchDecisionResult.Failure(selection.Witness!);
        }

        var validationFailure = SelectionApplier.ValidateSelection(
            state,
            selection.Selection!,
            _validator);

        if (validationFailure is not null)
        {
            return DispatchDecisionResult.Failure(validationFailure);
        }

        var applied = SelectionApplier.ApplySelection(
            state,
            selection.Selection!,
            candidates);

        return applied.IsSuccess
            ? applied
            : DispatchDecisionResult.Failure(applied.Witness!);
    }
}
