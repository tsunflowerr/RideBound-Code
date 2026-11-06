using RideBound.Application.Events;
using RideBound.Application.State;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Vehicles;

namespace RideBound.Application.Tests.State;

public sealed class EventReducerTests
{
    [Fact]
    public void Bootstrap_is_proposed_then_committed_only_by_matching_acknowledgement()
    {
        var coordinator = new EventReductionCoordinator(
            ApplicationTestData.InitialState());

        var proposed = coordinator.Propose(ApplicationTestData.BootstrapBatch());

        Assert.True(proposed.IsSuccess, proposed.Witness?.Message);
        Assert.Empty(coordinator.CommittedState.Run.Requests);
        Assert.Empty(coordinator.CommittedState.Run.Vehicles);
        Assert.Equal(0, coordinator.CommittedState.Run.AppliedEpoch);
        Assert.Single(coordinator.PendingState!.Run.Requests);
        Assert.Single(coordinator.PendingState.Run.Vehicles);
        Assert.NotNull(coordinator.PendingState.TravelTimes);

        var wrongAck = coordinator.ApplyDecisionAcknowledgement(2);

        Assert.False(wrongAck.IsSuccess);
        Assert.Equal(
            EventReductionFailureCodes.AcknowledgementMismatch,
            wrongAck.Witness?.Code);
        Assert.Equal(0, coordinator.CommittedState.Run.AppliedEpoch);

        var applied = coordinator.ApplyDecisionAcknowledgement(1);

        Assert.True(applied.IsSuccess);
        Assert.Equal(1, coordinator.CommittedState.Run.AppliedEpoch);
        Assert.Equal(4, coordinator.CommittedState.NextEventSequence);
        Assert.Null(coordinator.PendingState);
    }

    [Fact]
    public void Genesis_vehicle_cannot_preload_a_pending_request_into_its_route()
    {
        var time = new SimTime(1000);
        var request = ApplicationTestData.Request();
        var route = RoutePlan.Create(
            new PlanVersion(0),
            0,
            [],
            [
                new RouteStop(
                    new StopId("preloaded-pickup"),
                    request.OriginNodeId,
                    RouteStopKind.Pickup,
                    request.Id,
                    new Duration(0)),
                new RouteStop(
                    new StopId("preloaded-drop"),
                    request.DestinationNodeId,
                    RouteStopKind.DropOff,
                    request.Id,
                    new Duration(0)),
            ]).Value!;
        var vehicle = VehicleState.Create(
            ApplicationTestData.VehicleId,
            4,
            0,
            new NodePosition(ApplicationTestData.NodeZero),
            [],
            [],
            route,
            1).Value!;
        var batch = new InternalEventBatch(
            ApplicationTestData.RunId,
            ApplicationTestData.ScenarioId,
            1,
            time,
            [
                new RideBound.Application.Events.TravelTimesUpdated(
                    1,
                    time,
                    ApplicationTestData.Travel()),
                new RequestArrived(2, time, request),
                new VehicleAdvanced(3, time, vehicle),
            ]);

        var result = new EventReducer().Reduce(
            ApplicationTestData.InitialState(),
            batch);

        Assert.False(result.IsSuccess);
        Assert.Equal(RunFailureCodes.VehicleRiderMismatch, result.Witness?.Code);
        Assert.Equal(2, result.Witness?.EventIndex);
    }

    [Fact]
    public void Invalid_last_event_discards_every_prior_fold()
    {
        var initial = ApplicationTestData.InitialState();
        var reducer = new EventReducer();

        var result = reducer.Reduce(
            initial,
            ApplicationTestData.BootstrapBatch(
                appendUnknownVehicleEvent: true));

        Assert.False(result.IsSuccess);
        Assert.Equal("UNKNOWN_VEHICLE", result.Witness?.Code);
        Assert.Equal(3, result.Witness?.EventIndex);
        Assert.Equal(4, result.Witness?.EventSequence);
        Assert.Empty(initial.Run.Requests);
        Assert.Empty(initial.Run.Vehicles);
        Assert.Null(initial.TravelTimes);
        Assert.Equal(1, initial.NextEventSequence);
    }

    [Fact]
    public void Canonical_event_sequence_exhaustion_is_rejected_before_folding()
    {
        var initial = ApplicationTestData.InitialState() with
        {
            NextEventSequence = DomainLimits.MaxCanonicalInteger,
        };
        var time = new SimTime(1000);
        var batch = new InternalEventBatch(
            ApplicationTestData.RunId,
            ApplicationTestData.ScenarioId,
            1,
            time,
            [new TimerTick(DomainLimits.MaxCanonicalInteger, time)]);

        var result = new EventReducer().Reduce(initial, batch);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            EventReductionFailureCodes.InvalidEventSequence,
            result.Witness?.Code);
        Assert.Equal(0, result.Witness?.EventIndex);
    }

    [Fact]
    public void Same_state_and_batch_produce_equivalent_proposed_state()
    {
        var reducer = new EventReducer();
        var initial = ApplicationTestData.InitialState();
        var batch = ApplicationTestData.BootstrapBatch();

        var first = reducer.Reduce(initial, batch);
        var second = reducer.Reduce(initial, batch);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(
            first.ProposedState!.Run.AppliedEpoch,
            second.ProposedState!.Run.AppliedEpoch);
        Assert.Equal(
            first.ProposedState.NextEventSequence,
            second.ProposedState.NextEventSequence);
        Assert.Equal(
            first.ProposedState.TravelTimes!.SnapshotHash,
            second.ProposedState.TravelTimes!.SnapshotHash);
        Assert.Equal(
            first.ProposedState.Run.Requests.Keys,
            second.ProposedState.Run.Requests.Keys);
        Assert.True(
            first.ProposedState.Run.Vehicles[ApplicationTestData.VehicleId]
                .Route
                .IsSemanticallyEqual(
                    second.ProposedState.Run.Vehicles[
                        ApplicationTestData.VehicleId].Route));
        Assert.Empty(initial.Run.Requests);
    }

    [Fact]
    public void Second_epoch_reached_stop_advances_route_after_first_ack()
    {
        var coordinator = new EventReductionCoordinator(
            ApplicationTestData.InitialState());
        _ = coordinator.Propose(ApplicationTestData.BootstrapBatch());
        _ = coordinator.ApplyDecisionAcknowledgement(1);
        var time = new SimTime(1100);
        var batch = new InternalEventBatch(
            ApplicationTestData.RunId,
            ApplicationTestData.ScenarioId,
            2,
            time,
            [
                new VehicleReachedStop(
                    4,
                    time,
                    ApplicationTestData.VehicleId,
                    new StopId("waypoint"),
                    new PlanVersion(0),
                    new NodePosition(ApplicationTestData.NodeOne)),
                new TimerTick(5, time),
            ]);

        var result = coordinator.Propose(batch);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var route = result.ProposedState!.Run.Vehicles[
            ApplicationTestData.VehicleId].Route;
        Assert.Equal(1, route.ExecutedStopCount);
        Assert.Single(route.FrozenPrefix);
        Assert.Empty(route.MutableSuffix);
        Assert.Equal(1, coordinator.CommittedState.Run.AppliedEpoch);
    }

    [Fact]
    public void Gap_or_wrong_time_is_rejected_before_fold()
    {
        var initial = ApplicationTestData.InitialState();
        var time = new SimTime(1000);
        var gap = new InternalEventBatch(
            ApplicationTestData.RunId,
            ApplicationTestData.ScenarioId,
            1,
            time,
            [new TimerTick(2, time)]);
        var wrongTime = new InternalEventBatch(
            ApplicationTestData.RunId,
            ApplicationTestData.ScenarioId,
            1,
            time,
            [new TimerTick(1, new SimTime(1001))]);
        var reducer = new EventReducer();

        Assert.Equal(
            EventReductionFailureCodes.InvalidEventSequence,
            reducer.Reduce(initial, gap).Witness?.Code);
        Assert.Equal(
            EventReductionFailureCodes.InvalidEventTime,
            reducer.Reduce(initial, wrongTime).Witness?.Code);
        Assert.Equal(0, initial.Run.AppliedEpoch);
    }

    [Fact]
    public void Another_batch_cannot_be_proposed_while_ack_is_pending()
    {
        var coordinator = new EventReductionCoordinator(
            ApplicationTestData.InitialState());
        _ = coordinator.Propose(ApplicationTestData.BootstrapBatch());

        var second = coordinator.Propose(ApplicationTestData.BootstrapBatch());

        Assert.False(second.IsSuccess);
        Assert.Equal(
            EventReductionFailureCodes.PendingTransitionExists,
            second.Witness?.Code);
        Assert.Equal(0, coordinator.CommittedState.Run.AppliedEpoch);
    }

    [Fact]
    public void Initial_travel_snapshot_must_match_manifest_identity()
    {
        var initial = OnlineState.Create(
            RideBound.Domain.Runs.RideBoundRun.Create(
                ApplicationTestData.RunId,
                ApplicationTestData.ScenarioId,
                new SimTime(0)),
            new string('f', 64));

        var result = new EventReducer().Reduce(
            initial,
            ApplicationTestData.BootstrapBatch());

        Assert.False(result.IsSuccess);
        Assert.Equal(
            EventReductionFailureCodes.TravelSnapshotIdentityMismatch,
            result.Witness?.Code);
        Assert.Equal(0, result.Witness?.EventIndex);
        Assert.Equal(1, result.Witness?.EventSequence);
        Assert.Empty(initial.Run.Vehicles);
        Assert.Null(initial.TravelTimes);
    }

    [Fact]
    public void Pending_offer_decline_and_cancellation_fold_in_exact_order()
    {
        var secondId = new RequestId("r-2");
        var second = RideRequest.CreatePending(
            secondId,
            new SimTime(1000),
            ApplicationTestData.NodeOne,
            ApplicationTestData.NodeTwo,
            new SimTime(1000),
            new SimTime(2000),
            new Duration(1000),
            1,
            "standard",
            "uniform-v1").Value!;
        var run = ApplicationTestData.InitialState().Run
            .AddRequest(ApplicationTestData.Request()).Value!
            .AddRequest(second).Value!
            .BootstrapVehicle(ApplicationTestData.Vehicle()).Value!
            .AdvanceEpoch(1, new SimTime(1000)).Value!;
        var state = Committed(run);
        var time = new SimTime(1100);
        var batch = new InternalEventBatch(
            ApplicationTestData.RunId,
            ApplicationTestData.ScenarioId,
            2,
            time,
            [
                new OfferDeclined(4, time, ApplicationTestData.RequestId),
                new RequestCancelledBeforeAcceptance(5, time, secondId),
            ]);

        var result = new EventReducer().Reduce(state, batch);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.Equal(
            RequestLifecycle.Rejected,
            result.ProposedState!.Run.Requests[
                ApplicationTestData.RequestId].Lifecycle);
        Assert.Equal(
            RequestLifecycle.CancelledBeforeAcceptance,
            result.ProposedState.Run.Requests[secondId].Lifecycle);
        Assert.Equal(6, result.ProposedState.NextEventSequence);
        Assert.Equal(
            RequestLifecycle.Pending,
            state.Run.Requests[ApplicationTestData.RequestId].Lifecycle);
    }

    [Fact]
    public void Provisional_accepted_offer_decline_removes_assignment_and_route_stops()
    {
        var request = ApplicationTestData.Request();
        var route = RoutePlan.Create(
            new PlanVersion(1),
            0,
            [],
            [
                new RouteStop(
                    new StopId("offered-pickup"),
                    request.OriginNodeId,
                    RouteStopKind.Pickup,
                    request.Id,
                    new Duration(0)),
                new RouteStop(
                    new StopId("offered-drop"),
                    request.DestinationNodeId,
                    RouteStopKind.DropOff,
                    request.Id,
                    new Duration(0)),
            ]).Value!;
        var run = ApplicationTestData.InitialState().Run
            .AddRequest(request).Value!
            .BootstrapVehicle(ApplicationTestData.Vehicle()).Value!
            .UpdateVehicleRoute(ApplicationTestData.VehicleId, route).Value!
            .AcceptRequest(request.Id, ApplicationTestData.VehicleId).Value!
            .AdvanceEpoch(1, new SimTime(1_000)).Value!;
        var state = Committed(run);
        var time = new SimTime(1_100);

        var result = new EventReducer().Reduce(
            state,
            new InternalEventBatch(
                ApplicationTestData.RunId,
                ApplicationTestData.ScenarioId,
                2,
                time,
                [new OfferDeclined(4, time, request.Id)]));

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var proposed = result.ProposedState!.Run;
        Assert.Equal(
            RequestLifecycle.CancelledAfterAcceptance,
            proposed.Requests[request.Id].Lifecycle);
        var vehicle = proposed.Vehicles[ApplicationTestData.VehicleId];
        Assert.DoesNotContain(request.Id, vehicle.AcceptedRequestIds);
        Assert.DoesNotContain(
            vehicle.Route.RemainingStops,
            stop => stop.RequestId == request.Id);
    }

    [Fact]
    public void Booking_boarding_and_alighting_update_request_and_vehicle_together()
    {
        var route = RoutePlan.Create(
            new PlanVersion(0),
            0,
            [],
            [
                new RouteStop(
                    new StopId("pickup"),
                    ApplicationTestData.NodeOne,
                    RouteStopKind.Pickup,
                    ApplicationTestData.RequestId,
                    new Duration(0)),
                new RouteStop(
                    new StopId("drop"),
                    ApplicationTestData.NodeTwo,
                    RouteStopKind.DropOff,
                    ApplicationTestData.RequestId,
                    new Duration(0)),
            ]).Value!;
        var vehicle = VehicleState.Create(
            ApplicationTestData.VehicleId,
            4,
            0,
            new NodePosition(ApplicationTestData.NodeZero),
            [],
            [],
            route,
            1).Value!;
        var run = ApplicationTestData.InitialState().Run
            .AddRequest(ApplicationTestData.Request()).Value!
            .BootstrapVehicle(vehicle).Value!
            .AcceptRequest(
                ApplicationTestData.RequestId,
                ApplicationTestData.VehicleId).Value!
            .AdvanceEpoch(1, new SimTime(1000)).Value!;
        var state = Committed(run);
        var time = new SimTime(1100);
        var batch = new InternalEventBatch(
            ApplicationTestData.RunId,
            ApplicationTestData.ScenarioId,
            2,
            time,
            [
                new BookingConfirmed(
                    4,
                    time,
                    ApplicationTestData.RequestId),
                new PassengerBoarded(
                    5,
                    time,
                    ApplicationTestData.VehicleId,
                    ApplicationTestData.RequestId,
                    new PlanVersion(0)),
                new PassengerAlighted(
                    6,
                    time,
                    ApplicationTestData.VehicleId,
                    ApplicationTestData.RequestId,
                    new PlanVersion(0)),
            ]);

        var result = new EventReducer().Reduce(state, batch);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.Equal(
            RequestLifecycle.Completed,
            result.ProposedState!.Run.Requests[
                ApplicationTestData.RequestId].Lifecycle);
        var proposedVehicle = result.ProposedState.Run.Vehicles[
            ApplicationTestData.VehicleId];
        Assert.Equal(0, proposedVehicle.OccupiedSeats);
        Assert.Empty(proposedVehicle.OnboardRequestIds);
        Assert.Empty(proposedVehicle.AcceptedRequestIds);
        Assert.Equal(
            RequestLifecycle.Accepted,
            state.Run.Requests[ApplicationTestData.RequestId].Lifecycle);
    }

    [Fact]
    public void A_late_boarding_fails_by_default_and_is_recorded_as_a_fact_when_asked()
    {
        // Window [1000, 2000] ms; the rider boards at 2500 ms. By default the batch fails as
        // before. With recording on, the rider is onboard, the window is unchanged, and the
        // 500 ms gap is one separate record.
        var (state, batch) = BoardingAt(2_500);

        var rejected = new EventReducer().Reduce(state, batch);
        var recorded = new EventReducer(recordLatePickups: true).Reduce(state, batch);

        Assert.False(rejected.IsSuccess);
        Assert.Equal(RequestFailureCodes.PickupTimeOutsideWindow, rejected.Witness!.Code);
        Assert.True(recorded.IsSuccess, recorded.Witness?.Message);
        var rider = recorded.ProposedState!.Run.Requests[ApplicationTestData.RequestId];
        Assert.Equal(RequestLifecycle.Onboard, rider.Lifecycle);
        Assert.Equal(new SimTime(2_500), rider.ActualPickupTime);
        Assert.Equal(ApplicationTestData.Request().LatestPickup, rider.LatestPickup);
        var late = Assert.Single(recorded.ProposedState.Incidents.LatePickups);
        Assert.Equal(ApplicationTestData.RequestId, late.RequestId);
        Assert.Equal(ApplicationTestData.VehicleId, late.VehicleId);
        Assert.Equal(500, late.LatenessMilliseconds);
        Assert.Equal(5, late.SourceEventSequence);
        Assert.Equal(2, late.RecordedEpoch);
        Assert.Empty(state.Incidents.LatePickups);
    }

    [Fact]
    public void An_on_time_boarding_records_nothing_even_when_recording_is_on()
    {
        var (state, batch) = BoardingAt(2_000);

        var expected = new EventReducer().Reduce(state, batch);
        var actual = new EventReducer(recordLatePickups: true).Reduce(state, batch);

        Assert.True(expected.IsSuccess, expected.Witness?.Message);
        Assert.True(actual.IsSuccess, actual.Witness?.Message);
        Assert.Empty(actual.ProposedState!.Incidents.LatePickups);
        Assert.Same(state.Incidents, actual.ProposedState.Incidents);
        Assert.Equal(
            expected.ProposedState!.Run.Requests[ApplicationTestData.RequestId],
            actual.ProposedState.Run.Requests[ApplicationTestData.RequestId]);
    }

    [Fact]
    public void A_boarding_before_the_earliest_pickup_is_rejected_even_when_recording()
    {
        var (state, batch) = BoardingAt(999, bookingAt: 999);

        var result = new EventReducer(recordLatePickups: true).Reduce(state, batch);

        Assert.False(result.IsSuccess);
        Assert.Equal(RequestFailureCodes.PickupTimeOutsideWindow, result.Witness!.Code);
    }

    /// <summary>
    /// An accepted rider on a two-stop route, then one batch that confirms the booking and
    /// boards the rider at <paramref name="boardAt"/>.
    /// </summary>
    private static (OnlineState State, InternalEventBatch Batch) BoardingAt(
        long boardAt,
        long bookingAt = 1_000)
    {
        var route = RoutePlan.Create(
            new PlanVersion(0),
            0,
            [],
            [
                new RouteStop(
                    new StopId("pickup"),
                    ApplicationTestData.NodeOne,
                    RouteStopKind.Pickup,
                    ApplicationTestData.RequestId,
                    new Duration(0)),
                new RouteStop(
                    new StopId("drop"),
                    ApplicationTestData.NodeTwo,
                    RouteStopKind.DropOff,
                    ApplicationTestData.RequestId,
                    new Duration(0)),
            ]).Value!;
        var vehicle = VehicleState.Create(
            ApplicationTestData.VehicleId,
            4,
            0,
            new NodePosition(ApplicationTestData.NodeZero),
            [],
            [],
            route,
            1).Value!;
        var run = ApplicationTestData.InitialState().Run
            .AddRequest(ApplicationTestData.Request()).Value!
            .BootstrapVehicle(vehicle).Value!
            .AcceptRequest(
                ApplicationTestData.RequestId,
                ApplicationTestData.VehicleId).Value!
            .AdvanceEpoch(1, new SimTime(Math.Min(bookingAt, boardAt))).Value!;
        var time = new SimTime(boardAt);
        var batch = new InternalEventBatch(
            ApplicationTestData.RunId,
            ApplicationTestData.ScenarioId,
            2,
            time,
            [
                new BookingConfirmed(4, time, ApplicationTestData.RequestId),
                new PassengerBoarded(
                    5,
                    time,
                    ApplicationTestData.VehicleId,
                    ApplicationTestData.RequestId,
                    new PlanVersion(0)),
            ]);
        return (Committed(run), batch);
    }

    [Fact]
    public void Accepted_cancellation_removes_only_mutable_stops_atomically()
    {
        var route = RoutePlan.Create(
            new PlanVersion(0),
            0,
            [],
            [
                new RouteStop(
                    new StopId("pickup"),
                    ApplicationTestData.NodeOne,
                    RouteStopKind.Pickup,
                    ApplicationTestData.RequestId,
                    new Duration(0)),
                new RouteStop(
                    new StopId("drop"),
                    ApplicationTestData.NodeTwo,
                    RouteStopKind.DropOff,
                    ApplicationTestData.RequestId,
                    new Duration(0)),
            ]).Value!;
        var vehicle = VehicleState.Create(
            ApplicationTestData.VehicleId,
            4,
            0,
            new NodePosition(ApplicationTestData.NodeZero),
            [],
            [],
            route,
            1).Value!;
        var run = ApplicationTestData.InitialState().Run
            .AddRequest(ApplicationTestData.Request()).Value!
            .BootstrapVehicle(vehicle).Value!
            .AcceptRequest(
                ApplicationTestData.RequestId,
                ApplicationTestData.VehicleId).Value!
            .AdvanceEpoch(1, new SimTime(1000)).Value!;
        var state = Committed(run);
        var time = new SimTime(1100);
        var batch = new InternalEventBatch(
            ApplicationTestData.RunId,
            ApplicationTestData.ScenarioId,
            2,
            time,
            [
                new RequestCancelledAfterAcceptance(
                    4,
                    time,
                    ApplicationTestData.RequestId),
            ]);

        var result = new EventReducer().Reduce(state, batch);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.Equal(
            RequestLifecycle.CancelledAfterAcceptance,
            result.ProposedState!.Run.Requests[
                ApplicationTestData.RequestId].Lifecycle);
        Assert.Empty(
            result.ProposedState.Run.Vehicles[
                ApplicationTestData.VehicleId].Route.MutableSuffix);
        Assert.Equal(
            0,
            state.Run.Vehicles[
                ApplicationTestData.VehicleId].Route.Version.Value);
        Assert.Equal(
            1,
            result.ProposedState.Run.Vehicles[
                ApplicationTestData.VehicleId].Route.Version.Value);
    }

    [Fact]
    public void Travel_snapshot_version_gap_discards_batch()
    {
        var run = ApplicationTestData.InitialState().Run
            .AddRequest(ApplicationTestData.Request()).Value!
            .BootstrapVehicle(ApplicationTestData.Vehicle()).Value!
            .AdvanceEpoch(1, new SimTime(1000)).Value!;
        var state = Committed(run);
        var time = new SimTime(1100);
        var batch = new InternalEventBatch(
            ApplicationTestData.RunId,
            ApplicationTestData.ScenarioId,
            2,
            time,
            [
                new TravelTimesUpdated(
                    4,
                    time,
                    ApplicationTestData.Travel(3, 'c')),
            ]);

        var result = new EventReducer().Reduce(state, batch);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            RideBound.Application.Travel.TravelFailureCodes.StaleSnapshot,
            result.Witness?.Code);
        Assert.Equal(1, state.TravelTimes?.Version);
        Assert.Equal(1, state.Run.AppliedEpoch);
    }

    private static OnlineState Committed(RideBoundRun run) =>
        new(
            run,
            ApplicationTestData.Travel(),
            4,
            new string('a', 64),
            RideBound.Domain.Commitments.CommitmentLedger.Empty);
}
