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
    public void Published_configuration_is_strict_and_binds_both_configuration_hashes()
    {
        var commitment = CommitmentConfiguration();
        var bytes = Encoding.UTF8.GetBytes(FinalJson());

        var first = Wp4RunnerConfiguration.Decode(bytes, commitment);
        var second = Wp4RunnerConfiguration.Decode(bytes, commitment);
        var binding = first.BindToCommitmentConfiguration(
            commitment.ContentHash);

        Assert.Equal(RidePoolingPolicyRegistry.RideBoundHardVector, first.PolicyId);
        Assert.Equal("tier3-exploratory-v1", first.PolicyVersion);
        Assert.Equal(
            OrToolsCandidateSelectionSolver.AdapterVersion,
            "google-ortools-9.15.6755");
        Assert.NotNull(first.SolverPolicyOptions);
        Assert.Equal(
            CandidateRetentionStrategy.ServiceSetStabilityPortfolioV1,
            first.CandidateGeneration.RetentionStrategy);
        Assert.Equal(
            InitialPromiseTrigger.BookingConfirmation,
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
            Encoding.UTF8.GetBytes(FinalJson(json => json.Remove("emitSolverExecutionEvidence"))),
            commitment);
        var optedInJson = FinalJson(json => json["emitSolverExecutionEvidence"] = true);
        var optedIn = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(optedInJson),
            commitment);

        Assert.False(legacy.EmitSolverExecutionEvidence);
        Assert.True(optedIn.EmitSolverExecutionEvidence);
        Assert.NotEqual(legacy.ContentHash, optedIn.ContentHash);

        var invalid = Edit(optedInJson, json => json["emitSolverExecutionEvidence"] = 1);
        Assert.Throws<InvalidDataException>(
            () => Wp4RunnerConfiguration.Decode(
                Encoding.UTF8.GetBytes(invalid),
                commitment));
    }

    [Fact]
    public void Retained_portfolio_profile_is_strict_solver_only_and_seed_preserving()
    {
        var commitment = CommitmentConfiguration();
        var baseJson = FinalJson();
        var optedInJson = WithProfile(baseJson, "retained-portfolio-v1");

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

        var noBaseEvidence = Edit(optedInJson, json => json.Remove("emitSolverExecutionEvidence"));
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
              "policyId":"ridebound-hard-vector",
              "policyVersion":"wp13-evidence-negative-v1",
              "candidateGeneration":{
                "maximumCandidatesPerVehicle":10,
                "maximumNewRequestsPerVehicle":1,
                "exactSmallMode":false,
                "scheduleStrategy":"earliest-feasible",
                "maximumExplorationWorkUnits":100
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
        var baseJson = FinalJson();

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
        Edit(baseJson, json =>
        {
            json["emitSolverExecutionEvidence"] = true;
            json["solverExecutionEvidenceProfile"] = profile;
        });

    [Fact]
    public void Constant_level_skip_is_optional_and_off_unless_declared()
    {
        var commitment = CommitmentConfiguration();
        var baseJson = FinalJson();

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

        var optedInJson = Edit(baseJson, json => json["solverExecution"]!["skipConstantObjectiveLevels"] = true);
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

        var explicitlyOff = Edit(baseJson, json => json["solverExecution"]!["skipConstantObjectiveLevels"] = false);

        Assert.False(
            Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(explicitlyOff),
                    commitment)
                .SolverPolicyOptions!.ExecutionBudget.SolverBudget
                .SkipConstantObjectiveLevels);

        var wrongType = Edit(baseJson, json => json["solverExecution"]!["skipConstantObjectiveLevels"] = "yes");

        Assert.Throws<InvalidDataException>(
            () => Wp4RunnerConfiguration.Decode(
                Encoding.UTF8.GetBytes(wrongType),
                commitment));
    }

    [Fact]
    public void Candidate_retention_strategy_is_explicit_for_new_configs_and_legacy_when_absent()
    {
        // The final configuration without the two keys whose absence selects the archived defaults.
        var commitment = CommitmentConfiguration();
        var legacyJson = FinalJson(json =>
        {
            json.Remove("initialPromiseTrigger");
            json["candidateGeneration"]!.AsObject().Remove("retentionStrategy");
        });
        var legacy = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(legacyJson),
            commitment);
        var portfolioJson = Edit(
            legacyJson,
            json => json["candidateGeneration"]!["retentionStrategy"] = "service-set-stability-portfolio-v1");
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

        var unknownTrigger = Edit(portfolioJson, json => json["initialPromiseTrigger"] = "unknown-trigger");
        Assert.Contains(
            "initialPromiseTrigger",
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(unknownTrigger),
                    commitment)).Message);
    }

    [Fact]
    public void The_solver_execution_block_is_required()
    {
        var missingSolver = FinalJson(json => Assert.True(json.Remove("solverExecution")));

        Assert.Equal(
            "WP4 policy variant fields do not match the selected policyId.",
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(missingSolver),
                    CommitmentConfiguration())).Message);
    }

    [Fact]
    public void The_removed_multiple_plan_policy_is_refused_with_a_pointer_to_the_archive()
    {
        const string refusal =
            "WP4 policyId 'least-commitment-consensus' and the 'multiplePlan' block (the multiple-plan policy): "
            + "removed; see commit ebca912 (branch research/final-real-runs-2026-11-06)";
        var commitment = CommitmentConfiguration();
        const string b5 =
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
        var policyOnly = JsonNode.Parse(b5)!.AsObject();
        policyOnly.Remove("multiplePlan");
        var blockOnC1 = JsonNode.Parse(HardVectorJson(null))!.AsObject();
        blockOnC1["multiplePlan"] = JsonNode.Parse(b5)!["multiplePlan"]!.DeepClone();
        Assert.NotNull(
            Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(HardVectorJson(null)), commitment));

        // The B5 file the old test decoded, its policyId alone, and its block added to the valid C1 file.
        foreach (var json in new[] { b5, policyOnly.ToJsonString(), blockOnC1.ToJsonString() })
        {
            Assert.Equal(
                refusal,
                Assert.Throws<InvalidDataException>(
                    () => Wp4RunnerConfiguration.Decode(
                        Encoding.UTF8.GetBytes(json),
                        commitment)).Message);
        }
    }

    [Fact]
    public void The_removed_rolling_cost_policy_is_refused_with_a_pointer_to_the_archive()
    {
        const string refusal =
            "WP4 policyId 'rolling-cost' (the rolling-cost policy): "
            + "removed; see commit ebca912 (branch research/final-real-runs-2026-11-06)";
        var commitment = CommitmentConfiguration();
        var archived = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "benchmarks",
            "configurations",
            "wp4-rolling-cost-boundary-v1.json"));
        Assert.NotNull(Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(FinalJson()), commitment));

        // The rolling-cost file the old tests decoded, and the final C1 file with policyId rolling-cost.
        foreach (var json in new[] { archived, FinalJson(json => json["policyId"] = "rolling-cost") })
        {
            Assert.Equal(
                refusal,
                Assert.Throws<InvalidDataException>(
                    () => Wp4RunnerConfiguration.Decode(
                        Encoding.UTF8.GetBytes(json),
                        commitment)).Message);
        }
    }

    [Fact]
    public void The_removed_rolling_penalty_policy_is_refused_with_a_pointer_to_the_archive()
    {
        const string refusal =
            "WP4 policyId 'rolling-penalty' (the rolling-penalty policy): "
            + "removed; see commit ebca912 (branch research/final-real-runs-2026-11-06)";
        var commitment = CommitmentConfiguration();
        var decodedBefore = FinalJson(json =>
        {
            json["policyId"] = "rolling-penalty";
            Assert.True(json.Remove("selectionObjectiveOrder"));
            Assert.True(json.Remove("commitmentRecovery"));
        });
        Assert.NotNull(Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(FinalJson()), commitment));

        // The rolling-penalty file the old tests decoded (the final C1 file without its C1-only keys), and the final
        // C1 file with policyId rolling-penalty.
        foreach (var json in new[] { decodedBefore, FinalJson(json => json["policyId"] = "rolling-penalty") })
        {
            Assert.Equal(
                refusal,
                Assert.Throws<InvalidDataException>(
                    () => Wp4RunnerConfiguration.Decode(
                        Encoding.UTF8.GetBytes(json),
                        commitment)).Message);
        }
    }

    [Fact]
    public void The_removed_fixed_freeze_horizon_policy_is_refused_with_a_pointer_to_the_archive()
    {
        const string refusal =
            "WP4 policyId 'fixed-freeze-horizon' and the 'fixedFreeze' block (the fixed-freeze-horizon policy): "
            + "removed; see commit ebca912 (branch research/final-real-runs-2026-11-06)";
        var commitment = CommitmentConfiguration();
        var block = new JsonObject { ["horizonMs"] = 60_000, ["locks"] = new JsonArray("vehicle") };
        var decodedBefore = FinalJson(json =>
        {
            json["policyId"] = "fixed-freeze-horizon";
            Assert.True(json.Remove("selectionObjectiveOrder"));
            Assert.True(json.Remove("commitmentRecovery"));
            json["fixedFreeze"] = block.DeepClone();
        });
        Assert.NotNull(Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(FinalJson()), commitment));

        // A fixed-freeze-horizon file that was decoded before (the final C1 file without its C1-only keys, with the
        // fixedFreeze block), the final C1 file with policyId fixed-freeze-horizon, and the block added to the final
        // C1 file and to the removed B4 file (the fixed-freeze-horizon check runs before the B4 check).
        foreach (var json in new[]
                 {
                     decodedBefore,
                     FinalJson(json => json["policyId"] = "fixed-freeze-horizon"),
                     FinalJson(json => json["fixedFreeze"] = block.DeepClone()),
                     RepairJson(json => json["fixedFreeze"] = block.DeepClone()),
                 })
        {
            Assert.Equal(
                refusal,
                Assert.Throws<InvalidDataException>(
                    () => Wp4RunnerConfiguration.Decode(
                        Encoding.UTF8.GetBytes(json),
                        commitment)).Message);
        }
    }

    [Fact]
    public void The_removed_no_reassignment_repair_policy_is_refused_with_a_pointer_to_the_archive()
    {
        const string refusal =
            "WP4 policyId 'no-reassignment-repair' and the 'repair' block (the no-reassignment-repair policy): "
            + "removed; see commit ebca912 (branch research/final-real-runs-2026-11-06)";
        var commitment = CommitmentConfiguration();
        var block = new JsonObject { ["maximumRequestsConsideredPerVehicle"] = 1 };
        Assert.NotNull(Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(FinalJson()), commitment));

        // The no-reassignment-repair file that was decoded before, the final C1 file with policyId
        // no-reassignment-repair, and the repair block added to the final C1 file.
        foreach (var json in new[]
                 {
                     RepairJson(),
                     FinalJson(json => json["policyId"] = "no-reassignment-repair"),
                     FinalJson(json => json["repair"] = block.DeepClone()),
                 })
        {
            Assert.Equal(
                refusal,
                Assert.Throws<InvalidDataException>(
                    () => Wp4RunnerConfiguration.Decode(
                        Encoding.UTF8.GetBytes(json),
                        commitment)).Message);
        }
    }

    [Fact]
    public void The_removed_soft_hard_hybrid_policy_is_refused_with_a_pointer_to_the_archive()
    {
        const string refusal =
            "WP4 policyId 'commit-soft-hard-hybrid' and the 'warningProfiles' block (the soft-hard hybrid policy): "
            + "removed; see commit ebca912 (branch research/final-real-runs-2026-11-06)";
        var commitment = CommitmentConfiguration();
        var repository = RepositoryRoot();
        var archivedCommitment = CommitmentPolicyConfiguration.Decode(
            File.ReadAllBytes(Path.Combine(
                repository,
                "benchmarks",
                "configurations",
                "wp8-drop-eta-budget-loose-v1.json")));
        var archived = File.ReadAllText(Path.Combine(
            repository,
            "benchmarks",
            "configurations",
            "wp9-fleetpy-soft-hard-hybrid-audited-v1.json"));
        var limits = string.Join(
            ",",
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension =>
                    $"{{\"dimension\":\"{CommitmentDimensionVocabulary.ToProtocolValue(dimension)}\"}}"));
        var profiles = JsonNode.Parse($"[{{\"policyId\":\"uniform-v1\",\"limits\":[{limits}]}}]")!;
        // A soft-hard hybrid file that was decoded before: the final C1 file without its C1-only key
        // selectionObjectiveOrder, with policyId commit-soft-hard-hybrid and one warning profile per policy.
        var decodedBefore = FinalJson(json =>
        {
            json["policyId"] = "commit-soft-hard-hybrid";
            Assert.True(json.Remove("selectionObjectiveOrder"));
            json["warningProfiles"] = profiles.DeepClone();
        });
        Assert.NotNull(Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(FinalJson()), commitment));

        // The archived soft-hard hybrid file (decoded before with its own commitment file) and that file without its
        // warningProfiles block.
        foreach (var json in new[] { archived, Edit(archived, json => Assert.True(json.Remove("warningProfiles"))) })
        {
            Assert.Equal(
                refusal,
                Assert.Throws<InvalidDataException>(
                    () => Wp4RunnerConfiguration.Decode(
                        Encoding.UTF8.GetBytes(json),
                        archivedCommitment)).Message);
        }

        // The file decoded before, the final C1 file with policyId commit-soft-hard-hybrid, and a warningProfiles
        // block (empty or not) added to the final C1 file.
        foreach (var json in new[]
                 {
                     decodedBefore,
                     FinalJson(json => json["policyId"] = "commit-soft-hard-hybrid"),
                     FinalJson(json => json["warningProfiles"] = new JsonArray()),
                     FinalJson(json => json["warningProfiles"] = profiles.DeepClone()),
                 })
        {
            Assert.Equal(
                refusal,
                Assert.Throws<InvalidDataException>(
                    () => Wp4RunnerConfiguration.Decode(
                        Encoding.UTF8.GetBytes(json),
                        commitment)).Message);
        }
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

        Assert.Equal(
            SolverBackedSelectionObjectiveOrder.CostFirst,
            costFirst.SolverPolicyOptions!.SelectionObjectiveOrder);

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

    [Fact]
    public void Waiting_rider_relocation_needs_both_keys_and_cost_first_C1()
    {
        // The G2 arms: no vehicle lock at final confirmation, a pickup ratchet, no zero switch limit.
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "benchmarks",
            "configurations",
            "wp14-c1-h6-reference-v1.json")))!;
        var policy = json["policies"]![0]!.AsObject();
        policy["limits"]!.AsArray()
            .Single(value => (string?)value!["dimension"] == "vehicle_switch_count")!
            .AsObject()
            .Remove("hardLimit");
        policy["finalConfirmationLocks"] = new JsonArray("pickupStop", "pickupEta");
        policy["ratchetLocks"] = new JsonArray("pickupEta");
        var plain = CommitmentPolicyConfiguration.Decode(Encoding.UTF8.GetBytes(json.ToJsonString()));
        policy["waitingRiderRelocation"] = true;
        var relocating = CommitmentPolicyConfiguration.Decode(Encoding.UTF8.GetBytes(json.ToJsonString()));
        var costFirst = Tier3Wp4V2Json.Replace(
            "\"lateBoarding\": \"record-v1\"",
            "\"lateBoarding\": \"record-v1\",\n  \"selectionObjectiveOrder\": \"cost-first\"",
            StringComparison.Ordinal);
        var withG2 = costFirst.Replace(
            "\"retentionStrategy\": \"service-set-stability-portfolio-v1\"",
            "\"retentionStrategy\": \"service-set-stability-portfolio-v1\",\n"
                + "    \"waitingRiderRelocation\": { \"maximumTargetVehicles\": 8 }",
            StringComparison.Ordinal);
        Assert.NotEqual(costFirst, withG2);

        var g2 = Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(withG2), relocating);
        var off = Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(costFirst), plain);

        Assert.NotNull(g2.CandidateGeneration.WaitingRiderRelocation);
        Assert.Null(g2.CandidateGeneration.WaitingRiderRelocation!.MaximumRidersPerVehicle);
        Assert.Equal(8, g2.CandidateGeneration.WaitingRiderRelocation.MaximumTargetVehicles);
        Assert.Null(off.CandidateGeneration.WaitingRiderRelocation);
        Assert.NotEqual(off.ContentHash, g2.ContentHash);

        foreach (var (wp4, commitment, message) in new[]
                 {
                     (withG2, plain, "requires 'waitingRiderRelocation' on every commitment policy"),
                     (costFirst, relocating, "requires 'candidateGeneration.waitingRiderRelocation'"),
                     (withG2.Replace("\"cost-first\"", "\"stability-first\"", StringComparison.Ordinal),
                         relocating,
                         "requires the solver-backed C1 policy with selectionObjectiveOrder 'cost-first'"),
                     (withG2.Replace("{ \"maximumTargetVehicles\": 8 }", "{ \"maximumTargetVehicles\": 0 }", StringComparison.Ordinal),
                         relocating,
                         "maximumTargetVehicles"),
                     (withG2.Replace("{ \"maximumTargetVehicles\": 8 }", "{ \"targets\": 8 }", StringComparison.Ordinal),
                         relocating,
                         "targets"),
                 })
        {
            Assert.Contains(
                message,
                Assert.Throws<InvalidDataException>(
                    () => Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(wp4), commitment)).Message);
        }
    }

    [Fact]
    public void Stop_service_duration_is_optional_bounded_and_off_by_default()
    {
        // Bridge step 1.3: the dwell of the stops the generator inserts. Absent means 0, so the
        // archived tier-3 configuration keeps its content hash.
        var reference = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "benchmarks",
            "configurations",
            "wp14-c1-h6-reference-v1.json"));
        var commitment = CommitmentPolicyConfiguration.Decode(Encoding.UTF8.GetBytes(reference));
        string WithDwell(string value) => Tier3Wp4V2Json.Replace(
            "\"retentionStrategy\": \"service-set-stability-portfolio-v1\"",
            "\"retentionStrategy\": \"service-set-stability-portfolio-v1\",\n"
                + "    \"stopServiceDurationMs\": " + value,
            StringComparison.Ordinal);

        var off = Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(Tier3Wp4V2Json), commitment);
        var dwell = Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(WithDwell("24000")), commitment);
        var zero = Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(WithDwell("0")), commitment);

        Assert.Equal(0, off.CandidateGeneration.StopServiceDuration.Milliseconds);
        Assert.Equal(
            "fb2813c4036d309490e12df77e37de1683b92b92a9ebdd71e16cad35d50ecd42",
            off.ContentHash.Value);
        Assert.Equal(24_000, dwell.CandidateGeneration.StopServiceDuration.Milliseconds);
        Assert.Equal(0, zero.CandidateGeneration.StopServiceDuration.Milliseconds);
        Assert.NotEqual(off.ContentHash, dwell.ContentHash);

        foreach (var value in new[] { "-1", "3600001", "\"24000\"", "true" })
        {
            Assert.Contains(
                "stopServiceDurationMs",
                Assert.Throws<InvalidDataException>(
                    () => Wp4RunnerConfiguration.Decode(
                        Encoding.UTF8.GetBytes(WithDwell(value)),
                        commitment)).Message);
        }

        // A fraction never reaches the field reader: canonical JSON refuses it first, at the same path.
        Assert.Contains(
            "$.candidateGeneration.stopServiceDurationMs",
            Assert.Throws<RideBound.Contracts.Serialization.CanonicalJsonException>(
                () => Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(WithDwell("1.5")),
                    commitment)).Message);
    }

    [Fact]
    public void A_planning_forecast_is_optional_closed_and_parsed_to_the_exact_doubles()
    {
        // ADR-089 GĐ4: omitted keeps the archived content hash; factors are decimal strings
        // because canonical JSON holds integers only.
        var commitment = CommitmentConfiguration();
        string WithForecast(string value) => FinalJson(json => json["planningForecast"] = JsonNode.Parse(value));

        var off = Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(FinalJson()), commitment);
        var on = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(WithForecast(
                "{\"profile\": [\"1\", \"1.0638210000000001\", \"0.98\"], \"binMs\": 900000}")),
            commitment);

        Assert.Null(off.PlanningForecast);
        Assert.NotEqual(off.ContentHash, on.ContentHash);
        Assert.Equal(900_000, on.PlanningForecast!.BinMilliseconds);
        Assert.Equal([1.0, 1.0638210000000001, 0.98], on.PlanningForecast.Profile);

        foreach (var value in new[]
                 {
                     "{\"profile\": [], \"binMs\": 900000}",
                     "{\"profile\": [\"0\"], \"binMs\": 900000}",
                     "{\"profile\": [\"x\"], \"binMs\": 900000}",
                     "{\"profile\": [1], \"binMs\": 900000}",
                     "{\"profile\": [\"1\"], \"binMs\": 0}",
                     "{\"profile\": [\"1\"], \"binMs\": 1}",
                     "{\"profile\": [\"1\"], \"binMs\": 59999}",
                     "{\"profile\": [\"1\"]}",
                     "{\"profile\": [\"1\"], \"binMs\": 900000, \"clipPickup\": true}",
                 })
        {
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(WithForecast(value)), commitment));
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

    /// <summary>The final C1 configuration (<see cref="Final.FinalRuns.FinalC1Wp4WithoutShadow"/>) as JSON text,
    /// after <paramref name="edit"/>.</summary>
    private static string FinalJson(Action<JsonObject>? edit = null)
    {
        var json = Final.FinalRuns.FinalC1Wp4WithoutShadow();
        edit?.Invoke(json);
        return json.ToJsonString();
    }

    /// <summary>JSON text after <paramref name="edit"/>.</summary>
    private static string Edit(string json, Action<JsonObject> edit)
    {
        var node = JsonNode.Parse(json)!.AsObject();
        edit(node);
        return node.ToJsonString();
    }

    /// <summary>
    /// A no-reassignment-repair file (B4) that the Runner decoded before B4 was removed: the final C1 configuration
    /// with policyId no-reassignment-repair, without its C1-only keys selectionObjectiveOrder and commitmentRecovery,
    /// and with the repair block that B4 required, after <paramref name="edit"/>.
    /// </summary>
    private static string RepairJson(Action<JsonObject>? edit = null) =>
        FinalJson(json =>
        {
            json["policyId"] = "no-reassignment-repair";
            Assert.True(json.Remove("selectionObjectiveOrder"));
            Assert.True(json.Remove("commitmentRecovery"));
            json["repair"] = new JsonObject { ["maximumRequestsConsideredPerVehicle"] = 1 };
            edit?.Invoke(json);
        });

    private static string RepositoryRoot() =>
        typeof(Wp4RunnerConfigurationTests)
            .Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(value => value.Key == "RideBoundRepositoryRoot")
            .Value!;
}
