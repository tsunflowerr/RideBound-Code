using RideBound.Application.State;
using RideBound.Domain.Common;
using RideBound.Domain.Routes;

namespace RideBound.Application.Tests.State;

public sealed class VersionedPlanPoolTests
{
    [Fact]
    public void Exact_identity_is_vehicle_order_invariant_but_route_order_sensitive()
    {
        var first = new CanonicalVehiclePlan(
            new VehicleId("vehicle-a"),
            Route(("stop-a", "node-a"), ("stop-x", "node-x")));
        var second = VehiclePlan("vehicle-b", "stop-b", "node-b");
        var reorderedVehicles = CanonicalFleetPlan.Create(3, [second, first]);
        var canonical = CanonicalFleetPlan.Create(3, [first, second]);
        var reversedRoute = CanonicalFleetPlan.Create(
            3,
            [
                new CanonicalVehiclePlan(
                    first.VehicleId,
                    Route(
                        ("stop-x", "node-x"),
                        ("stop-a", "node-a"))),
                second,
            ]);

        Assert.Equal(canonical.PlanId, reorderedVehicles.PlanId);
        Assert.NotEqual(canonical.PlanId, reversedRoute.PlanId);
    }

    [Fact]
    public void Rehydrate_rejects_a_semantically_forged_plan_id()
    {
        var plan = CanonicalFleetPlan.Create(
            1,
            [VehiclePlan("vehicle-a", "stop-a", "node-a")]);

        var error = Assert.Throws<ArgumentException>(
            () => CanonicalFleetPlan.Rehydrate(
                new string('a', 64),
                plan.SourceEpoch,
                plan.VehiclePlans));

        Assert.Contains("exact fleet-route semantics", error.Message);
    }

    [Fact]
    public void Pool_version_advances_once_and_binds_distinguished_same_epoch_plan()
    {
        var planA = CanonicalFleetPlan.Create(
            2,
            [VehiclePlan("vehicle-a", "stop-a", "node-a")]);
        var planB = CanonicalFleetPlan.Create(
            2,
            [VehiclePlan("vehicle-a", "stop-b", "node-b")]);

        var pool = VersionedPlanPool.CreateNext(
            VersionedPlanPool.Empty,
            2,
            planB.PlanId,
            [planB, planA]);

        Assert.Equal(1, pool.Version);
        Assert.Equal(planB.PlanId, pool.DistinguishedPlan!.PlanId);
        Assert.Equal(
            pool.Plans.OrderBy(value => value.PlanId).Select(value => value.PlanId),
            pool.Plans.Select(value => value.PlanId));
        Assert.Throws<ArgumentException>(
            () => VersionedPlanPool.CreateNext(
                pool,
                3,
                planA.PlanId,
                [planA]));
    }

    private static CanonicalVehiclePlan VehiclePlan(
        string vehicle,
        string stop,
        string node) =>
        new(new VehicleId(vehicle), Route((stop, node)));

    private static RoutePlan Route(params (string Stop, string Node)[] stops) =>
        RoutePlan.Create(
            new PlanVersion(1),
            0,
            [],
            stops.Select(
                value => new RouteStop(
                    new StopId(value.Stop),
                    new NodeId(value.Node),
                    RouteStopKind.Waypoint,
                    null,
                    new Duration(0)))).Value!;
}
