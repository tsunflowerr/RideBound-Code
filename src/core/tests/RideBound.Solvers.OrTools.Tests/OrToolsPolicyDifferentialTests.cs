using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Commitments;
using RideBound.Algorithms.Policies;
using RideBound.Application.Optimization;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Routes;
using RideBound.Solvers.OrTools;

namespace RideBound.Solvers.OrTools.Tests;

public sealed class OrToolsPolicyDifferentialTests
{
    [Fact]
    public void C1_mapper_and_ortools_match_independent_oracle_for_64_seeds()
    {
        const int seedCount = 64;

        for (var seed = 0; seed < seedCount; seed++)
        {
            var fixture = CreateFixture(seed);
            var solverBudget = DeterministicSolverBudget.Create(
                1_000_000,
                10_000_000,
                seed).Value!;
            var executionBudget =
                DeterministicCandidateSelectionExecutionBudget.Create(
                    1_000_000,
                    1_000_000,
                    solverBudget).Value!;
            var accounting = CandidateSelectionPreSolveAccounting.Create(
                executionBudget,
                0,
                0,
                0).Value!;

            var production = new SolverBackedFleetSelector(
                new OrToolsCandidateSelectionSolver()).Select(
                    fixture.Sets,
                    SolverBackedObjectiveProfile.HardVector,
                    executionBudget,
                    accounting,
                    new AllowAllValidator(),
                    hardAssessments: fixture.Assessments);
            var oracle = SelectIndependent(fixture.Sets, fixture.Assessments);

            Assert.True(
                production.IsSuccess,
                $"seed={seed}; {production.Witness?.Code}; {production.Witness?.Message}");
            Assert.Equal(
                oracle.Select(value => value.CandidateId),
                production.Selection!.Selection.VehiclePlans
                    .Select(value => value.Candidate.CandidateId));
            Assert.Equal(
                CandidateSelectionSolveStatus.Optimal,
                production.Selection.Execution.SolveResult.Status);
            Assert.All(
                production.Selection.Execution.SolveResult.Diagnostics.ObjectiveBounds,
                bound =>
                {
                    Assert.True(bound.IsProvenOptimal);
                    Assert.Equal(0, bound.GapNumerator);
                });
        }
    }

    /// <summary>
    /// RB-WP14-002 decision invariance on the production C1 mapping. The same 64
    /// fixtures are selected with and without the constant-level skip; the chosen
    /// candidates and every reported optimum must be identical.
    /// </summary>
    [Fact]
    public void Constant_level_skip_does_not_change_the_c1_selection_for_64_seeds()
    {
        const int seedCount = 64;
        var skippedAtLeastOnce = false;

        for (var seed = 0; seed < seedCount; seed++)
        {
            var fixture = CreateFixture(seed);
            var baseline = SelectHardVector(fixture, seed, skip: false);
            var skipped = SelectHardVector(fixture, seed, skip: true);

            Assert.True(baseline.IsSuccess, $"seed={seed}; {baseline.Witness?.Code}");
            Assert.True(skipped.IsSuccess, $"seed={seed}; {skipped.Witness?.Code}");
            Assert.Equal(
                baseline.Selection!.Selection.VehiclePlans
                    .Select(value => value.Candidate.CandidateId),
                skipped.Selection!.Selection.VehiclePlans
                    .Select(value => value.Candidate.CandidateId));

            var baselineSolution = baseline.Selection.Execution.SolveResult.Solution!;
            var skippedSolution = skipped.Selection.Execution.SolveResult.Solution!;
            Assert.Equal(
                baselineSolution.SelectedOptionIds,
                skippedSolution.SelectedOptionIds);
            Assert.Equal(
                baselineSolution.ObjectiveValues,
                skippedSolution.ObjectiveValues);

            var baselineDiagnostics =
                baseline.Selection.Execution.SolveResult.Diagnostics;
            var skippedDiagnostics =
                skipped.Selection.Execution.SolveResult.Diagnostics;
            Assert.Equal(
                baselineDiagnostics.ObjectiveBounds.Select(
                    bound => (bound.LevelIndex, bound.IncumbentValue, bound.BestBound)),
                skippedDiagnostics.ObjectiveBounds.Select(
                    bound => (bound.LevelIndex, bound.IncumbentValue, bound.BestBound)));
            Assert.True(
                skippedDiagnostics.ConsumedDeterministicTimeMicros
                    <= baselineDiagnostics.ConsumedDeterministicTimeMicros,
                $"seed={seed} recorded more solver time after skipping");

            skippedAtLeastOnce |= StringComparer.Ordinal.Equals(
                skippedDiagnostics.DetailCode,
                "ORTOOLS_OPTIMAL_CONSTANT_LEVELS_SKIPPED");
        }

        Assert.True(
            skippedAtLeastOnce,
            "no fixture exercised the skip, so the differential proves nothing");
    }

    private static SolverBackedFleetSelectionResult SelectHardVector(
        Fixture fixture,
        int seed,
        bool skip)
    {
        var solverBudget = DeterministicSolverBudget.Create(
            1_000_000,
            10_000_000,
            seed,
            skipConstantObjectiveLevels: skip).Value!;
        var executionBudget =
            DeterministicCandidateSelectionExecutionBudget.Create(
                1_000_000,
                1_000_000,
                solverBudget).Value!;
        var accounting = CandidateSelectionPreSolveAccounting.Create(
            executionBudget,
            0,
            0,
            0).Value!;
        return new SolverBackedFleetSelector(
            new OrToolsCandidateSelectionSolver()).Select(
                fixture.Sets,
                SolverBackedObjectiveProfile.HardVector,
                executionBudget,
                accounting,
                new AllowAllValidator(),
                hardAssessments: fixture.Assessments);
    }

    private static Fixture CreateFixture(int seed)
    {
        var random = new Random(seed);
        var vehicleCount = 1 + seed % 2;
        var requestCount = 1 + seed / 2 % 2;
        var requests = Enumerable.Range(0, requestCount)
            .Select(index => new RequestId($"request-{index}"))
            .ToArray();
        var sets = new List<VehicleCandidateSet>();
        var assessments = new Dictionary<string, HardVectorCandidateAssessment>(
            StringComparer.Ordinal);

        for (var vehicleIndex = 0; vehicleIndex < vehicleCount; vehicleIndex++)
        {
            var vehicleId = new VehicleId($"vehicle-{vehicleIndex}");
            var candidates = new List<InsertionCandidate>
            {
                Candidate($"v{vehicleIndex}-noop", vehicleId, [], random.Next(0, 5)),
            };

            for (var requestIndex = 0; requestIndex < requestCount; requestIndex++)
            {
                candidates.Add(
                    Candidate(
                        $"v{vehicleIndex}-r{requestIndex}",
                        vehicleId,
                        [requests[requestIndex]],
                        random.Next(0, 101)));
            }

            if (requestCount == 2)
            {
                candidates.Add(
                    Candidate(
                        $"v{vehicleIndex}-both",
                        vehicleId,
                        requests,
                        random.Next(0, 151)));
            }

            foreach (var candidate in candidates)
            {
                assessments.Add(
                    candidate.CandidateId,
                    new HardVectorCandidateAssessment(
                        candidate.CandidateId,
                        random.Next(0, 1_000_001),
                        Vector(random),
                        HasApplicableHardLimit: true));
            }

            sets.Add(new VehicleCandidateSet(vehicleId, candidates, [], false));
        }

        return new Fixture(sets.AsReadOnly(), assessments);
    }

    private static IReadOnlyList<InsertionCandidate> SelectIndependent(
        IReadOnlyList<VehicleCandidateSet> sets,
        IReadOnlyDictionary<string, HardVectorCandidateAssessment> assessments)
    {
        IReadOnlyList<InsertionCandidate>? best = null;
        Enumerate(0, [], new HashSet<RequestId>());
        return best!;

        void Enumerate(
            int index,
            IReadOnlyList<InsertionCandidate> selected,
            IReadOnlySet<RequestId> assigned)
        {
            if (index == sets.Count)
            {
                if (best is null || Compare(selected, best, assessments) < 0)
                {
                    best = selected.ToArray();
                }

                return;
            }

            foreach (var candidate in sets[index].Candidates)
            {
                if (candidate.NewRequestIds.Any(assigned.Contains))
                {
                    continue;
                }

                Enumerate(
                    index + 1,
                    selected.Append(candidate).ToArray(),
                    assigned.Concat(candidate.NewRequestIds).ToHashSet());
            }
        }
    }

    private static int Compare(
        IReadOnlyList<InsertionCandidate> left,
        IReadOnlyList<InsertionCandidate> right,
        IReadOnlyDictionary<string, HardVectorCandidateAssessment> assessments)
    {
        var leftAccepted = left.Sum(value => value.NewRequestIds.Count);
        var rightAccepted = right.Sum(value => value.NewRequestIds.Count);

        if (leftAccepted != rightAccepted)
        {
            return rightAccepted.CompareTo(leftAccepted);
        }

        var leftWorst = left.Max(
            value => assessments[value.CandidateId]
                .WorstHardUtilizationPartsPerMillion);
        var rightWorst = right.Max(
            value => assessments[value.CandidateId]
                .WorstHardUtilizationPartsPerMillion);

        if (leftWorst != rightWorst)
        {
            return leftWorst.CompareTo(rightWorst);
        }

        foreach (var dimension in CommitmentDimensionVocabulary.Ordered)
        {
            var leftRevision = left.Sum(
                value => assessments[value.CandidateId]
                    .DecisionInducedRevision.Get(dimension));
            var rightRevision = right.Sum(
                value => assessments[value.CandidateId]
                    .DecisionInducedRevision.Get(dimension));

            if (leftRevision != rightRevision)
            {
                return leftRevision.CompareTo(rightRevision);
            }
        }

        var leftCost = left.Sum(value => value.Schedule.OperationalCost);
        var rightCost = right.Sum(value => value.Schedule.OperationalCost);

        if (leftCost != rightCost)
        {
            return leftCost.CompareTo(rightCost);
        }

        for (var index = 0; index < left.Count; index++)
        {
            var id = StringComparer.Ordinal.Compare(
                left[index].CandidateId,
                right[index].CandidateId);

            if (id != 0)
            {
                return id;
            }
        }

        return 0;
    }

    private static InsertionCandidate Candidate(
        string id,
        VehicleId vehicleId,
        IReadOnlyList<RequestId> requests,
        long cost) =>
        new(
            id,
            vehicleId,
            RoutePlan.Create(new PlanVersion(0), 0, [], []).Value!,
            requests,
            new CandidateSchedule([], cost),
            requests.Count == 0);

    private static CommitmentVector Vector(Random random) =>
        new(
            random.Next(0, 21),
            random.Next(0, 21),
            random.Next(0, 4),
            random.Next(0, 2),
            random.Next(0, 21),
            random.Next(0, 4),
            random.Next(0, 21),
            random.Next(0, 4),
            random.Next(0, 4),
            random.Next(0, 4));

    private sealed class AllowAllValidator : IFleetSelectionValidator
    {
        public CandidateSelectionValidationResult Validate(
            FleetSelection selection) =>
            CandidateSelectionValidationResult.Valid();
    }

    private sealed record Fixture(
        IReadOnlyList<VehicleCandidateSet> Sets,
        IReadOnlyDictionary<string, HardVectorCandidateAssessment> Assessments);
}
