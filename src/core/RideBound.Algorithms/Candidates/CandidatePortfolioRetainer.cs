using RideBound.Domain.Common;
using RideBound.Domain.Routes;

namespace RideBound.Algorithms.Candidates;

public sealed record CandidateRetentionResult(
    IReadOnlyList<InsertionCandidate> Retained,
    IReadOnlyList<InsertionCandidate> Omitted);

/// <summary>
/// Reduces a physically feasible per-vehicle candidate pool without allowing
/// route variants of one service set to hide every alternative service set.
/// The v1 portfolio also reserves, when capacity permits, the route variant
/// that changes the incumbent prefix and incumbent schedule the least.
/// </summary>
public sealed class CandidatePortfolioRetainer
{
    public CandidateRetentionResult Retain(
        IReadOnlyCollection<InsertionCandidate> candidates,
        int maximumCandidates,
        CandidateRetentionStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        return RetainRanked(
            Rank(candidates).ToArray(),
            maximumCandidates,
            strategy);
    }

    /// <summary>
    /// Retains from a pool the caller has already put in <see cref="Rank"/>
    /// order. Ranking is this type's precondition, so a caller that had to rank
    /// anyway must not pay for a second sort; the order is still verified here
    /// rather than trusted.
    /// </summary>
    public CandidateRetentionResult RetainRanked(
        IReadOnlyList<InsertionCandidate> rankedCandidates,
        int maximumCandidates,
        CandidateRetentionStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(rankedCandidates);

        if (maximumCandidates < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCandidates));
        }

        if (!Enum.IsDefined(strategy))
        {
            throw new ArgumentOutOfRangeException(nameof(strategy));
        }

        if (rankedCandidates.Count == 0)
        {
            throw new ArgumentException(
                "A per-vehicle portfolio requires one no-op candidate.",
                nameof(rankedCandidates));
        }

        var ranked = rankedCandidates as InsertionCandidate[]
            ?? rankedCandidates.ToArray();
        RequireRankOrder(ranked);

        // The published WP1–WP6 path is intentionally a fast legacy cap.  It
        // already receives physically/schedule-validated candidates from the
        // generator, and must not pay the portfolio proof scans on every
        // rolling epoch.  The additional shape/no-op relationship proof is
        // required only when the new strategy can rely on it.
        if (strategy == CandidateRetentionStrategy.LegacyAcceptedCountCostSlack)
        {
            return RetainLegacy(ranked, maximumCandidates);
        }

        var vehicleId = ranked[0].VehicleId;

        if (ranked.Any(candidate => candidate.VehicleId != vehicleId)
            || ranked.Any(candidate => string.IsNullOrEmpty(candidate.CandidateId))
            || ranked.Select(candidate => candidate.CandidateId)
                .Distinct(StringComparer.Ordinal).Count() != ranked.Length
            || ranked.Count(candidate => candidate.IsNoOp) != 1)
        {
            throw new ArgumentException(
                "A portfolio requires one vehicle, globally unique non-empty " +
                "candidate IDs and exactly one no-op.",
                nameof(rankedCandidates));
        }

        foreach (var candidate in ranked)
        {
            ValidateCandidateShape(candidate, ranked);
        }

        var declaredNoOp = ranked.Single(candidate => candidate.IsNoOp);

        if (declaredNoOp.NewRequestIds.Count != 0)
        {
            throw new ArgumentException(
                "The no-op candidate cannot introduce a request.",
                nameof(rankedCandidates));
        }

        if (ranked.Length <= maximumCandidates)
        {
            return new CandidateRetentionResult(ranked, []);
        }

        var slotCount = maximumCandidates - 1;
        var retained = RetainStabilityPortfolio(
            ranked,
            declaredNoOp,
            slotCount);

        retained.Add(declaredNoOp);
        var retainedIds = retained
            .Select(candidate => candidate.CandidateId)
            .ToHashSet(StringComparer.Ordinal);
        var omitted = ranked
            .Where(candidate => !retainedIds.Contains(candidate.CandidateId))
            .ToArray();
        return new CandidateRetentionResult(retained.AsReadOnly(), omitted);
    }

    /// <summary>
    /// Fails closed when a caller claims a ranked pool that is not in
    /// <see cref="Rank"/> order. Verification is one linear comparison sweep, so
    /// skipping the redundant sort never costs the invariant.
    /// </summary>
    private static void RequireRankOrder(
        IReadOnlyList<InsertionCandidate> rankedCandidates)
    {
        for (var index = 1; index < rankedCandidates.Count; index++)
        {
            if (RankComparer.Instance.Compare(
                    rankedCandidates[index - 1],
                    rankedCandidates[index]) > 0)
            {
                throw new ArgumentException(
                    "A pre-ranked portfolio pool must already be in retainer " +
                    "rank order.",
                    nameof(rankedCandidates));
            }
        }
    }

    private static CandidateRetentionResult RetainLegacy(
        IReadOnlyList<InsertionCandidate> ranked,
        int maximumCandidates)
    {
        if (ranked.Count <= maximumCandidates)
        {
            return new CandidateRetentionResult(ranked, []);
        }

        // The legacy path deliberately keeps its historical failure behavior: an
        // absent or duplicated no-op throws from Single rather than producing a
        // typed portfolio message. Changing it would change published WP1–WP6
        // semantics for a case the generator cannot produce.
        var noOp = ranked.Single(candidate => candidate.IsNoOp);
        var retained = ranked
            .Where(candidate => !candidate.IsNoOp)
            .Take(maximumCandidates - 1)
            .Append(noOp)
            .ToArray();
        var retainedIds = retained
            .Select(candidate => candidate.CandidateId)
            .ToHashSet(StringComparer.Ordinal);
        var omitted = ranked
            .Where(candidate => !retainedIds.Contains(candidate.CandidateId))
            .ToArray();
        return new CandidateRetentionResult(retained, omitted);
    }

    private static void ValidateCandidateShape(
        InsertionCandidate candidate,
        IReadOnlyCollection<InsertionCandidate> candidates)
    {
        if (candidate.NewRequestIds is null
            || candidate.Route is null
            || candidate.Schedule is null
            || candidate.Schedule.Stops is null)
        {
            throw new ArgumentException(
                "A portfolio candidate requires route, schedule and service-set data.",
                nameof(candidates));
        }

        if (candidate.NewRequestIds.Distinct().Count()
            != candidate.NewRequestIds.Count)
        {
            throw new ArgumentException(
                "A candidate service set cannot contain duplicate request IDs.",
                nameof(candidates));
        }

        var routeStops = candidate.Route.RemainingStops.ToArray();

        if (candidate.Schedule.Stops.Count != routeStops.Length
            || !candidate.Schedule.Stops.Select(stop => stop.StopId)
                .SequenceEqual(routeStops.Select(stop => stop.StopId)))
        {
            throw new ArgumentException(
                "A candidate schedule must bind every remaining route stop in order.",
                nameof(candidates));
        }

        foreach (var requestId in candidate.NewRequestIds)
        {
            var requestStops = routeStops
                .Where(stop => stop.RequestId == requestId)
                .ToArray();

            if (requestStops.Count(stop => stop.Kind == RouteStopKind.Pickup) != 1
                || requestStops.Count(stop => stop.Kind == RouteStopKind.DropOff) != 1
                || requestStops.Length != 2)
            {
                throw new ArgumentException(
                    "Every request in a candidate service set requires exactly " +
                    "one remaining pickup and one remaining drop-off.",
                    nameof(candidates));
            }
        }
    }

    private static void ValidateRelationToNoOp(
        IReadOnlyCollection<InsertionCandidate> candidates,
        InsertionCandidate noOp,
        IReadOnlyCollection<InsertionCandidate> argument)
    {
        var baselineStops = noOp.Route.RemainingStops.ToArray();
        var baselineById = baselineStops.ToDictionary(stop => stop.StopId);
        var baselineStopIds = baselineById.Keys.ToHashSet();

        foreach (var candidate in candidates.Where(candidate => !candidate.IsNoOp))
        {
            var routeStops = candidate.Route.RemainingStops.ToArray();
            var routeById = routeStops.ToDictionary(stop => stop.StopId);

            // A retained variant may reorder the mutable suffix or add a
            // no-request waypoint, but it cannot silently delete or mutate an
            // incumbent stop. Stability and service-set dominance both use the
            // no-op route as their common incumbent baseline.
            if (baselineById.Any(
                    baseline => !routeById.TryGetValue(
                        baseline.Key,
                        out var routeStop)
                        || routeStop != baseline.Value))
            {
                throw new ArgumentException(
                    "A portfolio candidate must preserve every no-op remaining " +
                    "incumbent stop exactly.",
                    nameof(argument));
            }

            var declaredRequests = candidate.NewRequestIds.ToHashSet();
            var introducedRequests = routeStops
                .Where(stop => !baselineStopIds.Contains(stop.StopId))
                .Select(stop => stop.RequestId)
                .OfType<RequestId>()
                .ToHashSet();

            if (!introducedRequests.SetEquals(declaredRequests))
            {
                throw new ArgumentException(
                    "A candidate route's introduced request stops must exactly " +
                    "match its declared service set.",
                    nameof(argument));
            }
        }
    }

    public static IOrderedEnumerable<InsertionCandidate> Rank(
        IEnumerable<InsertionCandidate> candidates) => candidates
        .OrderByDescending(candidate => candidate.NewRequestIds.Count)
        .ThenBy(candidate => candidate.Schedule.OperationalCost)
        .ThenBy(
            candidate => candidate.CertifiedForwardSlackMilliseconds is null
                ? 0
                : 1)
        .ThenByDescending(
            candidate => candidate.CertifiedForwardSlackMilliseconds ?? 0)
        .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal);

    /// <summary>
    /// The pairwise form of <see cref="Rank"/>. It exists so a pre-ranked pool
    /// can be verified in linear time; the two must stay in exact agreement.
    /// </summary>
    private sealed class RankComparer : IComparer<InsertionCandidate>
    {
        public static RankComparer Instance { get; } = new();

        public int Compare(InsertionCandidate? left, InsertionCandidate? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return -1;
            }

            if (right is null)
            {
                return 1;
            }

            var value = right.NewRequestIds.Count.CompareTo(
                left.NewRequestIds.Count);
            value = value != 0
                ? value
                : left.Schedule.OperationalCost.CompareTo(
                    right.Schedule.OperationalCost);
            value = value != 0
                ? value
                : SlackClass(left).CompareTo(SlackClass(right));
            value = value != 0
                ? value
                : (right.CertifiedForwardSlackMilliseconds ?? 0).CompareTo(
                    left.CertifiedForwardSlackMilliseconds ?? 0);
            return value != 0
                ? value
                : StringComparer.Ordinal.Compare(
                    left.CandidateId,
                    right.CandidateId);
        }

        private static int SlackClass(InsertionCandidate candidate) =>
            candidate.CertifiedForwardSlackMilliseconds is null ? 0 : 1;
    }

    private static List<InsertionCandidate> RetainStabilityPortfolio(
        IReadOnlyList<InsertionCandidate> ranked,
        InsertionCandidate noOp,
        int slotCount)
    {
        // This relationship is a proof obligation of the service-set portfolio:
        // it is not part of the legacy cap, whose published timing and behavior
        // must remain untouched for WP1–WP6 configurations.
        ValidateRelationToNoOp(ranked, noOp, ranked);
        var retained = new List<InsertionCandidate>(slotCount);
        var retainedIds = new HashSet<string>(StringComparer.Ordinal);

        if (slotCount == 0)
        {
            return retained;
        }

        var rankById = ranked
            .Select((candidate, index) => (candidate.CandidateId, index))
            .ToDictionary(
                value => value.CandidateId,
                value => value.index,
                StringComparer.Ordinal);
        var stabilityBaseline = StabilityBaseline.Create(noOp);
        var stabilityById = ranked
            .Where(candidate => !candidate.IsNoOp)
            .ToDictionary(
                candidate => candidate.CandidateId,
                candidate => StabilityProfile.Create(
                    stabilityBaseline,
                    candidate),
                StringComparer.Ordinal);

        foreach (var cardinalityTier in ranked
                     .Where(candidate => !candidate.IsNoOp)
                     .GroupBy(candidate => candidate.NewRequestIds.Count)
                     .OrderByDescending(group => group.Key))
        {
            var serviceSets = cardinalityTier
                .GroupBy(candidate => CreateServiceSetKey(candidate.NewRequestIds))
                .Select(group => group.OrderBy(
                        candidate => rankById[candidate.CandidateId])
                    .ToArray())
                .OrderBy(group => rankById[group[0].CandidateId])
                .ToArray();

            // Phase A preserves the cheapest legacy representative of every
            // service set before spending slots on duplicate route variants.
            foreach (var serviceSet in serviceSets)
            {
                AddIfRoom(retained, retainedIds, serviceSet[0], slotCount);
            }

            // Phase B reserves the most stable route variant for each set.
            // If it is also the cost anchor this consumes no additional slot.
            foreach (var serviceSet in serviceSets)
            {
                var stabilityAnchor = serviceSet
                    .OrderBy(
                        candidate => stabilityById[candidate.CandidateId],
                        StabilityProfileComparer.Instance)
                    .ThenBy(candidate => rankById[candidate.CandidateId])
                    .First();
                AddIfRoom(retained, retainedIds, stabilityAnchor, slotCount);
            }

            // Phase C retains the old ranking for all still-unused slots.
            foreach (var candidate in cardinalityTier.OrderBy(
                         candidate => rankById[candidate.CandidateId]))
            {
                AddIfRoom(retained, retainedIds, candidate, slotCount);
            }

            if (retained.Count == slotCount)
            {
                break;
            }
        }

        return retained;
    }

    private static void AddIfRoom(
        ICollection<InsertionCandidate> retained,
        ISet<string> retainedIds,
        InsertionCandidate candidate,
        int slotCount)
    {
        if (retained.Count >= slotCount
            || !retainedIds.Add(candidate.CandidateId))
        {
            return;
        }

        retained.Add(candidate);
    }

    private static string CreateServiceSetKey(
        IEnumerable<RequestId> requestIds) => string.Concat(
        requestIds
            .Select(requestId => requestId.Value)
            .Order(StringComparer.Ordinal)
            .Select(value => $"{value.Length}:{value};"));

    private sealed record StabilityProfile(
        int NegativeStableIncumbentPrefixLength,
        long InsertedStopsBeforeIncumbentPickups,
        long MaximumIncumbentServiceShiftMilliseconds,
        long TotalIncumbentServiceShiftMilliseconds,
        long ShiftedIncumbentStopCount,
        long InsertedStopsBeforeIncumbentStops)
    {
        public static StabilityProfile Create(
            StabilityBaseline baseline,
            InsertionCandidate candidate)
        {
            var candidateStops = candidate.Route.RemainingStops.ToArray();
            var stablePrefixLength = 0;

            while (stablePrefixLength < baseline.IncumbentStops.Count
                   && stablePrefixLength < candidateStops.Length
                   && baseline.IncumbentStops[stablePrefixLength].StopId
                       == candidateStops[stablePrefixLength].StopId)
            {
                stablePrefixLength++;
            }

            var insertedBefore = 0L;
            var insertedBeforePickups = 0L;
            var insertedBeforeStops = 0L;

            foreach (var stop in candidateStops)
            {
                if (!baseline.IncumbentStopIds.Contains(stop.StopId))
                {
                    insertedBefore = SaturatingAdd(insertedBefore, 1);
                    continue;
                }

                insertedBeforeStops = SaturatingAdd(
                    insertedBeforeStops,
                    insertedBefore);

                if (stop.Kind == RouteStopKind.Pickup)
                {
                    insertedBeforePickups = SaturatingAdd(
                        insertedBeforePickups,
                        insertedBefore);
                }
            }

            var candidateTimes = candidate.Schedule.Stops.ToDictionary(
                stop => stop.StopId,
                stop => stop.ServiceStartTime.Milliseconds);
            var maximumShift = 0L;
            var totalShift = 0L;
            var shiftedStopCount = 0L;

            foreach (var stop in baseline.IncumbentStops)
            {
                if (!baseline.ServiceStartByStopId.TryGetValue(
                        stop.StopId,
                        out var before)
                    || !candidateTimes.TryGetValue(stop.StopId, out var after))
                {
                    return Worst;
                }

                var shift = before >= after ? before - after : after - before;
                maximumShift = Math.Max(maximumShift, shift);
                totalShift = SaturatingAdd(totalShift, shift);

                if (shift > 0)
                {
                    shiftedStopCount = SaturatingAdd(shiftedStopCount, 1);
                }
            }

            return new StabilityProfile(
                -stablePrefixLength,
                insertedBeforePickups,
                maximumShift,
                totalShift,
                shiftedStopCount,
                insertedBeforeStops);
        }

        private static StabilityProfile Worst { get; } = new(
            0,
            long.MaxValue,
            long.MaxValue,
            long.MaxValue,
            long.MaxValue,
            long.MaxValue);

        private static long SaturatingAdd(long left, long right) =>
            left > long.MaxValue - right ? long.MaxValue : left + right;
    }

    private sealed record StabilityBaseline(
        IReadOnlyList<RouteStop> IncumbentStops,
        IReadOnlySet<StopId> IncumbentStopIds,
        IReadOnlyDictionary<StopId, long> ServiceStartByStopId)
    {
        public static StabilityBaseline Create(InsertionCandidate noOp)
        {
            var stops = noOp.Route.RemainingStops.ToArray();
            return new StabilityBaseline(
                stops,
                stops.Select(stop => stop.StopId).ToHashSet(),
                noOp.Schedule.Stops.ToDictionary(
                    stop => stop.StopId,
                    stop => stop.ServiceStartTime.Milliseconds));
        }
    }

    private sealed class StabilityProfileComparer
        : IComparer<StabilityProfile>
    {
        public static StabilityProfileComparer Instance { get; } = new();

        public int Compare(StabilityProfile? left, StabilityProfile? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return -1;
            }

            if (right is null)
            {
                return 1;
            }

            var value = left.NegativeStableIncumbentPrefixLength.CompareTo(
                right.NegativeStableIncumbentPrefixLength);
            value = value != 0
                ? value
                : left.InsertedStopsBeforeIncumbentPickups.CompareTo(
                    right.InsertedStopsBeforeIncumbentPickups);
            value = value != 0
                ? value
                : left.MaximumIncumbentServiceShiftMilliseconds.CompareTo(
                    right.MaximumIncumbentServiceShiftMilliseconds);
            value = value != 0
                ? value
                : left.TotalIncumbentServiceShiftMilliseconds.CompareTo(
                    right.TotalIncumbentServiceShiftMilliseconds);
            value = value != 0
                ? value
                : left.ShiftedIncumbentStopCount.CompareTo(
                    right.ShiftedIncumbentStopCount);
            return value != 0
                ? value
                : left.InsertedStopsBeforeIncumbentStops.CompareTo(
                    right.InsertedStopsBeforeIncumbentStops);
        }
    }
}
