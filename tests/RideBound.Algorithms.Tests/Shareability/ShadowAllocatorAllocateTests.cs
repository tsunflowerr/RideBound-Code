using RideBound.Algorithms.Shareability;
using RideBound.Application.Scheduling;
using RideBound.Application.State;
using RideBound.Application.Travel;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Vehicles;

namespace RideBound.Algorithms.Tests.Shareability;

/// <summary>
/// Direct test of <see cref="ShadowAllocator.Allocate"/> (refactor safety net S3; the Runner calls it after every
/// decision that opens first promises). Tiny 4-node network and cell inputs of the final-config goldens. Vehicle
/// v-1 serves r-1 (promised earlier, n-1 -> n-3) and r-2 (first promise in this decision, n-1 -> n-2) on the route
/// p1, p2, d2, d1. The test rebuilds the score context from the documented rules and scores it with
/// <see cref="ShareabilityScorer"/> itself; the score values are the ones recorded at commit ebca912.
/// </summary>
public sealed class ShadowAllocatorAllocateTests
{
    private const long Now = 890_000;
    private const long MaxRide = 600_000;
    private static readonly RequestId First = new("r-1");
    private static readonly RequestId Opened = new("r-2");
    private static readonly VehicleId Vehicle = new("v-1");
    private static readonly NodeId[] Nodes = [new("n-0"), new("n-1"), new("n-2"), new("n-3")];
    private static readonly ushort[,] Seconds = { { 0, 1, 2, 7 }, { 1, 0, 1, 6 }, { 2, 1, 0, 5 }, { 7, 6, 5, 0 } };

    [Fact]
    public void Nothing_opened_returns_the_same_state_and_scores_nothing()
    {
        var (state, _) = State(ShadowAllocationClass.Allowed, onEdge: false);
        var allocator = new ShadowAllocator(Inputs(threshold: 0.01));

        var result = allocator.Allocate(state, [], new SimTime(Now));

        Assert.Same(state, result);
        Assert.Empty(allocator.LastScores);
    }

    [Theory]
    [InlineData(ShadowAllocationClass.Guaranteed, false)]
    [InlineData(ShadowAllocationClass.Allowed, false)]
    [InlineData(ShadowAllocationClass.Guaranteed, true)]
    [InlineData(ShadowAllocationClass.Allowed, true)]
    public void The_opened_rider_is_scored_on_the_documented_context_of_its_vehicle(
        ShadowAllocationClass coRiderClass,
        bool onEdge)
    {
        var (state, firstPromise) = State(coRiderClass, onEdge);
        var inputs = Inputs(threshold: 0.01);
        var allocator = new ShadowAllocator(inputs);

        var result = allocator.Allocate(state, [Opened], new SimTime(Now));

        var expected = new ShareabilityScorer(inputs.Network, Cell(inputs, state))
            .Score(Context(state, firstPromise, coRiderClass, onEdge));
        var score = Assert.Single(allocator.LastScores);
        Assert.Equal(Opened, score.Key);
        Assert.Equal(expected, score.Value);
        Assert.Equal(ShadowAllocation.From(expected.Score, 0.01), result.Commitments.ShadowAllocationOf(Opened));
        Assert.Equal(coRiderClass, result.Commitments.ShadowAllocationOf(First)!.Class);
        Assert.Equal(2, result.Commitments.ShadowAllocations.Count);
        Assert.Same(state.Run, result.Run);
    }

    [Theory]
    [InlineData(ShadowAllocationClass.Guaranteed, false, "0.0004169540890626198")]
    [InlineData(ShadowAllocationClass.Allowed, false, "0.0008339081781252396")]
    [InlineData(ShadowAllocationClass.Guaranteed, true, "0.0002501724534375719")]
    [InlineData(ShadowAllocationClass.Allowed, true, "0.0005003449068751438")]
    public void Scores_and_classes_are_the_ones_recorded_at_ebca912(
        ShadowAllocationClass coRiderClass,
        bool onEdge,
        string recordedScore)
    {
        var (state, _) = State(coRiderClass, onEdge);
        var score = double.Parse(recordedScore, System.Globalization.CultureInfo.InvariantCulture);

        var atScore = new ShadowAllocator(Inputs(threshold: score)).Allocate(state, [Opened], new SimTime(Now));
        var belowScore = new ShadowAllocator(Inputs(threshold: Math.BitDecrement(score)))
            .Allocate(state, [Opened], new SimTime(Now));

        Assert.Equal(
            new ShadowAllocation(ShadowAllocationClass.Guaranteed, recordedScore),
            atScore.Commitments.ShadowAllocationOf(Opened));
        Assert.Equal(
            new ShadowAllocation(ShadowAllocationClass.Allowed, recordedScore),
            belowScore.Commitments.ShadowAllocationOf(Opened));
    }

    private static ShadowAllocationInputs Inputs(double threshold)
    {
        var count = Nodes.Length;
        var distances = new ushort[count * count];
        var csrStart = new int[count + 1];
        var csrTo = new List<int>();
        var csrWeight = new List<int>();

        for (var from = 0; from < count; from++)
        {
            for (var to = 0; to < count; to++)
            {
                distances[(from * count) + to] = Seconds[from, to];

                if (from != to)
                {
                    csrTo.Add(to);
                    csrWeight.Add(Seconds[from, to]);
                }
            }

            csrStart[from + 1] = csrTo.Count;
        }

        return new ShadowAllocationInputs(
            ShareabilityNetwork.Create(count, distances, csrStart, [.. csrTo], [.. csrWeight], [0, 0, 1, 1], 2),
            Kappa: 3.0742501276388534,
            Factors: [1.0, 1.063821],
            PickupWindowMilliseconds: 600_000,
            StrideMilliseconds: 60_000,
            IntervalMilliseconds: 600_000,
            Alpha: 0.15,
            Phi: 0.11772821338238676,
            Sampled: 512,
            PriorOrigin: [3.0, 1.0],
            PriorDestination: [2.0, 4.0],
            Threshold: threshold,
            BinMilliseconds: 900_000,
            NetworkNodes: Enumerable.Range(0, count).ToDictionary(index => Nodes[index].Value, index => index));
    }

    /// <summary>The staged state after the decision that opened r-2's first promise.</summary>
    private static (OnlineState State, PromiseProjection FirstPromise) State(
        ShadowAllocationClass coRiderClass,
        bool onEdge)
    {
        var first = Request(First, arrival: 880_000, Nodes[1], Nodes[3]);
        var opened = Request(Opened, arrival: 885_000, Nodes[1], Nodes[2]);
        var route = RoutePlan.Create(
            new PlanVersion(3),
            0,
            [],
            [
                Stop("p1", Nodes[1], RouteStopKind.Pickup, First),
                Stop("p2", Nodes[1], RouteStopKind.Pickup, Opened),
                Stop("d2", Nodes[2], RouteStopKind.DropOff, Opened),
                Stop("d1", Nodes[3], RouteStopKind.DropOff, First),
            ]).Value!;
        VehiclePosition position = onEdge
            ? new EdgeProgressPosition(Nodes[0], Nodes[1], "e-0-1", 400)
            : new NodePosition(Nodes[0]);
        var vehicle = VehicleState.Create(Vehicle, 4, 0, position, [], [], route, 1).Value!;
        var run = RideBoundRun.Create(new RunIdentifier("shadow-run"), new ScenarioIdentifier("shadow-scenario"), new SimTime(0));
        run = run.AddRequest(first).Value!;
        run = run.AddRequest(opened).Value!;
        run = run.BootstrapVehicle(vehicle).Value!;
        run = run.AcceptRequest(First, Vehicle).Value!;
        run = run.AcceptRequest(Opened, Vehicle).Value!;
        run = run.AdvanceEpoch(1, new SimTime(Now)).Value!;
        var firstPromise = Projection(First, "p1", Nodes[1], "d1", Nodes[3], pickup: 884_000, drop: 902_000);
        var ledger = CommitmentLedger.Empty
            .OpenInitial("publication-1", firstPromise, 1, new SimTime(880_000), "INITIAL_ACCEPTANCE", 1).Ledger!
            .WithShadowAllocation(First, new ShadowAllocation(coRiderClass, "1"))
            .OpenInitial(
                "publication-2",
                Projection(Opened, "p2", Nodes[1], "d2", Nodes[2], pickup: 893_000, drop: 896_000),
                2,
                new SimTime(Now),
                "INITIAL_ACCEPTANCE",
                2).Ledger!;
        var travel = Travel();
        return (new OnlineState(run, travel, 3, travel.SnapshotHash, ledger), firstPromise);
    }

    /// <summary>The documented context (ShadowAllocator summary), built without the allocator.</summary>
    private static ShareabilityContext Context(
        OnlineState state,
        PromiseProjection firstPromise,
        ShadowAllocationClass coRiderClass,
        bool onEdge)
    {
        var vehicle = state.Run.Vehicles[Vehicle];
        var now = new SimTime(Now);
        var times = new RouteScheduleProjector()
            .Project(state.Run, vehicle, vehicle.Route, state.TravelTimes!, now)
            .Schedule!.Stops.Select(stop => (double)stop.ServiceStartTime.Milliseconds).ToArray();
        // Pickups of promised riders have no headroom; the opened rider's drop has its physical headroom; the
        // earlier rider's drop is capped by its remaining allowance (0 when guaranteed, slack * 1000 permille
        // when allowed, minus the 0 ms it has spent).
        var allowance = coRiderClass == ShadowAllocationClass.Guaranteed
            ? 0
            : CommitmentPolicy.SlackProportionalLimit(firstPromise, new Duration(MaxRide), 1000);
        var firstDrop = Math.Min(Math.Max(0, times[0] + MaxRide - times[3]), Math.Max(0, allowance - 0));
        ShareabilityStop[] stops =
        [
            new(1, true, times[0], 0, 1),
            new(1, true, times[1], 0, 1),
            new(2, false, times[2], Math.Max(0, times[1] + MaxRide - times[2]), 1),
            new(3, false, times[3], firstDrop, 1),
        ];
        var (startNode, startTime, arc) = onEdge
            ? (1, (double)state.TravelTimes!.Arrive(now, now, new Duration(RemainingOfEdge())).Milliseconds, new ShareabilityArc(0, 400))
            : (0, (double)Now, (ShareabilityArc?)null);
        return new ShareabilityContext(Now, (int)(Now / 900_000), startNode, startTime, arc, 0, 4, stops, 1, 2, 0);
    }

    private static ShareabilityCell Cell(ShadowAllocationInputs inputs, OnlineState state)
    {
        var requests = state.Run.Requests.Values.ToArray();
        int Zone(NodeId node) => inputs.Network.ZoneOf(inputs.NetworkNodes[node.Value]);
        var rates = ZoneDemandRates.Create(
            inputs.Network,
            requests.Select(value => (double)value.ArrivalTime.Milliseconds).ToArray(),
            requests.Select(value => Zone(value.OriginNodeId)).ToArray(),
            requests.Select(value => Zone(value.DestinationNodeId)).ToArray(),
            inputs.PriorOrigin,
            inputs.PriorDestination,
            inputs.Phi,
            inputs.Alpha,
            inputs.Sampled,
            inputs.IntervalMilliseconds);
        return new ShareabilityCell(
            inputs.Kappa,
            inputs.Factors,
            [0, 1, 2, 3],
            inputs.PickupWindowMilliseconds,
            inputs.StrideMilliseconds,
            rates,
            Oracle: false);
    }

    // The rest of the 3,000 ms edge n-0 -> n-1 at 400 permille, rounded up: 1,800 ms.
    private static long RemainingOfEdge() => 1_800;

    private static TravelTimeSnapshot Travel()
    {
        var arcs = new List<KeyValuePair<TravelArc, Duration>>();

        for (var from = 0; from < Nodes.Length; from++)
        {
            for (var to = 0; to < Nodes.Length; to++)
            {
                if (from != to)
                {
                    arcs.Add(new(new TravelArc(Nodes[from], Nodes[to]), new Duration(3_000L * Seconds[from, to])));
                }
            }
        }

        return TravelTimeSnapshot.Create(1, new string('b', 64), arcs).Value!;
    }

    private static RideRequest Request(RequestId id, long arrival, NodeId origin, NodeId destination) =>
        RideRequest.CreatePending(
            id,
            new SimTime(arrival),
            origin,
            destination,
            new SimTime(arrival),
            new SimTime(arrival + 600_000),
            new Duration(MaxRide),
            1,
            "standard",
            "uniform-v1").Value!;

    private static RouteStop Stop(string id, NodeId node, RouteStopKind kind, RequestId request) =>
        new(new StopId(id), node, kind, request, new Duration(0));

    private static PromiseProjection Projection(
        RequestId request,
        string pickupStop,
        NodeId pickupNode,
        string dropStop,
        NodeId dropNode,
        long pickup,
        long drop) =>
        new(
            request,
            Vehicle,
            new StopId(pickupStop),
            pickupNode,
            new StopId(dropStop),
            dropNode,
            new SimTime(pickup),
            new SimTime(drop),
            [
                new PromiseServiceToken(new StopId(pickupStop), request, RouteStopKind.Pickup),
                new PromiseServiceToken(new StopId(dropStop), request, RouteStopKind.DropOff),
            ]);
}
