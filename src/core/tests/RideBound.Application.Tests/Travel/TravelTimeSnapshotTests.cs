using RideBound.Application.Travel;
using RideBound.Domain.Common;

namespace RideBound.Application.Tests.Travel;

public sealed class TravelTimeSnapshotTests
{
    [Fact]
    public void Directed_lookup_is_exact_and_same_node_is_zero()
    {
        var snapshot = ApplicationTestData.Travel();

        Assert.True(
            snapshot.TryGetTravelTime(
                ApplicationTestData.NodeZero,
                ApplicationTestData.NodeOne,
                out var forward));
        Assert.Equal(100, forward.Milliseconds);
        Assert.False(
            snapshot.TryGetTravelTime(
                ApplicationTestData.NodeOne,
                ApplicationTestData.NodeZero,
                out _));
        Assert.True(
            snapshot.TryGetTravelTime(
                ApplicationTestData.NodeOne,
                ApplicationTestData.NodeOne,
                out var zero));
        Assert.Equal(0, zero.Milliseconds);
    }

    [Fact]
    public void Duplicate_directed_arc_is_rejected()
    {
        var arc = new TravelArc(
            ApplicationTestData.NodeZero,
            ApplicationTestData.NodeOne);

        var result = TravelTimeSnapshot.Create(
            1,
            new string('a', 64),
            [
                new KeyValuePair<TravelArc, Duration>(arc, new Duration(1)),
                new KeyValuePair<TravelArc, Duration>(arc, new Duration(2)),
            ]);

        Assert.False(result.IsSuccess);
        Assert.Equal(TravelFailureCodes.InvalidSnapshot, result.Failure?.Code);
    }
}
