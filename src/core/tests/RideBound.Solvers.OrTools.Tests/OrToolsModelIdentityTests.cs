using Google.OrTools.Sat;
using Google.Protobuf;
using RideBound.Application.Optimization;
using RideBound.Domain.Common;

namespace RideBound.Solvers.OrTools.Tests;

/// <summary>
/// ADR-089: the indexed model builder must produce the same CP-SAT model as the archived builder,
/// byte for byte, for every lexicographic pass. Same model and same parameters give the same
/// solve, so every recorded decision stays reproducible.
/// </summary>
public sealed class OrToolsModelIdentityTests
{
    [Fact]
    public void Indexed_builder_produces_the_archived_model_for_every_pass()
    {
        var compared = 0;

        for (var seed = 1; seed <= 400; seed++)
        {
            var problem = RandomProblem(new Random(seed), relocation: seed % 3 == 0, maximumLevel: seed % 4 == 0);
            compared += AssertSameModels(problem, seed);
        }

        Assert.True(compared > 2000, $"only {compared} passes compared");
    }

    [Fact]
    public void Indexed_builder_matches_on_a_fleet_sized_like_the_via_cells()
    {
        // 62 vehicles and one candidate-id rank level per vehicle, as in the Via jobs (64 levels).
        var problem = RandomProblem(new Random(62), relocation: true, maximumLevel: false, vehicles: 62);

        Assert.Equal(64, problem.ObjectiveLevels.Count);
        Assert.Equal(problem.ObjectiveLevels.Count, AssertSameModels(problem, 62));
    }

    [Fact]
    public void Indexed_builder_reports_the_same_overflow_failure()
    {
        var huge = DomainLimits.MaxCanonicalInteger;
        var first = new VehicleId("v1");
        var second = new VehicleId("v2");
        var level = new CandidateSelectionObjectiveLevel(
            "cost",
            CandidateSelectionObjectiveSense.Minimize,
            CandidateSelectionObjectiveAggregation.Sum);
        var problem = CandidateSelectionProblem.Create(
            [first, second],
            [],
            [level],
            [
                new CandidateSelectionOption("a", first, [], [huge], true),
                new CandidateSelectionOption("b", second, [], [huge], true),
            ]).Value!;

        var archived = ArchivedBuilder.Build(problem, []);
        var indexed = OrToolsCandidateSelectionSolver.BuildModel(
            problem,
            new OrToolsCandidateSelectionSolver.ModelIndex(problem),
            []);

        Assert.NotNull(archived.Error);
        Assert.Equal(archived.Error, indexed.Error);
    }

    /// <summary>
    /// Compares, at every pass, the archived fresh build with both production paths: a fresh
    /// indexed build, and the solver's own path (one build at the first pass, then each pass
    /// extends the previous pass's model, whose objective was already set).
    /// </summary>
    private static int AssertSameModels(CandidateSelectionProblem problem, int seed)
    {
        var index = new OrToolsCandidateSelectionSolver.ModelIndex(problem);
        var fixedBounds = new List<ObjectiveSolveBound>();
        var random = new Random(seed * 7919);
        OrToolsCandidateSelectionSolver.ModelContext? reused = null;
        var boundsInModel = 0;

        for (var level = 0; level < problem.ObjectiveLevels.Count; level++)
        {
            var archived = ArchivedBuilder.Build(problem, fixedBounds);
            var fresh = OrToolsCandidateSelectionSolver.BuildModel(problem, index, fixedBounds);
            var extended = reused is null
                ? OrToolsCandidateSelectionSolver.BuildModel(problem, index, fixedBounds)
                : OrToolsCandidateSelectionSolver.ExtendModel(reused, fixedBounds, boundsInModel);

            Assert.Equal(archived.Error, fresh.Error);
            Assert.Equal(archived.Error, extended.Error);

            if (archived.Error is not null)
            {
                return level;
            }

            reused = extended.Context!;
            boundsInModel = fixedBounds.Count;
            var archivedModel = WithObjective(archived.Model!, archived.Objectives![level], problem, level);
            var freshModel = WithObjective(
                fresh.Context!.Model,
                fresh.Context.Objective(level),
                problem,
                level);
            var extendedModel = WithObjective(reused.Model, reused.Objective(level), problem, level);

            Assert.True(
                archivedModel.SequenceEqual(freshModel),
                $"seed {seed}, pass {level}: fresh CP-SAT model differs");
            Assert.True(
                archivedModel.SequenceEqual(extendedModel),
                $"seed {seed}, pass {level}: extended CP-SAT model differs");
            Assert.Equal(archived.VariableOrder, fresh.Context.Variables.Keys.ToArray());
            Assert.Equal(archived.VariableOrder, reused.Variables.Keys.ToArray());

            var objective = problem.ObjectiveLevels[level];
            var value = random.NextInt64(0, 50);
            fixedBounds.Add(ObjectiveSolveBound.Create(level, objective, value, value).Value!);
        }

        return problem.ObjectiveLevels.Count;
    }

    private static byte[] WithObjective(
        CpModel model,
        LinearExpr objective,
        CandidateSelectionProblem problem,
        int level)
    {
        if (problem.ObjectiveLevels[level].Sense == CandidateSelectionObjectiveSense.Maximize)
        {
            model.Maximize(objective);
        }
        else
        {
            model.Minimize(objective);
        }

        return model.Model.ToByteArray();
    }

    private static CandidateSelectionProblem RandomProblem(
        Random random,
        bool relocation,
        bool maximumLevel,
        int? vehicles = null)
    {
        var vehicleCount = vehicles ?? random.Next(1, 9);
        var vehicleIds = Enumerable.Range(0, vehicleCount)
            .Select(index => new VehicleId($"veh-{index:D2}"))
            .ToArray();
        var requestIds = Enumerable.Range(0, random.Next(0, 7))
            .Select(index => new RequestId($"rq-{index:D2}"))
            .ToArray();
        var riders = relocation
            ? Enumerable.Range(0, random.Next(1, 4)).Select(index => new RequestId($"wait-{index:D2}")).ToArray()
            : [];
        var levels = new List<CandidateSelectionObjectiveLevel>
        {
            new("accepted-request-count", CandidateSelectionObjectiveSense.Maximize, CandidateSelectionObjectiveAggregation.Sum),
            new("operational-cost", CandidateSelectionObjectiveSense.Minimize, CandidateSelectionObjectiveAggregation.Sum),
        };

        if (maximumLevel)
        {
            levels.Add(new("worst-delay", CandidateSelectionObjectiveSense.Minimize, CandidateSelectionObjectiveAggregation.Maximum));
        }

        levels.AddRange(vehicleIds.Select(vehicle => new CandidateSelectionObjectiveLevel(
            $"candidate-id-rank:{vehicle.Value}",
            CandidateSelectionObjectiveSense.Minimize,
            CandidateSelectionObjectiveAggregation.Sum)));

        var options = new List<CandidateSelectionOption>();

        foreach (var vehicle in vehicleIds)
        {
            var count = random.Next(0, 6);

            for (var index = 0; index <= count; index++)
            {
                var isNoOp = index == 0;
                var requests = isNoOp || requestIds.Length == 0
                    ? []
                    : requestIds.OrderBy(_ => random.Next()).Take(random.Next(0, 3)).ToArray();
                RequestId? donated = null;
                RequestId? received = null;

                if (!isNoOp && riders.Length != 0 && random.Next(4) == 0)
                {
                    var rider = riders[random.Next(riders.Length)];

                    if (random.Next(2) == 0)
                    {
                        donated = rider;
                    }
                    else
                    {
                        received = rider;
                        requests = [];
                    }
                }

                var contributions = levels
                    .Select((level, levelIndex) => level.Name.StartsWith("candidate-id-rank", StringComparison.Ordinal)
                        ? level.Name.EndsWith(vehicle.Value, StringComparison.Ordinal) ? index : 0
                        : levelIndex == 0 ? requests.Length : random.NextInt64(0, 1_000_000))
                    .ToArray();
                options.Add(new CandidateSelectionOption(
                    $"{vehicle.Value}-opt-{index:D2}-{random.Next(1000):D3}",
                    vehicle,
                    requests,
                    contributions,
                    isNoOp,
                    donated,
                    received));
            }
        }

        var problem = CandidateSelectionProblem.Create(vehicleIds, requestIds, levels, options);
        Assert.True(problem.IsSuccess, problem.Failure?.Message);
        return problem.Value!;
    }

    /// <summary>
    /// The archived builder, verbatim from OrToolsCandidateSelectionSolver.cs at 59c4ee8 (lines
    /// 339-476), returning the model instead of the solver's private context.
    /// </summary>
    private static class ArchivedBuilder
    {
        public sealed record Result(
            CpModel? Model,
            IReadOnlyList<LinearExpr>? Objectives,
            string[]? VariableOrder,
            string? Error);

        public static Result Build(
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
                    return new Result(
                        null,
                        null,
                        null,
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
                ? new Result(model, objectives.AsReadOnly(), variables.Keys.ToArray(), null)
                : new Result(null, null, null, validation);
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
    }
}
