using System.Diagnostics;
using System.Globalization;
using Google.OrTools.Sat;
using RideBound.Application.Optimization;
using RideBound.Domain.Common;

namespace RideBound.Solvers.OrTools;

public sealed class OrToolsCandidateSelectionSolver : ICandidateSelectionSolver
{
    public const string AdapterVersion = "google-ortools-9.15.6755";

    public CandidateSelectionSolveResult Solve(
        CandidateSelectionProblem problem,
        DeterministicSolverBudget budget)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(budget);
        var stopwatch = Stopwatch.StartNew();
        CandidateSelectionSolution? incumbent = null;
        var bounds = new List<ObjectiveSolveBound>();
        var fixedBounds = new List<ObjectiveSolveBound>();
        long consumedWork = 0;
        long consumedDeterministicMicros = 0;
        var constants = budget.SkipConstantObjectiveLevels
            ? ConstantsToSkip(problem)
            : null;
        var skippedLevels = 0;

        for (var levelIndex = 0;
             levelIndex < problem.ObjectiveLevels.Count;
             levelIndex++)
        {
            if (constants?[levelIndex] is long constantValue)
            {
                // The level is the same number for every feasible assignment, so
                // the pass would only prove the optimum of a constant and the
                // equality it contributes to later passes is vacuous. Report the
                // exact optimum and do not build a model. The bound is not added
                // to the fixed set precisely because it constrains nothing.
                var constantBound = ObjectiveSolveBound.Create(
                    levelIndex,
                    problem.ObjectiveLevels[levelIndex],
                    constantValue,
                    constantValue);

                if (!constantBound.IsSuccess)
                {
                    return NoSolution(
                        problem,
                        budget,
                        stopwatch,
                        CandidateSelectionSolveStatus.ModelInvalid,
                        "ORTOOLS_BOUND_INVALID",
                        constantBound.Failure!.Message,
                        consumedWork,
                        consumedDeterministicMicros,
                        bounds);
                }

                bounds.Add(constantBound.Value!);
                skippedLevels++;
                continue;
            }

            var remainingWork = budget.MaximumWorkUnits - consumedWork;
            var remainingMicros = budget.MaximumDeterministicTimeMicros
                - consumedDeterministicMicros;

            if (remainingWork == 0 || remainingMicros == 0)
            {
                return incumbent is null
                    ? NoSolution(
                        problem,
                        budget,
                        stopwatch,
                        CandidateSelectionSolveStatus.Unknown,
                        "ORTOOLS_BUDGET_EXHAUSTED",
                        "The deterministic budget was exhausted before an incumbent.",
                        consumedWork,
                        consumedDeterministicMicros,
                        bounds)
                    : WithIncumbent(
                        problem,
                        budget,
                        stopwatch,
                        incumbent,
                        consumedWork,
                        consumedDeterministicMicros,
                        bounds,
                        "ORTOOLS_BUDGET_EXHAUSTED",
                        "The deterministic budget ended before every lexicographic " +
                        "level was proven optimal.");
            }

            var built = BuildModel(problem, fixedBounds);

            if (built.Error is not null)
            {
                return NoSolution(
                    problem,
                    budget,
                    stopwatch,
                    CandidateSelectionSolveStatus.ModelInvalid,
                    "ORTOOLS_MODEL_INVALID",
                    built.Error,
                    consumedWork,
                    consumedDeterministicMicros,
                    bounds);
            }

            var context = built.Context!;
            var level = problem.ObjectiveLevels[levelIndex];
            var expression = context.Objectives[levelIndex];

            if (level.Sense == CandidateSelectionObjectiveSense.Maximize)
            {
                context.Model.Maximize(expression);
            }
            else
            {
                context.Model.Minimize(expression);
            }

            using var solver = new CpSolver
            {
                StringParameters = Parameters(
                    budget.RandomSeed,
                    remainingWork,
                    remainingMicros),
            };
            var status = solver.Solve(context.Model);
            var response = solver.Response;

            if (response is null)
            {
                return NoSolution(
                    problem,
                    budget,
                    stopwatch,
                    CandidateSelectionSolveStatus.ModelInvalid,
                    "ORTOOLS_RESPONSE_MISSING",
                    "CP-SAT returned no response object.",
                    consumedWork,
                    consumedDeterministicMicros,
                    bounds);
            }

            var passWork = Math.Min(remainingWork, response.NumConflicts);
            var passMicros = DeterministicMicros(
                response.DeterministicTime,
                remainingMicros);
            consumedWork += passWork;
            consumedDeterministicMicros += passMicros;

            if (status == CpSolverStatus.ModelInvalid)
            {
                return NoSolution(
                    problem,
                    budget,
                    stopwatch,
                    CandidateSelectionSolveStatus.ModelInvalid,
                    "ORTOOLS_MODEL_INVALID",
                    context.Model.Validate(),
                    consumedWork,
                    consumedDeterministicMicros,
                    bounds);
            }

            if (status == CpSolverStatus.Infeasible)
            {
                return NoSolution(
                    problem,
                    budget,
                    stopwatch,
                    levelIndex == 0
                        ? CandidateSelectionSolveStatus.Infeasible
                        : CandidateSelectionSolveStatus.ModelInvalid,
                    levelIndex == 0
                        ? "ORTOOLS_INFEASIBLE"
                        : "ORTOOLS_LEXICOGRAPHIC_CONTRADICTION",
                    levelIndex == 0
                        ? "CP-SAT proved the candidate-selection model infeasible."
                        : "A later lexicographic pass contradicted a previously " +
                            "proven objective equality.",
                    consumedWork,
                    consumedDeterministicMicros,
                    bounds);
            }

            if (status == CpSolverStatus.Unknown)
            {
                return incumbent is null
                    ? NoSolution(
                        problem,
                        budget,
                        stopwatch,
                        CandidateSelectionSolveStatus.Unknown,
                        "ORTOOLS_UNKNOWN",
                        "CP-SAT returned UNKNOWN without a validated incumbent.",
                        consumedWork,
                        consumedDeterministicMicros,
                        bounds)
                    : WithIncumbent(
                        problem,
                        budget,
                        stopwatch,
                        incumbent,
                        consumedWork,
                        consumedDeterministicMicros,
                        bounds,
                        "ORTOOLS_UNKNOWN",
                        "CP-SAT returned UNKNOWN before all lexicographic levels " +
                        "were proven.");
            }

            if (status is not CpSolverStatus.Feasible
                and not CpSolverStatus.Optimal)
            {
                return NoSolution(
                    problem,
                    budget,
                    stopwatch,
                    CandidateSelectionSolveStatus.ModelInvalid,
                    "ORTOOLS_STATUS_INVALID",
                    "CP-SAT returned an unsupported status.",
                    consumedWork,
                    consumedDeterministicMicros,
                    bounds);
            }

            var selectedIds = context.Variables
                .Where(value => solver.BooleanValue(value.Value))
                .Select(value => value.Key)
                .ToArray();
            var created = CandidateSelectionSolution.Create(problem, selectedIds);

            if (!created.IsSuccess)
            {
                return NoSolution(
                    problem,
                    budget,
                    stopwatch,
                    CandidateSelectionSolveStatus.ModelInvalid,
                    "ORTOOLS_SOLUTION_INVALID",
                    created.Failure!.Message,
                    consumedWork,
                    consumedDeterministicMicros,
                    bounds);
            }

            incumbent = created.Value!;
            var incumbentValue = incumbent.ObjectiveValues[levelIndex];
            var bestBound = status == CpSolverStatus.Optimal
                ? incumbentValue
                : ConvertBestBound(
                    solver.BestObjectiveBound,
                    incumbentValue,
                    level.Sense);
            var bound = ObjectiveSolveBound.Create(
                levelIndex,
                level,
                incumbentValue,
                bestBound);

            if (!bound.IsSuccess)
            {
                return NoSolution(
                    problem,
                    budget,
                    stopwatch,
                    CandidateSelectionSolveStatus.ModelInvalid,
                    "ORTOOLS_BOUND_INVALID",
                    bound.Failure!.Message,
                    consumedWork,
                    consumedDeterministicMicros,
                    bounds);
            }

            bounds.Add(bound.Value!);
            fixedBounds.Add(bound.Value!);

            if (status == CpSolverStatus.Feasible)
            {
                return WithIncumbent(
                    problem,
                    budget,
                    stopwatch,
                    incumbent,
                    consumedWork,
                    consumedDeterministicMicros,
                    bounds,
                    "ORTOOLS_FEASIBLE_NOT_PROVEN",
                    "CP-SAT found a validated incumbent but did not prove the " +
                    "current lexicographic level optimal.");
            }
        }

        var diagnostics = Diagnostics(
            problem,
            budget,
            stopwatch,
            consumedWork,
            consumedDeterministicMicros,
            bounds,
            skippedLevels == 0
                ? "ORTOOLS_OPTIMAL"
                : "ORTOOLS_OPTIMAL_CONSTANT_LEVELS_SKIPPED",
            skippedLevels == 0
                ? AdapterVersion
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}; {1} of {2} lexicographic levels were constant over the " +
                    "feasible set and were reported without a solve.",
                    AdapterVersion,
                    skippedLevels,
                    problem.ObjectiveLevels.Count));
        return CandidateSelectionSolveResult.Optimal(incumbent!, diagnostics);
    }

    /// <summary>
    /// Returns the constant value per level, or <c>null</c> where the level must
    /// still be solved. At least one level is always left to solve so the pass
    /// loop still produces an incumbent assignment.
    /// </summary>
    private static IReadOnlyList<long?> ConstantsToSkip(
        CandidateSelectionProblem problem)
    {
        var constants = problem.ConstantObjectiveLevelValues().ToArray();

        if (constants.All(value => value is not null))
        {
            constants[0] = null;
        }

        return constants;
    }

    private static BuildResult BuildModel(
        CandidateSelectionProblem problem,
        IReadOnlyList<ObjectiveSolveBound> fixedBounds)
    {
        var model = new CpModel();
        var variables = problem.Options.ToDictionary(
            option => option.OptionId,
            option => model.NewBoolVar(option.OptionId),
            StringComparer.Ordinal);

        foreach (var vehicle in problem.VehicleIds)
        {
            model.Add(
                LinearExpr.Sum(
                    problem.Options
                        .Where(option => option.VehicleId == vehicle)
                        .Select(option => variables[option.OptionId])) == 1);
        }

        foreach (var request in problem.RequestIds)
        {
            model.Add(
                LinearExpr.Sum(
                    problem.Options
                        .Where(option => option.RequestIds.Contains(request))
                        .Select(option => variables[option.OptionId])) <= 1);
        }

        // G2 coupling, added only when relocation options exist: a rider's donors and
        // receivers are selected together, and at most one rider moves.
        var receivers = problem.Options
            .Where(option => option.RelocationIn is not null)
            .ToArray();

        if (receivers.Length != 0
            || problem.Options.Any(option => option.RelocationOut is not null))
        {
            foreach (var rider in problem.Options
                         .Select(option => option.RelocationOut ?? option.RelocationIn)
                         .OfType<RequestId>()
                         .Distinct()
                         .OrderBy(value => value.Value, StringComparer.Ordinal))
            {
                model.Add(
                    LinearExpr.Sum(
                        problem.Options
                            .Where(option => option.RelocationOut == rider)
                            .Select(option => variables[option.OptionId]))
                    == LinearExpr.Sum(
                        problem.Options
                            .Where(option => option.RelocationIn == rider)
                            .Select(option => variables[option.OptionId])));
            }

            model.Add(
                LinearExpr.Sum(receivers.Select(option => variables[option.OptionId])) <= 1);
        }

        var objectives = new List<LinearExpr>(problem.ObjectiveLevels.Count);

        for (var levelIndex = 0;
             levelIndex < problem.ObjectiveLevels.Count;
             levelIndex++)
        {
            var level = problem.ObjectiveLevels[levelIndex];
            var upperBound = ObjectiveUpperBound(problem, levelIndex);

            if (upperBound is null)
            {
                return BuildResult.Failure(
                    $"Objective '{level.Name}' can exceed the canonical range.");
            }

            if (level.Aggregation == CandidateSelectionObjectiveAggregation.Sum)
            {
                objectives.Add(
                    LinearExpr.WeightedSum(
                        problem.Options.Select(
                            option => variables[option.OptionId]),
                        problem.Options.Select(
                            option => option.ObjectiveContributions[levelIndex])));
                continue;
            }

            var maximum = model.NewIntVar(
                0,
                upperBound.Value,
                $"objective-max-{levelIndex}");
            model.AddMaxEquality(
                maximum,
                problem.Options.Select(
                    option => LinearExpr.Term(
                        variables[option.OptionId],
                        option.ObjectiveContributions[levelIndex])));
            objectives.Add(maximum);
        }

        foreach (var bound in fixedBounds)
        {
            model.Add(objectives[bound.LevelIndex] == bound.IncumbentValue);
        }

        var validation = model.Validate();
        return string.IsNullOrEmpty(validation)
            ? BuildResult.Success(
                new ModelContext(model, variables, objectives.AsReadOnly()))
            : BuildResult.Failure(validation);
    }

    private static long? ObjectiveUpperBound(
        CandidateSelectionProblem problem,
        int levelIndex)
    {
        if (problem.ObjectiveLevels[levelIndex].Aggregation
            == CandidateSelectionObjectiveAggregation.Maximum)
        {
            return problem.Options.Max(
                option => option.ObjectiveContributions[levelIndex]);
        }

        long total = 0;

        foreach (var vehicle in problem.VehicleIds)
        {
            var maximum = problem.Options
                .Where(option => option.VehicleId == vehicle)
                .Max(option => option.ObjectiveContributions[levelIndex]);

            if (total > DomainLimits.MaxCanonicalInteger - maximum)
            {
                return null;
            }

            total += maximum;
        }

        return total;
    }

    private static string Parameters(
        long seed,
        long maximumConflicts,
        long maximumDeterministicMicros) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "num_search_workers:1 random_seed:{0} " +
            "max_number_of_conflicts:{1} max_deterministic_time:{2:R} " +
            "log_search_progress:false",
            seed,
            maximumConflicts,
            maximumDeterministicMicros / 1_000_000d);

    private static long DeterministicMicros(
        double deterministicSeconds,
        long remainingMicros)
    {
        if (!double.IsFinite(deterministicSeconds) || deterministicSeconds < 0)
        {
            return remainingMicros;
        }

        var micros = Math.Ceiling(deterministicSeconds * 1_000_000d);
        return micros >= remainingMicros
            ? remainingMicros
            : checked((long)micros);
    }

    private static long ConvertBestBound(
        double value,
        long incumbent,
        CandidateSelectionObjectiveSense sense)
    {
        if (!double.IsFinite(value))
        {
            return sense == CandidateSelectionObjectiveSense.Minimize
                ? 0
                : DomainLimits.MaxCanonicalInteger;
        }

        var integral = sense == CandidateSelectionObjectiveSense.Minimize
            ? Math.Ceiling(value)
            : Math.Floor(value);
        var canonical = integral <= 0
            ? 0
            : integral >= DomainLimits.MaxCanonicalInteger
                ? DomainLimits.MaxCanonicalInteger
                : checked((long)integral);
        return sense == CandidateSelectionObjectiveSense.Minimize
            ? Math.Min(canonical, incumbent)
            : Math.Max(canonical, incumbent);
    }

    private static CandidateSelectionSolveResult WithIncumbent(
        CandidateSelectionProblem problem,
        DeterministicSolverBudget budget,
        Stopwatch stopwatch,
        CandidateSelectionSolution incumbent,
        long work,
        long deterministicMicros,
        IReadOnlyList<ObjectiveSolveBound> bounds,
        string reasonCode,
        string message) =>
        CandidateSelectionSolveResult.Feasible(
            incumbent,
            Diagnostics(
                problem,
                budget,
                stopwatch,
                work,
                deterministicMicros,
                bounds,
                reasonCode,
                message));

    private static CandidateSelectionSolveResult NoSolution(
        CandidateSelectionProblem problem,
        DeterministicSolverBudget budget,
        Stopwatch stopwatch,
        CandidateSelectionSolveStatus status,
        string reasonCode,
        string message,
        long work,
        long deterministicMicros,
        IReadOnlyList<ObjectiveSolveBound> bounds)
    {
        var diagnostics = Diagnostics(
            problem,
            budget,
            stopwatch,
            work,
            deterministicMicros,
            bounds,
            reasonCode,
            message);
        return status switch
        {
            CandidateSelectionSolveStatus.Infeasible =>
                CandidateSelectionSolveResult.Infeasible(
                    diagnostics,
                    reasonCode,
                    message),
            CandidateSelectionSolveStatus.Unknown =>
                CandidateSelectionSolveResult.Unknown(
                    diagnostics,
                    reasonCode,
                    message),
            CandidateSelectionSolveStatus.ModelInvalid =>
                CandidateSelectionSolveResult.ModelInvalid(
                    diagnostics,
                    reasonCode,
                    message),
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };
    }

    private static CandidateSelectionSolverDiagnostics Diagnostics(
        CandidateSelectionProblem problem,
        DeterministicSolverBudget budget,
        Stopwatch stopwatch,
        long work,
        long deterministicMicros,
        IEnumerable<ObjectiveSolveBound> bounds,
        string detailCode,
        string detail)
    {
        stopwatch.Stop();
        var wall = Math.Min(
            DomainLimits.MaxCanonicalInteger,
            stopwatch.ElapsedMilliseconds);
        var created = CandidateSelectionSolverDiagnostics.Create(
            problem,
            budget,
            work,
            deterministicMicros,
            wall,
            bounds,
            detailCode,
            detail);

        if (!created.IsSuccess)
        {
            throw new InvalidOperationException(created.Failure!.Message);
        }

        return created.Value!;
    }

    private sealed record ModelContext(
        CpModel Model,
        IReadOnlyDictionary<string, BoolVar> Variables,
        IReadOnlyList<LinearExpr> Objectives);

    private sealed record BuildResult(ModelContext? Context, string? Error)
    {
        public static BuildResult Success(ModelContext context) =>
            new(context, null);

        public static BuildResult Failure(string error) =>
            new(null, error);
    }
}
