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
            Encoding.UTF8.GetBytes(ArchivedCostFirst(fileName)),
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
        Assert.Equal(first.ContentHash, second.ContentHash);
        Assert.NotEqual(first.ContentHash, binding);
        Assert.NotEqual(commitment.ContentHash, binding);
        Assert.Equal(64, binding.Value.Length);
    }

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
        var c1 = ArchivedCostFirst("wp7-fleetpy-ridebound-hard-vector-v1.json");
        var policyOnly = JsonNode.Parse(b5)!.AsObject();
        policyOnly.Remove("multiplePlan");
        var blockOnC1 = JsonNode.Parse(c1)!.AsObject();
        blockOnC1["multiplePlan"] = JsonNode.Parse(b5)!["multiplePlan"]!.DeepClone();
        Assert.NotNull(Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(c1), commitment));

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
    public void The_removed_stability_first_ranking_is_refused_with_a_pointer_to_the_archive()
    {
        var commitment = CommitmentConfiguration();
        var archived = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "benchmarks",
            "configurations",
            "wp7-fleetpy-ridebound-hard-vector-v1.json"));
        Assert.NotNull(Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(FinalJson()), commitment));

        // Files that ran stability-first before (the archived C1 file and the final C1 file, each without the key or
        // with "stability-first"), and every other value of the key.
        foreach (var json in new[]
                 {
                     archived,
                     Edit(archived, json => json["selectionObjectiveOrder"] = "stability-first"),
                     FinalJson(json => Assert.True(json.Remove("selectionObjectiveOrder"))),
                     FinalJson(json => json["selectionObjectiveOrder"] = "stability-first"),
                     FinalJson(json => json["selectionObjectiveOrder"] = "cost-first-v2"),
                     FinalJson(json => json["selectionObjectiveOrder"] = ""),
                     FinalJson(json => json["selectionObjectiveOrder"] = true),
                     FinalJson(json => json["selectionObjectiveOrder"] = 1),
                 })
        {
            Assert.Equal(
                StabilityFirstRefusal,
                Assert.Throws<InvalidDataException>(
                    () => Wp4RunnerConfiguration.Decode(
                        Encoding.UTF8.GetBytes(json),
                        commitment)).Message);
        }

        // The archived tier-3 configuration with its own commitment file.
        var reference = CommitmentPolicyConfiguration.Decode(File.ReadAllBytes(Path.Combine(
            RepositoryRoot(),
            "benchmarks",
            "configurations",
            "wp14-c1-h6-reference-v1.json")));
        Assert.Equal(
            StabilityFirstRefusal,
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(Tier3Wp4V2Json),
                    reference)).Message);

        // A check that runs before the order check keeps its own message.
        Assert.Equal(
            "Unknown lateBoarding.",
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(FinalJson(json =>
                    {
                        Assert.True(json.Remove("selectionObjectiveOrder"));
                        json["lateBoarding"] = "late";
                    })),
                    commitment)).Message);
    }

    [Fact]
    public void The_removed_solver_execution_evidence_is_refused_with_a_pointer_to_the_archive()
    {
        const string refusal =
            "WP4 emitSolverExecutionEvidence true and the 'solverExecutionEvidenceProfile' key (solver execution "
            + "evidence): removed; see commit ebca912 (branch research/final-real-runs-2026-11-06)";
        var commitment = CommitmentConfiguration();

        // false, as in every final file, and an absent key, which meant false, still decode.
        Assert.NotNull(Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(FinalJson()), commitment));
        Assert.NotNull(
            Wp4RunnerConfiguration.Decode(
                Encoding.UTF8.GetBytes(FinalJson(json => Assert.True(json.Remove("emitSolverExecutionEvidence")))),
                commitment));

        // Files that were decoded before with evidence on: the archived audited and retained-portfolio C1 files, and
        // the final C1 file with evidence on, with each profile, or with a profile alone (any value).
        foreach (var json in new[]
                 {
                     ArchivedCostFirst("wp9-fleetpy-ridebound-hard-vector-audited-v1.json"),
                     ArchivedCostFirst("wp13-e1-fleetpy-ridebound-hard-vector-retained-v1.json"),
                     FinalJson(json => json["emitSolverExecutionEvidence"] = true),
                     FinalJson(json =>
                     {
                         json["emitSolverExecutionEvidence"] = true;
                         json["solverExecutionEvidenceProfile"] = "retained-portfolio-v1";
                     }),
                     FinalJson(json =>
                     {
                         json["emitSolverExecutionEvidence"] = true;
                         json["solverExecutionEvidenceProfile"] = "retained-portfolio-full-witness-v1";
                     }),
                     FinalJson(json => json["solverExecutionEvidenceProfile"] = "retained-portfolio-v1"),
                     FinalJson(json => json["solverExecutionEvidenceProfile"] = "unknown-portfolio"),
                     FinalJson(json => json["solverExecutionEvidenceProfile"] = 1),
                 })
        {
            Assert.Equal(
                refusal,
                Assert.Throws<InvalidDataException>(
                    () => Wp4RunnerConfiguration.Decode(
                        Encoding.UTF8.GetBytes(json),
                        commitment)).Message);
        }

        // A value that is not a boolean keeps its own error, and a file that the order check refuses keeps its message.
        Assert.Equal(
            "'emitSolverExecutionEvidence' must be a boolean.",
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(FinalJson(json => json["emitSolverExecutionEvidence"] = 1)),
                    commitment)).Message);
        Assert.Equal(
            StabilityFirstRefusal,
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(FinalJson(json =>
                    {
                        Assert.True(json.Remove("selectionObjectiveOrder"));
                        json["emitSolverExecutionEvidence"] = true;
                        json["solverExecutionEvidenceProfile"] = "retained-portfolio-v1";
                    })),
                    commitment)).Message);
    }

    /// <summary>The refusal of every selectionObjectiveOrder other than cost-first, and of an absent key.</summary>
    private const string StabilityFirstRefusal =
        "WP4 selectionObjectiveOrder must be 'cost-first'. The stability-first ranking, which 'stability-first' or an "
        + "absent key selected: removed; see commit ebca912 (branch research/final-real-runs-2026-11-06)";

    /// <summary>
    /// Text of the Tier-3 WP4 configuration `ridebound-scratchpad/tang3/configs/wp4-tier3-v2.json`
    /// (file SHA-256 66e9b59249d9491d520f3ed592e8c878a0d3b100e39432d5f4b73321617b4518), the one
    /// every archived T3.8 run used. The binding hashes its canonical form, so the whitespace of
    /// this copy does not matter. It has no `selectionObjectiveOrder` key, so the Runner refuses it;
    /// <see cref="Tier3CostFirstJson"/> adds cost-first and turns the solver execution evidence off.
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

    /// <summary>
    /// The tier-3 text with `"selectionObjectiveOrder": "cost-first"` and `"emitSolverExecutionEvidence": false`, the
    /// only values of these keys the Runner accepts.
    /// </summary>
    private static readonly string Tier3CostFirstJson = Tier3Wp4V2Json
        .Replace(
            "\"lateBoarding\": \"record-v1\"",
            "\"lateBoarding\": \"record-v1\",\n  \"selectionObjectiveOrder\": \"cost-first\"",
            StringComparison.Ordinal)
        .Replace(
            "\"emitSolverExecutionEvidence\": true",
            "\"emitSolverExecutionEvidence\": false",
            StringComparison.Ordinal);

    [Fact]
    public void A_per_rider_drop_budget_is_accepted_by_C1_only_under_cost_first()
    {
        // A Rule C per-rider drop budget runs under cost-first; any other order is the removed
        // stability-first ranking and is refused.
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

        var costFirst = Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(Tier3CostFirstJson), commitment);

        Assert.NotNull(costFirst.SolverPolicyOptions);

        foreach (var stabilityFirst in new[]
                 {
                     Tier3Wp4V2Json,
                     Tier3CostFirstJson.Replace("\"cost-first\"", "\"stability-first\"", StringComparison.Ordinal),
                 })
        {
            Assert.Equal(
                StabilityFirstRefusal,
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
        var costFirst = Tier3CostFirstJson;
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
                         StabilityFirstRefusal),
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
        // Bridge step 1.3: the dwell of the stops the generator inserts. Absent means 0, so a file
        // without the key keeps its content hash (pinned for the tier-3 text with cost-first and evidence off).
        var reference = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "benchmarks",
            "configurations",
            "wp14-c1-h6-reference-v1.json"));
        var commitment = CommitmentPolicyConfiguration.Decode(Encoding.UTF8.GetBytes(reference));
        string WithDwell(string value) => Tier3CostFirstJson.Replace(
            "\"retentionStrategy\": \"service-set-stability-portfolio-v1\"",
            "\"retentionStrategy\": \"service-set-stability-portfolio-v1\",\n"
                + "    \"stopServiceDurationMs\": " + value,
            StringComparison.Ordinal);

        var off = Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(Tier3CostFirstJson), commitment);
        var dwell = Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(WithDwell("24000")), commitment);
        var zero = Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(WithDwell("0")), commitment);

        Assert.Equal(0, off.CandidateGeneration.StopServiceDuration.Milliseconds);
        Assert.Equal(
            "e1a41d4502036da6c911e9375b65be646253f8f5d140929faaf12e6c3acc68f2",
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

    /// <summary>
    /// An archived configuration file with `"selectionObjectiveOrder": "cost-first"` added, the only order the Runner
    /// accepts (the archived files have no key, which used to select stability-first).
    /// </summary>
    private static string ArchivedCostFirst(string fileName) =>
        Edit(
            File.ReadAllText(Path.Combine(RepositoryRoot(), "benchmarks", "configurations", fileName)),
            json => json["selectionObjectiveOrder"] = "cost-first");

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
