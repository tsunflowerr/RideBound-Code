using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Commitments;
using RideBound.Application.Commitments;
using RideBound.Application.Optimization;
using RideBound.Domain.Common;
using RideBound.Domain.Validation;

namespace RideBound.Algorithms.Policies;

public enum RidePoolingPolicyKind
{
    RideBoundHardVector,
}

public static class RidePoolingPolicyRegistry
{
    public const string RideBoundHardVector = "ridebound-hard-vector";

    public static bool TryParse(
        string? policyId,
        out RidePoolingPolicyKind policyKind)
    {
        policyKind = policyId switch
        {
            RideBoundHardVector => RidePoolingPolicyKind.RideBoundHardVector,
            _ => default,
        };
        return policyId is RideBoundHardVector;
    }

    public static string ToPolicyId(RidePoolingPolicyKind policyKind) =>
        policyKind switch
        {
            RidePoolingPolicyKind.RideBoundHardVector => RideBoundHardVector,
            _ => throw new ArgumentOutOfRangeException(nameof(policyKind)),
        };
}

public sealed record SolverBackedRidePoolingPolicyOptions
{
    public SolverBackedRidePoolingPolicyOptions(
        RidePoolingPolicyKind policyKind,
        DeterministicCandidateSelectionExecutionBudget executionBudget,
        bool captureCandidatePortfolioEvidence = false,
        bool forcedReferenceRecovery = false,
        bool forcedNoWorseRecovery = false,
        SolverBackedSelectionObjectiveOrder selectionObjectiveOrder =
            SolverBackedSelectionObjectiveOrder.StabilityFirst)
    {
        ArgumentNullException.ThrowIfNull(executionBudget);

        if (!Enum.IsDefined(selectionObjectiveOrder))
        {
            throw new ArgumentOutOfRangeException(nameof(selectionObjectiveOrder));
        }

        if (selectionObjectiveOrder == SolverBackedSelectionObjectiveOrder.CostFirst
            && policyKind != RidePoolingPolicyKind.RideBoundHardVector)
        {
            throw new ArgumentException(
                "The cost-first selection order is only valid for the C1 policy.");
        }

        if (forcedNoWorseRecovery && !forcedReferenceRecovery)
        {
            throw new ArgumentException(
                "No-worse recovery extends forced-reference recovery, so it requires it.");
        }

        if (!Enum.IsDefined(policyKind))
        {
            throw new ArgumentOutOfRangeException(nameof(policyKind));
        }

        if (forcedReferenceRecovery
            && policyKind is not RidePoolingPolicyKind.RideBoundHardVector)
        {
            throw new ArgumentException(
                "Forced-reference recovery is only valid for the C1 and C2 policies.");
        }

        PolicyKind = policyKind;
        ExecutionBudget = executionBudget;
        CaptureCandidatePortfolioEvidence = captureCandidatePortfolioEvidence;
        ForcedReferenceRecovery = forcedReferenceRecovery;
        ForcedNoWorseRecovery = forcedNoWorseRecovery;
        SelectionObjectiveOrder = selectionObjectiveOrder;
    }

    public RidePoolingPolicyKind PolicyKind { get; }

    public DeterministicCandidateSelectionExecutionBudget ExecutionBudget { get; }

    public bool CaptureCandidatePortfolioEvidence { get; }

    /// <summary>
    /// Exploratory (ADR-075, off by default). A C1 vehicle whose safety no-op is
    /// rejected only by a deadline or budget gate keeps its route as a forced
    /// option, and the kept route is recorded as a typed breach instead of failing
    /// the run.
    /// </summary>
    public bool ForcedReferenceRecovery { get; }

    /// <summary>
    /// Exploratory no-worse-than-reference extension of <see cref="ForcedReferenceRecovery"/>
    /// (off by default): a forced vehicle may also choose a changed route that makes none of the
    /// kept route's gate overruns worse, so it can still serve a new rider.
    /// </summary>
    public bool ForcedNoWorseRecovery { get; }

    /// <summary>
    /// Exploratory (ADR-078, stability-first by default). Cost-first ranks a C1 selection by
    /// operational cost right after the accepted-request count; the hard assessment, gates,
    /// locks and recovery are unchanged.
    /// </summary>
    public SolverBackedSelectionObjectiveOrder SelectionObjectiveOrder { get; }
}

public sealed record SolverBackedPolicyDecision(
    DispatchDecision Decision,
    ICommitmentPolicyProvider EffectivePolicies);

public sealed record SolverBackedPolicyDecisionResult
{
    private SolverBackedPolicyDecisionResult(
        SolverBackedPolicyDecision? decision,
        RollingCostWitness? witness)
    {
        Decision = decision;
        Witness = witness;
    }

    public bool IsSuccess => Decision is not null;

    public SolverBackedPolicyDecision? Decision { get; }

    public RollingCostWitness? Witness { get; }

    public static SolverBackedPolicyDecisionResult Success(
        SolverBackedPolicyDecision decision) => new(decision, null);

    public static SolverBackedPolicyDecisionResult Failure(
        RollingCostWitness witness) => new(null, witness);
}

/// <summary>
/// Production policy path for C1. It generates the shared physical
/// candidate set once, performs only the named mechanism gate/assessment, maps
/// the exact hierarchy to the solver port, and independently validates every
/// solver or fallback fleet selection before returning it to Runner.
/// </summary>
public sealed class SolverBackedRidePoolingPolicy
{
    private static readonly byte[] OmissionAccountingDomain =
        "RideBound.Wp4OmissionAccounting.v1\0"u8.ToArray();
    private readonly InsertionCandidateGenerator _generator;
    private readonly HardVectorCandidateAssessor _hardAssessor;
    private readonly SolverBackedFleetSelector _selector;
    private readonly CommitmentDecisionValidator _commitmentValidator;
    private readonly PhysicalPlanValidator _physicalValidator;

    public SolverBackedRidePoolingPolicy(
        ICandidateSelectionSolver solver,
        InsertionCandidateGenerator? generator = null,
        HardVectorCandidateAssessor? hardAssessor = null,
        CommitmentDecisionValidator? commitmentValidator = null,
        PhysicalPlanValidator? physicalValidator = null)
    {
        ArgumentNullException.ThrowIfNull(solver);
        _generator = generator ?? new InsertionCandidateGenerator();
        _hardAssessor = hardAssessor ?? new HardVectorCandidateAssessor();
        _selector = new SolverBackedFleetSelector(solver);
        _commitmentValidator = commitmentValidator
            ?? new CommitmentDecisionValidator();
        _physicalValidator = physicalValidator ?? new PhysicalPlanValidator();
    }

    public SolverBackedPolicyDecisionResult Decide(
        CommitmentMechanismContext context,
        CandidateGenerationOptions generationOptions,
        SolverBackedRidePoolingPolicyOptions policyOptions)
    {
        ArgumentNullException.ThrowIfNull(context);

        // G2 candidates are assessed in pairs only by the C1 hard-vector assessor; any other
        // policy would see half moves, so the combination is a programming error.
        if (generationOptions?.WaitingRiderRelocation is not null
            && policyOptions?.PolicyKind != RidePoolingPolicyKind.RideBoundHardVector)
        {
            throw new ArgumentException(
                "Waiting-rider relocation runs only under the solver-backed C1 policy.",
                nameof(generationOptions));
        }
        ArgumentNullException.ThrowIfNull(generationOptions);
        ArgumentNullException.ThrowIfNull(policyOptions);

        var generated = _generator.Generate(
            context.ReducedState,
            generationOptions);

        if (!generated.IsSuccess)
        {
            return SolverBackedPolicyDecisionResult.Failure(
                RevisionPenaltyPolicy.GenerationFailure(generated.Witness!).Witness!);
        }

        var candidates = generated.VehicleCandidates!;
        IReadOnlyDictionary<string, HardVectorCandidateAssessment>?
            hardAssessments = null;
        var validationWorkUnits = 0L;
        var effectivePolicies = context.Policies;
        var profile = SolverBackedObjectiveProfile.RollingCost;
        IReadOnlySet<VehicleId>? forcedReferenceVehicles = null;

        switch (policyOptions.PolicyKind)
        {
            case RidePoolingPolicyKind.RideBoundHardVector:
                {
                    var assessed = _hardAssessor.AssessAndFilter(
                        context,
                        candidates,
                        requireSafetyNoOp: true,
                        forcedReferenceRecovery: policyOptions.ForcedReferenceRecovery,
                        forcedNoWorse: policyOptions.ForcedNoWorseRecovery);

                    if (!assessed.IsSuccess)
                    {
                        return AssessmentFailure(assessed.Witness!);
                    }

                    candidates = assessed.Batch!.FeasibleCandidateSets;
                    hardAssessments = assessed.Batch.Assessments;
                    forcedReferenceVehicles = assessed.Batch.ForcedReferenceVehicles.Count == 0
                        ? null
                        : assessed.Batch.ForcedReferenceVehicles;
                    validationWorkUnits = CountCandidates(
                        generated.VehicleCandidates!);
                    profile = SolverBackedObjectiveProfile.HardVector;
                    break;
                }
        }

        var accounting = CreateAccounting(
            generated.Diagnostics!,
            validationWorkUnits,
            policyOptions.ExecutionBudget);

        if (!accounting.IsSuccess)
        {
            return SolverBackedPolicyDecisionResult.Failure(
                new RollingCostWitness(
                    accounting.Failure!.Code,
                    accounting.Failure.Message,
                    Dimension: accounting.Failure.Dimension));
        }

        var validator = new FullFleetValidator(
            context,
            candidates,
            effectivePolicies,
            _commitmentValidator,
            _physicalValidator,
            forcedReferenceVehicles,
            policyOptions.ForcedNoWorseRecovery);
        var selected = _selector.Select(
            candidates,
            profile,
            policyOptions.ExecutionBudget,
            accounting.Value!,
            validator,
            hardAssessments,
            policyOptions.SelectionObjectiveOrder);

        if (!selected.IsSuccess)
        {
            return SolverBackedPolicyDecisionResult.Failure(selected.Witness!);
        }

        var finished = RevisionPenaltyPolicy.Finish(
            context,
            candidates,
            FleetSelectionResult.Success(selected.Selection!.Selection),
            _physicalValidator);

        if (!finished.IsSuccess)
        {
            return SolverBackedPolicyDecisionResult.Failure(finished.Witness!);
        }

        CandidatePortfolioEvidenceSnapshot? portfolioEvidence = null;

        if (policyOptions.CaptureCandidatePortfolioEvidence)
        {
            var captured = CandidatePortfolioEvidenceSnapshot.Create(
                profile,
                generated.VehicleCandidates!,
                candidates,
                selected.Selection.Problem,
                selected.Selection.Selection.VehiclePlans
                    .Select(value => value.Candidate.CandidateId)
                    .ToArray());

            if (!captured.IsSuccess)
            {
                return SolverBackedPolicyDecisionResult.Failure(
                    new RollingCostWitness(
                        captured.Failure!.Code,
                        captured.Failure.Message,
                        CandidateId: captured.Failure.EntityId,
                        Dimension: captured.Failure.Dimension));
            }

            portfolioEvidence = captured.Value;
        }

        var decision = finished.Decision! with
        {
            SelectionExecution = selected.Selection.Execution,
            GenerationDiagnostics = generated.Diagnostics,
            CandidatePortfolioEvidence = portfolioEvidence,
            ForcedReferenceVehicles = SelectedForcedVehicles(
                selected.Selection.Selection,
                hardAssessments,
                forcedReferenceVehicles),
        };
        return SolverBackedPolicyDecisionResult.Success(
            new SolverBackedPolicyDecision(decision, effectivePolicies));
    }

    /// <summary>
    /// The vehicles whose selected plan is a forced option (the kept no-op or, under no-worse
    /// recovery, a changed route); null when none is, so a run without a forced selection
    /// carries exactly the decision it had before.
    /// </summary>
    private static IReadOnlySet<VehicleId>? SelectedForcedVehicles(
        FleetSelection selection,
        IReadOnlyDictionary<string, HardVectorCandidateAssessment>? hardAssessments,
        IReadOnlySet<VehicleId>? forcedReferenceVehicles)
    {
        if (forcedReferenceVehicles is null || hardAssessments is null)
        {
            return null;
        }

        var selected = selection.VehiclePlans
            .Where(value => hardAssessments[value.Candidate.CandidateId].IsForcedReference)
            .Select(value => value.VehicleId)
            .ToFrozenSet();
        return selected.Count == 0 ? null : selected;
    }

    private static DomainResult<CandidateSelectionPreSolveAccounting>
        CreateAccounting(
            CandidateGenerationDiagnostics diagnostics,
            long validationWorkUnits,
            DeterministicCandidateSelectionExecutionBudget budget)
    {
        // G2 work (null losses when off) is charged to the same generation budget.
        var generationWork = SaturatingSum(
            diagnostics.VehicleLosses
                .Select(value => value.ExplorationWorkUnits)
                .Concat((diagnostics.RelocationLosses ?? []).Select(value => value.WorkUnits)));
        var omittedCount = SaturatingSum(
            diagnostics.Omissions.Select(value => value.Count));
        var saturated = diagnostics.Omissions.Any(value => value.CountWasSaturated)
            || diagnostics.VehicleLosses.Any(
                value => value.OmissionCountWasSaturated)
            || (diagnostics.RelocationLosses ?? []).Any(
                value => value.OmissionCountWasSaturated);
        var digest = omittedCount == 0
            ? null
            : AggregateOmissionDigest(diagnostics.Omissions);
        return CandidateSelectionPreSolveAccounting.Create(
            budget,
            generationWork,
            validationWorkUnits,
            omittedCount,
            digest,
            saturated);
    }

    private static string AggregateOmissionDigest(
        IReadOnlyList<CandidateOmissionWitness> omissions)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(OmissionAccountingDomain);

        foreach (var omission in omissions
                     .OrderBy(value => value.Code, StringComparer.Ordinal)
                     .ThenBy(value => value.StableDigest, StringComparer.Ordinal))
        {
            AppendFrame(hash, omission.Code);
            AppendFrame(hash, omission.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            AppendFrame(hash, omission.StableDigest);
            AppendFrame(hash, omission.CountWasSaturated ? "1" : "0");
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void AppendFrame(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        hash.AppendData(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(bytes.Length)));
        hash.AppendData(bytes);
    }

    private static long CountCandidates(
        IReadOnlyList<VehicleCandidateSet> candidates) =>
        SaturatingSum(candidates.Select(value => (long)value.Candidates.Count));

    private static long SaturatingSum(IEnumerable<long> values)
    {
        var sum = 0L;

        foreach (var value in values)
        {
            if (value > DomainLimits.MaxCanonicalInteger - sum)
            {
                return DomainLimits.MaxCanonicalInteger;
            }

            sum += value;
        }

        return sum;
    }

    private static SolverBackedPolicyDecisionResult AssessmentFailure(
        CommitmentAssessmentWitness witness) =>
        SolverBackedPolicyDecisionResult.Failure(
            new RollingCostWitness(
                DecisionFailureCodes.CommitmentAssessmentFailed,
                witness.Message,
                witness.VehicleId,
                witness.RequestId,
                witness.CandidateId,
                witness.Dimension));

    private sealed class FullFleetValidator(
        CommitmentMechanismContext context,
        IReadOnlyList<VehicleCandidateSet> candidates,
        ICommitmentPolicyProvider effectivePolicies,
        CommitmentDecisionValidator commitmentValidator,
        PhysicalPlanValidator physicalValidator,
        IReadOnlySet<VehicleId>? forcedReferenceVehicles,
        bool forcedNoWorse) : IFleetSelectionValidator
    {
        public CandidateSelectionValidationResult Validate(FleetSelection selection)
        {
            var physicalFailure = SelectionApplier.ValidateSelection(
                context.ReducedState,
                selection,
                physicalValidator);

            if (physicalFailure is not null)
            {
                return CandidateSelectionValidationResult.Invalid(
                    physicalFailure.Code,
                    physicalFailure.Message);
            }

            var applied = SelectionApplier.ApplySelection(
                context.ReducedState,
                selection,
                candidates);

            if (!applied.IsSuccess)
            {
                return CandidateSelectionValidationResult.Invalid(
                    applied.Witness!.Code,
                    applied.Witness.Message);
            }

            var validated = commitmentValidator.Validate(
                new CommitmentValidationContext(
                    context.BeforeEventState,
                    context.ReducedState,
                    applied.Decision!.ProposedState,
                    effectivePolicies,
                    context.StopDistances,
                    context.PublicationScope,
                    context.SourceEventSequence,
                    RevisionReasonCode: "WP4_SOLVER_SELECTION",
                    InitialPromiseTrigger: context.InitialPromiseTrigger,
                    ForcedReferenceVehicles: forcedReferenceVehicles,
                    ForcedNoWorse: forcedNoWorse));
            return validated.IsValid
                ? CandidateSelectionValidationResult.Valid()
                : CandidateSelectionValidationResult.Invalid(
                    validated.Witnesses[0].Code,
                    validated.Witnesses[0].Message);
        }
    }
}
