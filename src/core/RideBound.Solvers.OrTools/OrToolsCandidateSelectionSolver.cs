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
        ModelContext? context = null;
        var boundsInModel = 0;

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
                    ? Fail(
                        CandidateSelectionSolveStatus.Unknown,
                        "ORTOOLS_BUDGET_EXHAUSTED",
                        "The deterministic budget was exhausted before an incumbent.")
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

            // The first pass builds the model; each later pass only appends the equalities of the
            // levels proven since, which yields the model a fresh build would (ADR-089).
            var built = context is null
                ? BuildModel(problem, new ModelIndex(problem), fixedBounds)
                : ExtendModel(context, fixedBounds, boundsInModel);

            if (built.Error is not null)
            {
                return Fail(
                    CandidateSelectionSolveStatus.ModelInvalid,
                    "ORTOOLS_MODEL_INVALID",
                    built.Error);
            }

            context = built.Context!;
            boundsInModel = fixedBounds.Count;
            var level = problem.ObjectiveLevels[levelIndex];
            var expression = context.Objective(levelIndex);

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
                return Fail(
                    CandidateSelectionSolveStatus.ModelInvalid,
                    "ORTOOLS_RESPONSE_MISSING",
                    "CP-SAT returned no response object.");
            }

            var passWork = Math.Min(remainingWork, response.NumConflicts);
            var passMicros = DeterministicMicros(
                response.DeterministicTime,
                remainingMicros);
            consumedWork += passWork;
            consumedDeterministicMicros += passMicros;

            if (status == CpSolverStatus.ModelInvalid)
            {
                return Fail(
                    CandidateSelectionSolveStatus.ModelInvalid,
                    "ORTOOLS_MODEL_INVALID",
                    context.Model.Validate());
            }

            if (status == CpSolverStatus.Infeasible)
            {
                return Fail(
                    levelIndex == 0
                        ? CandidateSelectionSolveStatus.Infeasible
                        : CandidateSelectionSolveStatus.ModelInvalid,
                    levelIndex == 0
                        ? "ORTOOLS_INFEASIBLE"
                        : "ORTOOLS_LEXICOGRAPHIC_CONTRADICTION",
                    levelIndex == 0
                        ? "CP-SAT proved the candidate-selection model infeasible."
                        : "A later lexicographic pass contradicted a previously " +
                            "proven objective equality.");
            }

            if (status == CpSolverStatus.Unknown)
            {
                return incumbent is null
                    ? Fail(
                        CandidateSelectionSolveStatus.Unknown,
                        "ORTOOLS_UNKNOWN",
                        "CP-SAT returned UNKNOWN without a validated incumbent.")
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
                return Fail(
                    CandidateSelectionSolveStatus.ModelInvalid,
                    "ORTOOLS_STATUS_INVALID",
                    "CP-SAT returned an unsupported status.");
            }

            var selectedIds = context.Variables
                .Where(value => solver.BooleanValue(value.Value))
                .Select(value => value.Key)
                .ToArray();
            var created = CandidateSelectionSolution.Create(problem, selectedIds);

            if (!created.IsSuccess)
            {
                return Fail(
                    CandidateSelectionSolveStatus.ModelInvalid,
                    "ORTOOLS_SOLUTION_INVALID",
                    created.Failure!.Message);
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
                return Fail(
                    CandidateSelectionSolveStatus.ModelInvalid,
                    "ORTOOLS_BOUND_INVALID",
                    bound.Failure!.Message);
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

        // Ends the solve without a solution, reporting the work and bounds counted so far.
        CandidateSelectionSolveResult Fail(
            CandidateSelectionSolveStatus status,
            string reasonCode,
            string message) =>
            NoSolution(
                problem,
                budget,
                stopwatch,
                status,
                reasonCode,
                message,
                consumedWork,
                consumedDeterministicMicros,
                bounds);
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

    /// <summary>
    /// Builds the model of one lexicographic pass. The model is the same, variable for variable
    /// and constraint for constraint, as the archived builder's (ADR-089; the model-identity
    /// tests compare the CP-SAT protos byte for byte): the lookups come from
    /// <paramref name="index"/> instead of a scan of every option per vehicle, request and level,
    /// and a Sum objective is created only when a pass uses it, because creating an expression
    /// does not change the model.
    /// </summary>
    internal static BuildResult BuildModel(
        CandidateSelectionProblem problem,
        ModelIndex index,
        IReadOnlyList<ObjectiveSolveBound> fixedBounds)
    {
        var model = new CpModel();
        var options = problem.Options;
        var variables = new Dictionary<string, BoolVar>(options.Count, StringComparer.Ordinal);
        var literals = new BoolVar[options.Count];

        for (var optionIndex = 0; optionIndex < options.Count; optionIndex++)
        {
            var variable = model.NewBoolVar(options[optionIndex].OptionId);
            variables.Add(options[optionIndex].OptionId, variable);
            literals[optionIndex] = variable;
        }

        foreach (var vehicleOptions in index.VehicleOptions)
        {
            model.Add(LinearExpr.Sum(vehicleOptions.Select(option => literals[option])) == 1);
        }

        foreach (var requestOptions in index.RequestOptions)
        {
            model.Add(LinearExpr.Sum(requestOptions.Select(option => literals[option])) <= 1);
        }

        // G2 coupling, added only when relocation options exist: a rider's donors and
        // receivers are selected together, and at most one rider moves.
        if (index.HasRelocation)
        {
            foreach (var (donors, receivers) in index.RelocationRiders)
            {
                model.Add(
                    LinearExpr.Sum(donors.Select(option => literals[option]))
                    == LinearExpr.Sum(receivers.Select(option => literals[option])));
            }

            model.Add(
                LinearExpr.Sum(index.Receivers.Select(option => literals[option])) <= 1);
        }

        var objectives = new LinearExpr?[problem.ObjectiveLevels.Count];

        for (var levelIndex = 0;
             levelIndex < problem.ObjectiveLevels.Count;
             levelIndex++)
        {
            var level = problem.ObjectiveLevels[levelIndex];
            var upperBound = index.UpperBounds[levelIndex];

            if (upperBound is null)
            {
                return BuildResult.Failure(
                    $"Objective '{level.Name}' can exceed the canonical range.");
            }

            if (level.Aggregation == CandidateSelectionObjectiveAggregation.Sum)
            {
                continue;
            }

            var maximum = model.NewIntVar(
                0,
                upperBound.Value,
                $"objective-max-{levelIndex}");
            model.AddMaxEquality(
                maximum,
                options.Select(
                    (option, optionIndex) => LinearExpr.Term(
                        literals[optionIndex],
                        option.ObjectiveContributions[levelIndex])));
            objectives[levelIndex] = maximum;
        }

        var context = new ModelContext(model, variables, literals, options, objectives);

        foreach (var bound in fixedBounds)
        {
            model.Add(context.Objective(bound.LevelIndex) == bound.IncumbentValue);
        }

        var validation = model.Validate();
        return string.IsNullOrEmpty(validation)
            ? BuildResult.Success(context)
            : BuildResult.Failure(validation);
    }

    /// <summary>
    /// Brings the previous pass's model to the next pass: clears the objective and appends the
    /// equalities of the bounds proven since, in order. A fresh build adds the same equalities
    /// after the same base constraints, so the model is identical (ADR-089).
    /// </summary>
    internal static BuildResult ExtendModel(
        ModelContext context,
        IReadOnlyList<ObjectiveSolveBound> fixedBounds,
        int boundsInModel)
    {
        // CpModel.ClearObjective is not public in this package; clearing both proto fields is
        // what it does, and the model-identity tests check the result byte for byte.
        context.Model.Model.Objective = null;
        context.Model.Model.FloatingPointObjective = null;

        for (var boundIndex = boundsInModel; boundIndex < fixedBounds.Count; boundIndex++)
        {
            var bound = fixedBounds[boundIndex];
            context.Model.Add(context.Objective(bound.LevelIndex) == bound.IncumbentValue);
        }

        var validation = context.Model.Validate();
        return string.IsNullOrEmpty(validation)
            ? BuildResult.Success(context)
            : BuildResult.Failure(validation);
    }

    /// <summary>
    /// Per-problem lookups for <see cref="BuildModel"/>, computed once per solve. Every list keeps
    /// option order, and vehicles, requests and relocation riders keep the order the archived
    /// builder visited them in, so each constraint has the same terms in the same order.
    /// </summary>
    internal sealed class ModelIndex
    {
        public ModelIndex(CandidateSelectionProblem problem)
        {
            var options = problem.Options;
            var byVehicle = new Dictionary<VehicleId, List<int>>();
            var byRequest = new Dictionary<RequestId, List<int>>();
            var donorsByRider = new Dictionary<RequestId, List<int>>();
            var receiversByRider = new Dictionary<RequestId, List<int>>();
            var receivers = new List<int>();
            var riders = new HashSet<RequestId>();
            var hasDonor = false;

            for (var optionIndex = 0; optionIndex < options.Count; optionIndex++)
            {
                var option = options[optionIndex];
                Append(byVehicle, option.VehicleId, optionIndex);

                // Request identifiers are unique within an option (problem validation), so an
                // option enters each of its requests' lists once, as Contains selected it once.
                foreach (var request in option.RequestIds)
                {
                    Append(byRequest, request, optionIndex);
                }

                if (option.RelocationOut is RequestId donated)
                {
                    hasDonor = true;
                    riders.Add(donated);
                    Append(donorsByRider, donated, optionIndex);
                }

                if (option.RelocationIn is RequestId received)
                {
                    receivers.Add(optionIndex);
                    riders.Add(received);
                    Append(receiversByRider, received, optionIndex);
                }
            }

            VehicleOptions = problem.VehicleIds
                .Select(vehicle => Lookup(byVehicle, vehicle))
                .ToArray();
            RequestOptions = problem.RequestIds
                .Select(request => Lookup(byRequest, request))
                .ToArray();
            Receivers = receivers.ToArray();
            HasRelocation = receivers.Count != 0 || hasDonor;
            RelocationRiders = riders
                .OrderBy(value => value.Value, StringComparer.Ordinal)
                .Select(rider => (Lookup(donorsByRider, rider), Lookup(receiversByRider, rider)))
                .ToArray();
            UpperBounds = Enumerable.Range(0, problem.ObjectiveLevels.Count)
                .Select(level => ObjectiveUpperBound(problem, VehicleOptions, level))
                .ToArray();
        }

        public IReadOnlyList<int[]> VehicleOptions { get; }

        public IReadOnlyList<int[]> RequestOptions { get; }

        public int[] Receivers { get; }

        public bool HasRelocation { get; }

        public IReadOnlyList<(int[] Donors, int[] Receivers)> RelocationRiders { get; }

        public IReadOnlyList<long?> UpperBounds { get; }

        private static void Append<TKey>(
            Dictionary<TKey, List<int>> lists,
            TKey key,
            int optionIndex)
            where TKey : notnull
        {
            if (!lists.TryGetValue(key, out var list))
            {
                list = [];
                lists.Add(key, list);
            }

            list.Add(optionIndex);
        }

        private static int[] Lookup<TKey>(Dictionary<TKey, List<int>> lists, TKey key)
            where TKey : notnull =>
            lists.TryGetValue(key, out var list) ? list.ToArray() : [];

        private static long? ObjectiveUpperBound(
            CandidateSelectionProblem problem,
            IReadOnlyList<int[]> vehicleOptions,
            int levelIndex)
        {
            if (problem.ObjectiveLevels[levelIndex].Aggregation
                == CandidateSelectionObjectiveAggregation.Maximum)
            {
                return problem.Options.Max(
                    option => option.ObjectiveContributions[levelIndex]);
            }

            long total = 0;

            foreach (var options in vehicleOptions)
            {
                var maximum = options.Max(
                    option => problem.Options[option].ObjectiveContributions[levelIndex]);

                if (total > DomainLimits.MaxCanonicalInteger - maximum)
                {
                    return null;
                }

                total += maximum;
            }

            return total;
        }
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

    /// <summary>
    /// A built model and its objective expressions. A Maximum level's expression is the variable
    /// the build added; a Sum level's is created on first use and never enters the model by itself.
    /// </summary>
    internal sealed class ModelContext(
        CpModel model,
        IReadOnlyDictionary<string, BoolVar> variables,
        BoolVar[] literals,
        IReadOnlyList<CandidateSelectionOption> options,
        LinearExpr?[] objectives)
    {
        public CpModel Model { get; } = model;

        public IReadOnlyDictionary<string, BoolVar> Variables { get; } = variables;

        public LinearExpr Objective(int levelIndex) =>
            objectives[levelIndex] ??= LinearExpr.WeightedSum(
                literals.AsEnumerable(),
                options.Select(option => option.ObjectiveContributions[levelIndex]));
    }

    internal sealed record BuildResult(ModelContext? Context, string? Error)
    {
        public static BuildResult Success(ModelContext context) =>
            new(context, null);

        public static BuildResult Failure(string error) =>
            new(null, error);
    }
}
