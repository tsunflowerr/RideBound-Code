using RideBound.Domain.Commitments;
using RideBound.Domain.Common;

namespace RideBound.Domain.Tests.Commitments;

public sealed class CommitmentLedgerTests
{
    [Fact]
    public void Initial_promise_opens_version_one_without_consuming_budget()
    {
        var opened = CommitmentLedger.Empty.OpenInitial(
            "pub-1",
            CommitmentTestData.Projection(),
            1,
            new SimTime(1_000),
            "INITIAL_ACCEPTANCE",
            3);

        Assert.True(opened.IsSuccess, opened.Failure?.Message);
        Assert.Empty(CommitmentLedger.Empty.Histories);
        var current = opened.Ledger!.Histories[TestData.RequestOne].Current;
        Assert.Equal(CommitmentLedgerEntryKind.InitialPromise, current.Kind);
        Assert.Equal(1, current.PublishedPromise.Version.Value);
        Assert.Equal(CommitmentVector.Zero, current.BudgetBefore);
        Assert.Equal(CommitmentVector.Zero, current.BudgetAfter);
        Assert.Equal(CommitmentVector.Zero, current.Deltas.DecisionInduced);
    }

    [Fact]
    public void Revision_conserves_vector_and_does_not_refund_a_round_trip()
    {
        var opened = CommitmentLedger.Empty.OpenInitial(
            "pub-1",
            CommitmentTestData.Projection(),
            1,
            new SimTime(1_000),
            "INITIAL_ACCEPTANCE",
            3).Ledger!;
        var changed = CommitmentTestData.Projection(
            vehicleId: TestData.VehicleTwo,
            pickupEta: 10_004);
        var delta = new CommitmentVector(
            4,
            0,
            1,
            1,
            0,
            0,
            0,
            0,
            0,
            0);
        var first = opened.AppendRevision(
            "pub-2",
            TestData.RequestOne,
            new PromiseVersion(1),
            CommitmentTestData.Projection(),
            changed,
            new ThreeWayPromiseDelta(
                CommitmentVector.Zero,
                delta,
                delta),
            2,
            new SimTime(2_000),
            "REPLAN",
            4);

        Assert.True(first.IsSuccess, first.Failure?.Message);
        var returned = CommitmentTestData.Projection();
        var returnDelta = new CommitmentVector(
            4,
            0,
            1,
            1,
            0,
            0,
            0,
            0,
            0,
            0);
        var second = first.Ledger!.AppendRevision(
            "pub-3",
            TestData.RequestOne,
            new PromiseVersion(2),
            changed,
            returned,
            new ThreeWayPromiseDelta(
                CommitmentVector.Zero,
                returnDelta,
                returnDelta),
            3,
            new SimTime(3_000),
            "REPLAN",
            5);

        Assert.True(second.IsSuccess, second.Failure?.Message);
        var history = second.Ledger!.Histories[TestData.RequestOne];
        Assert.Collection(
            history.Entries,
            _ => { },
            _ => { },
            _ => { });
        Assert.Equal(8, history.Current.BudgetAfter.PickupEtaTotalMs);
        Assert.Equal(2, history.Current.BudgetAfter.VehicleSwitchCount);
        Assert.Equal(
            history.Current.BudgetBefore.Add(returnDelta).Value,
            history.Current.BudgetAfter);
        Assert.Single(opened.Histories[TestData.RequestOne].Entries);
    }

    [Fact]
    public void Stale_version_conflict_leaves_original_history_unchanged()
    {
        var ledger = CommitmentLedger.Empty.OpenInitial(
            "pub-1",
            CommitmentTestData.Projection(),
            1,
            new SimTime(1_000),
            "INITIAL_ACCEPTANCE",
            3).Ledger!;

        var result = ledger.AppendRevision(
            "pub-2",
            TestData.RequestOne,
            new PromiseVersion(2),
            CommitmentTestData.Projection(),
            CommitmentTestData.Projection(pickupEta: 10_001),
            new ThreeWayPromiseDelta(
                CommitmentVector.Zero,
                CommitmentTestData.Vector(
                    CommitmentDimension.PickupEtaTotalMs,
                    1),
                CommitmentTestData.Vector(
                    CommitmentDimension.PickupEtaTotalMs,
                    1)),
            2,
            new SimTime(2_000),
            "REPLAN",
            4);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            CommitmentFailureCodes.LedgerConflict,
            result.Failure?.Code);
        Assert.Single(ledger.Histories[TestData.RequestOne].Entries);
    }

    [Fact]
    public void Publication_identifier_is_unique_across_riders()
    {
        var first = CommitmentLedger.Empty.OpenInitial(
            "pub-1",
            CommitmentTestData.Projection(),
            1,
            new SimTime(1_000),
            "INITIAL_ACCEPTANCE",
            3).Ledger!;
        var requestTwo = new RequestId("r-2");
        var pickup = new StopId("pickup-2");
        var drop = new StopId("drop-2");
        var secondProjection = new PromiseProjection(
            requestTwo,
            TestData.VehicleOne,
            pickup,
            TestData.NodeOne,
            drop,
            TestData.NodeTwo,
            new SimTime(11_000),
            new SimTime(21_000),
            [
                new PromiseServiceToken(
                    pickup,
                    requestTwo,
                    RideBound.Domain.Routes.RouteStopKind.Pickup),
                new PromiseServiceToken(
                    drop,
                    requestTwo,
                    RideBound.Domain.Routes.RouteStopKind.DropOff),
            ]);

        var duplicate = first.OpenInitial(
            "pub-1",
            secondProjection,
            2,
            new SimTime(2_000),
            "INITIAL_ACCEPTANCE",
            4);

        Assert.False(duplicate.IsSuccess);
        Assert.Equal("publicationId", duplicate.Failure?.Dimension);
        Assert.Single(first.Histories);
    }

    /// <summary>
    /// ADR-089: the persistent publication set must reject every identifier any earlier entry
    /// used (the initial promise and earlier revisions), as the old scan of every entry did.
    /// </summary>
    [Fact]
    public void Revision_rejects_the_publication_identifier_of_any_earlier_entry()
    {
        var delta = new ThreeWayPromiseDelta(
            CommitmentVector.Zero,
            CommitmentTestData.Vector(CommitmentDimension.PickupEtaTotalMs, 1),
            CommitmentTestData.Vector(CommitmentDimension.PickupEtaTotalMs, 1));
        var opened = CommitmentLedger.Empty.OpenInitial(
            "pub-1",
            CommitmentTestData.Projection(),
            1,
            new SimTime(1_000),
            "INITIAL_ACCEPTANCE",
            3).Ledger!;
        var revised = opened.AppendRevision(
            "pub-2",
            TestData.RequestOne,
            new PromiseVersion(1),
            CommitmentTestData.Projection(),
            CommitmentTestData.Projection(pickupEta: 10_001),
            delta,
            2,
            new SimTime(2_000),
            "REPLAN",
            4);
        Assert.True(revised.IsSuccess, revised.Failure?.Message);

        CommitmentLedgerAppendResult Revise(string publicationId) =>
            revised.Ledger!.AppendRevision(
                publicationId,
                TestData.RequestOne,
                new PromiseVersion(2),
                CommitmentTestData.Projection(pickupEta: 10_001),
                CommitmentTestData.Projection(pickupEta: 10_002),
                delta,
                3,
                new SimTime(3_000),
                "REPLAN",
                5);

        foreach (var reused in new[] { "pub-1", "pub-2" })
        {
            var rejected = Revise(reused);
            Assert.False(rejected.IsSuccess);
            Assert.Equal("publicationId", rejected.Failure?.Dimension);
        }

        Assert.True(Revise("pub-3").IsSuccess);
        Assert.False(opened.AppendRevision(
            "pub-1",
            TestData.RequestOne,
            new PromiseVersion(1),
            CommitmentTestData.Projection(),
            CommitmentTestData.Projection(pickupEta: 10_001),
            delta,
            2,
            new SimTime(2_000),
            "REPLAN",
            4).IsSuccess);
        Assert.Equal(2, revised.Ledger!.Histories[TestData.RequestOne].Entries.Count);
        Assert.Single(opened.Histories[TestData.RequestOne].Entries);
    }

    [Fact]
    public void Deterministic_random_histories_conserve_all_dimensions_without_refund()
    {
        const int publishedSeedCount = 64;
        const int revisionsPerHistory = 12;

        for (var seed = 0; seed < publishedSeedCount; seed++)
        {
            var random = new Random(seed);
            var initial = CommitmentLedger.Empty.OpenInitial(
                $"seed-{seed}-publication-0",
                CommitmentTestData.Projection(),
                1,
                new SimTime(1_000),
                "INITIAL_ACCEPTANCE",
                1).Ledger!;
            var ledger = initial;
            var expected = CommitmentVector.Zero;

            for (var revision = 1; revision <= revisionsPerHistory; revision++)
            {
                var delta = new CommitmentVector(
                    random.Next(0, 8),
                    random.Next(0, 8),
                    random.Next(0, 3),
                    random.Next(0, 2),
                    random.Next(0, 8),
                    random.Next(0, 2),
                    random.Next(0, 8),
                    random.Next(0, 2),
                    random.Next(0, 4),
                    random.Next(0, 4));
                var appended = ledger.AppendRevision(
                    $"seed-{seed}-publication-{revision}",
                    TestData.RequestOne,
                    new PromiseVersion(revision),
                    CommitmentTestData.Projection(),
                    CommitmentTestData.Projection(),
                    new ThreeWayPromiseDelta(
                        CommitmentVector.Zero,
                        delta,
                        delta),
                    revision + 1,
                    new SimTime(1_000 + revision),
                    "PROPERTY_REPLAN",
                    revision + 1);

                Assert.True(
                    appended.IsSuccess,
                    $"seed={seed}; revision={revision}; " +
                    appended.Failure?.Message);
                ledger = appended.Ledger!;
                expected = expected.Add(delta).Value!;
                var current = ledger.Histories[TestData.RequestOne].Current;
                Assert.Equal(expected, current.BudgetAfter);
                Assert.Equal(
                    current.BudgetBefore.Add(delta).Value,
                    current.BudgetAfter);
            }

            Assert.Single(initial.Histories[TestData.RequestOne].Entries);
            Assert.Equal(
                revisionsPerHistory + 1,
                ledger.Histories[TestData.RequestOne].Entries.Count);
        }
    }
}
