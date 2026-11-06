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

public sealed class SolverBackedFleetSelectionTests
{
    [Fact]
    public void Policy_registry_round_trips_every_unique_published_name()
    {
        var ids = Enum.GetValues<RidePoolingPolicyKind>()
            .Select(RidePoolingPolicyRegistry.ToPolicyId)
            .ToArray();

        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(
            Enum.GetValues<RidePoolingPolicyKind>(),
            kind =>
            {
                Assert.True(
                    RidePoolingPolicyRegistry.TryParse(
                        RidePoolingPolicyRegistry.ToPolicyId(kind),
                        out var parsed));
                Assert.Equal(kind, parsed);
            });
        Assert.False(RidePoolingPolicyRegistry.TryParse("unknown", out _));
    }

    [Fact]
    public void Empty_vehicle_candidate_set_reports_infeasibility_not_id_collision()
    {
        var result = Select(
            [Set(AlgorithmTestData.VehicleOne)],
            SolverBackedObjectiveProfile.RollingCost);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            SolverBackedSelectionFailureCodes.ModelMappingFailed,
            result.Witness!.Code);
        Assert.Equal(
            "Every vehicle needs at least one feasible candidate.",
            result.Witness.Message);
    }

    [Fact]
    public void Duplicate_candidate_ids_report_the_global_identity_collision()
    {
        var result = Select(
            [
                Set(
                    AlgorithmTestData.VehicleOne,
                    Candidate("duplicate", AlgorithmTestData.VehicleOne, [], 0)),
                Set(
                    AlgorithmTestData.VehicleTwo,
                    Candidate("duplicate", AlgorithmTestData.VehicleTwo, [], 0)),
            ],
            SolverBackedObjectiveProfile.RollingCost);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            SolverBackedSelectionFailureCodes.ModelMappingFailed,
            result.Witness!.Code);
        Assert.Equal(
            "Candidate IDs must be globally unique across the fleet.",
            result.Witness.Message);
    }

    [Fact]
    public void Rolling_cost_mapping_enforces_request_uniqueness_and_cost_order()
    {
        var request = new RequestId("request-1");
        var first = AlgorithmTestData.VehicleOne;
        var second = AlgorithmTestData.VehicleTwo;
        var sets = new[]
        {
            Set(first, Candidate("v1-noop", first, [], 0), Candidate("v1-accept", first, [request], 10)),
            Set(second, Candidate("v2-noop", second, [], 0), Candidate("v2-accept", second, [request], 20)),
        };

        var result = Select(sets, SolverBackedObjectiveProfile.RollingCost);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.Equal(
            ["v1-accept", "v2-noop"],
            result.Selection!.Selection.VehiclePlans
                .Select(value => value.Candidate.CandidateId));
        Assert.Equal(1, result.Selection.Selection.AcceptedRequestCount);
        Assert.Equal(10, result.Selection.Selection.OperationalCost);
    }

    [Fact]
    public void Candidate_portfolio_snapshot_is_exact_immutable_and_fail_closed()
    {
        var request = new RequestId("request-1");
        var mutableRequests = new[] { request };
        var vehicle = AlgorithmTestData.VehicleOne;
        var noOp = Candidate("noop", vehicle, [], 0);
        var accept = Candidate("accept", vehicle, mutableRequests, 10);
        var mutableGenerated = new[] { noOp, accept };
        var generated = new[]
        {
            new VehicleCandidateSet(vehicle, mutableGenerated, [], false),
        };
        var eligible = new[]
        {
            new VehicleCandidateSet(vehicle, mutableGenerated, [], false),
        };
        var selection = Select(
            generated,
            SolverBackedObjectiveProfile.RollingCost);

        Assert.True(selection.IsSuccess, selection.Witness?.Message);
        var captured = CandidatePortfolioEvidenceSnapshot.Create(
            SolverBackedObjectiveProfile.RollingCost,
            generated,
            eligible,
            selection.Selection!.Problem,
            selection.Selection.Selection.VehiclePlans
                .Select(value => value.Candidate.CandidateId)
                .ToArray());

        Assert.True(captured.IsSuccess, captured.Failure?.Message);
        Assert.Equal(
            selection.Selection.Problem.Options.Select(value => value.OptionId),
            captured.Value!.PolicyEligibleCandidateSets
                .SelectMany(value => value.Candidates)
                .OrderBy(value => value.VehicleId.Value, StringComparer.Ordinal)
                .ThenBy(value => value.CandidateId, StringComparer.Ordinal)
                .Select(value => value.CandidateId));

        mutableGenerated[0] = Candidate("mutated", vehicle, [], 0);
        mutableRequests[0] = new RequestId("mutated-request");
        Assert.Equal(
            ["noop", "accept"],
            captured.Value.GeneratedPhysicalCandidateSets[0].Candidates
                .Select(value => value.CandidateId));
        Assert.Equal(
            request,
            captured.Value.GeneratedPhysicalCandidateSets[0].Candidates[1]
                .NewRequestIds[0]);

        var nonIdentitySubset = CandidatePortfolioEvidenceSnapshot.Create(
            SolverBackedObjectiveProfile.RollingCost,
            [Set(vehicle, noOp, accept)],
            [Set(vehicle, noOp, accept with { })],
            selection.Selection.Problem,
            ["accept"]);
        Assert.False(nonIdentitySubset.IsSuccess);
        Assert.Equal(
            RollingCostFailureCodes.CandidatePortfolioEvidenceInvalid,
            nonIdentitySubset.Failure?.Code);

        var freshAccept = Candidate("accept", vehicle, [request], 10);
        var missingSelection = CandidatePortfolioEvidenceSnapshot.Create(
            SolverBackedObjectiveProfile.RollingCost,
            [Set(vehicle, noOp, freshAccept)],
            [Set(vehicle, noOp, freshAccept)],
            selection.Selection.Problem,
            ["missing"]);
        Assert.False(missingSelection.IsSuccess);
        Assert.Equal(
            "selectedCandidateIds",
            missingSelection.Failure?.Dimension);

        var crossVehicle = CandidatePortfolioEvidenceSnapshot.Create(
            SolverBackedObjectiveProfile.RollingCost,
            [Set(vehicle, noOp, accept with
                {
                    VehicleId = AlgorithmTestData.VehicleTwo,
                })],
            [Set(vehicle, noOp)],
            selection.Selection.Problem,
            ["accept"]);
        Assert.False(crossVehicle.IsSuccess);
        Assert.Equal("vehicleId", crossVehicle.Failure?.Dimension);
    }

    [Fact]
    public void Revision_mapping_preserves_material_then_dimension_hierarchy()
    {
        var request = new RequestId("request-1");
        var vehicle = AlgorithmTestData.VehicleOne;
        var noOp = Candidate("noop", vehicle, [], 0);
        var lowerRevision = Candidate("lower-revision", vehicle, [request], 100);
        var cheaper = Candidate("cheaper", vehicle, [request], 1);
        var material = Candidate("material", vehicle, [request], 0);
        var assessments = new Dictionary<string, CandidateCommitmentAssessment>(
            StringComparer.Ordinal)
        {
            [noOp.CandidateId] = Revision(noOp, CommitmentVector.Zero),
            [lowerRevision.CandidateId] = Revision(lowerRevision, Vector(pickup: 1)),
            [cheaper.CandidateId] = Revision(cheaper, Vector(pickup: 2)),
            [material.CandidateId] = Revision(material, Vector(material: 1)),
        };

        var result = Select(
            [Set(vehicle, noOp, lowerRevision, cheaper, material)],
            SolverBackedObjectiveProfile.RevisionPenalty,
            revisionAssessments: assessments);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.Equal(
            "lower-revision",
            Assert.Single(result.Selection!.Selection.VehiclePlans)
                .Candidate.CandidateId);
        Assert.Equal(
            1,
            result.Selection.Selection.DecisionInducedRevision!.PickupEtaTotalMs);
    }

    [Fact]
    public void Hard_vector_mapping_uses_maximum_utilization_before_revision_and_cost()
    {
        var request = new RequestId("request-1");
        var vehicle = AlgorithmTestData.VehicleOne;
        var noOp = Candidate("noop", vehicle, [], 0);
        var highUtilization = Candidate("high-util", vehicle, [request], 0);
        var lowUtilization = Candidate("low-util", vehicle, [request], 100);
        var assessments = new Dictionary<string, HardVectorCandidateAssessment>(
            StringComparer.Ordinal)
        {
            [noOp.CandidateId] = Hard(noOp, 0, CommitmentVector.Zero),
            [highUtilization.CandidateId] = Hard(highUtilization, 900_000, CommitmentVector.Zero),
            [lowUtilization.CandidateId] = Hard(lowUtilization, 100_000, Vector(pickup: 999)),
        };

        var result = Select(
            [Set(vehicle, noOp, highUtilization, lowUtilization)],
            SolverBackedObjectiveProfile.HardVector,
            hardAssessments: assessments);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.Equal(
            "low-util",
            Assert.Single(result.Selection!.Selection.VehiclePlans)
                .Candidate.CandidateId);
        Assert.Equal(100_000, result.Selection.Selection.WorstHardUtilizationPartsPerMillion);
    }

    [Fact]
    public void Soft_hard_mapping_places_warning_vector_before_revision_and_cost()
    {
        var request = new RequestId("request-1");
        var vehicle = AlgorithmTestData.VehicleOne;
        var noOp = Candidate("noop", vehicle, [], 0);
        var lowerWarning = Candidate("lower-warning", vehicle, [request], 100);
        var lowerRevision = Candidate("lower-revision", vehicle, [request], 1);
        var assessments = new Dictionary<string, HardVectorCandidateAssessment>(
            StringComparer.Ordinal)
        {
            [noOp.CandidateId] = Hard(
                noOp,
                0,
                CommitmentVector.Zero,
                warning: CommitmentVector.Zero),
            [lowerWarning.CandidateId] = Hard(
                lowerWarning,
                100_000,
                Vector(pickup: 10),
                warning: Vector(pickup: 1)),
            [lowerRevision.CandidateId] = Hard(
                lowerRevision,
                100_000,
                Vector(pickup: 1),
                warning: Vector(pickup: 2)),
        };

        var result = Select(
            [Set(vehicle, noOp, lowerWarning, lowerRevision)],
            SolverBackedObjectiveProfile.SoftHardHybrid,
            hardAssessments: assessments);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.Equal(
            "lower-warning",
            Assert.Single(result.Selection!.Selection.VehiclePlans)
                .Candidate.CandidateId);
    }

    [Fact]
    public void Unbounded_hard_profile_is_semantically_rolling_cost()
    {
        var request = new RequestId("request-1");
        var vehicle = AlgorithmTestData.VehicleOne;
        var noOp = Candidate("noop", vehicle, [], 0);
        var cheapHighRevision = Candidate("cheap", vehicle, [request], 1);
        var expensiveLowRevision = Candidate("expensive", vehicle, [request], 100);
        var sets = new[] { Set(vehicle, noOp, cheapHighRevision, expensiveLowRevision) };
        var assessments = new Dictionary<string, HardVectorCandidateAssessment>(
            StringComparer.Ordinal)
        {
            [noOp.CandidateId] = Hard(noOp, 0, CommitmentVector.Zero, hasHard: false),
            [cheapHighRevision.CandidateId] = Hard(
                cheapHighRevision,
                1_000_000,
                Vector(pickup: 999),
                hasHard: false),
            [expensiveLowRevision.CandidateId] = Hard(
                expensiveLowRevision,
                0,
                CommitmentVector.Zero,
                hasHard: false),
        };

        var c1 = Select(
            sets,
            SolverBackedObjectiveProfile.HardVector,
            hardAssessments: assessments);
        var b1 = Select(sets, SolverBackedObjectiveProfile.RollingCost);

        Assert.Equal(
            Assert.Single(b1.Selection!.Selection.VehiclePlans).Candidate.CandidateId,
            Assert.Single(c1.Selection!.Selection.VehiclePlans).Candidate.CandidateId);
        Assert.Equal("cheap", Assert.Single(c1.Selection.Selection.VehiclePlans).Candidate.CandidateId);
        Assert.Null(c1.Selection.Selection.DecisionInducedRevision);
    }

    [Fact]
    public void Solver_incumbent_rejected_by_semantic_validator_uses_validated_no_op()
    {
        var request = new RequestId("request-1");
        var vehicle = AlgorithmTestData.VehicleOne;
        var sets = new[]
        {
            Set(
                vehicle,
                Candidate("noop", vehicle, [], 0),
                Candidate("accept", vehicle, [request], 1)),
        };
        var result = Select(
            sets,
            SolverBackedObjectiveProfile.RollingCost,
            validator: new FleetValidator(
                selection => selection.AcceptedRequestCount == 0));

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.Equal(
            CandidateSelectionSolveStatus.SafeFallback,
            result.Selection!.Execution.SolveResult.Status);
        Assert.Equal(
            "noop",
            Assert.Single(result.Selection.Selection.VehiclePlans).Candidate.CandidateId);
        Assert.True(result.Selection.Execution.Diagnostics.PrimaryIncumbentRejected);
    }

    [Fact]
    public void Bounded_generation_loss_reaches_execution_diagnostics_separately_from_solver_loss()
    {
        var firstRequest = AlgorithmTestData.PendingRequest("request-1");
        var secondRequest = AlgorithmTestData.PendingRequest(
            "request-2",
            AlgorithmTestData.NodeThree,
            AlgorithmTestData.NodeOne);
        var unadvanced = AlgorithmTestData.CreateState(
            [firstRequest, secondRequest],
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
        var policy = new CommitmentPolicy(
            "uniform-v1",
            CommitmentBudgetBasis.DecisionInduced,
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    null,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1, null));
        var solverBudget = DeterministicSolverBudget.Create(
            1000,
            1000,
            1).Value!;
        var executionBudget =
            DeterministicCandidateSelectionExecutionBudget.Create(
                100_000,
                100_000,
                solverBudget).Value!;
        var result = new SolverBackedRidePoolingPolicy(
            new UnknownTestSolver()).Decide(
                new CommitmentMechanismContext(
                    before,
                    state,
                    new CommitmentPolicyCatalog([policy]),
                    NoDistances.Instance,
                    "bounded-loss",
                    1),
                new CandidateGenerationOptions(
                    maximumCandidatesPerVehicle: 100,
                    maximumNewRequestsPerVehicle: 1,
                    exactSmallMode: false,
                    maximumExplorationWorkUnits: 100_000),
                new SolverBackedRidePoolingPolicyOptions(
                    RidePoolingPolicyKind.RollingCost,
                    executionBudget));

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.Equal(1, result.Decision!.Decision.GenerationDiagnostics!.OmittedRequestCount);
        Assert.True(
            result.Decision.Decision.SelectionExecution!.Diagnostics
                .CandidateLossOccurred);
        Assert.True(
            result.Decision.Decision.SelectionExecution.Diagnostics
                .SolverLossOccurred);
        Assert.Equal(
            CandidateSelectionSolveStatus.SafeFallback,
            result.Decision.Decision.SelectionExecution.SolveResult.Status);
    }

    [Fact]
    public void A_forced_vehicle_does_not_flatten_the_fleet_utilization_ranking()
    {
        // Vehicle 1 has only its forced no-op. Vehicle 2 can serve the request in two ways:
        // 30% utilization at cost 20, or 80% at cost 10. The forced option contributes 0 to
        // the fleet maximum, so vehicle 2 is still ranked by utilization. Had it contributed
        // the 100% cap, the maximum would tie at 100% and cost would pick the 80% plan.
        var request = new RequestId("request-1");
        var first = AlgorithmTestData.VehicleOne;
        var second = AlgorithmTestData.VehicleTwo;
        var forced = Candidate("v1-forced", first, [], 0);
        var noOp = Candidate("v2-noop", second, [], 0);
        var low = Candidate("v2-low", second, [request], 20);
        var high = Candidate("v2-high", second, [request], 10);
        var sets = new[] { Set(first, forced), Set(second, noOp, low, high) };
        var assessments = new Dictionary<string, HardVectorCandidateAssessment>
        {
            [forced.CandidateId] = Hard(forced, 0, Vector()) with { IsForcedReference = true },
            [noOp.CandidateId] = Hard(noOp, 0, Vector()),
            [low.CandidateId] = Hard(low, 300_000, Vector()),
            [high.CandidateId] = Hard(high, 800_000, Vector()),
        };

        var result = Select(
            sets,
            SolverBackedObjectiveProfile.HardVector,
            hardAssessments: assessments);
        var capped = Select(
            sets,
            SolverBackedObjectiveProfile.HardVector,
            hardAssessments: new Dictionary<string, HardVectorCandidateAssessment>(assessments)
            {
                [forced.CandidateId] = Hard(forced, HardVectorCandidateAssessor.PartsPerMillion, Vector()),
            });

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.Equal(
            ["v1-forced", "v2-low"],
            result.Selection!.Selection.VehiclePlans.Select(value => value.Candidate.CandidateId));
        Assert.Equal("forced-reference-count", result.Selection.Problem.ObjectiveLevels[0].Name);
        Assert.True(capped.IsSuccess, capped.Witness?.Message);
        Assert.Equal(
            ["v1-forced", "v2-high"],
            capped.Selection!.Selection.VehiclePlans.Select(value => value.Candidate.CandidateId));
        Assert.Equal("accepted-request-count", capped.Selection.Problem.ObjectiveLevels[0].Name);
    }

    private static readonly string[] StabilityFirstForcedLevels =
    [
        "forced-reference-count",
        "accepted-request-count",
        "worst-hard-utilization-ppm",
        "revision:pickup_eta_total_ms",
        "revision:drop_eta_total_ms",
        "revision:material_eta_revision_count",
        "revision:vehicle_switch_count",
        "revision:pickup_stop_relocation_mm",
        "revision:pickup_stop_switch_count",
        "revision:drop_stop_relocation_mm",
        "revision:drop_stop_switch_count",
        "revision:incumbent_order_inversion_count",
        "revision:pre_pickup_inserted_stop_count",
        "operational-cost",
        "candidate-id-rank:vehicle-1",
        "candidate-id-rank:vehicle-2",
    ];

    /// <summary>
    /// ADR-078. Vehicle 1 has only its forced no-op; vehicle 2 can serve the request at 30%
    /// utilization for cost 20 or at 80% for cost 10. The same candidates and assessments are
    /// ranked under both orders.
    /// </summary>
    private static (VehicleCandidateSet[] Sets, Dictionary<string, HardVectorCandidateAssessment> Hard)
        ForcedAndTwoPricedInsertions()
    {
        var request = new RequestId("request-1");
        var first = AlgorithmTestData.VehicleOne;
        var second = AlgorithmTestData.VehicleTwo;
        var forced = Candidate("v1-forced", first, [], 0);
        var noOp = Candidate("v2-noop", second, [], 0);
        var low = Candidate("v2-low", second, [request], 20);
        var high = Candidate("v2-high", second, [request], 10);
        return (
            [Set(first, forced), Set(second, noOp, low, high)],
            new Dictionary<string, HardVectorCandidateAssessment>
            {
                [forced.CandidateId] = Hard(forced, 0, Vector()) with { IsForcedReference = true },
                [noOp.CandidateId] = Hard(noOp, 0, Vector()),
                [low.CandidateId] = Hard(low, 300_000, Vector()),
                [high.CandidateId] = Hard(high, 800_000, Vector(drop: 5)),
            });
    }

    [Fact]
    public void Stability_first_keeps_the_exact_hard_vector_level_list_and_is_the_default()
    {
        var (sets, hard) = ForcedAndTwoPricedInsertions();

        var byDefault = Select(sets, SolverBackedObjectiveProfile.HardVector, hardAssessments: hard);
        var explicitly = Select(
            sets,
            SolverBackedObjectiveProfile.HardVector,
            hardAssessments: hard,
            objectiveOrder: SolverBackedSelectionObjectiveOrder.StabilityFirst);

        Assert.True(byDefault.IsSuccess, byDefault.Witness?.Message);
        Assert.Equal(
            StabilityFirstForcedLevels,
            byDefault.Selection!.Problem.ObjectiveLevels.Select(value => value.Name));
        Assert.Equal(
            byDefault.Selection.Problem.ObjectiveLevels,
            explicitly.Selection!.Problem.ObjectiveLevels);
        Assert.Equal(
            byDefault.Selection.Problem.Options.Select(value => value.ObjectiveContributions),
            explicitly.Selection.Problem.Options.Select(value => value.ObjectiveContributions));
        Assert.Equal(
            ["v1-forced", "v2-low"],
            byDefault.Selection.Selection.VehiclePlans.Select(value => value.Candidate.CandidateId));
    }

    [Fact]
    public void Cost_first_drops_only_the_utilization_and_revision_levels()
    {
        var (sets, hard) = ForcedAndTwoPricedInsertions();

        var stability = Select(sets, SolverBackedObjectiveProfile.HardVector, hardAssessments: hard);
        var cost = Select(
            sets,
            SolverBackedObjectiveProfile.HardVector,
            hardAssessments: hard,
            objectiveOrder: SolverBackedSelectionObjectiveOrder.CostFirst);

        Assert.True(cost.IsSuccess, cost.Witness?.Message);
        Assert.Equal(
            [
                "forced-reference-count",
                "accepted-request-count",
                "operational-cost",
                "candidate-id-rank:vehicle-1",
                "candidate-id-rank:vehicle-2",
            ],
            cost.Selection!.Problem.ObjectiveLevels.Select(value => value.Name));
        // Every kept level is the stability-first level itself (name, sense and aggregation).
        Assert.Equal(
            stability.Selection!.Problem.ObjectiveLevels.Where(
                value => value.Name != "worst-hard-utilization-ppm"
                    && !value.Name.StartsWith("revision:", StringComparison.Ordinal)),
            cost.Selection.Problem.ObjectiveLevels);
        Assert.Equal(
            stability.Selection.Problem.Options.Select(value => value.OptionId),
            cost.Selection.Problem.Options.Select(value => value.OptionId));
        // Each option contributes the same value on every kept level.
        var keptLevelIndices = stability.Selection.Problem.ObjectiveLevels
            .Select((level, index) => (level, index))
            .Where(pair => pair.level.Name != "worst-hard-utilization-ppm"
                && !pair.level.Name.StartsWith("revision:", StringComparison.Ordinal))
            .Select(pair => pair.index)
            .ToArray();
        Assert.Equal(
            stability.Selection.Problem.Options.Select(
                option => keptLevelIndices.Select(index => option.ObjectiveContributions[index]).ToArray()),
            cost.Selection.Problem.Options.Select(option => option.ObjectiveContributions.ToArray()));
        // The cheaper 80% plan wins; the forced vehicle and the accepted count are unchanged.
        Assert.Equal(
            ["v1-forced", "v2-high"],
            cost.Selection.Selection.VehiclePlans.Select(value => value.Candidate.CandidateId));
        Assert.Equal(1, cost.Selection.Selection.AcceptedRequestCount);
        Assert.Equal(10, cost.Selection.Selection.OperationalCost);
        Assert.Equal(20, stability.Selection.Selection.OperationalCost);
        // The selection still reports the utilization and revision it carries.
        Assert.Equal(800_000, cost.Selection.Selection.WorstHardUtilizationPartsPerMillion);
        Assert.Equal(5, cost.Selection.Selection.DecisionInducedRevision!.DropEtaTotalMs);
    }

    [Fact]
    public void Cost_first_keeps_the_forced_level_condition_and_equals_stability_first_without_hard_limits()
    {
        var (sets, hard) = ForcedAndTwoPricedInsertions();
        var unforced = new Dictionary<string, HardVectorCandidateAssessment>(hard)
        {
            ["v1-forced"] = hard["v1-forced"] with { IsForcedReference = false },
        };
        var withoutForced = Select(
            sets,
            SolverBackedObjectiveProfile.HardVector,
            hardAssessments: unforced,
            objectiveOrder: SolverBackedSelectionObjectiveOrder.CostFirst);

        Assert.Equal(
            "accepted-request-count",
            withoutForced.Selection!.Problem.ObjectiveLevels[0].Name);

        var unbounded = hard.ToDictionary(
            value => value.Key,
            value => value.Value with { HasApplicableHardLimit = false, IsForcedReference = false });
        var stability = Select(sets, SolverBackedObjectiveProfile.HardVector, hardAssessments: unbounded);
        var cost = Select(
            sets,
            SolverBackedObjectiveProfile.HardVector,
            hardAssessments: unbounded,
            objectiveOrder: SolverBackedSelectionObjectiveOrder.CostFirst);

        Assert.Equal(
            stability.Selection!.Problem.ObjectiveLevels,
            cost.Selection!.Problem.ObjectiveLevels);
        Assert.Equal(
            stability.Selection.Selection.VehiclePlans.Select(value => value.Candidate.CandidateId),
            cost.Selection.Selection.VehiclePlans.Select(value => value.Candidate.CandidateId));
    }

    [Fact]
    public void Cost_first_is_rejected_outside_the_hard_vector_profile()
    {
        var (sets, hard) = ForcedAndTwoPricedInsertions();

        foreach (var profile in new[]
                 {
                     SolverBackedObjectiveProfile.RollingCost,
                     SolverBackedObjectiveProfile.RevisionPenalty,
                     SolverBackedObjectiveProfile.SoftHardHybrid,
                 })
        {
            Assert.Throws<ArgumentException>(
                () => Select(
                    sets,
                    profile,
                    hardAssessments: hard,
                    objectiveOrder: SolverBackedSelectionObjectiveOrder.CostFirst));
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            () => Select(
                sets,
                SolverBackedObjectiveProfile.HardVector,
                hardAssessments: hard,
                objectiveOrder: (SolverBackedSelectionObjectiveOrder)2));
    }

    [Fact]
    public void Cost_first_policy_options_are_valid_only_for_C1_and_default_to_stability_first()
    {
        var budget = DeterministicCandidateSelectionExecutionBudget.Create(
            1_000,
            1_000,
            DeterministicSolverBudget.Create(1_000, 1_000, 1).Value!).Value!;

        Assert.Equal(
            SolverBackedSelectionObjectiveOrder.StabilityFirst,
            new SolverBackedRidePoolingPolicyOptions(
                RidePoolingPolicyKind.RideBoundHardVector,
                budget).SelectionObjectiveOrder);
        Assert.Equal(
            SolverBackedSelectionObjectiveOrder.CostFirst,
            new SolverBackedRidePoolingPolicyOptions(
                RidePoolingPolicyKind.RideBoundHardVector,
                budget,
                forcedReferenceRecovery: true,
                forcedNoWorseRecovery: true,
                selectionObjectiveOrder: SolverBackedSelectionObjectiveOrder.CostFirst)
                .SelectionObjectiveOrder);

        foreach (var kind in new[]
                 {
                     RidePoolingPolicyKind.RollingCost,
                     RidePoolingPolicyKind.RollingPenalty,
                     RidePoolingPolicyKind.CommitSoftHardHybrid,
                 })
        {
            Assert.Throws<ArgumentException>(
                () => new SolverBackedRidePoolingPolicyOptions(
                    kind,
                    budget,
                    selectionObjectiveOrder: SolverBackedSelectionObjectiveOrder.CostFirst));
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SolverBackedRidePoolingPolicyOptions(
                RidePoolingPolicyKind.RideBoundHardVector,
                budget,
                selectionObjectiveOrder: (SolverBackedSelectionObjectiveOrder)2));
    }

    private static SolverBackedFleetSelectionResult Select(
        IReadOnlyList<VehicleCandidateSet> sets,
        SolverBackedObjectiveProfile profile,
        IReadOnlyDictionary<string, CandidateCommitmentAssessment>?
            revisionAssessments = null,
        IReadOnlyDictionary<string, HardVectorCandidateAssessment>?
            hardAssessments = null,
        IFleetSelectionValidator? validator = null,
        SolverBackedSelectionObjectiveOrder objectiveOrder =
            SolverBackedSelectionObjectiveOrder.StabilityFirst)
    {
        var solverBudget = DeterministicSolverBudget.Create(1_000, 1_000, 1).Value!;
        var budget = DeterministicCandidateSelectionExecutionBudget.Create(
            1_000,
            1_000,
            solverBudget).Value!;
        var accounting = CandidateSelectionPreSolveAccounting.Create(
            budget,
            1,
            0,
            0).Value!;
        return new SolverBackedFleetSelector(new ExactTestSolver()).Select(
            sets,
            profile,
            budget,
            accounting,
            validator ?? new FleetValidator(_ => true),
            revisionAssessments,
            hardAssessments,
            objectiveOrder);
    }

    private static VehicleCandidateSet Set(
        VehicleId vehicleId,
        params InsertionCandidate[] candidates) =>
        new(vehicleId, candidates, [], false);

    private static InsertionCandidate Candidate(
        string id,
        VehicleId vehicleId,
        IReadOnlyList<RequestId> requests,
        long cost) =>
        new(
            id,
            vehicleId,
            AlgorithmTestData.Vehicle(id: vehicleId).Route,
            requests,
            new CandidateSchedule([], cost),
            requests.Count == 0);

    private static CandidateCommitmentAssessment Revision(
        InsertionCandidate candidate,
        CommitmentVector revision) =>
        new(candidate.CandidateId, revision);

    private static HardVectorCandidateAssessment Hard(
        InsertionCandidate candidate,
        long utilization,
        CommitmentVector revision,
        bool hasHard = true,
        CommitmentVector? warning = null) =>
        new(
            candidate.CandidateId,
            utilization,
            revision,
            hasHard,
            warning,
            warning is not null);

    private static CommitmentVector Vector(
        long pickup = 0,
        long drop = 0,
        long material = 0) =>
        new(pickup, drop, material, 0, 0, 0, 0, 0, 0, 0);

    private sealed class FleetValidator(Func<FleetSelection, bool> validate) :
        IFleetSelectionValidator
    {
        public CandidateSelectionValidationResult Validate(
            FleetSelection selection) =>
            validate(selection)
                ? CandidateSelectionValidationResult.Valid()
                : CandidateSelectionValidationResult.Invalid(
                    "TEST_FLEET_REJECTED",
                    "The test fleet validator rejected the selection.");
    }

    private sealed class ExactTestSolver : ICandidateSelectionSolver
    {
        public CandidateSelectionSolveResult Solve(
            CandidateSelectionProblem problem,
            DeterministicSolverBudget budget)
        {
            CandidateSelectionSolution? best = null;
            long work = 0;
            Enumerate(0, []);

            if (best is null)
            {
                return CandidateSelectionSolveResult.Infeasible(
                    Diagnostics(problem, budget, work, []),
                    "TEST_INFEASIBLE",
                    "No assignment exists.");
            }

            var bounds = problem.ObjectiveLevels
                .Select(
                    (level, index) => ObjectiveSolveBound.Create(
                        index,
                        level,
                        best.ObjectiveValues[index],
                        best.ObjectiveValues[index]).Value!)
                .ToArray();
            return CandidateSelectionSolveResult.Optimal(
                best,
                Diagnostics(problem, budget, work, bounds));

            void Enumerate(int vehicleIndex, IReadOnlyList<string> selected)
            {
                if (vehicleIndex == problem.VehicleIds.Count)
                {
                    work++;
                    var solution = CandidateSelectionSolution.Create(problem, selected);

                    if (solution.IsSuccess
                        && (best is null
                            || LexicographicObjectiveComparer.Compare(
                                solution.Value!.ObjectiveValues,
                                best.ObjectiveValues,
                                problem.ObjectiveLevels) < 0))
                    {
                        best = solution.Value;
                    }

                    return;
                }

                foreach (var option in problem.Options.Where(
                             value => value.VehicleId
                                 == problem.VehicleIds[vehicleIndex]))
                {
                    Enumerate(vehicleIndex + 1, selected.Append(option.OptionId).ToArray());
                }
            }
        }

        private static CandidateSelectionSolverDiagnostics Diagnostics(
            CandidateSelectionProblem problem,
            DeterministicSolverBudget budget,
            long work,
            IReadOnlyList<ObjectiveSolveBound> bounds) =>
            CandidateSelectionSolverDiagnostics.Create(
                problem,
                budget,
                Math.Min(work, budget.MaximumWorkUnits),
                1,
                0,
                bounds).Value!;
    }

    private sealed class UnknownTestSolver : ICandidateSelectionSolver
    {
        public CandidateSelectionSolveResult Solve(
            CandidateSelectionProblem problem,
            DeterministicSolverBudget budget)
        {
            var diagnostics = CandidateSelectionSolverDiagnostics.Create(
                problem,
                budget,
                0,
                0,
                0,
                []).Value!;
            return CandidateSelectionSolveResult.Unknown(
                diagnostics,
                "TEST_UNKNOWN",
                "The test solver did not produce an incumbent.");
        }
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
