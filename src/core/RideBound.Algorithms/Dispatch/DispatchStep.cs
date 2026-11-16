using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Commitments;
using RideBound.Algorithms.Policies;
using RideBound.Algorithms.Shareability;
using RideBound.Application.Commitments;
using RideBound.Application.State;
using RideBound.Domain.Commitments;

namespace RideBound.Algorithms.Dispatch;

/// <summary>
/// The result of <see cref="DispatchStep.Run"/>: the decision, the state to stage and the promises
/// the decision publishes, or the message of the first failure.
/// </summary>
public sealed record DispatchStepResult(
    DispatchDecision? Decision,
    OnlineState? StateToStage,
    IReadOnlyList<PromisePublication>? Publications,
    string? FailureMessage)
{
    public bool IsSuccess => Decision is not null;

    public static DispatchStepResult Success(
        DispatchDecision decision,
        OnlineState stateToStage,
        IReadOnlyList<PromisePublication> publications) =>
        new(decision, stateToStage, publications, null);

    public static DispatchStepResult Failure(string message) =>
        new(null, null, null, message);
}

/// <summary>
/// One dispatch decision on the reduced state of an event batch, in this order: the policy decides,
/// the commitment validator checks the decision again, exogenous service-quality breaches go into
/// the incident ledger, and every rider whose first promise the decision publishes gets its Shadow
/// class. The first failure ends the run.
/// </summary>
public sealed class DispatchStep
{
    private readonly SolverBackedRidePoolingPolicy _policy;
    private readonly CommitmentDecisionValidator _commitmentValidator;
    private readonly ExogenousServiceQualityBreachBridge _breachBridge = new();
    private readonly ShadowAllocator? _shadowAllocator;

    /// <summary>
    /// <paramref name="shadowAllocator"/> is null when the run has no Shadow Control.
    /// </summary>
    public DispatchStep(
        SolverBackedRidePoolingPolicy policy,
        CommitmentDecisionValidator commitmentValidator,
        ShadowAllocator? shadowAllocator)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(commitmentValidator);
        _policy = policy;
        _commitmentValidator = commitmentValidator;
        _shadowAllocator = shadowAllocator;
    }

    public DispatchStepResult Run(
        CommitmentMechanismContext context,
        CandidateGenerationOptions generationOptions,
        SolverBackedRidePoolingPolicyOptions policyOptions)
    {
        var solved = _policy.Decide(
            context,
            generationOptions,
            policyOptions);

        if (!solved.IsSuccess)
        {
            return DispatchStepResult.Failure(
                solved.Witness!.Message);
        }

        var decision = DispatchDecisionResult.Success(
            solved.Decision!.Decision);
        var validationPolicies = solved.Decision.EffectivePolicies;

        if (!decision.IsSuccess)
        {
            return DispatchStepResult.Failure(
                decision.Witness!.Message);
        }

        var validation = _commitmentValidator.Validate(
            new CommitmentValidationContext(
                context.BeforeEventState,
                context.ReducedState,
                decision.Decision!.ProposedState,
                validationPolicies,
                context.StopDistances,
                context.PublicationScope,
                context.SourceEventSequence));

        if (!validation.IsValid)
        {
            // The policy already validated this decision, so a rejection here is an
            // internal inconsistency. The commitment code is not a protocol error
            // code, and encoding it used to throw; it is kept in the message.
            var witness = validation.Witnesses[0];
            return DispatchStepResult.Failure(
                $"{witness.Code}: {witness.Message}");
        }

        var stateToStage = validation.ValidatedState!;
        IReadOnlyList<PromisePublication> publications = validation.Publications;

        if (decision.Decision.GenerationDiagnostics is { } diagnostics
            && diagnostics.ExogenousServiceQualityBreaches.Count != 0)
        {
            var bridged = _breachBridge.Apply(
                context.ReducedState,
                stateToStage,
                diagnostics,
                validationPolicies,
                context.StopDistances,
                context.SourceEventSequence);

            if (!bridged.IsSuccess)
            {
                return DispatchStepResult.Failure(
                    $"EXOGENOUS_BREACH_BRIDGE_FAILED: {bridged.Error}");
            }

            stateToStage = bridged.State!;
        }

        // ADR-089 GĐ4: Shadow Control. Every rider whose first promise this decision publishes
        // gets its frozen class now, from this staged state, so the next decision reads it and
        // the state hash and checkpoint carry it (engine17.js:742-776).
        if (_shadowAllocator is not null)
        {
            var opened = publications
                .Where(value => value.Entry.Kind == CommitmentLedgerEntryKind.InitialPromise)
                .Select(value => value.Entry.PublishedPromise.Projection.RequestId)
                .ToArray();
            stateToStage = _shadowAllocator.Allocate(
                stateToStage,
                opened,
                stateToStage.Run.SimulationTime);
        }

        return DispatchStepResult.Success(
            decision.Decision,
            stateToStage,
            publications);
    }
}
