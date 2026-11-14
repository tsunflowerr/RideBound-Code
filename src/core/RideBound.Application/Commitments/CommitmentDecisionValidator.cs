using System.Security.Cryptography;
using System.Text;
using RideBound.Application.Promises;
using RideBound.Application.Scheduling;
using RideBound.Application.State;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Runs;
using RideBound.Domain.Validation;
using RideBound.Domain.Vehicles;

namespace RideBound.Application.Commitments;

public interface ICommitmentPolicyProvider
{
    bool TryGetPolicy(string policyId, out CommitmentPolicy policy);
}

public sealed class CommitmentPolicyCatalog : ICommitmentPolicyProvider
{
    private readonly IReadOnlyDictionary<string, CommitmentPolicy> _policies;

    public CommitmentPolicyCatalog(IEnumerable<CommitmentPolicy> policies)
    {
        ArgumentNullException.ThrowIfNull(policies);
        var materialized = policies.ToArray();

        if (materialized.Select(value => value.PolicyId)
            .Distinct(StringComparer.Ordinal).Count() != materialized.Length)
        {
            throw new ArgumentException(
                "Commitment policy identifiers must be unique.",
                nameof(policies));
        }

        _policies = materialized.ToDictionary(
            value => value.PolicyId,
            StringComparer.Ordinal);
    }

    public bool TryGetPolicy(string policyId, out CommitmentPolicy policy) =>
        _policies.TryGetValue(policyId, out policy!);
}

public sealed record CommitmentValidationContext(
    OnlineState BeforeEventState,
    OnlineState ReducedState,
    OnlineState CandidateState,
    ICommitmentPolicyProvider Policies,
    IStopDistanceLookup StopDistances,
    string PublicationScope,
    long SourceEventSequence,
    string RevisionReasonCode = "ONLINE_REPLAN",
    VehicleId? ScopedVehicleId = null);

public enum CommitmentValidationStage
{
    State,
    Physical,
    Projection,
    Lock,
    Budget,
    Ledger,
}

public sealed record CommitmentValidationWitness(
    CommitmentValidationStage Stage,
    string Code,
    string Message,
    VehicleId? VehicleId = null,
    RequestId? RequestId = null,
    string? Dimension = null,
    string? Rule = null,
    long? Limit = null,
    long? Before = null,
    long? Delta = null,
    long? After = null);

public sealed record PromisePublication(
    string PublicationId,
    CommitmentLedgerEntry Entry);

public sealed record CommitmentDecisionValidationResult
{
    private CommitmentDecisionValidationResult(
        OnlineState? validatedState,
        IReadOnlyList<PromisePublication> publications,
        IReadOnlyList<CommitmentValidationWitness> witnesses)
    {
        ValidatedState = validatedState;
        Publications = publications;
        Witnesses = witnesses;
    }

    public bool IsValid => ValidatedState is not null && Witnesses.Count == 0;

    public OnlineState? ValidatedState { get; }

    public IReadOnlyList<PromisePublication> Publications { get; }

    public IReadOnlyList<CommitmentValidationWitness> Witnesses { get; }

    public static CommitmentDecisionValidationResult Valid(
        OnlineState state,
        IReadOnlyList<PromisePublication> publications) =>
        new(state, publications, []);

    public static CommitmentDecisionValidationResult Invalid(
        IEnumerable<CommitmentValidationWitness> witnesses) =>
        new(null, [], Array.AsReadOnly(witnesses.ToArray()));
}

public sealed class CommitmentDecisionValidator
{
    private readonly PhysicalPlanValidator _physicalValidator;
    private readonly RouteScheduleProjector _scheduleProjector;
    private readonly PromiseProjector _promiseProjector;
    private readonly PromiseDeltaCalculator _deltaCalculator;
    private readonly CommitmentLockEvaluator _lockEvaluator;
    private readonly CommitmentBudgetEvaluator _budgetEvaluator;

    public CommitmentDecisionValidator(
        PhysicalPlanValidator? physicalValidator = null,
        RouteScheduleProjector? scheduleProjector = null,
        PromiseProjector? promiseProjector = null,
        PromiseDeltaCalculator? deltaCalculator = null,
        CommitmentLockEvaluator? lockEvaluator = null,
        CommitmentBudgetEvaluator? budgetEvaluator = null)
    {
        _physicalValidator = physicalValidator ?? new PhysicalPlanValidator();
        _scheduleProjector = scheduleProjector ?? new RouteScheduleProjector();
        _promiseProjector = promiseProjector ?? new PromiseProjector();
        _deltaCalculator = deltaCalculator ?? new PromiseDeltaCalculator();
        _lockEvaluator = lockEvaluator ?? new CommitmentLockEvaluator();
        _budgetEvaluator = budgetEvaluator ?? new CommitmentBudgetEvaluator();
    }

    public CommitmentDecisionValidationResult Validate(
        CommitmentValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.BeforeEventState);
        ArgumentNullException.ThrowIfNull(context.ReducedState);
        ArgumentNullException.ThrowIfNull(context.CandidateState);
        ArgumentNullException.ThrowIfNull(context.Policies);
        ArgumentNullException.ThrowIfNull(context.StopDistances);

        var structural = ValidateStateBoundary(context, out var relocation);

        if (structural is not null)
        {
            return CommitmentDecisionValidationResult.Invalid([structural]);
        }

        var physicalWitnesses = ValidatePhysicalPlans(context, relocation);

        if (physicalWitnesses.Count != 0)
        {
            return CommitmentDecisionValidationResult.Invalid(physicalWitnesses);
        }

        var schedules = ProjectCandidateSchedules(context);

        if (!schedules.IsSuccess)
        {
            return CommitmentDecisionValidationResult.Invalid(
                [schedules.Witness!]);
        }

        var ledger = context.ReducedState.Commitments;
        var publications = new List<PromisePublication>();

        foreach (var request in context.CandidateState.Run.Requests.Values
                     .Where(
                         value => value.IsAcceptedActive
                             && (context.ScopedVehicleId is null
                                 || value.AssignedVehicleId
                                     == context.ScopedVehicleId))
                     .OrderBy(value => value.Id.Value, StringComparer.Ordinal))
        {
            if (!context.Policies.TryGetPolicy(
                    request.CommitmentPolicyId,
                    out var policy)
                || !string.Equals(
                    policy.PolicyId,
                    request.CommitmentPolicyId,
                    StringComparison.Ordinal))
            {
                return Invalid(
                    CommitmentValidationStage.State,
                    "COMMITMENT_POLICY_NOT_FOUND",
                    "The active request has no exact commitment policy.",
                    requestId: request.Id,
                    dimension: "commitmentPolicyId");
            }

            var candidateProjection = Project(
                context.CandidateState.Run,
                request,
                schedules.Values![request.AssignedVehicleId!.Value],
                previous: ledger.Histories.TryGetValue(
                    request.Id,
                    out var priorHistory)
                    ? priorHistory.Current.PublishedPromise.Projection
                    : null);

            if (!candidateProjection.IsSuccess)
            {
                return ProjectionFailure(candidateProjection.Failure!, request);
            }

            if (priorHistory is null)
            {
                if (request.Lifecycle == RequestLifecycle.Accepted)
                {
                    // The assignment is still a provisional booking offer. It is
                    // physically validated but deliberately has no rider promise.
                    continue;
                }

                // The first promise opens when the booking is confirmed: the rider was
                // accepted before this batch and is now waiting or onboard.
                var opensAtBooking = request.Lifecycle is RequestLifecycle.WaitingPickup
                        or RequestLifecycle.Onboard
                    && context.BeforeEventState.Run.Requests.TryGetValue(
                        request.Id,
                        out var beforeBooking)
                    && beforeBooking.Lifecycle == RequestLifecycle.Accepted
                    && context.ReducedState.Run.Requests.TryGetValue(
                        request.Id,
                        out var afterBooking)
                    && afterBooking.Lifecycle is RequestLifecycle.WaitingPickup
                        or RequestLifecycle.Onboard;

                if (!opensAtBooking)
                {
                    return Invalid(
                        CommitmentValidationStage.Ledger,
                        CommitmentFailureCodes.LedgerConflict,
                        "Initial promise trigger does not match the request lifecycle transition.",
                        requestId: request.Id,
                        dimension: "promiseVersion");
                }

                var publicationId = CreatePublicationId(
                    context.PublicationScope,
                    request.Id,
                    new PromiseVersion(1));
                var opened = ledger.OpenInitial(
                    publicationId,
                    candidateProjection.Value!,
                    context.CandidateState.Run.AppliedEpoch,
                    context.CandidateState.Run.SimulationTime,
                    "INITIAL_BOOKING_CONFIRMATION",
                    context.SourceEventSequence);

                if (!opened.IsSuccess)
                {
                    return LedgerFailure(opened.Failure!, request.Id);
                }

                ledger = opened.Ledger!;
                var entry = ledger.Histories[request.Id].Current;
                publications.Add(new PromisePublication(publicationId, entry));
                continue;
            }

            if (!context.ReducedState.Run.Requests.TryGetValue(
                    request.Id,
                    out var oldRequest)
                || !oldRequest.IsAcceptedActive
                || oldRequest.AssignedVehicleId is not VehicleId oldVehicleId
                || !context.ReducedState.Run.Vehicles.TryGetValue(
                    oldVehicleId,
                    out var oldVehicle))
            {
                return Invalid(
                    CommitmentValidationStage.State,
                    CommitmentFailureCodes.LedgerConflict,
                    "An active ledger history has no matching reduced assignment.",
                    requestId: request.Id,
                    dimension: "assignedVehicleId");
            }

            var oldSchedule = _scheduleProjector.Project(
                context.ReducedState.Run,
                oldVehicle,
                oldVehicle.Route,
                context.ReducedState.TravelTimes!,
                context.ReducedState.Run.SimulationTime);

            if (!oldSchedule.IsSuccess)
            {
                return Invalid(
                    CommitmentValidationStage.Projection,
                    SchedulingFailureCodes.ScheduleProjectionFailed,
                    oldSchedule.Failure!.Message,
                    oldVehicle.Id,
                    request.Id,
                    "exogenousSchedule");
            }

            var exogenousProjection = _promiseProjector.Project(
                context.ReducedState.Run,
                oldVehicle,
                oldVehicle.Route,
                oldSchedule.Schedule!,
                request.Id,
                priorHistory.Current.PublishedPromise.Projection);

            if (!exogenousProjection.IsSuccess)
            {
                return ProjectionFailure(exogenousProjection.Failure!, request);
            }

            // Only a deadline or a slack-proportional budget reads the initial promise, so
            // without one this path is unchanged. The ledger can only be opened by an initial
            // promise, so a different first entry is a broken invariant, not a rejectable candidate.
            PromiseProjection? anchor = null;

            if (policy.ReadsInitialPromise)
            {
                var initialEntry = priorHistory.Entries[0];

                if (initialEntry.Kind != CommitmentLedgerEntryKind.InitialPromise)
                {
                    throw new InvalidOperationException(
                        "The first ledger entry of a rider must be the initial promise.");
                }

                anchor = initialEntry.PublishedPromise.Projection;
            }

            // ADR-089 GĐ4: under Shadow Control the rider's class, frozen at its first promise,
            // scales its slack-proportional budget or deadline.
            var shadowAllocation = policy.DropEtaShadowAllocation
                ? ledger.ShadowAllocationOf(request.Id)
                : null;

            // Per-rider drop_eta_total_ms limit of a slack-proportional budget; null otherwise.
            var dropEtaTotalLimit = policy.DropEtaTotalLimitFor(
                anchor,
                request.MaxRideTime,
                shadowAllocation);

            var lockWitnesses = _lockEvaluator.Evaluate(
                request,
                priorHistory.Current.PublishedPromise,
                exogenousProjection.Value!,
                candidateProjection.Value!,
                policy,
                anchor,
                shadowAllocation);

            if (lockWitnesses.Count != 0)
            {
                var lockFailures = lockWitnesses.Select(
                    value => value.Rule switch
                    {
                        CommitmentLockEvaluator.DeadlineCapRule => new CommitmentValidationWitness(
                            CommitmentValidationStage.Lock,
                            CommitmentFailureCodes.DeadlineExceeded,
                            "The candidate delays the drop ETA beyond the initial-promise deadline.",
                            request.AssignedVehicleId,
                            value.RequestId,
                            value.Dimension,
                            value.Rule),
                        _ => new CommitmentValidationWitness(
                            CommitmentValidationStage.Lock,
                            CommitmentFailureCodes.PhaseLock,
                            "The candidate changes a phase-locked promise field.",
                            request.AssignedVehicleId,
                            value.RequestId,
                            value.Dimension,
                            value.Rule),
                    });

                return CommitmentDecisionValidationResult.Invalid(lockFailures);
            }

            var calculated = _deltaCalculator.Calculate(
                priorHistory.Current.PublishedPromise,
                exogenousProjection.Value!,
                candidateProjection.Value!,
                policy.MaterialRevisionRule,
                context.StopDistances);

            if (!calculated.IsSuccess)
            {
                return Invalid(
                    CommitmentValidationStage.Projection,
                    calculated.Failure!.Code,
                    calculated.Failure.Message,
                    request.AssignedVehicleId,
                    request.Id,
                    calculated.Failure.Dimension);
            }

            // The budget is charged by the decision-induced delta.
            var budget = _budgetEvaluator.Evaluate(
                request.Id,
                request.Lifecycle,
                priorHistory.Current.BudgetAfter,
                calculated.Deltas!.DecisionInduced,
                policy,
                dropEtaTotalLimit);

            if (!budget.IsAllowed)
            {
                var budgetFailures = budget.Witnesses.Select(
                    value => new CommitmentValidationWitness(
                        CommitmentValidationStage.Budget,
                        CommitmentFailureCodes.BudgetExceeded,
                        "The candidate exceeds a hard commitment dimension.",
                        request.AssignedVehicleId,
                        value.RequestId,
                        value.Dimension,
                        Limit: value.Limit,
                        Before: value.Before,
                        Delta: value.Delta,
                        After: value.After));

                return CommitmentDecisionValidationResult.Invalid(budgetFailures);
            }

            var unchanged = calculated.Deltas.Exogenous == CommitmentVector.Zero
                && calculated.Deltas.DecisionInduced == CommitmentVector.Zero
                && calculated.Deltas.Visible == CommitmentVector.Zero;

            if (!unchanged)
            {
                var nextVersion = priorHistory.Current.PublishedPromise.Version.Next();
                var revisionId = CreatePublicationId(
                    context.PublicationScope,
                    request.Id,
                    nextVersion);
                var revised = ledger.AppendRevision(
                    revisionId,
                    request.Id,
                    priorHistory.Current.PublishedPromise.Version,
                    exogenousProjection.Value!,
                    candidateProjection.Value!,
                    calculated.Deltas,
                    context.CandidateState.Run.AppliedEpoch,
                    context.CandidateState.Run.SimulationTime,
                    context.RevisionReasonCode,
                    context.SourceEventSequence);

                if (!revised.IsSuccess)
                {
                    return LedgerFailure(revised.Failure!, request.Id);
                }

                ledger = revised.Ledger!;
                publications.Add(
                    new PromisePublication(
                        revisionId,
                        ledger.Histories[request.Id].Current));
            }
        }

        return CommitmentDecisionValidationResult.Valid(
            context.CandidateState with { Commitments = ledger },
            publications.AsReadOnly());
    }

    /// <param name="relocation">
    /// The decision's one waiting-rider relocation when the boundary holds and the rider's
    /// policy allows it; null when the decision moves nobody.
    /// </param>
    private static CommitmentValidationWitness? ValidateStateBoundary(
        CommitmentValidationContext context,
        out WaitingRiderRelocation? relocation)
    {
        relocation = null;
        WaitingRiderRelocation? found = null;

        if (context.SourceEventSequence is < 1 or > DomainLimits.MaxCanonicalInteger
            || context.BeforeEventState.Run.AppliedEpoch + 1
                != context.ReducedState.Run.AppliedEpoch
            || context.BeforeEventState.Run.SimulationTime.Milliseconds
                > context.ReducedState.Run.SimulationTime.Milliseconds
            || context.BeforeEventState.NextEventSequence
                > context.SourceEventSequence
            || context.ReducedState.NextEventSequence
                != context.SourceEventSequence + 1
            || context.ReducedState.NextEventSequence
                is < 1 or > DomainLimits.MaxCanonicalInteger
            || context.BeforeEventState.Run.Id != context.ReducedState.Run.Id
            || context.ReducedState.Run.Id != context.CandidateState.Run.Id
            || context.BeforeEventState.Run.ScenarioId
                != context.ReducedState.Run.ScenarioId
            || context.ReducedState.Run.ScenarioId
                != context.CandidateState.Run.ScenarioId
            || context.ReducedState.Run.AppliedEpoch
                != context.CandidateState.Run.AppliedEpoch
            || context.ReducedState.Run.SimulationTime
                != context.CandidateState.Run.SimulationTime
            || context.ReducedState.NextEventSequence
                != context.CandidateState.NextEventSequence
            || !string.Equals(
                context.ReducedState.ExpectedInitialTravelTimeSnapshotHash,
                context.CandidateState.ExpectedInitialTravelTimeSnapshotHash,
                StringComparison.Ordinal)
            || context.ReducedState.TravelTimes is null
            || context.CandidateState.TravelTimes != context.ReducedState.TravelTimes
            || !ReferenceEquals(
                context.BeforeEventState.Commitments,
                context.ReducedState.Commitments)
            || !ReferenceEquals(
                context.ReducedState.Commitments,
                context.CandidateState.Commitments)
            || !ReferenceEquals(
                context.ReducedState.Incidents,
                context.CandidateState.Incidents)
            || !RequestsMatchDecisionBoundary(
                context.ReducedState.Run,
                context.CandidateState.Run,
                out found)
            || found is not null && !IsPermittedRelocation(context, found)
            || !VehiclesMatchDecisionBoundary(
                context.ReducedState.Run,
                context.CandidateState.Run,
                found))
        {
            return new CommitmentValidationWitness(
                CommitmentValidationStage.State,
                "COMMITMENT_STATE_BOUNDARY_MISMATCH",
                "Candidate state may only decide pending requests and replace " +
                "vehicle mutable route suffixes at the shared epoch, travel, " +
                "incident, and commitment-ledger boundary.");
        }

        relocation = found;
        return null;
    }

    /// <summary>
    /// A relocation is part of the boundary only when the rider's policy enables it.
    /// </summary>
    private static bool IsPermittedRelocation(
        CommitmentValidationContext context,
        WaitingRiderRelocation relocation) =>
        context.CandidateState.Run.Requests.TryGetValue(relocation.RequestId, out var request)
        && context.Policies.TryGetPolicy(request.CommitmentPolicyId, out var policy)
        && policy.WaitingRiderRelocation;

    /// <param name="relocation">
    /// Set to the one non-pending request whose only change is a waiting rider's vehicle; a
    /// second such change, or any other change, breaks the boundary.
    /// </param>
    private static bool RequestsMatchDecisionBoundary(
        RideBoundRun reduced,
        RideBoundRun candidate,
        out WaitingRiderRelocation? relocation)
    {
        relocation = null;

        if (reduced.Requests.Count != candidate.Requests.Count)
        {
            return false;
        }

        foreach (var pair in reduced.Requests)
        {
            if (!candidate.Requests.TryGetValue(pair.Key, out var proposed))
            {
                return false;
            }

            var current = pair.Value;

            if (current.Lifecycle != RequestLifecycle.Pending)
            {
                if (current != proposed)
                {
                    if (relocation is not null
                        || WaitingRiderRelocation.FromChange(current, proposed) is not { } moved)
                    {
                        relocation = null;
                        return false;
                    }

                    relocation = moved;
                }

                continue;
            }

            if (!HasSameRequestDefinition(current, proposed)
                || proposed.ActualPickupTime is not null
                || proposed.Lifecycle switch
                {
                    RequestLifecycle.Pending =>
                        proposed.AssignedVehicleId is null,
                    RequestLifecycle.Accepted =>
                        proposed.AssignedVehicleId is not null,
                    RequestLifecycle.Rejected =>
                        proposed.AssignedVehicleId is null,
                    _ => false,
                } is false)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasSameRequestDefinition(
        RideRequest current,
        RideRequest proposed) =>
        current.Id == proposed.Id
        && current.ArrivalTime == proposed.ArrivalTime
        && current.OriginNodeId == proposed.OriginNodeId
        && current.DestinationNodeId == proposed.DestinationNodeId
        && current.EarliestPickup == proposed.EarliestPickup
        && current.LatestPickup == proposed.LatestPickup
        && current.MaxRideTime == proposed.MaxRideTime
        && current.PartySize == proposed.PartySize
        && string.Equals(
            current.ServiceClass,
            proposed.ServiceClass,
            StringComparison.Ordinal)
        && string.Equals(
            current.CommitmentPolicyId,
            proposed.CommitmentPolicyId,
            StringComparison.Ordinal);

    private static bool VehiclesMatchDecisionBoundary(
        RideBoundRun reduced,
        RideBoundRun candidate,
        WaitingRiderRelocation? relocation)
    {
        if (reduced.Vehicles.Count != candidate.Vehicles.Count)
        {
            return false;
        }

        foreach (var pair in reduced.Vehicles)
        {
            if (!candidate.Vehicles.TryGetValue(pair.Key, out var proposed))
            {
                return false;
            }

            var current = pair.Value;
            var newlyAssigned = candidate.Requests.Values
                .Where(
                    value => value.Lifecycle == RequestLifecycle.Accepted
                        && value.AssignedVehicleId == current.Id
                        && reduced.Requests[value.Id].Lifecycle
                            == RequestLifecycle.Pending)
                .Select(value => value.Id);
            var expectedAccepted = current.AcceptedRequestIds
                .Concat(newlyAssigned)
                .ToHashSet();

            // A relocated waiting rider leaves the donor's accepted set and joins the receiver's.
            if (relocation is not null && relocation.DonorVehicleId == current.Id)
            {
                expectedAccepted.Remove(relocation.RequestId);
            }
            else if (relocation is not null && relocation.ReceiverVehicleId == current.Id)
            {
                expectedAccepted.Add(relocation.RequestId);
            }
            var routeBoundary = current.Route.IsSemanticallyEqual(proposed.Route)
                || current.Route.HasExactFrozenPrefix(proposed.Route)
                    && proposed.Route.Version.Value
                        == current.Route.Version.Value + 1;
            var routeRequestsAreAssigned = proposed.Route.RemainingStops.All(
                stop => stop.RequestId is not RequestId requestId
                    || candidate.Requests.TryGetValue(requestId, out var request)
                        && request.IsAcceptedActive
                        && request.AssignedVehicleId == current.Id);

            if (current.Capacity != proposed.Capacity
                || current.OccupiedSeats != proposed.OccupiedSeats
                || current.Position != proposed.Position
                || current.LastObservedEpoch != proposed.LastObservedEpoch
                || !current.OnboardRequestIds.SetEquals(
                    proposed.OnboardRequestIds)
                || !expectedAccepted.SetEquals(proposed.AcceptedRequestIds)
                || !routeBoundary
                || !routeRequestsAreAssigned)
            {
                return false;
            }
        }

        return true;
    }

    private IReadOnlyList<CommitmentValidationWitness> ValidatePhysicalPlans(
        CommitmentValidationContext context,
        WaitingRiderRelocation? relocation)
    {
        var witnesses = new List<CommitmentValidationWitness>();

        foreach (var vehicle in context.CandidateState.Run.Vehicles.Values
                     .Where(
                         value => context.ScopedVehicleId is null
                             || value.Id == context.ScopedVehicleId)
                     .OrderBy(value => value.Id.Value, StringComparer.Ordinal))
        {
            var validation = _physicalValidator.ValidateWithExogenousRelief(
                context.ReducedState.Run,
                vehicle.Id,
                vehicle.Route,
                context.ReducedState.TravelTimes!,
                context.ReducedState.Run.SimulationTime,
                relocation);

            if (!validation.IsFeasible)
            {
                var value = validation.Witness!;
                witnesses.Add(
                    new CommitmentValidationWitness(
                        CommitmentValidationStage.Physical,
                        value.Code,
                        value.Message,
                        value.VehicleId,
                        value.RequestId,
                        value.Dimension,
                        Before: value.Expected,
                        After: value.Actual));
            }
        }

        return witnesses.AsReadOnly();
    }

    private ScheduleProjectionSet ProjectCandidateSchedules(
        CommitmentValidationContext context)
    {
        var values = new Dictionary<VehicleId, ProjectedRouteSchedule>();

        foreach (var vehicle in context.CandidateState.Run.Vehicles.Values
                     .Where(
                         value => context.ScopedVehicleId is null
                             || value.Id == context.ScopedVehicleId)
                     .OrderBy(value => value.Id.Value, StringComparer.Ordinal))
        {
            var projected = _scheduleProjector.Project(
                context.CandidateState.Run,
                vehicle,
                vehicle.Route,
                context.CandidateState.TravelTimes!,
                context.CandidateState.Run.SimulationTime);

            if (!projected.IsSuccess)
            {
                return ScheduleProjectionSet.Failure(
                    new CommitmentValidationWitness(
                        CommitmentValidationStage.Projection,
                        SchedulingFailureCodes.ScheduleProjectionFailed,
                        projected.Failure!.Message,
                        vehicle.Id,
                        Dimension: "candidateSchedule"));
            }

            values.Add(vehicle.Id, projected.Schedule!);
        }

        return ScheduleProjectionSet.Success(values);
    }

    private DomainResult<PromiseProjection> Project(
        RideBoundRun run,
        RideRequest request,
        ProjectedRouteSchedule schedule,
        PromiseProjection? previous)
    {
        var vehicle = run.Vehicles[request.AssignedVehicleId!.Value];
        return _promiseProjector.Project(
            run,
            vehicle,
            vehicle.Route,
            schedule,
            request.Id,
            previous);
    }

    private static CommitmentDecisionValidationResult ProjectionFailure(
        DomainFailure failure,
        RideRequest request) =>
        Invalid(
            CommitmentValidationStage.Projection,
            failure.Code,
            failure.Message,
            request.AssignedVehicleId,
            request.Id,
            failure.Dimension);

    private static CommitmentDecisionValidationResult LedgerFailure(
        DomainFailure failure,
        RequestId requestId) =>
        Invalid(
            CommitmentValidationStage.Ledger,
            failure.Code,
            failure.Message,
            requestId: requestId,
            dimension: failure.Dimension);

    private static CommitmentDecisionValidationResult Invalid(
        CommitmentValidationStage stage,
        string code,
        string message,
        VehicleId? vehicleId = null,
        RequestId? requestId = null,
        string? dimension = null) =>
        CommitmentDecisionValidationResult.Invalid(
            [
                new CommitmentValidationWitness(
                    stage,
                    code,
                    message,
                    vehicleId,
                    requestId,
                    dimension),
            ]);

    private static string CreatePublicationId(
        string scope,
        RequestId requestId,
        PromiseVersion version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        var material = Encoding.UTF8.GetBytes(
            $"RideBound.PromisePublication.v1\0{scope}\0{requestId.Value}\0{version.Value}");
        return $"promise-{Convert.ToHexStringLower(SHA256.HashData(material))}";
    }

    private sealed record ScheduleProjectionSet(
        IReadOnlyDictionary<VehicleId, ProjectedRouteSchedule>? Values,
        CommitmentValidationWitness? Witness)
    {
        public bool IsSuccess => Values is not null;

        public static ScheduleProjectionSet Success(
            IReadOnlyDictionary<VehicleId, ProjectedRouteSchedule> values) =>
            new(values, null);

        public static ScheduleProjectionSet Failure(
            CommitmentValidationWitness witness) =>
            new(null, witness);
    }
}
