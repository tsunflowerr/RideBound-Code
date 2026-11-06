using RideBound.Contracts.Protocol;

namespace RideBound.Contracts.Tests.Serialization;

public sealed class CanonicalUnitTests
{
    [Theory]
    [InlineData("0.0005", 0)]
    [InlineData("0.0015", 2)]
    [InlineData("1.2345", 1234)]
    [InlineData("1.2355", 1236)]
    public void Seconds_use_round_ties_to_even(string source, long expected)
    {
        var converted = CanonicalUnitConversions.TrySecondsToMilliseconds(
            decimal.Parse(source, System.Globalization.CultureInfo.InvariantCulture),
            out var milliseconds);

        Assert.True(converted);
        Assert.Equal(expected, milliseconds.Value);
    }

    [Fact]
    public void Unit_conversions_reject_negative_distance_and_overflow()
    {
        Assert.False(
            CanonicalUnitConversions.TryMetersToMillimeters(-0.0001m, out _));
        Assert.False(
            CanonicalUnitConversions.TryMetersToMillimeters(-0.001m, out _));
        Assert.False(
            CanonicalUnitConversions.TryMetersToMillimeters(decimal.MaxValue, out _));
        Assert.False(
            CanonicalUnitConversions.TrySecondsToMilliseconds(-0.0001m, out _));
    }

    [Theory]
    [InlineData("-90", -900_000_000)]
    [InlineData("90", 900_000_000)]
    [InlineData("10.12345675", 101_234_568)]
    public void Latitude_uses_e7_and_ties_to_even(string source, long expected)
    {
        var converted = CanonicalUnitConversions.TryLatitudeDegrees(
            decimal.Parse(source, System.Globalization.CultureInfo.InvariantCulture),
            out var latitude);

        Assert.True(converted);
        Assert.Equal(expected, latitude.Value);
    }

    [Theory]
    [InlineData(-901_000_000, false)]
    [InlineData(-900_000_000, true)]
    [InlineData(900_000_000, true)]
    [InlineData(901_000_000, false)]
    public void Latitude_enforces_geographic_range(long value, bool expected)
    {
        Assert.Equal(expected, LatitudeE7.TryCreate(value, out _));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(999, true)]
    [InlineData(1000, false)]
    public void Edge_progress_excludes_endpoints(int value, bool expected)
    {
        Assert.Equal(expected, EdgeProgressPermille.TryCreate(value, out _));
    }

    [Fact]
    public void Signed_cost_accepts_both_safe_range_boundaries()
    {
        Assert.True(
            CostMicros.TryCreate(ProtocolLimits.MinCanonicalInteger, out _));
        Assert.True(
            CostMicros.TryCreate(ProtocolLimits.MaxCanonicalInteger, out _));
        Assert.False(CostMicros.TryCreate(long.MinValue, out _));
        Assert.False(CostMicros.TryCreate(long.MaxValue, out _));
    }
}
