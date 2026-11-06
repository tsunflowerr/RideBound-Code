using RideBound.Domain.Common;
using RideBound.Domain.Routes;

namespace RideBound.Domain.Tests.Routes;

public sealed class RoutePlanTests
{
    [Fact]
    public void No_op_preserves_exact_prefix_suffix_and_version()
    {
        var route = TestData.Route(
            frozen: [TestData.Waypoint("frozen")],
            mutable: [TestData.Pickup(), TestData.DropOff()]);

        var noOp = route.CreateNoOp();

        Assert.Same(route, noOp);
        Assert.True(route.IsSemanticallyEqual(noOp));
        Assert.Equal(0, noOp.Version.Value);
    }

    [Fact]
    public void Replacing_suffix_advances_version_and_preserves_exact_prefix()
    {
        var route = TestData.Route(
            frozen: [TestData.Waypoint("frozen")],
            mutable: [TestData.Pickup(), TestData.DropOff()]);

        var changed = route.ReplaceMutableSuffix(
            [TestData.Waypoint("new", TestData.NodeTwo)]);

        Assert.True(changed.IsSuccess);
        Assert.Equal(1, changed.Value!.Version.Value);
        Assert.True(route.HasExactFrozenPrefix(changed.Value));
        Assert.Single(changed.Value.MutableSuffix);
    }

    [Fact]
    public void Reached_stop_progress_is_monotonic_and_moves_mutable_stop_to_prefix()
    {
        var route = TestData.Route(
            frozen: [TestData.Waypoint("locked")],
            mutable: [TestData.Waypoint("next", TestData.NodeTwo)]);

        var lockedReached = route.AdvanceReachedStop(new StopId("locked"));
        var nextReached = lockedReached.Value!.AdvanceReachedStop(new StopId("next"));

        Assert.Equal(1, lockedReached.Value.ExecutedStopCount);
        Assert.Equal(2, nextReached.Value!.ExecutedStopCount);
        Assert.Equal(2, nextReached.Value.FrozenPrefix.Count);
        Assert.Empty(nextReached.Value.MutableSuffix);
    }

    [Fact]
    public void Unexpected_or_duplicate_progress_does_not_change_route()
    {
        var route = TestData.Route(
            mutable: [TestData.Waypoint("expected")]);

        var wrong = route.AdvanceReachedStop(new StopId("other"));
        var first = route.AdvanceReachedStop(new StopId("expected"));
        var duplicate = first.Value!.AdvanceReachedStop(new StopId("expected"));

        Assert.False(wrong.IsSuccess);
        Assert.Equal(RouteFailureCodes.UnexpectedReachedStop, wrong.Failure?.Code);
        Assert.Equal(0, route.ExecutedStopCount);
        Assert.False(duplicate.IsSuccess);
        Assert.Equal(RouteFailureCodes.NoRemainingStop, duplicate.Failure?.Code);
    }

    [Fact]
    public void Frozen_request_stop_cannot_be_removed_by_cancellation()
    {
        var route = TestData.Route(
            frozen: [TestData.Pickup()],
            mutable: [TestData.DropOff()]);

        var result = route.RemoveRequestFromMutableSuffix(TestData.RequestOne);

        Assert.False(result.IsSuccess);
        Assert.Equal(RouteFailureCodes.FrozenPrefix, result.Failure?.Code);
        Assert.Equal(2, route.AllStops.Count());
    }

    [Fact]
    public void Route_rejects_duplicate_stop_identity()
    {
        var result = RoutePlan.Create(
            new PlanVersion(0),
            0,
            [TestData.Waypoint("same")],
            [TestData.Waypoint("same", TestData.NodeTwo)]);

        Assert.False(result.IsSuccess);
        Assert.Equal(RouteFailureCodes.DuplicateStop, result.Failure?.Code);
    }

    [Fact]
    public void Generated_suffix_replacements_never_change_frozen_prefix()
    {
        for (var count = 1; count <= 32; count++)
        {
            var frozen = Enumerable.Range(0, count)
                .Select(
                    index => TestData.Waypoint(
                        $"f-{index}",
                        index % 2 == 0 ? TestData.NodeOne : TestData.NodeTwo))
                .ToArray();
            var route = TestData.Route(frozen: frozen);
            var changed = route.ReplaceMutableSuffix(
                [TestData.Waypoint($"new-{count}")]);

            Assert.True(changed.IsSuccess);
            Assert.True(route.HasExactFrozenPrefix(changed.Value!));
            Assert.Equal(frozen, changed.Value!.FrozenPrefix);
        }
    }

    [Fact]
    public void Remaining_legs_follow_exact_stop_order()
    {
        var route = TestData.Route(
            frozen: [TestData.Waypoint("executed", TestData.NodeOne)],
            mutable:
            [
                TestData.Waypoint("next", TestData.NodeTwo),
                TestData.Waypoint("last", TestData.NodeOne),
            ],
            executed: 1);

        var legs = route.GetRemainingLegs(TestData.NodeOne).ToArray();

        Assert.Equal(2, legs.Length);
        Assert.Equal(TestData.NodeOne, legs[0].FromNodeId);
        Assert.Equal(TestData.NodeTwo, legs[0].ToNodeId);
        Assert.Equal(new StopId("next"), legs[0].DestinationStopId);
        Assert.Equal(TestData.NodeTwo, legs[1].FromNodeId);
        Assert.Equal(TestData.NodeOne, legs[1].ToNodeId);
    }
}
