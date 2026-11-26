using RideBound.Domain.Commitments;

namespace RideBound.Domain.Tests.Commitments;

public sealed class CommitmentPolicyTests
{
    [Fact]
    public void Dimension_limit_rejects_unknown_dimension()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CommitmentDimensionLimit(
                (CommitmentDimension)99,
                null,
                CommitmentPhase.AllActive));
    }

    [Fact]
    public void Policy_rejects_unknown_lock_bits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreatePolicy(
                finalLocks: (PromiseLock)64));
    }

    [Fact]
    public void Policy_has_no_deadline_by_default()
    {
        var policy = CreatePolicy();

        Assert.Null(policy.DropEtaDeadlineSlackPermille);
    }

    [Fact]
    public void Policy_keeps_the_configured_deadline()
    {
        var policy = CreateDeadlinePolicy(350);

        Assert.Equal(350, policy.DropEtaDeadlineSlackPermille);
    }

    private static CommitmentPolicy CreateDeadlinePolicy(int permille) =>
        new(
            "validated-policy",
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    null,
                    CommitmentPhase.AllActive)),
            dropEtaDeadlineSlackPermille: permille);

    private static CommitmentPolicy CreatePolicy(
        PromiseLock finalLocks = PromiseLock.None) =>
        new(
            "validated-policy",
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    null,
                    CommitmentPhase.AllActive)),
            finalLocks);
}
