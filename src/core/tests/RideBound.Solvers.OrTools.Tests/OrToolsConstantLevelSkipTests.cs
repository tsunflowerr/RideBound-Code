using RideBound.Application.Optimization;
using RideBound.Domain.Common;

namespace RideBound.Solvers.OrTools.Tests;

/// <summary>
/// RB-WP14-002. Skipping a lexicographic level that is constant over the feasible
/// set must not change which assignment is returned, only how much solver work is
/// recorded.
/// </summary>
public sealed class OrToolsConstantLevelSkipTests
{
    [Fact]
    public void Skipping_constant_levels_returns_the_same_assignment_and_optima()
    {
        var problem = ProductionShapedProblem();

        var solved = new OrToolsCandidateSelectionSolver().Solve(
            problem,
            Budget(skip: false));
        var skipped = new OrToolsCandidateSelectionSolver().Solve(
            problem,
            Budget(skip: true));

        Assert.Equal(CandidateSelectionSolveStatus.Optimal, solved.Status);
        Assert.Equal(CandidateSelectionSolveStatus.Optimal, skipped.Status);
        Assert.Equal(
            solved.Solution!.SelectedOptionIds,
            skipped.Solution!.SelectedOptionIds);
        Assert.Equal(
            solved.Solution.ObjectiveValues,
            skipped.Solution.ObjectiveValues);
        Assert.Equal(
            solved.Diagnostics.ObjectiveBounds.Select(BoundKey),
            skipped.Diagnostics.ObjectiveBounds.Select(BoundKey));
    }

    [Fact]
    public void Skipping_records_less_solver_work_and_says_so()
    {
        var problem = ProductionShapedProblem();

        var solved = new OrToolsCandidateSelectionSolver().Solve(
            problem,
            Budget(skip: false));
        var skipped = new OrToolsCandidateSelectionSolver().Solve(
            problem,
            Budget(skip: true));

        Assert.Equal("ORTOOLS_OPTIMAL", solved.Diagnostics.DetailCode);
        Assert.Equal(
            "ORTOOLS_OPTIMAL_CONSTANT_LEVELS_SKIPPED",
            skipped.Diagnostics.DetailCode);
        Assert.Contains(
            "lexicographic levels were constant",
            skipped.Diagnostics.Detail);

        // The recorded deterministic time must fall, because fewer models were
        // really solved. Evidence never claims work that did not happen, so the
        // option stays opt-in and every published run keeps its own numbers.
        Assert.True(
            skipped.Diagnostics.ConsumedDeterministicTimeMicros
                < solved.Diagnostics.ConsumedDeterministicTimeMicros,
            "skipping constant levels must consume strictly less deterministic time");
    }

    [Fact]
    public void Every_level_reports_a_bound_even_when_it_was_not_solved()
    {
        var problem = ProductionShapedProblem();
        var constants = problem.ConstantObjectiveLevelValues();

        var skipped = new OrToolsCandidateSelectionSolver().Solve(
            problem,
            Budget(skip: true));

        Assert.Equal(
            problem.ObjectiveLevels.Count,
            skipped.Diagnostics.ObjectiveBounds.Count);
        Assert.All(
            skipped.Diagnostics.ObjectiveBounds,
            bound => Assert.True(bound.IsProvenOptimal));
        Assert.Contains(constants, value => value is not null);

        for (var level = 0; level < constants.Count; level++)
        {
            if (constants[level] is not long constant)
            {
                continue;
            }

            Assert.Equal(
                constant,
                skipped.Diagnostics.ObjectiveBounds[level].IncumbentValue);
        }
    }

    [Fact]
    public void An_all_constant_hierarchy_still_produces_an_incumbent()
    {
        var vehicle = new VehicleId("vehicle-1");
        var problem = CandidateSelectionProblem.Create(
            [vehicle],
            [],
            [
                Level("only-level", CandidateSelectionObjectiveAggregation.Sum),
            ],
            [
                new CandidateSelectionOption("noop", vehicle, [], [42], true),
            ]).Value!;

        Assert.Equal([42L], problem.ConstantObjectiveLevelValues());

        var result = new OrToolsCandidateSelectionSolver().Solve(
            problem,
            Budget(skip: true));

        Assert.Equal(CandidateSelectionSolveStatus.Optimal, result.Status);
        Assert.Equal("noop", Assert.Single(result.Solution!.SelectedOptionIds));
        Assert.Equal("ORTOOLS_OPTIMAL", result.Diagnostics.DetailCode);
    }

    [Fact]
    public void A_level_wrongly_treated_as_constant_changes_the_assignment()
    {
        // Mutation guard. The skip is only safe because a constant level cannot
        // discriminate; if a discriminating level were skipped the returned
        // assignment would change. This proves the property the guard relies on
        // is real rather than incidental.
        var vehicle = new VehicleId("vehicle-1");
        var request = new RequestId("request-1");
        var deciding = CandidateSelectionProblem.Create(
            [vehicle],
            [request],
            [
                Level("cost", CandidateSelectionObjectiveAggregation.Sum),
                Level("tie-break", CandidateSelectionObjectiveAggregation.Sum),
            ],
            [
                new CandidateSelectionOption("cheap", vehicle, [request], [1, 0], false),
                new CandidateSelectionOption("noop", vehicle, [], [9, 1], true),
            ]).Value!;
        var withoutDeciding = CandidateSelectionProblem.Create(
            [vehicle],
            [request],
            [
                Level("cost", CandidateSelectionObjectiveAggregation.Sum),
                Level("tie-break", CandidateSelectionObjectiveAggregation.Sum),
            ],
            [
                new CandidateSelectionOption("cheap", vehicle, [request], [5, 0], false),
                new CandidateSelectionOption("noop", vehicle, [], [5, 1], true),
            ]).Value!;

        Assert.Null(deciding.ConstantObjectiveLevelValues()[0]);
        Assert.Equal(5, withoutDeciding.ConstantObjectiveLevelValues()[0]);

        var solver = new OrToolsCandidateSelectionSolver();
        var decided = solver.Solve(deciding, Budget(skip: true));
        var tied = solver.Solve(withoutDeciding, Budget(skip: true));

        Assert.Equal("cheap", Assert.Single(decided.Solution!.SelectedOptionIds));
        Assert.Equal("cheap", Assert.Single(tied.Solution!.SelectedOptionIds));
        Assert.NotEqual(
            decided.Solution.ObjectiveValues[0],
            tied.Solution.ObjectiveValues[0]);
    }

    [Fact]
    public void Skipping_is_off_unless_the_budget_asks_for_it()
    {
        var budget = DeterministicSolverBudget.Create(
            maximumWorkUnits: 1_000_000,
            maximumDeterministicTimeMicros: 10_000_000,
            randomSeed: 7).Value!;

        Assert.False(budget.SkipConstantObjectiveLevels);
    }

    /// <summary>
    /// Mirrors the production hierarchy: an accepted-count level, a constant
    /// worst-utilisation level, several constant revision levels, a deciding cost
    /// level and one candidate-id rank tie-break per vehicle.
    /// </summary>
    private static CandidateSelectionProblem ProductionShapedProblem()
    {
        var vehicleOne = new VehicleId("vehicle-1");
        var vehicleTwo = new VehicleId("vehicle-2");
        var requestOne = new RequestId("request-1");
        var requestTwo = new RequestId("request-2");
        CandidateSelectionObjectiveLevel[] levels =
        [
            new(
                "accepted-request-count",
                CandidateSelectionObjectiveSense.Maximize,
                CandidateSelectionObjectiveAggregation.Sum),
            Level(
                "worst-hard-utilization-ppm",
                CandidateSelectionObjectiveAggregation.Maximum),
            Level("revision:pickup_eta_total_ms", CandidateSelectionObjectiveAggregation.Sum),
            Level("revision:vehicle_switch_count", CandidateSelectionObjectiveAggregation.Sum),
            Level("operational-cost", CandidateSelectionObjectiveAggregation.Sum),
            Level("candidate-id-rank:vehicle-1", CandidateSelectionObjectiveAggregation.Sum),
            Level("candidate-id-rank:vehicle-2", CandidateSelectionObjectiveAggregation.Sum),
        ];
        CandidateSelectionOption[] options =
        [
            //                                       acc  ppm  rev  rev  cost  r1  r2
            new("v1-a-noop", vehicleOne, [], [0, 1_000_000, 0, 0, 0, 0, 0], true),
            new("v1-b-one", vehicleOne, [requestOne], [1, 1_000_000, 0, 0, 40, 1, 0], false),
            new("v1-c-two", vehicleOne, [requestTwo], [1, 1_000_000, 0, 0, 90, 2, 0], false),
            new("v2-a-noop", vehicleTwo, [], [0, 1_000_000, 0, 0, 0, 0, 0], true),
            new("v2-b-one", vehicleTwo, [requestOne], [1, 1_000_000, 0, 0, 70, 0, 1], false),
            new("v2-c-two", vehicleTwo, [requestTwo], [1, 1_000_000, 0, 0, 30, 0, 2], false),
        ];

        return CandidateSelectionProblem.Create(
            [vehicleOne, vehicleTwo],
            [requestOne, requestTwo],
            levels,
            options).Value!;
    }

    private static CandidateSelectionObjectiveLevel Level(
        string name,
        CandidateSelectionObjectiveAggregation aggregation) =>
        new(name, CandidateSelectionObjectiveSense.Minimize, aggregation);

    private static DeterministicSolverBudget Budget(bool skip) =>
        DeterministicSolverBudget.Create(
            maximumWorkUnits: 1_000_000,
            maximumDeterministicTimeMicros: 10_000_000,
            randomSeed: 7,
            skipConstantObjectiveLevels: skip).Value!;

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
