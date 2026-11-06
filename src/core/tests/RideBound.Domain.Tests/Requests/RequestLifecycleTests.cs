using RideBound.Domain.Common;
using RideBound.Domain.Requests;

namespace RideBound.Domain.Tests.Requests;

public sealed class RequestLifecycleTests
{
    public static TheoryData<RequestLifecycle, string, bool> TransitionCases
    {
        get
        {
            var data = new TheoryData<RequestLifecycle, string, bool>();
            var allowed = new HashSet<(RequestLifecycle, string)>
            {
                (RequestLifecycle.Pending, "accept"),
                (RequestLifecycle.Pending, "reject"),
                (RequestLifecycle.Pending, "cancelBefore"),
                (RequestLifecycle.Accepted, "confirm"),
                (RequestLifecycle.Accepted, "cancelAfter"),
                (RequestLifecycle.WaitingPickup, "cancelAfter"),
                (RequestLifecycle.WaitingPickup, "board"),
                (RequestLifecycle.Onboard, "complete"),
            };

            foreach (var state in Enum.GetValues<RequestLifecycle>())
            {
                foreach (var operation in new[]
                         {
                             "accept",
                             "confirm",
                             "reject",
                             "cancelBefore",
                             "cancelAfter",
                             "board",
                             "complete",
                         })
                {
                    data.Add(state, operation, allowed.Contains((state, operation)));
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(TransitionCases))]
    public void Transition_table_is_explicit_and_total(
        RequestLifecycle initial,
        string operation,
        bool expectedSuccess)
    {
        var request = InState(initial);

        var result = operation switch
        {
            "accept" => request.Accept(TestData.VehicleOne),
            "confirm" => request.ConfirmWaitingPickup(),
            "reject" => request.Reject(),
            "cancelBefore" => request.CancelBeforeAcceptance(),
            "cancelAfter" => request.CancelAfterAcceptance(),
            "board" => request.Board(TestData.VehicleOne, new SimTime(1200)),
            "complete" => request.Complete(TestData.VehicleOne),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

        Assert.Equal(expectedSuccess, result.IsSuccess);

        if (!expectedSuccess)
        {
            Assert.Equal(
                RequestFailureCodes.InvalidLifecycleTransition,
                result.Failure?.Code);
            Assert.Equal(initial, request.Lifecycle);
        }
    }

    [Fact]
    public void Boarding_wrong_vehicle_returns_assignment_witness_without_mutation()
    {
        var request = InState(RequestLifecycle.WaitingPickup);

        var result = request.Board(TestData.VehicleTwo, new SimTime(1200));

        Assert.False(result.IsSuccess);
        Assert.Equal(RequestFailureCodes.AssignmentMismatch, result.Failure?.Code);
        Assert.Equal(RequestLifecycle.WaitingPickup, request.Lifecycle);
        Assert.Equal(TestData.VehicleOne, request.AssignedVehicleId);
    }

    [Theory]
    [InlineData(999)]
    [InlineData(2001)]
    public void Boarding_outside_the_pickup_window_is_rejected(long pickupTimeMs)
    {
        var request = InState(RequestLifecycle.WaitingPickup);

        var result = request.Board(
            TestData.VehicleOne,
            new SimTime(pickupTimeMs));

        Assert.False(result.IsSuccess);
        Assert.Equal(
            RequestFailureCodes.PickupTimeOutsideWindow,
            result.Failure?.Code);
        Assert.Equal(RequestLifecycle.WaitingPickup, request.Lifecycle);
    }

    [Fact]
    public void A_late_boarding_is_accepted_only_when_allowed_and_keeps_the_window()
    {
        var request = InState(RequestLifecycle.WaitingPickup);

        var late = request.Board(TestData.VehicleOne, new SimTime(2001), allowLatePickup: true);
        var early = request.Board(TestData.VehicleOne, new SimTime(999), allowLatePickup: true);

        Assert.True(late.IsSuccess, late.Failure?.Message);
        Assert.Equal(RequestLifecycle.Onboard, late.Value!.Lifecycle);
        Assert.Equal(new SimTime(2001), late.Value.ActualPickupTime);
        Assert.Equal(request.EarliestPickup, late.Value.EarliestPickup);
        Assert.Equal(request.LatestPickup, late.Value.LatestPickup);
        Assert.False(early.IsSuccess);
        Assert.Equal(RequestFailureCodes.PickupTimeOutsideWindow, early.Failure?.Code);
    }

    [Theory]
    [InlineData(RequestLifecycle.Onboard)]
    [InlineData(RequestLifecycle.Completed)]
    public void Rehydrating_a_late_pickup_requires_the_explicit_allowance(
        RequestLifecycle lifecycle)
    {
        var template = TestData.PendingRequest();

        DomainResult<RideRequest> Rehydrate(bool allow) =>
            RideRequest.Rehydrate(
                template.Id,
                template.ArrivalTime,
                template.OriginNodeId,
                template.DestinationNodeId,
                template.EarliestPickup,
                template.LatestPickup,
                template.MaxRideTime,
                template.PartySize,
                template.ServiceClass,
                template.CommitmentPolicyId,
                lifecycle,
                TestData.VehicleOne,
                new SimTime(template.LatestPickup.Milliseconds + 1),
                allow);

        Assert.False(Rehydrate(allow: false).IsSuccess);
        var allowed = Rehydrate(allow: true);
        Assert.True(allowed.IsSuccess, allowed.Failure?.Message);
        Assert.Equal(lifecycle, allowed.Value!.Lifecycle);
    }

    [Fact]
    public void Accepted_request_can_never_transition_to_rejected()
    {
        foreach (var lifecycle in new[]
                 {
                     RequestLifecycle.Accepted,
                     RequestLifecycle.WaitingPickup,
                     RequestLifecycle.Onboard,
                 })
        {
            var request = InState(lifecycle);
            var result = request.Reject();

            Assert.False(result.IsSuccess);
            Assert.Equal(lifecycle, request.Lifecycle);
        }
    }

    private static RideRequest InState(RequestLifecycle lifecycle)
    {
        var pending = TestData.PendingRequest();
        var accepted = pending.Accept(TestData.VehicleOne).Value!;
        var waiting = accepted.ConfirmWaitingPickup().Value!;
        var onboard = waiting.Board(TestData.VehicleOne, new SimTime(1200)).Value!;

        return lifecycle switch
        {
            RequestLifecycle.Pending => pending,
            RequestLifecycle.Accepted => accepted,
            RequestLifecycle.WaitingPickup => waiting,
            RequestLifecycle.Onboard => onboard,
            RequestLifecycle.Completed =>
                onboard.Complete(TestData.VehicleOne).Value!,
            RequestLifecycle.Rejected => pending.Reject().Value!,
            RequestLifecycle.CancelledBeforeAcceptance =>
                pending.CancelBeforeAcceptance().Value!,
            RequestLifecycle.CancelledAfterAcceptance =>
                accepted.CancelAfterAcceptance().Value!,
            _ => throw new ArgumentOutOfRangeException(nameof(lifecycle)),
        };
    }
}
