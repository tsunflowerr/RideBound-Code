using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Incidents;
using RideBound.Domain.Tests.Commitments;
using RideBound.Domain.Validation;

namespace RideBound.Domain.Tests.Incidents;

public sealed class OperationalIncidentLedgerTests
{
    [Fact]
    public void Resolve_preserves_incident_and_breach_history_without_refund()
    {
        var incidentId = new IncidentId("incident-1");
        var opened = OperationalIncidentLedger.Empty.Open(
            incidentId,
            "ROAD_CLOSED",
            [TestData.VehicleOne],
            [TestData.RequestOne],
            10,
            new SimTime(2_000));
        Assert.True(opened.IsSuccess, opened.Failure?.Message);
        var breach = Breach(incidentId);
        var appended = opened.Ledger!.AppendBreach(breach);
        Assert.True(appended.IsSuccess, appended.Failure?.Message);

        var resolved = appended.Ledger!.Resolve(
            incidentId,
            12,
            new SimTime(3_000));

        Assert.True(resolved.IsSuccess, resolved.Failure?.Message);
        Assert.False(resolved.Ledger!.Incidents[incidentId].IsOpen);
        Assert.Same(breach, Assert.Single(resolved.Ledger.Breaches));
        Assert.Equal(7, breach.BudgetBefore.PickupEtaTotalMs);
        Assert.Equal(11, breach.AttemptedBudgetAfter.PickupEtaTotalMs);
        Assert.False(breach.NormalOperation);
    }

    [Fact]
    public void Duplicate_unknown_and_stale_transitions_are_rejected()
    {
        var id = new IncidentId("incident-1");
        var opened = OperationalIncidentLedger.Empty.Open(
            id,
            "ROAD_CLOSED",
            [TestData.VehicleOne],
            [],
            10,
            new SimTime(2_000)).Ledger!;

        Assert.Equal(
            IncidentFailureCodes.DuplicateIncident,
            opened.Open(
                id,
                "ROAD_CLOSED",
                [TestData.VehicleOne],
                [],
                11,
                new SimTime(2_001)).Failure?.Code);
        Assert.Equal(
            IncidentFailureCodes.UnknownIncident,
            opened.Resolve(
                new IncidentId("unknown"),
                11,
                new SimTime(2_001)).Failure?.Code);
        var resolved = opened.Resolve(id, 11, new SimTime(2_001)).Ledger!;
        Assert.Equal(
            IncidentFailureCodes.StaleIncidentTransition,
            resolved.Resolve(id, 12, new SimTime(2_002)).Failure?.Code);
    }

    [Fact]
    public void Breach_requires_open_incident_and_affected_rider()
    {
        var id = new IncidentId("incident-1");
        var opened = OperationalIncidentLedger.Empty.Open(
            id,
            "ROAD_CLOSED",
            [TestData.VehicleOne],
            [],
            10,
            new SimTime(2_000)).Ledger!;

        Assert.Equal(
            IncidentFailureCodes.RiderNotAffected,
            opened.AppendBreach(Breach(id)).Failure?.Code);
    }

    [Fact]
    public void Breach_budget_and_affected_vehicle_are_cross_checked()
    {
        var id = new IncidentId("incident-1");
        var opened = OperationalIncidentLedger.Empty.Open(
            id,
            "ROAD_CLOSED",
            [TestData.VehicleOne],
            [TestData.RequestOne],
            10,
            new SimTime(2_000)).Ledger!;

        Assert.Throws<ArgumentException>(
            () => Breach(id, attemptedPickupEta: 12));
        Assert.Equal(
            IncidentFailureCodes.BreachIncidentMismatch,
            opened.AppendBreach(
                Breach(id, vehicleId: TestData.VehicleTwo)).Failure?.Code);
    }

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
        Assert.Equal(
            CommitmentBreachKind.ExogenousServiceQuality,
            stored.Kind);
        Assert.Null(stored.IncidentId);
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

    [Fact]
    public void Other_ledger_operations_keep_the_recorded_late_pickups()
    {
        var late = new ObservedLatePickup(
            TestData.RequestOne,
            TestData.VehicleOne,
            new SimTime(2_000),
            new SimTime(2_500),
            5,
            2);
        var ledger = OperationalIncidentLedger.Empty.RecordLatePickup(late).Ledger!;
        var incidentId = new IncidentId("incident-late");

        var opened = ledger.Open(
            incidentId,
            "VEHICLE_BREAKDOWN",
            [TestData.VehicleOne],
            [TestData.RequestOne],
            6,
            new SimTime(1_500)).Ledger!;
        var operational = opened.AppendBreach(Breach(incidentId));
        var exogenous = operational.Ledger!.AppendBreach(ExogenousBreach());
        var resolved = exogenous.Ledger!.Resolve(incidentId, 12, new SimTime(2_700)).Ledger!;

        Assert.True(operational.IsSuccess, operational.Failure?.Message);
        Assert.True(exogenous.IsSuccess, exogenous.Failure?.Message);
        Assert.Same(late, Assert.Single(opened.LatePickups));
        Assert.Same(late, Assert.Single(operational.Ledger.LatePickups));
        Assert.Same(late, Assert.Single(exogenous.Ledger.LatePickups));
        Assert.Same(late, Assert.Single(resolved.LatePickups));
        Assert.Equal(2, resolved.Breaches.Count);
    }

    /// <summary>An exogenous service-quality breach (no incident), as in the test above.</summary>
    private static CommitmentBreachRecord ExogenousBreach()
    {
        var projection = CommitmentTestData.Projection();
        var previous = new PublishedPromise(
            new PromiseVersion(1),
            1,
            new SimTime(1_000),
            projection);

        return CommitmentBreachRecord.CreateExogenousServiceQuality(
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
    }

    private static CommitmentBreachRecord Breach(
        IncidentId incidentId,
        long attemptedPickupEta = 11,
        VehicleId? vehicleId = null)
    {
        var projection = CommitmentTestData.Projection(vehicleId);
        var previous = new PublishedPromise(
            new PromiseVersion(1),
            1,
            new SimTime(1_000),
            projection);
        var delta = CommitmentTestData.Vector(
            CommitmentDimension.PickupEtaTotalMs,
            4);

        return new CommitmentBreachRecord(
            "breach-1",
            incidentId,
            TestData.RequestOne,
            previous,
            projection,
            projection,
            new ThreeWayPromiseDelta(CommitmentVector.Zero, delta, delta),
            CommitmentTestData.Vector(
                CommitmentDimension.PickupEtaTotalMs,
                7),
            CommitmentTestData.Vector(
                CommitmentDimension.PickupEtaTotalMs,
                attemptedPickupEta),
            ["pickup_eta_total_ms"],
            11,
            2,
            new SimTime(2_000));
    }
}
