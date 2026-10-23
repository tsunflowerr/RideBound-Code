using RideBound.Domain.Common;

namespace RideBound.Application.Optimization;

/// <summary>
/// Keeps candidate generation, semantic validation and solver search budgets
/// separate. Observed wall time is deliberately absent: deterministic work is
/// the only input allowed to change a replay outcome.
/// </summary>
public sealed class DeterministicCandidateSelectionExecutionBudget
{
    private DeterministicCandidateSelectionExecutionBudget(
        long maximumGenerationWorkUnits,
        long maximumValidationWorkUnits,
        DeterministicSolverBudget solverBudget)
    {
        MaximumGenerationWorkUnits = maximumGenerationWorkUnits;
        MaximumValidationWorkUnits = maximumValidationWorkUnits;
        SolverBudget = solverBudget;
    }

    public long MaximumGenerationWorkUnits { get; }

    public long MaximumValidationWorkUnits { get; }

    public DeterministicSolverBudget SolverBudget { get; }

    public static DomainResult<DeterministicCandidateSelectionExecutionBudget> Create(
        long maximumGenerationWorkUnits,
        long maximumValidationWorkUnits,
        DeterministicSolverBudget solverBudget)
    {
        ArgumentNullException.ThrowIfNull(solverBudget);

        if (maximumGenerationWorkUnits is < 1
                or > DomainLimits.MaxCanonicalInteger
            || maximumValidationWorkUnits is < 1
                or > DomainLimits.MaxCanonicalInteger)
        {
            return DomainResult<DeterministicCandidateSelectionExecutionBudget>.Fail(
                CandidateSelectionFailureCodes.InvalidExecutionBudget,
                "Generation and validation work limits must be positive canonical integers.",
                dimension: "executionBudget");
        }

        return DomainResult<DeterministicCandidateSelectionExecutionBudget>.Success(
            new DeterministicCandidateSelectionExecutionBudget(
                maximumGenerationWorkUnits,
                maximumValidationWorkUnits,
                solverBudget));
    }
}

/// <summary>
/// Audited work and candidate omission information produced before selection.
/// A non-zero omission count must carry a stable digest so candidate loss can
/// never be confused with solver loss.
/// </summary>
public sealed class CandidateSelectionPreSolveAccounting
{
    private CandidateSelectionPreSolveAccounting(
        long consumedGenerationWorkUnits,
        long consumedValidationWorkUnits,
        long omittedCandidateCount,
        string? omissionDigest,
        bool omissionCountWasSaturated)
    {
        ConsumedGenerationWorkUnits = consumedGenerationWorkUnits;
        ConsumedValidationWorkUnits = consumedValidationWorkUnits;
        OmittedCandidateCount = omittedCandidateCount;
        OmissionDigest = omissionDigest;
        OmissionCountWasSaturated = omissionCountWasSaturated;
    }

    public long ConsumedGenerationWorkUnits { get; }

    public long ConsumedValidationWorkUnits { get; }

    public long OmittedCandidateCount { get; }

    public string? OmissionDigest { get; }

    public bool OmissionCountWasSaturated { get; }

    public static DomainResult<CandidateSelectionPreSolveAccounting> Create(
        DeterministicCandidateSelectionExecutionBudget budget,
        long consumedGenerationWorkUnits,
        long consumedValidationWorkUnits,
        long omittedCandidateCount,
        string? omissionDigest = null,
        bool omissionCountWasSaturated = false)
    {
        ArgumentNullException.ThrowIfNull(budget);

        var hasCanonicalLoss = omittedCandidateCount == 0
            ? omissionDigest is null && !omissionCountWasSaturated
            : IsLowerHexSha256(omissionDigest);

        if (consumedGenerationWorkUnits is < 0
                or > DomainLimits.MaxCanonicalInteger
            || consumedGenerationWorkUnits > budget.MaximumGenerationWorkUnits
            || consumedValidationWorkUnits is < 0
                or > DomainLimits.MaxCanonicalInteger
            || consumedValidationWorkUnits > budget.MaximumValidationWorkUnits
            || omittedCandidateCount is < 0
                or > DomainLimits.MaxCanonicalInteger
            || !hasCanonicalLoss)
        {
            return DomainResult<CandidateSelectionPreSolveAccounting>.Fail(
                CandidateSelectionFailureCodes.InvalidExecutionAccounting,
                "Pre-solve counters must fit their stage budgets and candidate loss requires a stable digest.",
                dimension: "preSolveAccounting");
        }

        return DomainResult<CandidateSelectionPreSolveAccounting>.Success(
            new CandidateSelectionPreSolveAccounting(
                consumedGenerationWorkUnits,
                consumedValidationWorkUnits,
                omittedCandidateCount,
                omissionDigest,
                omissionCountWasSaturated));
    }

    private static bool IsLowerHexSha256(string? value) =>
        value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');
}

public sealed record CandidateSelectionValidationResult
{
    private CandidateSelectionValidationResult(
        bool isValid,
        string? reasonCode,
        string? message)
    {
        IsValid = isValid;
        ReasonCode = reasonCode;
        Message = message;
    }

    public bool IsValid { get; }

    public string? ReasonCode { get; }

    public string? Message { get; }

    public static CandidateSelectionValidationResult Valid() =>
        new(true, null, null);

    public static CandidateSelectionValidationResult Invalid(
        string reasonCode,
        string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(reasonCode);
        ArgumentException.ThrowIfNullOrEmpty(message);
        return new CandidateSelectionValidationResult(false, reasonCode, message);
    }
}

/// <summary>
/// Implementations must perform the independent semantic/full-state validation
/// appropriate to their publication boundary. Model feasibility alone is not a
/// substitute for this check.
/// </summary>
public interface ICandidateSelectionSolutionValidator
{
    CandidateSelectionValidationResult Validate(
        CandidateSelectionProblem problem,
        CandidateSelectionSolution solution);
}

public enum CandidateSelectionExecutionPath
{
    None,
    ValidatedIncumbent,
    SafeNoOp,
    GreedySingleRequest,
}

public sealed record CandidateSelectionValidationWitness(
    CandidateSelectionExecutionPath AttemptedPath,
    IReadOnlyList<string> SelectedOptionIds,
    string ReasonCode,
    string Message);

public sealed record CandidateSelectionExecutionDiagnostics(
    long ConsumedGenerationWorkUnits,
    long ConsumedValidationWorkUnits,
    long OmittedCandidateCount,
    string? OmissionDigest,
    bool OmissionCountWasSaturated,
    CandidateSelectionSolveStatus PrimarySolveStatus,
    CandidateSelectionSolverDiagnostics PrimarySolverDiagnostics,
    CandidateSelectionExecutionPath ExecutionPath,
    long FallbackValidationAttempts,
    bool PrimaryIncumbentRejected,
    IReadOnlyList<CandidateSelectionValidationWitness> ValidationWitnesses)
{
    public bool CandidateLossOccurred => OmittedCandidateCount != 0;

    public bool SolverLossOccurred =>
        PrimarySolveStatus != CandidateSelectionSolveStatus.Optimal;
}

public sealed record CandidateSelectionExecutionResult(
    CandidateSelectionSolveResult SolveResult,
    CandidateSelectionExecutionDiagnostics Diagnostics);

/// <summary>
/// Executes the solver and a deterministic, independently validated fallback
/// portfolio. It never uses an unvalidated incumbent and never fabricates an
/// incident-recovery result.
/// </summary>
public sealed class SafeCandidateSelectionExecutor
{
    private readonly ICandidateSelectionSolver _solver;
    private readonly ICandidateSelectionSolutionValidator _validator;

    public SafeCandidateSelectionExecutor(
        ICandidateSelectionSolver solver,
        ICandidateSelectionSolutionValidator validator)
    {
        _solver = solver ?? throw new ArgumentNullException(nameof(solver));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public CandidateSelectionExecutionResult Execute(
        CandidateSelectionProblem problem,
        DeterministicCandidateSelectionExecutionBudget budget,
        CandidateSelectionPreSolveAccounting preSolveAccounting)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(preSolveAccounting);

        if (preSolveAccounting.ConsumedGenerationWorkUnits
                > budget.MaximumGenerationWorkUnits
            || preSolveAccounting.ConsumedValidationWorkUnits
                > budget.MaximumValidationWorkUnits)
        {
            throw new ArgumentException(
                "Pre-solve accounting exceeds the execution budget supplied to this run.",
                nameof(preSolveAccounting));
        }

        var primary = _solver.Solve(problem, budget.SolverBudget);
        var consumedValidation = preSolveAccounting.ConsumedValidationWorkUnits;
        var fallbackAttempts = 0L;
        var primaryRejected = false;
        var validationWitnesses = new List<CandidateSelectionValidationWitness>();

        if (primary.Solution is not null)
        {
            var incumbentValidation = Validate(
                primary.Solution,
                CandidateSelectionExecutionPath.ValidatedIncumbent);

            if (incumbentValidation == ValidationAttempt.Valid)
            {
                return Complete(
                    primary,
                    CandidateSelectionExecutionPath.ValidatedIncumbent);
            }

            if (incumbentValidation == ValidationAttempt.BudgetExhausted)
            {
                return ValidationBudgetFailure();
            }

            primaryRejected = true;
        }

        var noOp = CandidateSelectionSolution.Create(
            problem,
            problem.Options
                .Where(option => option.IsNoOp)
                .Select(option => option.OptionId));

        if (noOp.IsSuccess)
        {
            fallbackAttempts++;
            var noOpValidation = Validate(
                noOp.Value!,
                CandidateSelectionExecutionPath.SafeNoOp);

            if (noOpValidation == ValidationAttempt.Valid)
            {
                return SafeFallback(
                    noOp.Value!,
                    CandidateSelectionExecutionPath.SafeNoOp,
                    "SAFE_FALLBACK_NO_OP",
                    "The primary solver outcome was not publishable; the independently validated no-op selection was used.");
            }

            if (noOpValidation == ValidationAttempt.BudgetExhausted)
            {
                return ValidationBudgetFailure();
            }
        }

        foreach (var greedy in CreateGreedySingleRequestSolutions(problem))
        {
            fallbackAttempts++;
            var greedyValidation = Validate(
                greedy,
                CandidateSelectionExecutionPath.GreedySingleRequest);

            if (greedyValidation == ValidationAttempt.Valid)
            {
                return SafeFallback(
                    greedy,
                    CandidateSelectionExecutionPath.GreedySingleRequest,
                    "SAFE_FALLBACK_GREEDY_SINGLE_REQUEST",
                    "The primary and no-op outcomes were not publishable; an independently validated deterministic single-request insertion was used.");
            }

            if (greedyValidation == ValidationAttempt.BudgetExhausted)
            {
                return ValidationBudgetFailure();
            }
        }

        var unavailable = CandidateSelectionSolveResult.Unknown(
            primary.Diagnostics,
            CandidateSelectionFailureCodes.NoValidatedFallback,
            "Neither the primary incumbent nor a deterministic fallback passed independent validation.");
        return Complete(unavailable, CandidateSelectionExecutionPath.None);

        ValidationAttempt Validate(
            CandidateSelectionSolution solution,
            CandidateSelectionExecutionPath attemptedPath)
        {
            if (consumedValidation >= budget.MaximumValidationWorkUnits)
            {
                return ValidationAttempt.BudgetExhausted;
            }

            consumedValidation++;
            var validation = _validator.Validate(problem, solution);

            if (validation.IsValid)
            {
                return ValidationAttempt.Valid;
            }

            validationWitnesses.Add(
                new CandidateSelectionValidationWitness(
                    attemptedPath,
                    Array.AsReadOnly(solution.SelectedOptionIds.ToArray()),
                    validation.ReasonCode!,
                    validation.Message!));
            return ValidationAttempt.Invalid;
        }

        CandidateSelectionExecutionResult ValidationBudgetFailure()
        {
            var failure = CandidateSelectionSolveResult.Unknown(
                primary.Diagnostics,
                CandidateSelectionFailureCodes.ValidationBudgetExhausted,
                "Independent validation work was exhausted before a publishable selection was proven.");
            return Complete(failure, CandidateSelectionExecutionPath.None);
        }

        CandidateSelectionExecutionResult SafeFallback(
            CandidateSelectionSolution solution,
            CandidateSelectionExecutionPath path,
            string reasonCode,
            string message)
        {
            var diagnostics = CandidateSelectionSolverDiagnostics.Create(
                problem,
                budget.SolverBudget,
                primary.Diagnostics.ConsumedWorkUnits,
                primary.Diagnostics.ConsumedDeterministicTimeMicros,
                primary.Diagnostics.WallTimeMilliseconds,
                [],
                reasonCode,
                message).Value!;
            return Complete(
                CandidateSelectionSolveResult.SafeFallback(
                    solution,
                    diagnostics,
                    reasonCode,
                    message),
                path);
        }

        CandidateSelectionExecutionResult Complete(
            CandidateSelectionSolveResult result,
            CandidateSelectionExecutionPath path) =>
            new(
                result,
                new CandidateSelectionExecutionDiagnostics(
                    preSolveAccounting.ConsumedGenerationWorkUnits,
                    consumedValidation,
                    preSolveAccounting.OmittedCandidateCount,
                    preSolveAccounting.OmissionDigest,
                    preSolveAccounting.OmissionCountWasSaturated,
                    primary.Status,
                    primary.Diagnostics,
                    path,
                    fallbackAttempts,
                    primaryRejected,
                    validationWitnesses.AsReadOnly()));
    }

    private static IReadOnlyList<CandidateSelectionSolution>
        CreateGreedySingleRequestSolutions(CandidateSelectionProblem problem)
    {
        var noOpsByVehicle = problem.Options
            .Where(option => option.IsNoOp)
            .ToDictionary(option => option.VehicleId, option => option.OptionId);
        var solutions = new List<CandidateSelectionSolution>();

        foreach (var option in problem.Options.Where(
                     option => !option.IsNoOp
                         && option.RequestIds.Count == 1
                         && option.RelocationOut is null
                         && option.RelocationIn is null))
        {
            var selectedIds = problem.VehicleIds
                .Select(
                    vehicleId => vehicleId == option.VehicleId
                        ? option.OptionId
                        : noOpsByVehicle[vehicleId]);
            var solution = CandidateSelectionSolution.Create(problem, selectedIds);

            if (solution.IsSuccess)
            {
                solutions.Add(solution.Value!);
            }
        }

        solutions.Sort(
            (left, right) =>
            {
                var objectiveComparison = LexicographicObjectiveComparer.Compare(
                    left.ObjectiveValues,
                    right.ObjectiveValues,
                    problem.ObjectiveLevels);
                return objectiveComparison != 0
                    ? objectiveComparison
                    : CompareOptionIds(
                        left.SelectedOptionIds,
                        right.SelectedOptionIds);
            });

        return solutions.AsReadOnly();
    }

    private static int CompareOptionIds(
        IReadOnlyList<string> left,
        IReadOnlyList<string> right)
    {
        for (var index = 0; index < left.Count; index++)
        {
            var comparison = StringComparer.Ordinal.Compare(left[index], right[index]);

            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }

    private enum ValidationAttempt
    {
        Valid,
        Invalid,
        BudgetExhausted,
    }
}
