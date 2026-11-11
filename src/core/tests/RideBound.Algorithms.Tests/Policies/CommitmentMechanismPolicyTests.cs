using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Commitments;
using RideBound.Algorithms.Policies;
using RideBound.Application.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Validation;

namespace RideBound.Algorithms.Tests.Policies;

public sealed class CommitmentMechanismPolicyTests
{
    [Fact]
    public void C1_ranks_by_operational_cost_after_acceptance()
    {
        var request = AlgorithmTestData.PendingRequest();
        var vehicle = AlgorithmTestData.Vehicle();
        var noOp = Candidate("noop", vehicle.Id, [], 0);
        var cheap = Candidate("cheap", vehicle.Id, [request.Id], 1);
        var expensive = Candidate("expensive", vehicle.Id, [request.Id], 100);
        var assessments = new Dictionary<string, HardVectorCandidateAssessment>(
            StringComparer.Ordinal)
        {
            [noOp.CandidateId] = HardAssessment(noOp),
            [cheap.CandidateId] = HardAssessment(cheap),
            [expensive.CandidateId] = HardAssessment(expensive),
        };

        var result = new HardVectorFleetSelector().Select(
            [
                new VehicleCandidateSet(
                    vehicle.Id,
                    [expensive, noOp, cheap],
                    [],
                    false),
            ],
            assessments);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.Equal(
            cheap.CandidateId,
            Assert.Single(result.Selection!.VehiclePlans).Candidate.CandidateId);
        Assert.Equal(1, result.Selection.AcceptedRequestCount);
        Assert.Equal(1, result.Selection.OperationalCost);
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

        var b1 = new CandidateFleetSelector().Select(sets);
        var hardAssessments = new Dictionary<
            string,
            HardVectorCandidateAssessment>(StringComparer.Ordinal)
        {
            [first.CandidateId] = HardAssessment(first),
            [second.CandidateId] = HardAssessment(second),
        };
        var c1 = new HardVectorFleetSelector().Select(sets, hardAssessments);

        Assert.Equal(
            DecisionFailureCodes.OperationalCostOverflow,
            b1.Witness?.Code);
        Assert.Equal(
            DecisionFailureCodes.OperationalCostOverflow,
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

    private static HardVectorCandidateAssessment HardAssessment(InsertionCandidate candidate) =>
        new(candidate.CandidateId);
}
