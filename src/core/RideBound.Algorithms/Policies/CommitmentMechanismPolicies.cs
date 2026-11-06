using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Commitments;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Validation;

namespace RideBound.Algorithms.Policies;

public sealed class RevisionPenaltyPolicy
{
    private readonly InsertionCandidateGenerator _generator;
    private readonly CommitmentCandidateAssessor _assessor;
    private readonly RevisionPenaltyFleetSelector _selector;
    private readonly PhysicalPlanValidator _physicalValidator;

    public RevisionPenaltyPolicy(
        InsertionCandidateGenerator? generator = null,
        CommitmentCandidateAssessor? assessor = null,
        RevisionPenaltyFleetSelector? selector = null,
        PhysicalPlanValidator? physicalValidator = null)
    {
        _generator = generator ?? new InsertionCandidateGenerator();
        _assessor = assessor ?? new CommitmentCandidateAssessor();
        _selector = selector ?? new RevisionPenaltyFleetSelector();
        _physicalValidator = physicalValidator ?? new PhysicalPlanValidator();
    }

    public string PolicyId => RidePoolingPolicyRegistry.RollingPenalty;

    public RollingCostDecisionResult Decide(
        CommitmentMechanismContext context,
        CandidateGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);
        var generated = _generator.Generate(context.ReducedState, options);

        if (!generated.IsSuccess)
        {
            return GenerationFailure(generated.Witness!);
        }

        var assessed = _assessor.AssessRevisionPenalty(
            context,
            generated.VehicleCandidates!);

        if (!assessed.IsSuccess)
        {
            var witness = assessed.Witness!;
            return RollingCostDecisionResult.Failure(
                new RollingCostWitness(
                    RollingCostFailureCodes.CommitmentAssessmentFailed,
                    witness.Message,
                    witness.VehicleId,
                    witness.RequestId,
                    witness.CandidateId,
                    witness.Dimension));
        }

        var selected = _selector.Select(
            generated.VehicleCandidates!,
            assessed.Assessments!);

        return Finish(
            context,
            generated.VehicleCandidates!,
            selected,
            _physicalValidator);
    }

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

public sealed class FixedFreezeHorizonPolicy
{
    private readonly Duration _freezeHorizon;
    private readonly PromiseLock _freezeLocks;
    private readonly InsertionCandidateGenerator _generator;
    private readonly CandidateFleetSelector _selector;
    private readonly PhysicalPlanValidator _physicalValidator;

    public FixedFreezeHorizonPolicy(
        Duration freezeHorizon,
        PromiseLock freezeLocks,
        InsertionCandidateGenerator? generator = null,
        CandidateFleetSelector? selector = null,
        PhysicalPlanValidator? physicalValidator = null)
    {
        if (freezeHorizon.Milliseconds == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(freezeHorizon));
        }

        const PromiseLock allLocks = PromiseLock.Vehicle
            | PromiseLock.PickupStop
            | PromiseLock.DropStop
            | PromiseLock.PickupEta
            | PromiseLock.DropEta;

        if (freezeLocks == PromiseLock.None
            || (freezeLocks & ~allLocks) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(freezeLocks));
        }

        _freezeHorizon = freezeHorizon;
        _freezeLocks = freezeLocks;
        _generator = generator ?? new InsertionCandidateGenerator();
        _selector = selector ?? new CandidateFleetSelector();
        _physicalValidator = physicalValidator ?? new PhysicalPlanValidator();
    }

    public string PolicyId => RidePoolingPolicyRegistry.FixedFreezeHorizon;

    public RollingCostDecisionResult Decide(
        CommitmentMechanismContext context,
        CandidateGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);
        var generated = _generator.Generate(context.ReducedState, options);

        if (!generated.IsSuccess)
        {
            return RevisionPenaltyPolicy.GenerationFailure(generated.Witness!);
        }

        var policies = MechanismCommitmentPolicyProvider.FixedFreeze(
            context.Policies,
            _freezeHorizon,
            _freezeLocks);
        var filter = new CommitmentCandidateFilter(
            context.BeforeEventState,
            policies,
            context.StopDistances,
            context.PublicationScope,
            context.SourceEventSequence,
            initialPromiseTrigger: context.InitialPromiseTrigger);
        var filtered = filter.Filter(
            context.ReducedState,
            generated.VehicleCandidates!);
        var selected = _selector.Select(filtered);
        return RevisionPenaltyPolicy.Finish(
            context,
            filtered,
            selected,
            _physicalValidator);
    }
}

public sealed class NoReassignmentRepairPolicy
{
    private readonly int _maximumRepairRequestsConsideredPerVehicle;
    private readonly RollingCostPolicy _inner;

    public NoReassignmentRepairPolicy(
        int maximumRepairRequestsConsideredPerVehicle,
        InsertionCandidateGenerator? generator = null,
        CandidateFleetSelector? selector = null,
        PhysicalPlanValidator? physicalValidator = null)
    {
        if (maximumRepairRequestsConsideredPerVehicle < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumRepairRequestsConsideredPerVehicle));
        }

        _maximumRepairRequestsConsideredPerVehicle =
            maximumRepairRequestsConsideredPerVehicle;
        _inner = new RollingCostPolicy(generator, selector, physicalValidator);
    }

    public string PolicyId => RidePoolingPolicyRegistry.NoReassignmentRepair;

    public RollingCostDecisionResult Decide(
        RideBound.Application.State.OnlineState state,
        CandidateGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(options);
        return _inner.Decide(
            state,
            options.WithRepairRequestCap(
                _maximumRepairRequestsConsideredPerVehicle));
    }
}
