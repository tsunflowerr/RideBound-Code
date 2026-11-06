using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Routes;

namespace RideBound.Domain.Tests.Commitments;

public sealed class RiderPromiseTests
{
    [Fact]
    public void Promise_version_enforces_canonical_range_and_checked_next()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PromiseVersion(0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PromiseVersion(DomainLimits.MaxCanonicalInteger + 1));
        Assert.Throws<OverflowException>(
            () => new PromiseVersion(
                DomainLimits.MaxCanonicalInteger).Next());
    }

    [Fact]
    public void Service_order_rejects_pickup_after_drop()
    {
        Assert.Throws<ArgumentException>(
            () => new PromiseProjection(
                TestData.RequestOne,
                TestData.VehicleOne,
                new StopId("pickup"),
                TestData.NodeOne,
                new StopId("drop"),
                TestData.NodeTwo,
                new SimTime(10_000),
                new SimTime(20_000),
                [
                    new PromiseServiceToken(
                        new StopId("drop"),
                        TestData.RequestOne,
                        RouteStopKind.DropOff),
                    new PromiseServiceToken(
                        new StopId("pickup"),
                        TestData.RequestOne,
                        RouteStopKind.Pickup),
                ]));
    }
}
