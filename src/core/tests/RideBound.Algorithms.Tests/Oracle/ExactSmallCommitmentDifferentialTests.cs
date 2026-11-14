using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Commitments;
using RideBound.Algorithms.Policies;
using RideBound.Application.Commitments;
using RideBound.Application.Optimization;
using RideBound.Application.State;
using RideBound.Application.Travel;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Routes;
using RideBound.Domain.Validation;

namespace RideBound.Algorithms.Tests.Oracle;

public sealed class ExactSmallCommitmentDifferentialTests
{
    [Fact]
    public void Production_commitment_filter_matches_independent_small_oracle()
    {
        const int publishedSeedCount = 16;

        for (var seed = 0; seed < publishedSeedCount; seed++)
        {
            var fixture = CreateState(seed);
            var generated = new InsertionCandidateGenerator().Generate(
                fixture.State,
                AlgorithmTestData.SmallGeneration);
            Assert.True(
                generated.IsSuccess,
                $"seed={seed}; {generated.Witness?.Message}");
            Assert.True(generated.Diagnostics!.IsComplete, $"seed={seed}");
            var oracleCandidates = ExactSmallOracle.Generate(fixture.State)
                [AlgorithmTestData.VehicleOne];
            var expected = oracleCandidates
                .Where(
                    value => OracleAllows(
                        fixture.State,
                        value.SemanticKey,
                        fixture.IncumbentId,
                        fixture.BaselinePickup,
                        fixture.BaselineDrop,
                        fixture.HardEtaLimit))
                .Select(value => value.SemanticKey)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var filter = new CommitmentCandidateFilter(
                fixture.BeforeEventState,
                new CommitmentPolicyCatalog([fixture.Policy]),
                NoDistances.Instance,
                $"commitment-oracle-{seed}",
                1);
            var generatedByVehicle = Assert.IsAssignableFrom<
                IReadOnlyList<VehicleCandidateSet>>(
                generated.VehicleCandidates);
            var retained = Assert.Single(
                filter.Filter(
                    fixture.State,
                    generatedByVehicle));
            var actual = retained.Candidates
                .Select(SemanticKey)
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void Loosening_eta_budget_never_removes_an_exact_small_candidate()
    {
        const int publishedSeedCount = 16;

        for (var seed = 0; seed < publishedSeedCount; seed++)
        {
            var fixture = CreateState(seed);
            var generated = new InsertionCandidateGenerator().Generate(
                fixture.State,
                AlgorithmTestData.SmallGeneration);
            Assert.True(generated.IsSuccess, generated.Witness?.Message);
            Assert.True(generated.Diagnostics!.IsComplete);
            var candidateSets = generated.VehicleCandidates!;
            var tight = RetainedKeys(
                fixture,
                candidateSets,
                EtaPolicy(40));
            var loose = RetainedKeys(
                fixture,
                candidateSets,
                EtaPolicy(160));

            Assert.All(
                tight,
                key => Assert.Contains(key, loose));
        }
    }

    [Fact]
    public void C1_assessor_matches_independent_filter_and_kills_hard_gate_removal_mutation()
    {
        var fixture = CreateState(seed: 3);
        var generated = new InsertionCandidateGenerator().Generate(
            fixture.State,
            AlgorithmTestData.SmallGeneration);
        Assert.True(generated.IsSuccess, generated.Witness?.Message);
        Assert.True(generated.Diagnostics!.IsComplete);
        var raw = generated.VehicleCandidates!;
        var context = MechanismContext(fixture, "c1-assessment");

        var assessed = new HardVectorCandidateAssessor().AssessAndFilter(
            context,
            raw);
        var reference = new CommitmentCandidateFilter(
            fixture.BeforeEventState,
            context.Policies,
            context.StopDistances,
            "c1-reference",
            1).Filter(fixture.State, raw);

        Assert.True(assessed.IsSuccess, assessed.Witness?.Message);
        Assert.True(
            raw.Single().Candidates.Count
                > assessed.Batch!.FeasibleCandidateSets.Single().Candidates.Count,
            "The published fixture must contain a hard-invalid candidate so removing the hard gate is killed.");
        Assert.Equal(
            reference.Single().Candidates.Select(value => value.CandidateId),
            assessed.Batch.FeasibleCandidateSets.Single().Candidates
                .Select(value => value.CandidateId));

        AlgorithmTestData.AssertGeneratedInFull(context.ReducedState, AlgorithmTestData.SmallGeneration);
        var decision = new RideBoundHardVectorPolicy().Decide(
            context,
            AlgorithmTestData.SmallGeneration);
        Assert.True(decision.IsSuccess, decision.Witness?.Message);
    }

    [Fact]
    public void Cost_first_C1_ranks_the_same_gated_candidates_by_cost_and_picks_a_cheaper_plan()
    {
        // ADR-078, end to end below the Runner: the solver sees exactly the candidates the C1
        // assessment keeps, ranked with the C1 levels (cost-first), and the decision is the plan an
        // independent pick over those candidates gives: most new requests, then lowest cost, then
        // lowest candidate id. The Runner's validation accepts it.
        // In at least one published seed the cost level decides between plans that accept the same
        // number of requests.
        var costDecides = 0;

        for (var seed = 0; seed < 64; seed++)
        {
            var fixture = CreateState(seed);
            var context = MechanismContext(fixture, $"c1-order-{seed}");
            var generated = new InsertionCandidateGenerator().Generate(
                fixture.State,
                AlgorithmTestData.SmallGeneration);
            Assert.True(generated.IsSuccess, $"seed={seed}; {generated.Witness?.Message}");
            Assert.True(generated.Diagnostics!.IsComplete, $"seed={seed}");
            var gated = new HardVectorCandidateAssessor().AssessAndFilter(
                context,
                generated.VehicleCandidates!,
                requireSafetyNoOp: true);
            Assert.True(gated.IsSuccess, $"seed={seed}; {gated.Witness?.Message}");
            var options = gated.Batch!.FeasibleCandidateSets.Single().Candidates;
            var solver = new EnumeratingSolver();

            AlgorithmTestData.AssertGeneratedInFull(context.ReducedState, AlgorithmTestData.SmallGeneration);
            var result = new SolverBackedRidePoolingPolicy(solver).Decide(
                context,
                AlgorithmTestData.SmallGeneration,
                HardVectorOptions());

            Assert.True(result.IsSuccess, $"seed={seed}; {result.Witness?.Message}");
            var decision = result.Decision!.Decision;
            Assert.Equal(
                options.Select(value => value.CandidateId).Order(StringComparer.Ordinal),
                solver.LastProblem!.Options.Select(value => value.OptionId).Order(StringComparer.Ordinal));
            Assert.Equal(
                ["accepted-request-count", "operational-cost", "candidate-id-rank:vehicle-1"],
                solver.LastProblem.ObjectiveLevels.Select(value => value.Name));
            var best = options
                .OrderByDescending(value => value.NewRequestIds.Count)
                .ThenBy(value => value.Schedule.OperationalCost)
                .ThenBy(value => value.CandidateId, StringComparer.Ordinal)
                .First();
            Assert.Equal(best.CandidateId, decision.VehiclePlans.Single().Candidate.CandidateId);
            Assert.Equal(best.Schedule.OperationalCost, decision.OperationalCost);

            // The chosen plan passes the validation the Runner applies.
            var validated = new CommitmentDecisionValidator().Validate(
                RunnerLikeContext(context, decision));
            Assert.True(
                validated.IsValid,
                $"seed={seed}; {validated.Witnesses.FirstOrDefault()?.Message}");

            if (options.Any(
                    value => value.NewRequestIds.Count == best.NewRequestIds.Count
                        && value.Schedule.OperationalCost > best.Schedule.OperationalCost))
            {
                costDecides++;
            }
        }

        Assert.True(
            costDecides > 0,
            "At least one published exact-small seed must have a dearer plan with the same request count.");
    }

    [Fact]
    public void C1_with_unbounded_limits_and_no_locks_is_semantically_equal_to_B1()
    {
        var fixture = CreateState(seed: 2);
        var policy = new CommitmentPolicy(
            "uniform-v1",
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    null,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1_000, null));
        var context = new CommitmentMechanismContext(
            fixture.BeforeEventState,
            fixture.State,
            new CommitmentPolicyCatalog([policy]),
            NoDistances.Instance,
            "c1-unbounded-equivalence",
            1);

        AlgorithmTestData.AssertGeneratedInFull(fixture.State, AlgorithmTestData.SmallGeneration);
        var b1 = new SelectionApplier().Decide(
            fixture.State,
            AlgorithmTestData.SmallGeneration);
        AlgorithmTestData.AssertGeneratedInFull(context.ReducedState, AlgorithmTestData.SmallGeneration);
        var c1 = new RideBoundHardVectorPolicy().Decide(
            context,
            AlgorithmTestData.SmallGeneration);

        Assert.True(b1.IsSuccess, b1.Witness?.Message);
        Assert.True(c1.IsSuccess, c1.Witness?.Message);
        Assert.Equal(b1.Decision!.RequestActions, c1.Decision!.RequestActions);
        Assert.Equal(
            b1.Decision.VehiclePlans.Select(value => value.Candidate.CandidateId),
            c1.Decision.VehiclePlans.Select(value => value.Candidate.CandidateId));
        Assert.All(
            b1.Decision.VehiclePlans.Zip(c1.Decision.VehiclePlans),
            pair => Assert.True(
                pair.First.Candidate.Route.IsSemanticallyEqual(
                    pair.Second.Candidate.Route)));
    }

    [Fact]
    public void An_exhausted_budget_prunes_disturbing_candidates_but_retains_zero_delta_options()
    {
        // The gate-level safety property that makes a finite commitment budget
        // usable: the no-op
        // carries a zero decision delta for everyone, so `after == before` and
        // it can never be pruned by the budget gate. Without this invariant a
        // tight budget could remove the safety option and take the assessor down
        // through C1_VEHICLE_HAS_NO_FEASIBLE_CANDIDATE.
        var fixture = CreateState(seed: 3);
        var generated = new InsertionCandidateGenerator().Generate(
            fixture.State,
            AlgorithmTestData.SmallGeneration);
        Assert.True(generated.IsSuccess, generated.Witness?.Message);
        Assert.True(generated.Diagnostics!.IsComplete);
        Assert.True(
            generated.VehicleCandidates!.Single().Candidates.Count > 1,
            "The fixture must offer more than the no-op for this to prove anything.");

        // One millisecond of cumulative drop-ETA drift: anything that touches a
        // live promise is out.
        var exhausted = new CommitmentMechanismContext(
            fixture.BeforeEventState,
            fixture.State,
            new CommitmentPolicyCatalog([EtaPolicy(1)]),
            NoDistances.Instance,
            "c1-exhausted-budget",
            1);

        var assessed = new HardVectorCandidateAssessor().AssessAndFilter(
            exhausted,
            generated.VehicleCandidates!);

        Assert.True(assessed.IsSuccess, assessed.Witness?.Message);
        var retained = assessed.Batch!.FeasibleCandidateSets.Single().Candidates;

        // The budget bites: some plan that disturbed a live promise was removed.
        Assert.True(
            retained.Count < generated.VehicleCandidates!.Single().Candidates.Count,
            "A one-millisecond budget must prune at least one disturbing plan.");

        // But the vehicle always keeps a legal option. This test stops at the
        // assessor boundary; it deliberately makes no claim about the later
        // fleet-selection action (accept, defer or reject).
        Assert.Contains(retained, candidate => candidate.IsNoOp);
        var commitmentPrunes = assessed.Batch.FeasibleCandidateSets.Single()
            .PrunedCandidates
            .Where(value => value.CommitmentWitnesses is { Count: > 0 })
            .ToArray();
        Assert.NotEmpty(commitmentPrunes);
        Assert.All(
            commitmentPrunes,
            value => Assert.All(
                value.CommitmentWitnesses!,
                witness => Assert.False(string.IsNullOrWhiteSpace(witness.Code))));

        // Serving a newcomer is not itself a budget cost. A plan that seats one
        // without delaying anybody's promised drop-off stays legal at any
        // budget. Whether such an option is selected is tested at the policy
        // boundary elsewhere.
        Assert.All(
            retained,
            candidate => Assert.Equal(0, DecisionInducedDropEtaMs(exhausted, candidate)));
    }

    /// <summary>
    /// The decision-induced drop-ETA change a candidate publishes, summed over its riders, from
    /// the validation the C1 assessment runs for it.
    /// </summary>
    private static long DecisionInducedDropEtaMs(
        CommitmentMechanismContext context,
        InsertionCandidate candidate)
    {
        var updated = CandidateStateApplicator.Apply(context.ReducedState.Run, candidate);
        Assert.True(updated.IsSuccess, updated.Failure?.Message);
        var validation = new CommitmentDecisionValidator().Validate(
            new CommitmentValidationContext(
                context.BeforeEventState,
                context.ReducedState,
                context.ReducedState with { Run = updated.Value! },
                context.Policies,
                context.StopDistances,
                context.PublicationScope,
                context.SourceEventSequence,
                RevisionReasonCode: "C1_HARD_VECTOR",
                ScopedVehicleId: candidate.VehicleId));
        Assert.True(validation.IsValid, validation.Witnesses.FirstOrDefault()?.Message);
        return validation.Publications.Sum(value => value.Entry.Deltas.DecisionInduced.DropEtaTotalMs);
    }

    [Fact]
    public void C1_fails_closed_with_a_typed_witness_when_a_vehicle_loses_every_candidate()
    {
        // A configuration whose policy catalog does not declare the policy the
        // requests were booked under is exactly the misconfiguration that must
        // never degrade into a silently empty candidate set: the solver would
        // then face a vehicle with no selectable option. This asserts the
        // fail-closed contract and that the diagnosis is machine-readable.
        var fixture = CreateState(seed: 3);
        var generated = new InsertionCandidateGenerator().Generate(
            fixture.State,
            AlgorithmTestData.SmallGeneration);
        Assert.True(generated.IsSuccess, generated.Witness?.Message);
        Assert.True(generated.Diagnostics!.IsComplete);
        var undeclared = new CommitmentPolicy(
            "undeclared-policy-v1",
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    null,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1_000, null));
        var context = new CommitmentMechanismContext(
            fixture.BeforeEventState,
            fixture.State,
            new CommitmentPolicyCatalog([undeclared]),
            NoDistances.Instance,
            "c1-hard-empty",
            1);

        var assessed = new HardVectorCandidateAssessor().AssessAndFilter(
            context,
            generated.VehicleCandidates!);

        Assert.False(assessed.IsSuccess);
        var witness = assessed.Witness!;
        Assert.Equal(
            CommitmentFailureCodes.VehicleHasNoFeasibleCandidate,
            witness.Code);
        Assert.Equal(AlgorithmTestData.VehicleOne, witness.VehicleId);

        // Every diagnostic field must be typed rather than parsed out of prose.
        Assert.NotNull(witness.CandidateId);
        Assert.NotNull(witness.UnderlyingCode);
        Assert.Equal(
            generated.VehicleCandidates!.Single().Candidates.Count,
            witness.GeneratedCandidateCount);
        Assert.Equal(
            witness.GeneratedCandidateCount,
            witness.RejectedCandidateCount);
        Assert.DoesNotContain("firstCode=", witness.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void C1_fails_closed_when_a_vehicle_set_carries_no_candidate_at_all()
    {
        var fixture = CreateState(seed: 3);
        var context = MechanismContext(fixture, "c1-empty-set");
        var empty = new[]
        {
            new VehicleCandidateSet(
                AlgorithmTestData.VehicleOne,
                [],
                [],
                false,
                new VehicleCandidateLoss(
                    0, 0, 0, 0, 0, 0, 0, false, false, false)),
        };

        var assessed = new HardVectorCandidateAssessor().AssessAndFilter(
            context,
            empty);

        Assert.False(assessed.IsSuccess);
        Assert.Equal(
            CommitmentFailureCodes.VehicleHasNoFeasibleCandidate,
            assessed.Witness!.Code);
        Assert.Equal(AlgorithmTestData.VehicleOne, assessed.Witness.VehicleId);
        Assert.Null(assessed.Witness.CandidateId);
        Assert.Equal(0, assessed.Witness.GeneratedCandidateCount);
    }

    [Fact]
    public void C1_fail_closed_cites_the_safety_no_op_when_every_candidate_is_rejected()
    {
        // The incumbent was booked under a policy the catalog does not declare, so the
        // validator rejects every candidate, the no-op included. The witness must cite
        // the no-op itself.
        var (generated, set, noOp, ordinalFirst, context) = EveryCandidateRejected();

        var assessed = new HardVectorCandidateAssessor().AssessAndFilter(
            context,
            generated.VehicleCandidates!,
            requireSafetyNoOp: true);

        Assert.False(assessed.IsSuccess);
        var witness = assessed.Witness!;
        Assert.Equal(CommitmentFailureCodes.VehicleHasNoFeasibleCandidate, witness.Code);
        Assert.Equal(noOp.CandidateId, witness.CandidateId);
        Assert.NotEqual(ordinalFirst.CandidateId, witness.CandidateId);
        Assert.Equal("COMMITMENT_POLICY_NOT_FOUND", witness.UnderlyingCode);
        Assert.Equal(set.Candidates.Count, witness.RejectedCandidateCount);
        Assert.Contains(". No-op rejection: ", witness.Message, StringComparison.Ordinal);
        Assert.EndsWith(
            "The active request has no exact commitment policy.",
            witness.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_caller_that_does_not_require_the_no_op_still_cites_the_ordinal_first_rejection()
    {
        var (generated, _, _, ordinalFirst, context) = EveryCandidateRejected();

        var assessed = new HardVectorCandidateAssessor().AssessAndFilter(
            context,
            generated.VehicleCandidates!);

        Assert.False(assessed.IsSuccess);
        var witness = assessed.Witness!;
        Assert.Equal(CommitmentFailureCodes.VehicleHasNoFeasibleCandidate, witness.Code);
        Assert.Equal(ordinalFirst.CandidateId, witness.CandidateId);
        Assert.Contains(". First rejection: ", witness.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_relaxed_deadline_keeps_exactly_the_candidates_no_later_than_the_deadline_or_the_kept_route()
    {
        // DL-HZ. The kept route drops the incumbent at e and the independent oracle gives every
        // candidate's incumbent drop p. For each slack s and first promise p0, the relaxed deadline
        // keeps exactly the candidates with p <= max(p0 + s, e).
        var fixture = CreateState(seed: 3);
        var generated = new InsertionCandidateGenerator().Generate(
            fixture.State,
            AlgorithmTestData.SmallGeneration);
        Assert.True(generated.IsSuccess, generated.Witness?.Message);
        Assert.True(generated.Diagnostics!.IsComplete);
        var set = generated.VehicleCandidates!.Single();
        var kept = fixture.BaselineDrop.Milliseconds;
        var drops = set.Candidates.ToDictionary(
            value => value.CandidateId,
            value => OracleEtas(fixture.State, SemanticKey(value), fixture.IncumbentId)
                .Drop.Milliseconds,
            StringComparer.Ordinal);
        var delays = drops.Values
            .Select(value => value - kept)
            .Where(value => value > 0)
            .Distinct()
            .Order()
            .ToArray();
        Assert.True(delays.Length >= 2, "The fixture needs two distinct incumbent delays.");
        Assert.Contains(drops.Values, value => value == kept);
        var driftCases = 0;
        var insideCases = 0;

        foreach (var slack in new[] { 0, delays[0], delays[1] })
        {
            // How far drift carried the kept route past the deadline p0 + s (negative: inside).
            foreach (var overrun in new[]
                     {
                         delays[1] + 1, delays[0] + 1, delays[0], 1, 0, -1, -delays[0], -delays[1],
                     })
            {
                var initial = kept - slack - overrun;

                // A first promise cannot drop the incumbent before it was picked up.
                if (initial < fixture.BaselinePickup.Milliseconds)
                {
                    continue;
                }

                var relaxed = new HardVectorCandidateAssessor().AssessAndFilter(
                    SlackDeadlineContext(fixture, initial, slack),
                    generated.VehicleCandidates!,
                    requireSafetyNoOp: true);

                Assert.True(relaxed.IsSuccess, relaxed.Witness?.Message);
                var expected = drops
                    .Where(value => value.Value <= Math.Max(initial + slack, kept))
                    .Select(value => value.Key)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                Assert.Equal(expected, RetainedIds(relaxed));

                if (overrun > 0)
                {
                    driftCases++;
                }
                else
                {
                    insideCases++;
                }
            }
        }

        Assert.True(driftCases >= 6, $"Only {driftCases} cases had the kept route past the deadline.");
        Assert.True(insideCases >= 6, $"Only {insideCases} cases had the kept route inside the deadline.");
    }

    [Fact]
    public void A_relaxed_deadline_decision_serves_the_new_rider_as_normal_operation()
    {
        // Drift carried the kept route 1 ms past a zero-slack deadline. The relaxed rule bounds the
        // incumbent by the kept route, so the policy still serves the new rider, and the decision
        // validates as the Runner validates it, with no breach.
        var fixture = CreateState(seed: 3);
        var context = SlackDeadlineContext(fixture, fixture.BaselineDrop.Milliseconds - 1, 0);

        AlgorithmTestData.AssertGeneratedInFull(context.ReducedState, AlgorithmTestData.SmallGeneration);
        var result = new SolverBackedRidePoolingPolicy(new EnumeratingSolver()).Decide(
            context,
            AlgorithmTestData.SmallGeneration,
            HardVectorOptions());

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var decision = result.Decision!.Decision;
        var chosen = decision.VehiclePlans.Single().Candidate;
        Assert.False(chosen.IsNoOp);
        Assert.True(
            OracleEtas(fixture.State, SemanticKey(chosen), fixture.IncumbentId).Drop.Milliseconds
                <= fixture.BaselineDrop.Milliseconds);

        var validated = new CommitmentDecisionValidator().Validate(RunnerLikeContext(context, decision));

        Assert.True(validated.IsValid, validated.Witnesses.FirstOrDefault()?.Message);
        Assert.Empty(validated.ValidatedState!.Incidents.Breaches);
    }

    [Fact]
    public void Zero_switch_limits_give_every_thesis_arm_the_same_selection_problem()
    {
        // Every Tier-3 configuration of the thesis sets a zero hard limit on vehicle_switch_count
        // in all active phases, and zero stop-switch limits. With the same state, no drop limit (U),
        // Rule C, and the relaxed deadline hand the solver the same levels and the same
        // contributions: no C1 level reads a commitment budget. With a zero bound, the signature
        // sees the candidate Rule C prunes (control).
        var fixture = CreateState(seed: 3);
        var kept = fixture.BaselineDrop.Milliseconds;
        const long Bound = 30_000;

        static long? HardLimit(CommitmentDimension dimension, long? dropLimit) =>
            dimension switch
            {
                CommitmentDimension.VehicleSwitchCount
                    or CommitmentDimension.PickupStopSwitchCount
                    or CommitmentDimension.DropStopSwitchCount => 0,
                CommitmentDimension.DropEtaTotalMs => dropLimit,
                _ => null,
            };

        CommitmentPolicy Arm(
            long? dropLimit = null,
            long? deadlineSlack = null) =>
            new(
                fixture.Policy.PolicyId,
                CommitmentDimensionVocabulary.Ordered.Select(
                    dimension => new CommitmentDimensionLimit(
                        dimension,
                        HardLimit(dimension, dropLimit),
                        CommitmentPhase.AllActive)),
                new MaterialRevisionRule(1_000, null),
                dropEtaDeadlineSlack: deadlineSlack is long slack ? new Duration(slack) : null);

        CandidateSelectionProblem Problem(CommitmentPolicy policy)
        {
            var solver = new EnumeratingSolver();
            var promiseContext = PromiseContext(fixture, kept, policy);
            AlgorithmTestData.AssertGeneratedInFull(promiseContext.ReducedState, AlgorithmTestData.SmallGeneration);
            var decided = new SolverBackedRidePoolingPolicy(solver).Decide(
                promiseContext,
                AlgorithmTestData.SmallGeneration,
                HardVectorOptions());
            Assert.True(decided.IsSuccess, decided.Witness?.Message);
            return solver.LastProblem!;
        }

        static string[] Signature(CandidateSelectionProblem problem) =>
            problem.ObjectiveLevels
                .Select(value => $"{value.Name}/{value.Sense}")
                .Concat(
                    problem.Options.Select(
                        value => $"{value.OptionId}={string.Join(",", value.ObjectiveContributions)}"))
                .ToArray();

        var unlimited = Problem(Arm());
        var expected = Signature(unlimited);

        Assert.Equal(expected, Signature(Problem(Arm(dropLimit: Bound))));
        Assert.Equal(expected, Signature(Problem(Arm(deadlineSlack: Bound))));
        Assert.Equal(
            ["accepted-request-count", "operational-cost", "candidate-id-rank:vehicle-1"],
            unlimited.ObjectiveLevels.Select(value => value.Name));

        var frozen = Signature(Problem(Arm(dropLimit: 0)));
        Assert.True(frozen.Length < expected.Length, "A zero bound must prune a candidate.");
    }

    private static (
        CandidateGenerationResult Generated,
        VehicleCandidateSet Set,
        InsertionCandidate NoOp,
        InsertionCandidate OrdinalFirst,
        CommitmentMechanismContext Context) EveryCandidateRejected()
    {
        var fixture = CreateState(seed: 3);
        var generated = new InsertionCandidateGenerator().Generate(
            fixture.State,
            AlgorithmTestData.SmallGeneration);
        Assert.True(generated.IsSuccess, generated.Witness?.Message);
        Assert.True(generated.Diagnostics!.IsComplete);
        var set = generated.VehicleCandidates!.Single();
        var noOp = set.Candidates.Single(value => value.IsNoOp);
        var ordinalFirst = set.Candidates
            .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
            .First();
        // The test only discriminates when the no-op is not also the ordinal first.
        Assert.NotEqual(ordinalFirst.CandidateId, noOp.CandidateId);
        var undeclared = new CommitmentPolicy(
            "undeclared-policy-v1",
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    null,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1_000, null));
        var context = new CommitmentMechanismContext(
            fixture.BeforeEventState,
            fixture.State,
            new CommitmentPolicyCatalog([undeclared]),
            NoDistances.Instance,
            "c1-every-candidate-rejected",
            1);
        return (generated, set, noOp, ordinalFirst, context);
    }

    private static SolverBackedRidePoolingPolicyOptions HardVectorOptions() =>
        new(
            RidePoolingPolicyKind.RideBoundHardVector,
            DeterministicCandidateSelectionExecutionBudget.Create(
                100_000,
                100_000,
                DeterministicSolverBudget.Create(100_000, 100_000, 1).Value!).Value!);

    /// <summary>The validation the Runner applies to a policy decision.</summary>
    private static CommitmentValidationContext RunnerLikeContext(
        CommitmentMechanismContext context,
        DispatchDecision decision) =>
        new(
            context.BeforeEventState,
            context.ReducedState,
            decision.ProposedState,
            context.Policies,
            context.StopDistances,
            context.PublicationScope,
            context.SourceEventSequence);

    /// <summary>
    /// Exact lexicographic enumeration of every one-option-per-vehicle assignment; it
    /// records the last problem so a test can inspect the objective hierarchy.
    /// </summary>
    private sealed class EnumeratingSolver : ICandidateSelectionSolver
    {
        public CandidateSelectionProblem? LastProblem { get; private set; }

        public CandidateSelectionSolveResult Solve(
            CandidateSelectionProblem problem,
            DeterministicSolverBudget budget)
        {
            LastProblem = problem;
            CandidateSelectionSolution? best = null;
            long work = 0;
            Enumerate(0, []);

            if (best is null)
            {
                return CandidateSelectionSolveResult.Infeasible(
                    Diagnostics(problem, budget, work, []),
                    "TEST_INFEASIBLE",
                    "No assignment exists.");
            }

            var bounds = problem.ObjectiveLevels
                .Select(
                    (level, index) => ObjectiveSolveBound.Create(
                        index,
                        level,
                        best.ObjectiveValues[index],
                        best.ObjectiveValues[index]).Value!)
                .ToArray();
            return CandidateSelectionSolveResult.Optimal(
                best,
                Diagnostics(problem, budget, work, bounds));

            void Enumerate(int vehicleIndex, IReadOnlyList<string> selected)
            {
                if (vehicleIndex == problem.VehicleIds.Count)
                {
                    work++;
                    var solution = CandidateSelectionSolution.Create(problem, selected);

                    if (solution.IsSuccess
                        && (best is null
                            || LexicographicObjectiveComparer.Compare(
                                solution.Value!.ObjectiveValues,
                                best.ObjectiveValues,
                                problem.ObjectiveLevels) < 0))
                    {
                        best = solution.Value;
                    }

                    return;
                }

                foreach (var option in problem.Options.Where(
                             value => value.VehicleId
                                 == problem.VehicleIds[vehicleIndex]))
                {
                    Enumerate(vehicleIndex + 1, selected.Append(option.OptionId).ToArray());
                }
            }
        }

        private static CandidateSelectionSolverDiagnostics Diagnostics(
            CandidateSelectionProblem problem,
            DeterministicSolverBudget budget,
            long work,
            IReadOnlyList<ObjectiveSolveBound> bounds) =>
            CandidateSelectionSolverDiagnostics.Create(
                problem,
                budget,
                Math.Min(work, budget.MaximumWorkUnits),
                1,
                0,
                bounds).Value!;
    }

    /// <summary>
    /// A relaxed (DL-HZ) deadline of <paramref name="slack"/> from the given first promise, with no
    /// budget limit.
    /// </summary>
    private static CommitmentMechanismContext SlackDeadlineContext(
        Fixture fixture,
        long initialDropMs,
        long slack) =>
        PromiseContext(
            fixture,
            initialDropMs,
            new CommitmentPolicy(
                fixture.Policy.PolicyId,
                CommitmentDimensionVocabulary.Ordered.Select(
                    dimension => new CommitmentDimensionLimit(
                        dimension,
                        null,
                        CommitmentPhase.AllActive)),
                new MaterialRevisionRule(1_000, null),
                dropEtaDeadlineSlack: new Duration(slack)));

    private static string[] RetainedIds(HardVectorCandidateAssessmentResult result) =>
        result.Batch!.FeasibleCandidateSets.Single().Candidates
            .Select(value => value.CandidateId)
            .Order(StringComparer.Ordinal)
            .ToArray();

    /// <summary>
    /// The incumbent's only ledger entry is an initial promise at the kept pickup
    /// ETA and the given drop ETA; the policy is the one the incumbent booked.
    /// </summary>
    private static CommitmentMechanismContext PromiseContext(
        Fixture fixture,
        long initialDropMs,
        CommitmentPolicy policy)
    {
        var incumbent = fixture.State.Run.Requests[fixture.IncumbentId];
        var pickupStop = new StopId("incumbent-pickup");
        var dropStop = new StopId("incumbent-drop");
        var initial = new PromiseProjection(
            incumbent.Id,
            AlgorithmTestData.VehicleOne,
            pickupStop,
            incumbent.OriginNodeId,
            dropStop,
            incumbent.DestinationNodeId,
            fixture.BaselinePickup,
            new SimTime(initialDropMs),
            [
                new PromiseServiceToken(pickupStop, incumbent.Id, RouteStopKind.Pickup),
                new PromiseServiceToken(dropStop, incumbent.Id, RouteStopKind.DropOff),
            ]);
        var ledger = CommitmentLedger.Empty.OpenInitial(
            "oracle-initial-deadline",
            initial,
            1,
            fixture.State.Run.SimulationTime,
            "INITIAL_ACCEPTANCE",
            2).Ledger!;

        return new CommitmentMechanismContext(
            fixture.BeforeEventState with { Commitments = ledger },
            fixture.State with { Commitments = ledger },
            new CommitmentPolicyCatalog([policy]),
            NoDistances.Instance,
            "c1-deadline",
            1);
    }

    private static IReadOnlySet<string> RetainedKeys(
        Fixture fixture,
        IReadOnlyList<VehicleCandidateSet> candidates,
        CommitmentPolicy policy)
    {
        var filter = new CommitmentCandidateFilter(
            fixture.BeforeEventState,
            new CommitmentPolicyCatalog([policy]),
            NoDistances.Instance,
            "commitment-monotonicity",
            1);
        return filter.Filter(fixture.State, candidates)
            .Single()
            .Candidates
            .Select(SemanticKey)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static CommitmentPolicy EtaPolicy(long hardLimit) =>
        new(
            "uniform-v1",
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    dimension is CommitmentDimension.PickupEtaTotalMs
                        or CommitmentDimension.DropEtaTotalMs
                        ? hardLimit
                        : null,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1_000, null));

    private static CommitmentMechanismContext MechanismContext(
        Fixture fixture,
        string scope) =>
        new(
            fixture.BeforeEventState,
            fixture.State,
            new CommitmentPolicyCatalog([fixture.Policy]),
            NoDistances.Instance,
            scope,
            1);

    private static Fixture CreateState(int seed)
    {
        var incumbent = AlgorithmTestData.PendingRequest(
            "incumbent",
            AlgorithmTestData.NodeOne,
            AlgorithmTestData.NodeTwo,
            latestPickup: 5_000,
            maxRideTime: 5_000);
        var dynamicOrigin = seed % 2 == 0
            ? AlgorithmTestData.NodeThree
            : AlgorithmTestData.NodeTwo;
        var dynamicDestination = dynamicOrigin == AlgorithmTestData.NodeThree
            ? AlgorithmTestData.NodeOne
            : AlgorithmTestData.NodeThree;
        var dynamicRequest = AlgorithmTestData.PendingRequest(
            "dynamic",
            dynamicOrigin,
            dynamicDestination,
            latestPickup: 5_000,
            maxRideTime: 5_000);
        var pickupStop = new RouteStop(
            new StopId("incumbent-pickup"),
            incumbent.OriginNodeId,
            RouteStopKind.Pickup,
            incumbent.Id,
            new Duration(0));
        var dropStop = new RouteStop(
            new StopId("incumbent-drop"),
            incumbent.DestinationNodeId,
            RouteStopKind.DropOff,
            incumbent.Id,
            new Duration(0));
        var state = AlgorithmTestData.CreateState(
            [incumbent, dynamicRequest],
            [AlgorithmTestData.Vehicle(mutableSuffix: [pickupStop, dropStop])],
            arcs: CompleteArcs(seed));
        var run = state.Run.AcceptRequest(
            incumbent.Id,
            AlgorithmTestData.VehicleOne).Value!;
        var beforeRun = run;
        run = run.AdvanceEpoch(1, run.SimulationTime).Value!;
        state = state with { Run = run, NextEventSequence = 2 };
        var baseline = OracleEtas(
            state,
            "E:incumbent-pickup|E:incumbent-drop",
            incumbent.Id);
        var projection = new PromiseProjection(
            incumbent.Id,
            AlgorithmTestData.VehicleOne,
            pickupStop.StopId,
            pickupStop.NodeId,
            dropStop.StopId,
            dropStop.NodeId,
            baseline.Pickup,
            baseline.Drop,
            [
                new PromiseServiceToken(
                    pickupStop.StopId,
                    incumbent.Id,
                    RouteStopKind.Pickup),
                new PromiseServiceToken(
                    dropStop.StopId,
                    incumbent.Id,
                    RouteStopKind.DropOff),
            ]);
        var ledger = CommitmentLedger.Empty.OpenInitial(
            $"oracle-initial-{seed}",
            projection,
            1,
            state.Run.SimulationTime,
            "INITIAL_ACCEPTANCE",
            2).Ledger!;
        state = state with { Commitments = ledger };
        var before = state with
        {
            Run = beforeRun,
            NextEventSequence = 1,
        };
        var limit = seed % 4 * 40L;
        var policy = new CommitmentPolicy(
            "uniform-v1",
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    dimension is CommitmentDimension.PickupEtaTotalMs
                        or CommitmentDimension.DropEtaTotalMs
                        ? limit
                        : null,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1_000, null));

        return new Fixture(
            before,
            state,
            incumbent.Id,
            baseline.Pickup,
            baseline.Drop,
            limit,
            policy);
    }

    private static bool OracleAllows(
        OnlineState state,
        string semanticKey,
        RequestId incumbentId,
        SimTime baselinePickup,
        SimTime baselineDrop,
        long hardLimit)
    {
        var candidate = OracleEtas(state, semanticKey, incumbentId);
        return Difference(candidate.Pickup, baselinePickup) <= hardLimit
            && Difference(candidate.Drop, baselineDrop) <= hardLimit;
    }

    private static (SimTime Pickup, SimTime Drop) OracleEtas(
        OnlineState state,
        string semanticKey,
        RequestId incumbentId)
    {
        var vehicle = state.Run.Vehicles[AlgorithmTestData.VehicleOne];
        var time = state.Run.SimulationTime;
        var node = ((NodePosition)vehicle.Position).NodeId;
        SimTime? incumbentPickup = null;
        SimTime? incumbentDrop = null;

        foreach (var token in semanticKey.Split('|'))
        {
            var kind = token[..1];
            var id = token[2..];
            RouteStop stop;

            if (kind == "E")
            {
                stop = vehicle.Route.MutableSuffix.Single(
                    value => value.StopId.Value == id);
            }
            else
            {
                var requestId = new RequestId(id);
                var request = state.Run.Requests[requestId];
                stop = new RouteStop(
                    new StopId($"oracle-{kind}-{id}"),
                    kind == "P"
                        ? request.OriginNodeId
                        : request.DestinationNodeId,
                    kind == "P"
                        ? RouteStopKind.Pickup
                        : RouteStopKind.DropOff,
                    requestId,
                    new Duration(0));
            }

            if (node != stop.NodeId)
            {
                Assert.True(
                    state.TravelTimes!.TryGetTravelTime(
                        node,
                        stop.NodeId,
                        out var travel));
                time += travel;
            }

            if (stop.Kind == RouteStopKind.Pickup)
            {
                var request = state.Run.Requests[stop.RequestId!.Value];

                if (time.Milliseconds < request.EarliestPickup.Milliseconds)
                {
                    time = request.EarliestPickup;
                }
            }

            if (stop.RequestId == incumbentId
                && stop.Kind == RouteStopKind.Pickup)
            {
                incumbentPickup = time;
            }

            if (stop.RequestId == incumbentId
                && stop.Kind == RouteStopKind.DropOff)
            {
                incumbentDrop = time;
            }

            time += stop.ServiceDuration;
            node = stop.NodeId;
        }

        return (
            incumbentPickup
                ?? throw new InvalidOperationException("Oracle lost pickup."),
            incumbentDrop
                ?? throw new InvalidOperationException("Oracle lost drop-off."));
    }

    private static IReadOnlyList<KeyValuePair<TravelArc, Duration>> CompleteArcs(
        int seed)
    {
        var nodes = new[]
        {
            AlgorithmTestData.NodeZero,
            AlgorithmTestData.NodeOne,
            AlgorithmTestData.NodeTwo,
            AlgorithmTestData.NodeThree,
        };
        var arcs = new List<KeyValuePair<TravelArc, Duration>>();

        for (var from = 0; from < nodes.Length; from++)
        {
            for (var to = 0; to < nodes.Length; to++)
            {
                if (from == to)
                {
                    continue;
                }

                arcs.Add(
                    new KeyValuePair<TravelArc, Duration>(
                        new TravelArc(nodes[from], nodes[to]),
                        new Duration(
                            70 + ((from + 1) * 19 + (to + 1) * 23 + seed)
                                % 90)));
            }
        }

        return arcs;
    }

    private static long Difference(SimTime left, SimTime right) =>
        Math.Abs(left.Milliseconds - right.Milliseconds);

    private static string SemanticKey(InsertionCandidate candidate) =>
        string.Join(
            "|",
            candidate.Route.MutableSuffix.Select(
                stop => stop.RequestId is RequestId requestId
                    && candidate.NewRequestIds.Contains(requestId)
                    ? $"{(stop.Kind == RouteStopKind.Pickup ? "P" : "D")}:" +
                        requestId.Value
                    : $"E:{stop.StopId.Value}"));

    private sealed record Fixture(
        OnlineState BeforeEventState,
        OnlineState State,
        RequestId IncumbentId,
        SimTime BaselinePickup,
        SimTime BaselineDrop,
        long HardEtaLimit,
        CommitmentPolicy Policy);

    private sealed class NoDistances : IStopDistanceLookup
    {
        public static NoDistances Instance { get; } = new();

        public bool TryGetDistanceMillimeters(
            NodeId fromNodeId,
            NodeId toNodeId,
            out long distanceMillimeters)
        {
            distanceMillimeters = 0;
            return false;
        }
    }
}
