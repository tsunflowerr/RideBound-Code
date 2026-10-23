using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Vehicles;

namespace RideBound.Domain.Tests.Runs;

/// <summary>
/// Waiting-rider relocation (G2) at the domain level: the routes are applied first, then one
/// atomic reassignment moves the request and both accepted sets together.
/// </summary>
public sealed class WaitingRiderRelocationTests
{
    [Fact]
    public void A_waiting_rider_moves_with_both_accepted_sets_and_the_result_rehydrates()
    {
        var moved = Moved(Waiting());

        var result = moved.ReassignWaitingRequest(TestData.RequestOne, TestData.VehicleTwo);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var run = result.Value!;
        var request = run.Requests[TestData.RequestOne];
        Assert.Equal(RequestLifecycle.WaitingPickup, request.Lifecycle);
        Assert.Equal(TestData.VehicleTwo, request.AssignedVehicleId);
        Assert.Empty(run.Vehicles[TestData.VehicleOne].AcceptedRequestIds);
        Assert.Equal([TestData.RequestOne], run.Vehicles[TestData.VehicleTwo].AcceptedRequestIds);
        Assert.Equal(moved.Vehicles[TestData.VehicleOne].Route, run.Vehicles[TestData.VehicleOne].Route);
        Assert.Equal(moved.Vehicles[TestData.VehicleTwo].Route, run.Vehicles[TestData.VehicleTwo].Route);
        Assert.Equal(TestData.VehicleOne, moved.Requests[TestData.RequestOne].AssignedVehicleId);

        var rehydrated = RideBoundRun.Rehydrate(
            run.Id,
            run.ScenarioId,
            run.AppliedEpoch,
            run.SimulationTime,
            run.Requests.Values,
            run.Vehicles.Values);
        Assert.True(rehydrated.IsSuccess, rehydrated.Failure?.Message);

        // The moved rider now boards the receiver, not the donor.
        var atPickup = run.ReachStop(
            TestData.VehicleTwo,
            new StopId("pickup"),
            new PlanVersion(1),
            new NodePosition(TestData.NodeOne),
            0).Value!;
        Assert.True(atPickup.Board(TestData.VehicleTwo, TestData.RequestOne, new PlanVersion(1), new SimTime(1200)).IsSuccess);
        Assert.Equal(
            RequestFailureCodes.AssignmentMismatch,
            run.Board(TestData.VehicleOne, TestData.RequestOne, new PlanVersion(1), new SimTime(1200)).Failure?.Code);
    }

    [Fact]
    public void Only_a_confirmed_waiting_rider_moves()
    {
        var accepted = TwoVehicles().AcceptRequest(TestData.RequestOne, TestData.VehicleOne).Value!;
        var onboard = Waiting()
            .ReachStop(TestData.VehicleOne, new StopId("pickup"), new PlanVersion(0), new NodePosition(TestData.NodeOne), 0).Value!
            .Board(TestData.VehicleOne, TestData.RequestOne, new PlanVersion(0), new SimTime(1200)).Value!;
        var cancelled = Waiting().CancelAfterAcceptance(TestData.RequestOne).Value!;

        foreach (var run in new[] { accepted, onboard, cancelled })
        {
            var result = run.ReassignWaitingRequest(TestData.RequestOne, TestData.VehicleTwo);

            Assert.False(result.IsSuccess);
            Assert.Equal(RequestFailureCodes.InvalidLifecycleTransition, result.Failure?.Code);
        }

        var pending = TwoVehicles().ReassignWaitingRequest(TestData.RequestOne, TestData.VehicleTwo);
        Assert.Equal(RequestFailureCodes.InvalidLifecycleTransition, pending.Failure?.Code);
    }

    [Fact]
    public void A_move_to_the_same_vehicle_or_an_unknown_id_is_rejected()
    {
        var run = Moved(Waiting());

        Assert.Equal(
            RequestFailureCodes.AssignmentMismatch,
            run.ReassignWaitingRequest(TestData.RequestOne, TestData.VehicleOne).Failure?.Code);
        Assert.Equal(
            RunFailureCodes.UnknownRequest,
            run.ReassignWaitingRequest(new RequestId("r-unknown"), TestData.VehicleTwo).Failure?.Code);
        Assert.Equal(
            RunFailureCodes.UnknownVehicle,
            run.ReassignWaitingRequest(TestData.RequestOne, new VehicleId("v-unknown")).Failure?.Code);
    }

    [Fact]
    public void A_half_applied_move_fails_closed_and_changes_nothing()
    {
        var waiting = Waiting();
        // Receiver route applied, donor route still carries the rider.
        var donorNotCleared = waiting.UpdateVehicleRoute(
            TestData.VehicleTwo,
            TestData.Route(mutable: [TestData.Pickup(), TestData.DropOff()], version: 1)).Value!;
        // Donor route cleared, receiver route lacks the rider.
        var receiverMissing = waiting.UpdateVehicleRoute(
            TestData.VehicleOne,
            TestData.Route(version: 1)).Value!;
        // Receiver carries the drop before the pickup.
        var receiverReversed = receiverMissing.UpdateVehicleRoute(
            TestData.VehicleTwo,
            TestData.Route(mutable: [TestData.DropOff(), TestData.Pickup()], version: 1)).Value!;
        // Receiver carries only the pickup.
        var receiverHalf = receiverMissing.UpdateVehicleRoute(
            TestData.VehicleTwo,
            TestData.Route(mutable: [TestData.Pickup()], version: 1)).Value!;

        var donor = donorNotCleared.ReassignWaitingRequest(TestData.RequestOne, TestData.VehicleTwo);
        Assert.Equal(VehicleFailureCodes.RiderStateMismatch, donor.Failure?.Code);
        Assert.Equal("routeStops", donor.Failure?.Dimension);

        foreach (var run in new[] { receiverMissing, receiverReversed, receiverHalf })
        {
            var result = run.ReassignWaitingRequest(TestData.RequestOne, TestData.VehicleTwo);

            Assert.Equal(RunFailureCodes.VehicleRiderMismatch, result.Failure?.Code);
            Assert.Equal("routeStops", result.Failure?.Dimension);
            Assert.Equal(TestData.VehicleOne, run.Requests[TestData.RequestOne].AssignedVehicleId);
            Assert.Contains(TestData.RequestOne, run.Vehicles[TestData.VehicleOne].AcceptedRequestIds);
        }
    }

    [Fact]
    public void A_rider_whose_pickup_is_in_the_donors_frozen_prefix_cannot_move()
    {
        // The frozen prefix cannot change, so the donor keeps the pickup and the move fails closed.
        var frozenRoute = TestData.Route(frozen: [TestData.Pickup()], mutable: [TestData.DropOff()]);
        var run = TwoVehicles(frozenRoute)
            .AcceptRequest(TestData.RequestOne, TestData.VehicleOne).Value!
            .ConfirmWaitingPickup(TestData.RequestOne).Value!;
        run = run.UpdateVehicleRoute(
            TestData.VehicleOne,
            TestData.Route(frozen: [TestData.Pickup()], version: 1)).Value!;
        run = run.UpdateVehicleRoute(
            TestData.VehicleTwo,
            TestData.Route(mutable: [TestData.Pickup(), TestData.DropOff()], version: 1)).Value!;

        var result = run.ReassignWaitingRequest(TestData.RequestOne, TestData.VehicleTwo);

        Assert.False(result.IsSuccess);
        Assert.Equal(VehicleFailureCodes.RiderStateMismatch, result.Failure?.Code);
    }

    [Fact]
    public void Release_alone_requires_an_assigned_waiting_rider_without_remaining_stops()
    {
        var vehicle = TestData.EmptyVehicle(TestData.Route(mutable: [TestData.Pickup(), TestData.DropOff()]))
            .Assign(TestData.RequestOne).Value!;

        Assert.Equal(
            VehicleFailureCodes.RiderStateMismatch,
            vehicle.Release(new RequestId("r-other")).Failure?.Code);
        Assert.Equal("routeStops", vehicle.Release(TestData.RequestOne).Failure?.Dimension);

        var cleared = vehicle.UpdateRoute(TestData.Route(version: 1)).Value!;
        var released = cleared.Release(TestData.RequestOne);

        Assert.True(released.IsSuccess);
        Assert.Empty(released.Value!.AcceptedRequestIds);
        Assert.Same(cleared.Route, released.Value.Route);
    }

    private static RideBoundRun TwoVehicles(RoutePlan? donorRoute = null)
    {
        var run = TestData.EmptyRun().AddRequest(TestData.PendingRequest()).Value!;
        run = run.BootstrapVehicle(TestData.EmptyVehicle(
            donorRoute ?? TestData.Route(mutable: [TestData.Pickup(), TestData.DropOff()]))).Value!;
        return run.BootstrapVehicle(TestData.EmptyVehicle(id: TestData.VehicleTwo)).Value!;
    }

    private static RideBoundRun Waiting() =>
        TwoVehicles()
            .AcceptRequest(TestData.RequestOne, TestData.VehicleOne).Value!
            .ConfirmWaitingPickup(TestData.RequestOne).Value!;

    /// <summary>Both routes applied: the donor without the rider, the receiver with it.</summary>
    private static RideBoundRun Moved(RideBoundRun waiting) =>
        waiting
            .UpdateVehicleRoute(TestData.VehicleOne, TestData.Route(version: 1)).Value!
            .UpdateVehicleRoute(
                TestData.VehicleTwo,
                TestData.Route(mutable: [TestData.Pickup(), TestData.DropOff()], version: 1)).Value!;
}
