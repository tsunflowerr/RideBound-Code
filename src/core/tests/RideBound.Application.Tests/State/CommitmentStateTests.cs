using RideBound.Application.State;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Routes;

namespace RideBound.Application.Tests.State;

public sealed class CommitmentStateTests
{
    [Fact]
    public void Ledger_is_staged_with_route_state_and_committed_only_on_ack()
    {
        var coordinator = new EventReductionCoordinator(
            ApplicationTestData.InitialState());
        var reduced = coordinator.Propose(
            ApplicationTestData.BootstrapBatch()).ProposedState!;
        var projection = new PromiseProjection(
            ApplicationTestData.RequestId,
            ApplicationTestData.VehicleId,
            new StopId("pickup"),
            ApplicationTestData.NodeOne,
            new StopId("drop"),
            ApplicationTestData.NodeTwo,
            new SimTime(1_100),
            new SimTime(1_200),
            [
                new PromiseServiceToken(
                    new StopId("pickup"),
                    ApplicationTestData.RequestId,
                    RouteStopKind.Pickup),
                new PromiseServiceToken(
                    new StopId("drop"),
                    ApplicationTestData.RequestId,
                    RouteStopKind.DropOff),
            ]);
        var ledger = CommitmentLedger.Empty.OpenInitial(
            "pub-1",
            projection,
            1,
            new SimTime(1_000),
            "INITIAL_ACCEPTANCE",
            3).Ledger!;

        var staged = coordinator.StageDecisionState(
            reduced,
            reduced with { Commitments = ledger });

        Assert.True(staged.IsSuccess, staged.Witness?.Message);
        Assert.Empty(coordinator.CommittedState.Commitments.Histories);
        Assert.Single(coordinator.PendingState!.Commitments.Histories);

        var applied = coordinator.ApplyDecisionAcknowledgement(1);

        Assert.True(applied.IsSuccess, applied.Witness?.Message);
        Assert.Single(coordinator.CommittedState.Commitments.Histories);
        Assert.Null(coordinator.PendingState);
    }
}
