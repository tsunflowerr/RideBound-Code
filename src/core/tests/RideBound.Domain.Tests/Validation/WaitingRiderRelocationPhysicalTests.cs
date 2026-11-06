using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Validation;

namespace RideBound.Domain.Tests.Validation;

/// <summary>
/// The physical validator checks a candidate against the reduced state, where the moved rider
/// still belongs to the donor. Only the declared relocation of a waiting rider is exempt from the
/// reassignment and preservation guards; without it both guards fire exactly as before.
/// </summary>
public sealed class WaitingRiderRelocationPhysicalTests
{
    private static readonly WaitingRiderRelocation Move =
        new(TestData.RequestOne, TestData.VehicleOne, TestData.VehicleTwo);

    [Fact]
    public void The_receiver_may_carry_only_the_declared_waiting_rider()
    {
        var state = Waiting();
        var receiverRoute = Replace(state, TestData.VehicleTwo, [TestData.Pickup(), TestData.DropOff()]);

        Assert.True(Validate(state, TestData.VehicleTwo, receiverRoute, Move).IsFeasible);
        Assert.Equal(
            PhysicalViolationCodes.Reassignment,
            Validate(state, TestData.VehicleTwo, receiverRoute, null).Witness?.Code);

        foreach (var wrong in new[]
                 {
                     Move with { ReceiverVehicleId = new VehicleId("v-3") },
                     Move with { DonorVehicleId = new VehicleId("v-3") },
                     Move with { RequestId = new RequestId("r-other") },
                 })
        {
            Assert.Equal(
                PhysicalViolationCodes.Reassignment,
                Validate(state, TestData.VehicleTwo, receiverRoute, wrong).Witness?.Code);
        }
    }

    [Fact]
    public void An_accepted_or_onboard_rider_never_counts_as_relocated()
    {
        var accepted = TwoVehicles().AcceptRequest(TestData.RequestOne, TestData.VehicleOne).Value!;
        var route = Replace(accepted, TestData.VehicleTwo, [TestData.Pickup(), TestData.DropOff()]);

        Assert.Equal(
            PhysicalViolationCodes.Reassignment,
            Validate(accepted, TestData.VehicleTwo, route, Move).Witness?.Code);

        var onboard = Waiting()
            .ReachStop(TestData.VehicleOne, new StopId("pickup"), new PlanVersion(0), new NodePosition(TestData.NodeOne), 0).Value!
            .Board(TestData.VehicleOne, TestData.RequestOne, new PlanVersion(0), new SimTime(1200)).Value!;
        var donorWithoutDrop = Replace(onboard, TestData.VehicleOne, []);

        Assert.Equal(
            PhysicalViolationCodes.OnboardPreservation,
            Validate(onboard, TestData.VehicleOne, donorWithoutDrop, Move).Witness?.Code);
    }

    [Fact]
    public void The_donor_must_drop_the_declared_rider_completely()
    {
        var state = Waiting();
        var cleared = Replace(state, TestData.VehicleOne, []);
        // Changed routes (a waypoint added) that still carry the rider, fully or half.
        var stillCarries = Replace(
            state,
            TestData.VehicleOne,
            [TestData.Waypoint("extra", TestData.NodeZero), TestData.Pickup(), TestData.DropOff()]);
        var halfCarries = Replace(state, TestData.VehicleOne, [TestData.Pickup()]);

        Assert.True(Validate(state, TestData.VehicleOne, cleared, Move).IsFeasible);
        Assert.Equal(
            PhysicalViolationCodes.AcceptedPreservation,
            Validate(state, TestData.VehicleOne, cleared, null).Witness?.Code);

        foreach (var route in new[] { stillCarries, halfCarries })
        {
            var witness = Validate(state, TestData.VehicleOne, route, Move).Witness;

            Assert.Equal(PhysicalViolationCodes.AcceptedPreservation, witness?.Code);
            Assert.Equal("routeStops", witness?.Dimension);
        }
    }

    [Fact]
    public void The_receiver_must_carry_both_stops_of_the_declared_rider()
    {
        var state = Waiting();
        var nothing = Replace(state, TestData.VehicleTwo, [TestData.Waypoint()]);

        var witness = Validate(state, TestData.VehicleTwo, nothing, Move).Witness;

        Assert.Equal(PhysicalViolationCodes.AcceptedPreservation, witness?.Code);
        Assert.Equal(TestData.RequestOne, witness?.RequestId);
        Assert.False(Validate(
            state,
            TestData.VehicleTwo,
            Replace(state, TestData.VehicleTwo, [TestData.Pickup()]),
            Move).IsFeasible);
    }

    [Fact]
    public void Find_lists_exactly_the_waiting_riders_whose_only_change_is_the_vehicle()
    {
        var reduced = Waiting();
        var candidate = reduced
            .UpdateVehicleRoute(TestData.VehicleOne, TestData.Route(version: 1)).Value!
            .UpdateVehicleRoute(
                TestData.VehicleTwo,
                TestData.Route(mutable: [TestData.Pickup(), TestData.DropOff()], version: 1)).Value!
            .ReassignWaitingRequest(TestData.RequestOne, TestData.VehicleTwo).Value!;
        var cancelled = reduced.CancelAfterAcceptance(TestData.RequestOne).Value!;

        Assert.Equal([Move], WaitingRiderRelocation.Find(reduced, candidate));
        Assert.Empty(WaitingRiderRelocation.Find(reduced, reduced));
        Assert.Empty(WaitingRiderRelocation.Find(reduced, cancelled));
        // Read backwards, the same pair of states is the reverse move.
        Assert.Equal(
            [new WaitingRiderRelocation(TestData.RequestOne, TestData.VehicleTwo, TestData.VehicleOne)],
            WaitingRiderRelocation.Find(candidate, reduced));
    }

    private static RideBoundRun TwoVehicles()
    {
        var run = TestData.EmptyRun().AddRequest(TestData.PendingRequest()).Value!;
        run = run.BootstrapVehicle(TestData.EmptyVehicle(
            TestData.Route(mutable: [TestData.Pickup(), TestData.DropOff()]))).Value!;
        return run.BootstrapVehicle(TestData.EmptyVehicle(id: TestData.VehicleTwo)).Value!;
    }

    private static RideBoundRun Waiting() =>
        TwoVehicles()
            .AcceptRequest(TestData.RequestOne, TestData.VehicleOne).Value!
            .ConfirmWaitingPickup(TestData.RequestOne).Value!;

    private static RoutePlan Replace(RideBoundRun state, VehicleId vehicleId, RouteStop[] suffix)
    {
        var route = state.Vehicles[vehicleId].Route;
        return RoutePlan.Create(
            new PlanVersion(route.Version.Value + 1),
            route.ExecutedStopCount,
            route.FrozenPrefix,
            suffix).Value!;
    }

    private static PhysicalValidationResult Validate(
        RideBoundRun state,
        VehicleId vehicleId,
        RoutePlan candidate,
        WaitingRiderRelocation? relocation) =>
        new PhysicalPlanValidator().Validate(
            new PhysicalValidationContext(
                state,
                vehicleId,
                candidate,
                new Travel(),
                new SimTime(1000),
                Relocation: relocation));

    private sealed class Travel : ITravelTimeLookup
    {
        public bool TryGetTravelTime(NodeId fromNodeId, NodeId toNodeId, out Duration travelTime)
        {
            travelTime = new Duration(fromNodeId == toNodeId ? 0 : 100);
            return true;
        }
    }
}
