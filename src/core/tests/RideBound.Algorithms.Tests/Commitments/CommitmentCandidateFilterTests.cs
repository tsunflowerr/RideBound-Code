using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Commitments;
using RideBound.Application.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Validation;

namespace RideBound.Algorithms.Tests.Commitments;

public sealed class CommitmentCandidateFilterTests
{
    [Fact]
    public void Missing_policy_for_new_request_is_pruned_before_fleet_selection()
    {
        var request = AlgorithmTestData.PendingRequest();
        var state = AlgorithmTestData.CreateState(
            [request],
            [AlgorithmTestData.Vehicle()]);
        var reduced = state with
        {
            Run = state.Run.AdvanceEpoch(1, state.Run.SimulationTime).Value!,
            NextEventSequence = 2,
        };
        var generated = new InsertionCandidateGenerator().Generate(
            reduced,
            CandidateGenerationOptions.ExactSmall);
        Assert.True(generated.IsSuccess, generated.Witness?.Message);
        var candidateSets = generated.VehicleCandidates!;
        Assert.Contains(
            candidateSets.Single().Candidates,
            value => value.NewRequestIds.Contains(request.Id));
        var filter = new CommitmentCandidateFilter(
            state,
            new CommitmentPolicyCatalog([]),
            NoDistances.Instance,
            "candidate-filter-test",
            1);

        var filtered = filter.Filter(
            reduced,
            candidateSets);

        var set = Assert.Single(filtered);
        Assert.Single(set.Candidates, value => value.IsNoOp);
        var prune = Assert.Single(
            set.PrunedCandidates,
            value => value.NewRequestIds.Contains(request.Id)
                && value.Code == "COMMITMENT_POLICY_NOT_FOUND");
        var commitmentWitness = Assert.Single(prune.CommitmentWitnesses!);
        Assert.Equal("COMMITMENT_POLICY_NOT_FOUND", commitmentWitness.Code);
    }

    private sealed class NoDistances : IStopDistanceLookup
    {
        public static NoDistances Instance { get; } = new();

        public bool TryGetDistanceMillimeters(
            NodeId fromNodeId,
            NodeId toNodeId,
            out long distanceMillimeters)
        {
            distanceMillimeters = 0;
            return false;
        }
    }
}
