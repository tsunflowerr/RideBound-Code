using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Incidents;
using RideBound.Domain.Tests.Commitments;
using RideBound.Domain.Validation;

namespace RideBound.Domain.Tests.Incidents;

public sealed class OperationalIncidentLedgerTests
{
    [Fact]
    public void Exogenous_service_quality_breach_needs_no_incident_and_charges_no_budget()
    {
        var projection = CommitmentTestData.Projection();
        var previous = new PublishedPromise(
            new PromiseVersion(1),
            1,
            new SimTime(1_000),
            projection);
        var breach = CommitmentBreachRecord.CreateExogenousServiceQuality(
            "exogenous-breach-1",
            TestData.RequestOne,
            previous,
            projection,
            projection,
            new ThreeWayPromiseDelta(
                CommitmentVector.Zero,
                CommitmentVector.Zero,
                CommitmentVector.Zero),
            CommitmentTestData.Vector(
                CommitmentDimension.PickupEtaTotalMs,
                7),
            CommitmentTestData.Vector(
                CommitmentDimension.PickupEtaTotalMs,
                7),
            [PhysicalViolationCodes.PickupWindow],
            [
                new ServiceQualityBreach(
                    TestData.RequestOne,
                    PhysicalViolationCodes.PickupWindow,
                    "latestPickupMs",
                    100,
                    101),
            ],
            11,
            2,
            new SimTime(2_000));

        var appended = OperationalIncidentLedger.Empty.AppendBreach(breach);

        Assert.True(appended.IsSuccess, appended.Failure?.Message);
        var stored = Assert.Single(appended.Ledger!.Breaches);
        Assert.Equal(stored.BudgetBefore, stored.AttemptedBudgetAfter);
        Assert.Equal(CommitmentVector.Zero, stored.Deltas.DecisionInduced);
        Assert.Single(stored.ServiceQualityWitnesses);
    }

    [Fact]
    public void Exogenous_service_quality_breach_rejects_non_breach_and_wrong_dimension()
    {
        var projection = CommitmentTestData.Projection();
        var previous = new PublishedPromise(
            new PromiseVersion(1),
            1,
            new SimTime(1_000),
            projection);

        Assert.Throws<ArgumentException>(
            () => CommitmentBreachRecord.CreateExogenousServiceQuality(
                "exogenous-breach-1",
                TestData.RequestOne,
                previous,
                projection,
                projection,
                new ThreeWayPromiseDelta(
                    CommitmentVector.Zero,
                    CommitmentVector.Zero,
                    CommitmentVector.Zero),
                CommitmentVector.Zero,
                CommitmentVector.Zero,
                [PhysicalViolationCodes.PickupWindow],
                [
                    new ServiceQualityBreach(
                        TestData.RequestOne,
                        PhysicalViolationCodes.PickupWindow,
                        "maxRideTimeMs",
                        100,
                        100),
                ],
                11,
                2,
                new SimTime(2_000)));

        Assert.Throws<ArgumentException>(
            () => CommitmentBreachRecord.CreateExogenousServiceQuality(
                "exogenous-breach-2",
                TestData.RequestOne,
                previous,
                projection,
                projection,
                new ThreeWayPromiseDelta(
                    CommitmentVector.Zero,
                    CommitmentVector.Zero,
                    CommitmentVector.Zero),
                CommitmentVector.Zero,
                CommitmentTestData.Vector(
                    CommitmentDimension.PickupEtaTotalMs,
                    1),
                [PhysicalViolationCodes.PickupWindow],
                [
                    new ServiceQualityBreach(
                        TestData.RequestOne,
                        PhysicalViolationCodes.PickupWindow,
                        "latestPickupMs",
                        100,
                        101),
                ],
                11,
                2,
                new SimTime(2_000)));
    }

    [Fact]
    public void A_late_pickup_is_after_the_window_and_recorded_once_per_rider()
    {
        var late = new ObservedLatePickup(
            TestData.RequestOne,
            TestData.VehicleOne,
            new SimTime(2_000),
            new SimTime(2_500),
            5,
            2);

        var once = OperationalIncidentLedger.Empty.RecordLatePickup(late);

        Assert.True(once.IsSuccess, once.Failure?.Message);
        Assert.Equal(500, Assert.Single(once.Ledger!.LatePickups).LatenessMilliseconds);
        Assert.Empty(OperationalIncidentLedger.Empty.LatePickups);
        Assert.Equal(
            IncidentFailureCodes.DuplicateLatePickup,
            once.Ledger.RecordLatePickup(late).Failure?.Code);
        // On time is not late, and a record needs a real event and epoch.
        Assert.Throws<ArgumentException>(
            () => new ObservedLatePickup(
                TestData.RequestOne,
                TestData.VehicleOne,
                new SimTime(2_000),
                new SimTime(2_000),
                5,
                2));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ObservedLatePickup(
                TestData.RequestOne,
                TestData.VehicleOne,
                new SimTime(2_000),
                new SimTime(2_500),
                0,
                2));
    }
}
