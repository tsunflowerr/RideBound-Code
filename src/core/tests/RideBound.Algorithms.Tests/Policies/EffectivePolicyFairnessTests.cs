using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Commitments;
using RideBound.Algorithms.Policies;
using RideBound.Application.Commitments;
using RideBound.Application.Optimization;
using RideBound.Application.State;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Runs;
using RideBound.Domain.Validation;

namespace RideBound.Algorithms.Tests.Policies;

/// <summary>
/// The WP8 budget frontier is only interpretable if a commitment budget binds every
/// arm exactly as configured. That property is not an operational convention — it is
/// decided by the effective policies that <c>SolverBackedRidePoolingPolicy</c> hands
/// the runner, which the validator then scores the decision against. These tests pin
/// them so a later edit cannot silently make one arm easier or harder than the
/// published comparison claims.
/// </summary>
public sealed class EffectivePolicyFairnessTests
{
    private const string PolicyId = "uniform-v1";
    private const long DropEtaHardLimit = 30_000;

    /// <summary>
    /// No declared kind is exempt from the cumulative burden limits: the baseline arms
    /// that were scored without them (B1 to B4) were removed, and C1 and C2 keep them.
    /// </summary>
    private static readonly RidePoolingPolicyKind[] ExemptKinds = [];

    [Theory]
    [InlineData(RidePoolingPolicyKind.RideBoundHardVector)]
    [InlineData(RidePoolingPolicyKind.CommitSoftHardHybrid)]
    public void A_commitment_arm_is_scored_against_the_configured_limits(
        RidePoolingPolicyKind kind)
    {
        var policy = EffectivePolicyFor(kind);

        Assert.Equal(
            Fingerprint(ConfiguredPolicy(DropEtaHardLimit)),
            Fingerprint(policy));
    }

    /// <summary>
    /// The drop-ETA deadline is a research probe for the deadline family. Like every
    /// cumulative limit it binds the commitment arms as configured.
    /// </summary>
    [Theory]
    [InlineData(RidePoolingPolicyKind.RideBoundHardVector)]
    [InlineData(RidePoolingPolicyKind.CommitSoftHardHybrid)]
    public void A_commitment_arm_keeps_a_configured_deadline(RidePoolingPolicyKind kind)
    {
        var policy = EffectivePolicyFor(
            kind,
            deadlineSlackMs: 30_000,
            deadlineTwoSided: true);

        Assert.Equal(
            Fingerprint(ConfiguredPolicy(DropEtaHardLimit, 30_000, true)),
            Fingerprint(policy));
    }

    /// <summary>
    /// The per-decision revision limit (P-b) and the relaxed deadline (DL-HZ) are research
    /// probes of the same kind, and the commitment arms keep them as configured.
    /// </summary>
    [Theory]
    [InlineData(RidePoolingPolicyKind.RideBoundHardVector)]
    [InlineData(RidePoolingPolicyKind.CommitSoftHardHybrid)]
    public void A_commitment_arm_keeps_a_revision_limit_and_a_relaxed_deadline(
        RidePoolingPolicyKind kind)
    {
        var policy = EffectivePolicyFor(
            kind,
            deadlineSlackMs: 30_000,
            deadlineRelaxed: true,
            revisionLimitMs: 60_000);

        Assert.Equal(60_000, policy.DropEtaRevisionLimit?.Milliseconds);
        Assert.True(policy.DropEtaDeadlineRelaxed);
        Assert.Equal(
            Fingerprint(
                ConfiguredPolicy(
                    DropEtaHardLimit,
                    30_000,
                    deadlineRelaxed: true,
                    revisionLimitMs: 60_000)),
            Fingerprint(policy));
    }

    /// <summary>
    /// A policy kind added later must make a deliberate choice. Landing in the default
    /// branch silently exempts the new arm from every limit — the exact failure mode
    /// that would make a published comparison unfair without any test going red.
    /// </summary>
    [Fact]
    public void Every_declared_policy_kind_is_classified_deliberately()
    {
        var exempted = SupportedKinds()
            .Where(kind => EffectivePolicyFor(kind).Limits.Values
                .All(limit => limit.HardLimit is null))
            .OrderBy(kind => (int)kind)
            .ToArray();

        Assert.Equal(ExemptKinds.OrderBy(kind => (int)kind), exempted);
    }

    private static string Fingerprint(CommitmentPolicy policy) =>
        string.Join(
            "|",
            new[]
            {
                policy.PolicyId,
                policy.BudgetBasis.ToString(),
                policy.MaterialRevisionRule.RawEtaThresholdMs?.ToString() ?? "none",
                policy.MaterialRevisionRule.DisplayBucketWidthMs?.ToString() ?? "none",
                policy.FreezeHorizon?.Milliseconds.ToString() ?? "none",
                ((int)policy.FreezeHorizonLocks).ToString(),
                ((int)policy.FinalConfirmationLocks).ToString(),
                policy.DropEtaDeadlineSlack?.Milliseconds.ToString() ?? "none",
                policy.DropEtaDeadlineTwoSided.ToString(),
                policy.DropEtaDeadlineRelaxed.ToString(),
                policy.DropEtaRevisionLimit?.Milliseconds.ToString() ?? "none",
            }.Concat(
                CommitmentDimensionVocabulary.Ordered.Select(
                    dimension => string.Join(
                        ":",
                        (int)dimension,
                        policy.Limits[dimension].HardLimit?.ToString() ?? "none",
                        (int)policy.Limits[dimension].ApplicablePhases))));

    private static IEnumerable<RidePoolingPolicyKind> SupportedKinds() =>
        Enum.GetValues<RidePoolingPolicyKind>();

    private static CommitmentPolicy EffectivePolicyFor(
        RidePoolingPolicyKind kind,
        long? dropEtaHardLimit = DropEtaHardLimit,
        long? deadlineSlackMs = null,
        bool deadlineTwoSided = false,
        bool deadlineRelaxed = false,
        long? revisionLimitMs = null)
    {
        var request = AlgorithmTestData.PendingRequest("request-1");
        var unadvanced = AlgorithmTestData.CreateState(
            [request],
            [AlgorithmTestData.Vehicle()]);
        var state = unadvanced with
        {
            Run = unadvanced.Run.AdvanceEpoch(
                1,
                unadvanced.Run.SimulationTime).Value!,
            NextEventSequence = 2,
        };
        var before = OnlineState.Create(
            RideBoundRun.Create(
                AlgorithmTestData.RunId,
                AlgorithmTestData.ScenarioId,
                new SimTime(0)),
            state.ExpectedInitialTravelTimeSnapshotHash);
        var configured = ConfiguredPolicy(
            dropEtaHardLimit,
            deadlineSlackMs,
            deadlineTwoSided,
            deadlineRelaxed,
            revisionLimitMs);
        var solverBudget = DeterministicSolverBudget.Create(1000, 1000, 1).Value!;
        var executionBudget =
            DeterministicCandidateSelectionExecutionBudget.Create(
                100_000,
                100_000,
                solverBudget).Value!;
        var result = new SolverBackedRidePoolingPolicy(
            new NoIncumbentSolver()).Decide(
                new CommitmentMechanismContext(
                    before,
                    state,
                    new CommitmentPolicyCatalog([configured]),
                    NoDistances.Instance,
                    "effective-policy-fairness",
                    1),
                new CandidateGenerationOptions(
                    maximumCandidatesPerVehicle: 100,
                    maximumNewRequestsPerVehicle: 1,
                    exactSmallMode: false,
                    maximumExplorationWorkUnits: 100_000),
                new SolverBackedRidePoolingPolicyOptions(kind, executionBudget),
                WarningProfiles());

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.True(
            result.Decision!.EffectivePolicies.TryGetPolicy(PolicyId, out var policy));
        return policy;
    }

    private static CommitmentPolicy ConfiguredPolicy(
        long? dropEtaHardLimit,
        long? deadlineSlackMs = null,
        bool deadlineTwoSided = false,
        bool deadlineRelaxed = false,
        long? revisionLimitMs = null) =>
        new(
            PolicyId,
            CommitmentBudgetBasis.DecisionInduced,
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    dimension == CommitmentDimension.DropEtaTotalMs
                        ? dropEtaHardLimit
                        : null,
                    dimension == CommitmentDimension.VehicleSwitchCount
                        ? CommitmentPhase.WaitingPickup
                        : CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1, 30_000),
            dropEtaDeadlineSlack: deadlineSlackMs is long slack ? new Duration(slack) : null,
            dropEtaDeadlineTwoSided: deadlineTwoSided,
            dropEtaDeadlineRelaxed: deadlineRelaxed,
            dropEtaRevisionLimit: revisionLimitMs is long limit ? new Duration(limit) : null);

    /// <summary>
    /// Only C2 consults warning profiles, but supplying them for every arm keeps the
    /// sweep uniform: an arm that starts consulting them later must still land in the
    /// classification this file pins, not fall over on a null argument.
    /// </summary>
    private static ICommitmentWarningProfileProvider WarningProfiles() =>
        new CommitmentWarningProfileCatalog(
        [
            new CommitmentWarningProfile(
                PolicyId,
                CommitmentDimensionVocabulary.Ordered.Select(
                    dimension => new CommitmentWarningLimit(dimension, null))),
        ]);

    /// <summary>
    /// The effective-policy decision is taken before the solver runs, so the cheapest
    /// solver that still drives the policy to a valid safe fallback is enough here.
    /// </summary>
    private sealed class NoIncumbentSolver : ICandidateSelectionSolver
    {
        public CandidateSelectionSolveResult Solve(
            CandidateSelectionProblem problem,
            DeterministicSolverBudget budget) =>
            CandidateSelectionSolveResult.Unknown(
                CandidateSelectionSolverDiagnostics.Create(
                    problem,
                    budget,
                    0,
                    0,
                    0,
                    []).Value!,
                "TEST_UNKNOWN",
                "The fairness test solver never produces an incumbent.");
    }

    private sealed class NoDistances : IStopDistanceLookup
    {
        public static NoDistances Instance { get; } = new();

        public bool TryGetDistanceMillimeters(
            NodeId fromNodeId,
            NodeId toNodeId,
            out long distanceMillimeters)
        {
            distanceMillimeters = 0;
            return false;
        }
    }
}
