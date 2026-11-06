using System.Numerics;
using RideBound.Application.State;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Validation;
using RideBound.Domain.Vehicles;

namespace RideBound.Algorithms.Candidates;

public sealed class InsertionCandidateGenerator
{
    private readonly PhysicalPlanValidator _validator;
    private readonly ForwardSlackProfileCache _slackCache;
    private readonly OriginHoldCandidateTransformer _originHoldTransformer;
    private readonly WaitingIncumbentRepairSeedBuilder _repairSeedBuilder;
    private readonly CandidatePortfolioRetainer _portfolioRetainer;
    private readonly WaitingRiderRelocationBuilder _relocationBuilder;

    public InsertionCandidateGenerator(
        PhysicalPlanValidator? validator = null,
        CandidateScheduleEvaluator? scheduleEvaluator = null,
        ForwardSlackProfileCache? slackCache = null,
        OriginHoldCandidateTransformer? originHoldTransformer = null,
        WaitingIncumbentRepairSeedBuilder? repairSeedBuilder = null,
        CandidatePortfolioRetainer? portfolioRetainer = null,
        WaitingRiderRelocationBuilder? relocationBuilder = null)
    {
        _validator = validator ?? new PhysicalPlanValidator();
        _slackCache = slackCache ?? new ForwardSlackProfileCache(
            new ForwardSlackProfileBuilder(scheduleEvaluator));
        _originHoldTransformer =
            originHoldTransformer ?? new OriginHoldCandidateTransformer();
        _repairSeedBuilder =
            repairSeedBuilder ?? new WaitingIncumbentRepairSeedBuilder();
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

        if (options.ExactSmallMode
            && pendingRequests.Length > options.MaximumNewRequestsPerVehicle)
        {
            return CandidateGenerationResult.Failure(
                new CandidateGenerationWitness(
                    CandidateGenerationFailureCodes.ExactSmallRequestBoundExceeded,
                    "Exact-small mode cannot omit pending requests above its " +
                    "published request bound.",
                    RequestId: pendingRequests[
                        options.MaximumNewRequestsPerVehicle].Id,
                    Dimension: "maximumNewRequestsPerVehicle"));
        }

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
                    "Pending requests beyond the deterministic " +
                    "(latestPickup, arrivalTime, requestId) bound were omitted.",
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

        // G2 runs only when configured, after the ordinary search, and its candidates are
        // appended after retention, so the ordinary sets are unchanged when it is off.
        if (options.WaitingRiderRelocation is { } relocationOptions)
        {
            var relocated = GenerateRelocations(state, requests, options, relocationOptions);

            if (relocated.Witness is not null)
            {
                return CandidateGenerationResult.Failure(relocated.Witness);
            }

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

    /// <summary>
    /// G2 (waiting-rider relocation), mirroring the emulator's reference (engine.js 463-497):
    /// for each donor in ordinal order and each eligible waiting rider w,
    /// <list type="number">
    /// <item>the donor-only route D0 (the suffix without w) must be strictly feasible;</item>
    /// <item>every receiver route (w's own stops inserted into another vehicle's suffix, no new
    /// request) is evaluated, and at least one must be feasible;</item>
    /// <item>donor routes taking the new requests are explored from D0 with the ordinary
    /// best-first search and work cap.</item>
    /// </list>
    /// A rider failing 1 or 2 yields no candidate. Every route is checked physically against the
    /// reduced state with the move declared; commitments are assessed later, as a pair.
    /// </summary>
    private RelocationGenerationResult GenerateRelocations(
        OnlineState state,
        IReadOnlyList<RideRequest> requests,
        CandidateGenerationOptions options,
        WaitingRiderRelocationGenerationOptions relocationOptions)
    {
        var result = new RelocationGenerationResult();

        foreach (var donor in state.Run.Vehicles.Values.OrderBy(
                     value => value.Id.Value,
                     StringComparer.Ordinal))
        {
            var eligibility = _relocationBuilder.Eligible(
                state,
                donor,
                relocationOptions.MaximumRidersPerVehicle);

            if (eligibility.EligibleRequestIds.Count == 0)
            {
                continue;
            }

            if (options.ExactSmallMode && eligibility.OmittedRequestIds.Count > 0)
            {
                return RelocationGenerationResult.Failure(
                    new CandidateGenerationWitness(
                        CandidateGenerationFailureCodes.ExactSmallRelocationRiderBoundExceeded,
                        "Exact-small G2 cannot omit an eligible waiting rider above its " +
                        "explicit rider cap.",
                        donor.Id,
                        eligibility.OmittedRequestIds[0],
                        "maximumRidersPerVehicle"));
            }

            if (eligibility.OmittedRequestIds.Count > 0)
            {
                result.Omissions.Add(
                    new CandidateOmissionWitness(
                        CandidateGenerationFailureCodes.RelocationRiderBoundOmission,
                        eligibility.OmittedRequestIds.Count,
                        CandidateIdentity.CreateOmissionDigest(
                            eligibility.OmittedRequestIds.Select(id => id.Value)),
                        "Eligible waiting riders beyond the G2 rider cap were not " +
                        "considered for relocation.",
                        donor.Id,
                        eligibility.OmittedRequestIds));
                result.Truncated.Add(donor.Id);
            }

            var work = 0L;
            var evaluatedReceivers = 0L;
            var feasibleReceivers = 0L;
            var evaluatedDonorPaths = 0L;
            var feasibleDonors = 0L;
            var omittedDonorPaths = 0L;
            var saturated = false;
            var withoutReceiver = 0L;
            var donorInfeasible = 0L;

            foreach (var seed in eligibility.Considered)
            {
                var targets = _relocationBuilder.Targets(
                    state,
                    donor,
                    seed.Request,
                    relocationOptions.MaximumTargetVehicles);

                if (targets.Count == 0)
                {
                    withoutReceiver++;
                    continue;
                }

                // The donor's own checks ignore the receiver, so the context names the first
                // target; each receiver route is checked with its own vehicle below.
                var donorContext = new WaitingRiderRelocation(
                    seed.Request.Id,
                    donor.Id,
                    targets[0].Id);
                var donorFeasible =
                    new Dictionary<string, InsertionCandidate>(StringComparer.Ordinal);
                var donorPruned =
                    new Dictionary<string, CandidatePruneWitness>(StringComparer.Ordinal);
                var donorOnly = donor.Route.ReplaceMutableSuffix(seed.DonorSuffix);
                work = SaturatingAdd(work, 1);
                evaluatedDonorPaths = SaturatingAdd(evaluatedDonorPaths, 1);

                if (donorOnly.IsSuccess)
                {
                    EvaluateRoute(
                        state,
                        donor,
                        donorOnly.Value!,
                        [],
                        isNoOp: false,
                        repairedIncumbentRequestId: null,
                        options,
                        ServiceQualityAllowance.Strict,
                        donorFeasible,
                        donorPruned,
                        relocation: donorContext,
                        relocationRole: RelocationRole.Donor);
                }

                if (donorFeasible.Count == 0)
                {
                    donorInfeasible++;
                    continue;
                }

                var receiverFeasible =
                    new Dictionary<string, InsertionCandidate>(StringComparer.Ordinal);
                var receiverPruned =
                    new Dictionary<string, CandidatePruneWitness>(StringComparer.Ordinal);

                foreach (var receiver in targets)
                {
                    var receiverContext = new WaitingRiderRelocation(
                        seed.Request.Id,
                        donor.Id,
                        receiver.Id);

                    foreach (var route in WaitingRiderRelocationBuilder.ReceiverRoutes(
                                 receiver,
                                 seed.Pickup,
                                 seed.DropOff))
                    {
                        work = SaturatingAdd(work, 1);
                        evaluatedReceivers = SaturatingAdd(evaluatedReceivers, 1);
                        EvaluateRoute(
                            state,
                            receiver,
                            route,
                            [],
                            isNoOp: false,
                            repairedIncumbentRequestId: null,
                            options,
                            ServiceQualityAllowance.Strict,
                            receiverFeasible,
                            receiverPruned,
                            relocation: receiverContext,
                            relocationRole: RelocationRole.Receiver);
                    }
                }

                if (receiverFeasible.Count == 0)
                {
                    withoutReceiver++;
                    continue;
                }

                var exploration = ExploreBestFirst(
                    state,
                    donor,
                    requests,
                    [],
                    options,
                    ServiceQualityAllowance.Strict,
                    donorFeasible,
                    donorPruned,
                    new RelocationRoot(donorContext, seed.DonorSuffix));

                if (exploration.Witness is not null)
                {
                    return RelocationGenerationResult.Failure(
                        exploration.Witness with
                        {
                            Code = CandidateGenerationFailureCodes
                                .ExactSmallRelocationWorkCapExceeded,
                            RequestId = seed.Request.Id,
                        });
                }

                work = SaturatingAdd(work, exploration.WorkUnits);
                evaluatedDonorPaths = SaturatingAdd(
                    evaluatedDonorPaths,
                    exploration.EvaluatedCandidatePathCount);
                omittedDonorPaths = SaturatingAdd(
                    omittedDonorPaths,
                    exploration.OmittedCandidatePathCount);
                saturated |= exploration.OmissionCountWasSaturated;

                if (exploration.OmittedCandidatePathCount > 0)
                {
                    result.Omissions.Add(
                        new CandidateOmissionWitness(
                            CandidateGenerationFailureCodes.RelocationWorkBoundOmission,
                            exploration.OmittedCandidatePathCount,
                            CandidateIdentity.CreateOmissionDigest(
                                exploration.OmittedSubtreeIds),
                            "Deterministic work cap left G2 donor exploration paths " +
                            "unexpanded; their feasibility is unknown.",
                            donor.Id,
                            [seed.Request.Id],
                            exploration.OmissionCountWasSaturated));
                    result.Truncated.Add(donor.Id);
                }

                feasibleDonors = SaturatingAdd(feasibleDonors, donorFeasible.Count);
                feasibleReceivers = SaturatingAdd(feasibleReceivers, receiverFeasible.Count);
                result.Add(donorFeasible.Values, donorPruned.Values);
                result.Add(receiverFeasible.Values, receiverPruned.Values);
            }

            result.Losses.Add(
                new RelocationCandidateLoss(
                    donor.Id,
                    eligibility.EligibleRequestIds.Count,
                    eligibility.Considered.Count,
                    eligibility.OmittedRequestIds.Count,
                    work,
                    evaluatedReceivers,
                    feasibleReceivers,
                    evaluatedDonorPaths,
                    feasibleDonors,
                    omittedDonorPaths,
                    saturated,
                    withoutReceiver,
                    donorInfeasible));
        }

        return result;
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
        var repair = options.MaximumRepairRequestsConsideredPerVehicle == 0
            ? new WaitingIncumbentRepairSeedResult([], [], [], [])
            : _repairSeedBuilder.Build(
                state,
                vehicle,
                options.MaximumRepairRequestsConsideredPerVehicle);

        if (options.ExactSmallMode && repair.OmittedRequestIds.Count > 0)
        {
            return VehicleGenerationResult.Failure(
                new CandidateGenerationWitness(
                    CandidateGenerationFailureCodes
                        .ExactSmallRepairRequestBoundExceeded,
                    "Exact-small B4 repair cannot omit an eligible waiting " +
                    "incumbent above its explicit request cap.",
                    vehicle.Id,
                    repair.OmittedRequestIds[0],
                    "maximumRepairRequestsConsideredPerVehicle"));
        }

        // ADR-045. Probe the unchanged active route first. Whatever it can no
        // longer honour on the two service-quality dimensions is exogenous by
        // construction. The resulting relaxation is applied only to the safety
        // no-op; ADR-047 keeps every changed candidate strictly contractual.
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
            repairedIncumbentRequestId: null,
            options,
            serviceQuality,
            feasibleById,
            prunedById);

        if (!feasibleById.TryGetValue(noOpCandidateId, out var noOp)
            || !noOp.IsNoOp)
        {
            prunedById.TryGetValue(noOpCandidateId, out var prune);
            return VehicleGenerationResult.Failure(
                new CandidateGenerationWitness(
                    CandidateGenerationFailureCodes.ActiveRouteInfeasible,
                    prune is null
                        ? "The active route could not be retained as the safety " +
                          "no-op candidate."
                        : "The active route could not be retained as the safety " +
                          $"no-op candidate: {prune.Code}: {prune.Message}" +
                          FormatPhysicalDetails(prune.PhysicalWitness),
                    vehicle.Id,
                    prune?.PhysicalWitness?.RequestId,
                    prune?.PhysicalWitness?.Dimension ?? prune?.Code));
        }

        // ADR-047. The relief exists to keep the safety no-op alive, and the
        // no-op is never handed to the simulator: the adapter only submits a
        // plan when the route actually changed. Every *changed* candidate is
        // therefore validated against the published contractual bounds, with no
        // relief at all. Anything looser would let RideBound propose a plan that
        // FleetPy's own `VehiclePlan.is_feasible` rejects — which is exactly how
        // three Panel B jobs died, one of them by 13 ms on a pickup window.
        var exploration = ExploreBestFirst(
            state,
            vehicle,
            pendingRequests,
            repair.Seeds,
            options,
            ServiceQualityAllowance.Strict,
            feasibleById,
            prunedById);

        if (exploration.Witness is not null)
        {
            return VehicleGenerationResult.Failure(exploration.Witness);
        }

        var beforeCap = feasibleById.Values.ToArray();

        // Rank once and hand the ordered result to the retainer. Ranking is the
        // retainer's own precondition, so re-sorting the same pool inside it was
        // pure duplicated O(n log n) on every vehicle of every epoch.
        var rankedCandidates = CandidatePortfolioRetainer.Rank(beforeCap).ToArray();
        var ranked = rankedCandidates.ToList();
        var omittedByCap = Array.Empty<InsertionCandidate>();

        if (ranked.Count > options.MaximumCandidatesPerVehicle)
        {
            if (options.ExactSmallMode)
            {
                return VehicleGenerationResult.Failure(
                    new CandidateGenerationWitness(
                        CandidateGenerationFailureCodes
                            .ExactSmallCandidateCapExceeded,
                        "Exact-small candidate count exceeded the configured " +
                        "cap; no candidate was silently omitted.",
                        vehicle.Id,
                        Dimension: "maximumCandidatesPerVehicle"));
            }

            var retention = _portfolioRetainer.RetainRanked(
                rankedCandidates,
                options.MaximumCandidatesPerVehicle,
                options.RetentionStrategy);
            omittedByCap = retention.Omitted.ToArray();
            ranked = retention.Retained.ToList();
        }

        var omissions = new List<CandidateOmissionWitness>();

        if (repair.OmittedRequestIds.Count > 0)
        {
            omissions.Add(
                new CandidateOmissionWitness(
                    CandidateGenerationFailureCodes.RepairRequestBoundOmission,
                    repair.OmittedRequestIds.Count,
                    CandidateIdentity.CreateOmissionDigest(
                        repair.OmittedRequestIds.Select(id => id.Value)),
                    "Eligible same-vehicle waiting incumbents beyond the " +
                    "explicit B4 repair-request cap were omitted.",
                    vehicle.Id,
                    repair.OmittedRequestIds));
        }

        if (exploration.OmittedCandidatePathCount > 0)
        {
            omissions.Add(
                new CandidateOmissionWitness(
                    CandidateGenerationFailureCodes.WorkBoundOmission,
                    exploration.OmittedCandidatePathCount,
                    CandidateIdentity.CreateOmissionDigest(
                        exploration.OmittedSubtreeIds),
                    "Deterministic work cap left raw candidate exploration " +
                    "paths unexpanded; their feasibility is unknown.",
                    vehicle.Id,
                    CountWasSaturated: exploration.OmissionCountWasSaturated));
        }

        if (omittedByCap.Length > 0)
        {
            omissions.Add(
                new CandidateOmissionWitness(
                    CandidateGenerationFailureCodes.CandidateCapOmission,
                    omittedByCap.Length,
                    CandidateIdentity.CreateOmissionDigest(
                        omittedByCap.Select(candidate => candidate.CandidateId)),
                    "Feasible candidates were omitted by the deterministic " +
                    $"'{options.RetentionStrategy}' cap.",
                    vehicle.Id));
        }

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
            exploration.OmissionCountWasSaturated,
            repair.EligibleRequestIds.Count,
            repair.ConsideredRequestIds.Count,
            repair.OmittedRequestIds.Count,
            vehicle.Id);
        var wasTruncated = requestsWereOmitted
            || loss.WorkBudgetExhausted
            || loss.CandidateCapApplied
            || loss.OmittedRepairRequestCount > 0;

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
        IReadOnlyList<WaitingIncumbentRepairSeed> repairSeeds,
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
        var roots = relocationRoot is not null
            ? [CreateNode(requestIndex: 0, relocationRoot.Suffix, [], repairedIncumbentRequestId: null)]
            : new[]
            {
                CreateNode(
                    requestIndex: 0,
                    vehicle.Route.MutableSuffix,
                    [],
                    repairedIncumbentRequestId: null),
            }
            .Concat(
                repairSeeds.Select(
                    seed => CreateNode(
                        requestIndex: 0,
                        seed.Route.MutableSuffix,
                        [],
                        seed.RequestId)))
            .GroupBy(node => node.StableId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

        var rootIsActiveRoute = relocationRoot is null;

        foreach (var root in roots)
        {
            frontier.Enqueue(
                root,
                CreatePriority(state, vehicle, requests, serviceQuality, root, rootIsActiveRoute));
        }
        var workUnits = 0L;
        var evaluatedPaths = 0L;

        while (workUnits < options.MaximumExplorationWorkUnits
            && frontier.TryDequeue(out var node, out _))
        {
            workUnits++;

            if (node.RequestIndex == requests.Count)
            {
                if (node.InsertedRequestIds.Count == 0
                    && node.RepairedIncumbentRequestId is null)
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
                        node.RepairedIncumbentRequestId,
                        options,
                        serviceQuality,
                        feasibleById,
                        prunedById,
                        node.SlackLookup,
                        relocationRoot?.Relocation,
                        relocationRoot is null ? null : RelocationRole.Donor);
                }

                continue;
            }

            var children = Expand(node, requests[node.RequestIndex], options.StopServiceDuration)
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

        if (omittedCount > 0 && options.ExactSmallMode)
        {
            return ExplorationResult.Failure(
                new CandidateGenerationWitness(
                    CandidateGenerationFailureCodes.ExactSmallWorkCapExceeded,
                    "Exact-small exploration exceeded the deterministic work " +
                    "cap; no raw candidate path was silently omitted.",
                    vehicle.Id,
                    Dimension: "maximumExplorationWorkUnits"));
        }

        var saturated = omittedCount > DomainLimits.MaxCanonicalInteger;
        var canonicalOmitted = saturated
            ? DomainLimits.MaxCanonicalInteger
            : (long)omittedCount;

        return ExplorationResult.Success(
            workUnits,
            evaluatedPaths,
            canonicalOmitted,
            omittedSubtreeIds.AsReadOnly(),
            saturated);
    }

    private static IEnumerable<ExplorationNode> Expand(
        ExplorationNode node,
        RideRequest request,
        Duration serviceDuration)
    {
        yield return CreateNode(
            node.RequestIndex + 1,
            node.Suffix,
            node.InsertedRequestIds,
            node.RepairedIncumbentRequestId);

        // The dwell of an inserted stop is CandidateGenerationOptions.StopServiceDuration (0 by default).
        var pickup = new RouteStop(
            CandidateIdentity.CreateStopId(request.Id, RouteStopKind.Pickup),
            request.OriginNodeId,
            RouteStopKind.Pickup,
            request.Id,
            serviceDuration);
        var dropOff = new RouteStop(
            CandidateIdentity.CreateStopId(request.Id, RouteStopKind.DropOff),
            request.DestinationNodeId,
            RouteStopKind.DropOff,
            request.Id,
            serviceDuration);

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
                    inserted,
                    node.RepairedIncumbentRequestId);
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

        // The ordinary root is the current route, but a B4 repair root already
        // carries a different mutable suffix. Ranking that root by the current
        // route's slack would make the bounded best-first order blind to the
        // repair it is deciding whether to explore. Keep the no-repair fast
        // path, while every repair root (and every expanded node) is projected
        // on its own candidate suffix.
        var route = node.RequestIndex == 0
                    && node.RepairedIncumbentRequestId is null
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
        IEnumerable<RequestId> insertedRequestIds,
        RequestId? repairedIncumbentRequestId) =>
        CreateOwnedNode(
            requestIndex,
            suffix.ToArray(),
            Array.AsReadOnly(insertedRequestIds.ToArray()),
            repairedIncumbentRequestId);

    /// <summary>
    /// Builds a node from a suffix array the caller has just allocated and will
    /// not touch again, and from an already immutable inserted-request snapshot.
    /// This avoids re-copying both sequences on the expansion hot path.
    /// </summary>
    private static ExplorationNode CreateOwnedNode(
        int requestIndex,
        RouteStop[] ownedSuffix,
        IReadOnlyList<RequestId> insertedRequestIds,
        RequestId? repairedIncumbentRequestId) =>
        new(
            requestIndex,
            Array.AsReadOnly(ownedSuffix),
            insertedRequestIds,
            repairedIncumbentRequestId);

    private static BigInteger CountTerminalCandidatePaths(
        ExplorationNode node,
        int requestCount,
        IDictionary<(int SuffixCount, int Remaining, bool Inserted), BigInteger> memo) =>
        Count(
            node.Suffix.Count,
            requestCount - node.RequestIndex,
            node.InsertedRequestIds.Count > 0
                || node.RepairedIncumbentRequestId is not null,
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
        RequestId? repairedIncumbentRequestId,
        CandidateGenerationOptions options,
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

        var selectedRoute = route;
        var selectedCandidateId = candidateId;
        var selectedProfile = profileLookup.Result.Profile!;
        var selectedStrategy = CandidateScheduleStrategy.EarliestFeasible;
        var relocatedWait = 0L;

        if (!isNoOp
            && options.ScheduleStrategy
                == CandidateScheduleStrategy.OriginHoldRelocatedWait)
        {
            var transformed = _originHoldTransformer.Transform(
                vehicle,
                route,
                selectedProfile,
                candidateId);

            if (transformed.WasApplied)
            {
                var transformedId = CandidateIdentity.Create(
                    state,
                    vehicle.Id,
                    transformed.Route,
                    orderedRequests);
                var transformedValidation = _validator.Validate(
                    new PhysicalValidationContext(
                        state.Run,
                        vehicle.Id,
                        transformed.Route,
                        state.TravelTimes!,
                        state.Run.SimulationTime,
                        serviceQuality,
                        relocation));

                if (!transformedValidation.IsFeasible)
                {
                    prunedById.TryAdd(
                        transformedId,
                        new CandidatePruneWitness(
                            transformedId,
                            vehicle.Id,
                            orderedRequests,
                            transformedValidation.Witness!.Code,
                            transformedValidation.Witness.Message,
                            transformedValidation.Witness));
                    return;
                }

                var transformedProfile = _slackCache.GetOrBuild(
                    state,
                    vehicle,
                    transformed.Route,
                    state.TravelTimes!,
                    state.Run.SimulationTime,
                    serviceQuality);

                if (!transformedProfile.Result.IsSuccess
                    || !HasExactServiceEquivalence(
                        selectedProfile,
                        transformedProfile.Result.Profile!))
                {
                    prunedById.TryAdd(
                        transformedId,
                        new CandidatePruneWitness(
                            transformedId,
                            vehicle.Id,
                            orderedRequests,
                            CandidateGenerationFailureCodes
                                .ScheduleStrategyEquivalenceFailed,
                            "Origin-hold transformation did not preserve exact " +
                            "service/departure times and operational cost."));
                    return;
                }

                selectedRoute = transformed.Route;
                selectedCandidateId = transformedId;
                selectedProfile = transformedProfile.Result.Profile!;
                selectedStrategy =
                    CandidateScheduleStrategy.OriginHoldRelocatedWait;
                relocatedWait = transformed.RelocatedWaitMilliseconds;
            }
        }

        feasibleById.TryAdd(
            selectedCandidateId,
            new InsertionCandidate(
                selectedCandidateId,
                vehicle.Id,
                selectedRoute,
                orderedRequests,
                selectedProfile.Schedule,
                isNoOp,
                selectedStrategy,
                relocatedWait,
                selectedProfile.CertifiedDelayAtRouteStartMilliseconds,
                repairedIncumbentRequestId,
                relocation?.RequestId,
                relocationRole));
    }

    private static bool HasExactServiceEquivalence(
        ForwardSlackProfile earliest,
        ForwardSlackProfile originHold)
    {
        if (originHold.Stops.Count != earliest.Stops.Count + 1
            || originHold.Schedule.OperationalCost
                != earliest.Schedule.OperationalCost)
        {
            return false;
        }

        for (var index = 0; index < earliest.Stops.Count; index++)
        {
            var original = earliest.Stops[index];
            var transformed = originHold.Stops[index + 1];

            if (original.StopId != transformed.StopId
                || original.ServiceStartTime != transformed.ServiceStartTime
                || original.DepartureTime != transformed.DepartureTime
                || index > 0 && original.ArrivalTime != transformed.ArrivalTime)
            {
                return false;
            }
        }

        return true;
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
        IReadOnlyList<RequestId> insertedRequestIds,
        RequestId? repairedIncumbentRequestId)
    {
        private string? _stableId;
        private DomainResult<RoutePlan>? _projection;
        private long? _mandatoryServiceMilliseconds;

        public int RequestIndex { get; } = requestIndex;

        public IReadOnlyList<RouteStop> Suffix { get; } = suffix;

        public IReadOnlyList<RequestId> InsertedRequestIds { get; } =
            insertedRequestIds;

        public RequestId? RepairedIncumbentRequestId { get; } =
            repairedIncumbentRequestId;

        public ForwardSlackCacheLookup? SlackLookup { get; private set; }

        public long MandatoryServiceMilliseconds =>
            _mandatoryServiceMilliseconds ??= Suffix.Aggregate(
                0L,
                (total, stop) => SaturatingAdd(
                    total,
                    stop.ServiceDuration.Milliseconds));

        public string StableId => _stableId ??= $"{RequestIndex:D8}:" +
            CandidateIdentity.CreateSearchNodeDigest(
                Suffix.Select(stop => stop.StopId.Value)
                    .Concat(InsertedRequestIds.Select(request => request.Value))
                    .Append(RepairedIncumbentRequestId?.Value ?? "no-repair"),
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
        bool OmissionCountWasSaturated,
        CandidateGenerationWitness? Witness)
    {
        public static ExplorationResult Success(
            long workUnits,
            long evaluatedCandidatePathCount,
            long omittedCandidatePathCount,
            IReadOnlyList<string> omittedSubtreeIds,
            bool omissionCountWasSaturated) =>
            new(
                workUnits,
                evaluatedCandidatePathCount,
                omittedCandidatePathCount,
                omittedSubtreeIds,
                omissionCountWasSaturated,
                null);

        public static ExplorationResult Failure(
            CandidateGenerationWitness witness) =>
            new(0, 0, 0, [], false, witness);
    }

    /// <summary>The root of a G2 donor search: D0's suffix and the declared move.</summary>
    private sealed record RelocationRoot(
        WaitingRiderRelocation Relocation,
        IReadOnlyList<RouteStop> Suffix);

    /// <summary>
    /// G2 output, kept per vehicle until it is appended to the ordinary sets after retention.
    /// </summary>
    private sealed class RelocationGenerationResult
    {
        private readonly Dictionary<VehicleId, List<InsertionCandidate>> _candidates = [];
        private readonly Dictionary<VehicleId, List<CandidatePruneWitness>> _pruned = [];

        public CandidateGenerationWitness? Witness { get; private init; }

        public List<CandidateOmissionWitness> Omissions { get; } = [];

        public List<RelocationCandidateLoss> Losses { get; } = [];

        public HashSet<VehicleId> Truncated { get; } = [];

        public static RelocationGenerationResult Failure(CandidateGenerationWitness witness) =>
            new() { Witness = witness };

        public void Add(
            IEnumerable<InsertionCandidate> candidates,
            IEnumerable<CandidatePruneWitness> pruned)
        {
            foreach (var candidate in candidates)
            {
                (_candidates.TryGetValue(candidate.VehicleId, out var list)
                    ? list
                    : _candidates[candidate.VehicleId] = []).Add(candidate);
            }

            foreach (var witness in pruned)
            {
                (_pruned.TryGetValue(witness.VehicleId, out var list)
                    ? list
                    : _pruned[witness.VehicleId] = []).Add(witness);
            }
        }

        /// <summary>
        /// The set unchanged when G2 produced nothing for its vehicle; otherwise the union,
        /// still ordered by candidate id. G2 candidates are never capped (emulator-equal).
        /// </summary>
        public VehicleCandidateSet MergeInto(VehicleCandidateSet set)
        {
            var hasCandidates = _candidates.TryGetValue(set.VehicleId, out var candidates);
            var hasPruned = _pruned.TryGetValue(set.VehicleId, out var pruned);
            var truncated = Truncated.Contains(set.VehicleId);

            if (!hasCandidates && !hasPruned && !truncated)
            {
                return set;
            }

            return set with
            {
                Candidates = set.Candidates
                    .Concat(candidates ?? [])
                    .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
                    .ToArray(),
                PrunedCandidates = set.PrunedCandidates
                    .Concat(pruned ?? [])
                    .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
                    .ToArray(),
                WasTruncated = set.WasTruncated || truncated,
            };
        }
    }

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
