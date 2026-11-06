using RideBound.Application.Optimization;
using RideBound.Domain.Common;

namespace RideBound.Application.Tests.Optimization;

public sealed class CandidateSelectionConstantLevelTests
{
    private static readonly VehicleId VehicleOne = new("vehicle-1");
    private static readonly VehicleId VehicleTwo = new("vehicle-2");
    private static readonly RequestId RequestOne = new("request-1");

    [Fact]
    public void A_level_is_constant_only_when_every_vehicle_agrees_internally()
    {
        var problem = Problem(
            [Sum("agrees"), Sum("disagrees")],
            [
                Option("v1-noop", VehicleOne, [], [5, 1], isNoOp: true),
                Option("v1-accept", VehicleOne, [RequestOne], [5, 2]),
                Option("v2-noop", VehicleTwo, [], [9, 3], isNoOp: true),
            ]);

        var constants = problem.ConstantObjectiveLevelValues();

        // Vehicle 1 contributes 5 and vehicle 2 contributes 9 on the first level,
        // so every feasible assignment sums to 14 even though the two vehicles
        // disagree with each other.
        Assert.Equal(14, constants[0]);
        Assert.Null(constants[1]);
    }

    [Fact]
    public void Maximum_aggregation_reports_the_largest_per_vehicle_value()
    {
        var problem = Problem(
            [Maximum("worst")],
            [
                Option("v1-noop", VehicleOne, [], [400], isNoOp: true),
                Option("v1-accept", VehicleOne, [RequestOne], [400]),
                Option("v2-noop", VehicleTwo, [], [700], isNoOp: true),
            ]);

        Assert.Equal(700, problem.ConstantObjectiveLevelValues()[0]);
    }

    [Fact]
    public void A_constant_level_matches_the_value_of_every_feasible_assignment()
    {
        var problem = Problem(
            [Sum("constant-sum"), Maximum("constant-max"), Sum("free")],
            [
                Option("v1-noop", VehicleOne, [], [5, 400, 0], isNoOp: true),
                Option("v1-accept", VehicleOne, [RequestOne], [5, 400, 7]),
                Option("v2-noop", VehicleTwo, [], [9, 700, 0], isNoOp: true),
                Option("v2-accept", VehicleTwo, [RequestOne], [9, 700, 3]),
            ]);
        var constants = problem.ConstantObjectiveLevelValues();

        // Request uniqueness forbids both vehicles accepting request-1, so this
        // enumerates exactly the feasible assignments.
        string[][] feasible =
        [
            ["v1-noop", "v2-noop"],
            ["v1-accept", "v2-noop"],
            ["v1-noop", "v2-accept"],
        ];

        foreach (var selection in feasible)
        {
            var solution = CandidateSelectionSolution.Create(problem, selection);

            Assert.True(solution.IsSuccess);
            Assert.Equal(constants[0], solution.Value!.ObjectiveValues[0]);
            Assert.Equal(constants[1], solution.Value.ObjectiveValues[1]);
        }

        Assert.Null(constants[2]);
    }

    [Fact]
    public void A_sum_that_leaves_the_canonical_range_is_not_reported_constant()
    {
        var half = DomainLimits.MaxCanonicalInteger / 2 + 1;
        var problem = Problem(
            [Sum("overflowing")],
            [
                Option("v1-noop", VehicleOne, [], [half], isNoOp: true),
                Option("v2-noop", VehicleTwo, [], [half], isNoOp: true),
            ]);

        Assert.Null(problem.ConstantObjectiveLevelValues()[0]);
    }

    [Fact]
    public void A_single_option_per_vehicle_makes_every_level_constant()
    {
        var problem = Problem(
            [Sum("first"), Maximum("second")],
            [
                Option("v1-noop", VehicleOne, [], [3, 11], isNoOp: true),
                Option("v2-noop", VehicleTwo, [], [4, 22], isNoOp: true),
            ]);

        Assert.Equal([7L, 22L], problem.ConstantObjectiveLevelValues());
    }

    private static CandidateSelectionObjectiveLevel Sum(string name) =>
        new(
            name,
            CandidateSelectionObjectiveSense.Minimize,
            CandidateSelectionObjectiveAggregation.Sum);

    private static CandidateSelectionObjectiveLevel Maximum(string name) =>
        new(
            name,
            CandidateSelectionObjectiveSense.Minimize,
            CandidateSelectionObjectiveAggregation.Maximum);

    private static CandidateSelectionOption Option(
        string optionId,
        VehicleId vehicleId,
        RequestId[] requestIds,
        long[] contributions,
        bool isNoOp = false) =>
        new(optionId, vehicleId, requestIds, contributions, isNoOp);

    private static CandidateSelectionProblem Problem(
        CandidateSelectionObjectiveLevel[] levels,
        CandidateSelectionOption[] options)
    {
        var vehicles = options.Select(option => option.VehicleId).Distinct();
        var requests = options.SelectMany(option => option.RequestIds).Distinct();
        var created = CandidateSelectionProblem.Create(
            vehicles,
            requests,
            levels,
            options);

        Assert.True(created.IsSuccess, created.Failure?.Message);
        return created.Value!;
    }
}
