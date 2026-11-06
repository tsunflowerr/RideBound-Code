using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Commitments;
using RideBound.Application.State;
using RideBound.Domain.Common;
using RideBound.Domain.Routes;
using RideBound.Domain.Validation;

namespace RideBound.Algorithms.Policies;

public sealed record MultiplePlanPoolOptions
{
    public MultiplePlanPoolOptions(
        int maximumPlanCount,
        long maximumCombinationWorkUnits,
        bool requireCompleteEnumeration)
    {
        if (maximumPlanCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumPlanCount));
        }

        if (maximumCombinationWorkUnits is < 1
            or > DomainLimits.MaxCanonicalInteger)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCombinationWorkUnits));
        }

        MaximumPlanCount = maximumPlanCount;
        MaximumCombinationWorkUnits = maximumCombinationWorkUnits;
        RequireCompleteEnumeration = requireCompleteEnumeration;
    }

    public int MaximumPlanCount { get; }

    public long MaximumCombinationWorkUnits { get; }

    public bool RequireCompleteEnumeration { get; }
}

public sealed record MultiplePlanPoolDiagnostics(
    long CombinationWorkUnits,
    long ConsistentFleetCombinationCount,
    long CompatibleFleetPlanCount,
    long SemanticallyUniquePlanCount,
    long DominatedPlanCount,
    long RetainedPlanCount,
    bool WasCombinationWorkTruncated);

public sealed record MultiplePlanSelection(
    FleetSelection DistinguishedSelection,
    VersionedPlanPool PlanPool,
    MultiplePlanPoolDiagnostics Diagnostics);

public sealed record MultiplePlanSelectionResult
{
    private MultiplePlanSelectionResult(
        MultiplePlanSelection? selection,
        RollingCostWitness? witness)
    {
        Selection = selection;
        Witness = witness;
    }

    public bool IsSuccess => Selection is not null;

    public MultiplePlanSelection? Selection { get; }

    public RollingCostWitness? Witness { get; }

    public static MultiplePlanSelectionResult Success(
        MultiplePlanSelection selection) => new(selection, null);

    public static MultiplePlanSelectionResult Failure(
        RollingCostWitness witness) => new(null, witness);
}

public static class MultiplePlanFailureCodes
{
    public const string CombinationWorkBoundExceeded =
        "MULTIPLE_PLAN_COMBINATION_WORK_BOUND_EXCEEDED";
    public const string NoCompatiblePlan = "NO_COMPATIBLE_MULTIPLE_PLAN";
    public const string PlanPoolVersionOverflow = "PLAN_POOL_VERSION_OVERFLOW";
    public const string AlternativeRebaseFailed =
        "MULTIPLE_PLAN_ALTERNATIVE_REBASE_FAILED";
}

/// <summary>
/// Enumerates globally consistent fleet selections from one shared raw candidate
/// set, removes dominated plans, retains a deterministic max-min diverse pool,
/// and distinguishes the plan with greatest shared executable-prefix consensus.
/// </summary>
public sealed class MultiplePlanFleetSelector
{
    private readonly PhysicalPlanValidator _validator;

    public MultiplePlanFleetSelector(PhysicalPlanValidator? validator = null)
    {
        _validator = validator ?? new PhysicalPlanValidator();
    }

    public MultiplePlanSelectionResult Select(
        OnlineState state,
        IReadOnlyList<VehicleCandidateSet> vehicleCandidateSets,
        MultiplePlanPoolOptions options)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(vehicleCandidateSets);
        ArgumentNullException.ThrowIfNull(options);

        if (state.TravelTimes is null)
        {
            return MultiplePlanSelectionResult.Failure(
                new RollingCostWitness(
                    CandidateGenerationFailureCodes.TravelSnapshotRequired,
                    "Multiple-plan selection requires a travel snapshot."));
        }

        var orderedSets = vehicleCandidateSets
            .OrderBy(value => value.VehicleId.Value, StringComparer.Ordinal)
            .ToArray();
        var missing = orderedSets.FirstOrDefault(value => value.Candidates.Count == 0);

        if (missing is not null)
        {
            return MultiplePlanSelectionResult.Failure(
                new RollingCostWitness(
                    RollingCostFailureCodes.NoVehiclePlan,
                    "Every vehicle requires at least one feasible plan.",
                    missing.VehicleId));
        }

        var combinations = new List<FleetSelection>();
        long work = 0;
        var truncated = false;
        RollingCostWitness? overflow = null;
        Enumerate(
            0,
            orderedSets,
            [],
            new HashSet<RequestId>(),
            0,
            0,
            options.MaximumCombinationWorkUnits,
            ref work,
            ref truncated,
            combinations,
            ref overflow);

        if (truncated && options.RequireCompleteEnumeration)
        {
            return MultiplePlanSelectionResult.Failure(
                new RollingCostWitness(
                    MultiplePlanFailureCodes.CombinationWorkBoundExceeded,
                    "Exact multiple-plan enumeration exceeded its explicit " +
                    "combination work bound.",
                    Dimension: "maximumCombinationWorkUnits"));
        }

        if (combinations.Count == 0)
        {
            return MultiplePlanSelectionResult.Failure(
                overflow
                ?? new RollingCostWitness(
                    RollingCostFailureCodes.NoVehiclePlan,
                    "No globally consistent fleet candidate set exists."));
        }

        var baseline = combinations.Aggregate(
            (current, candidate) => IsOperationallyBetter(candidate, current)
                ? candidate
                : current);
        var acceptedAssignment = NewRequestAssignment(baseline);
        var compatible = new List<RankedFleetPlan>();

        foreach (var selection in combinations)
        {
            if (!AssignmentsEqual(
                    acceptedAssignment,
                    NewRequestAssignment(selection))
                || !IsCompatibleAndFeasible(state, selection))
            {
                continue;
            }

            var canonical = CanonicalFleetPlan.Create(
                state.Run.AppliedEpoch,
                selection.VehiclePlans.Select(
                    value => new CanonicalVehiclePlan(
                        value.VehicleId,
                        value.Candidate.Route)));
            compatible.Add(
                new RankedFleetPlan(
                    selection,
                    canonical,
                    MinimumForwardSlack(selection)));
        }

        var unique = compatible
            .GroupBy(value => value.Plan.PlanId, StringComparer.Ordinal)
            .Select(
                group => group.Aggregate(
                    (current, candidate) =>
                        IsOperationallyBetter(
                            candidate.Selection,
                            current.Selection)
                            ? candidate
                            : current))
            .OrderBy(value => value.Plan.PlanId, StringComparer.Ordinal)
            .ToArray();

        if (unique.Length == 0)
        {
            return MultiplePlanSelectionResult.Failure(
                new RollingCostWitness(
                    MultiplePlanFailureCodes.NoCompatiblePlan,
                    "No fleet plan remained compatible with the distinguished " +
                    "acceptance/assignment and executed/frozen decisions."));
        }

        var nondominated = unique
            .Where(
                candidate => !unique.Any(
                    other => !ReferenceEquals(other, candidate)
                        && Dominates(other, candidate)))
            .ToArray();
        var retained = RetainDiverse(nondominated, options.MaximumPlanCount);
        var distinguished = retained
            .OrderByDescending(value => ConsensusScore(value, retained))
            .ThenByDescending(value => value.Selection.AcceptedRequestCount)
            .ThenBy(value => value.Selection.OperationalCost)
            .ThenBy(value => CandidateVector(value.Selection), StringComparer.Ordinal)
            .ThenBy(value => value.Plan.PlanId, StringComparer.Ordinal)
            .First();
        var rebased = RebaseAgainstDistinguished(retained, distinguished);

        if (rebased.Witness is not null)
        {
            return MultiplePlanSelectionResult.Failure(rebased.Witness);
        }

        var finalPlans = rebased.Plans!;
        var finalDistinguished = finalPlans.Single(
            value => value.VehiclePlans.All(
                vehicle => distinguished.Selection.VehiclePlans.Single(
                        selected => selected.VehicleId == vehicle.VehicleId)
                    .Candidate.Route.IsSemanticallyEqual(vehicle.Route)));
        VersionedPlanPool pool;

        try
        {
            pool = VersionedPlanPool.CreateNext(
                state.PlanPool,
                state.Run.AppliedEpoch,
                finalDistinguished.PlanId,
                finalPlans);
        }
        catch (OverflowException)
        {
            return MultiplePlanSelectionResult.Failure(
                new RollingCostWitness(
                    MultiplePlanFailureCodes.PlanPoolVersionOverflow,
                    "Plan-pool version cannot advance.",
                    Dimension: "planPoolVersion"));
        }

        return MultiplePlanSelectionResult.Success(
            new MultiplePlanSelection(
                distinguished.Selection,
                pool,
                new MultiplePlanPoolDiagnostics(
                    work,
                    combinations.Count,
                    compatible.Count,
                    unique.Length,
                    unique.Length - nondominated.Length,
                    retained.Count,
                    truncated)));
    }

    private static void Enumerate(
        int index,
        IReadOnlyList<VehicleCandidateSet> sets,
        IReadOnlyList<SelectedVehiclePlan> selected,
        IReadOnlySet<RequestId> assignedRequests,
        int acceptedCount,
        long operationalCost,
        long maximumWork,
        ref long work,
        ref bool truncated,
        ICollection<FleetSelection> output,
        ref RollingCostWitness? overflow)
    {
        if (index == sets.Count)
        {
            output.Add(
                new FleetSelection(
                    selected.ToArray(),
                    acceptedCount,
                    operationalCost));
            return;
        }

        foreach (var candidate in sets[index].Candidates
                     .OrderByDescending(value => value.NewRequestIds.Count)
                     .ThenBy(value => value.Schedule.OperationalCost)
                     .ThenBy(
                         value => value.CertifiedForwardSlackMilliseconds is null
                             ? 0
                             : 1)
                     .ThenByDescending(
                         value => value.CertifiedForwardSlackMilliseconds ?? 0)
                     .ThenBy(value => value.CandidateId, StringComparer.Ordinal))
        {
            if (work == maximumWork)
            {
                truncated = true;
                return;
            }

            work++;

            if (candidate.NewRequestIds.Any(assignedRequests.Contains))
            {
                continue;
            }

            long nextCost;

            try
            {
                nextCost = checked(
                    operationalCost + candidate.Schedule.OperationalCost);

                if (nextCost > DomainLimits.MaxCanonicalInteger)
                {
                    throw new OverflowException();
                }
            }
            catch (OverflowException)
            {
                overflow ??= new RollingCostWitness(
                    RollingCostFailureCodes.OperationalCostOverflow,
                    "Fleet operational cost exceeded the integer range.",
                    candidate.VehicleId,
                    CandidateId: candidate.CandidateId,
                    Dimension: "operationalCost");
                continue;
            }

            Enumerate(
                index + 1,
                sets,
                selected.Append(
                    new SelectedVehiclePlan(sets[index].VehicleId, candidate))
                    .ToArray(),
                assignedRequests.Concat(candidate.NewRequestIds).ToHashSet(),
                acceptedCount + candidate.NewRequestIds.Count,
                nextCost,
                maximumWork,
                ref work,
                ref truncated,
                output,
                ref overflow);

            if (truncated)
            {
                return;
            }
        }
    }

    private bool IsCompatibleAndFeasible(
        OnlineState state,
        FleetSelection selection)
    {
        if (selection.VehiclePlans.Count != state.Run.Vehicles.Count)
        {
            return false;
        }

        foreach (var plan in selection.VehiclePlans)
        {
            if (!state.Run.Vehicles.TryGetValue(plan.VehicleId, out var vehicle)
                || !vehicle.Route.HasExactFrozenPrefix(plan.Candidate.Route))
            {
                return false;
            }

            var validation = _validator.ValidateWithExogenousRelief(
                state.Run,
                plan.VehicleId,
                plan.Candidate.Route,
                state.TravelTimes!,
                state.Run.SimulationTime);

            if (!validation.IsFeasible)
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<RequestId, VehicleId> NewRequestAssignment(
        FleetSelection selection) =>
        selection.VehiclePlans
            .SelectMany(
                plan => plan.Candidate.NewRequestIds.Select(
                    request => new KeyValuePair<RequestId, VehicleId>(
                        request,
                        plan.VehicleId)))
            .ToDictionary(value => value.Key, value => value.Value);

    private static bool AssignmentsEqual(
        IReadOnlyDictionary<RequestId, VehicleId> left,
        IReadOnlyDictionary<RequestId, VehicleId> right) =>
        left.Count == right.Count
        && left.All(
            value => right.TryGetValue(value.Key, out var vehicle)
                && vehicle == value.Value);

    private static long MinimumForwardSlack(FleetSelection selection) =>
        selection.VehiclePlans.Min(
            value => value.Candidate.CertifiedForwardSlackMilliseconds
                ?? DomainLimits.MaxCanonicalInteger);

    private static bool Dominates(
        RankedFleetPlan candidate,
        RankedFleetPlan current) =>
        candidate.Selection.AcceptedRequestCount
            >= current.Selection.AcceptedRequestCount
        && candidate.Selection.OperationalCost
            <= current.Selection.OperationalCost
        && candidate.MinimumForwardSlackMilliseconds
            >= current.MinimumForwardSlackMilliseconds
        && (candidate.Selection.AcceptedRequestCount
                > current.Selection.AcceptedRequestCount
            || candidate.Selection.OperationalCost
                < current.Selection.OperationalCost
            || candidate.MinimumForwardSlackMilliseconds
                > current.MinimumForwardSlackMilliseconds);

    private static IReadOnlyList<RankedFleetPlan> RetainDiverse(
        IReadOnlyList<RankedFleetPlan> candidates,
        int maximumPlanCount)
    {
        var remaining = candidates.ToList();
        var first = remaining
            .OrderByDescending(value => value.Selection.AcceptedRequestCount)
            .ThenBy(value => value.Selection.OperationalCost)
            .ThenBy(value => CandidateVector(value.Selection), StringComparer.Ordinal)
            .ThenBy(value => value.Plan.PlanId, StringComparer.Ordinal)
            .First();
        var retained = new List<RankedFleetPlan> { first };
        remaining.Remove(first);

        while (retained.Count < maximumPlanCount && remaining.Count > 0)
        {
            var next = remaining
                .OrderByDescending(
                    candidate => retained.Min(
                        selected => RouteDistance(candidate.Plan, selected.Plan)))
                .ThenByDescending(value => value.Selection.AcceptedRequestCount)
                .ThenBy(value => value.Selection.OperationalCost)
                .ThenBy(value => CandidateVector(value.Selection), StringComparer.Ordinal)
                .ThenBy(value => value.Plan.PlanId, StringComparer.Ordinal)
                .First();
            retained.Add(next);
            remaining.Remove(next);
        }

        return retained.AsReadOnly();
    }

    private static long RouteDistance(
        CanonicalFleetPlan left,
        CanonicalFleetPlan right)
    {
        long distance = 0;

        foreach (var (leftVehicle, rightVehicle) in left.VehiclePlans.Zip(
                     right.VehiclePlans))
        {
            var leftStops = leftVehicle.Route.RemainingStops.ToArray();
            var rightStops = rightVehicle.Route.RemainingStops.ToArray();
            var maximum = Math.Max(leftStops.Length, rightStops.Length);

            for (var index = 0; index < maximum; index++)
            {
                if (index >= leftStops.Length
                    || index >= rightStops.Length
                    || !Equals(leftStops[index], rightStops[index]))
                {
                    distance = SaturatingIncrement(distance);
                }
            }
        }

        return distance;
    }

    private static RebasedPlanResult RebaseAgainstDistinguished(
        IReadOnlyList<RankedFleetPlan> retained,
        RankedFleetPlan distinguished)
    {
        var plans = new List<CanonicalFleetPlan>(retained.Count);

        foreach (var alternative in retained)
        {
            var vehicles = new List<CanonicalVehiclePlan>(
                alternative.Plan.VehiclePlans.Count);

            foreach (var vehicle in alternative.Plan.VehiclePlans)
            {
                var publishedRoute = distinguished.Selection.VehiclePlans.Single(
                    value => value.VehicleId == vehicle.VehicleId).Candidate.Route;
                RoutePlan executableRoute;

                if (publishedRoute.IsSemanticallyEqual(vehicle.Route))
                {
                    executableRoute = publishedRoute;
                }
                else
                {
                    PlanVersion nextVersion;

                    try
                    {
                        nextVersion = publishedRoute.Version.Next();
                    }
                    catch (OverflowException)
                    {
                        return RebasedPlanResult.Failure(
                            new RollingCostWitness(
                                MultiplePlanFailureCodes.AlternativeRebaseFailed,
                                "An alternative route cannot advance beyond the " +
                                "distinguished route version.",
                                vehicle.VehicleId,
                                Dimension: "planVersion"));
                    }

                    var created = RoutePlan.Create(
                        nextVersion,
                        vehicle.Route.ExecutedStopCount,
                        vehicle.Route.FrozenPrefix,
                        vehicle.Route.MutableSuffix);

                    if (!created.IsSuccess)
                    {
                        return RebasedPlanResult.Failure(
                            new RollingCostWitness(
                                MultiplePlanFailureCodes.AlternativeRebaseFailed,
                                created.Failure!.Message,
                                vehicle.VehicleId,
                                Dimension: created.Failure.Dimension));
                    }

                    executableRoute = created.Value!;
                }

                vehicles.Add(
                    new CanonicalVehiclePlan(vehicle.VehicleId, executableRoute));
            }

            plans.Add(
                CanonicalFleetPlan.Create(
                    alternative.Plan.SourceEpoch,
                    vehicles));
        }

        return RebasedPlanResult.Success(plans.AsReadOnly());
    }

    private static long ConsensusScore(
        RankedFleetPlan candidate,
        IReadOnlyList<RankedFleetPlan> pool)
    {
        long score = 0;

        foreach (var other in pool)
        {
            if (ReferenceEquals(candidate, other))
            {
                continue;
            }

            foreach (var (candidateVehicle, otherVehicle) in
                     candidate.Plan.VehiclePlans.Zip(other.Plan.VehiclePlans))
            {
                foreach (var (candidateStop, otherStop) in
                         candidateVehicle.Route.RemainingStops.Zip(
                             otherVehicle.Route.RemainingStops))
                {
                    if (!Equals(candidateStop, otherStop))
                    {
                        break;
                    }

                    score = SaturatingIncrement(score);
                }
            }
        }

        return score;
    }

    private static long SaturatingIncrement(long value) =>
        value == DomainLimits.MaxCanonicalInteger ? value : value + 1;

    private static bool IsOperationallyBetter(
        FleetSelection candidate,
        FleetSelection current)
    {
        if (candidate.AcceptedRequestCount != current.AcceptedRequestCount)
        {
            return candidate.AcceptedRequestCount > current.AcceptedRequestCount;
        }

        if (candidate.OperationalCost != current.OperationalCost)
        {
            return candidate.OperationalCost < current.OperationalCost;
        }

        return StringComparer.Ordinal.Compare(
            CandidateVector(candidate),
            CandidateVector(current)) < 0;
    }

    private static string CandidateVector(FleetSelection selection) =>
        string.Join(
            "\u001f",
            selection.VehiclePlans
                .OrderBy(value => value.VehicleId.Value, StringComparer.Ordinal)
                .Select(value => value.Candidate.CandidateId));

    private sealed record RankedFleetPlan(
        FleetSelection Selection,
        CanonicalFleetPlan Plan,
        long MinimumForwardSlackMilliseconds);

    private sealed record RebasedPlanResult(
        IReadOnlyList<CanonicalFleetPlan>? Plans,
        RollingCostWitness? Witness)
    {
        public static RebasedPlanResult Success(
            IReadOnlyList<CanonicalFleetPlan> plans) => new(plans, null);

        public static RebasedPlanResult Failure(
            RollingCostWitness witness) => new(null, witness);
    }
}

public sealed record MultiplePlanDecision(
    RollingCostDecision DistinguishedDecision,
    VersionedPlanPool PlanPool,
    MultiplePlanPoolDiagnostics Diagnostics,
    CandidateGenerationDiagnostics GenerationDiagnostics);

public sealed record MultiplePlanDecisionResult
{
    private MultiplePlanDecisionResult(
        MultiplePlanDecision? decision,
        RollingCostWitness? witness)
    {
        Decision = decision;
        Witness = witness;
    }

    public bool IsSuccess => Decision is not null;

    public MultiplePlanDecision? Decision { get; }

    public RollingCostWitness? Witness { get; }

    public static MultiplePlanDecisionResult Success(
        MultiplePlanDecision decision) => new(decision, null);

    public static MultiplePlanDecisionResult Failure(
        RollingCostWitness witness) => new(null, witness);
}

/// <summary>
/// B5 policy. Candidate generation happens once; alternatives cannot publish.
/// The distinguished selection alone is applied and exposed as request actions.
/// </summary>
public sealed class MultiplePlanConsensusPolicy
{
    private readonly InsertionCandidateGenerator _generator;
    private readonly MultiplePlanFleetSelector _selector;
    private readonly PhysicalPlanValidator _validator;

    public MultiplePlanConsensusPolicy(
        InsertionCandidateGenerator? generator = null,
        MultiplePlanFleetSelector? selector = null,
        PhysicalPlanValidator? validator = null)
    {
        _generator = generator ?? new InsertionCandidateGenerator();
        _selector = selector ?? new MultiplePlanFleetSelector();
        _validator = validator ?? new PhysicalPlanValidator();
    }

    public string PolicyId => RidePoolingPolicyRegistry.LeastCommitmentConsensus;

    public MultiplePlanDecisionResult Decide(
        OnlineState state,
        CandidateGenerationOptions generationOptions,
        MultiplePlanPoolOptions poolOptions,
        CommitmentCandidateFilter? commitmentFilter = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(generationOptions);
        ArgumentNullException.ThrowIfNull(poolOptions);

        var generated = _generator.Generate(state, generationOptions);

        if (!generated.IsSuccess)
        {
            return MultiplePlanDecisionResult.Failure(
                new RollingCostWitness(
                    RollingCostFailureCodes.CandidateGenerationFailed,
                    generated.Witness!.Message,
                    generated.Witness.VehicleId,
                    generated.Witness.RequestId,
                    Dimension: generated.Witness.Dimension));
        }

        var candidates = commitmentFilter is null
            ? generated.VehicleCandidates!
            : commitmentFilter.Filter(state, generated.VehicleCandidates!);
        var selected = _selector.Select(state, candidates, poolOptions);

        if (!selected.IsSuccess)
        {
            return MultiplePlanDecisionResult.Failure(selected.Witness!);
        }

        var selection = selected.Selection!;
        var validation = RollingCostPolicy.ValidateSelection(
            state,
            selection.DistinguishedSelection,
            _validator);

        if (validation is not null)
        {
            return MultiplePlanDecisionResult.Failure(validation);
        }

        var applied = RollingCostPolicy.ApplySelection(
            state,
            selection.DistinguishedSelection,
            candidates);

        if (!applied.IsSuccess)
        {
            return MultiplePlanDecisionResult.Failure(applied.Witness!);
        }

        var distinguished = applied.Decision! with
        {
            ProposedState = applied.Decision.ProposedState with
            {
                PlanPool = selection.PlanPool,
            },
        };

        return MultiplePlanDecisionResult.Success(
            new MultiplePlanDecision(
                distinguished,
                selection.PlanPool,
                selection.Diagnostics,
                generated.Diagnostics!));
    }
}
