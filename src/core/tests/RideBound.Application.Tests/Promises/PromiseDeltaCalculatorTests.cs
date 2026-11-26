using RideBound.Application.Promises;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Routes;

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

        var deltas = new PromiseDeltaCalculator().Calculate(
            previous,
            exogenous,
            proposed);

        Assert.Equal(1000, deltas.Exogenous.PickupEtaTotalMs);
        Assert.Equal(500, deltas.DecisionInduced.PickupEtaTotalMs);
        Assert.Equal(500, deltas.Visible.PickupEtaTotalMs);
        Assert.Equal(1000, deltas.Exogenous.DropEtaTotalMs);
        Assert.Equal(500, deltas.DecisionInduced.DropEtaTotalMs);
        Assert.Equal(500, deltas.Visible.DropEtaTotalMs);
        Assert.NotEqual(
            deltas.Exogenous.PickupEtaTotalMs
                + deltas.DecisionInduced.PickupEtaTotalMs,
            deltas.Visible.PickupEtaTotalMs);
        Assert.Equal(1, deltas.DecisionInduced.VehicleSwitchCount);
        Assert.Equal(1, deltas.DecisionInduced.PickupStopSwitchCount);
        Assert.Equal(1, deltas.DecisionInduced.DropStopSwitchCount);
        Assert.Equal(0, deltas.Exogenous.VehicleSwitchCount);
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
        var mutations = new (CommitmentDimension Dimension, PromiseProjection Projection)[]
        {
            (CommitmentDimension.PickupEtaTotalMs,
                Projection("v-1", "pickup", "n-1", "drop", "n-2", 10_001, 20_000, OldOrder())),
            (CommitmentDimension.DropEtaTotalMs,
                Projection("v-1", "pickup", "n-1", "drop", "n-2", 10_000, 20_001, OldOrder())),
            (CommitmentDimension.VehicleSwitchCount,
                Projection("v-2", "pickup", "n-1", "drop", "n-2", 10_000, 20_000, OldOrder())),
            (CommitmentDimension.PickupStopSwitchCount,
                Projection("v-1", "pickup-new", "n-1", "drop", "n-2", 10_000, 20_000, SubjectStops("pickup-new", "drop"))),
            (CommitmentDimension.DropStopSwitchCount,
                Projection("v-1", "pickup", "n-1", "drop-new", "n-2", 10_000, 20_000, SubjectStops("pickup", "drop-new"))),
        };
        var previous = new PublishedPromise(
            new PromiseVersion(1),
            1,
            new SimTime(1_000),
            before);

        Assert.Equal(
            CommitmentDimensionVocabulary.Ordered,
            mutations.Select(value => value.Dimension));

        foreach (var (dimension, projection) in mutations)
        {
            var deltas = new PromiseDeltaCalculator().Calculate(
                previous,
                before,
                projection);

            Assert.True(
                deltas.DecisionInduced.Get(dimension) > 0,
                $"Mutation did not exercise {dimension}.");
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

    private static IReadOnlyList<PromiseServiceToken> SubjectStops(string pickup, string drop) =>
    [
        Token("a-pickup", IncumbentA, RouteStopKind.Pickup),
        Token("b-pickup", IncumbentB, RouteStopKind.Pickup),
        Token(pickup, Subject, RouteStopKind.Pickup),
        Token(drop, Subject, RouteStopKind.DropOff),
    ];

    private static PromiseServiceToken Token(
        string stop,
        RequestId? request,
        RouteStopKind kind) =>
        new(new StopId(stop), request, kind);
}
