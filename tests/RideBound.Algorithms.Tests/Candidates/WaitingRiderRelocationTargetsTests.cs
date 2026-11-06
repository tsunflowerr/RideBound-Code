using RideBound.Algorithms.Candidates;
using RideBound.Application.State;
using RideBound.Application.Travel;
using RideBound.Domain.Common;
using RideBound.Domain.Routes;
using RideBound.Domain.Vehicles;

namespace RideBound.Algorithms.Tests.Candidates;

/// <summary>
/// Golden for <see cref="WaitingRiderRelocationBuilder.Targets"/>: which receivers G2 tries for
/// one waiting rider when the fleet is larger than the target cap k.
/// <para>
/// The rider waits at node-1 and vehicle-1 is its donor. Twelve other vehicles start at these
/// travel times to node-1 (directed arcs; a vehicle on an edge starts at the edge's end node):
/// </para>
/// <list type="table">
/// <item><term>0 ms</term><description>vehicle-8 (at node-1 itself)</description></item>
/// <item><term>100 ms</term><description>vehicle-5 (node-5), vehicle-7 (edge node-0 to node-5)</description></item>
/// <item><term>200 ms</term><description>vehicle-4 (node-2), vehicle-6 (node-3), vehicle-10 (node-2),
/// vehicle-13 (edge node-5 to node-2)</description></item>
/// <item><term>300 ms</term><description>vehicle-2 (node-0), vehicle-11 (node-0)</description></item>
/// <item><term>no arc</term><description>vehicle-3 (node-4), vehicle-12 (node-4), vehicle-9 (edge
/// node-1 to node-4; only the reverse arc node-1 to node-4 exists)</description></item>
/// </list>
/// <para>
/// Ranking is by travel time, then by ordinal id ("vehicle-10" sorts before "vehicle-2"); a
/// vehicle without an arc ranks last. The kept vehicles are returned in ordinal id order.
/// </para>
/// </summary>
public sealed class WaitingRiderRelocationTargetsTests
{
    private const string AllOthers =
        "vehicle-10,vehicle-11,vehicle-12,vehicle-13,vehicle-2,vehicle-3,vehicle-4,vehicle-5,vehicle-6,vehicle-7,vehicle-8,vehicle-9";

    private static readonly NodeId[] Nodes =
        Enumerable.Range(0, 6).Select(index => new NodeId($"node-{index}")).ToArray();

    [Theory]
    [InlineData(1, "vehicle-8")]
    [InlineData(2, "vehicle-5,vehicle-8")]
    [InlineData(3, "vehicle-5,vehicle-7,vehicle-8")]
    [InlineData(4, "vehicle-10,vehicle-5,vehicle-7,vehicle-8")]
    [InlineData(5, "vehicle-10,vehicle-13,vehicle-5,vehicle-7,vehicle-8")]
    [InlineData(6, "vehicle-10,vehicle-13,vehicle-4,vehicle-5,vehicle-7,vehicle-8")]
    [InlineData(7, "vehicle-10,vehicle-13,vehicle-4,vehicle-5,vehicle-6,vehicle-7,vehicle-8")]
    [InlineData(8, "vehicle-10,vehicle-11,vehicle-13,vehicle-4,vehicle-5,vehicle-6,vehicle-7,vehicle-8")]
    [InlineData(9, "vehicle-10,vehicle-11,vehicle-13,vehicle-2,vehicle-4,vehicle-5,vehicle-6,vehicle-7,vehicle-8")]
    [InlineData(10, "vehicle-10,vehicle-11,vehicle-12,vehicle-13,vehicle-2,vehicle-4,vehicle-5,vehicle-6,vehicle-7,vehicle-8")]
    [InlineData(11, "vehicle-10,vehicle-11,vehicle-12,vehicle-13,vehicle-2,vehicle-3,vehicle-4,vehicle-5,vehicle-6,vehicle-7,vehicle-8")]
    [InlineData(12, AllOthers)]
    [InlineData(13, AllOthers)]
    public void The_k_nearest_receivers_break_ties_by_ordinal_id_and_rank_missing_arcs_last(
        int cap,
        string expected)
    {
        Assert.Equal(expected, Targets(cap));
    }

    [Fact]
    public void Without_a_cap_every_other_vehicle_is_a_receiver_in_ordinal_order()
    {
        Assert.Equal(AllOthers, Targets(null));
    }

    [Fact]
    public void A_cap_of_at_least_the_other_vehicles_needs_no_travel_arc()
    {
        // No arc reaches node-1 at all; with a cap that does not bind, nothing is looked up.
        var state = Fixture(arcs: [Arc(1, 0, 100)]);
        var donor = state.Run.Vehicles[new VehicleId("vehicle-1")];
        var request = state.Run.Requests[new RequestId("waiting")];

        Assert.Equal(
            AllOthers,
            Join(new WaitingRiderRelocationBuilder().Targets(state, donor, request, 12)));
    }

    private static string Targets(int? cap)
    {
        var state = Fixture(arcs: null);
        var donor = state.Run.Vehicles[new VehicleId("vehicle-1")];
        var request = state.Run.Requests[new RequestId("waiting")];

        return Join(new WaitingRiderRelocationBuilder().Targets(state, donor, request, cap));
    }

    private static string Join(IReadOnlyList<VehicleState> vehicles) =>
        string.Join(",", vehicles.Select(vehicle => vehicle.Id.Value));

    /// <summary>
    /// The 13 vehicles of the class summary, bootstrapped in a scrambled order so the result
    /// cannot follow insertion order. The default arcs are the ones in the table, plus node-1
    /// to node-4 (the reverse of the missing arc) and node-0 to node-1 being slower than
    /// node-5 to node-1, so an edge vehicle ranks by its end node.
    /// </summary>
    private static OnlineState Fixture(IReadOnlyList<KeyValuePair<TravelArc, Duration>>? arcs)
    {
        var request = AlgorithmTestData.PendingRequest("waiting", Nodes[1], Nodes[3]);
        var vehicles = new[]
        {
            Vehicle(9, new EdgeProgressPosition(Nodes[1], Nodes[4], "node-1>node-4", 500)),
            Vehicle(2, new NodePosition(Nodes[0])),
            Vehicle(13, new EdgeProgressPosition(Nodes[5], Nodes[2], "node-5>node-2", 250)),
            Vehicle(6, new NodePosition(Nodes[3])),
            Vehicle(1, new NodePosition(Nodes[1])),
            Vehicle(11, new NodePosition(Nodes[0])),
            Vehicle(4, new NodePosition(Nodes[2])),
            Vehicle(7, new EdgeProgressPosition(Nodes[0], Nodes[5], "node-0>node-5", 750)),
            Vehicle(12, new NodePosition(Nodes[4])),
            Vehicle(3, new NodePosition(Nodes[4])),
            Vehicle(10, new NodePosition(Nodes[2])),
            Vehicle(5, new NodePosition(Nodes[5])),
            Vehicle(8, new NodePosition(Nodes[1])),
        };

        return AlgorithmTestData.CreateState(
            [request],
            vehicles,
            arcs: arcs ??
            [
                Arc(0, 1, 300),
                Arc(2, 1, 200),
                Arc(3, 1, 200),
                Arc(5, 1, 100),
                Arc(1, 4, 50),
                Arc(0, 5, 250),
                Arc(5, 2, 100),
            ]);
    }

    private static VehicleState Vehicle(int number, VehiclePosition position) =>
        VehicleState.Create(
            new VehicleId($"vehicle-{number}"),
            4,
            0,
            position,
            [],
            [],
            RoutePlan.Create(new PlanVersion(0), 0, [], []).Value!,
            1).Value!;

    private static KeyValuePair<TravelArc, Duration> Arc(int from, int to, long milliseconds) =>
        new(new TravelArc(Nodes[from], Nodes[to]), new Duration(milliseconds));
}
