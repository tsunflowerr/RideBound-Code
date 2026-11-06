using RideBound.Domain.Common;

namespace RideBound.Application.Optimization;

public sealed class CandidateSelectionSolution
{
    private CandidateSelectionSolution(
        IReadOnlyList<string> selectedOptionIds,
        IReadOnlyList<long> objectiveValues)
    {
        SelectedOptionIds = selectedOptionIds;
        ObjectiveValues = objectiveValues;
    }

    public IReadOnlyList<string> SelectedOptionIds { get; }

    public IReadOnlyList<long> ObjectiveValues { get; }

    public static DomainResult<CandidateSelectionSolution> Create(
        CandidateSelectionProblem problem,
        IEnumerable<string> selectedOptionIds)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(selectedOptionIds);

        var selectedIds = selectedOptionIds.ToArray();

        if (selectedIds.Length != problem.VehicleIds.Count
            || selectedIds.Any(optionId => optionId is null)
            || selectedIds.Distinct(StringComparer.Ordinal).Count()
                != selectedIds.Length)
        {
            return Fail(
                "A solution must select exactly one unique option per vehicle.",
                "selectedOptionIds");
        }

        var optionsById = problem.Options.ToDictionary(
            option => option.OptionId,
            StringComparer.Ordinal);

        if (selectedIds.Any(optionId => !optionsById.ContainsKey(optionId)))
        {
            return Fail(
                "A solution selected an option outside the problem.",
                "selectedOptionIds");
        }

        var selected = selectedIds.Select(optionId => optionsById[optionId]).ToArray();

        if (selected.GroupBy(option => option.VehicleId).Count()
                != problem.VehicleIds.Count
            || selected.GroupBy(option => option.VehicleId)
                .Any(group => group.Count() != 1))
        {
            return Fail(
                "A solution must select exactly one option for every declared vehicle.",
                "vehicleId");
        }

        var selectedRequests = selected.SelectMany(option => option.RequestIds).ToArray();

        if (selectedRequests.Distinct().Count() != selectedRequests.Length)
        {
            return Fail(
                "A request cannot be accepted by more than one selected option.",
                "requestId");
        }

        // G2: a rider leaves one vehicle exactly when it joins another, and at most one rider
        // moves per decision. Both sets are empty without relocation options.
        var movedOut = selected
            .Where(option => option.RelocationOut is not null)
            .Select(option => option.RelocationOut!.Value)
            .OrderBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();
        var movedIn = selected
            .Where(option => option.RelocationIn is not null)
            .Select(option => option.RelocationIn!.Value)
            .OrderBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();

        if (movedIn.Length > 1 || !movedOut.SequenceEqual(movedIn))
        {
            return Fail(
                "A selection must pair each relocated rider's donor with one receiver, and " +
                "may move at most one rider.",
                "relocation");
        }

        var objectiveValues = new long[problem.ObjectiveLevels.Count];

        try
        {
            for (var levelIndex = 0;
                 levelIndex < problem.ObjectiveLevels.Count;
                 levelIndex++)
            {
                objectiveValues[levelIndex] =
                    problem.ObjectiveLevels[levelIndex].Aggregation switch
                    {
                        CandidateSelectionObjectiveAggregation.Sum => selected.Aggregate(
                            0L,
                            (total, option) => checked(
                                total + option.ObjectiveContributions[levelIndex])),
                        CandidateSelectionObjectiveAggregation.Maximum => selected.Max(
                            option => option.ObjectiveContributions[levelIndex]),
                        _ => throw new InvalidOperationException(
                            "Unknown objective aggregation."),
                    };

                if (objectiveValues[levelIndex] > DomainLimits.MaxCanonicalInteger)
                {
                    throw new OverflowException();
                }
            }
        }
        catch (OverflowException)
        {
            return DomainResult<CandidateSelectionSolution>.Fail(
                CandidateSelectionFailureCodes.ObjectiveOverflow,
                "Selected objective aggregation exceeded the canonical integer range.",
                dimension: "objectiveValues");
        }

        var canonicalIds = selected
            .OrderBy(option => option.VehicleId.Value, StringComparer.Ordinal)
            .Select(option => option.OptionId)
            .ToArray();

        return DomainResult<CandidateSelectionSolution>.Success(
            new CandidateSelectionSolution(
                Array.AsReadOnly(canonicalIds),
                Array.AsReadOnly(objectiveValues)));
    }

    private static DomainResult<CandidateSelectionSolution> Fail(
        string message,
        string dimension) =>
        DomainResult<CandidateSelectionSolution>.Fail(
            CandidateSelectionFailureCodes.InvalidSolution,
            message,
            dimension: dimension);
}

public static class LexicographicObjectiveComparer
{
    /// <summary>
    /// Returns a negative value when the left vector is preferred, following
    /// normal comparer ordering while respecting each level's objective sense.
    /// </summary>
    public static int Compare(
        IReadOnlyList<long> left,
        IReadOnlyList<long> right,
        IReadOnlyList<CandidateSelectionObjectiveLevel> levels)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        ArgumentNullException.ThrowIfNull(levels);

        if (left.Count != levels.Count || right.Count != levels.Count)
        {
            throw new ArgumentException(
                "Objective vectors must match the declared level count.");
        }

        for (var index = 0; index < levels.Count; index++)
        {
            if (left[index] is < 0 or > DomainLimits.MaxCanonicalInteger
                || right[index] is < 0 or > DomainLimits.MaxCanonicalInteger)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(left),
                    "Objective values must be canonical integers.");
            }

            var comparison = left[index].CompareTo(right[index]);

            if (comparison == 0)
            {
                continue;
            }

            return levels[index].Sense == CandidateSelectionObjectiveSense.Maximize
                ? -comparison
                : comparison;
        }

        return 0;
    }
}
