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
            DecisionFailureCodes.CandidatePortfolioEvidenceInvalid,
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
                    RidePoolingPolicyKind.RideBoundHardVector,
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

    /// <summary>
    /// ADR-078. Vehicle 1 has only its forced no-op; vehicle 2 can serve the request for cost 20
    /// (v2-dear) or for cost 10 (v2-cheap).
    /// </summary>
    private static (VehicleCandidateSet[] Sets, Dictionary<string, HardVectorCandidateAssessment> Hard)
        ForcedAndTwoPricedInsertions()
    {
        var request = new RequestId("request-1");
        var first = AlgorithmTestData.VehicleOne;
        var second = AlgorithmTestData.VehicleTwo;
        var forced = Candidate("v1-forced", first, [], 0);
        var noOp = Candidate("v2-noop", second, [], 0);
        var dear = Candidate("v2-dear", second, [request], 20);
        var cheap = Candidate("v2-cheap", second, [request], 10);
        return (
            [Set(first, forced), Set(second, noOp, dear, cheap)],
            new Dictionary<string, HardVectorCandidateAssessment>
            {
                [forced.CandidateId] = new(forced.CandidateId, IsForcedReference: true),
                [noOp.CandidateId] = new(noOp.CandidateId),
                [dear.CandidateId] = new(dear.CandidateId),
                [cheap.CandidateId] = new(cheap.CandidateId),
            });
    }

    [Fact]
    public void Cost_first_drops_only_the_utilization_and_revision_levels()
    {
        // The C1 levels (cost-first, the only order): the forced level when a forced option
        // exists, the accepted count, cost, then one candidate-id rank level per vehicle. There is
        // no utilization or revision level.
        var (sets, hard) = ForcedAndTwoPricedInsertions();
        var unforcedHard = hard.ToDictionary(
            value => value.Key,
            value => value.Value with { IsForcedReference = false });

        var forced = Select(sets, SolverBackedObjectiveProfile.HardVector, hardAssessments: hard);
        var unforced = Select(sets, SolverBackedObjectiveProfile.HardVector, hardAssessments: unforcedHard);

        Assert.True(forced.IsSuccess, forced.Witness?.Message);
        Assert.True(unforced.IsSuccess, unforced.Witness?.Message);
        CandidateSelectionObjectiveLevel[] levels =
        [
            new("forced-reference-count", CandidateSelectionObjectiveSense.Minimize, CandidateSelectionObjectiveAggregation.Sum),
            new("accepted-request-count", CandidateSelectionObjectiveSense.Maximize, CandidateSelectionObjectiveAggregation.Sum),
            new("operational-cost", CandidateSelectionObjectiveSense.Minimize, CandidateSelectionObjectiveAggregation.Sum),
            new("candidate-id-rank:vehicle-1", CandidateSelectionObjectiveSense.Minimize, CandidateSelectionObjectiveAggregation.Sum),
            new("candidate-id-rank:vehicle-2", CandidateSelectionObjectiveSense.Minimize, CandidateSelectionObjectiveAggregation.Sum),
        ];
        Assert.Equal(levels, forced.Selection!.Problem.ObjectiveLevels);
        Assert.Equal(levels.Skip(1), unforced.Selection!.Problem.ObjectiveLevels);

        // Each option's contribution on every level, in level order (options sorted by vehicle, then id).
        static string[] Contributions(SolverBackedFleetSelectionResult result) =>
            result.Selection!.Problem.Options
                .Select(value => $"{value.OptionId}={string.Join(",", value.ObjectiveContributions)}")
                .ToArray();
        Assert.Equal(
            ["v1-forced=1,0,0,0,0", "v2-cheap=0,1,10,0,0", "v2-dear=0,1,20,0,1", "v2-noop=0,0,0,0,2"],
            Contributions(forced));
        Assert.Equal(
            ["v1-forced=0,0,0,0", "v2-cheap=1,10,0,0", "v2-dear=1,20,0,1", "v2-noop=0,0,0,2"],
            Contributions(unforced));

        // The cheaper plan wins; the forced vehicle keeps its only option.
        Assert.Equal(
            ["v1-forced", "v2-cheap"],
            forced.Selection.Selection.VehiclePlans.Select(value => value.Candidate.CandidateId));
        Assert.Equal(1, forced.Selection.Selection.AcceptedRequestCount);
        Assert.Equal(10, forced.Selection.Selection.OperationalCost);
    }

    private static SolverBackedFleetSelectionResult Select(
        IReadOnlyList<VehicleCandidateSet> sets,
        SolverBackedObjectiveProfile profile,
        IReadOnlyDictionary<string, HardVectorCandidateAssessment>?
            hardAssessments = null,
        IFleetSelectionValidator? validator = null)
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
            hardAssessments: hardAssessments);
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
