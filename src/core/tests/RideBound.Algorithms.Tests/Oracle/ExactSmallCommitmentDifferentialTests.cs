using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Commitments;
using RideBound.Algorithms.Policies;
using RideBound.Application.Commitments;
using RideBound.Application.Optimization;
using RideBound.Application.State;
using RideBound.Application.Travel;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Incidents;
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
        // independent pick over those candidates gives: fewest forced options, then most new
        // requests, then lowest cost, then lowest candidate id. The Runner's validation accepts it.
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
                requireSafetyNoOp: true,
                forcedReferenceRecovery: true);
            Assert.True(gated.IsSuccess, $"seed={seed}; {gated.Witness?.Message}");
            var options = gated.Batch!.FeasibleCandidateSets.Single().Candidates;
            bool IsForced(InsertionCandidate candidate) =>
                gated.Batch.Assessments[candidate.CandidateId].IsForcedReference;
            var solver = new EnumeratingSolver();

            AlgorithmTestData.AssertGeneratedInFull(context.ReducedState, AlgorithmTestData.SmallGeneration);
            var result = new SolverBackedRidePoolingPolicy(solver).Decide(
                context,
                AlgorithmTestData.SmallGeneration,
                HardVectorOptions(forcedReferenceRecovery: true));

            Assert.True(result.IsSuccess, $"seed={seed}; {result.Witness?.Message}");
            var decision = result.Decision!.Decision;
            Assert.Equal(
                options.Select(value => value.CandidateId).Order(StringComparer.Ordinal),
                solver.LastProblem!.Options.Select(value => value.OptionId).Order(StringComparer.Ordinal));
            Assert.Equal(
                (options.Any(IsForced) ? ["forced-reference-count"] : Array.Empty<string>())
                    .Concat(["accepted-request-count", "operational-cost", "candidate-id-rank:vehicle-1"]),
                solver.LastProblem.ObjectiveLevels.Select(value => value.Name));
            var best = options
                .OrderBy(value => IsForced(value) ? 1 : 0)
                .ThenByDescending(value => value.NewRequestIds.Count)
                .ThenBy(value => value.Schedule.OperationalCost)
                .ThenBy(value => value.CandidateId, StringComparer.Ordinal)
                .First();
            Assert.Equal(best.CandidateId, decision.VehiclePlans.Single().Candidate.CandidateId);
            Assert.Equal(best.Schedule.OperationalCost, decision.OperationalCost);

            // The chosen plan passes the validation the Runner applies.
            var validated = new CommitmentDecisionValidator().Validate(
                RunnerLikeContext(context, decision, decision.ForcedReferenceVehicles));
            Assert.True(
                validated.IsValid,
                $"seed={seed}; {validated.Witnesses.FirstOrDefault()?.Message}");

            if (options.Any(
                    value => IsForced(value) == IsForced(best)
                        && value.NewRequestIds.Count == best.NewRequestIds.Count
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
                ScopedVehicleId: candidate.VehicleId,
                InitialPromiseTrigger: context.InitialPromiseTrigger));
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
    public void C1_reports_a_rejected_safety_no_op_that_leaves_other_candidates_alive()
    {
        // The no-op keeps the route, so its drop ETA is the baseline. The rider's
        // initial promise is placed exactly one insertion's delay LATER than that,
        // and a zero-slack two-sided deadline then rejects the no-op (too early)
        // while the insertion that causes exactly that delay survives. Before the
        // diagnostic change the solver-backed path died later with a generic
        // mapping error.
        var (generated, set, noOp, context) = NoOpRejectedWhileAnInsertionSurvives();

        var assessed = new HardVectorCandidateAssessor().AssessAndFilter(
            context,
            generated.VehicleCandidates!,
            requireSafetyNoOp: true);

        Assert.False(assessed.IsSuccess);
        var witness = assessed.Witness!;
        Assert.Equal(CommitmentFailureCodes.SafetyNoOpRejected, witness.Code);
        Assert.Equal(noOp.CandidateId, witness.CandidateId);
        Assert.Equal(AlgorithmTestData.VehicleOne, witness.VehicleId);
        Assert.Equal(CommitmentFailureCodes.DeadlineExceeded, witness.UnderlyingCode);
        Assert.Equal(CreateState(seed: 3).IncumbentId, witness.RequestId);
        Assert.Equal("drop_eta_ms", witness.Dimension);
        Assert.Equal(set.Candidates.Count, witness.GeneratedCandidateCount);
        Assert.True(witness.GeneratedCandidateCount > witness.RejectedCandidateCount);
        Assert.StartsWith(
            "C1 rejected the safety no-op while other candidates survived. No-op rejection: ",
            witness.Message,
            StringComparison.Ordinal);
        Assert.EndsWith(
            "outside the initial-promise window.",
            witness.Message,
            StringComparison.Ordinal);
        Assert.DoesNotContain("firstCode=", witness.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_caller_that_does_not_require_the_no_op_keeps_the_surviving_candidates()
    {
        // The legacy policies select without a no-op, so for them the same state is
        // not a failure: the assessor returns what survived, exactly as before.
        var (generated, _, noOp, context) = NoOpRejectedWhileAnInsertionSurvives();

        var assessed = new HardVectorCandidateAssessor().AssessAndFilter(
            context,
            generated.VehicleCandidates!);

        Assert.True(assessed.IsSuccess, assessed.Witness?.Message);
        var retained = assessed.Batch!.FeasibleCandidateSets.Single().Candidates;
        Assert.NotEmpty(retained);
        Assert.DoesNotContain(retained, value => value.CandidateId == noOp.CandidateId);
    }

    [Fact]
    public void The_solver_backed_policy_reports_the_rejected_no_op_instead_of_a_mapping_error()
    {
        var (_, _, _, context) = NoOpRejectedWhileAnInsertionSurvives();
        var solverBudget = DeterministicSolverBudget.Create(1000, 1000, 1).Value!;
        var executionBudget = DeterministicCandidateSelectionExecutionBudget.Create(
            100_000,
            100_000,
            solverBudget).Value!;

        AlgorithmTestData.AssertGeneratedInFull(context.ReducedState, AlgorithmTestData.SmallGeneration);
        var result = new SolverBackedRidePoolingPolicy(new UnreachableSolver()).Decide(
            context,
            AlgorithmTestData.SmallGeneration,
            new SolverBackedRidePoolingPolicyOptions(
                RidePoolingPolicyKind.RideBoundHardVector,
                executionBudget));

        Assert.False(result.IsSuccess);
        Assert.Equal(
            DecisionFailureCodes.CommitmentAssessmentFailed,
            result.Witness!.Code);
        Assert.StartsWith(
            "C1 rejected the safety no-op while other candidates survived. No-op rejection: ",
            result.Witness.Message,
            StringComparison.Ordinal);
        Assert.DoesNotContain("exactly one no-op option", result.Witness.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void C1_fail_closed_cites_the_safety_no_op_when_every_candidate_is_rejected()
    {
        // An initial promise one millisecond EARLIER than the kept route and a
        // zero-slack one-sided deadline: the no-op is already too late, and the
        // insertion generator can only delay an incumbent further, so every
        // candidate is rejected. The witness must now cite the no-op itself.
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
        Assert.Equal(CommitmentFailureCodes.DeadlineExceeded, witness.UnderlyingCode);
        Assert.Equal(set.Candidates.Count, witness.RejectedCandidateCount);
        Assert.Contains(". No-op rejection: ", witness.Message, StringComparison.Ordinal);
        Assert.EndsWith(
            "beyond the initial-promise deadline.",
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
    public void Forced_recovery_keeps_a_deadline_rejected_no_op_when_every_candidate_is_rejected()
    {
        // The same state that fails closed above. Under forced-reference recovery the
        // no-op, which the one-sided deadline rejects by exogenous drift alone, is kept
        // as the vehicle's only option; every insertion stays pruned.
        var (generated, set, noOp, _, context) = EveryCandidateRejected();

        var assessed = new HardVectorCandidateAssessor().AssessAndFilter(
            context,
            generated.VehicleCandidates!,
            requireSafetyNoOp: true,
            forcedReferenceRecovery: true);

        Assert.True(assessed.IsSuccess, assessed.Witness?.Message);
        var batch = assessed.Batch!;
        var output = batch.FeasibleCandidateSets.Single();
        Assert.Equal([noOp.CandidateId], output.Candidates.Select(value => value.CandidateId));
        Assert.Equal([AlgorithmTestData.VehicleOne], batch.ForcedReferenceVehicles);
        Assert.DoesNotContain(output.PrunedCandidates, value => value.CandidateId == noOp.CandidateId);
        Assert.Equal(
            set.Candidates.Count - 1,
            output.PrunedCandidates.Count(
                value => value.Code == CommitmentFailureCodes.DeadlineExceeded));
        var assessment = batch.Assessments.Single().Value;
        Assert.Equal(noOp.CandidateId, assessment.CandidateId);
        Assert.True(assessment.IsForcedReference);
    }

    [Fact]
    public void Forced_recovery_keeps_the_no_op_beside_the_surviving_insertions()
    {
        var (generated, _, noOp, context) = NoOpRejectedWhileAnInsertionSurvives();
        var legacy = new HardVectorCandidateAssessor().AssessAndFilter(
            context,
            generated.VehicleCandidates!);

        var assessed = new HardVectorCandidateAssessor().AssessAndFilter(
            context,
            generated.VehicleCandidates!,
            requireSafetyNoOp: true,
            forcedReferenceRecovery: true);

        Assert.True(assessed.IsSuccess, assessed.Witness?.Message);
        var batch = assessed.Batch!;
        var retained = batch.FeasibleCandidateSets.Single().Candidates
            .Select(value => value.CandidateId)
            .ToArray();
        var expected = legacy.Batch!.FeasibleCandidateSets.Single().Candidates
            .Select(value => value.CandidateId)
            .Append(noOp.CandidateId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected, retained);
        Assert.Equal([AlgorithmTestData.VehicleOne], batch.ForcedReferenceVehicles);
        Assert.Equal(
            [noOp.CandidateId],
            batch.Assessments.Values
                .Where(value => value.IsForcedReference)
                .Select(value => value.CandidateId));

        // The surviving insertions keep exactly the assessment they had without recovery.
        foreach (var (candidateId, value) in legacy.Batch.Assessments)
        {
            Assert.Equal(value, batch.Assessments[candidateId]);
        }
    }

    [Fact]
    public void Forced_recovery_does_not_rescue_a_no_op_rejected_outside_the_gates()
    {
        // The requests were booked under a policy the catalog does not declare. That
        // rejection is a configuration failure, not a deadline or budget gate, so
        // recovery leaves the fail-closed witness unchanged.
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
        var legacy = new HardVectorCandidateAssessor().AssessAndFilter(
            context,
            generated.VehicleCandidates!,
            requireSafetyNoOp: true);

        var assessed = new HardVectorCandidateAssessor().AssessAndFilter(
            context,
            generated.VehicleCandidates!,
            requireSafetyNoOp: true,
            forcedReferenceRecovery: true);

        Assert.False(assessed.IsSuccess);
        Assert.Equal(legacy.Witness, assessed.Witness);
        Assert.Equal(
            CommitmentFailureCodes.VehicleHasNoFeasibleCandidate,
            assessed.Witness!.Code);
    }

    [Fact]
    public void Forced_recovery_changes_nothing_when_no_gate_rejects_the_no_op()
    {
        var fixture = CreateState(seed: 3);
        var generated = new InsertionCandidateGenerator().Generate(
            fixture.State,
            AlgorithmTestData.SmallGeneration);
        Assert.True(generated.IsSuccess, generated.Witness?.Message);
        Assert.True(generated.Diagnostics!.IsComplete);
        var context = MechanismContext(fixture, "c1-forced-inert");
        var legacy = new HardVectorCandidateAssessor().AssessAndFilter(
            context,
            generated.VehicleCandidates!,
            requireSafetyNoOp: true);

        var assessed = new HardVectorCandidateAssessor().AssessAndFilter(
            context,
            generated.VehicleCandidates!,
            requireSafetyNoOp: true,
            forcedReferenceRecovery: true);

        Assert.True(legacy.IsSuccess, legacy.Witness?.Message);
        Assert.True(assessed.IsSuccess, assessed.Witness?.Message);
        Assert.Empty(assessed.Batch!.ForcedReferenceVehicles);
        Assert.Empty(legacy.Batch!.ForcedReferenceVehicles);
        Assert.Equal(
            legacy.Batch.FeasibleCandidateSets.Single().Candidates,
            assessed.Batch.FeasibleCandidateSets.Single().Candidates);
        Assert.Equal(
            legacy.Batch.FeasibleCandidateSets.Single().PrunedCandidates
                .Select(value => (value.CandidateId, value.Code, value.Message)),
            assessed.Batch.FeasibleCandidateSets.Single().PrunedCandidates
                .Select(value => (value.CandidateId, value.Code, value.Message)));
        Assert.Equal(
            legacy.Batch.Assessments.OrderBy(value => value.Key, StringComparer.Ordinal),
            assessed.Batch.Assessments.OrderBy(value => value.Key, StringComparer.Ordinal));
    }

    [Fact]
    public void The_solver_backed_policy_keeps_the_route_and_its_validation_records_one_breach()
    {
        // End to end below the Runner: without recovery the policy fails closed; with
        // it the only option is the forced no-op, the decision names its vehicle, and
        // validating the decision the way the Runner does records one typed breach.
        var (_, _, _, _, context) = EveryCandidateRejected();
        var solver = new EnumeratingSolver();
        AlgorithmTestData.AssertGeneratedInFull(context.ReducedState, AlgorithmTestData.SmallGeneration);
        var legacy = new SolverBackedRidePoolingPolicy(solver).Decide(
            context,
            AlgorithmTestData.SmallGeneration,
            HardVectorOptions(forcedReferenceRecovery: false));
        Assert.False(legacy.IsSuccess);
        Assert.Equal(
            DecisionFailureCodes.CommitmentAssessmentFailed,
            legacy.Witness!.Code);

        var result = new SolverBackedRidePoolingPolicy(solver).Decide(
            context,
            AlgorithmTestData.SmallGeneration,
            HardVectorOptions(forcedReferenceRecovery: true));

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var decision = result.Decision!.Decision;
        Assert.True(decision.VehiclePlans.Single().Candidate.IsNoOp);
        Assert.Equal([AlgorithmTestData.VehicleOne], decision.ForcedReferenceVehicles!);
        Assert.Equal("forced-reference-count", solver.LastProblem!.ObjectiveLevels[0].Name);

        var validator = new CommitmentDecisionValidator();
        var strict = validator.Validate(RunnerLikeContext(context, decision, forced: null));
        Assert.False(strict.IsValid);
        Assert.Equal(CommitmentFailureCodes.DeadlineExceeded, strict.Witnesses[0].Code);

        var validated = validator.Validate(
            RunnerLikeContext(context, decision, decision.ForcedReferenceVehicles));
        Assert.True(validated.IsValid, validated.Witnesses.FirstOrDefault()?.Message);
        var breach = Assert.Single(validated.ForcedBreaches);
        Assert.Equal(CreateState(seed: 3).IncumbentId, breach.RequestId);
        Assert.Equal([CommitmentFailureCodes.DeadlineExceeded], breach.WitnessCodes);
        Assert.Contains(breach, validated.ValidatedState!.Incidents.Breaches);
    }

    [Fact]
    public void The_solver_backed_policy_prefers_a_surviving_insertion_over_the_forced_no_op()
    {
        var (_, _, noOp, context) = NoOpRejectedWhileAnInsertionSurvives();
        var solver = new EnumeratingSolver();

        AlgorithmTestData.AssertGeneratedInFull(context.ReducedState, AlgorithmTestData.SmallGeneration);
        var result = new SolverBackedRidePoolingPolicy(solver).Decide(
            context,
            AlgorithmTestData.SmallGeneration,
            HardVectorOptions(forcedReferenceRecovery: true));

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var decision = result.Decision!.Decision;
        Assert.NotEqual(noOp.CandidateId, decision.VehiclePlans.Single().Candidate.CandidateId);
        Assert.Null(decision.ForcedReferenceVehicles);
        Assert.Equal("forced-reference-count", solver.LastProblem!.ObjectiveLevels[0].Name);
        Assert.Contains(
            solver.LastProblem.Options,
            value => value.OptionId == noOp.CandidateId);
    }

    [Fact]
    public void No_worse_recovery_also_keeps_the_insertions_that_do_not_delay_the_late_rider()
    {
        // The state where every candidate is rejected because drift alone put the incumbent past
        // its deadline. Under (a) only the no-op survives. Under no-worse recovery an insertion
        // that leaves the incumbent's drop no later than the kept route survives too, as a forced
        // option; insertions that delay the incumbent stay pruned by the deadline.
        var (generated, set, noOp, _, context) = EveryCandidateRejected();
        var kept = new HardVectorCandidateAssessor().AssessAndFilter(
            context,
            generated.VehicleCandidates!,
            requireSafetyNoOp: true,
            forcedReferenceRecovery: true);

        var relaxed = new HardVectorCandidateAssessor().AssessAndFilter(
            context,
            generated.VehicleCandidates!,
            requireSafetyNoOp: true,
            forcedReferenceRecovery: true,
            forcedNoWorse: true);

        Assert.True(relaxed.IsSuccess, relaxed.Witness?.Message);
        var output = relaxed.Batch!.FeasibleCandidateSets.Single();
        Assert.Equal(
            [noOp.CandidateId],
            kept.Batch!.FeasibleCandidateSets.Single().Candidates.Select(value => value.CandidateId));
        Assert.Contains(output.Candidates, value => !value.IsNoOp);
        Assert.Contains(output.Candidates, value => value.CandidateId == noOp.CandidateId);
        Assert.All(
            output.Candidates,
            value => Assert.True(relaxed.Batch.Assessments[value.CandidateId].IsForcedReference));
        Assert.Equal([AlgorithmTestData.VehicleOne], relaxed.Batch.ForcedReferenceVehicles);
        Assert.Equal(set.Candidates.Count, output.Candidates.Count + output.PrunedCandidates.Count(
            value => value.Code == CommitmentFailureCodes.DeadlineExceeded));
        Assert.Contains(
            output.PrunedCandidates,
            value => value.Code == CommitmentFailureCodes.DeadlineExceeded);
    }

    [Fact]
    public void The_solver_backed_policy_serves_the_new_rider_under_no_worse_recovery()
    {
        // Under (a) the vehicle is frozen on its kept route; under no-worse recovery it accepts the
        // new rider on a route that does not delay the late incumbent, and validating the decision
        // the way the Runner does records one no-worse breach for the incumbent.
        var (_, _, _, _, context) = EveryCandidateRejected();
        var solver = new EnumeratingSolver();
        AlgorithmTestData.AssertGeneratedInFull(context.ReducedState, AlgorithmTestData.SmallGeneration);
        var frozen = new SolverBackedRidePoolingPolicy(solver).Decide(
            context,
            AlgorithmTestData.SmallGeneration,
            HardVectorOptions(forcedReferenceRecovery: true));
        Assert.True(frozen.IsSuccess, frozen.Witness?.Message);
        Assert.True(frozen.Decision!.Decision.VehiclePlans.Single().Candidate.IsNoOp);

        var result = new SolverBackedRidePoolingPolicy(solver).Decide(
            context,
            AlgorithmTestData.SmallGeneration,
            HardVectorOptions(forcedReferenceRecovery: true, forcedNoWorseRecovery: true));

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var decision = result.Decision!.Decision;
        Assert.False(decision.VehiclePlans.Single().Candidate.IsNoOp);
        Assert.Equal([AlgorithmTestData.VehicleOne], decision.ForcedReferenceVehicles!);
        Assert.Equal("forced-reference-count", solver.LastProblem!.ObjectiveLevels[0].Name);

        var validator = new CommitmentDecisionValidator();
        Assert.False(
            validator.Validate(RunnerLikeContext(context, decision, decision.ForcedReferenceVehicles))
                .IsValid);
        var validated = validator.Validate(
            RunnerLikeContext(context, decision, decision.ForcedReferenceVehicles, noWorse: true));
        Assert.True(validated.IsValid, validated.Witnesses.FirstOrDefault()?.Message);
        var breach = Assert.Single(validated.ForcedBreaches);
        Assert.Equal(CommitmentBreachKind.ForcedNoWorse, breach.Kind);
        Assert.Equal(CreateState(seed: 3).IncumbentId, breach.RequestId);
        Assert.Equal([CommitmentFailureCodes.DeadlineExceeded], breach.WitnessCodes);
        Assert.True(
            breach.SafetyProjection.DropEta.Milliseconds
                <= breach.ExogenousProjection.DropEta.Milliseconds);
    }

    [Fact]
    public void Cost_first_under_no_worse_recovery_keeps_the_forced_treatment_and_the_no_worse_gate()
    {
        // ADR-078 with recovery rule (b), as in the Tier-3 configuration: every candidate breaks a
        // deadline 1 ms before the kept drop, and a zero vehicle-switch limit applies. The vehicle is
        // forced, the solver sees exactly the candidates the no-worse assessment keeps, ranked with
        // the forced level first, and the chosen route still passes the no-worse gate as the Runner
        // validates it.
        var fixture = CreateState(seed: 3);
        var policy = new CommitmentPolicy(
            fixture.Policy.PolicyId,
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    dimension == CommitmentDimension.VehicleSwitchCount ? 0 : null,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1_000, null),
            dropEtaDeadlineSlack: new Duration(0));
        var context = PromiseContext(fixture, fixture.BaselineDrop.Milliseconds - 1, policy);
        var generated = new InsertionCandidateGenerator().Generate(
            fixture.State,
            AlgorithmTestData.SmallGeneration);
        Assert.True(generated.IsSuccess, generated.Witness?.Message);
        Assert.True(generated.Diagnostics!.IsComplete);
        var gated = new HardVectorCandidateAssessor().AssessAndFilter(
            context,
            generated.VehicleCandidates!,
            requireSafetyNoOp: true,
            forcedReferenceRecovery: true,
            forcedNoWorse: true);
        Assert.True(gated.IsSuccess, gated.Witness?.Message);
        var solver = new EnumeratingSolver();

        AlgorithmTestData.AssertGeneratedInFull(context.ReducedState, AlgorithmTestData.SmallGeneration);
        var result = new SolverBackedRidePoolingPolicy(solver).Decide(
            context,
            AlgorithmTestData.SmallGeneration,
            HardVectorOptions(forcedReferenceRecovery: true, forcedNoWorseRecovery: true));

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var decision = result.Decision!.Decision;
        Assert.Equal(
            gated.Batch!.FeasibleCandidateSets.Single().Candidates
                .Select(value => value.CandidateId)
                .Order(StringComparer.Ordinal),
            solver.LastProblem!.Options.Select(value => value.OptionId).Order(StringComparer.Ordinal));
        Assert.Equal(
            ["forced-reference-count", "accepted-request-count", "operational-cost", "candidate-id-rank:vehicle-1"],
            solver.LastProblem.ObjectiveLevels.Select(value => value.Name));
        Assert.Equal([AlgorithmTestData.VehicleOne], decision.ForcedReferenceVehicles!);
        Assert.False(decision.VehiclePlans.Single().Candidate.IsNoOp);

        var validated = new CommitmentDecisionValidator().Validate(
            RunnerLikeContext(context, decision, decision.ForcedReferenceVehicles, noWorse: true));
        Assert.True(validated.IsValid, validated.Witnesses.FirstOrDefault()?.Message);
        Assert.All(
            validated.ForcedBreaches,
            breach => Assert.True(
                breach.SafetyProjection.DropEta.Milliseconds
                    <= breach.ExogenousProjection.DropEta.Milliseconds));
    }

    [Fact]
    public void A_relaxed_deadline_keeps_exactly_what_the_strict_deadline_keeps_under_no_worse_recovery()
    {
        // DL-HZ equivalence. The kept route drops the incumbent at e and the independent oracle
        // gives every candidate's incumbent drop p. For each slack s and first promise p0, the
        // strict deadline with no-worse-than-reference recovery, the relaxed deadline with the same
        // recovery requested, and the relaxed deadline without recovery keep the same candidates:
        // those with p <= max(p0 + s, e). Only the strict rule needs a forced vehicle for it.
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
        var forcedCases = 0;
        var normalCases = 0;

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

                var strictContext = SlackDeadlineContext(fixture, initial, slack, relaxed: false);
                var relaxedContext = SlackDeadlineContext(fixture, initial, slack, relaxed: true);
                var strict = new HardVectorCandidateAssessor().AssessAndFilter(
                    strictContext,
                    generated.VehicleCandidates!,
                    requireSafetyNoOp: true,
                    forcedReferenceRecovery: true,
                    forcedNoWorse: true);
                var relaxed = new HardVectorCandidateAssessor().AssessAndFilter(
                    relaxedContext,
                    generated.VehicleCandidates!,
                    requireSafetyNoOp: true,
                    forcedReferenceRecovery: true,
                    forcedNoWorse: true);
                var plain = new HardVectorCandidateAssessor().AssessAndFilter(
                    relaxedContext,
                    generated.VehicleCandidates!,
                    requireSafetyNoOp: true);

                Assert.True(strict.IsSuccess, strict.Witness?.Message);
                Assert.True(relaxed.IsSuccess, relaxed.Witness?.Message);
                Assert.True(plain.IsSuccess, plain.Witness?.Message);
                var expected = drops
                    .Where(value => value.Value <= Math.Max(initial + slack, kept))
                    .Select(value => value.Key)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                Assert.Equal(expected, RetainedIds(strict));
                Assert.Equal(expected, RetainedIds(relaxed));
                Assert.Equal(expected, RetainedIds(plain));
                Assert.Equal(PrunedKeys(strict), PrunedKeys(relaxed));
                Assert.Equal(PrunedKeys(plain), PrunedKeys(relaxed));

                // The strict rule rejects the no-op exactly when drift put the kept route past the
                // deadline; only then does it need a forced vehicle. The relaxed rule never does.
                Assert.Equal(
                    overrun > 0 ? [AlgorithmTestData.VehicleOne] : Array.Empty<VehicleId>(),
                    strict.Batch!.ForcedReferenceVehicles.ToArray());
                Assert.Empty(relaxed.Batch!.ForcedReferenceVehicles);
                Assert.DoesNotContain(relaxed.Batch.Assessments.Values, value => value.IsForcedReference);
                Assert.Equal(
                    plain.Batch!.Assessments.OrderBy(value => value.Key, StringComparer.Ordinal),
                    relaxed.Batch.Assessments.OrderBy(value => value.Key, StringComparer.Ordinal));

                if (overrun > 0)
                {
                    forcedCases++;
                }
                else
                {
                    normalCases++;
                }
            }
        }

        Assert.True(forcedCases >= 6, $"Only {forcedCases} cases exercised the forced path.");
        Assert.True(normalCases >= 6, $"Only {normalCases} cases exercised the normal path.");
    }

    [Fact]
    public void A_relaxed_deadline_decision_needs_no_forced_vehicle_and_records_no_breach()
    {
        // Drift carried the kept route 1 ms past a zero-slack deadline. The strict rule reaches its
        // decision through a forced vehicle and records a breach; the relaxed rule reaches the same
        // plan as normal operation. On this one-vehicle fixture every option of the forced vehicle
        // counts 1 on the forced level, so that level cannot change the choice.
        var fixture = CreateState(seed: 3);
        var initial = fixture.BaselineDrop.Milliseconds - 1;
        var strictContext = SlackDeadlineContext(fixture, initial, 0, relaxed: false);
        var relaxedContext = SlackDeadlineContext(fixture, initial, 0, relaxed: true);
        var strictSolver = new EnumeratingSolver();
        var relaxedSolver = new EnumeratingSolver();
        var options = HardVectorOptions(forcedReferenceRecovery: true, forcedNoWorseRecovery: true);

        AlgorithmTestData.AssertGeneratedInFull(strictContext.ReducedState, AlgorithmTestData.SmallGeneration);
        AlgorithmTestData.AssertGeneratedInFull(relaxedContext.ReducedState, AlgorithmTestData.SmallGeneration);
        var strict = new SolverBackedRidePoolingPolicy(strictSolver).Decide(
            strictContext,
            AlgorithmTestData.SmallGeneration,
            options);
        var relaxed = new SolverBackedRidePoolingPolicy(relaxedSolver).Decide(
            relaxedContext,
            AlgorithmTestData.SmallGeneration,
            options);

        Assert.True(strict.IsSuccess, strict.Witness?.Message);
        Assert.True(relaxed.IsSuccess, relaxed.Witness?.Message);
        var strictDecision = strict.Decision!.Decision;
        var relaxedDecision = relaxed.Decision!.Decision;
        Assert.Equal([AlgorithmTestData.VehicleOne], strictDecision.ForcedReferenceVehicles!);
        Assert.Null(relaxedDecision.ForcedReferenceVehicles);
        Assert.Equal("forced-reference-count", strictSolver.LastProblem!.ObjectiveLevels[0].Name);
        Assert.DoesNotContain(
            relaxedSolver.LastProblem!.ObjectiveLevels,
            value => value.Name == "forced-reference-count");
        Assert.False(relaxedDecision.VehiclePlans.Single().Candidate.IsNoOp);
        Assert.Equal(
            strictDecision.VehiclePlans.Single().Candidate.CandidateId,
            relaxedDecision.VehiclePlans.Single().Candidate.CandidateId);

        var validator = new CommitmentDecisionValidator();
        var strictValidated = validator.Validate(
            RunnerLikeContext(
                strictContext,
                strictDecision,
                strictDecision.ForcedReferenceVehicles,
                noWorse: true));
        var relaxedValidated = validator.Validate(
            RunnerLikeContext(
                relaxedContext,
                relaxedDecision,
                relaxedDecision.ForcedReferenceVehicles,
                noWorse: true));

        Assert.True(strictValidated.IsValid, strictValidated.Witnesses.FirstOrDefault()?.Message);
        Assert.Equal(
            [CommitmentFailureCodes.DeadlineExceeded],
            Assert.Single(strictValidated.ForcedBreaches).WitnessCodes);
        Assert.True(relaxedValidated.IsValid, relaxedValidated.Witnesses.FirstOrDefault()?.Message);
        Assert.Empty(relaxedValidated.ForcedBreaches);
        Assert.Empty(relaxedValidated.ForcedExemptions);
        Assert.Empty(relaxedValidated.ValidatedState!.Incidents.Breaches);
        Assert.Equal(
            strictValidated.Publications.Select(
                value => (value.PublicationId, value.Entry.PublishedPromise.Projection.DropEta)),
            relaxedValidated.Publications.Select(
                value => (value.PublicationId, value.Entry.PublishedPromise.Projection.DropEta)));
    }

    [Fact]
    public void The_revision_cap_prunes_by_the_incumbent_change_and_never_makes_a_vehicle_forced()
    {
        // The kept route drifted 500 ms earlier than the incumbent's promise; no deadline is set. A
        // cap one millisecond below the smallest positive incumbent delay keeps exactly the
        // candidates that leave the incumbent's drop on the kept route. The drift itself is free,
        // so the no-op is never rejected, and neither recovery mode finds a vehicle to force.
        var fixture = CreateState(seed: 3);
        var generated = new InsertionCandidateGenerator().Generate(
            fixture.State,
            AlgorithmTestData.SmallGeneration);
        Assert.True(generated.IsSuccess, generated.Witness?.Message);
        Assert.True(generated.Diagnostics!.IsComplete);
        var set = generated.VehicleCandidates!.Single();
        var cap = SmallestPositiveDelay(fixture, set) - 1;
        var kept = fixture.BaselineDrop.Milliseconds;
        var context = PromiseContext(
            fixture,
            kept + 500,
            new CommitmentPolicy(
                fixture.Policy.PolicyId,
                CommitmentDimensionVocabulary.Ordered.Select(
                    dimension => new CommitmentDimensionLimit(
                        dimension,
                        null,
                        CommitmentPhase.AllActive)),
                new MaterialRevisionRule(1_000, null),
                dropEtaRevisionLimit: new Duration(cap)));
        var expected = set.Candidates
            .Where(
                value => Math.Abs(
                    OracleEtas(fixture.State, SemanticKey(value), fixture.IncumbentId)
                        .Drop.Milliseconds - kept) <= cap)
            .Select(value => value.CandidateId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Contains(set.Candidates.Single(value => value.IsNoOp).CandidateId, expected);
        Assert.True(expected.Length < set.Candidates.Count);

        var results = new[]
        {
            new HardVectorCandidateAssessor().AssessAndFilter(
                context,
                generated.VehicleCandidates!,
                requireSafetyNoOp: true),
            new HardVectorCandidateAssessor().AssessAndFilter(
                context,
                generated.VehicleCandidates!,
                requireSafetyNoOp: true,
                forcedReferenceRecovery: true),
            new HardVectorCandidateAssessor().AssessAndFilter(
                context,
                generated.VehicleCandidates!,
                requireSafetyNoOp: true,
                forcedReferenceRecovery: true,
                forcedNoWorse: true),
        };

        foreach (var result in results)
        {
            Assert.True(result.IsSuccess, result.Witness?.Message);
            Assert.Empty(result.Batch!.ForcedReferenceVehicles);
            Assert.DoesNotContain(result.Batch.Assessments.Values, value => value.IsForcedReference);
            Assert.Equal(expected, RetainedIds(result));
            var pruned = result.Batch.FeasibleCandidateSets.Single().PrunedCandidates
                .Where(value => set.Candidates.Any(candidate => candidate.CandidateId == value.CandidateId))
                .ToArray();
            Assert.Equal(set.Candidates.Count - expected.Length, pruned.Length);
            Assert.All(
                pruned,
                value =>
                {
                    Assert.Equal(CommitmentFailureCodes.RevisionLimitExceeded, value.Code);
                    Assert.Equal(
                        CommitmentLockEvaluator.RevisionCapRule,
                        Assert.Single(value.CommitmentWitnesses!).Rule);
                });
        }

        Assert.Equal(PrunedKeys(results[0]), PrunedKeys(results[1]));
        Assert.Equal(PrunedKeys(results[0]), PrunedKeys(results[2]));
    }

    [Fact]
    public void Zero_switch_limits_give_every_thesis_arm_the_same_selection_problem()
    {
        // Every Tier-3 configuration of the thesis sets a zero hard limit on vehicle_switch_count
        // in all active phases, and zero stop-switch limits. With the same state, no drop limit (U),
        // Rule C, the revision cap, and the strict or relaxed deadline hand the solver the same
        // levels and the same contributions: no C1 level reads a commitment budget. With a zero
        // bound, Rule C and the cap also keep the same candidates, and the signature sees the
        // candidate they prune (control).
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
            long? revisionLimit = null,
            long? deadlineSlack = null,
            bool relaxed = false) =>
            new(
                fixture.Policy.PolicyId,
                CommitmentDimensionVocabulary.Ordered.Select(
                    dimension => new CommitmentDimensionLimit(
                        dimension,
                        HardLimit(dimension, dropLimit),
                        CommitmentPhase.AllActive)),
                new MaterialRevisionRule(1_000, null),
                dropEtaDeadlineSlack: deadlineSlack is long slack ? new Duration(slack) : null,
                dropEtaDeadlineRelaxed: relaxed,
                dropEtaRevisionLimit: revisionLimit is long limit ? new Duration(limit) : null);

        CandidateSelectionProblem Problem(CommitmentPolicy policy)
        {
            var solver = new EnumeratingSolver();
            var promiseContext = PromiseContext(fixture, kept, policy);
            AlgorithmTestData.AssertGeneratedInFull(promiseContext.ReducedState, AlgorithmTestData.SmallGeneration);
            var decided = new SolverBackedRidePoolingPolicy(solver).Decide(
                promiseContext,
                AlgorithmTestData.SmallGeneration,
                HardVectorOptions(forcedReferenceRecovery: false));
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
        Assert.Equal(expected, Signature(Problem(Arm(revisionLimit: Bound))));
        Assert.Equal(expected, Signature(Problem(Arm(deadlineSlack: Bound))));
        Assert.Equal(expected, Signature(Problem(Arm(deadlineSlack: Bound, relaxed: true))));
        Assert.Equal(
            ["accepted-request-count", "operational-cost", "candidate-id-rank:vehicle-1"],
            unlimited.ObjectiveLevels.Select(value => value.Name));

        var frozen = Signature(Problem(Arm(dropLimit: 0)));
        Assert.Equal(frozen, Signature(Problem(Arm(revisionLimit: 0))));
        Assert.True(frozen.Length < expected.Length, "A zero bound must prune a candidate.");
    }

    [Fact]
    public void No_worse_recovery_requires_forced_reference_recovery()
    {
        var (generated, _, _, context) = NoOpRejectedWhileAnInsertionSurvives();
        var budget = DeterministicCandidateSelectionExecutionBudget.Create(
            100_000,
            100_000,
            DeterministicSolverBudget.Create(1000, 1000, 1).Value!).Value!;

        Assert.Throws<ArgumentException>(
            () => new HardVectorCandidateAssessor().AssessAndFilter(
                context,
                generated.VehicleCandidates!,
                requireSafetyNoOp: true,
                forcedNoWorse: true));
        Assert.Throws<ArgumentException>(
            () => new SolverBackedRidePoolingPolicyOptions(
                RidePoolingPolicyKind.RideBoundHardVector,
                budget,
                forcedNoWorseRecovery: true));
    }

    [Fact]
    public void Forced_recovery_requires_the_safety_no_op()
    {
        var (generated, _, _, context) = NoOpRejectedWhileAnInsertionSurvives();

        Assert.Throws<ArgumentException>(
            () => new HardVectorCandidateAssessor().AssessAndFilter(
                context,
                generated.VehicleCandidates!,
                forcedReferenceRecovery: true));
    }

    private static (
        CandidateGenerationResult Generated,
        VehicleCandidateSet Set,
        InsertionCandidate NoOp,
        CommitmentMechanismContext Context) NoOpRejectedWhileAnInsertionSurvives()
    {
        var fixture = CreateState(seed: 3);
        var generated = new InsertionCandidateGenerator().Generate(
            fixture.State,
            AlgorithmTestData.SmallGeneration);
        Assert.True(generated.IsSuccess, generated.Witness?.Message);
        Assert.True(generated.Diagnostics!.IsComplete);
        var set = generated.VehicleCandidates!.Single();
        var noOp = set.Candidates.Single(value => value.IsNoOp);
        var context = DeadlineContext(
            fixture,
            initialDropMs: fixture.BaselineDrop.Milliseconds + SmallestPositiveDelay(fixture, set),
            twoSided: true);
        return (generated, set, noOp, context);
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
        var context = DeadlineContext(
            fixture,
            initialDropMs: fixture.BaselineDrop.Milliseconds - 1,
            twoSided: false);
        return (generated, set, noOp, ordinalFirst, context);
    }

    private static long SmallestPositiveDelay(Fixture fixture, VehicleCandidateSet set) =>
        set.Candidates
            .Where(value => !value.IsNoOp)
            .Select(value => OracleEtas(fixture.State, SemanticKey(value), fixture.IncumbentId)
                .Drop.Milliseconds - fixture.BaselineDrop.Milliseconds)
            .Where(value => value > 0)
            .Min();

    private static SolverBackedRidePoolingPolicyOptions HardVectorOptions(
        bool forcedReferenceRecovery,
        bool forcedNoWorseRecovery = false) =>
        new(
            RidePoolingPolicyKind.RideBoundHardVector,
            DeterministicCandidateSelectionExecutionBudget.Create(
                100_000,
                100_000,
                DeterministicSolverBudget.Create(100_000, 100_000, 1).Value!).Value!,
            forcedReferenceRecovery: forcedReferenceRecovery,
            forcedNoWorseRecovery: forcedNoWorseRecovery);

    /// <summary>The validation the Runner applies to a policy decision.</summary>
    private static CommitmentValidationContext RunnerLikeContext(
        CommitmentMechanismContext context,
        DispatchDecision decision,
        IReadOnlySet<VehicleId>? forced,
        bool noWorse = false) =>
        new(
            context.BeforeEventState,
            context.ReducedState,
            decision.ProposedState,
            context.Policies,
            context.StopDistances,
            context.PublicationScope,
            context.SourceEventSequence,
            InitialPromiseTrigger: context.InitialPromiseTrigger,
            ForcedReferenceVehicles: forced,
            ForcedNoWorse: noWorse);

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

    /// <summary>The assessment fails before selection, so the solver is never called.</summary>
    private sealed class UnreachableSolver : ICandidateSelectionSolver
    {
        public CandidateSelectionSolveResult Solve(
            CandidateSelectionProblem problem,
            DeterministicSolverBudget budget) =>
            throw new InvalidOperationException("The solver must not be reached.");
    }

    private static CommitmentMechanismContext DeadlineContext(
        Fixture fixture,
        long initialDropMs,
        bool twoSided) =>
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
                dropEtaDeadlineSlack: new Duration(0),
                dropEtaDeadlineTwoSided: twoSided));

    /// <summary>
    /// A one-sided deadline of <paramref name="slack"/> from the given first promise, strict or
    /// relaxed (DL-HZ), with no budget limit.
    /// </summary>
    private static CommitmentMechanismContext SlackDeadlineContext(
        Fixture fixture,
        long initialDropMs,
        long slack,
        bool relaxed) =>
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
                dropEtaDeadlineSlack: new Duration(slack),
                dropEtaDeadlineRelaxed: relaxed));

    private static string[] RetainedIds(HardVectorCandidateAssessmentResult result) =>
        result.Batch!.FeasibleCandidateSets.Single().Candidates
            .Select(value => value.CandidateId)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static (string CandidateId, string Code, string Message)[] PrunedKeys(
        HardVectorCandidateAssessmentResult result) =>
        result.Batch!.FeasibleCandidateSets.Single().PrunedCandidates
            .Select(value => (value.CandidateId, value.Code, value.Message))
            .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
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
