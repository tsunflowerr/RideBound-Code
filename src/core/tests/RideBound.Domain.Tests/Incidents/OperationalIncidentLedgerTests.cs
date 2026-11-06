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
    public void Forced_reference_breach_charges_the_visible_delta_and_needs_no_incident()
    {
        var drift = CommitmentTestData.Vector(CommitmentDimension.DropEtaTotalMs, 40);
        var breach = ForcedReference(
            new ThreeWayPromiseDelta(drift, CommitmentVector.Zero, drift),
            before: 25,
            after: 65,
            [CommitmentFailureCodes.BudgetExceeded]);

        var appended = OperationalIncidentLedger.Empty.AppendBreach(breach);

        Assert.True(appended.IsSuccess, appended.Failure?.Message);
        var stored = Assert.Single(appended.Ledger!.Breaches);
        Assert.Equal(CommitmentBreachKind.ForcedReference, stored.Kind);
        Assert.Null(stored.IncidentId);
        Assert.Empty(stored.ServiceQualityWitnesses);
        Assert.Equal(65, stored.AttemptedBudgetAfter.DropEtaTotalMs);
        Assert.False(stored.NormalOperation);
    }

    [Fact]
    public void Forced_reference_breach_accepts_exactly_the_two_basis_charges()
    {
        // Under decisionInduced the kept route adds nothing; under customerVisible it adds
        // the visible drift. Any other charge is not a charge by either basis.
        var drift = CommitmentTestData.Vector(CommitmentDimension.DropEtaTotalMs, 40);
        var deltas = new ThreeWayPromiseDelta(drift, CommitmentVector.Zero, drift);

        var decisionBasis = ForcedReference(
            deltas,
            before: 25,
            after: 25,
            [CommitmentFailureCodes.DeadlineExceeded]);
        var visibleBasis = ForcedReference(
            deltas,
            before: 25,
            after: 65,
            [CommitmentFailureCodes.DeadlineExceeded]);

        Assert.Equal(25, decisionBasis.AttemptedBudgetAfter.DropEtaTotalMs);
        Assert.Equal(65, visibleBasis.AttemptedBudgetAfter.DropEtaTotalMs);
        Assert.Throws<ArgumentException>(
            () => ForcedReference(
                deltas,
                before: 25,
                after: 26,
                [CommitmentFailureCodes.DeadlineExceeded]));
    }

    [Fact]
    public void Forced_reference_breach_rejects_a_phase_lock_and_a_differing_kept_projection()
    {
        // A lock compares with the exogenous projection, which a kept route equals, so a
        // phase lock on a kept route would be a defect. The published projection must be
        // the kept route's, which is the exogenous one.
        var drift = CommitmentTestData.Vector(CommitmentDimension.DropEtaTotalMs, 40);
        var deltas = new ThreeWayPromiseDelta(drift, CommitmentVector.Zero, drift);

        Assert.Throws<ArgumentException>(
            () => ForcedReference(deltas, 25, 25, [CommitmentFailureCodes.PhaseLock]));
        Assert.Throws<ArgumentException>(
            () => ForcedReference(
                deltas,
                25,
                25,
                [CommitmentFailureCodes.DeadlineExceeded],
                keptRoute: CommitmentTestData.Projection(TestData.VehicleTwo)));
    }

    [Fact]
    public void Forced_reference_breach_rejects_a_decision_delta_a_wrong_budget_and_foreign_codes()
    {
        var drift = CommitmentTestData.Vector(CommitmentDimension.DropEtaTotalMs, 40);
        var decision = CommitmentTestData.Vector(CommitmentDimension.DropEtaTotalMs, 5);

        // Keeping the route adds no decision-induced change.
        Assert.Throws<ArgumentException>(
            () => ForcedReference(
                new ThreeWayPromiseDelta(drift, decision, drift),
                25,
                30,
                [CommitmentFailureCodes.BudgetExceeded]));
        // The budget must follow one of the two bases exactly.
        Assert.Throws<ArgumentException>(
            () => ForcedReference(
                new ThreeWayPromiseDelta(drift, CommitmentVector.Zero, drift),
                25,
                50,
                [CommitmentFailureCodes.BudgetExceeded]));
        // With no decision change the visible delta is the exogenous one.
        Assert.Throws<ArgumentException>(
            () => ForcedReference(
                new ThreeWayPromiseDelta(drift, CommitmentVector.Zero, decision),
                25,
                25,
                [CommitmentFailureCodes.BudgetExceeded]));
        // Only commitment-gate codes; a service-quality code belongs to the exogenous kind.
        Assert.Throws<ArgumentException>(
            () => ForcedReference(
                new ThreeWayPromiseDelta(drift, CommitmentVector.Zero, drift),
                25,
                25,
                [PhysicalViolationCodes.PickupWindow]));
    }

    [Fact]
    public void Forced_reference_breach_identifier_is_unique_in_the_ledger()
    {
        var drift = CommitmentTestData.Vector(CommitmentDimension.DropEtaTotalMs, 40);
        var breach = ForcedReference(
            new ThreeWayPromiseDelta(drift, CommitmentVector.Zero, drift),
            25,
            25,
            [CommitmentFailureCodes.DeadlineExceeded]);
        var once = OperationalIncidentLedger.Empty.AppendBreach(breach).Ledger!;

        Assert.Equal(
            IncidentFailureCodes.DuplicateBreach,
            once.AppendBreach(breach).Failure?.Code);
    }

    [Fact]
    public void No_worse_breach_records_a_changed_route_of_the_same_vehicle_without_an_incident()
    {
        // Kept route drops at 20 000 ms; the changed route drops earlier, at 19 990 ms: 10 ms of
        // decision-induced change on top of the 40 ms drift, charged under the visible basis.
        var drift = CommitmentTestData.Vector(CommitmentDimension.DropEtaTotalMs, 40);
        var decision = CommitmentTestData.Vector(CommitmentDimension.DropEtaTotalMs, 10);
        var visible = CommitmentTestData.Vector(CommitmentDimension.DropEtaTotalMs, 30);
        var breach = NoWorse(
            new ThreeWayPromiseDelta(drift, decision, visible),
            before: 25,
            after: 55,
            [CommitmentFailureCodes.DeadlineExceeded],
            CommitmentTestData.Projection(dropEta: 19_990));

        var appended = OperationalIncidentLedger.Empty.AppendBreach(breach);

        Assert.True(appended.IsSuccess, appended.Failure?.Message);
        var stored = Assert.Single(appended.Ledger!.Breaches);
        Assert.Equal(CommitmentBreachKind.ForcedNoWorse, stored.Kind);
        Assert.Null(stored.IncidentId);
        Assert.False(stored.NormalOperation);
        Assert.Equal(19_990, stored.SafetyProjection.DropEta.Milliseconds);
    }

    [Fact]
    public void No_worse_breach_rejects_an_unchanged_route_and_another_vehicle()
    {
        var drift = CommitmentTestData.Vector(CommitmentDimension.DropEtaTotalMs, 40);
        var deltas = new ThreeWayPromiseDelta(drift, CommitmentVector.Zero, drift);

        // An unchanged route is a kept-route (ForcedReference) record, not a no-worse one.
        Assert.Throws<ArgumentException>(
            () => NoWorse(deltas, 25, 25, [CommitmentFailureCodes.DeadlineExceeded],
                CommitmentTestData.Projection()));
        // The changed route belongs to the same vehicle: a reassignment is never no-worse.
        Assert.Throws<ArgumentException>(
            () => NoWorse(deltas, 25, 25, [CommitmentFailureCodes.DeadlineExceeded],
                CommitmentTestData.Projection(TestData.VehicleTwo, dropEta: 19_990)));
    }

    [Fact]
    public void No_worse_breach_rejects_foreign_codes_and_a_charge_outside_both_bases()
    {
        var drift = CommitmentTestData.Vector(CommitmentDimension.DropEtaTotalMs, 40);
        var deltas = new ThreeWayPromiseDelta(drift, CommitmentVector.Zero, drift);
        var changed = CommitmentTestData.Projection(dropEta: 19_990);

        Assert.Throws<ArgumentException>(
            () => NoWorse(deltas, 25, 25, [CommitmentFailureCodes.PhaseLock], changed));
        Assert.Throws<ArgumentException>(
            () => NoWorse(deltas, 25, 26, [CommitmentFailureCodes.DeadlineExceeded], changed));
    }

    [Fact]
    public void Forced_breaches_reject_the_revision_limit_code()
    {
        // The per-decision revision cap compares with the exogenous projection, like a lock, so
        // a kept route never trips it; a forced breach naming it would be a defect and fails
        // closed. The same records with the deadline code are accepted.
        var drift = CommitmentTestData.Vector(CommitmentDimension.DropEtaTotalMs, 40);
        var deltas = new ThreeWayPromiseDelta(drift, CommitmentVector.Zero, drift);
        var changed = CommitmentTestData.Projection(dropEta: 19_990);

        Assert.DoesNotContain(
            CommitmentFailureCodes.RevisionLimitExceeded,
            CommitmentBreachRecord.ForcedReferenceWitnessCodes);
        Assert.Throws<ArgumentException>(
            () => ForcedReference(
                deltas,
                25,
                25,
                [CommitmentFailureCodes.RevisionLimitExceeded]));
        Assert.Throws<ArgumentException>(
            () => NoWorse(
                deltas,
                25,
                25,
                [CommitmentFailureCodes.RevisionLimitExceeded],
                changed));
        Assert.Equal(
            [CommitmentFailureCodes.DeadlineExceeded],
            ForcedReference(deltas, 25, 25, [CommitmentFailureCodes.DeadlineExceeded])
                .WitnessCodes);
        Assert.Equal(
            [CommitmentFailureCodes.DeadlineExceeded],
            NoWorse(deltas, 25, 25, [CommitmentFailureCodes.DeadlineExceeded], changed)
                .WitnessCodes);
    }

    [Fact]
    public void Projection_equality_is_field_by_field()
    {
        Assert.True(
            CommitmentBreachRecord.ProjectionsEqual(
                CommitmentTestData.Projection(),
                CommitmentTestData.Projection()));
        Assert.False(
            CommitmentBreachRecord.ProjectionsEqual(
                CommitmentTestData.Projection(),
                CommitmentTestData.Projection(dropEta: 19_990)));
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
        var drift = CommitmentTestData.Vector(CommitmentDimension.DropEtaTotalMs, 40);
        var forced = operational.Ledger!.AppendBreach(
            ForcedReference(
                new ThreeWayPromiseDelta(drift, CommitmentVector.Zero, drift),
                25,
                25,
                [CommitmentFailureCodes.DeadlineExceeded]));
        var resolved = forced.Ledger!.Resolve(incidentId, 12, new SimTime(2_700)).Ledger!;

        Assert.True(operational.IsSuccess, operational.Failure?.Message);
        Assert.True(forced.IsSuccess, forced.Failure?.Message);
        Assert.Same(late, Assert.Single(opened.LatePickups));
        Assert.Same(late, Assert.Single(operational.Ledger.LatePickups));
        Assert.Same(late, Assert.Single(forced.Ledger.LatePickups));
        Assert.Same(late, Assert.Single(resolved.LatePickups));
        Assert.Equal(2, resolved.Breaches.Count);
    }

    private static CommitmentBreachRecord ForcedReference(
        ThreeWayPromiseDelta deltas,
        long before,
        long after,
        IEnumerable<string> witnessCodes,
        PromiseProjection? keptRoute = null)
    {
        var projection = CommitmentTestData.Projection();
        var previous = new PublishedPromise(
            new PromiseVersion(1),
            1,
            new SimTime(1_000),
            projection);

        return CommitmentBreachRecord.CreateForcedReference(
            "forced-breach-1",
            TestData.RequestOne,
            previous,
            projection,
            keptRoute ?? projection,
            deltas,
            CommitmentTestData.Vector(CommitmentDimension.DropEtaTotalMs, before),
            CommitmentTestData.Vector(CommitmentDimension.DropEtaTotalMs, after),
            witnessCodes,
            11,
            2,
            new SimTime(2_000));
    }

    private static CommitmentBreachRecord NoWorse(
        ThreeWayPromiseDelta deltas,
        long before,
        long after,
        IEnumerable<string> witnessCodes,
        PromiseProjection published)
    {
        var kept = CommitmentTestData.Projection();
        var previous = new PublishedPromise(
            new PromiseVersion(1),
            1,
            new SimTime(1_000),
            kept);

        return CommitmentBreachRecord.CreateForcedNoWorse(
            "no-worse-breach-1",
            TestData.RequestOne,
            previous,
            kept,
            published,
            deltas,
            CommitmentTestData.Vector(CommitmentDimension.DropEtaTotalMs, before),
            CommitmentTestData.Vector(CommitmentDimension.DropEtaTotalMs, after),
            witnessCodes,
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
