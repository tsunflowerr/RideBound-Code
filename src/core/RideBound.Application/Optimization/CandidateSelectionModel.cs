using System.Text;
using RideBound.Domain.Common;

namespace RideBound.Application.Optimization;

public enum CandidateSelectionObjectiveSense
{
    Minimize,
    Maximize,
}

/// <summary>
/// One lexicographic level. Its value for a selection is the sum of the selected options'
/// contributions.
/// </summary>
public sealed record CandidateSelectionObjectiveLevel(
    string Name,
    CandidateSelectionObjectiveSense Sense);

/// <param name="RelocationOut">
/// The waiting rider this option gives up (a G2 donor); null otherwise.
/// </param>
/// <param name="RelocationIn">
/// The waiting rider this option takes over (a G2 receiver, which accepts no request); null
/// otherwise. A selection picks a donor of a rider exactly when it picks a receiver of it, and
/// at most one receiver in all.
/// </param>
public sealed record CandidateSelectionOption(
    string OptionId,
    VehicleId VehicleId,
    IReadOnlyList<RequestId> RequestIds,
    IReadOnlyList<long> ObjectiveContributions,
    bool IsNoOp,
    RequestId? RelocationOut = null,
    RequestId? RelocationIn = null);

/// <summary>
/// Solver-neutral assignment model. Input collections are validated and then
/// canonicalized so solver adapters cannot depend on dictionary enumeration order.
/// ObjectiveLevels deliberately retains caller order because that order is the
/// lexicographic policy contract.
/// </summary>
public sealed class CandidateSelectionProblem
{
    private CandidateSelectionProblem(
        IReadOnlyList<VehicleId> vehicleIds,
        IReadOnlyList<RequestId> requestIds,
        IReadOnlyList<CandidateSelectionObjectiveLevel> objectiveLevels,
        IReadOnlyList<CandidateSelectionOption> options)
    {
        VehicleIds = vehicleIds;
        RequestIds = requestIds;
        ObjectiveLevels = objectiveLevels;
        Options = options;
    }

    public IReadOnlyList<VehicleId> VehicleIds { get; }

    public IReadOnlyList<RequestId> RequestIds { get; }

    public IReadOnlyList<CandidateSelectionObjectiveLevel> ObjectiveLevels { get; }

    public IReadOnlyList<CandidateSelectionOption> Options { get; }

    public static DomainResult<CandidateSelectionProblem> Create(
        IEnumerable<VehicleId> vehicleIds,
        IEnumerable<RequestId> requestIds,
        IEnumerable<CandidateSelectionObjectiveLevel> objectiveLevels,
        IEnumerable<CandidateSelectionOption> options)
    {
        ArgumentNullException.ThrowIfNull(vehicleIds);
        ArgumentNullException.ThrowIfNull(requestIds);
        ArgumentNullException.ThrowIfNull(objectiveLevels);
        ArgumentNullException.ThrowIfNull(options);

        var vehicles = vehicleIds.ToArray();
        var requests = requestIds.ToArray();
        var levels = objectiveLevels.ToArray();
        var candidateOptions = options.ToArray();

        if (vehicles.Length == 0
            || vehicles.Distinct().Count() != vehicles.Length)
        {
            return Fail(
                "Problem must declare at least one unique vehicle.",
                "vehicleIds");
        }

        if (requests.Distinct().Count() != requests.Length)
        {
            return Fail(
                "Problem request identifiers must be unique.",
                "requestIds");
        }

        if (levels.Length == 0)
        {
            return Fail(
                "Problem must declare at least one objective level.",
                "objectiveLevels");
        }

        if (levels.Any(level => level is null)
            || levels.Any(level => !IsValidIdentifier(level.Name))
            || levels.Select(level => level.Name)
                .Distinct(StringComparer.Ordinal)
                .Count() != levels.Length
            || levels.Any(level => !Enum.IsDefined(level.Sense)))
        {
            return Fail(
                "Objective levels must have unique canonical names and known semantics.",
                "objectiveLevels");
        }

        if (candidateOptions.Length == 0
            || candidateOptions.Any(option => option is null))
        {
            return Fail(
                "Problem must declare candidate options.",
                "options");
        }

        if (candidateOptions.Any(option => !IsValidIdentifier(option.OptionId))
            || candidateOptions.Select(option => option.OptionId)
                .Distinct(StringComparer.Ordinal)
                .Count() != candidateOptions.Length)
        {
            return Fail(
                "Candidate option identifiers must be unique canonical identifiers.",
                "optionId");
        }

        var vehicleSet = vehicles.ToHashSet();
        var requestSet = requests.ToHashSet();

        foreach (var option in candidateOptions)
        {
            if (!vehicleSet.Contains(option.VehicleId))
            {
                return Fail(
                    "Candidate option references an undeclared vehicle.",
                    "vehicleId",
                    option.OptionId);
            }

            if (option.RequestIds is null
                || option.ObjectiveContributions is null)
            {
                return Fail(
                    "Candidate option collections cannot be null.",
                    "options",
                    option.OptionId);
            }

            if (option.RequestIds.Distinct().Count() != option.RequestIds.Count
                || option.RequestIds.Any(requestId => !requestSet.Contains(requestId)))
            {
                return Fail(
                    "Candidate option requests must be unique and declared by the problem.",
                    "requestIds",
                    option.OptionId);
            }

            if (option.IsNoOp && option.RequestIds.Count != 0)
            {
                return Fail(
                    "A no-op candidate cannot accept a request.",
                    "isNoOp",
                    option.OptionId);
            }

            if (option.RelocationOut is not null && option.RelocationIn is not null
                || option.IsNoOp && (option.RelocationOut is not null || option.RelocationIn is not null)
                || option.RelocationIn is not null && option.RequestIds.Count != 0
                || option.RelocationOut is RequestId outgoing && requestSet.Contains(outgoing)
                || option.RelocationIn is RequestId incoming && requestSet.Contains(incoming))
            {
                return Fail(
                    "A relocation option is either a donor or a receiver of one waiting rider; " +
                    "it is never a no-op, a receiver accepts no request, and the rider is not " +
                    "a decided request.",
                    "relocation",
                    option.OptionId);
            }

            if (option.ObjectiveContributions.Count != levels.Length
                || option.ObjectiveContributions.Any(
                    value => value is < 0 or > DomainLimits.MaxCanonicalInteger))
            {
                return Fail(
                    "Each option must provide one canonical integer contribution per objective level.",
                    "objectiveContributions",
                    option.OptionId);
            }
        }

        foreach (var vehicleId in vehicles)
        {
            var vehicleOptions = candidateOptions
                .Where(option => option.VehicleId == vehicleId)
                .ToArray();

            if (vehicleOptions.Length == 0
                || vehicleOptions.Count(option => option.IsNoOp) != 1)
            {
                return Fail(
                    "Each vehicle must have candidate options and exactly one no-op option.",
                    "options",
                    vehicleId.Value);
            }
        }

        var canonicalVehicles = vehicles
            .OrderBy(vehicleId => vehicleId.Value, StringComparer.Ordinal)
            .ToArray();
        var canonicalRequests = requests
            .OrderBy(requestId => requestId.Value, StringComparer.Ordinal)
            .ToArray();
        var canonicalOptions = candidateOptions
            .OrderBy(option => option.VehicleId.Value, StringComparer.Ordinal)
            .ThenBy(option => option.OptionId, StringComparer.Ordinal)
            .Select(
                option => option with
                {
                    RequestIds = Array.AsReadOnly(
                        option.RequestIds
                            .OrderBy(requestId => requestId.Value, StringComparer.Ordinal)
                            .ToArray()),
                    ObjectiveContributions = Array.AsReadOnly(
                        option.ObjectiveContributions.ToArray()),
                })
            .ToArray();

        return DomainResult<CandidateSelectionProblem>.Success(
            new CandidateSelectionProblem(
                Array.AsReadOnly(canonicalVehicles),
                Array.AsReadOnly(canonicalRequests),
                Array.AsReadOnly(levels.ToArray()),
                Array.AsReadOnly(canonicalOptions)));
    }

    private static DomainResult<CandidateSelectionProblem> Fail(
        string message,
        string dimension,
        string? entityId = null) =>
        DomainResult<CandidateSelectionProblem>.Fail(
            CandidateSelectionFailureCodes.InvalidProblem,
            message,
            entityId,
            dimension);

    private static bool IsValidIdentifier(string? value)
    {
        if (string.IsNullOrEmpty(value)
            || Encoding.UTF8.GetByteCount(value) > 128)
        {
            return false;
        }

        for (var index = 0; index < value.Length; index++)
        {
            if (char.IsHighSurrogate(value[index]))
            {
                if (index + 1 >= value.Length
                    || !char.IsLowSurrogate(value[index + 1]))
                {
                    return false;
                }

                index++;
            }
            else if (char.IsLowSurrogate(value[index]))
            {
                return false;
            }
        }

        return true;
    }
}

public static class CandidateSelectionFailureCodes
{
    public const string InvalidProblem = "INVALID_CANDIDATE_SELECTION_PROBLEM";
    public const string InvalidBudget = "INVALID_SOLVER_BUDGET";
    public const string InvalidSolution = "INVALID_CANDIDATE_SELECTION_SOLUTION";
    public const string ObjectiveOverflow = "OBJECTIVE_OVERFLOW";
    public const string InvalidDiagnostics = "INVALID_SOLVER_DIAGNOSTICS";
    public const string InvalidExecutionBudget = "INVALID_EXECUTION_BUDGET";
    public const string InvalidExecutionAccounting =
        "INVALID_EXECUTION_ACCOUNTING";
    public const string ValidationBudgetExhausted =
        "VALIDATION_BUDGET_EXHAUSTED";
    public const string NoValidatedFallback = "NO_VALIDATED_FALLBACK";
}
