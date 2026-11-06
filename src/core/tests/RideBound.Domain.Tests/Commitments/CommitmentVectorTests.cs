using RideBound.Domain.Commitments;
using RideBound.Domain.Common;

namespace RideBound.Domain.Tests.Commitments;

public sealed class CommitmentVectorTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(DomainLimits.MaxCanonicalInteger + 1)]
    public void Vector_rejects_values_outside_canonical_range(long value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CommitmentVector(
                value,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0));
    }

    [Fact]
    public void Addition_fails_closed_before_runtime_or_canonical_overflow()
    {
        var nearLimit = new CommitmentVector(
            DomainLimits.MaxCanonicalInteger,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0);
        var one = new CommitmentVector(1, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        var result = nearLimit.Add(one);

        Assert.Equal(CommitmentFailureCodes.VectorOverflow, result.Failure?.Code);
        Assert.Equal("pickup_eta_total_ms", result.Failure?.Dimension);
    }
}
