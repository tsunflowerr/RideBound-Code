using RideBound.Domain.Common;

namespace RideBound.Application.Optimization;

public sealed class DeterministicSolverBudget
{
    private DeterministicSolverBudget(
        long maximumWorkUnits,
        long maximumDeterministicTimeMicros,
        long randomSeed,
        bool skipConstantObjectiveLevels)
    {
        MaximumWorkUnits = maximumWorkUnits;
        MaximumDeterministicTimeMicros = maximumDeterministicTimeMicros;
        RandomSeed = randomSeed;
        SkipConstantObjectiveLevels = skipConstantObjectiveLevels;
    }

    public long MaximumWorkUnits { get; }

    public long MaximumDeterministicTimeMicros { get; }

    public long RandomSeed { get; }

    /// <summary>
    /// Opt-in. When set, an adapter may skip the solve pass for a lexicographic
    /// level that <see cref="CandidateSelectionProblem.ConstantObjectiveLevelValues"/>
    /// proves constant, reporting the exact optimum without building a model.
    /// Default is off so every published run keeps its recorded solver work, and
    /// therefore its decision hash, unchanged.
    /// </summary>
    public bool SkipConstantObjectiveLevels { get; }

    public int WorkerCount => 1;

    public static DomainResult<DeterministicSolverBudget> Create(
        long maximumWorkUnits,
        long maximumDeterministicTimeMicros,
        long randomSeed,
        bool skipConstantObjectiveLevels = false)
    {
        if (maximumWorkUnits is < 1 or > DomainLimits.MaxCanonicalInteger
            || maximumDeterministicTimeMicros is < 1
                or > DomainLimits.MaxCanonicalInteger
            || randomSeed is < 0 or > int.MaxValue)
        {
            return DomainResult<DeterministicSolverBudget>.Fail(
                CandidateSelectionFailureCodes.InvalidBudget,
                "Work, deterministic-time, and seed values must be canonical integers; limits must be positive.",
                dimension: "budget");
        }

        return DomainResult<DeterministicSolverBudget>.Success(
            new DeterministicSolverBudget(
                maximumWorkUnits,
                maximumDeterministicTimeMicros,
                randomSeed,
                skipConstantObjectiveLevels));
    }
}

public enum CandidateSelectionSolveStatus
{
    Optimal,
    Feasible,
    Infeasible,
    Unknown,
    ModelInvalid,
    SafeFallback,
}

public sealed class ObjectiveSolveBound
{
    private ObjectiveSolveBound(
        int levelIndex,
        string objectiveName,
        long incumbentValue,
        long bestBound,
        long gapNumerator,
        long gapDenominator,
        bool isProvenOptimal)
    {
        LevelIndex = levelIndex;
        ObjectiveName = objectiveName;
        IncumbentValue = incumbentValue;
        BestBound = bestBound;
        GapNumerator = gapNumerator;
        GapDenominator = gapDenominator;
        IsProvenOptimal = isProvenOptimal;
    }

    public int LevelIndex { get; }

    public string ObjectiveName { get; }

    public long IncumbentValue { get; }

    public long BestBound { get; }

    /// <summary>
    /// Exact normalized gap numerator. The corresponding denominator is
    /// max(1, incumbent), avoiding lossy floating-point percentages.
    /// </summary>
    public long GapNumerator { get; }

    public long GapDenominator { get; }

    public bool IsProvenOptimal { get; }

    public static DomainResult<ObjectiveSolveBound> Create(
        int levelIndex,
        CandidateSelectionObjectiveLevel objective,
        long incumbentValue,
        long bestBound)
    {
        ArgumentNullException.ThrowIfNull(objective);

        if (levelIndex < 0
            || incumbentValue is < 0 or > DomainLimits.MaxCanonicalInteger
            || bestBound is < 0 or > DomainLimits.MaxCanonicalInteger)
        {
            return Fail("Bound values must be canonical integers.");
        }

        var hasValidDirection = objective.Sense switch
        {
            CandidateSelectionObjectiveSense.Minimize => bestBound <= incumbentValue,
            CandidateSelectionObjectiveSense.Maximize => bestBound >= incumbentValue,
            _ => false,
        };

        if (!hasValidDirection)
        {
            return Fail(
                "Best bound is on the wrong side of the incumbent for the objective sense.");
        }

        var gap = objective.Sense == CandidateSelectionObjectiveSense.Minimize
            ? incumbentValue - bestBound
            : bestBound - incumbentValue;

        return DomainResult<ObjectiveSolveBound>.Success(
            new ObjectiveSolveBound(
                levelIndex,
                objective.Name,
                incumbentValue,
                bestBound,
                gap,
                Math.Max(1, incumbentValue),
                gap == 0));
    }

    private static DomainResult<ObjectiveSolveBound> Fail(string message) =>
        DomainResult<ObjectiveSolveBound>.Fail(
            CandidateSelectionFailureCodes.InvalidDiagnostics,
            message,
            dimension: "objectiveBound");
}

public sealed class CandidateSelectionSolverDiagnostics
{
    private CandidateSelectionSolverDiagnostics(
        long consumedWorkUnits,
        long consumedDeterministicTimeMicros,
        long wallTimeMilliseconds,
        IReadOnlyList<ObjectiveSolveBound> objectiveBounds,
        string? detailCode,
        string? detail)
    {
        ConsumedWorkUnits = consumedWorkUnits;
        ConsumedDeterministicTimeMicros = consumedDeterministicTimeMicros;
        WallTimeMilliseconds = wallTimeMilliseconds;
        ObjectiveBounds = objectiveBounds;
        DetailCode = detailCode;
        Detail = detail;
    }

    public long ConsumedWorkUnits { get; }

    public long ConsumedDeterministicTimeMicros { get; }

    /// <summary>Observed metric only; never used as a deterministic decision key.</summary>
    public long WallTimeMilliseconds { get; }

    public IReadOnlyList<ObjectiveSolveBound> ObjectiveBounds { get; }

    public string? DetailCode { get; }

    public string? Detail { get; }

    public static DomainResult<CandidateSelectionSolverDiagnostics> Create(
        CandidateSelectionProblem problem,
        DeterministicSolverBudget budget,
        long consumedWorkUnits,
        long consumedDeterministicTimeMicros,
        long wallTimeMilliseconds,
        IEnumerable<ObjectiveSolveBound> objectiveBounds,
        string? detailCode = null,
        string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(objectiveBounds);

        var bounds = objectiveBounds.ToArray();

        if (consumedWorkUnits is < 0
                or > DomainLimits.MaxCanonicalInteger
            || consumedWorkUnits > budget.MaximumWorkUnits
            || consumedDeterministicTimeMicros is < 0
                or > DomainLimits.MaxCanonicalInteger
            || consumedDeterministicTimeMicros
                > budget.MaximumDeterministicTimeMicros
            || wallTimeMilliseconds is < 0 or > DomainLimits.MaxCanonicalInteger
            || bounds.Length > problem.ObjectiveLevels.Count)
        {
            return Fail("Diagnostics counters or bound count are outside the model contract.");
        }

        for (var index = 0; index < bounds.Length; index++)
        {
            if (bounds[index].LevelIndex != index
                || !StringComparer.Ordinal.Equals(
                    bounds[index].ObjectiveName,
                    problem.ObjectiveLevels[index].Name))
            {
                return Fail(
                    "Objective bounds must be an ordered prefix of the lexicographic levels.");
            }
        }

        return DomainResult<CandidateSelectionSolverDiagnostics>.Success(
            new CandidateSelectionSolverDiagnostics(
                consumedWorkUnits,
                consumedDeterministicTimeMicros,
                wallTimeMilliseconds,
                Array.AsReadOnly(bounds),
                detailCode,
                detail));
    }

    private static DomainResult<CandidateSelectionSolverDiagnostics> Fail(
        string message) =>
        DomainResult<CandidateSelectionSolverDiagnostics>.Fail(
            CandidateSelectionFailureCodes.InvalidDiagnostics,
            message,
            dimension: "diagnostics");
}

public sealed class CandidateSelectionSolveResult
{
    private CandidateSelectionSolveResult(
        CandidateSelectionSolveStatus status,
        CandidateSelectionSolution? solution,
        CandidateSelectionSolverDiagnostics diagnostics,
        string? reasonCode,
        string? message)
    {
        Status = status;
        Solution = solution;
        Diagnostics = diagnostics;
        ReasonCode = reasonCode;
        Message = message;
    }

    public CandidateSelectionSolveStatus Status { get; }

    public CandidateSelectionSolution? Solution { get; }

    public CandidateSelectionSolverDiagnostics Diagnostics { get; }

    public string? ReasonCode { get; }

    public string? Message { get; }

    public static CandidateSelectionSolveResult Optimal(
        CandidateSelectionSolution solution,
        CandidateSelectionSolverDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(solution);
        ArgumentNullException.ThrowIfNull(diagnostics);

        if (diagnostics.ObjectiveBounds.Count != solution.ObjectiveValues.Count
            || diagnostics.ObjectiveBounds.Any(bound => !bound.IsProvenOptimal)
            || !BoundsMatchSolution(solution, diagnostics))
        {
            throw new ArgumentException(
                "Optimal status requires an exact bound for every objective level.",
                nameof(diagnostics));
        }

        return new CandidateSelectionSolveResult(
            CandidateSelectionSolveStatus.Optimal,
            solution,
            diagnostics,
            null,
            null);
    }

    public static CandidateSelectionSolveResult Feasible(
        CandidateSelectionSolution solution,
        CandidateSelectionSolverDiagnostics diagnostics) =>
        WithSolution(
            CandidateSelectionSolveStatus.Feasible,
            solution,
            diagnostics,
            null,
            null);

    public static CandidateSelectionSolveResult SafeFallback(
        CandidateSelectionSolution solution,
        CandidateSelectionSolverDiagnostics diagnostics,
        string reasonCode,
        string message) =>
        WithSolution(
            CandidateSelectionSolveStatus.SafeFallback,
            solution,
            diagnostics,
            reasonCode,
            message);

    public static CandidateSelectionSolveResult Infeasible(
        CandidateSelectionSolverDiagnostics diagnostics,
        string reasonCode,
        string message) =>
        WithoutSolution(
            CandidateSelectionSolveStatus.Infeasible,
            diagnostics,
            reasonCode,
            message);

    public static CandidateSelectionSolveResult Unknown(
        CandidateSelectionSolverDiagnostics diagnostics,
        string reasonCode,
        string message) =>
        WithoutSolution(
            CandidateSelectionSolveStatus.Unknown,
            diagnostics,
            reasonCode,
            message);

    public static CandidateSelectionSolveResult ModelInvalid(
        CandidateSelectionSolverDiagnostics diagnostics,
        string reasonCode,
        string message) =>
        WithoutSolution(
            CandidateSelectionSolveStatus.ModelInvalid,
            diagnostics,
            reasonCode,
            message);

    private static CandidateSelectionSolveResult WithSolution(
        CandidateSelectionSolveStatus status,
        CandidateSelectionSolution solution,
        CandidateSelectionSolverDiagnostics diagnostics,
        string? reasonCode,
        string? message)
    {
        ArgumentNullException.ThrowIfNull(solution);
        ArgumentNullException.ThrowIfNull(diagnostics);

        if (status is not CandidateSelectionSolveStatus.Feasible
            and not CandidateSelectionSolveStatus.SafeFallback)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (!BoundsMatchSolution(solution, diagnostics))
        {
            throw new ArgumentException(
                "Diagnostic incumbents must match the validated solution vector.",
                nameof(diagnostics));
        }

        if (status == CandidateSelectionSolveStatus.SafeFallback)
        {
            ArgumentException.ThrowIfNullOrEmpty(reasonCode);
            ArgumentException.ThrowIfNullOrEmpty(message);
        }

        return new CandidateSelectionSolveResult(
            status,
            solution,
            diagnostics,
            reasonCode,
            message);
    }

    private static bool BoundsMatchSolution(
        CandidateSelectionSolution solution,
        CandidateSelectionSolverDiagnostics diagnostics) =>
        diagnostics.ObjectiveBounds.All(
            bound => bound.LevelIndex < solution.ObjectiveValues.Count
                && bound.IncumbentValue
                    == solution.ObjectiveValues[bound.LevelIndex]);

    private static CandidateSelectionSolveResult WithoutSolution(
        CandidateSelectionSolveStatus status,
        CandidateSelectionSolverDiagnostics diagnostics,
        string reasonCode,
        string message)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentException.ThrowIfNullOrEmpty(reasonCode);
        ArgumentException.ThrowIfNullOrEmpty(message);

        if (status is not CandidateSelectionSolveStatus.Infeasible
            and not CandidateSelectionSolveStatus.Unknown
            and not CandidateSelectionSolveStatus.ModelInvalid)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        return new CandidateSelectionSolveResult(
            status,
            null,
            diagnostics,
            reasonCode,
            message);
    }
}

public interface ICandidateSelectionSolver
{
    CandidateSelectionSolveResult Solve(
        CandidateSelectionProblem problem,
        DeterministicSolverBudget budget);
}
