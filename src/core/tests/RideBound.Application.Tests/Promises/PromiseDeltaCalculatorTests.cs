using RideBound.Application.Promises;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Routes;
using RideBound.Domain.Validation;

namespace RideBound.Application.Tests.Promises;

public sealed class PromiseDeltaCalculatorTests
{
    private static readonly RequestId Subject = new("r-1");
    private static readonly RequestId IncumbentA = new("r-2");
    private static readonly RequestId IncumbentB = new("r-3");

    [Fact]
    public void Three_way_delta_is_separate_and_covers_every_dimension()
    {
        var old = Projection(
            "v-1",
            "pickup",
            "n-1",
            "drop",
            "n-2",
            10_000,
            20_000,
            OldOrder());
        var exogenous = Projection(
            "v-1",
            "pickup",
            "n-1",
            "drop",
            "n-2",
            11_000,
            21_000,
            OldOrder());
        var proposed = Projection(
            "v-2",
            "pickup-new",
            "n-3",
            "drop-new",
            "n-4",
            10_500,
            20_500,
            NewOrder());
        var previous = new PublishedPromise(
            new PromiseVersion(1),
            1,
            new SimTime(1_000),
            old);

        var result = new PromiseDeltaCalculator().Calculate(
            previous,
            exogenous,
            proposed,
            new MaterialRevisionRule(600, 60_000),
            new Distances(
                new Dictionary<(string, string), long>
                {
                    [("n-1", "n-3")] = 100,
                    [("n-2", "n-4")] = 200,
                }));

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(1000, result.Deltas!.Exogenous.PickupEtaTotalMs);
        Assert.Equal(500, result.Deltas.DecisionInduced.PickupEtaTotalMs);
        Assert.Equal(500, result.Deltas.Visible.PickupEtaTotalMs);
        Assert.Equal(1000, result.Deltas.Exogenous.DropEtaTotalMs);
        Assert.Equal(500, result.Deltas.DecisionInduced.DropEtaTotalMs);
        Assert.Equal(500, result.Deltas.Visible.DropEtaTotalMs);
        Assert.NotEqual(
            result.Deltas.Exogenous.PickupEtaTotalMs
                + result.Deltas.DecisionInduced.PickupEtaTotalMs,
            result.Deltas.Visible.PickupEtaTotalMs);
        Assert.Equal(1, result.Deltas.DecisionInduced.VehicleSwitchCount);
        Assert.Equal(100, result.Deltas.DecisionInduced.PickupStopRelocationMm);
        Assert.Equal(1, result.Deltas.DecisionInduced.PickupStopSwitchCount);
        Assert.Equal(200, result.Deltas.DecisionInduced.DropStopRelocationMm);
        Assert.Equal(1, result.Deltas.DecisionInduced.DropStopSwitchCount);
        Assert.Equal(
            1,
            result.Deltas.DecisionInduced.IncumbentOrderInversionCount);
        Assert.Equal(
            1,
            result.Deltas.DecisionInduced.PrePickupInsertedStopCount);
        Assert.Equal(
            1,
            result.Deltas.Exogenous.MaterialEtaRevisionCount);
        Assert.Equal(
            0,
            result.Deltas.DecisionInduced.MaterialEtaRevisionCount);
    }

    [Fact]
    public void Changed_stop_without_distance_returns_exact_dimension_witness()
    {
        var old = Projection(
            "v-1",
            "pickup",
            "n-1",
            "drop",
            "n-2",
            10_000,
            20_000,
            SubjectOnlyOrder("pickup", "drop"));
        var changed = Projection(
            "v-1",
            "pickup-new",
            "n-missing",
            "drop",
            "n-2",
            10_000,
            20_000,
            SubjectOnlyOrder("pickup-new", "drop"));
        var previous = new PublishedPromise(
            new PromiseVersion(1),
            1,
            new SimTime(1_000),
            old);

        var result = new PromiseDeltaCalculator().Calculate(
            previous,
            old,
            changed,
            new MaterialRevisionRule(1_000, null),
            new Distances(
                new Dictionary<(string From, string To), long>()));

        Assert.False(result.IsSuccess);
        Assert.Equal(
            CommitmentFailureCodes.StopDistanceRequired,
            result.Failure?.Code);
        Assert.Equal("pickup_stop_relocation_mm", result.Failure?.Dimension);
    }

    [Fact]
    public void Recreated_incumbent_stop_is_not_treated_as_the_same_order_token()
    {
        var before = Projection(
            "v-1",
            "pickup",
            "n-1",
            "drop",
            "n-2",
            10_000,
            20_000,
            [
                Token("a-old", IncumbentA, RouteStopKind.Pickup),
                Token("b", IncumbentB, RouteStopKind.Pickup),
                Token("pickup", Subject, RouteStopKind.Pickup),
                Token("drop", Subject, RouteStopKind.DropOff),
            ]);
        var after = Projection(
            "v-1",
            "pickup",
            "n-1",
            "drop",
            "n-2",
            10_000,
            20_000,
            [
                Token("b", IncumbentB, RouteStopKind.Pickup),
                Token("a-new", IncumbentA, RouteStopKind.Pickup),
                Token("pickup", Subject, RouteStopKind.Pickup),
                Token("drop", Subject, RouteStopKind.DropOff),
            ]);
        var previous = new PublishedPromise(
            new PromiseVersion(1),
            1,
            new SimTime(1_000),
            before);

        var result = new PromiseDeltaCalculator().Calculate(
            previous,
            before,
            after,
            new MaterialRevisionRule(1_000, null),
            new Distances(new Dictionary<(string, string), long>()));

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(
            0,
            result.Deltas!.DecisionInduced.IncumbentOrderInversionCount);
    }

    [Fact]
    public void Every_commitment_dimension_has_a_killing_projection_mutation()
    {
        var before = Projection(
            "v-1",
            "pickup",
            "n-1",
            "drop",
            "n-2",
            10_000,
            20_000,
            OldOrder());
        var mutations = new[]
        {
            Case(
                CommitmentDimension.PickupEtaTotalMs,
                Projection("v-1", "pickup", "n-1", "drop", "n-2",
                    10_001, 20_000, OldOrder()),
                threshold: 1_000),
            Case(
                CommitmentDimension.DropEtaTotalMs,
                Projection("v-1", "pickup", "n-1", "drop", "n-2",
                    10_000, 20_001, OldOrder()),
                threshold: 1_000),
            Case(
                CommitmentDimension.MaterialEtaRevisionCount,
                Projection("v-1", "pickup", "n-1", "drop", "n-2",
                    10_100, 20_000, OldOrder()),
                threshold: 100),
            Case(
                CommitmentDimension.VehicleSwitchCount,
                Projection("v-2", "pickup", "n-1", "drop", "n-2",
                    10_000, 20_000, OldOrder())),
            Case(
                CommitmentDimension.PickupStopRelocationMm,
                Projection("v-1", "pickup", "n-3", "drop", "n-2",
                    10_000, 20_000, OldOrder())),
            Case(
                CommitmentDimension.PickupStopSwitchCount,
                Projection("v-1", "pickup-new", "n-1", "drop", "n-2",
                    10_000, 20_000, SubjectPickup("pickup-new"))),
            Case(
                CommitmentDimension.DropStopRelocationMm,
                Projection("v-1", "pickup", "n-1", "drop", "n-4",
                    10_000, 20_000, OldOrder())),
            Case(
                CommitmentDimension.DropStopSwitchCount,
                Projection("v-1", "pickup", "n-1", "drop-new", "n-2",
                    10_000, 20_000, SubjectDrop("drop-new"))),
            Case(
                CommitmentDimension.IncumbentOrderInversionCount,
                Projection("v-1", "pickup", "n-1", "drop", "n-2",
                    10_000, 20_000, InvertedIncumbents())),
            Case(
                CommitmentDimension.PrePickupInsertedStopCount,
                Projection("v-1", "pickup", "n-1", "drop", "n-2",
                    10_000, 20_000, InsertedBeforePickup())),
        };
        var previous = new PublishedPromise(
            new PromiseVersion(1),
            1,
            new SimTime(1_000),
            before);
        var distances = new Distances(
            new Dictionary<(string, string), long>
            {
                [("n-1", "n-3")] = 17,
                [("n-2", "n-4")] = 19,
            });

        foreach (var mutation in mutations)
        {
            var result = new PromiseDeltaCalculator().Calculate(
                previous,
                before,
                mutation.Projection,
                new MaterialRevisionRule(mutation.MaterialThreshold, null),
                distances);

            Assert.True(
                result.IsSuccess,
                $"dimension={mutation.Dimension}; {result.Failure?.Message}");
            Assert.True(
                result.Deltas!.DecisionInduced.Get(mutation.Dimension) > 0,
                $"Mutation did not exercise {mutation.Dimension}.");
        }
    }

    private static PromiseProjection Projection(
        string vehicle,
        string pickupStop,
        string pickupNode,
        string dropStop,
        string dropNode,
        long pickupEta,
        long dropEta,
        IEnumerable<PromiseServiceToken> order) =>
        new(
            Subject,
            new VehicleId(vehicle),
            new StopId(pickupStop),
            new NodeId(pickupNode),
            new StopId(dropStop),
            new NodeId(dropNode),
            new SimTime(pickupEta),
            new SimTime(dropEta),
            order);

    private static IReadOnlyList<PromiseServiceToken> OldOrder() =>
    [
        Token("a-pickup", IncumbentA, RouteStopKind.Pickup),
        Token("b-pickup", IncumbentB, RouteStopKind.Pickup),
        Token("pickup", Subject, RouteStopKind.Pickup),
        Token("drop", Subject, RouteStopKind.DropOff),
    ];

    private static IReadOnlyList<PromiseServiceToken> NewOrder() =>
    [
        Token("inserted", null, RouteStopKind.Waypoint),
        Token("b-pickup", IncumbentB, RouteStopKind.Pickup),
        Token("a-pickup", IncumbentA, RouteStopKind.Pickup),
        Token("pickup-new", Subject, RouteStopKind.Pickup),
        Token("drop-new", Subject, RouteStopKind.DropOff),
    ];

    private static IReadOnlyList<PromiseServiceToken> SubjectPickup(
        string pickup) =>
    [
        Token("a-pickup", IncumbentA, RouteStopKind.Pickup),
        Token("b-pickup", IncumbentB, RouteStopKind.Pickup),
        Token(pickup, Subject, RouteStopKind.Pickup),
        Token("drop", Subject, RouteStopKind.DropOff),
    ];

    private static IReadOnlyList<PromiseServiceToken> SubjectDrop(string drop) =>
    [
        Token("a-pickup", IncumbentA, RouteStopKind.Pickup),
        Token("b-pickup", IncumbentB, RouteStopKind.Pickup),
        Token("pickup", Subject, RouteStopKind.Pickup),
        Token(drop, Subject, RouteStopKind.DropOff),
    ];

    private static IReadOnlyList<PromiseServiceToken> InvertedIncumbents() =>
    [
        Token("b-pickup", IncumbentB, RouteStopKind.Pickup),
        Token("a-pickup", IncumbentA, RouteStopKind.Pickup),
        Token("pickup", Subject, RouteStopKind.Pickup),
        Token("drop", Subject, RouteStopKind.DropOff),
    ];

    private static IReadOnlyList<PromiseServiceToken> InsertedBeforePickup() =>
    [
        Token("inserted", null, RouteStopKind.Waypoint),
        .. OldOrder(),
    ];

    private static IReadOnlyList<PromiseServiceToken> SubjectOnlyOrder(
        string pickup,
        string drop) =>
    [
        Token(pickup, Subject, RouteStopKind.Pickup),
        Token(drop, Subject, RouteStopKind.DropOff),
    ];

    private static PromiseServiceToken Token(
        string stop,
        RequestId? request,
        RouteStopKind kind) =>
        new(new StopId(stop), request, kind);

    private static MutationCase Case(
        CommitmentDimension dimension,
        PromiseProjection projection,
        long threshold = 1_000) =>
        new(dimension, projection, threshold);

    private sealed record MutationCase(
        CommitmentDimension Dimension,
        PromiseProjection Projection,
        long MaterialThreshold);

    private sealed class Distances(
        IReadOnlyDictionary<(string From, string To), long> distances)
        : IStopDistanceLookup
    {
        public bool TryGetDistanceMillimeters(
            NodeId fromNodeId,
            NodeId toNodeId,
            out long distanceMillimeters) =>
            distances.TryGetValue(
                (fromNodeId.Value, toNodeId.Value),
                out distanceMillimeters);
    }
}
