using RideBound.Application.Optimization;
using RideBound.Domain.Common;

namespace RideBound.Application.Tests.Optimization;

public sealed class CandidateSelectionExecutionTests
{
    [Fact]
    public void Validated_optimal_incumbent_is_preserved_without_fallback()
    {
        var problem = Problem();
        var budget = Budget();
        var incumbent = Solution(problem, "v1-accept", "v2-noop");
        var solver = new StubSolver(
            (_, solverBudget) => Optimal(problem, incumbent, solverBudget));
        var validator = new StubValidator(_ => true);

        var result = new SafeCandidateSelectionExecutor(solver, validator).Execute(
            problem,
            budget,
            Accounting(budget));

        Assert.Equal(CandidateSelectionSolveStatus.Optimal, result.SolveResult.Status);
        Assert.Equal(
            CandidateSelectionExecutionPath.ValidatedIncumbent,
            result.Diagnostics.ExecutionPath);
        Assert.Equal(["v1-accept", "v2-noop"], result.SolveResult.Solution!.SelectedOptionIds);
        Assert.Equal(1, result.Diagnostics.ConsumedValidationWorkUnits);
        Assert.Equal(0, result.Diagnostics.FallbackValidationAttempts);
        Assert.False(result.Diagnostics.PrimaryIncumbentRejected);
    }

    [Fact]
    public void Feasible_incumbent_is_not_promoted_to_optimal()
    {
        var problem = Problem();
        var budget = Budget();
        var incumbent = Solution(problem, "v1-accept", "v2-noop");
        var solver = new StubSolver(
            (_, solverBudget) => CandidateSelectionSolveResult.Feasible(
                incumbent,
                Diagnostics(problem, solverBudget)));

        var result = new SafeCandidateSelectionExecutor(
            solver,
            new StubValidator(_ => true)).Execute(
                problem,
                budget,
                Accounting(budget));

        Assert.Equal(CandidateSelectionSolveStatus.Feasible, result.SolveResult.Status);
        Assert.True(result.Diagnostics.SolverLossOccurred);
    }

    [Theory]
    [InlineData(CandidateSelectionSolveStatus.Unknown)]
    [InlineData(CandidateSelectionSolveStatus.Infeasible)]
    [InlineData(CandidateSelectionSolveStatus.ModelInvalid)]
    public void Solver_without_solution_uses_independently_validated_no_op(
        CandidateSelectionSolveStatus primaryStatus)
    {
        var problem = Problem();
        var budget = Budget();
        var solver = new StubSolver(
            (_, solverBudget) => NoSolution(
                problem,
                solverBudget,
                primaryStatus));

        var result = new SafeCandidateSelectionExecutor(
            solver,
            new StubValidator(_ => true)).Execute(
                problem,
                budget,
                Accounting(budget));

        Assert.Equal(
            CandidateSelectionSolveStatus.SafeFallback,
            result.SolveResult.Status);
        Assert.Equal(
            CandidateSelectionExecutionPath.SafeNoOp,
            result.Diagnostics.ExecutionPath);
        Assert.Equal(["v1-noop", "v2-noop"], result.SolveResult.Solution!.SelectedOptionIds);
        Assert.Empty(result.SolveResult.Diagnostics.ObjectiveBounds);
        Assert.Equal(primaryStatus, result.Diagnostics.PrimarySolveStatus);
        Assert.Equal(7, result.Diagnostics.PrimarySolverDiagnostics.ConsumedWorkUnits);
    }

    [Fact]
    public void Rejected_primary_incumbent_cannot_bypass_no_op_validation()
    {
        var problem = Problem();
        var budget = Budget();
        var incumbent = Solution(problem, "v1-accept", "v2-noop");
        var solver = new StubSolver(
            (_, solverBudget) => CandidateSelectionSolveResult.Feasible(
                incumbent,
                Diagnostics(problem, solverBudget)));
        var validator = new StubValidator(
            solution => solution.SelectedOptionIds.All(id => id.EndsWith("noop", StringComparison.Ordinal)));

        var result = new SafeCandidateSelectionExecutor(solver, validator).Execute(
            problem,
            budget,
            Accounting(budget));

        Assert.Equal(CandidateSelectionSolveStatus.SafeFallback, result.SolveResult.Status);
        Assert.Equal(["v1-noop", "v2-noop"], result.SolveResult.Solution!.SelectedOptionIds);
        Assert.True(result.Diagnostics.PrimaryIncumbentRejected);
        Assert.Equal(2, result.Diagnostics.ConsumedValidationWorkUnits);
        Assert.Equal(1, result.Diagnostics.FallbackValidationAttempts);
        var witness = Assert.Single(result.Diagnostics.ValidationWitnesses);
        Assert.Equal(
            CandidateSelectionExecutionPath.ValidatedIncumbent,
            witness.AttemptedPath);
        Assert.Equal(["v1-accept", "v2-noop"], witness.SelectedOptionIds);
    }

    [Fact]
    public void Greedy_single_request_fallback_uses_lexicographic_then_id_order()
    {
        var problem = Problem();
        var budget = Budget();
        var solver = new StubSolver(
            (_, solverBudget) => NoSolution(
                problem,
                solverBudget,
                CandidateSelectionSolveStatus.Unknown));
        var validator = new StubValidator(
            solution => solution.SelectedOptionIds.Contains(
                "v1-accept",
                StringComparer.Ordinal));

        var result = new SafeCandidateSelectionExecutor(solver, validator).Execute(
            problem,
            budget,
            Accounting(budget));

        Assert.Equal(CandidateSelectionSolveStatus.SafeFallback, result.SolveResult.Status);
        Assert.Equal(
            CandidateSelectionExecutionPath.GreedySingleRequest,
            result.Diagnostics.ExecutionPath);
        Assert.Equal(["v1-accept", "v2-noop"], result.SolveResult.Solution!.SelectedOptionIds);
        Assert.Equal(2, result.Diagnostics.FallbackValidationAttempts);
    }

    [Fact]
    public void Validation_budget_exhaustion_returns_unknown_without_solution()
    {
        var problem = Problem();
        var budget = Budget(maximumValidationWorkUnits: 2);
        var incumbent = Solution(problem, "v1-accept", "v2-noop");
        var solver = new StubSolver(
            (_, solverBudget) => CandidateSelectionSolveResult.Feasible(
                incumbent,
                Diagnostics(problem, solverBudget)));
        var accounting = CandidateSelectionPreSolveAccounting.Create(
            budget,
            consumedGenerationWorkUnits: 4,
            consumedValidationWorkUnits: 2,
            omittedCandidateCount: 0).Value!;
        var validator = new StubValidator(_ => true);

        var result = new SafeCandidateSelectionExecutor(solver, validator).Execute(
            problem,
            budget,
            accounting);

        Assert.Equal(CandidateSelectionSolveStatus.Unknown, result.SolveResult.Status);
        Assert.Null(result.SolveResult.Solution);
        Assert.Equal(
            CandidateSelectionFailureCodes.ValidationBudgetExhausted,
            result.SolveResult.ReasonCode);
        Assert.Equal(0, validator.CallCount);
    }

    [Fact]
    public void Exhausted_fallback_portfolio_does_not_fabricate_incident_recovery()
    {
        var problem = Problem();
        var budget = Budget();
        var solver = new StubSolver(
            (_, solverBudget) => NoSolution(
                problem,
                solverBudget,
                CandidateSelectionSolveStatus.Unknown));

        var result = new SafeCandidateSelectionExecutor(
            solver,
            new StubValidator(_ => false)).Execute(
                problem,
                budget,
                Accounting(budget));

        Assert.Equal(CandidateSelectionSolveStatus.Unknown, result.SolveResult.Status);
        Assert.Null(result.SolveResult.Solution);
        Assert.Equal(
            CandidateSelectionFailureCodes.NoValidatedFallback,
            result.SolveResult.ReasonCode);
        Assert.Equal(CandidateSelectionExecutionPath.None, result.Diagnostics.ExecutionPath);
        Assert.Equal(3, result.Diagnostics.FallbackValidationAttempts);
    }

    [Fact]
    public void Candidate_loss_and_solver_loss_are_reported_separately()
    {
        var problem = Problem();
        var budget = Budget();
        var solver = new StubSolver(
            (_, solverBudget) => NoSolution(
                problem,
                solverBudget,
                CandidateSelectionSolveStatus.Unknown));
        var accounting = CandidateSelectionPreSolveAccounting.Create(
            budget,
            consumedGenerationWorkUnits: 8,
            consumedValidationWorkUnits: 1,
            omittedCandidateCount: 3,
            omissionDigest: new string('a', 64),
            omissionCountWasSaturated: true).Value!;

        var result = new SafeCandidateSelectionExecutor(
            solver,
            new StubValidator(_ => true)).Execute(
                problem,
                budget,
                accounting);

        Assert.True(result.Diagnostics.CandidateLossOccurred);
        Assert.True(result.Diagnostics.SolverLossOccurred);
        Assert.Equal(3, result.Diagnostics.OmittedCandidateCount);
        Assert.Equal(new string('a', 64), result.Diagnostics.OmissionDigest);
        Assert.True(result.Diagnostics.OmissionCountWasSaturated);
        Assert.Equal(8, result.Diagnostics.ConsumedGenerationWorkUnits);
        Assert.Equal(2, result.Diagnostics.ConsumedValidationWorkUnits);
    }

    [Fact]
    public void Pre_solve_accounting_rejects_cross_stage_overrun_and_undigested_loss()
    {
        var budget = Budget();

        var generationOverrun = CandidateSelectionPreSolveAccounting.Create(
            budget,
            consumedGenerationWorkUnits: 101,
            consumedValidationWorkUnits: 0,
            omittedCandidateCount: 0);
        var missingDigest = CandidateSelectionPreSolveAccounting.Create(
            budget,
            consumedGenerationWorkUnits: 1,
            consumedValidationWorkUnits: 1,
            omittedCandidateCount: 1);

        Assert.Equal(
            CandidateSelectionFailureCodes.InvalidExecutionAccounting,
            generationOverrun.Failure?.Code);
        Assert.Equal(
            CandidateSelectionFailureCodes.InvalidExecutionAccounting,
            missingDigest.Failure?.Code);
    }

    [Fact]
    public void Accounting_created_for_a_larger_budget_cannot_overrun_this_execution()
    {
        var problem = Problem();
        var larger = DeterministicCandidateSelectionExecutionBudget.Create(
            maximumGenerationWorkUnits: 200,
            maximumValidationWorkUnits: 200,
            Budget().SolverBudget).Value!;
        var accounting = CandidateSelectionPreSolveAccounting.Create(
            larger,
            consumedGenerationWorkUnits: 150,
            consumedValidationWorkUnits: 0,
            omittedCandidateCount: 0).Value!;
        var executor = new SafeCandidateSelectionExecutor(
            new StubSolver(
                (_, solverBudget) => NoSolution(
                    problem,
                    solverBudget,
                    CandidateSelectionSolveStatus.Unknown)),
            new StubValidator(_ => true));

        Assert.Throws<ArgumentException>(
            () => executor.Execute(problem, Budget(), accounting));
    }

    private static CandidateSelectionProblem Problem() =>
        CandidateSelectionProblem.Create(
            [new VehicleId("v-2"), new VehicleId("v-1")],
            [new RequestId("r-1")],
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
                new CandidateSelectionOption(
                    "v2-noop",
                    new VehicleId("v-2"),
                    [],
                    [0, 0],
                    true),
                new CandidateSelectionOption(
                    "v2-accept",
                    new VehicleId("v-2"),
                    [new RequestId("r-1")],
                    [1, 20],
                    false),
                new CandidateSelectionOption(
                    "v1-noop",
                    new VehicleId("v-1"),
                    [],
                    [0, 0],
                    true),
                new CandidateSelectionOption(
                    "v1-accept",
                    new VehicleId("v-1"),
                    [new RequestId("r-1")],
                    [1, 10],
                    false),
            ]).Value!;

    private static DeterministicCandidateSelectionExecutionBudget Budget(
        long maximumValidationWorkUnits = 100) =>
        DeterministicCandidateSelectionExecutionBudget.Create(
            maximumGenerationWorkUnits: 100,
            maximumValidationWorkUnits,
            DeterministicSolverBudget.Create(
                maximumWorkUnits: 50,
                maximumDeterministicTimeMicros: 1_000,
                randomSeed: 7).Value!).Value!;

    private static CandidateSelectionPreSolveAccounting Accounting(
        DeterministicCandidateSelectionExecutionBudget budget) =>
        CandidateSelectionPreSolveAccounting.Create(
            budget,
            consumedGenerationWorkUnits: 4,
            consumedValidationWorkUnits: 0,
            omittedCandidateCount: 0).Value!;

    private static CandidateSelectionSolution Solution(
        CandidateSelectionProblem problem,
        params string[] optionIds) =>
        CandidateSelectionSolution.Create(problem, optionIds).Value!;

    private static CandidateSelectionSolveResult Optimal(
        CandidateSelectionProblem problem,
        CandidateSelectionSolution solution,
        DeterministicSolverBudget budget)
    {
        var bounds = problem.ObjectiveLevels
            .Select(
                (level, index) => ObjectiveSolveBound.Create(
                    index,
                    level,
                    solution.ObjectiveValues[index],
                    solution.ObjectiveValues[index]).Value!)
            .ToArray();
        return CandidateSelectionSolveResult.Optimal(
            solution,
            Diagnostics(problem, budget, bounds));
    }

    private static CandidateSelectionSolveResult NoSolution(
        CandidateSelectionProblem problem,
        DeterministicSolverBudget budget,
        CandidateSelectionSolveStatus status)
    {
        var diagnostics = Diagnostics(problem, budget);
        return status switch
        {
            CandidateSelectionSolveStatus.Unknown =>
                CandidateSelectionSolveResult.Unknown(
                    diagnostics,
                    "TEST_UNKNOWN",
                    "Test solver did not conclude."),
            CandidateSelectionSolveStatus.Infeasible =>
                CandidateSelectionSolveResult.Infeasible(
                    diagnostics,
                    "TEST_INFEASIBLE",
                    "Test solver reported infeasible."),
            CandidateSelectionSolveStatus.ModelInvalid =>
                CandidateSelectionSolveResult.ModelInvalid(
                    diagnostics,
                    "TEST_MODEL_INVALID",
                    "Test solver rejected the model."),
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };
    }

    private static CandidateSelectionSolverDiagnostics Diagnostics(
        CandidateSelectionProblem problem,
        DeterministicSolverBudget budget,
        IReadOnlyList<ObjectiveSolveBound>? bounds = null) =>
        CandidateSelectionSolverDiagnostics.Create(
            problem,
            budget,
            consumedWorkUnits: 7,
            consumedDeterministicTimeMicros: 11,
            wallTimeMilliseconds: 13,
            bounds ?? []).Value!;

    private sealed class StubSolver(
        Func<CandidateSelectionProblem, DeterministicSolverBudget,
            CandidateSelectionSolveResult> solve) : ICandidateSelectionSolver
    {
        public CandidateSelectionSolveResult Solve(
            CandidateSelectionProblem problem,
            DeterministicSolverBudget budget) => solve(problem, budget);
    }

    private sealed class StubValidator(
        Func<CandidateSelectionSolution, bool> validate) :
        ICandidateSelectionSolutionValidator
    {
        public int CallCount { get; private set; }

        public CandidateSelectionValidationResult Validate(
            CandidateSelectionProblem problem,
            CandidateSelectionSolution solution)
        {
            CallCount++;
            return validate(solution)
                ? CandidateSelectionValidationResult.Valid()
                : CandidateSelectionValidationResult.Invalid(
                    "TEST_SEMANTIC_REJECTION",
                    "The test semantic validator rejected the solution.");
        }
    }
}
