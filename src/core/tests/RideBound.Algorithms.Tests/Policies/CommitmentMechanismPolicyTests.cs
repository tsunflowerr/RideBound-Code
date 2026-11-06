using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Commitments;
using RideBound.Algorithms.Policies;
using RideBound.Application.Commitments;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Validation;

namespace RideBound.Algorithms.Tests.Policies;

public sealed class CommitmentMechanismPolicyTests
{
    [Fact]
    public void Mechanism_providers_remove_every_cumulative_limit()
    {
        var source = new CommitmentPolicyCatalog([SourcePolicy(hardLimit: 0)]);
        var b2 = MechanismCommitmentPolicyProvider.RevisionPenalty(source);
        var b3 = MechanismCommitmentPolicyProvider.FixedFreeze(
            source,
            new Duration(500),
            PromiseLock.PickupEta | PromiseLock.DropEta);

        Assert.True(b2.TryGetPolicy("uniform-v1", out var penalty));
        Assert.True(b3.TryGetPolicy("uniform-v1", out var freeze));
        Assert.All(penalty.Limits.Values, limit => Assert.Null(limit.HardLimit));
        Assert.All(freeze.Limits.Values, limit => Assert.Null(limit.HardLimit));
        Assert.Null(penalty.FreezeHorizon);
        Assert.Equal(PromiseLock.None, penalty.FreezeHorizonLocks);
        Assert.Equal(500, freeze.FreezeHorizon!.Value.Milliseconds);
        Assert.Equal(
            PromiseLock.PickupEta | PromiseLock.DropEta,
            freeze.FreezeHorizonLocks);
        Assert.Equal(PromiseLock.None, freeze.FinalConfirmationLocks);
    }

    [Fact]
    public void Fixed_freeze_requires_explicit_positive_horizon_and_locks()
    {
        var source = new CommitmentPolicyCatalog([SourcePolicy(null)]);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => MechanismCommitmentPolicyProvider.FixedFreeze(
                source,
                new Duration(0),
                PromiseLock.PickupEta));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MechanismCommitmentPolicyProvider.FixedFreeze(
                source,
                new Duration(1),
                PromiseLock.None));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new FixedFreezeHorizonPolicy(
                new Duration(0),
                PromiseLock.PickupEta));
    }

    [Fact]
    public void B2_acceptance_dominates_revision_and_revision_dominates_cost()
    {
        var request = AlgorithmTestData.PendingRequest();
        var vehicle = AlgorithmTestData.Vehicle();
        var noOp = Candidate("noop", vehicle.Id, [], cost: 0);
        var lowerRevision = Candidate(
            "accept-lower-revision",
            vehicle.Id,
            [request.Id],
            cost: 100);
        var lowerCost = Candidate(
            "accept-lower-cost",
            vehicle.Id,
            [request.Id],
            cost: 1);
        var materialButCheap = Candidate(
            "accept-material-cheap",
            vehicle.Id,
            [request.Id],
            cost: 0);
        var set = new VehicleCandidateSet(
            vehicle.Id,
            [noOp, lowerRevision, lowerCost, materialButCheap],
            [],
            false);
        var assessments = new Dictionary<string, CandidateCommitmentAssessment>(
            StringComparer.Ordinal)
        {
            [noOp.CandidateId] = Assessment(noOp, CommitmentVector.Zero),
            [lowerRevision.CandidateId] = Assessment(
                lowerRevision,
                Vector(pickupEta: 1)),
            [lowerCost.CandidateId] = Assessment(
                lowerCost,
                Vector(pickupEta: 2)),
            [materialButCheap.CandidateId] = Assessment(
                materialButCheap,
                Vector(material: 1)),
        };

        var result = new RevisionPenaltyFleetSelector().Select(
            [set],
            assessments);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var selected = Assert.Single(result.Selection!.VehiclePlans).Candidate;
        Assert.Equal(lowerRevision.CandidateId, selected.CandidateId);
        Assert.Equal(1, result.Selection.AcceptedRequestCount);
        Assert.Equal(1, result.Selection.DecisionInducedRevision!.PickupEtaTotalMs);
        Assert.Equal(100, result.Selection.OperationalCost);
    }

    [Fact]
    public void B2_uses_stable_dimension_order_before_operational_cost()
    {
        var request = AlgorithmTestData.PendingRequest();
        var vehicle = AlgorithmTestData.Vehicle();
        var lowerPickupHigherDrop = Candidate(
            "lower-pickup",
            vehicle.Id,
            [request.Id],
            cost: 1000);
        var higherPickupLowerDrop = Candidate(
            "higher-pickup",
            vehicle.Id,
            [request.Id],
            cost: 0);
        var noOp = Candidate("noop", vehicle.Id, [], 0);
        var assessments = new Dictionary<string, CandidateCommitmentAssessment>(
            StringComparer.Ordinal)
        {
            [noOp.CandidateId] = Assessment(noOp, CommitmentVector.Zero),
            [lowerPickupHigherDrop.CandidateId] = Assessment(
                lowerPickupHigherDrop,
                Vector(pickupEta: 1, dropEta: 999)),
            [higherPickupLowerDrop.CandidateId] = Assessment(
                higherPickupLowerDrop,
                Vector(pickupEta: 2, dropEta: 0)),
        };
        var result = new RevisionPenaltyFleetSelector().Select(
            [
                new VehicleCandidateSet(
                    vehicle.Id,
                    [noOp, lowerPickupHigherDrop, higherPickupLowerDrop],
                    [],
                    false),
            ],
            assessments);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.Equal(
            lowerPickupHigherDrop.CandidateId,
            Assert.Single(result.Selection!.VehiclePlans).Candidate.CandidateId);
    }

    [Fact]
    public void C1_acceptance_then_worst_utilization_dominate_revision_and_cost()
    {
        var request = AlgorithmTestData.PendingRequest();
        var vehicle = AlgorithmTestData.Vehicle();
        var noOp = Candidate("noop", vehicle.Id, [], 0);
        var highUtilization = Candidate("high-util", vehicle.Id, [request.Id], 0);
        var lowUtilization = Candidate("low-util", vehicle.Id, [request.Id], 100);
        var assessments = new Dictionary<string, HardVectorCandidateAssessment>(
            StringComparer.Ordinal)
        {
            [noOp.CandidateId] = HardAssessment(noOp, 0, CommitmentVector.Zero),
            [highUtilization.CandidateId] = HardAssessment(
                highUtilization,
                900_000,
                CommitmentVector.Zero),
            [lowUtilization.CandidateId] = HardAssessment(
                lowUtilization,
                100_000,
                Vector(pickupEta: 999)),
        };

        var result = new HardVectorFleetSelector().Select(
            [
                new VehicleCandidateSet(
                    vehicle.Id,
                    [noOp, highUtilization, lowUtilization],
                    [],
                    false),
            ],
            assessments);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.Equal(
            lowUtilization.CandidateId,
            Assert.Single(result.Selection!.VehiclePlans).Candidate.CandidateId);
        Assert.Equal(100_000, result.Selection.WorstHardUtilizationPartsPerMillion);
    }

    [Fact]
    public void C1_uses_ten_dimension_order_before_cost_when_hard_treatment_exists()
    {
        var request = AlgorithmTestData.PendingRequest();
        var vehicle = AlgorithmTestData.Vehicle();
        var lowerPickup = Candidate("lower-pickup", vehicle.Id, [request.Id], 100);
        var lowerDrop = Candidate("lower-drop", vehicle.Id, [request.Id], 0);
        var assessments = new Dictionary<string, HardVectorCandidateAssessment>(
            StringComparer.Ordinal)
        {
            [lowerPickup.CandidateId] = HardAssessment(
                lowerPickup,
                500_000,
                Vector(pickupEta: 1, dropEta: 999)),
            [lowerDrop.CandidateId] = HardAssessment(
                lowerDrop,
                500_000,
                Vector(pickupEta: 2, dropEta: 0)),
        };

        var result = new HardVectorFleetSelector().Select(
            [
                new VehicleCandidateSet(
                    vehicle.Id,
                    [lowerDrop, lowerPickup],
                    [],
                    false),
            ],
            assessments);

        Assert.Equal(
            lowerPickup.CandidateId,
            Assert.Single(result.Selection!.VehiclePlans).Candidate.CandidateId);
    }

    [Fact]
    public void C1_unbounded_treatment_degenerates_to_operational_cost_order()
    {
        var request = AlgorithmTestData.PendingRequest();
        var vehicle = AlgorithmTestData.Vehicle();
        var cheapRevision = Candidate("cheap", vehicle.Id, [request.Id], 1);
        var expensiveStable = Candidate("expensive", vehicle.Id, [request.Id], 100);
        var assessments = new Dictionary<string, HardVectorCandidateAssessment>(
            StringComparer.Ordinal)
        {
            [cheapRevision.CandidateId] = HardAssessment(
                cheapRevision,
                0,
                Vector(pickupEta: 999),
                hasHardLimit: false),
            [expensiveStable.CandidateId] = HardAssessment(
                expensiveStable,
                0,
                CommitmentVector.Zero,
                hasHardLimit: false),
        };

        var result = new HardVectorFleetSelector().Select(
            [
                new VehicleCandidateSet(
                    vehicle.Id,
                    [expensiveStable, cheapRevision],
                    [],
                    false),
            ],
            assessments);

        Assert.Equal(
            cheapRevision.CandidateId,
            Assert.Single(result.Selection!.VehiclePlans).Candidate.CandidateId);
    }

    [Fact]
    public void C2_warning_excess_precedes_raw_revision_and_cost()
    {
        var request = AlgorithmTestData.PendingRequest();
        var vehicle = AlgorithmTestData.Vehicle();
        var lowWarning = Candidate("low-warning", vehicle.Id, [request.Id], 100);
        var lowRevision = Candidate("low-revision", vehicle.Id, [request.Id], 0);
        var assessments = new Dictionary<string, HardVectorCandidateAssessment>(
            StringComparer.Ordinal)
        {
            [lowWarning.CandidateId] = new HardVectorCandidateAssessment(
                lowWarning.CandidateId,
                500_000,
                Vector(pickupEta: 999),
                true,
                Vector(pickupEta: 1),
                true),
            [lowRevision.CandidateId] = new HardVectorCandidateAssessment(
                lowRevision.CandidateId,
                500_000,
                CommitmentVector.Zero,
                true,
                Vector(pickupEta: 2),
                true),
        };

        var result = new SoftHardHybridFleetSelector().Select(
            [
                new VehicleCandidateSet(
                    vehicle.Id,
                    [lowRevision, lowWarning],
                    [],
                    false),
            ],
            assessments);

        Assert.Equal(
            lowWarning.CandidateId,
            Assert.Single(result.Selection!.VehiclePlans).Candidate.CandidateId);
        Assert.Equal(1, result.Selection.WarningExcess!.PickupEtaTotalMs);
    }

    [Fact]
    public void C2_without_enabled_warning_uses_the_exact_C1_selector_path()
    {
        var request = AlgorithmTestData.PendingRequest();
        var vehicle = AlgorithmTestData.Vehicle();
        var cheap = Candidate("cheap", vehicle.Id, [request.Id], 1);
        var stable = Candidate("stable", vehicle.Id, [request.Id], 100);
        var assessments = new Dictionary<string, HardVectorCandidateAssessment>(
            StringComparer.Ordinal)
        {
            [cheap.CandidateId] = HardAssessment(
                cheap,
                500_000,
                Vector(pickupEta: 2)),
            [stable.CandidateId] = HardAssessment(
                stable,
                500_000,
                Vector(pickupEta: 1)),
        };
        var sets = new[]
        {
            new VehicleCandidateSet(vehicle.Id, [cheap, stable], [], false),
        };

        var c1 = new HardVectorFleetSelector().Select(sets, assessments);
        var c2 = new SoftHardHybridFleetSelector().Select(sets, assessments);

        Assert.Equal(
            Assert.Single(c1.Selection!.VehiclePlans).Candidate.CandidateId,
            Assert.Single(c2.Selection!.VehiclePlans).Candidate.CandidateId);
        Assert.Null(c2.Selection.WarningExcess);
    }

    [Fact]
    public void Warning_profile_requires_all_ten_explicit_dimensions()
    {
        Assert.Throws<ArgumentException>(
            () => new CommitmentWarningProfile(
                "uniform-v1",
                [
                    new CommitmentWarningLimit(
                        CommitmentDimension.PickupEtaTotalMs,
                        1),
                ]));
    }

    [Fact]
    public void Fleet_selectors_fail_closed_above_canonical_cost_range()
    {
        var firstVehicle = AlgorithmTestData.Vehicle(AlgorithmTestData.VehicleOne);
        var secondVehicle = AlgorithmTestData.Vehicle(AlgorithmTestData.VehicleTwo);
        var first = Candidate(
            "v1-noop",
            firstVehicle.Id,
            [],
            DomainLimits.MaxCanonicalInteger);
        var second = Candidate("v2-noop", secondVehicle.Id, [], 1);
        var sets = new[]
        {
            new VehicleCandidateSet(firstVehicle.Id, [first], [], false),
            new VehicleCandidateSet(secondVehicle.Id, [second], [], false),
        };
        var assessments = new Dictionary<string, CandidateCommitmentAssessment>(
            StringComparer.Ordinal)
        {
            [first.CandidateId] = Assessment(first, CommitmentVector.Zero),
            [second.CandidateId] = Assessment(second, CommitmentVector.Zero),
        };

        var b1 = new CandidateFleetSelector().Select(sets);
        var b2 = new RevisionPenaltyFleetSelector().Select(sets, assessments);
        var hardAssessments = new Dictionary<
            string,
            HardVectorCandidateAssessment>(StringComparer.Ordinal)
        {
            [first.CandidateId] = HardAssessment(
                first,
                0,
                CommitmentVector.Zero),
            [second.CandidateId] = HardAssessment(
                second,
                0,
                CommitmentVector.Zero),
        };
        var c1 = new HardVectorFleetSelector().Select(sets, hardAssessments);

        Assert.Equal(
            RollingCostFailureCodes.OperationalCostOverflow,
            b1.Witness?.Code);
        Assert.Equal(
            RollingCostFailureCodes.OperationalCostOverflow,
            b2.Witness?.Code);
        Assert.Equal(
            RollingCostFailureCodes.OperationalCostOverflow,
            c1.Witness?.Code);
    }

    private static InsertionCandidate Candidate(
        string id,
        VehicleId vehicleId,
        IReadOnlyList<RequestId> requestIds,
        long cost) =>
        new(
            id,
            vehicleId,
            AlgorithmTestData.Vehicle(id: vehicleId).Route,
            requestIds,
            new CandidateSchedule([], cost),
            requestIds.Count == 0);

    private static CandidateCommitmentAssessment Assessment(
        InsertionCandidate candidate,
        CommitmentVector vector) =>
        new(candidate.CandidateId, vector);

    private static HardVectorCandidateAssessment HardAssessment(
        InsertionCandidate candidate,
        long utilization,
        CommitmentVector vector,
        bool hasHardLimit = true) =>
        new(candidate.CandidateId, utilization, vector, hasHardLimit);

    private static CommitmentVector Vector(
        long pickupEta = 0,
        long dropEta = 0,
        long material = 0) =>
        new(pickupEta, dropEta, material, 0, 0, 0, 0, 0, 0, 0);

    private static CommitmentPolicy SourcePolicy(long? hardLimit) =>
        new(
            "uniform-v1",
            CommitmentBudgetBasis.DecisionInduced,
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    hardLimit,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1, null),
            new Duration(10_000),
            PromiseLock.PickupEta,
            PromiseLock.DropEta);
}
