using System.Numerics;
using RideBound.Application.State;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Validation;
using RideBound.Domain.Vehicles;

namespace RideBound.Algorithms.Candidates;

public sealed partial class InsertionCandidateGenerator
{
    private readonly PhysicalPlanValidator _validator;
    private readonly ForwardSlackProfileCache _slackCache;
    private readonly CandidatePortfolioRetainer _portfolioRetainer;
    private readonly WaitingRiderRelocationBuilder _relocationBuilder;

    public InsertionCandidateGenerator(
        PhysicalPlanValidator? validator = null,
        CandidateScheduleEvaluator? scheduleEvaluator = null,
        ForwardSlackProfileCache? slackCache = null,
        CandidatePortfolioRetainer? portfolioRetainer = null,
        WaitingRiderRelocationBuilder? relocationBuilder = null)
    {
        _validator = validator ?? new PhysicalPlanValidator();
        _slackCache = slackCache ?? new ForwardSlackProfileCache(
            new ForwardSlackProfileBuilder(scheduleEvaluator));
        _portfolioRetainer = portfolioRetainer
            ?? new CandidatePortfolioRetainer();
        _relocationBuilder = relocationBuilder
            ?? new WaitingRiderRelocationBuilder();
    }

    public CandidateGenerationResult Generate(
        OnlineState state,
        CandidateGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(options);

        if (state.TravelTimes is null)
        {
            return CandidateGenerationResult.Failure(
                new CandidateGenerationWitness(
                    CandidateGenerationFailureCodes.TravelSnapshotRequired,
                    "Candidate generation requires a travel snapshot.",
                    Dimension: "travelTimes"));
        }

        var pendingRequests = state.Run.Requests.Values
            .Where(value => value.Lifecycle == RequestLifecycle.Pending)
            .OrderBy(value => value.LatestPickup.Milliseconds)
            .ThenBy(value => value.ArrivalTime.Milliseconds)
            .ThenBy(value => value.Id.Value, StringComparer.Ordinal)
            .ToArray();

        var requests = pendingRequests
            .Take(options.MaximumNewRequestsPerVehicle)
            .ToArray();
        var omittedRequests = pendingRequests
            .Skip(options.MaximumNewRequestsPerVehicle)
            .ToArray();
        var vehicleSets = new List<VehicleCandidateSet>();
        var omissions = new List<CandidateOmissionWitness>();
        var breaches = new List<ExogenousServiceQualityBreach>();

        if (omittedRequests.Length > 0)
        {
            omissions.Add(
                new CandidateOmissionWitness(
                    CandidateGenerationFailureCodes.RequestBoundOmission,
                    omittedRequests.Length,
                    CandidateIdentity.CreateOmissionDigest(
                        omittedRequests.Select(request => request.Id.Value)),
                    RequestIds: Array.AsReadOnly(
                        omittedRequests.Select(request => request.Id).ToArray())));
        }

        foreach (var vehicle in state.Run.Vehicles.Values.OrderBy(
                     value => value.Id.Value,
                     StringComparer.Ordinal))
        {
            var generated = GenerateForVehicle(
                state,
                vehicle,
                requests,
                options,
                omittedRequests.Length > 0);

            if (generated.Witness is not null)
            {
                return CandidateGenerationResult.Failure(generated.Witness);
            }

            vehicleSets.Add(generated.Candidates!);
            omissions.AddRange(generated.Omissions);
            breaches.AddRange(generated.Breaches);
        }

        IReadOnlyList<RelocationCandidateLoss>? relocationLosses = null;

        // G2 (WaitingRiderRelocationGenerator.cs) runs only when configured, after the ordinary
        // search, and its candidates are appended after retention, so the ordinary sets are
        // unchanged when it is off.
        if (options.WaitingRiderRelocation is { } relocationOptions)
        {
            var relocated = GenerateRelocations(state, requests, options, relocationOptions);

            for (var index = 0; index < vehicleSets.Count; index++)
            {
                vehicleSets[index] = relocated.MergeInto(vehicleSets[index]);
            }

            omissions.AddRange(relocated.Omissions);
            relocationLosses = relocated.Losses;
        }

        return CandidateGenerationResult.Success(
            vehicleSets.AsReadOnly(),
            new CandidateGenerationDiagnostics(
                pendingRequests.Length,
                requests.Length,
                omittedRequests.Length,
                vehicleSets.Select(set => set.Loss!).ToArray(),
                omissions.AsReadOnly(),
                breaches.AsReadOnly(),
                relocationLosses));
    }

    private VehicleGenerationResult GenerateForVehicle(
        OnlineState state,
        VehicleState vehicle,
        IReadOnlyList<RideRequest> pendingRequests,
        CandidateGenerationOptions options,
        bool requestsWereOmitted)
    {
        var feasibleById =
            new Dictionary<string, InsertionCandidate>(StringComparer.Ordinal);
        var prunedById =
            new Dictionary<string, CandidatePruneWitness>(StringComparer.Ordinal);

        // Probe the unchanged active route first. Whatever it can no
        // longer honour on the two service-quality dimensions is exogenous by
        // construction. The resulting relaxation is applied only to the safety
        // no-op; every changed candidate stays strictly contractual.
        var probe = _validator.ProbeServiceQuality(
            state.Run,
            vehicle.Id,
            state.TravelTimes!,
            state.Run.SimulationTime);

        if (!probe.IsSuccess)
        {
            return VehicleGenerationResult.Failure(
                new CandidateGenerationWitness(
                    CandidateGenerationFailureCodes.ActiveRouteInfeasible,
                    "The active route violates a structural physical constraint: " +
                    $"{probe.Witness!.Code}: {probe.Witness.Message}" +
                    FormatPhysicalDetails(probe.Witness),
                    vehicle.Id,
                    probe.Witness.RequestId,
                    probe.Witness.Dimension ?? probe.Witness.Code));
        }

        var serviceQuality = probe.Allowance;
        var breaches = serviceQuality.Breaches
            .Select(
                value => new ExogenousServiceQualityBreach(
                    vehicle.Id,
                    value.RequestId,
                    value.Code,
                    value.Dimension,
                    value.ContractualMilliseconds,
                    value.ExogenousMilliseconds))
            .ToArray();

        var noOpFailure = EvaluateNoOp(
            state,
            vehicle,
            serviceQuality,
            feasibleById,
            prunedById);

        if (noOpFailure is not null)
        {
            return VehicleGenerationResult.Failure(noOpFailure);
        }

        // The relief exists to keep the safety no-op alive, and the
        // no-op is never handed to the simulator: the adapter only submits a
        // plan when the route actually changed. Every *changed* candidate is
        // therefore validated against the published contractual bounds, with no
        // relief at all. Anything looser would let RideBound propose a plan that
        // FleetPy's own `VehiclePlan.is_feasible` rejects (a job stops on such a
        // rejection, even when a pickup window is missed by a few milliseconds).
        var exploration = ExploreBestFirst(
            state,
            vehicle,
            pendingRequests,
            options,
            ServiceQualityAllowance.Strict,
            feasibleById,
            prunedById);

        var beforeCap = feasibleById.Values.ToArray();

        // Rank once and hand the ordered result to the retainer. Ranking is the
        // retainer's own precondition, so re-sorting the same pool inside it was
        // pure duplicated O(n log n) on every vehicle of every epoch.
        var rankedCandidates = CandidatePortfolioRetainer.Rank(beforeCap).ToArray();
        var ranked = rankedCandidates.ToList();
        var omittedByCap = Array.Empty<InsertionCandidate>();

        if (ranked.Count > options.MaximumCandidatesPerVehicle)
        {
            var retention = _portfolioRetainer.RetainRanked(
                rankedCandidates,
                options.MaximumCandidatesPerVehicle);
            omittedByCap = retention.Omitted.ToArray();
            ranked = retention.Retained.ToList();
        }

        var omissions = BuildOmissions(exploration, omittedByCap);

        var loss = new VehicleCandidateLoss(
            exploration.WorkUnits,
            exploration.EvaluatedCandidatePathCount,
            beforeCap.Length,
            ranked.Count,
            prunedById.Count,
            exploration.OmittedCandidatePathCount,
            omittedByCap.Length,
            exploration.OmittedCandidatePathCount > 0,
            omittedByCap.Length > 0,
            exploration.OmissionCountWasSaturated);
        var wasTruncated = requestsWereOmitted
            || loss.WorkBudgetExhausted
            || loss.CandidateCapApplied;

        return VehicleGenerationResult.Success(
            new VehicleCandidateSet(
                vehicle.Id,
                ranked
                    .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
                    .ToArray(),
                prunedById.Values
                    .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
                    .ToArray(),
                wasTruncated,
                loss),
            omissions.AsReadOnly(),
            breaches);
    }

    /// <summary>
    /// Evaluates the unchanged active route as the safety no-op candidate, under the probed
    /// allowance. Returns the failure witness when the route cannot be kept, otherwise null.
    /// </summary>
    private CandidateGenerationWitness? EvaluateNoOp(
        OnlineState state,
        VehicleState vehicle,
        ServiceQualityAllowance serviceQuality,
        Dictionary<string, InsertionCandidate> feasibleById,
        Dictionary<string, CandidatePruneWitness> prunedById)
    {
        var noOpCandidateId = CandidateIdentity.Create(
            state,
            vehicle.Id,
            vehicle.Route,
            []);
        EvaluateRoute(
            state,
            vehicle,
            vehicle.Route,
            [],
            isNoOp: true,
            serviceQuality,
            feasibleById,
            prunedById);

        if (!feasibleById.TryGetValue(noOpCandidateId, out var noOp)
            || !noOp.IsNoOp)
        {
            prunedById.TryGetValue(noOpCandidateId, out var prune);
            return new CandidateGenerationWitness(
                CandidateGenerationFailureCodes.ActiveRouteInfeasible,
                prune is null
                    ? "The active route could not be retained as the safety " +
                      "no-op candidate."
                    : "The active route could not be retained as the safety " +
                      $"no-op candidate: {prune.Code}: {prune.Message}" +
                      FormatPhysicalDetails(prune.PhysicalWitness),
                vehicle.Id,
                prune?.PhysicalWitness?.RequestId,
                prune?.PhysicalWitness?.Dimension ?? prune?.Code);
        }

        return null;
    }

    /// <summary>
    /// The omission witnesses of one vehicle: first the paths the work bound left unexplored,
    /// then the feasible candidates the cap dropped.
    /// </summary>
    private static List<CandidateOmissionWitness> BuildOmissions(
        ExplorationResult exploration,
        InsertionCandidate[] omittedByCap)
    {
        var omissions = new List<CandidateOmissionWitness>();

        if (exploration.OmittedCandidatePathCount > 0)
        {
            omissions.Add(
                new CandidateOmissionWitness(
                    CandidateGenerationFailureCodes.WorkBoundOmission,
                    exploration.OmittedCandidatePathCount,
                    CandidateIdentity.CreateOmissionDigest(
                        exploration.OmittedSubtreeIds),
                    CountWasSaturated: exploration.OmissionCountWasSaturated));
        }

        if (omittedByCap.Length > 0)
        {
            omissions.Add(
                new CandidateOmissionWitness(
                    CandidateGenerationFailureCodes.CandidateCapOmission,
                    omittedByCap.Length,
                    CandidateIdentity.CreateOmissionDigest(
                        omittedByCap.Select(candidate => candidate.CandidateId))));
        }

        return omissions;
    }

    private static string FormatPhysicalDetails(
        PhysicalViolationWitness? witness) =>
        witness is null
            ? string.Empty
            : $" [vehicleId={witness.VehicleId.Value};" +
              $"requestId={witness.RequestId?.Value ?? "<none>"};" +
              $"stopId={witness.StopId?.Value ?? "<none>"};" +
              $"dimension={witness.Dimension ?? "<none>"};" +
              $"expected={witness.Expected?.ToString() ?? "<none>"};" +
              $"actual={witness.Actual?.ToString() ?? "<none>"}]";

    private ExplorationResult ExploreBestFirst(
        OnlineState state,
        VehicleState vehicle,
        IReadOnlyList<RideRequest> requests,
        CandidateGenerationOptions options,
        ServiceQualityAllowance serviceQuality,
        IDictionary<string, InsertionCandidate> feasibleById,
        IDictionary<string, CandidatePruneWitness> prunedById,
        RelocationRoot? relocationRoot = null)
    {
        var frontier = new PriorityQueue<ExplorationNode, SearchPriority>(
            SearchPriorityComparer.Instance);
        // A G2 donor search has the single root D0 (the donor suffix without the moved
        // rider). Its root-only path is D0 itself, evaluated by the caller like the no-op.
        var root = CreateNode(
            requestIndex: 0,
            relocationRoot?.Suffix ?? vehicle.Route.MutableSuffix,
            []);
        var rootIsActiveRoute = relocationRoot is null;
        frontier.Enqueue(
            root,
            CreatePriority(state, vehicle, requests, serviceQuality, root, rootIsActiveRoute));
        var workUnits = 0L;
        var evaluatedPaths = 0L;

        while (workUnits < options.MaximumExplorationWorkUnits
            && frontier.TryDequeue(out var node, out _))
        {
            workUnits++;

            if (node.RequestIndex == requests.Count)
            {
                if (node.InsertedRequestIds.Count == 0)
                {
                    continue;
                }

                evaluatedPaths++;
                var routeResult = node.Project(vehicle);

                if (routeResult.IsSuccess)
                {
                    EvaluateRoute(
                        state,
                        vehicle,
                        routeResult.Value!,
                        node.InsertedRequestIds,
                        isNoOp: false,
                        serviceQuality,
                        feasibleById,
                        prunedById,
                        node.SlackLookup,
                        relocationRoot?.Relocation,
                        relocationRoot is null ? null : RelocationRole.Donor);
                }

                continue;
            }

            var children = Expand(node, requests[node.RequestIndex])
                .Select(
                    child => new PrioritizedNode(
                        child,
                        CreatePriority(
                            state,
                            vehicle,
                            requests,
                            serviceQuality,
                            child,
                            rootIsActiveRoute)))
                .ToList();
            children.Sort(
                (left, right) => SearchPriorityComparer.Instance.Compare(
                    left.Priority,
                    right.Priority));

            foreach (var prioritized in children)
            {
                frontier.Enqueue(prioritized.Node, prioritized.Priority);
            }
        }

        var omittedCount = BigInteger.Zero;
        var omittedSubtreeIds = new List<string>();

        // The combinatorial memo is keyed only by (suffixCount, remaining,
        // hasInserted); nothing in it depends on the individual frontier node.
        // Allocating it per node discarded one dictionary per unexpanded node.
        var subtreeMemo =
            new Dictionary<(int SuffixCount, int Remaining, bool Inserted), BigInteger>();

        while (frontier.TryDequeue(out var omitted, out _))
        {
            var subtreeCount = CountTerminalCandidatePaths(
                omitted,
                requests.Count,
                subtreeMemo);

            if (subtreeCount > 0)
            {
                omittedCount += subtreeCount;
                omittedSubtreeIds.Add(omitted.StableId);
            }
        }

        var saturated = omittedCount > DomainLimits.MaxCanonicalInteger;
        var canonicalOmitted = saturated
            ? DomainLimits.MaxCanonicalInteger
            : (long)omittedCount;

        return new ExplorationResult(
            workUnits,
            evaluatedPaths,
            canonicalOmitted,
            omittedSubtreeIds.AsReadOnly(),
            saturated);
    }

    private static IEnumerable<ExplorationNode> Expand(
        ExplorationNode node,
        RideRequest request)
    {
        yield return CreateNode(
            node.RequestIndex + 1,
            node.Suffix,
            node.InsertedRequestIds);

        // An inserted pickup or drop stop has no service duration (dwell).
        var pickup = new RouteStop(
            CandidateIdentity.CreateStopId(request.Id, RouteStopKind.Pickup),
            request.OriginNodeId,
            RouteStopKind.Pickup,
            request.Id,
            new Duration(0));
        var dropOff = new RouteStop(
            CandidateIdentity.CreateStopId(request.Id, RouteStopKind.DropOff),
            request.DestinationNodeId,
            RouteStopKind.DropOff,
            request.Id,
            new Duration(0));

        var baseStops = node.Suffix;

        // Every child of this expansion serves exactly the same request set, so
        // the inserted-request list is built once and shared as an immutable
        // snapshot instead of being rebuilt per child.
        var insertedBuffer = new RequestId[node.InsertedRequestIds.Count + 1];

        for (var index = 0; index < node.InsertedRequestIds.Count; index++)
        {
            insertedBuffer[index] = node.InsertedRequestIds[index];
        }

        insertedBuffer[^1] = request.Id;
        var inserted = Array.AsReadOnly(insertedBuffer);

        for (var pickupIndex = 0; pickupIndex <= baseStops.Count; pickupIndex++)
        {
            for (var dropIndex = pickupIndex + 1;
                 dropIndex <= baseStops.Count + 1;
                 dropIndex++)
            {
                // Write the child suffix straight into its final array. The old
                // form built an intermediate list per pickup slot and copied it
                // again per drop slot, so every child paid three O(k) copies.
                var withPair = new RouteStop[baseStops.Count + 2];
                var source = 0;

                for (var index = 0; index < withPair.Length; index++)
                {
                    withPair[index] = index == pickupIndex
                        ? pickup
                        : index == dropIndex
                            ? dropOff
                            : baseStops[source++];
                }

                yield return CreateOwnedNode(
                    node.RequestIndex + 1,
                    withPair,
                    inserted);
            }
        }
    }

    private SearchPriority CreatePriority(
        OnlineState state,
        VehicleState vehicle,
        IReadOnlyList<RideRequest> requests,
        ServiceQualityAllowance serviceQuality,
        ExplorationNode node,
        bool rootIsActiveRoute)
    {
        var remaining = requests.Count - node.RequestIndex;
        var potentialAccepted = node.InsertedRequestIds.Count + remaining;
        var mandatoryService = node.MandatoryServiceMilliseconds;

        // The ordinary root is the current route, so it is ranked on that route
        // directly; a G2 donor root carries a different mutable suffix, so it
        // (like every expanded node) is projected on its own candidate suffix.
        var route = node.RequestIndex == 0
                    && rootIsActiveRoute
            ? vehicle.Route
            : node.Project(vehicle).Value;
        long? slack = 0;

        if (route is not null)
        {
            var profile = _slackCache.GetOrBuild(
                state,
                vehicle,
                route,
                state.TravelTimes!,
                state.Run.SimulationTime,
                serviceQuality);
            node.SetSlackLookup(profile);

            if (profile.Result.IsSuccess)
            {
                slack = profile.Result.Profile!
                    .CertifiedDelayAtRouteStartMilliseconds;
            }
        }

        return new SearchPriority(
            -potentialAccepted,
            mandatoryService,
            slack is null ? 0 : 1,
            -(slack ?? 0),
            node);
    }

    private static ExplorationNode CreateNode(
        int requestIndex,
        IEnumerable<RouteStop> suffix,
        IEnumerable<RequestId> insertedRequestIds) =>
        CreateOwnedNode(
            requestIndex,
            suffix.ToArray(),
            Array.AsReadOnly(insertedRequestIds.ToArray()));

    /// <summary>
    /// Builds a node from a suffix array the caller has just allocated and will
    /// not touch again, and from an already immutable inserted-request snapshot.
    /// This avoids re-copying both sequences on the expansion hot path.
    /// </summary>
    private static ExplorationNode CreateOwnedNode(
        int requestIndex,
        RouteStop[] ownedSuffix,
        IReadOnlyList<RequestId> insertedRequestIds) =>
        new(
            requestIndex,
            Array.AsReadOnly(ownedSuffix),
            insertedRequestIds);

    private static BigInteger CountTerminalCandidatePaths(
        ExplorationNode node,
        int requestCount,
        IDictionary<(int SuffixCount, int Remaining, bool Inserted), BigInteger> memo) =>
        Count(
            node.Suffix.Count,
            requestCount - node.RequestIndex,
            node.InsertedRequestIds.Count > 0,
            memo);

    private static BigInteger Count(
        int suffixCount,
        int remaining,
        bool hasInserted,
        IDictionary<(int, int, bool), BigInteger> memo)
    {
        // Every remaining request can independently be skipped or inserted at
        // least one way. At 54 remaining requests, 2^54 - 1 already exceeds the
        // canonical reporting range, so recursion can truthfully saturate early.
        if (remaining >= 54)
        {
            return new BigInteger(DomainLimits.MaxCanonicalInteger) + 1;
        }

        var key = (suffixCount, remaining, hasInserted);

        if (memo.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (remaining == 0)
        {
            return hasInserted ? BigInteger.One : BigInteger.Zero;
        }

        var insertionPositions = new BigInteger(suffixCount + 1)
            * (suffixCount + 2)
            / 2;
        var result = Count(suffixCount, remaining - 1, hasInserted, memo)
            + insertionPositions
            * Count(suffixCount + 2, remaining - 1, true, memo);
        memo[key] = result;
        return result;
    }

    private static long SaturatingAdd(long left, long right) =>
        left > DomainLimits.MaxCanonicalInteger - right
            ? DomainLimits.MaxCanonicalInteger
            : left + right;

    private void EvaluateRoute(
        OnlineState state,
        VehicleState vehicle,
        RoutePlan route,
        IReadOnlyList<RequestId> insertedRequestIds,
        bool isNoOp,
        ServiceQualityAllowance serviceQuality,
        IDictionary<string, InsertionCandidate> feasibleById,
        IDictionary<string, CandidatePruneWitness> prunedById,
        ForwardSlackCacheLookup? prefetchedProfile = null,
        WaitingRiderRelocation? relocation = null,
        RelocationRole? relocationRole = null)
    {
        var orderedRequests = insertedRequestIds
            .OrderBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();
        var candidateId = CandidateIdentity.Create(
            state,
            vehicle.Id,
            route,
            orderedRequests);

        if (feasibleById.ContainsKey(candidateId)
            || prunedById.ContainsKey(candidateId))
        {
            return;
        }

        var validation = _validator.Validate(
            new PhysicalValidationContext(
                state.Run,
                vehicle.Id,
                route,
                state.TravelTimes!,
                state.Run.SimulationTime,
                serviceQuality,
                relocation));

        if (!validation.IsFeasible)
        {
            prunedById.TryAdd(
                candidateId,
                new CandidatePruneWitness(
                    candidateId,
                    vehicle.Id,
                    orderedRequests,
                    validation.Witness!.Code,
                    validation.Witness.Message,
                    validation.Witness));
            return;
        }

        if (prefetchedProfile is not null
            && !prefetchedProfile.Key.Matches(
                state,
                vehicle,
                route,
                state.TravelTimes!,
                state.Run.SimulationTime,
                serviceQuality))
        {
            throw new InvalidOperationException(
                "A prefetched slack profile must match the exact candidate state and route.");
        }

        var profileLookup = prefetchedProfile ?? _slackCache.GetOrBuild(
                state,
                vehicle,
                route,
                state.TravelTimes!,
                state.Run.SimulationTime,
                serviceQuality);

        if (!profileLookup.Result.IsSuccess)
        {
            prunedById.TryAdd(
                candidateId,
                new CandidatePruneWitness(
                    candidateId,
                    vehicle.Id,
                    orderedRequests,
                    profileLookup.Result.Code!,
                    profileLookup.Result.Message!));
            return;
        }

        var profile = profileLookup.Result.Profile!;
        feasibleById.TryAdd(
            candidateId,
            new InsertionCandidate(
                candidateId,
                vehicle.Id,
                route,
                orderedRequests,
                profile.Schedule,
                isNoOp,
                profile.CertifiedDelayAtRouteStartMilliseconds,
                relocation?.RequestId,
                relocationRole));
    }

    /// <summary>
    /// A search node. <see cref="StableId"/> and the projected route are both
    /// derived, deterministic and expensive, so they are computed at most once
    /// and only if something actually asks for them. The frontier holds far
    /// more nodes than the work budget can expand, and the priority comparison
    /// only reaches the identity tie-break when every earlier key ties.
    /// </summary>
    private sealed class ExplorationNode(
        int requestIndex,
        IReadOnlyList<RouteStop> suffix,
        IReadOnlyList<RequestId> insertedRequestIds)
    {
        private string? _stableId;
        private DomainResult<RoutePlan>? _projection;
        private long? _mandatoryServiceMilliseconds;

        public int RequestIndex { get; } = requestIndex;

        public IReadOnlyList<RouteStop> Suffix { get; } = suffix;

        public IReadOnlyList<RequestId> InsertedRequestIds { get; } =
            insertedRequestIds;

        public ForwardSlackCacheLookup? SlackLookup { get; private set; }

        public long MandatoryServiceMilliseconds =>
            _mandatoryServiceMilliseconds ??= Suffix.Aggregate(
                0L,
                (total, stop) => SaturatingAdd(
                    total,
                    stop.ServiceDuration.Milliseconds));

        /// <remarks>
        /// The digest still ends with the token "no-repair", which marked a node outside the removed
        /// waiting-incumbent repair search (B4). Node ids order the frontier on ties and name the
        /// omitted subtrees in the omission digests, so the token keeps both byte-identical.
        /// </remarks>
        public string StableId => _stableId ??= $"{RequestIndex:D8}:" +
            CandidateIdentity.CreateSearchNodeDigest(
                Suffix.Select(stop => stop.StopId.Value)
                    .Concat(InsertedRequestIds.Select(request => request.Value))
                    .Append("no-repair"),
                Suffix.Count + InsertedRequestIds.Count + 1);

        public DomainResult<RoutePlan> Project(VehicleState vehicle) =>
            _projection ??= vehicle.Route.ReplaceMutableSuffix(Suffix);

        public void SetSlackLookup(ForwardSlackCacheLookup lookup)
        {
            ArgumentNullException.ThrowIfNull(lookup);

            if (SlackLookup is not null && SlackLookup != lookup)
            {
                throw new InvalidOperationException(
                    "A search node cannot be ranked against two slack profiles.");
            }

            SlackLookup = lookup;
        }
    }

    private sealed record PrioritizedNode(
        ExplorationNode Node,
        SearchPriority Priority);

    /// <summary>
    /// The tie-break key carries the node rather than its identity string so
    /// that the identity digest is only computed when the comparison actually
    /// reaches it. The resulting order is unchanged.
    /// </summary>
    private readonly record struct SearchPriority(
        int NegativePotentialAcceptedCount,
        long MandatoryServiceLowerBound,
        int SlackBoundClass,
        long NegativeCertifiedSlack,
        ExplorationNode Node);

    private sealed class SearchPriorityComparer : IComparer<SearchPriority>
    {
        public static SearchPriorityComparer Instance { get; } = new();

        public int Compare(SearchPriority left, SearchPriority right)
        {
            var comparison = left.NegativePotentialAcceptedCount.CompareTo(
                right.NegativePotentialAcceptedCount);
            comparison = comparison != 0
                ? comparison
                : left.MandatoryServiceLowerBound.CompareTo(
                    right.MandatoryServiceLowerBound);
            comparison = comparison != 0
                ? comparison
                : left.SlackBoundClass.CompareTo(right.SlackBoundClass);
            comparison = comparison != 0
                ? comparison
                : left.NegativeCertifiedSlack.CompareTo(
                    right.NegativeCertifiedSlack);
            return comparison != 0
                ? comparison
                : StringComparer.Ordinal.Compare(
                    left.Node.StableId,
                    right.Node.StableId);
        }
    }

    private sealed record ExplorationResult(
        long WorkUnits,
        long EvaluatedCandidatePathCount,
        long OmittedCandidatePathCount,
        IReadOnlyList<string> OmittedSubtreeIds,
        bool OmissionCountWasSaturated);

    private sealed record VehicleGenerationResult(
        VehicleCandidateSet? Candidates,
        IReadOnlyList<CandidateOmissionWitness> Omissions,
        CandidateGenerationWitness? Witness,
        IReadOnlyList<ExogenousServiceQualityBreach> Breaches)
    {
        public static VehicleGenerationResult Success(
            VehicleCandidateSet candidates,
            IReadOnlyList<CandidateOmissionWitness> omissions,
            IReadOnlyList<ExogenousServiceQualityBreach> breaches) =>
            new(candidates, omissions, null, breaches);

        public static VehicleGenerationResult Failure(
            CandidateGenerationWitness witness) =>
            new(null, [], witness, []);
    }
}
