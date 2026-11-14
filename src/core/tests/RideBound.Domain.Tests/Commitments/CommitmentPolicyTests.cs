using RideBound.Domain.Commitments;
using RideBound.Domain.Common;

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

        Assert.Null(policy.DropEtaDeadlineSlack);
    }

    [Fact]
    public void Policy_keeps_the_configured_deadline()
    {
        var policy = CreateDeadlinePolicy(new Duration(30_000));

        Assert.Equal(new Duration(30_000), policy.DropEtaDeadlineSlack);
    }

    [Fact]
    public void Policy_has_no_relaxed_deadline_by_default()
    {
        var policy = CreatePolicy();
        var deadline = CreateDeadlinePolicy(new Duration(30_000));

        Assert.False(policy.DropEtaDeadlineRelaxed);
        Assert.False(deadline.DropEtaDeadlineRelaxed);
    }

    [Fact]
    public void Policy_keeps_a_configured_relaxed_deadline()
    {
        var policy = CreateDeadlinePolicy(new Duration(30_000), relaxed: true);

        Assert.Equal(new Duration(30_000), policy.DropEtaDeadlineSlack);
        Assert.True(policy.DropEtaDeadlineRelaxed);
    }

    [Fact]
    public void Policy_rejects_a_relaxed_deadline_without_a_slack()
    {
        var error = Assert.Throws<ArgumentException>(
            () => CreateDeadlinePolicy(null, relaxed: true));

        Assert.Equal("dropEtaDeadlineRelaxed", error.ParamName);
    }

    private static CommitmentPolicy CreateDeadlinePolicy(
        Duration? slack,
        bool relaxed = false) =>
        new(
            "validated-policy",
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    null,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1, null),
            dropEtaDeadlineSlack: slack,
            dropEtaDeadlineRelaxed: relaxed);

    private static CommitmentPolicy CreatePolicy(
        PromiseLock finalLocks = PromiseLock.None) =>
        new(
            "validated-policy",
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    null,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1, null),
            finalLocks);
}
