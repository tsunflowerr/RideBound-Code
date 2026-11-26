using System.Text;
using System.Text.Json.Nodes;
using RideBound.Algorithms.Policies;
using RideBound.Domain.Commitments;
using RideBound.Runner.Configuration;
using RideBound.Solvers.OrTools;
using RideBound.Tests;

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
        Assert.Equal(first.ContentHash, second.ContentHash);
        Assert.NotEqual(first.ContentHash, binding);
        Assert.NotEqual(commitment.ContentHash, binding);
        Assert.Equal(64, binding.Value.Length);
    }

    [Fact]
    public void The_solver_execution_block_is_required()
    {
        var missingSolver = FinalJson(json => Assert.True(json.Remove("solverExecution")));

        Assert.Equal(
            "Missing field 'solverExecution' at '$'.",
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(missingSolver),
                    CommitmentConfiguration())).Message);
    }

    [Fact]
    public void Keys_with_one_supported_value_refuse_any_other_value_and_an_absent_key()
    {
        // The final files set each of these keys to the one value the Runner supports.
        var cases = new (string Message, Action<JsonObject> Edit)[]
        {
            ("'$.initialPromiseTrigger' must be 'booking-confirmation'.", json => json["initialPromiseTrigger"] = "initial-acceptance"),
            ("'$.initialPromiseTrigger' must be 'booking-confirmation'.", json => json.Remove("initialPromiseTrigger")),
            ("'$.commitmentRecovery' must be 'no-worse-than-reference-v1'.", json => json["commitmentRecovery"] = "fail-closed"),
            ("'$.commitmentRecovery' must be 'no-worse-than-reference-v1'.", json => json.Remove("commitmentRecovery")),
            ("'$.lateBoarding' must be 'record-v1'.", json => json["lateBoarding"] = "reject"),
            ("'$.lateBoarding' must be 'record-v1'.", json => json.Remove("lateBoarding")),
            ("'$.selectionObjectiveOrder' must be 'cost-first'.", json => json["selectionObjectiveOrder"] = "stability-first"),
            ("'$.selectionObjectiveOrder' must be 'cost-first'.", json => json.Remove("selectionObjectiveOrder")),
            ("'$.emitSolverExecutionEvidence' must be false.", json => json["emitSolverExecutionEvidence"] = true),
            ("'$.candidateGeneration.scheduleStrategy' must be 'earliest-feasible'.",
                json => json["candidateGeneration"]!["scheduleStrategy"] = "origin-hold-relocated-wait"),
            ("'$.candidateGeneration.retentionStrategy' must be 'service-set-stability-portfolio-v1'.",
                json => json["candidateGeneration"]!["retentionStrategy"] = "legacy-accepted-count-cost-slack"),
            ("'$.candidateGeneration.retentionStrategy' must be 'service-set-stability-portfolio-v1'.",
                json => json["candidateGeneration"]!.AsObject().Remove("retentionStrategy")),
            ("'$.candidateGeneration.exactSmallMode' must be false.",
                json => json["candidateGeneration"]!["exactSmallMode"] = true),
            ("Unknown field 'stopServiceDurationMs' at '$.candidateGeneration'.",
                json => json["candidateGeneration"]!["stopServiceDurationMs"] = 0),
            ("Unknown field 'skipConstantObjectiveLevels' at '$.solverExecution'.",
                json => json["solverExecution"]!["skipConstantObjectiveLevels"] = false),
            ("Unknown field 'multiplePlan' at '$'.", json => json["multiplePlan"] = new JsonObject()),
            ("Unknown WP4 policyId 'rolling-cost'.", json => json["policyId"] = "rolling-cost"),
        };

        Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(FinalJson()), CommitmentConfiguration());

        foreach (var (message, edit) in cases)
        {
            Assert.Equal(
                message,
                Assert.Throws<InvalidDataException>(
                    () => Wp4RunnerConfiguration.Decode(
                        Encoding.UTF8.GetBytes(FinalJson(edit)),
                        CommitmentConfiguration())).Message);
        }
    }

    /// <summary>The refusal of every selectionObjectiveOrder other than cost-first, and of an absent key.</summary>
    private const string StabilityFirstRefusal = "'$.selectionObjectiveOrder' must be 'cost-first'.";

    /// <summary>
    /// Text of the Tier-3 WP4 configuration file `wp4-tier3-v2.json`
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
            TestPaths.Root(),
            "configs",
            "test",
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
            TestPaths.Root(),
            "configs",
            "test",
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
        // Omitted keeps the archived content hash; factors are decimal strings
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
    /// An archived configuration file with `"selectionObjectiveOrder": "cost-first"`, `"lateBoarding": "record-v1"` and
    /// `"commitmentRecovery": "no-worse-than-reference-v1"` added, the only values the Runner accepts (the archived files
    /// have none of these keys; an absent order key selected stability-first, an absent lateBoarding key the
    /// late-boarding rejection, and an absent commitmentRecovery key the fail-closed recovery mode).
    /// </summary>
    private static string ArchivedCostFirst(string fileName) =>
        Edit(
            File.ReadAllText(Path.Combine(TestPaths.Root(), "configs", "test", fileName)),
            json =>
            {
                json["selectionObjectiveOrder"] = "cost-first";
                json["lateBoarding"] = "record-v1";
                json["commitmentRecovery"] = "no-worse-than-reference-v1";
            });

    private static CommitmentPolicyConfiguration CommitmentConfiguration() =>
        CommitmentPolicyConfiguration.Decode(
            File.ReadAllBytes(Path.Combine(
                TestPaths.Root(),
                "configs",
                "test",
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
}
