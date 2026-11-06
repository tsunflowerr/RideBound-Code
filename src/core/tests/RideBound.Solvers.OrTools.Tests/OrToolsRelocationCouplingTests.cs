using RideBound.Application.Optimization;
using RideBound.Domain.Common;

namespace RideBound.Solvers.OrTools.Tests;

/// <summary>
/// G2 coupling in the real CP-SAT model: a rider's donor is selected exactly when one of its
/// receivers is, and at most one rider moves. Levels: cost, then relocation-candidate-count.
/// </summary>
public sealed class OrToolsRelocationCouplingTests
{
    private static readonly VehicleId V1 = new("vehicle-1");
    private static readonly VehicleId V2 = new("vehicle-2");
    private static readonly VehicleId V3 = new("vehicle-3");
    private static readonly RequestId W1 = new("waiting-1");
    private static readonly RequestId W2 = new("waiting-2");

    [Fact]
    public void A_cheap_donor_alone_is_never_selected_and_a_cheaper_move_is()
    {
        // Donor alone would cost 0 + 0; it is infeasible without its receiver. The move costs
        // 0 + 110 < 220 + 0, so the pair is chosen.
        var problem = Problem(receiverCost: 110);

        var result = Solve(problem);

        Assert.Equal(CandidateSelectionSolveStatus.Optimal, result.Status);
        Assert.Equal(["v1-donor", "v2-receiver"], result.Solution!.SelectedOptionIds);
        Assert.Equal([110, 2], result.Solution.ObjectiveValues);
        Assert.False(CandidateSelectionSolution.Create(problem, ["v1-donor", "v2-noop"]).IsSuccess);
        Assert.False(CandidateSelectionSolution.Create(problem, ["v1-noop", "v2-receiver"]).IsSuccess);
    }

    [Fact]
    public void A_dearer_move_and_an_equal_cost_move_keep_the_rider()
    {
        // 0 + 300 > 220: keep. At 0 + 220 = 220 the relocation level prefers keeping (prefer-keep).
        Assert.Equal(["v1-noop", "v2-noop"], Solve(Problem(receiverCost: 300)).Solution!.SelectedOptionIds);
        var tie = Solve(Problem(receiverCost: 220));
        Assert.Equal(["v1-noop", "v2-noop"], tie.Solution!.SelectedOptionIds);
        Assert.Equal([220, 0], tie.Solution.ObjectiveValues);
    }

    [Fact]
    public void At_most_one_rider_moves_even_when_two_moves_would_be_cheaper()
    {
        // Two riders on vehicle-1 could each move to their own receiver; each move saves cost, but
        // only one may happen. The cheaper single move wins.
        var levels = Levels();
        var problem = CandidateSelectionProblem.Create(
            [V1, V2, V3],
            [],
            levels,
            [
                new CandidateSelectionOption("v1-noop", V1, [], [300, 0], true),
                new CandidateSelectionOption("v1-donor-w1", V1, [], [150, 1], false, RelocationOut: W1),
                new CandidateSelectionOption("v1-donor-w2", V1, [], [160, 1], false, RelocationOut: W2),
                new CandidateSelectionOption("v2-noop", V2, [], [0, 0], true),
                new CandidateSelectionOption("v2-receiver-w1", V2, [], [50, 1], false, RelocationIn: W1),
                new CandidateSelectionOption("v3-noop", V3, [], [0, 0], true),
                new CandidateSelectionOption("v3-receiver-w2", V3, [], [10, 1], false, RelocationIn: W2),
            ]).Value!;

        var result = Solve(problem);

        Assert.Equal(CandidateSelectionSolveStatus.Optimal, result.Status);
        Assert.Equal(["v1-donor-w2", "v2-noop", "v3-receiver-w2"], result.Solution!.SelectedOptionIds);
        Assert.Equal([170, 2], result.Solution.ObjectiveValues);
    }

    [Fact]
    public void Malformed_relocation_options_are_rejected_by_the_model()
    {
        var levels = Levels();

        foreach (var bad in new[]
                 {
                     new CandidateSelectionOption("bad", V1, [], [0, 1], false, RelocationOut: W1, RelocationIn: W1),
                     new CandidateSelectionOption("bad", V1, [], [0, 1], true, RelocationOut: W1),
                     new CandidateSelectionOption("bad", V1, [W2], [0, 1], false, RelocationIn: W1),
                     new CandidateSelectionOption("bad", V1, [W1], [0, 1], false, RelocationOut: W1),
                 })
        {
            var problem = CandidateSelectionProblem.Create(
                [V1],
                bad.RequestIds,
                levels,
                [new CandidateSelectionOption("v1-noop", V1, [], [0, 0], true), bad]);

            Assert.False(problem.IsSuccess, bad.ToString());
        }
    }

    private static CandidateSelectionProblem Problem(long receiverCost) =>
        CandidateSelectionProblem.Create(
            [V1, V2],
            [],
            Levels(),
            [
                new CandidateSelectionOption("v1-noop", V1, [], [220, 0], true),
                new CandidateSelectionOption("v1-donor", V1, [], [0, 1], false, RelocationOut: W1),
                new CandidateSelectionOption("v2-noop", V2, [], [0, 0], true),
                new CandidateSelectionOption("v2-receiver", V2, [], [receiverCost, 1], false, RelocationIn: W1),
            ]).Value!;

    private static CandidateSelectionObjectiveLevel[] Levels() =>
    [
        new("operational-cost", CandidateSelectionObjectiveSense.Minimize, CandidateSelectionObjectiveAggregation.Sum),
        new("relocation-candidate-count", CandidateSelectionObjectiveSense.Minimize, CandidateSelectionObjectiveAggregation.Sum),
    ];

    private static CandidateSelectionSolveResult Solve(CandidateSelectionProblem problem) =>
        new OrToolsCandidateSelectionSolver().Solve(
            problem,
            DeterministicSolverBudget.Create(
                maximumWorkUnits: 100_000,
                maximumDeterministicTimeMicros: 1_000_000,
                randomSeed: 7).Value!);
}
