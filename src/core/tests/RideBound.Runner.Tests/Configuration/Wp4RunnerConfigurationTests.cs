using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
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
        // The file names the portfolio retention, the only one the decoder accepts.
        var commitment = CommitmentConfiguration();
        var configuration = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(ArchivedCostFirst(fileName)),
            commitment);

        Assert.Equal(policyId, configuration.PolicyId);
        Assert.Equal("wp7-fleetpy-v1", configuration.PolicyVersion);
        Assert.Equal(
            InitialPromiseTrigger.BookingConfirmation,
            configuration.InitialPromiseTrigger);
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
            InitialPromiseTrigger.BookingConfirmation,
            first.InitialPromiseTrigger);
        Assert.Equal(first.ContentHash, second.ContentHash);
        Assert.NotEqual(first.ContentHash, binding);
        Assert.NotEqual(commitment.ContentHash, binding);
        Assert.Equal(64, binding.Value.Length);
    }

    [Fact]
    public void An_absent_initial_promise_trigger_is_initial_acceptance_and_unknown_values_are_refused()
    {
        // The final configuration without initialPromiseTrigger, whose absence selects initial-acceptance.
        var commitment = CommitmentConfiguration();
        var portfolioJson = FinalJson(json => Assert.True(json.Remove("initialPromiseTrigger")));
        var portfolio = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(portfolioJson),
            commitment);

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

    [Fact]
    public void The_removed_origin_hold_schedule_is_refused_with_a_pointer_to_the_archive()
    {
        const string refusal =
            "WP4 scheduleStrategy 'origin-hold-relocated-wait' (the origin-hold schedule): "
            + "removed; see commit ebca912 (branch research/final-real-runs-2026-11-06)";
        var commitment = CommitmentConfiguration();
        static void OriginHold(JsonObject json) =>
            json["candidateGeneration"]!["scheduleStrategy"] = "origin-hold-relocated-wait";
        static string Refused(string json, CommitmentPolicyConfiguration configuration) =>
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(json), configuration)).Message;
        Assert.NotNull(Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(FinalJson()), commitment));

        // The final C1 file with the origin-hold schedule (decoded before), and with a G2 block added (refused before,
        // because G2 needed the earliest-feasible schedule).
        Assert.Equal(refusal, Refused(FinalJson(OriginHold), commitment));
        Assert.Equal(
            refusal,
            Refused(
                FinalJson(json =>
                {
                    OriginHold(json);
                    json["candidateGeneration"]!["waitingRiderRelocation"] =
                        new JsonObject { ["maximumTargetVehicles"] = 8 };
                }),
                commitment));

        // The final INS file with its Shadow block decodes. With the origin-hold schedule it is refused (before,
        // because Shadow needed the earliest-feasible schedule).
        var shadowFile = JsonNode.Parse(File.ReadAllText(
            Path.Combine(Final.FinalRuns.FixtureDirectory, "wp4", "INS-CL-light-v1.json")))!.AsObject();
        shadowFile["shadowAllocation"]!["networkDirectory"] =
            Path.Combine(Final.FinalRuns.FixtureDirectory, "network");
        var shadowCommitment = CommitmentPolicyConfiguration.Decode(
            File.ReadAllBytes(Path.Combine(Final.FinalRuns.FixtureDirectory, "commitment", "INS-SHRC.json")));
        Assert.NotNull(
            Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(shadowFile.ToJsonString()), shadowCommitment));
        OriginHold(shadowFile);
        Assert.Equal(refusal, Refused(shadowFile.ToJsonString(), shadowCommitment));

        // An unknown value and a missing key keep their own errors.
        Assert.Equal(
            "Unknown candidate scheduleStrategy.",
            Refused(FinalJson(json => json["candidateGeneration"]!["scheduleStrategy"] = "origin-hold"), commitment));
        Assert.Equal(
            "Missing field 'scheduleStrategy' at '$.candidateGeneration'.",
            Refused(
                FinalJson(json => Assert.True(json["candidateGeneration"]!.AsObject().Remove("scheduleStrategy"))),
                commitment));
    }

    [Fact]
    public void The_removed_legacy_retention_is_refused_with_a_pointer_to_the_archive()
    {
        const string refusal =
            "WP4 retentionStrategy must be 'service-set-stability-portfolio-v1'. The legacy retention, which "
            + "'legacy-accepted-count-cost-slack' or an absent key selected: removed; see commit ebca912 (branch "
            + "research/final-real-runs-2026-11-06)";
        const string evidenceRefusal =
            "WP4 emitSolverExecutionEvidence true and the 'solverExecutionEvidenceProfile' key (solver execution "
            + "evidence): removed; see commit ebca912 (branch research/final-real-runs-2026-11-06)";
        var commitment = CommitmentConfiguration();
        static void Legacy(JsonObject json) =>
            json["candidateGeneration"]!["retentionStrategy"] = "legacy-accepted-count-cost-slack";
        static void Absent(JsonObject json) =>
            Assert.True(json["candidateGeneration"]!.AsObject().Remove("retentionStrategy"));
        string Refused(string json) =>
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(json), commitment)).Message;
        Assert.NotNull(Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(FinalJson()), commitment));

        // Files that were decoded before with the legacy retention: the final C1 file with the legacy value or without
        // the key, and the archived boundary C1 file (no key) made cost-first.
        Assert.Equal(refusal, Refused(FinalJson(Legacy)));
        Assert.Equal(refusal, Refused(FinalJson(Absent)));
        Assert.Equal(refusal, Refused(ArchivedCostFirst("wp4-ridebound-hard-vector-boundary-v1.json")));

        // A check that runs later (here the G2 pairing) no longer gets to a legacy file.
        Assert.Equal(
            refusal,
            Refused(FinalJson(json =>
            {
                Legacy(json);
                json["candidateGeneration"]!["waitingRiderRelocation"] =
                    new JsonObject { ["maximumTargetVehicles"] = 8 };
            })));

        // A file that the order or the evidence check refuses keeps its message: the archived boundary file as it is,
        // and the final file without both keys, or with the legacy value and evidence on.
        Assert.Equal(
            StabilityFirstRefusal,
            Refused(File.ReadAllText(Path.Combine(
                RepositoryRoot(),
                "benchmarks",
                "configurations",
                "wp4-ridebound-hard-vector-boundary-v1.json"))));
        Assert.Equal(
            StabilityFirstRefusal,
            Refused(FinalJson(json =>
            {
                Absent(json);
                Assert.True(json.Remove("selectionObjectiveOrder"));
            })));
        Assert.Equal(
            evidenceRefusal,
            Refused(FinalJson(json =>
            {
                Legacy(json);
                json["emitSolverExecutionEvidence"] = true;
            })));

        // An unknown value and a value that is not a string keep their own errors.
        Assert.Equal(
            "Unknown candidate retentionStrategy.",
            Refused(FinalJson(json => json["candidateGeneration"]!["retentionStrategy"] = "legacy")));
        Assert.Equal(
            "'retentionStrategy' must be a non-empty string.",
            Refused(FinalJson(json => json["candidateGeneration"]!["retentionStrategy"] = 1)));
    }

    [Fact]
    public void The_removed_exact_small_mode_is_refused_with_a_pointer_to_the_archive()
    {
        const string refusal =
            "WP4 exactSmallMode true (the exact-small mode): "
            + "removed; see commit ebca912 (branch research/final-real-runs-2026-11-06)";
        var commitment = CommitmentConfiguration();
        static void ExactSmall(JsonObject json) =>
            json["candidateGeneration"]!["exactSmallMode"] = true;
        string Refused(string json) =>
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(json), commitment)).Message;
        Assert.NotNull(Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(FinalJson()), commitment));

        // The final C1 file with exactSmallMode true (decoded before).
        Assert.Equal(refusal, Refused(FinalJson(ExactSmall)));

        // Checks that run later no longer get to such a file: the work limit, and the evidence check in Decode.
        Assert.Equal(
            refusal,
            Refused(FinalJson(json =>
            {
                ExactSmall(json);
                json["candidateGeneration"]!["maximumExplorationWorkUnits"] = 0;
            })));
        Assert.Equal(
            refusal,
            Refused(FinalJson(json =>
            {
                ExactSmall(json);
                json["emitSolverExecutionEvidence"] = true;
            })));

        // Checks that run before it keep their messages: the candidate limit, a value that is not a boolean, and a
        // missing key.
        Assert.Equal(
            "'maximumCandidatesPerVehicle' must be positive.",
            Refused(FinalJson(json =>
            {
                ExactSmall(json);
                json["candidateGeneration"]!["maximumCandidatesPerVehicle"] = 0;
            })));
        Assert.Equal(
            "'exactSmallMode' must be a boolean.",
            Refused(FinalJson(json => json["candidateGeneration"]!["exactSmallMode"] = "false")));
        Assert.Equal(
            "Missing field 'exactSmallMode' at '$.candidateGeneration'.",
            Refused(FinalJson(json => Assert.True(json["candidateGeneration"]!.AsObject().Remove("exactSmallMode")))));
    }

    [Fact]
    public void The_removed_stop_service_duration_is_refused_with_a_pointer_to_the_archive()
    {
        const string refusal =
            "WP4 'candidateGeneration.stopServiceDurationMs' key (the dwell of inserted stops): "
            + "removed; see commit ebca912 (branch research/final-real-runs-2026-11-06)";
        var commitment = CommitmentConfiguration();
        static void Dwell(JsonObject json) =>
            json["candidateGeneration"]!["stopServiceDurationMs"] = 24_000;
        string Refused(string json) =>
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(json), commitment)).Message;
        Assert.NotNull(Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(FinalJson()), commitment));

        // The final C1 file with the key, whatever its value: 24000 and 0 were decoded before, the others were refused
        // by the old reader with their own messages.
        foreach (var value in new JsonNode[] { 24_000, 0, -1, 3_600_001, "24000", true })
        {
            Assert.Equal(
                refusal,
                Refused(FinalJson(json => json["candidateGeneration"]!["stopServiceDurationMs"] = value)));
        }

        // Checks that run later no longer get to such a file: the candidate limit, the exact-small refusal, and the
        // evidence check in Decode.
        Assert.Equal(
            refusal,
            Refused(FinalJson(json =>
            {
                Dwell(json);
                json["candidateGeneration"]!["maximumCandidatesPerVehicle"] = 0;
            })));
        Assert.Equal(
            refusal,
            Refused(FinalJson(json =>
            {
                Dwell(json);
                json["candidateGeneration"]!["exactSmallMode"] = true;
            })));
        Assert.Equal(
            refusal,
            Refused(FinalJson(json =>
            {
                Dwell(json);
                json["emitSolverExecutionEvidence"] = true;
            })));

        // Checks that run before it keep their messages: the origin-hold refusal and a G2 target cap of 0.
        Assert.Equal(
            "WP4 scheduleStrategy 'origin-hold-relocated-wait' (the origin-hold schedule): "
            + "removed; see commit ebca912 (branch research/final-real-runs-2026-11-06)",
            Refused(FinalJson(json =>
            {
                Dwell(json);
                json["candidateGeneration"]!["scheduleStrategy"] = "origin-hold-relocated-wait";
            })));
        Assert.Equal(
            "'maximumTargetVehicles' must be positive.",
            Refused(FinalJson(json =>
            {
                Dwell(json);
                json["candidateGeneration"]!["waitingRiderRelocation"] =
                    new JsonObject { ["maximumTargetVehicles"] = 0 };
            })));

        // A fraction never reaches the field reader: canonical JSON refuses it first, at the same path.
        Assert.Contains(
            "$.candidateGeneration.stopServiceDurationMs",
            Assert.Throws<RideBound.Contracts.Serialization.CanonicalJsonException>(
                () => Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(FinalJson(json =>
                        json["candidateGeneration"]!["stopServiceDurationMs"] = 1.5)),
                    commitment)).Message);

        // A file without the key decodes as before and keeps its content hash (the tier-3 cost-first text).
        var reference = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "benchmarks",
            "configurations",
            "wp14-c1-h6-reference-v1.json"));
        Assert.Equal(
            "e1a41d4502036da6c911e9375b65be646253f8f5d140929faaf12e6c3acc68f2",
            Wp4RunnerConfiguration.Decode(
                Encoding.UTF8.GetBytes(Tier3CostFirstJson),
                CommitmentPolicyConfiguration.Decode(Encoding.UTF8.GetBytes(reference))).ContentHash.Value);
    }

    [Fact]
    public void The_removed_relocation_rider_cap_is_refused_with_a_pointer_to_the_archive()
    {
        const string refusal =
            "WP4 'candidateGeneration.waitingRiderRelocation.maximumRidersPerVehicle' key (the G2 rider cap): "
            + "removed; see commit ebca912 (branch research/final-real-runs-2026-11-06)";
        var commitment = CommitmentConfiguration();
        // The final file with a G2 block (k = 8, as the final G2 files) that also has the rider cap key.
        static void G2(JsonObject json, JsonNode? riders) =>
            json["candidateGeneration"]!["waitingRiderRelocation"] = new JsonObject
            {
                ["maximumTargetVehicles"] = 8,
                ["maximumRidersPerVehicle"] = riders,
            };
        string Refused(string json) =>
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(json), commitment)).Message;

        // Any value: 1 to Int32.MaxValue were read before, the others were refused by the old reader with its own
        // messages.
        foreach (var value in new JsonNode[] { 1, 2, int.MaxValue, 0, -1, 2_147_483_648L, "2", true })
        {
            Assert.Equal(refusal, Refused(FinalJson(json => G2(json, value))));
        }

        // Checks that run later no longer get to such a file: the target cap, the dwell refusal, the exact-small
        // refusal, and the evidence check in Decode.
        Assert.Equal(
            refusal,
            Refused(FinalJson(json =>
            {
                G2(json, 2);
                json["candidateGeneration"]!["waitingRiderRelocation"]!["maximumTargetVehicles"] = 0;
            })));
        Assert.Equal(
            refusal,
            Refused(FinalJson(json =>
            {
                G2(json, 2);
                json["candidateGeneration"]!["stopServiceDurationMs"] = 24_000;
            })));
        Assert.Equal(
            refusal,
            Refused(FinalJson(json =>
            {
                G2(json, 2);
                json["candidateGeneration"]!["exactSmallMode"] = true;
            })));
        Assert.Equal(
            refusal,
            Refused(FinalJson(json =>
            {
                G2(json, 2);
                json["emitSolverExecutionEvidence"] = true;
            })));

        // Checks that run before it keep their messages: the origin-hold refusal and an unknown key in the G2 block.
        Assert.Equal(
            "WP4 scheduleStrategy 'origin-hold-relocated-wait' (the origin-hold schedule): "
            + "removed; see commit ebca912 (branch research/final-real-runs-2026-11-06)",
            Refused(FinalJson(json =>
            {
                G2(json, 2);
                json["candidateGeneration"]!["scheduleStrategy"] = "origin-hold-relocated-wait";
            })));
        Assert.Equal(
            "Unknown field 'targets' at '$.candidateGeneration.waitingRiderRelocation'.",
            Refused(FinalJson(json =>
            {
                G2(json, 2);
                json["candidateGeneration"]!["waitingRiderRelocation"]!["targets"] = 8;
            })));

        // A fraction never reaches the field reader: canonical JSON refuses it first, at the same path.
        Assert.Contains(
            "$.candidateGeneration.waitingRiderRelocation.maximumRidersPerVehicle",
            Assert.Throws<RideBound.Contracts.Serialization.CanonicalJsonException>(
                () => Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(FinalJson(json => G2(json, 1.5))),
                    commitment)).Message);
    }

    [Fact]
    public void The_removed_constant_level_skip_is_refused_with_a_pointer_to_the_archive()
    {
        const string refusal =
            "WP4 'solverExecution.skipConstantObjectiveLevels' key (the constant-level skip): "
            + "removed; see commit ebca912 (branch research/final-real-runs-2026-11-06)";
        var commitment = CommitmentConfiguration();
        static void Skip(JsonObject json) =>
            json["solverExecution"]!["skipConstantObjectiveLevels"] = true;
        string Refused(string json) =>
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(json), commitment)).Message;

        // The final C1 file with the key, whatever its value: true and false were decoded before, the others were
        // refused by the old reader with its own message.
        foreach (var value in new JsonNode[] { true, false, "yes", 1 })
        {
            Assert.Equal(
                refusal,
                Refused(FinalJson(json => json["solverExecution"]!["skipConstantObjectiveLevels"] = value)));
        }

        // Checks that run later no longer get to such a file: the generation and validation work limits.
        Assert.Equal(
            refusal,
            Refused(FinalJson(json =>
            {
                Skip(json);
                json["solverExecution"]!["maximumGenerationWorkUnits"] = 0;
            })));
        Assert.Equal(
            refusal,
            Refused(FinalJson(json =>
            {
                Skip(json);
                json["solverExecution"]!["maximumValidationWorkUnits"] = 0;
            })));

        // Checks that run before it keep their messages: the solver work limit, the seed, an unknown solver key, and
        // the evidence check in Decode.
        Assert.Equal(
            "'maximumSolverWorkUnits' must be positive.",
            Refused(FinalJson(json =>
            {
                Skip(json);
                json["solverExecution"]!["maximumSolverWorkUnits"] = 0;
            })));
        Assert.Equal(
            "'randomSeed' exceeds Int32.",
            Refused(FinalJson(json =>
            {
                Skip(json);
                json["solverExecution"]!["randomSeed"] = 2_147_483_648L;
            })));
        Assert.Equal(
            "Unknown field 'seed' at '$.solverExecution'.",
            Refused(FinalJson(json =>
            {
                Skip(json);
                json["solverExecution"]!["seed"] = 7;
            })));
        Assert.Equal(
            "WP4 emitSolverExecutionEvidence true and the 'solverExecutionEvidenceProfile' key "
            + "(solver execution evidence): removed; see commit ebca912 (branch research/final-real-runs-2026-11-06)",
            Refused(FinalJson(json =>
            {
                Skip(json);
                json["emitSolverExecutionEvidence"] = true;
            })));

        // A fraction never reaches the field reader: canonical JSON refuses it first, at the same path.
        Assert.Contains(
            "$.solverExecution.skipConstantObjectiveLevels",
            Assert.Throws<RideBound.Contracts.Serialization.CanonicalJsonException>(
                () => Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(FinalJson(json =>
                        json["solverExecution"]!["skipConstantObjectiveLevels"] = 1.5)),
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
