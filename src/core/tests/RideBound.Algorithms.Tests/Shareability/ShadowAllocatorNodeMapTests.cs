using RideBound.Algorithms.Shareability;
using RideBound.Domain.Common;

namespace RideBound.Algorithms.Tests.Shareability;

/// <summary>
/// ADR-089 GĐ4 Shadow gate, 2026-11-04: the first real Shadow job failed at its first promise with
/// "Node 'node-825512e8…' is not a network node". Run node identifiers are the simulator adapter's
/// opaque ones (node- + SHA-256 of the FleetPy node), not "node-%06d" network numbers, so the
/// allocator now reads them through the cell's networkNodes map and never parses them.
/// </summary>
public sealed class ShadowAllocatorNodeMapTests
{
    private const string Opaque = "node-825512e813deba6eb575fedefddfd4109f5cf0be2a99003d0958a506283f57f1";

    private static ShadowAllocator Allocator(IReadOnlyDictionary<string, int> nodes) =>
        new(new ShadowAllocationInputs(
            ShareabilityNetwork.Create(2, [0, 5, 5, 0], [0, 1, 2], [1, 0], [5, 5], [0, 0], 1),
            Kappa: 1.0,
            Factors: [1.0],
            PickupWindowMilliseconds: 600_000,
            StrideMilliseconds: 0,
            IntervalMilliseconds: 600_000,
            Alpha: 0.15,
            Phi: 1.0,
            Sampled: 1,
            PriorOrigin: [1.0],
            PriorDestination: [1.0],
            Threshold: 50.0,
            BinMilliseconds: 900_000,
            NetworkNodes: nodes));

    [Fact]
    public void An_opaque_run_node_identifier_is_read_through_the_cell_map()
    {
        var allocator = Allocator(new Dictionary<string, int> { [Opaque] = 1, ["node-other"] = 0 });

        Assert.Equal(1, allocator.NetworkNode(new NodeId(Opaque)));
        Assert.Equal(0, allocator.NetworkNode(new NodeId("node-other")));
    }

    [Fact]
    public void A_node_outside_the_map_is_refused_even_in_the_old_numeric_form()
    {
        var allocator = Allocator(new Dictionary<string, int> { [Opaque] = 1 });

        var unknown = Assert.Throws<InvalidOperationException>(() => allocator.NetworkNode(new NodeId("node-unknown")));
        Assert.Contains("networkNodes", unknown.Message, StringComparison.Ordinal);
        // "node-000001" used to be parsed as network node 1; it is not in the map, so it is refused too.
        Assert.Throws<InvalidOperationException>(() => allocator.NetworkNode(new NodeId("node-000001")));
    }
}
