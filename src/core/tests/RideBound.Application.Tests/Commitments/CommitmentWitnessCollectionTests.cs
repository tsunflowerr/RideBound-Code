using RideBound.Application.Commitments;
using RideBound.Domain.Commitments;

namespace RideBound.Application.Tests.Commitments;

/// <summary>
/// RB-WP14-003. Collecting the full witness set must widen the diagnostics without
/// moving the accept/reject line, and must stay off unless an evidence profile
/// asks for it.
/// </summary>
public sealed class CommitmentWitnessCollectionTests
{
    [Fact]
    public void Collecting_witnesses_is_off_by_default()
    {
        var context = CommitmentValidatorFixtures.OverBudget().Context;

        Assert.False(context.CollectAllCommitmentWitnesses);
    }

    [Fact]
    public void The_verdict_is_identical_with_and_without_collection()
    {
        foreach (var hardLimit in new long?[] { 0, 5, 10, 1_000, null })
        {
            var failFast = CommitmentValidatorFixtures.WithHardLimit(hardLimit);
            var collecting = CommitmentValidatorFixtures.WithHardLimit(hardLimit);
            var validator = new CommitmentDecisionValidator();

            var baseline = validator.Validate(failFast.Context);
            var full = validator.Validate(
                collecting.Context with { CollectAllCommitmentWitnesses = true });

            Assert.Equal(baseline.IsValid, full.IsValid);

            if (baseline.IsValid)
            {
                Assert.Equal(
                    baseline.Publications.Count,
                    full.Publications.Count);
                Assert.Empty(full.Witnesses);
                continue;
            }

            // Fail-fast reports the first failing layer; collecting reports at
            // least that same witness and never fewer.
            Assert.NotEmpty(full.Witnesses);
            Assert.True(full.Witnesses.Count >= baseline.Witnesses.Count);
            Assert.Contains(
                full.Witnesses,
                witness => witness.Code == baseline.Witnesses[0].Code
                    && witness.Dimension == baseline.Witnesses[0].Dimension);
        }
    }

    [Fact]
    public void A_rejected_candidate_never_advances_the_ledger_while_collecting()
    {
        var fixture = CommitmentValidatorFixtures.OverBudget();

        var result = new CommitmentDecisionValidator().Validate(
            fixture.Context with { CollectAllCommitmentWitnesses = true });

        Assert.False(result.IsValid);
        Assert.Null(result.ValidatedState);
        Assert.Empty(result.Publications);
        Assert.Single(
            fixture.Context.ReducedState.Commitments.Histories[
                ApplicationTestData.RequestId].Entries);
    }

    [Fact]
    public void Fail_fast_hides_the_budget_layer_behind_the_lock_layer()
    {
        var fixture = CommitmentValidatorFixtures.LockAndBudget();
        var validator = new CommitmentDecisionValidator();

        var failFast = validator.Validate(fixture.Context);
        var full = validator.Validate(
            fixture.Context with { CollectAllCommitmentWitnesses = true });

        Assert.False(failFast.IsValid);
        Assert.False(full.IsValid);

        // Fail-fast stops at the lock layer and never reaches the budget layer,
        // so the recorded attribution is incomplete for this candidate.
        Assert.All(
            failFast.Witnesses,
            witness => Assert.Equal(
                CommitmentValidationStage.Lock,
                witness.Stage));
        Assert.Contains(
            full.Witnesses,
            witness => witness.Stage == CommitmentValidationStage.Lock
                && witness.Code == CommitmentFailureCodes.PhaseLock);
        Assert.Contains(
            full.Witnesses,
            witness => witness.Stage == CommitmentValidationStage.Budget
                && witness.Code == CommitmentFailureCodes.BudgetExceeded);
        Assert.True(full.Witnesses.Count > failFast.Witnesses.Count);
    }

    [Fact]
    public void Collecting_still_reports_the_budget_dimension_that_failed()
    {
        var fixture = CommitmentValidatorFixtures.OverBudget();

        var result = new CommitmentDecisionValidator().Validate(
            fixture.Context with { CollectAllCommitmentWitnesses = true });

        var witness = Assert.Single(
            result.Witnesses,
            value => value.Dimension == "drop_eta_total_ms");
        Assert.Equal(CommitmentValidationStage.Budget, witness.Stage);
        Assert.Equal(CommitmentFailureCodes.BudgetExceeded, witness.Code);
    }
}
