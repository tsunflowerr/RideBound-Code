using RideBound.Application.Commitments;
using RideBound.Application.Promises;
using RideBound.Application.Scheduling;
using RideBound.Application.State;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Validation;
using RideBound.Domain.Vehicles;

namespace RideBound.Application.Tests.Commitments;

public sealed class CommitmentDecisionValidatorTests
{
    [Fact]
    public void Rebuilds_delta_from_route_and_rejects_candidate_over_hard_budget()
    {
        var fixture = CommitmentValidatorFixtures.WithHardLimit(0);

        var result = new CommitmentDecisionValidator().Validate(
            fixture.Context);

        Assert.False(result.IsValid);
        var witness = Assert.Single(
            result.Witnesses,
            value => value.Dimension == "drop_eta_total_ms");
        Assert.Equal(CommitmentValidationStage.Budget, witness.Stage);
        Assert.Equal(CommitmentFailureCodes.BudgetExceeded, witness.Code);
        Assert.Equal("drop_eta_total_ms", witness.Dimension);
        Assert.Equal(0, witness.Limit);
        Assert.Equal(10, witness.Delta);
        Assert.Single(
            fixture.Context.ReducedState.Commitments.Histories[
                ApplicationTestData.RequestId].Entries);
    }

    [Fact]
    public void Valid_revision_is_recomputed_and_appended_to_a_new_ledger()
    {
        var fixture = CommitmentValidatorFixtures.WithHardLimit(10);

        var result = new CommitmentDecisionValidator().Validate(
            fixture.Context);

        Assert.True(result.IsValid, result.Witnesses.FirstOrDefault()?.Message);
        var publication = Assert.Single(result.Publications);
        Assert.Equal(
            10,
            publication.Entry.Deltas.DecisionInduced.DropEtaTotalMs);
        Assert.Equal(2, publication.Entry.PublishedPromise.Version.Value);
        Assert.Equal(10, publication.Entry.BudgetAfter.DropEtaTotalMs);
        Assert.Equal(
            2,
            result.ValidatedState!.Commitments.Histories[
                ApplicationTestData.RequestId].Entries.Count);
        Assert.Single(
            fixture.Context.ReducedState.Commitments.Histories[
                ApplicationTestData.RequestId].Entries);
    }

    [Fact]
    public void Physical_failure_precedes_commitment_budget_checks()
    {
        var fixture = CommitmentValidatorFixtures.WithHardLimit(0);
        var vehicle = fixture.Context.CandidateState.Run.Vehicles[
            ApplicationTestData.VehicleId];
        var invalidRoute = RoutePlan.Create(
            new PlanVersion(1),
            0,
            [],
            [vehicle.Route.MutableSuffix[1]]).Value!;
        var invalidRun = fixture.Context.ReducedState.Run.UpdateVehicleRoute(
            ApplicationTestData.VehicleId,
            invalidRoute).Value!;
        var invalidContext = fixture.Context with
        {
            CandidateState = fixture.Context.CandidateState with
            {
                Run = invalidRun,
            },
        };

        var result = new CommitmentDecisionValidator().Validate(invalidContext);

        Assert.False(result.IsValid);
        Assert.All(
            result.Witnesses,
            value => Assert.Equal(CommitmentValidationStage.Physical, value.Stage));
        Assert.Equal("ROUTE_CONNECTIVITY", result.Witnesses[0].Code);
    }

    [Fact]
    public void Candidate_cannot_mutate_existing_request_definition()
    {
        var fixture = CommitmentValidatorFixtures.WithHardLimit(10);
        var candidateRun = fixture.Context.CandidateState.Run;
        var current = candidateRun.Requests[ApplicationTestData.RequestId];
        var mutated = RideRequest.Rehydrate(
            current.Id,
            current.ArrivalTime,
            current.OriginNodeId,
            current.DestinationNodeId,
            current.EarliestPickup,
            current.LatestPickup,
            current.MaxRideTime,
            current.PartySize,
            "silently-changed-service-class",
            current.CommitmentPolicyId,
            current.Lifecycle,
            current.AssignedVehicleId,
            current.ActualPickupTime).Value!;
        var mutatedRun = RideBoundRun.Rehydrate(
            candidateRun.Id,
            candidateRun.ScenarioId,
            candidateRun.AppliedEpoch,
            candidateRun.SimulationTime,
            candidateRun.Requests.Values.Select(
                value => value.Id == mutated.Id ? mutated : value),
            candidateRun.Vehicles.Values).Value!;

        var result = new CommitmentDecisionValidator().Validate(
            fixture.Context with
            {
                CandidateState = fixture.Context.CandidateState with
                {
                    Run = mutatedRun,
                },
            });

        Assert.False(result.IsValid);
        var witness = Assert.Single(result.Witnesses);
        Assert.Equal(CommitmentValidationStage.State, witness.Stage);
        Assert.Equal("COMMITMENT_STATE_BOUNDARY_MISMATCH", witness.Code);
    }

    [Fact]
    public void Candidate_cannot_hide_unaccepted_pending_stops_in_the_route()
    {
        var fixture = CommitmentValidatorFixtures.WithHardLimit(10);
        var pending = RideRequest.CreatePending(
            new RequestId("r-pending"),
            new SimTime(1_000),
            ApplicationTestData.NodeTwo,
            ApplicationTestData.NodeZero,
            new SimTime(1_000),
            new SimTime(2_000),
            new Duration(1_000),
            1,
            "standard",
            "uniform-v1").Value!;
        var beforeRun = fixture.Context.BeforeEventState.Run.AddRequest(
            pending).Value!;
        var reducedRun = fixture.Context.ReducedState.Run.AddRequest(
            pending).Value!;
        var currentRoute = reducedRun.Vehicles[
            ApplicationTestData.VehicleId].Route;
        var hiddenRoute = currentRoute.ReplaceMutableSuffix(
            currentRoute.MutableSuffix.Concat(
                [
                    new RouteStop(
                        new StopId("hidden-pickup"),
                        pending.OriginNodeId,
                        RouteStopKind.Pickup,
                        pending.Id,
                        new Duration(0)),
                    new RouteStop(
                        new StopId("hidden-drop"),
                        pending.DestinationNodeId,
                        RouteStopKind.DropOff,
                        pending.Id,
                        new Duration(0)),
                ])).Value!;
        var candidateRun = reducedRun.UpdateVehicleRoute(
            ApplicationTestData.VehicleId,
            hiddenRoute).Value!;

        var result = new CommitmentDecisionValidator().Validate(
            fixture.Context with
            {
                BeforeEventState = fixture.Context.BeforeEventState with
                {
                    Run = beforeRun,
                },
                ReducedState = fixture.Context.ReducedState with
                {
                    Run = reducedRun,
                },
                CandidateState = fixture.Context.CandidateState with
                {
                    Run = candidateRun,
                },
            });

        Assert.False(result.IsValid);
        var witness = Assert.Single(result.Witnesses);
        Assert.Equal(CommitmentValidationStage.State, witness.Stage);
        Assert.Equal("COMMITMENT_STATE_BOUNDARY_MISMATCH", witness.Code);
    }

    [Fact]
    public void Booking_confirmation_trigger_keeps_offer_provisional_then_opens_v1_once()
    {
        var request = ApplicationTestData.Request();
        var emptyRoute = RoutePlan.Create(
            new PlanVersion(0),
            0,
            [],
            []).Value!;
        var vehicle = VehicleState.Create(
            ApplicationTestData.VehicleId,
            4,
            0,
            new NodePosition(ApplicationTestData.NodeZero),
            [],
            [],
            emptyRoute,
            0).Value!;
        var beforeRun = RideBoundRun.Create(
            ApplicationTestData.RunId,
            ApplicationTestData.ScenarioId,
            new SimTime(1_000));
        beforeRun = beforeRun.BootstrapVehicle(vehicle).Value!;
        var travel = ApplicationTestData.Travel();
        var before = new OnlineState(
            beforeRun,
            travel,
            1,
            travel.SnapshotHash,
            CommitmentLedger.Empty);
        var reducedRun = beforeRun.AddRequest(request).Value!;
        reducedRun = reducedRun.AdvanceEpoch(1, new SimTime(1_000)).Value!;
        var reduced = before with
        {
            Run = reducedRun,
            NextEventSequence = 2,
        };
        var assignedRoute = RoutePlan.Create(
            new PlanVersion(1),
            0,
            [],
            [
                new RouteStop(
                    new StopId("booking-pickup"),
                    request.OriginNodeId,
                    RouteStopKind.Pickup,
                    request.Id,
                    new Duration(0)),
                new RouteStop(
                    new StopId("booking-drop"),
                    request.DestinationNodeId,
                    RouteStopKind.DropOff,
                    request.Id,
                    new Duration(0)),
            ]).Value!;
        var candidateRun = reducedRun.UpdateVehicleRoute(
            vehicle.Id,
            assignedRoute).Value!;
        candidateRun = candidateRun.AcceptRequest(request.Id, vehicle.Id).Value!;
        var candidate = reduced with { Run = candidateRun };
        var policy = new CommitmentPolicy(
            request.CommitmentPolicyId,
            CommitmentBudgetBasis.DecisionInduced,
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    DomainLimits.MaxCanonicalInteger,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1, null));
        var policies = new CommitmentPolicyCatalog([policy]);
        var validator = new CommitmentDecisionValidator();

        var offered = validator.Validate(
            new CommitmentValidationContext(
                before,
                reduced,
                candidate,
                policies,
                CommitmentValidatorFixtures.EmptyDistances.Instance,
                "booking-offer-scope",
                1,
                InitialPromiseTrigger:
                    InitialPromiseTrigger.BookingConfirmation));

        Assert.True(offered.IsValid, offered.Witnesses.FirstOrDefault()?.Message);
        Assert.Empty(offered.Publications);
        Assert.Empty(offered.ValidatedState!.Commitments.Histories);
        Assert.Equal(
            RequestLifecycle.Accepted,
            offered.ValidatedState.Run.Requests[request.Id].Lifecycle);

        var beforeConfirmation = offered.ValidatedState;
        var confirmedRun = beforeConfirmation.Run.ConfirmWaitingPickup(
            request.Id).Value!;
        confirmedRun = confirmedRun.AdvanceEpoch(2, new SimTime(1_000)).Value!;
        var confirmed = beforeConfirmation with
        {
            Run = confirmedRun,
            NextEventSequence = 3,
        };
        var published = validator.Validate(
            new CommitmentValidationContext(
                beforeConfirmation,
                confirmed,
                confirmed,
                policies,
                CommitmentValidatorFixtures.EmptyDistances.Instance,
                "booking-confirmation-scope",
                2,
                InitialPromiseTrigger:
                    InitialPromiseTrigger.BookingConfirmation));

        Assert.True(
            published.IsValid,
            published.Witnesses.FirstOrDefault()?.Message);
        var publication = Assert.Single(published.Publications);
        Assert.Equal(
            CommitmentLedgerEntryKind.InitialPromise,
            publication.Entry.Kind);
        Assert.Equal(1, publication.Entry.PublishedPromise.Version.Value);
        Assert.Equal("INITIAL_BOOKING_CONFIRMATION", publication.Entry.ReasonCode);
        Assert.Equal(2, publication.Entry.SourceEventSequence);
        Assert.Single(published.ValidatedState!.Commitments.Histories);
    }

    [Fact]
    public void Booking_and_boarding_in_one_batch_open_v1_from_realized_pickup()
    {
        var fixture = CreateBookingOfferFixture();
        var offered = fixture.Validator.Validate(fixture.OfferContext);
        Assert.True(offered.IsValid, offered.Witnesses.FirstOrDefault()?.Message);
        var beforeConfirmation = offered.ValidatedState!;
        var confirmedRun = beforeConfirmation.Run.ConfirmWaitingPickup(
            fixture.Request.Id).Value!;
        confirmedRun = confirmedRun.ReachStop(
            fixture.Vehicle.Id,
            new StopId("booking-pickup"),
            new PlanVersion(1),
            new NodePosition(fixture.Request.OriginNodeId),
            2).Value!;
        confirmedRun = confirmedRun.Board(
            fixture.Vehicle.Id,
            fixture.Request.Id,
            new PlanVersion(1),
            new SimTime(1_000)).Value!;
        confirmedRun = confirmedRun.AdvanceEpoch(2, new SimTime(1_000)).Value!;
        var reduced = beforeConfirmation with
        {
            Run = confirmedRun,
            NextEventSequence = 5,
        };

        var published = fixture.Validator.Validate(
            new CommitmentValidationContext(
                beforeConfirmation,
                reduced,
                reduced,
                fixture.Policies,
                CommitmentValidatorFixtures.EmptyDistances.Instance,
                "booking-and-boarding-scope",
                4,
                InitialPromiseTrigger:
                    InitialPromiseTrigger.BookingConfirmation));

        Assert.True(
            published.IsValid,
            published.Witnesses.FirstOrDefault()?.Message);
        var publication = Assert.Single(published.Publications);
        Assert.Equal("INITIAL_BOOKING_CONFIRMATION", publication.Entry.ReasonCode);
        Assert.Equal(
            new StopId("booking-pickup"),
            publication.Entry.PublishedPromise.Projection.PickupStopId);
        Assert.Equal(
            1_000,
            publication.Entry.PublishedPromise.Projection.PickupEta.Milliseconds);
    }

    private static BookingOfferFixture CreateBookingOfferFixture()
    {
        var request = ApplicationTestData.Request();
        var emptyRoute = RoutePlan.Create(
            new PlanVersion(0),
            0,
            [],
            []).Value!;
        var vehicle = VehicleState.Create(
            ApplicationTestData.VehicleId,
            4,
            0,
            new NodePosition(ApplicationTestData.NodeZero),
            [],
            [],
            emptyRoute,
            0).Value!;
        var beforeRun = RideBoundRun.Create(
            ApplicationTestData.RunId,
            ApplicationTestData.ScenarioId,
            new SimTime(1_000));
        beforeRun = beforeRun.BootstrapVehicle(vehicle).Value!;
        var travel = ApplicationTestData.Travel();
        var before = new OnlineState(
            beforeRun,
            travel,
            1,
            travel.SnapshotHash,
            CommitmentLedger.Empty);
        var reducedRun = beforeRun.AddRequest(request).Value!;
        reducedRun = reducedRun.AdvanceEpoch(1, new SimTime(1_000)).Value!;
        var reduced = before with
        {
            Run = reducedRun,
            NextEventSequence = 2,
        };
        var assignedRoute = RoutePlan.Create(
            new PlanVersion(1),
            0,
            [],
            [
                new RouteStop(
                    new StopId("booking-pickup"),
                    request.OriginNodeId,
                    RouteStopKind.Pickup,
                    request.Id,
                    new Duration(0)),
                new RouteStop(
                    new StopId("booking-drop"),
                    request.DestinationNodeId,
                    RouteStopKind.DropOff,
                    request.Id,
                    new Duration(0)),
            ]).Value!;
        var candidateRun = reducedRun.UpdateVehicleRoute(
            vehicle.Id,
            assignedRoute).Value!;
        candidateRun = candidateRun.AcceptRequest(request.Id, vehicle.Id).Value!;
        var candidate = reduced with { Run = candidateRun };
        var policy = new CommitmentPolicy(
            request.CommitmentPolicyId,
            CommitmentBudgetBasis.DecisionInduced,
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    DomainLimits.MaxCanonicalInteger,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1, null));
        var policies = new CommitmentPolicyCatalog([policy]);
        var validator = new CommitmentDecisionValidator();
        var context = new CommitmentValidationContext(
            before,
            reduced,
            candidate,
            policies,
            CommitmentValidatorFixtures.EmptyDistances.Instance,
            "booking-offer-scope",
            1,
            InitialPromiseTrigger:
                InitialPromiseTrigger.BookingConfirmation);

        return new BookingOfferFixture(
            request,
            vehicle,
            policies,
            validator,
            context);
    }

    private sealed record BookingOfferFixture(
        RideRequest Request,
        VehicleState Vehicle,
        CommitmentPolicyCatalog Policies,
        CommitmentDecisionValidator Validator,
        CommitmentValidationContext OfferContext);

}
