using RideBound.Contracts.Protocol;

namespace RideBound.Contracts.Tests.Protocol;

public sealed class ProtocolPrimitiveTests
{
    [Theory]
    [InlineData("1.0.0", 1, 0, 0)]
    [InlineData("12.34.56", 12, 34, 56)]
    public void Protocol_version_accepts_exact_three_part_decimal(
        string value,
        int major,
        int minor,
        int patch)
    {
        var parsed = ProtocolVersion.TryParse(value, out var version);

        Assert.True(parsed);
        Assert.NotNull(version);
        Assert.Equal(major, version.Major);
        Assert.Equal(minor, version.Minor);
        Assert.Equal(patch, version.Patch);
        Assert.Equal(value, version.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("1.0")]
    [InlineData("v1.0.0")]
    [InlineData("01.0.0")]
    [InlineData("1.0.0+meta")]
    [InlineData("1.0.-1")]
    public void Protocol_version_rejects_noncanonical_forms(string value)
    {
        Assert.False(ProtocolVersion.TryParse(value, out _));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(ProtocolLimits.MaxCanonicalInteger, true)]
    [InlineData(-1, false)]
    [InlineData(long.MaxValue, false)]
    public void Simulation_time_enforces_canonical_range(long value, bool expected)
    {
        Assert.Equal(
            expected,
            SimulationTimeMilliseconds.TryCreate(value, out _));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(ProtocolLimits.MaxCanonicalInteger, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void Event_sequence_starts_at_one(long value, bool expected)
    {
        Assert.Equal(expected, EventSequence.TryCreate(value, out _));
    }

    [Fact]
    public void Opaque_identifiers_reject_unpaired_surrogates()
    {
        Assert.False(RunId.TryCreate("\uD800", out _));
        Assert.False(ScenarioId.TryCreate("\uDC00", out _));
    }
}
