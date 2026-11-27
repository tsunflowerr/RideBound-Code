using RideBound.Domain.Common;

namespace RideBound.Application.Optimization;

public sealed class DeterministicSolverBudget
{
    private DeterministicSolverBudget(
        long maximumWorkUnits,
        long maximumDeterministicTimeMicros,
        long randomSeed)
    {
        MaximumWorkUnits = maximumWorkUnits;
        MaximumDeterministicTimeMicros = maximumDeterministicTimeMicros;
        RandomSeed = randomSeed;
    }

    public long MaximumWorkUnits { get; }

    public long MaximumDeterministicTimeMicros { get; }

    public long RandomSeed { get; }

    public static DomainResult<DeterministicSolverBudget> Create(
        long maximumWorkUnits,
        long maximumDeterministicTimeMicros,
        long randomSeed)
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
                randomSeed));
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
        long incumbentValue)
    {
        LevelIndex = levelIndex;
        ObjectiveName = objectiveName;
        IncumbentValue = incumbentValue;
    }

    public int LevelIndex { get; }

    public string ObjectiveName { get; }

    public long IncumbentValue { get; }

    public static DomainResult<ObjectiveSolveBound> Create(
        int levelIndex,
        CandidateSelectionObjectiveLevel objective,
        long incumbentValue)
    {
        ArgumentNullException.ThrowIfNull(objective);

        if (levelIndex < 0
            || incumbentValue is < 0 or > DomainLimits.MaxCanonicalInteger)
        {
            return Fail("Bound values must be canonical integers.");
        }

        return DomainResult<ObjectiveSolveBound>.Success(
            new ObjectiveSolveBound(
                levelIndex,
                objective.Name,
                incumbentValue));
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
        IReadOnlyList<ObjectiveSolveBound> objectiveBounds)
    {
        ConsumedWorkUnits = consumedWorkUnits;
        ConsumedDeterministicTimeMicros = consumedDeterministicTimeMicros;
        ObjectiveBounds = objectiveBounds;
    }

    public long ConsumedWorkUnits { get; }

    public long ConsumedDeterministicTimeMicros { get; }

    public IReadOnlyList<ObjectiveSolveBound> ObjectiveBounds { get; }

    public static DomainResult<CandidateSelectionSolverDiagnostics> Create(
        CandidateSelectionProblem problem,
        DeterministicSolverBudget budget,
        long consumedWorkUnits,
        long consumedDeterministicTimeMicros,
        IEnumerable<ObjectiveSolveBound> objectiveBounds)
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
                Array.AsReadOnly(bounds)));
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
