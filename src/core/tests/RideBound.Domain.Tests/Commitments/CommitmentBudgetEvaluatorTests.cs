using RideBound.Domain.Commitments;
using RideBound.Domain.Requests;

namespace RideBound.Domain.Tests.Commitments;

public sealed class CommitmentBudgetEvaluatorTests
{
    public static IEnumerable<object[]> Dimensions() =>
        CommitmentDimensionVocabulary.Ordered.Select(
            dimension => new object[] { dimension });

    [Theory]
    [MemberData(nameof(Dimensions))]
    public void Every_dimension_accepts_exact_limit_and_rejects_one_over(
        CommitmentDimension dimension)
    {
        var evaluator = new CommitmentBudgetEvaluator();
        var policy = CommitmentTestData.Policy(hardLimit: 10);
        var exact = evaluator.Evaluate(
            TestData.RequestOne,
            RequestLifecycle.Accepted,
            CommitmentTestData.Vector(dimension, 7),
            CommitmentTestData.Vector(dimension, 3),
            policy);
        var exceeded = evaluator.Evaluate(
            TestData.RequestOne,
            RequestLifecycle.Accepted,
            CommitmentTestData.Vector(dimension, 7),
            CommitmentTestData.Vector(dimension, 4),
            policy);

        Assert.True(exact.IsAllowed);
        var witness = Assert.Single(exceeded.Witnesses);
        Assert.Equal(
            CommitmentDimensionVocabulary.ToProtocolValue(dimension),
            witness.Dimension);
        Assert.Equal(10, witness.Limit);
        Assert.Equal(7, witness.Before);
        Assert.Equal(4, witness.Delta);
        Assert.Equal(11, witness.After);
    }

    [Fact]
    public void Zero_is_hard_and_null_is_unbounded()
    {
        var limits = CommitmentDimensionVocabulary.Ordered.Select(
            dimension => new CommitmentDimensionLimit(
                dimension,
                dimension == CommitmentDimension.PickupEtaTotalMs
                    ? 0
                    : null,
                CommitmentPhase.AllActive));
        var policy = new CommitmentPolicy(
            "zero-and-infinite",
            CommitmentBudgetBasis.DecisionInduced,
            limits,
            new MaterialRevisionRule(1, null));
        var delta = new CommitmentVector(
            1,
            RideBound.Domain.Common.DomainLimits.MaxCanonicalInteger,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0);

        var result = new CommitmentBudgetEvaluator().Evaluate(
            TestData.RequestOne,
            RequestLifecycle.Accepted,
            CommitmentVector.Zero,
            delta,
            policy);

        var witness = Assert.Single(result.Witnesses);
        Assert.Equal("pickup_eta_total_ms", witness.Dimension);
    }

    [Fact]
    public void Loosening_a_limit_never_removes_a_previously_allowed_delta()
    {
        var evaluator = new CommitmentBudgetEvaluator();

        for (var before = 0L; before <= 20; before++)
        {
            for (var delta = 0L; delta <= 20; delta++)
            {
                var tight = evaluator.Evaluate(
                    TestData.RequestOne,
                    RequestLifecycle.Accepted,
                    CommitmentTestData.Vector(
                        CommitmentDimension.PickupEtaTotalMs,
                        before),
                    CommitmentTestData.Vector(
                        CommitmentDimension.PickupEtaTotalMs,
                        delta),
                    CommitmentTestData.Policy(hardLimit: 20));
                var loose = evaluator.Evaluate(
                    TestData.RequestOne,
                    RequestLifecycle.Accepted,
                    CommitmentTestData.Vector(
                        CommitmentDimension.PickupEtaTotalMs,
                        before),
                    CommitmentTestData.Vector(
                        CommitmentDimension.PickupEtaTotalMs,
                        delta),
                    CommitmentTestData.Policy(hardLimit: 40));

                Assert.False(tight.IsAllowed && !loose.IsAllowed);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Dimensions))]
    public void Canonical_overflow_reports_the_exact_dimension(
        CommitmentDimension dimension)
    {
        var before = CommitmentTestData.Vector(
            dimension,
            RideBound.Domain.Common.DomainLimits.MaxCanonicalInteger);
        var delta = CommitmentTestData.Vector(dimension, 1);

        var result = new CommitmentBudgetEvaluator().Evaluate(
            TestData.RequestOne,
            RequestLifecycle.Accepted,
            before,
            delta,
            CommitmentTestData.Policy(hardLimit: null));

        var witness = Assert.Single(result.Witnesses);
        Assert.Equal(
            CommitmentDimensionVocabulary.ToProtocolValue(dimension),
            witness.Dimension);
        Assert.Equal(
            RideBound.Domain.Common.DomainLimits.MaxCanonicalInteger,
            witness.Before);
        Assert.Equal(1, witness.Delta);
        Assert.Equal(
            RideBound.Domain.Common.DomainLimits.MaxCanonicalInteger + 1,
            witness.After);
    }
}
