using RideBound.Algorithms.Candidates;
using RideBound.Application.State;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Vehicles;

namespace RideBound.Algorithms.Tests.Candidates;

/// <summary>
/// G2 candidate generation. The waiting rider w rides vehicle-1 (pickup node-1, drop node-2);
/// vehicle-2 is idle at node-0. Travel is 100 ms + 10 ms per node index step.
/// </summary>
public sealed class WaitingRiderRelocationGenerationTests
{
    private static readonly RequestId Waiting = new("waiting");

    [Fact]
    public void Off_or_with_no_eligible_rider_the_ordinary_sets_are_unchanged()
    {
        var accepted = Fixture(confirm: false);
        var off = Generate(accepted, relocation: null);
        var onButNoneEligible = Generate(accepted, new WaitingRiderRelocationGenerationOptions());

        Assert.True(off.IsSuccess);
        Assert.Null(off.Diagnostics!.RelocationLosses);
        Assert.Empty(onButNoneEligible.Diagnostics!.RelocationLosses!);
        Assert.Equal(Ids(off), Ids(onButNoneEligible));
        Assert.All(
            onButNoneEligible.VehicleCandidates!.SelectMany(set => set.Candidates),
            candidate => Assert.Null(candidate.RelocationRole));

        // Confirmed, but G2 off: still no relocation candidate.
        var waitingOff = Generate(Fixture(), relocation: null);
        Assert.All(
            waitingOff.VehicleCandidates!.SelectMany(set => set.Candidates),
            candidate => Assert.Null(candidate.RelocatedRequestId));
    }

    [Fact]
    public void A_waiting_rider_yields_the_donor_only_route_and_one_receiver_per_insertion()
    {
        var state = Fixture();
        var result = Generate(state, new WaitingRiderRelocationGenerationOptions());

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var donors = Relocation(result, RelocationRole.Donor);
        var receivers = Relocation(result, RelocationRole.Receiver);

        var donor = Assert.Single(donors);
        Assert.Equal(AlgorithmTestData.VehicleOne, donor.VehicleId);
        Assert.Empty(donor.Route.MutableSuffix);
        Assert.Empty(donor.NewRequestIds);
        Assert.False(donor.IsNoOp);
        Assert.Equal(Waiting, donor.RelocatedRequestId);

        var receiver = Assert.Single(receivers);
        Assert.Equal(AlgorithmTestData.VehicleTwo, receiver.VehicleId);
        Assert.Empty(receiver.NewRequestIds);
        Assert.Equal(
            state.Run.Vehicles[AlgorithmTestData.VehicleOne].Route.MutableSuffix,
            receiver.Route.MutableSuffix);
        Assert.Equal(1, receiver.Route.Version.Value);

        var loss = Assert.Single(result.Diagnostics!.RelocationLosses!);
        Assert.Equal(AlgorithmTestData.VehicleOne, loss.DonorVehicleId);
        Assert.Equal((1, 1, 0), (loss.EligibleRiderCount, loss.ConsideredRiderCount, loss.OmittedRiderCount));
        Assert.Equal(1, loss.EvaluatedReceiverRouteCount);
        Assert.Equal(1, loss.FeasibleReceiverCount);
        Assert.Equal(1, loss.FeasibleDonorCount);
        Assert.True(result.Diagnostics.IsComplete);
        AssertIdsAreUnique(result);
    }

    [Fact]
    public void Donors_take_the_pending_request_and_never_carry_the_moved_rider()
    {
        var pending = AlgorithmTestData.PendingRequest("pending", AlgorithmTestData.NodeThree, AlgorithmTestData.NodeOne);
        var result = Generate(Fixture(extraPending: pending), new WaitingRiderRelocationGenerationOptions());

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var donors = Relocation(result, RelocationRole.Donor);

        Assert.Contains(donors, value => value.NewRequestIds.Count == 0);
        Assert.Contains(donors, value => value.NewRequestIds.SequenceEqual([pending.Id]));
        Assert.All(donors, value => Assert.DoesNotContain(value.Route.RemainingStops, stop => stop.RequestId == Waiting));
        Assert.All(donors, value => Assert.Equal(AlgorithmTestData.VehicleOne, value.VehicleId));
        Assert.All(Relocation(result, RelocationRole.Receiver), value => Assert.Empty(value.NewRequestIds));
        AssertIdsAreUnique(result);
    }

    [Fact]
    public void Every_insertion_position_on_the_receiver_is_evaluated()
    {
        // vehicle-2 already serves another rider (2 stops): (2+1)(2+2)/2 = 6 insertions.
        var other = AlgorithmTestData.PendingRequest("other", AlgorithmTestData.NodeThree, AlgorithmTestData.NodeOne);
        var result = Generate(Fixture(receiverRider: other), new WaitingRiderRelocationGenerationOptions());

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var loss = Assert.Single(result.Diagnostics!.RelocationLosses!, value => value.DonorVehicleId == AlgorithmTestData.VehicleOne);
        Assert.Equal(6, loss.EvaluatedReceiverRouteCount);
        Assert.Equal(
            loss.FeasibleReceiverCount,
            Relocation(result, RelocationRole.Receiver).Count(value => value.RelocatedRequestId == Waiting));
        Assert.All(
            Relocation(result, RelocationRole.Receiver).Where(value => value.RelocatedRequestId == Waiting),
            value => Assert.Equal(4, value.Route.MutableSuffix.Count));
    }

    [Fact]
    public void A_target_cap_keeps_the_nearest_vehicles_by_travel_to_the_origin()
    {
        // vehicle-2 at node-0 (110 ms to node-1), vehicle-3 at node-3 (120 ms): cap 1 keeps vehicle-2.
        var third = AlgorithmTestData.Vehicle(new VehicleId("vehicle-3"), position: AlgorithmTestData.NodeThree);
        var state = Fixture(extraVehicle: third);
        var all = Generate(state, new WaitingRiderRelocationGenerationOptions());
        var nearest = Generate(state, new WaitingRiderRelocationGenerationOptions(maximumTargetVehicles: 1));

        Assert.Equal(
            [AlgorithmTestData.VehicleTwo, third.Id],
            Relocation(all, RelocationRole.Receiver).Select(value => value.VehicleId).Distinct().OrderBy(value => value.Value, StringComparer.Ordinal).ToArray());
        Assert.Equal(
            [AlgorithmTestData.VehicleTwo],
            Relocation(nearest, RelocationRole.Receiver).Select(value => value.VehicleId).Distinct().ToArray());
        // The target cap is a neighbourhood choice, not an omission.
        Assert.True(nearest.Diagnostics!.IsComplete);
    }

    [Fact]
    public void A_rider_without_a_feasible_receiver_yields_no_candidate()
    {
        // The pickup window closes at 1,050 ms; vehicle-1 waits at the origin, vehicle-2 needs 110 ms.
        var result = Generate(
            Fixture(latestPickup: 1_050, donorPosition: AlgorithmTestData.NodeOne),
            new WaitingRiderRelocationGenerationOptions());

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.Empty(Relocation(result, RelocationRole.Donor));
        Assert.Empty(Relocation(result, RelocationRole.Receiver));
        var loss = Assert.Single(result.Diagnostics!.RelocationLosses!);
        Assert.Equal(1, loss.RidersWithoutFeasibleReceiver);
        Assert.Equal(1, loss.EvaluatedReceiverRouteCount);
        Assert.Equal(0, loss.FeasibleReceiverCount);
    }

    [Fact]
    public void A_rider_cap_omits_later_riders_with_a_witness_and_exact_small_fails()
    {
        var second = AlgorithmTestData.PendingRequest("waiting-2", AlgorithmTestData.NodeOne, AlgorithmTestData.NodeTwo);
        var state = Fixture(secondWaiting: second);
        var capped = Generate(state, new WaitingRiderRelocationGenerationOptions(maximumRidersPerVehicle: 1));

        Assert.True(capped.IsSuccess, capped.Witness?.Message);
        var omission = Assert.Single(
            capped.Diagnostics!.Omissions,
            value => value.Code == CandidateGenerationFailureCodes.RelocationRiderBoundOmission);
        Assert.Equal(1, omission.Count);
        Assert.Equal([second.Id], omission.RequestIds);
        Assert.False(capped.Diagnostics.IsComplete);
        Assert.DoesNotContain(
            Relocation(capped, RelocationRole.Donor),
            value => value.RelocatedRequestId == second.Id);
        Assert.True(capped.VehicleCandidates!.Single(set => set.VehicleId == AlgorithmTestData.VehicleOne).WasTruncated);

        var exact = new InsertionCandidateGenerator().Generate(
            state,
            new CandidateGenerationOptions(
                100_000,
                2,
                exactSmallMode: true,
                waitingRiderRelocation: new WaitingRiderRelocationGenerationOptions(maximumRidersPerVehicle: 1)));

        Assert.False(exact.IsSuccess);
        Assert.Equal(CandidateGenerationFailureCodes.ExactSmallRelocationRiderBoundExceeded, exact.Witness!.Code);
    }

    [Fact]
    public void Relocation_options_survive_the_repair_cap_copy_and_reject_origin_hold()
    {
        var options = new CandidateGenerationOptions(
            100,
            2,
            exactSmallMode: false,
            waitingRiderRelocation: new WaitingRiderRelocationGenerationOptions(2, 3));

        Assert.Equal(options.WaitingRiderRelocation, options.WithRepairRequestCap(4).WaitingRiderRelocation);
        Assert.Throws<ArgumentException>(
            () => new CandidateGenerationOptions(
                100,
                2,
                exactSmallMode: false,
                CandidateScheduleStrategy.OriginHoldRelocatedWait,
                waitingRiderRelocation: new WaitingRiderRelocationGenerationOptions()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WaitingRiderRelocationGenerationOptions(0, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WaitingRiderRelocationGenerationOptions(null, 0));
    }

    private static CandidateGenerationResult Generate(
        OnlineState state,
        WaitingRiderRelocationGenerationOptions? relocation) =>
        new InsertionCandidateGenerator().Generate(
            state,
            new CandidateGenerationOptions(
                100,
                2,
                exactSmallMode: false,
                waitingRiderRelocation: relocation));

    private static InsertionCandidate[] Relocation(CandidateGenerationResult result, RelocationRole role) =>
        result.VehicleCandidates!
            .SelectMany(set => set.Candidates)
            .Where(candidate => candidate.RelocationRole == role)
            .ToArray();

    private static string[] Ids(CandidateGenerationResult result) =>
        result.VehicleCandidates!
            .SelectMany(set => set.Candidates.Select(candidate => candidate.CandidateId))
            .ToArray();

    private static void AssertIdsAreUnique(CandidateGenerationResult result)
    {
        var ids = Ids(result);
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// w on vehicle-1 (accepted, and confirmed unless told otherwise); vehicle-2 idle at node-0,
    /// optionally serving another accepted rider; optional extra vehicle, pending request and
    /// second waiting rider on vehicle-1.
    /// </summary>
    private static OnlineState Fixture(
        bool confirm = true,
        RideRequest? extraPending = null,
        RideRequest? receiverRider = null,
        VehicleState? extraVehicle = null,
        RideRequest? secondWaiting = null,
        long latestPickup = 10_000,
        NodeId? donorPosition = null)
    {
        var waiting = AlgorithmTestData.PendingRequest(
            Waiting.Value,
            AlgorithmTestData.NodeOne,
            AlgorithmTestData.NodeTwo,
            latestPickup: latestPickup);
        var donorStops = new List<RouteStop> { Stop(waiting, RouteStopKind.Pickup), Stop(waiting, RouteStopKind.DropOff) };

        if (secondWaiting is not null)
        {
            donorStops.Add(Stop(secondWaiting, RouteStopKind.Pickup));
            donorStops.Add(Stop(secondWaiting, RouteStopKind.DropOff));
        }

        var receiverStops = receiverRider is null
            ? []
            : new[] { Stop(receiverRider, RouteStopKind.Pickup), Stop(receiverRider, RouteStopKind.DropOff) };
        var requests = new List<RideRequest> { waiting };
        requests.AddRange(new[] { extraPending, receiverRider, secondWaiting }.OfType<RideRequest>());
        var vehicles = new List<VehicleState>
        {
            AlgorithmTestData.Vehicle(mutableSuffix: donorStops, position: donorPosition),
            AlgorithmTestData.Vehicle(AlgorithmTestData.VehicleTwo, mutableSuffix: receiverStops),
        };

        if (extraVehicle is not null)
        {
            vehicles.Add(extraVehicle);
        }

        var state = AlgorithmTestData.CreateState(requests, vehicles);
        var run = state.Run.AcceptRequest(waiting.Id, AlgorithmTestData.VehicleOne).Value!;

        if (confirm)
        {
            run = run.ConfirmWaitingPickup(waiting.Id).Value!;
        }

        if (receiverRider is not null)
        {
            run = run.AcceptRequest(receiverRider.Id, AlgorithmTestData.VehicleTwo).Value!;
            run = run.ConfirmWaitingPickup(receiverRider.Id).Value!;
        }

        if (secondWaiting is not null)
        {
            run = run.AcceptRequest(secondWaiting.Id, AlgorithmTestData.VehicleOne).Value!;
            run = run.ConfirmWaitingPickup(secondWaiting.Id).Value!;
        }

        return state with { Run = run };
    }

    private static RouteStop Stop(RideRequest request, RouteStopKind kind) =>
        new(
            new StopId($"{request.Id.Value}-{kind}"),
            kind == RouteStopKind.Pickup ? request.OriginNodeId : request.DestinationNodeId,
            kind,
            request.Id,
            new Duration(0));
}
