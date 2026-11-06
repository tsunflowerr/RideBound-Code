using RideBound.Application.Optimization;
using RideBound.Domain.Common;

namespace RideBound.Solvers.OrTools.Tests;

public sealed class OrToolsCandidateSelectionSolverTests
{
    [Fact]
    public void Multi_pass_lexicographic_solve_honors_request_uniqueness_and_maximum()
    {
        var problem = TradeoffProblem();

        var result = new OrToolsCandidateSelectionSolver().Solve(
            problem,
            LargeBudget(seed: 7));

        Assert.Equal(CandidateSelectionSolveStatus.Optimal, result.Status);
        Assert.Equal(["v1-request-2", "v2-request-1"], result.Solution!.SelectedOptionIds);
        Assert.Equal([2, 100, 110, 3], result.Solution.ObjectiveValues);
        Assert.Equal(4, result.Diagnostics.ObjectiveBounds.Count);
        Assert.All(
            result.Diagnostics.ObjectiveBounds,
            bound => Assert.True(bound.IsProvenOptimal));
        Assert.All(
            result.Diagnostics.ObjectiveBounds,
            bound => Assert.Equal(
                result.Solution.ObjectiveValues[bound.LevelIndex],
                bound.BestBound));
    }

    [Fact]
    public void Accepted_count_remains_strictly_above_later_objectives()
    {
        var vehicle = new VehicleId("vehicle-1");
        var request = new RequestId("request-1");
        var problem = CandidateSelectionProblem.Create(
            [vehicle],
            [request],
            [
                new CandidateSelectionObjectiveLevel(
                    "accepted",
                    CandidateSelectionObjectiveSense.Maximize,
                    CandidateSelectionObjectiveAggregation.Sum),
                new CandidateSelectionObjectiveLevel(
                    "cost",
                    CandidateSelectionObjectiveSense.Minimize,
                    CandidateSelectionObjectiveAggregation.Sum),
            ],
            [
                new CandidateSelectionOption("noop", vehicle, [], [0, 0], true),
                new CandidateSelectionOption(
                    "accept-expensive",
                    vehicle,
                    [request],
                    [1, 1_000_000],
                    false),
            ]).Value!;

        var result = new OrToolsCandidateSelectionSolver().Solve(
            problem,
            LargeBudget(seed: 1));

        Assert.Equal(CandidateSelectionSolveStatus.Optimal, result.Status);
        Assert.Equal("accept-expensive", Assert.Single(result.Solution!.SelectedOptionIds));
    }

    [Fact]
    public void Medium_fleet_tie_break_levels_fit_the_locked_wp4_budget()
    {
        var vehicles = Enumerable.Range(0, 32)
            .Select(index => new VehicleId($"vehicle-{index:D3}"))
            .ToArray();
        var request = new RequestId("request-1");
        var levels = new[]
        {
            new CandidateSelectionObjectiveLevel(
                "accepted",
                CandidateSelectionObjectiveSense.Maximize,
                CandidateSelectionObjectiveAggregation.Sum),
            new CandidateSelectionObjectiveLevel(
                "cost",
                CandidateSelectionObjectiveSense.Minimize,
                CandidateSelectionObjectiveAggregation.Sum),
        }.Concat(
            vehicles.Select(
                vehicle => new CandidateSelectionObjectiveLevel(
                    $"candidate-id-rank:{vehicle.Value}",
                    CandidateSelectionObjectiveSense.Minimize,
                    CandidateSelectionObjectiveAggregation.Sum)))
            .ToArray();
        var options = vehicles.SelectMany(
                (vehicle, vehicleIndex) =>
                {
                    var noOp = new long[levels.Length];
                    var accept = new long[levels.Length];
                    accept[0] = 1;
                    accept[1] = 1_000;
                    accept[vehicleIndex + 2] = 1;
                    return new[]
                    {
                        new CandidateSelectionOption(
                            $"noop-{vehicle.Value}",
                            vehicle,
                            [],
                            noOp,
                            true),
                        new CandidateSelectionOption(
                            $"accept-{vehicle.Value}",
                            vehicle,
                            [request],
                            accept,
                            false),
                    };
                })
            .ToArray();
        var problem = CandidateSelectionProblem.Create(
            vehicles,
            [request],
            levels,
            options).Value!;
        var budget = DeterministicSolverBudget.Create(
            maximumWorkUnits: 100_000,
            maximumDeterministicTimeMicros: 1_000_000,
            randomSeed: 12_345).Value!;

        var result = new OrToolsCandidateSelectionSolver().Solve(problem, budget);

        Assert.Equal(CandidateSelectionSolveStatus.Optimal, result.Status);
        Assert.Equal(1, result.Solution!.ObjectiveValues[0]);
        Assert.Equal(34, result.Diagnostics.ObjectiveBounds.Count);
    }

    [Fact]
    public void Same_canonical_problem_seed_and_budget_replay_identically()
    {
        var solver = new OrToolsCandidateSelectionSolver();
        var problem = TradeoffProblem(reverseInput: true);
        var budget = LargeBudget(seed: 42);

        var runs = Enumerable.Range(0, 8)
            .Select(_ => solver.Solve(problem, budget))
            .ToArray();

        Assert.All(
            runs,
            result => Assert.Equal(
                CandidateSelectionSolveStatus.Optimal,
                result.Status));
        Assert.All(
            runs.Skip(1),
            result =>
            {
                Assert.Equal(
                    runs[0].Solution!.SelectedOptionIds,
                    result.Solution!.SelectedOptionIds);
                Assert.Equal(
                    runs[0].Solution!.ObjectiveValues,
                    result.Solution.ObjectiveValues);
                Assert.Equal(
                    runs[0].Diagnostics.ObjectiveBounds.Select(BoundKey),
                    result.Diagnostics.ObjectiveBounds.Select(BoundKey));
                Assert.Equal(
                    runs[0].Diagnostics.ConsumedWorkUnits,
                    result.Diagnostics.ConsumedWorkUnits);
                Assert.Equal(
                    runs[0].Diagnostics.ConsumedDeterministicTimeMicros,
                    result.Diagnostics.ConsumedDeterministicTimeMicros);
            });
    }

    [Fact]
    public void Aggregate_objective_overflow_is_model_invalid_not_solver_optimal()
    {
        var first = new VehicleId("vehicle-1");
        var second = new VehicleId("vehicle-2");
        var problem = CandidateSelectionProblem.Create(
            [first, second],
            [],
            [
                new CandidateSelectionObjectiveLevel(
                    "overflow",
                    CandidateSelectionObjectiveSense.Minimize,
                    CandidateSelectionObjectiveAggregation.Sum),
            ],
            [
                new CandidateSelectionOption(
                    "v1-noop",
                    first,
                    [],
                    [DomainLimits.MaxCanonicalInteger],
                    true),
                new CandidateSelectionOption(
                    "v2-noop",
                    second,
                    [],
                    [DomainLimits.MaxCanonicalInteger],
                    true),
            ]).Value!;

        var result = new OrToolsCandidateSelectionSolver().Solve(
            problem,
            LargeBudget(seed: 0));

        Assert.Equal(CandidateSelectionSolveStatus.ModelInvalid, result.Status);
        Assert.Null(result.Solution);
        Assert.Equal("ORTOOLS_MODEL_INVALID", result.ReasonCode);
        Assert.Empty(result.Diagnostics.ObjectiveBounds);
    }

    [Fact]
    public void Diagnostics_never_exceed_explicit_deterministic_budgets()
    {
        var budget = DeterministicSolverBudget.Create(
            maximumWorkUnits: 100_000,
            maximumDeterministicTimeMicros: 5_000_000,
            randomSeed: 19).Value!;

        var result = new OrToolsCandidateSelectionSolver().Solve(
            TradeoffProblem(),
            budget);

        Assert.InRange(
            result.Diagnostics.ConsumedWorkUnits,
            0,
            budget.MaximumWorkUnits);
        Assert.InRange(
            result.Diagnostics.ConsumedDeterministicTimeMicros,
            0,
            budget.MaximumDeterministicTimeMicros);
        Assert.Equal("ORTOOLS_OPTIMAL", result.Diagnostics.DetailCode);
        Assert.Contains(
            OrToolsCandidateSelectionSolver.AdapterVersion,
            result.Diagnostics.Detail);
    }

    private static CandidateSelectionProblem TradeoffProblem(
        bool reverseInput = false)
    {
        var vehicleOne = new VehicleId("vehicle-1");
        var vehicleTwo = new VehicleId("vehicle-2");
        var requestOne = new RequestId("request-1");
        var requestTwo = new RequestId("request-2");
        var levels = new[]
        {
            new CandidateSelectionObjectiveLevel(
                "accepted",
                CandidateSelectionObjectiveSense.Maximize,
                CandidateSelectionObjectiveAggregation.Sum),
            new CandidateSelectionObjectiveLevel(
                "worst-utilization",
                CandidateSelectionObjectiveSense.Minimize,
                CandidateSelectionObjectiveAggregation.Maximum),
            new CandidateSelectionObjectiveLevel(
                "cost",
                CandidateSelectionObjectiveSense.Minimize,
                CandidateSelectionObjectiveAggregation.Sum),
            new CandidateSelectionObjectiveLevel(
                "stable-id",
                CandidateSelectionObjectiveSense.Minimize,
                CandidateSelectionObjectiveAggregation.Sum),
        };
        var options = new[]
        {
            new CandidateSelectionOption(
                "v1-noop", vehicleOne, [], [0, 0, 0, 0], true),
            new CandidateSelectionOption(
                "v1-request-1", vehicleOne, [requestOne], [1, 900, 1, 1], false),
            new CandidateSelectionOption(
                "v1-request-2", vehicleOne, [requestTwo], [1, 100, 100, 2], false),
            new CandidateSelectionOption(
                "v2-noop", vehicleTwo, [], [0, 0, 0, 0], true),
            new CandidateSelectionOption(
                "v2-request-1", vehicleTwo, [requestOne], [1, 100, 10, 1], false),
            new CandidateSelectionOption(
                "v2-request-2", vehicleTwo, [requestTwo], [1, 200, 10, 2], false),
        };

        return CandidateSelectionProblem.Create(
            reverseInput ? [vehicleTwo, vehicleOne] : [vehicleOne, vehicleTwo],
            reverseInput ? [requestTwo, requestOne] : [requestOne, requestTwo],
            levels,
            reverseInput ? options.Reverse() : options).Value!;
    }

    private static DeterministicSolverBudget LargeBudget(long seed) =>
        DeterministicSolverBudget.Create(
            maximumWorkUnits: 1_000_000,
            maximumDeterministicTimeMicros: 10_000_000,
            randomSeed: seed).Value!;

    private static string BoundKey(ObjectiveSolveBound bound) =>
        string.Join(
            ":",
            bound.LevelIndex,
            bound.ObjectiveName,
            bound.IncumbentValue,
            bound.BestBound,
            bound.GapNumerator,
            bound.GapDenominator,
            bound.IsProvenOptimal);
}
