using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Policies;
using RideBound.Application.Commitments;
using RideBound.Domain.Commitments;
using RideBound.Runner.Configuration;
using RideBound.Solvers.OrTools;

namespace RideBound.Runner.Tests.Configuration;

public sealed class Wp4RunnerConfigurationTests
{
    [Theory]
    [InlineData("wp7-fleetpy-rolling-cost-v1.json", "rolling-cost")]
    [InlineData(
        "wp7-fleetpy-ridebound-hard-vector-v1.json",
        "ridebound-hard-vector")]
    public void Wp7_fleetpy_configs_explicitly_bind_the_portfolio_strategy(
        string fileName,
        string policyId)
    {
        var commitment = CommitmentConfiguration();
        var configuration = Wp4RunnerConfiguration.Decode(
            File.ReadAllBytes(Path.Combine(
                RepositoryRoot(),
                "benchmarks",
                "configurations",
                fileName)),
            commitment);

        Assert.Equal(policyId, configuration.PolicyId);
        Assert.Equal("wp7-fleetpy-v1", configuration.PolicyVersion);
        Assert.Equal(
            InitialPromiseTrigger.BookingConfirmation,
            configuration.InitialPromiseTrigger);
        Assert.Equal(
            CandidateRetentionStrategy.ServiceSetStabilityPortfolioV1,
            configuration.CandidateGeneration.RetentionStrategy);
        Assert.NotEqual(
            configuration.ContentHash,
            configuration.BindToCommitmentConfiguration(
                commitment.ContentHash));
    }

    [Theory]
    [InlineData("wp9-fleetpy-rolling-cost-audited-v1.json", "rolling-cost")]
    [InlineData(
        "wp9-fleetpy-ridebound-hard-vector-audited-v1.json",
        "ridebound-hard-vector")]
    public void Wp9_primary_configs_require_audited_solver_evidence(
        string fileName,
        string policyId)
    {
        var configuration = Wp4RunnerConfiguration.Decode(
            File.ReadAllBytes(Path.Combine(
                RepositoryRoot(),
                "benchmarks",
                "configurations",
                fileName)),
            CommitmentConfiguration());

        Assert.Equal(policyId, configuration.PolicyId);
        Assert.Equal("wp9-confirmatory-audited-v1", configuration.PolicyVersion);
        Assert.True(configuration.EmitSolverExecutionEvidence);
        Assert.Equal(
            CandidateRetentionStrategy.ServiceSetStabilityPortfolioV1,
            configuration.CandidateGeneration.RetentionStrategy);
    }

    [Fact]
    public void Wp9_exploratory_c2_config_binds_warning_to_finite_hard_limit()
    {
        var repository = RepositoryRoot();
        var commitment = CommitmentPolicyConfiguration.Decode(
            File.ReadAllBytes(Path.Combine(
                repository,
                "benchmarks",
                "configurations",
                "wp8-drop-eta-budget-loose-v1.json")));
        var configuration = Wp4RunnerConfiguration.Decode(
            File.ReadAllBytes(Path.Combine(
                repository,
                "benchmarks",
                "configurations",
                "wp9-fleetpy-soft-hard-hybrid-audited-v1.json")),
            commitment);

        Assert.Equal(
            RidePoolingPolicyRegistry.CommitSoftHardHybrid,
            configuration.PolicyId);
        Assert.True(configuration.EmitSolverExecutionEvidence);
        Assert.True(configuration.TryGetProfile(
            "wp6-synthetic-policy-overlay-v1",
            out var profile));
        Assert.Equal(
            60_000,
            profile.Limits[CommitmentDimension.DropEtaTotalMs].WarningLimit);
    }

    [Fact]
    public void Published_configuration_is_strict_and_binds_both_configuration_hashes()
    {
        var commitment = CommitmentConfiguration();
        var bytes = File.ReadAllBytes(Path.Combine(
            RepositoryRoot(),
            "benchmarks",
            "configurations",
            "wp4-rolling-cost-boundary-v1.json"));

        var first = Wp4RunnerConfiguration.Decode(bytes, commitment);
        var second = Wp4RunnerConfiguration.Decode(bytes, commitment);
        var binding = first.BindToCommitmentConfiguration(
            commitment.ContentHash);

        Assert.Equal(RidePoolingPolicyRegistry.RollingCost, first.PolicyId);
        Assert.Equal("wp4-boundary-v1", first.PolicyVersion);
        Assert.Equal(
            OrToolsCandidateSelectionSolver.AdapterVersion,
            "google-ortools-9.15.6755");
        Assert.NotNull(first.SolverPolicyOptions);
        Assert.Null(first.MultiplePlanOptions);
        Assert.Equal(
            CandidateRetentionStrategy.LegacyAcceptedCountCostSlack,
            first.CandidateGeneration.RetentionStrategy);
        Assert.Equal(
            InitialPromiseTrigger.InitialAcceptance,
            first.InitialPromiseTrigger);
        Assert.False(first.EmitSolverExecutionEvidence);
        Assert.Equal(first.ContentHash, second.ContentHash);
        Assert.NotEqual(first.ContentHash, binding);
        Assert.NotEqual(commitment.ContentHash, binding);
        Assert.Equal(64, binding.Value.Length);
    }

    [Fact]
    public void Solver_execution_evidence_is_explicit_opt_in_and_hash_bound()
    {
        var commitment = CommitmentConfiguration();
        var legacy = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(PublishedJson()),
            commitment);
        var optedInJson = PublishedJson().Replace(
            "\"policyVersion\": \"wp4-boundary-v1\"",
            "\"policyVersion\": \"wp4-boundary-v1\",\n  " +
            "\"emitSolverExecutionEvidence\": true",
            StringComparison.Ordinal);
        var optedIn = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(optedInJson),
            commitment);

        Assert.False(legacy.EmitSolverExecutionEvidence);
        Assert.True(optedIn.EmitSolverExecutionEvidence);
        Assert.NotEqual(legacy.ContentHash, optedIn.ContentHash);

        var invalid = optedInJson.Replace(
            "\"emitSolverExecutionEvidence\": true",
            "\"emitSolverExecutionEvidence\": 1",
            StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(
            () => Wp4RunnerConfiguration.Decode(
                Encoding.UTF8.GetBytes(invalid),
                commitment));
    }

    [Fact]
    public void Retained_portfolio_profile_is_strict_solver_only_and_seed_preserving()
    {
        var commitment = CommitmentConfiguration();
        var baseJson = PublishedJson();
        var optedInJson = baseJson.Replace(
            "\"policyVersion\": \"wp4-boundary-v1\"",
            "\"policyVersion\": \"wp4-boundary-v1\",\n  " +
            "\"emitSolverExecutionEvidence\": true,\n  " +
            "\"solverExecutionEvidenceProfile\": " +
            "\"retained-portfolio-v1\"",
            StringComparison.Ordinal);

        var configuration = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(optedInJson),
            commitment);

        Assert.Equal(
            Wp4RunnerConfiguration.RetainedPortfolioEvidenceProfile,
            configuration.SolverExecutionEvidenceProfile);
        Assert.True(configuration.EmitSolverExecutionEvidence);
        Assert.True(
            configuration.SolverPolicyOptions!
                .CaptureCandidatePortfolioEvidence);
        var seeded = configuration.CreateSolverPolicyOptionsForRun(19);
        Assert.True(seeded.CaptureCandidatePortfolioEvidence);
        Assert.Equal(19, seeded.ExecutionBudget.SolverBudget.RandomSeed);
        Assert.NotEqual(
            Wp4RunnerConfiguration.Decode(
                Encoding.UTF8.GetBytes(baseJson),
                commitment).ContentHash,
            configuration.ContentHash);

        var noBaseEvidence = optedInJson.Replace(
            "\"emitSolverExecutionEvidence\": true,\n  ",
            string.Empty,
            StringComparison.Ordinal);
        Assert.Contains(
            "emitSolverExecutionEvidence=true",
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(noBaseEvidence),
                    commitment)).Message);

        var unknown = optedInJson.Replace(
            "retained-portfolio-v1",
            "unknown-portfolio",
            StringComparison.Ordinal);
        Assert.Contains(
            "Unknown solverExecutionEvidenceProfile",
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(unknown),
                    commitment)).Message);

        const string nonSolver =
            """
            {
              "configurationVersion":"1.0.0",
              "policyId":"least-commitment-consensus",
              "policyVersion":"wp13-evidence-negative-v1",
              "candidateGeneration":{
                "maximumCandidatesPerVehicle":10,
                "maximumNewRequestsPerVehicle":1,
                "exactSmallMode":false,
                "scheduleStrategy":"earliest-feasible",
                "maximumExplorationWorkUnits":100
              },
              "multiplePlan":{
                "maximumPlanCount":2,
                "maximumCombinationWorkUnits":100,
                "requireCompleteEnumeration":true
              },
              "emitSolverExecutionEvidence":true,
              "solverExecutionEvidenceProfile":"retained-portfolio-v1"
            }
            """;
        Assert.Contains(
            "solver-backed execution",
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(nonSolver),
                    commitment)).Message);
    }

    [Fact]
    public void Full_witness_collection_needs_its_own_profile()
    {
        var commitment = CommitmentConfiguration();
        var baseJson = PublishedJson();

        // Off by default, so the validator keeps its fail-fast path and its cost.
        Assert.False(
            Wp4RunnerConfiguration.Decode(
                Encoding.UTF8.GetBytes(baseJson),
                commitment).CollectAllCommitmentWitnesses);

        // The frozen E1 configurations declare retained-portfolio-v1. Widening
        // that profile in place would change their recorded witnesses and so
        // their decision hashes, so it must never collect the full set.
        var retained = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(
                WithProfile(
                    baseJson,
                    Wp4RunnerConfiguration.RetainedPortfolioEvidenceProfile)),
            commitment);
        Assert.False(retained.CollectAllCommitmentWitnesses);
        Assert.True(
            retained.SolverPolicyOptions!.CaptureCandidatePortfolioEvidence);

        var fullWitness = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(
                WithProfile(
                    baseJson,
                    Wp4RunnerConfiguration
                        .RetainedPortfolioFullWitnessEvidenceProfile)),
            commitment);
        Assert.True(fullWitness.CollectAllCommitmentWitnesses);
        Assert.True(
            fullWitness.SolverPolicyOptions!.CaptureCandidatePortfolioEvidence);
        Assert.NotEqual(retained.ContentHash, fullWitness.ContentHash);

        Assert.Throws<InvalidDataException>(
            () => Wp4RunnerConfiguration.Decode(
                Encoding.UTF8.GetBytes(
                    WithProfile(baseJson, "retained-portfolio-v9")),
                commitment));
    }

    [Fact]
    public void Frozen_e1_configurations_do_not_collect_the_full_witness_set()
    {
        var commitment = CommitmentConfiguration();

        foreach (var name in new[]
                 {
                     "wp13-e1-fleetpy-rolling-cost-retained-v1.json",
                     "wp13-e1-fleetpy-ridebound-hard-vector-retained-v1.json",
                 })
        {
            var json = File.ReadAllBytes(
                Path.Combine(
                    RepositoryRoot(),
                    "benchmarks",
                    "configurations",
                    name));
            var configuration = Wp4RunnerConfiguration.Decode(json, commitment);

            Assert.Equal(
                Wp4RunnerConfiguration.RetainedPortfolioEvidenceProfile,
                configuration.SolverExecutionEvidenceProfile);
            Assert.False(
                configuration.CollectAllCommitmentWitnesses,
                $"{name} must keep the evidence its freeze receipt recorded");
        }
    }

    private static string WithProfile(string baseJson, string profile) =>
        baseJson.Replace(
            "\"policyVersion\": \"wp4-boundary-v1\"",
            "\"policyVersion\": \"wp4-boundary-v1\",\n  " +
            "\"emitSolverExecutionEvidence\": true,\n  " +
            "\"solverExecutionEvidenceProfile\": " +
            $"\"{profile}\"",
            StringComparison.Ordinal);

    [Fact]
    public void Constant_level_skip_is_optional_and_off_unless_declared()
    {
        var commitment = CommitmentConfiguration();
        var baseJson = PublishedJson();

        // Every configuration written before RB-WP14-002 omits the field and must
        // keep the solver work, and therefore the decision hash, it recorded.
        var published = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(baseJson),
            commitment);

        Assert.False(
            published.SolverPolicyOptions!.ExecutionBudget.SolverBudget
                .SkipConstantObjectiveLevels);
        Assert.False(
            published.CreateSolverPolicyOptionsForRun(19).ExecutionBudget
                .SolverBudget.SkipConstantObjectiveLevels);

        var optedInJson = baseJson.Replace(
            "\"randomSeed\": 7",
            "\"randomSeed\": 7,\n    \"skipConstantObjectiveLevels\": true",
            StringComparison.Ordinal);
        var optedIn = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(optedInJson),
            commitment);

        Assert.True(
            optedIn.SolverPolicyOptions!.ExecutionBudget.SolverBudget
                .SkipConstantObjectiveLevels);
        Assert.True(
            optedIn.CreateSolverPolicyOptionsForRun(19).ExecutionBudget
                .SolverBudget.SkipConstantObjectiveLevels);

        // Opting in is a different configuration and must not share a hash.
        Assert.NotEqual(published.ContentHash, optedIn.ContentHash);

        var explicitlyOff = baseJson.Replace(
            "\"randomSeed\": 7",
            "\"randomSeed\": 7,\n    \"skipConstantObjectiveLevels\": false",
            StringComparison.Ordinal);

        Assert.False(
            Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(explicitlyOff),
                    commitment)
                .SolverPolicyOptions!.ExecutionBudget.SolverBudget
                .SkipConstantObjectiveLevels);

        var wrongType = baseJson.Replace(
            "\"randomSeed\": 7",
            "\"randomSeed\": 7,\n    \"skipConstantObjectiveLevels\": \"yes\"",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(
            () => Wp4RunnerConfiguration.Decode(
                Encoding.UTF8.GetBytes(wrongType),
                commitment));
    }

    [Fact]
    public void Candidate_retention_strategy_is_explicit_for_new_configs_and_legacy_when_absent()
    {
        var commitment = CommitmentConfiguration();
        var legacy = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(PublishedJson()),
            commitment);
        var portfolioJson = PublishedJson().Replace(
            "\"scheduleStrategy\": \"earliest-feasible\"",
            "\"scheduleStrategy\": \"earliest-feasible\",\n    " +
            "\"retentionStrategy\": \"service-set-stability-portfolio-v1\"",
            StringComparison.Ordinal);
        var portfolio = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(portfolioJson),
            commitment);

        Assert.Equal(
            CandidateRetentionStrategy.LegacyAcceptedCountCostSlack,
            legacy.CandidateGeneration.RetentionStrategy);
        Assert.Equal(
            CandidateRetentionStrategy.ServiceSetStabilityPortfolioV1,
            portfolio.CandidateGeneration.RetentionStrategy);
        Assert.Equal(
            InitialPromiseTrigger.InitialAcceptance,
            portfolio.InitialPromiseTrigger);

        var unknown = portfolioJson.Replace(
            "service-set-stability-portfolio-v1",
            "unknown-retention",
            StringComparison.Ordinal);
        Assert.Contains(
            "retentionStrategy",
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(unknown),
                    commitment)).Message);

        var unknownTrigger = portfolioJson.Replace(
            "\"policyVersion\": \"wp4-boundary-v1\"",
            "\"policyVersion\": \"wp4-boundary-v1\",\n  " +
            "\"initialPromiseTrigger\": \"unknown-trigger\"",
            StringComparison.Ordinal);
        Assert.Contains(
            "initialPromiseTrigger",
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(unknownTrigger),
                    commitment)).Message);
    }

    [Fact]
    public void Policy_specific_fields_are_required_and_forbidden_exactly()
    {
        var commitment = CommitmentConfiguration();
        var rolling = PublishedJson();
        var missingFreeze = rolling.Replace(
            "\"rolling-cost\"",
            "\"fixed-freeze-horizon\"",
            StringComparison.Ordinal);
        var unexpectedRepair = rolling.Replace(
            "\n  \"solverExecution\"",
            "\n  \"repair\": {\"maximumRequestsConsideredPerVehicle\":1},\n  \"solverExecution\"",
            StringComparison.Ordinal);

        Assert.Contains(
            "variant fields",
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(missingFreeze),
                    commitment)).Message);
        Assert.Contains(
            "variant fields",
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(unexpectedRepair),
                    commitment)).Message);
    }

    [Fact]
    public void C2_requires_all_profiles_and_rejects_warning_above_hard_limit()
    {
        var commitment = CommitmentConfiguration();
        var limits = string.Join(
            ",",
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension =>
                    $"{{\"dimension\":\"{CommitmentDimensionVocabulary.ToProtocolValue(dimension)}\"}}"));
        var warningProfiles =
            $"[{{\"policyId\":\"uniform-v1\",\"limits\":[{limits}]}}]";
        var c2 = PublishedJson()
            .Replace(
                "\"rolling-cost\"",
                "\"commit-soft-hard-hybrid\"",
                StringComparison.Ordinal)
            .Replace(
                "\n  \"solverExecution\"",
                $"\n  \"warningProfiles\":{warningProfiles},\n  \"solverExecution\"",
                StringComparison.Ordinal);

        var valid = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(c2),
            commitment);
        Assert.True(valid.TryGetProfile("uniform-v1", out var profile));
        Assert.All(profile.Limits.Values, value => Assert.Null(value.WarningLimit));

        var invalid = c2.Replace(
            "\"dimension\":\"vehicle_switch_count\"",
            "\"dimension\":\"vehicle_switch_count\",\"warningLimit\":1",
            StringComparison.Ordinal);
        Assert.Contains(
            "cannot exceed",
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(invalid),
                    commitment)).Message);
    }

    [Fact]
    public void B5_uses_explicit_plan_pool_budget_and_forbids_solver_block()
    {
        var commitment = CommitmentConfiguration();
        var b5 =
            """
            {
              "configurationVersion":"1.0.0",
              "policyId":"least-commitment-consensus",
              "policyVersion":"wp4-b5-test-v1",
              "candidateGeneration":{
                "maximumCandidatesPerVehicle":100,
                "maximumNewRequestsPerVehicle":2,
                "exactSmallMode":false,
                "scheduleStrategy":"earliest-feasible",
                "maximumExplorationWorkUnits":10000
              },
              "multiplePlan":{
                "maximumPlanCount":4,
                "maximumCombinationWorkUnits":10000,
                "requireCompleteEnumeration":true
              }
            }
            """;

        var configuration = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(b5),
            commitment);

        Assert.Null(configuration.SolverPolicyOptions);
        Assert.Equal(4, configuration.MultiplePlanOptions!.MaximumPlanCount);
        Assert.True(configuration.MultiplePlanOptions.RequireCompleteEnumeration);
    }

    [Fact]
    public void Selection_objective_order_is_optional_stability_first_and_cost_first_only_for_C1()
    {
        var commitment = CommitmentConfiguration();
        var absent = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(HardVectorJson(null)),
            commitment);
        var stability = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(HardVectorJson("stability-first")),
            commitment);
        var cost = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(HardVectorJson("cost-first")),
            commitment);

        Assert.Equal(
            SolverBackedSelectionObjectiveOrder.StabilityFirst,
            absent.SolverPolicyOptions!.SelectionObjectiveOrder);
        Assert.Equal(
            SolverBackedSelectionObjectiveOrder.StabilityFirst,
            absent.CreateSolverPolicyOptionsForRun(19).SelectionObjectiveOrder);
        Assert.Equal(
            SolverBackedSelectionObjectiveOrder.StabilityFirst,
            stability.SolverPolicyOptions!.SelectionObjectiveOrder);
        Assert.Equal(
            SolverBackedSelectionObjectiveOrder.CostFirst,
            cost.SolverPolicyOptions!.SelectionObjectiveOrder);
        Assert.Equal(
            SolverBackedSelectionObjectiveOrder.CostFirst,
            cost.CreateSolverPolicyOptionsForRun(19).SelectionObjectiveOrder);
        // Only the order differs; the policy, gates and budgets are the same.
        Assert.Equal(absent.PolicyId, cost.PolicyId);
        Assert.Equal(absent.SolverPolicyOptions.PolicyKind, cost.SolverPolicyOptions.PolicyKind);
        Assert.Equal(
            absent.SolverPolicyOptions.ExecutionBudget.MaximumGenerationWorkUnits,
            cost.SolverPolicyOptions.ExecutionBudget.MaximumGenerationWorkUnits);
        Assert.Equal(
            absent.SolverPolicyOptions.ExecutionBudget.MaximumValidationWorkUnits,
            cost.SolverPolicyOptions.ExecutionBudget.MaximumValidationWorkUnits);
        Assert.Equal(
            absent.SolverPolicyOptions.ExecutionBudget.SolverBudget.MaximumWorkUnits,
            cost.SolverPolicyOptions.ExecutionBudget.SolverBudget.MaximumWorkUnits);
        Assert.Equal(
            absent.SolverPolicyOptions.ForcedReferenceRecovery,
            cost.SolverPolicyOptions.ForcedReferenceRecovery);
        Assert.Equal(absent.CandidateGeneration, cost.CandidateGeneration);
        // Declaring the key is a different configuration, even with the default value, so a
        // run that must reproduce an archived binding omits it.
        Assert.NotEqual(absent.ContentHash, stability.ContentHash);
        Assert.NotEqual(stability.ContentHash, cost.ContentHash);

        foreach (var invalid in new[] { "\"cost-first-v2\"", "\"\"", "true", "1" })
        {
            var json = HardVectorJson(null).Replace(
                "\"policyVersion\": \"wp7-fleetpy-v1\"",
                "\"policyVersion\": \"wp7-fleetpy-v1\",\n  \"selectionObjectiveOrder\": " + invalid,
                StringComparison.Ordinal);
            Assert.Contains(
                "selectionObjectiveOrder",
                Assert.Throws<InvalidDataException>(
                    () => Wp4RunnerConfiguration.Decode(
                        Encoding.UTF8.GetBytes(json),
                        commitment)).Message);
        }

        var rollingCost = PublishedJson().Replace(
            "\"policyVersion\": \"wp4-boundary-v1\"",
            "\"policyVersion\": \"wp4-boundary-v1\",\n  \"selectionObjectiveOrder\": \"cost-first\"",
            StringComparison.Ordinal);
        Assert.Contains(
            "requires the solver-backed C1 policy",
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(rollingCost),
                    commitment)).Message);

        var repository = RepositoryRoot();
        var c2Commitment = CommitmentPolicyConfiguration.Decode(
            File.ReadAllBytes(Path.Combine(
                repository,
                "benchmarks",
                "configurations",
                "wp8-drop-eta-budget-loose-v1.json")));
        var c2 = File.ReadAllText(Path.Combine(
                repository,
                "benchmarks",
                "configurations",
                "wp9-fleetpy-soft-hard-hybrid-audited-v1.json"))
            .Replace(
                "\"configurationVersion\": \"1.0.0\"",
                "\"configurationVersion\": \"1.0.0\",\n  \"selectionObjectiveOrder\": \"cost-first\"",
                StringComparison.Ordinal);
        Assert.Contains("selectionObjectiveOrder", c2, StringComparison.Ordinal);
        Assert.Contains(
            "requires the solver-backed C1 policy",
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(c2),
                    c2Commitment)).Message);
    }

    /// <summary>
    /// Text of the Tier-3 WP4 configuration `ridebound-scratchpad/tang3/configs/wp4-tier3-v2.json`
    /// (file SHA-256 66e9b59249d9491d520f3ed592e8c878a0d3b100e39432d5f4b73321617b4518), the one
    /// every archived T3.8 run used. The binding hashes its canonical form, so the whitespace of
    /// this copy does not matter. It has no `selectionObjectiveOrder` key.
    /// </summary>
    private const string Tier3Wp4V2Json =
        """
        {
          "configurationVersion": "1.0.0",
          "policyId": "ridebound-hard-vector",
          "policyVersion": "tier3-exploratory-v1",
          "initialPromiseTrigger": "booking-confirmation",
          "emitSolverExecutionEvidence": true,
          "commitmentRecovery": "no-worse-than-reference-v1",
          "lateBoarding": "record-v1",
          "candidateGeneration": {
            "maximumCandidatesPerVehicle": 100,
            "maximumNewRequestsPerVehicle": 2,
            "exactSmallMode": false,
            "scheduleStrategy": "earliest-feasible",
            "maximumExplorationWorkUnits": 10000,
            "retentionStrategy": "service-set-stability-portfolio-v1"
          },
          "solverExecution": {
            "adapterVersion": "google-ortools-9.15.6755",
            "maximumGenerationWorkUnits": 10000,
            "maximumValidationWorkUnits": 10000,
            "maximumSolverWorkUnits": 100000,
            "maximumSolverDeterministicTimeMicros": 1000000,
            "randomSeed": 7
          }
        }
        """;

    [Fact]
    public void A_configuration_without_the_order_key_keeps_the_archived_tier3_bindings()
    {
        // ADR-078 equivalence gate. The policyConfigurationHash that every archived T3.8 run
        // sent in initializeRun (C-30: 51/51 runs, U: 34/34 runs under
        // C:/RideBoundData/research/tier3-t38-v1) is recomputed from the same inputs. C-30 is
        // byte-identical to wp14-c1-h6-reference-v1.json; U removes its drop hard limit; C-120
        // sets it to 120 s (that binding was derived, not archived).
        var reference = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "benchmarks",
            "configurations",
            "wp14-c1-h6-reference-v1.json"));
        var wp4 = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(Tier3Wp4V2Json),
            CommitmentPolicyConfiguration.Decode(Encoding.UTF8.GetBytes(reference)));

        Assert.Equal(
            "fb2813c4036d309490e12df77e37de1683b92b92a9ebdd71e16cad35d50ecd42",
            wp4.ContentHash.Value);
        Assert.Equal(
            SolverBackedSelectionObjectiveOrder.StabilityFirst,
            wp4.SolverPolicyOptions!.SelectionObjectiveOrder);

        foreach (var (dropLimitMs, commitmentHash, binding) in new (long?, string, string)[]
                 {
                     (30_000, "f07395f5f2c967e0d89cceb4a55b23c4f896971db071d8c7876f05e20ef09884",
                         "a2f11a3ada0773f9b30b85c56c935058e2e931f13c907f46b2ddfff8c1285f1c"),
                     (null, "7fc79a433abe1796830ecceeac280da0e0af3871daf268dda8d6fc93b270370d",
                         "251b331eb56fe870e734d651bf6937e95304919ed92967b2f3b0d0b9da1ac346"),
                     (120_000, "8dc9350b1d6b656b30d37ad23bac0dc4e4188f222427be47d6b007e060f2a5a2",
                         "a9ac58b33558c6ec2d604ee7fe3e7fdb02deaecfd866ea73282b911326ed9010"),
                 })
        {
            var json = JsonNode.Parse(reference)!;
            var drop = json["policies"]![0]!["limits"]!.AsArray()
                .Single(value => (string?)value!["dimension"] == "drop_eta_total_ms")!
                .AsObject();
            Assert.Equal(30_000, (long)drop["hardLimit"]!);

            if (dropLimitMs is null)
            {
                drop.Remove("hardLimit");
            }
            else
            {
                drop["hardLimit"] = dropLimitMs.Value;
            }

            var commitment = CommitmentPolicyConfiguration.Decode(
                Encoding.UTF8.GetBytes(json.ToJsonString()));

            Assert.Equal(commitmentHash, commitment.ContentHash.Value);
            Assert.Equal(binding, wp4.BindToCommitmentConfiguration(commitment.ContentHash).Value);
        }

        var costFirst = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(Tier3Wp4V2Json.Replace(
                "\"lateBoarding\": \"record-v1\"",
                "\"lateBoarding\": \"record-v1\",\n  \"selectionObjectiveOrder\": \"cost-first\"",
                StringComparison.Ordinal)),
            CommitmentPolicyConfiguration.Decode(Encoding.UTF8.GetBytes(reference)));

        Assert.Equal(
            SolverBackedSelectionObjectiveOrder.CostFirst,
            costFirst.SolverPolicyOptions!.SelectionObjectiveOrder);
        Assert.NotEqual(wp4.ContentHash, costFirst.ContentHash);
    }

    [Fact]
    public void A_per_rider_drop_budget_is_accepted_by_C1_only_under_cost_first()
    {
        // Hard utilization reads the fixed hard limit only, so under stability-first the per-rider
        // budget would silently leave the ranking; the configuration fails closed instead.
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "benchmarks",
            "configurations",
            "wp14-c1-h6-reference-v1.json")))!;
        var policy = json["policies"]![0]!.AsObject();
        policy["limits"]!.AsArray()
            .Single(value => (string?)value!["dimension"] == "drop_eta_total_ms")!
            .AsObject()
            .Remove("hardLimit");
        policy["dropEtaBudgetSlackPermille"] = 350;
        var commitment = CommitmentPolicyConfiguration.Decode(
            Encoding.UTF8.GetBytes(json.ToJsonString()));
        var costFirstJson = Tier3Wp4V2Json.Replace(
            "\"lateBoarding\": \"record-v1\"",
            "\"lateBoarding\": \"record-v1\",\n  \"selectionObjectiveOrder\": \"cost-first\"",
            StringComparison.Ordinal);

        var costFirst = Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(costFirstJson), commitment);
        var rollingCost = Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(PublishedJson()), commitment);

        Assert.Equal(
            SolverBackedSelectionObjectiveOrder.CostFirst,
            costFirst.SolverPolicyOptions!.SelectionObjectiveOrder);
        Assert.Equal(RidePoolingPolicyRegistry.RollingCost, rollingCost.PolicyId);

        foreach (var stabilityFirst in new[]
                 {
                     Tier3Wp4V2Json,
                     costFirstJson.Replace("\"cost-first\"", "\"stability-first\"", StringComparison.Ordinal),
                 })
        {
            Assert.Contains(
                "'dropEtaBudgetSlackPermille' is not ranked by hard utilization",
                Assert.Throws<InvalidDataException>(
                    () => Wp4RunnerConfiguration.Decode(
                        Encoding.UTF8.GetBytes(stabilityFirst),
                        commitment)).Message);
        }
    }

    private static string HardVectorJson(string? selectionObjectiveOrder)
    {
        var json = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "benchmarks",
            "configurations",
            "wp7-fleetpy-ridebound-hard-vector-v1.json"));
        return selectionObjectiveOrder is null
            ? json
            : json.Replace(
                "\"policyVersion\": \"wp7-fleetpy-v1\"",
                "\"policyVersion\": \"wp7-fleetpy-v1\",\n  \"selectionObjectiveOrder\": \""
                    + selectionObjectiveOrder + "\"",
                StringComparison.Ordinal);
    }

    private static CommitmentPolicyConfiguration CommitmentConfiguration() =>
        CommitmentPolicyConfiguration.Decode(
            File.ReadAllBytes(Path.Combine(
                RepositoryRoot(),
                "benchmarks",
                "configurations",
                "wp3-boundary-test-v1.json")));

    private static string PublishedJson() =>
        File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "benchmarks",
            "configurations",
            "wp4-rolling-cost-boundary-v1.json"));

    private static string RepositoryRoot() =>
        typeof(Wp4RunnerConfigurationTests)
            .Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(value => value.Key == "RideBoundRepositoryRoot")
            .Value!;
}
