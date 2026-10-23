using System.Security.Cryptography;
using System.Text;
using RideBound.Application.Promises;
using RideBound.Application.Scheduling;
using RideBound.Application.State;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Incidents;
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
    VehicleId? ScopedVehicleId = null,
    InitialPromiseTrigger InitialPromiseTrigger =
        InitialPromiseTrigger.InitialAcceptance,
    bool CollectAllCommitmentWitnesses = false,
    IReadOnlySet<VehicleId>? ForcedReferenceVehicles = null,
    bool ForcedNoWorse = false);

public enum InitialPromiseTrigger
{
    InitialAcceptance,
    BookingConfirmation,
}

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

/// <summary>
/// One rider on a forced-reference vehicle whose kept route a commitment gate rejected in
/// this decision, with the sorted, distinct gate codes. <paramref name="NoWorse"/> is true when
/// the decision changed the vehicle's route under no-worse-than-reference recovery.
/// </summary>
public sealed record ForcedReferenceExemption(
    RequestId RequestId,
    VehicleId VehicleId,
    IReadOnlyList<string> WitnessCodes,
    bool NoWorse = false);

public sealed record CommitmentDecisionValidationResult
{
    private CommitmentDecisionValidationResult(
        OnlineState? validatedState,
        IReadOnlyList<PromisePublication> publications,
        IReadOnlyList<CommitmentValidationWitness> witnesses,
        IReadOnlyList<CommitmentBreachRecord> forcedBreaches,
        IReadOnlyList<ForcedReferenceExemption> forcedExemptions)
    {
        ValidatedState = validatedState;
        Publications = publications;
        Witnesses = witnesses;
        ForcedBreaches = forcedBreaches;
        ForcedExemptions = forcedExemptions;
    }

    public bool IsValid => ValidatedState is not null && Witnesses.Count == 0;

    public OnlineState? ValidatedState { get; }

    public IReadOnlyList<PromisePublication> Publications { get; }

    public IReadOnlyList<CommitmentValidationWitness> Witnesses { get; }

    /// <summary>
    /// Forced breaches newly recorded by this decision (kept-route ForcedReference, or
    /// ForcedNoWorse for a changed route under no-worse recovery); already appended to the
    /// validated state's incident ledger. A breach is an event: a rider whose promise
    /// is revised, or whose overrun is new, gets one; a later decision that changes
    /// nothing for the rider repeats the exemption but not the record. Always empty
    /// unless the context names forced-reference vehicles.
    /// </summary>
    public IReadOnlyList<CommitmentBreachRecord> ForcedBreaches { get; }

    /// <summary>
    /// Every rider whose gate-rejected kept route this decision accepted, whether or not a
    /// new breach was recorded; a decision with any exemption is not normal operation.
    /// </summary>
    public IReadOnlyList<ForcedReferenceExemption> ForcedExemptions { get; }

    public static CommitmentDecisionValidationResult Valid(
        OnlineState state,
        IReadOnlyList<PromisePublication> publications,
        IReadOnlyList<CommitmentBreachRecord>? forcedBreaches = null,
        IReadOnlyList<ForcedReferenceExemption>? forcedExemptions = null) =>
        new(state, publications, [], forcedBreaches ?? [], forcedExemptions ?? []);

    public static CommitmentDecisionValidationResult Invalid(
        IEnumerable<CommitmentValidationWitness> witnesses) =>
        new(null, [], Array.AsReadOnly(witnesses.ToArray()), [], []);
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

        var structural = ValidateStateBoundary(context);

        if (structural is not null)
        {
            return CommitmentDecisionValidationResult.Invalid([structural]);
        }

        var physicalWitnesses = ValidatePhysicalPlans(context);

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
        var incidents = context.CandidateState.Incidents;
        var forcedBreaches = new List<CommitmentBreachRecord>();
        var forcedExemptions = new List<ForcedReferenceExemption>();

        // RB-WP14-003. Fail-fast is correct on the hot path, but it makes the
        // recorded prune witness depend on request order: the first failing
        // request and, inside it, the first failing layer are the only ones ever
        // reported. When an evidence profile asks for the full set, the lock and
        // budget layers are evaluated for every request before the candidate is
        // rejected. Structural failures still stop immediately, because there is
        // nothing meaningful to keep scanning with.
        var collected = context.CollectAllCommitmentWitnesses
            ? new List<CommitmentValidationWitness>()
            : null;

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
                if (context.InitialPromiseTrigger
                        == InitialPromiseTrigger.BookingConfirmation
                    && request.Lifecycle == RequestLifecycle.Accepted)
                {
                    // The assignment is still a provisional booking offer. It is
                    // physically validated but deliberately has no rider promise.
                    continue;
                }

                var opensAtAcceptance = context.InitialPromiseTrigger
                        == InitialPromiseTrigger.InitialAcceptance
                    && context.ReducedState.Run.Requests.TryGetValue(
                        request.Id,
                        out var reducedRequest)
                    && reducedRequest.Lifecycle == RequestLifecycle.Pending;
                var opensAtBooking = context.InitialPromiseTrigger
                        == InitialPromiseTrigger.BookingConfirmation
                    && request.Lifecycle is RequestLifecycle.WaitingPickup
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

                if (!opensAtAcceptance && !opensAtBooking)
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
                    opensAtBooking
                        ? "INITIAL_BOOKING_CONFIRMATION"
                        : "INITIAL_ACCEPTANCE",
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

            // Per-rider drop_eta_total_ms limit of a slack-proportional budget; null otherwise.
            var dropEtaTotalLimit = policy.DropEtaTotalLimitFor(anchor, request.MaxRideTime);

            var lockWitnesses = _lockEvaluator.Evaluate(
                request,
                priorHistory.Current.PublishedPromise,
                exogenousProjection.Value!,
                candidateProjection.Value!,
                context.CandidateState.Run.SimulationTime,
                policy,
                anchor);

            // A vehicle named as forced-reference keeps its route although a gate rejects it:
            // its deadline and budget overruns become an exemption instead of a rejection. A
            // phase lock or the revision cap cannot fire on a kept route; if one did, the breach
            // factory rejects it.
            // Under no-worse recovery a changed route of that vehicle is exempted only for gates the
            // kept route also overruns, and only by no more than the kept route would.
            var keptRoute = IsForcedReference(context, request);
            var noWorseRoute = !keptRoute && IsForcedNoWorse(context, request);
            var forcedCodes = keptRoute || noWorseRoute
                ? new List<string>()
                : null;
            NoWorseReference? reference = noWorseRoute
                ? KeptRouteReference(
                    request,
                    priorHistory,
                    exogenousProjection.Value!,
                    context,
                    policy,
                    anchor,
                    dropEtaTotalLimit)
                : null;

            if (noWorseRoute && reference is null)
            {
                return Invalid(
                    CommitmentValidationStage.Projection,
                    CommitmentFailureCodes.LedgerConflict,
                    "The kept-route reference of a no-worse candidate could not be evaluated.",
                    request.AssignedVehicleId,
                    request.Id,
                    "forcedNoWorse");
            }

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
                        CommitmentLockEvaluator.DeadlineFloorRule => new CommitmentValidationWitness(
                            CommitmentValidationStage.Lock,
                            CommitmentFailureCodes.DeadlineExceeded,
                            "The candidate moves the drop ETA outside the initial-promise window.",
                            request.AssignedVehicleId,
                            value.RequestId,
                            value.Dimension,
                            value.Rule),
                        CommitmentLockEvaluator.RevisionCapRule => new CommitmentValidationWitness(
                            CommitmentValidationStage.Lock,
                            CommitmentFailureCodes.RevisionLimitExceeded,
                            "The candidate moves the drop ETA beyond the per-decision revision limit.",
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

                var exempted = forcedCodes is not null
                    && (!noWorseRoute
                        || lockWitnesses.All(
                            value => reference!.AllowsLock(value, candidateProjection.Value!)));

                if (exempted)
                {
                    forcedCodes!.AddRange(lockFailures.Select(value => value.Code));
                }
                else if (collected is null)
                {
                    return CommitmentDecisionValidationResult.Invalid(lockFailures);
                }
                else
                {
                    collected.AddRange(lockFailures);
                }
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

            var charged = policy.BudgetBasis == CommitmentBudgetBasis.DecisionInduced
                ? calculated.Deltas!.DecisionInduced
                : calculated.Deltas!.Visible;
            var budget = _budgetEvaluator.Evaluate(
                request.Id,
                request.Lifecycle,
                priorHistory.Current.BudgetAfter,
                charged,
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

                var exempted = forcedCodes is not null
                    && (!noWorseRoute
                        || budget.Witnesses.All(value => reference!.AllowsBudget(value)));

                if (exempted)
                {
                    forcedCodes!.AddRange(budgetFailures.Select(value => value.Code));
                }
                else if (collected is null)
                {
                    return CommitmentDecisionValidationResult.Invalid(budgetFailures);
                }
                else
                {
                    collected.AddRange(budgetFailures);
                }
            }

            if (collected is { Count: > 0 })
            {
                // This request already failed, so its revision must not be
                // appended. Keep scanning only to complete the witness set.
                continue;
            }

            var budgetBefore = priorHistory.Current.BudgetAfter;
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
                    policy.BudgetBasis,
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

            if (forcedCodes is { Count: > 0 })
            {
                var codes = forcedCodes
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                // A changed route whose projection for this rider equals the kept one publishes
                // exactly the kept route's promise, so it is recorded as a kept-route breach.
                var noWorseRecord = noWorseRoute
                    && !CommitmentBreachRecord.ProjectionsEqual(
                        exogenousProjection.Value!,
                        candidateProjection.Value!);
                forcedExemptions.Add(
                    new ForcedReferenceExemption(
                        request.Id,
                        request.AssignedVehicleId!.Value,
                        Array.AsReadOnly(codes),
                        noWorseRoute));

                // A breach is an event. With the promise unchanged and the rider in the same
                // lifecycle phase as before this batch, the gates see the same inputs as at the
                // rider's last forced breach, so the exemption repeats without a new record. A
                // phase change can make another hard limit apply, so it always records; the
                // code comparison is a defence the current gates cannot trigger on their own.
                var latest = incidents.Breaches.LastOrDefault(
                    value => (value.Kind is CommitmentBreachKind.ForcedReference
                            or CommitmentBreachKind.ForcedNoWorse)
                        && value.RequestId == request.Id);
                var samePhase = context.BeforeEventState.Run.Requests.TryGetValue(
                        request.Id,
                        out var previousRequest)
                    && previousRequest.Lifecycle == request.Lifecycle;

                if (unchanged
                    && samePhase
                    && latest is not null
                    && latest.WitnessCodes.SequenceEqual(codes, StringComparer.Ordinal))
                {
                    continue;
                }

                // The rider sees the kept-route projection and the ledger is charged as for any
                // publication (never reset); the breach states which gate was overrun.
                var budgetAfter = unchanged
                    ? budgetBefore
                    : ledger.Histories[request.Id].Current.BudgetAfter;
                CommitmentBreachRecord breach;

                try
                {
                    breach = noWorseRecord
                        ? CommitmentBreachRecord.CreateForcedNoWorse(
                            CreateForcedBreachId(context, request.Id),
                            request.Id,
                            priorHistory.Current.PublishedPromise,
                            exogenousProjection.Value!,
                            candidateProjection.Value!,
                            calculated.Deltas,
                            budgetBefore,
                            budgetAfter,
                            forcedCodes,
                            context.SourceEventSequence,
                            context.CandidateState.Run.AppliedEpoch,
                            context.CandidateState.Run.SimulationTime)
                        : CommitmentBreachRecord.CreateForcedReference(
                            CreateForcedBreachId(context, request.Id),
                            request.Id,
                            priorHistory.Current.PublishedPromise,
                            exogenousProjection.Value!,
                            candidateProjection.Value!,
                            calculated.Deltas,
                            budgetBefore,
                            budgetAfter,
                            forcedCodes,
                            context.SourceEventSequence,
                            context.CandidateState.Run.AppliedEpoch,
                            context.CandidateState.Run.SimulationTime);
                }
                catch (ArgumentException error)
                {
                    return Invalid(
                        CommitmentValidationStage.Ledger,
                        CommitmentFailureCodes.LedgerConflict,
                        noWorseRecord
                            ? $"The no-worse forced breach is inconsistent: {error.Message}"
                            : $"The forced-reference breach is inconsistent: {error.Message}",
                        request.AssignedVehicleId,
                        request.Id,
                        noWorseRecord ? "forcedNoWorse" : "forcedReference");
                }

                var appended = incidents.AppendBreach(breach);

                if (!appended.IsSuccess)
                {
                    return Invalid(
                        CommitmentValidationStage.Ledger,
                        appended.Failure!.Code,
                        appended.Failure.Message,
                        request.AssignedVehicleId,
                        request.Id,
                        noWorseRecord ? "forcedNoWorse" : "forcedReference");
                }

                incidents = appended.Ledger!;
                forcedBreaches.Add(breach);
            }
        }

        return collected is { Count: > 0 }
            ? CommitmentDecisionValidationResult.Invalid(collected)
            : CommitmentDecisionValidationResult.Valid(
                context.CandidateState with { Commitments = ledger, Incidents = incidents },
                publications.AsReadOnly(),
                forcedBreaches.AsReadOnly(),
                forcedExemptions.AsReadOnly());
    }

    /// <summary>
    /// True only for a request on a vehicle the caller named as forced-reference whose candidate
    /// route is exactly its reduced route. A changed route is exempted only under no-worse
    /// recovery, and then only through <see cref="IsForcedNoWorse"/> and its stricter rule.
    /// </summary>
    private static bool IsForcedReference(
        CommitmentValidationContext context,
        RideRequest request) =>
        context.ForcedReferenceVehicles is { Count: > 0 } forced
        && request.AssignedVehicleId is VehicleId vehicleId
        && forced.Contains(vehicleId)
        && context.CandidateState.Run.Vehicles.TryGetValue(vehicleId, out var candidate)
        && context.ReducedState.Run.Vehicles.TryGetValue(vehicleId, out var reduced)
        && candidate.Route.IsSemanticallyEqual(reduced.Route);

    /// <summary>
    /// True only under no-worse recovery, for a request on a forced-reference vehicle whose
    /// candidate route differs from its reduced route.
    /// </summary>
    private static bool IsForcedNoWorse(
        CommitmentValidationContext context,
        RideRequest request) =>
        context.ForcedNoWorse
        && context.ForcedReferenceVehicles is { Count: > 0 } forced
        && request.AssignedVehicleId is VehicleId vehicleId
        && forced.Contains(vehicleId)
        && context.CandidateState.Run.Vehicles.TryGetValue(vehicleId, out var candidate)
        && context.ReducedState.Run.Vehicles.TryGetValue(vehicleId, out var reduced)
        && !candidate.Route.IsSemanticallyEqual(reduced.Route);

    /// <summary>
    /// Evaluates the kept route (the exogenous projection published as it is) against the same
    /// gates, so a changed route can be compared with it. Null when the deltas cannot be formed.
    /// </summary>
    private NoWorseReference? KeptRouteReference(
        RideRequest request,
        RiderCommitmentHistory priorHistory,
        PromiseProjection exogenous,
        CommitmentValidationContext context,
        CommitmentPolicy policy,
        PromiseProjection? anchor,
        long? dropEtaTotalLimit)
    {
        var keptLocks = _lockEvaluator.Evaluate(
            request,
            priorHistory.Current.PublishedPromise,
            exogenous,
            exogenous,
            context.CandidateState.Run.SimulationTime,
            policy,
            anchor);
        var keptDeltas = _deltaCalculator.Calculate(
            priorHistory.Current.PublishedPromise,
            exogenous,
            exogenous,
            policy.MaterialRevisionRule,
            context.StopDistances);

        if (!keptDeltas.IsSuccess)
        {
            return null;
        }

        var keptCharged = policy.BudgetBasis == CommitmentBudgetBasis.DecisionInduced
            ? keptDeltas.Deltas!.DecisionInduced
            : keptDeltas.Deltas!.Visible;
        var keptBudget = _budgetEvaluator.Evaluate(
            request.Id,
            request.Lifecycle,
            priorHistory.Current.BudgetAfter,
            keptCharged,
            policy,
            dropEtaTotalLimit);
        return new NoWorseReference(exogenous, keptLocks, keptBudget.Witnesses);
    }

    /// <summary>
    /// The kept route's gate overruns for one rider. A changed route may be exempted for a
    /// deadline only if the kept route overruns the same side and the changed route is not
    /// further out on that side; for a budget dimension only if the kept route also exceeds it
    /// and the changed route's total is not larger. Phase locks and the revision cap are never
    /// exempted: both compare with the kept route, which therefore never overruns them.
    /// </summary>
    private sealed class NoWorseReference(
        PromiseProjection kept,
        IReadOnlyCollection<CommitmentLockWitness> keptLocks,
        IReadOnlyCollection<CommitmentBudgetWitness> keptBudget)
    {
        public bool AllowsLock(CommitmentLockWitness witness, PromiseProjection candidate) =>
            witness.Rule switch
            {
                CommitmentLockEvaluator.DeadlineCapRule =>
                    keptLocks.Any(value => value.Rule == CommitmentLockEvaluator.DeadlineCapRule)
                    && candidate.DropEta.Milliseconds <= kept.DropEta.Milliseconds,
                CommitmentLockEvaluator.DeadlineFloorRule =>
                    keptLocks.Any(value => value.Rule == CommitmentLockEvaluator.DeadlineFloorRule)
                    && candidate.DropEta.Milliseconds >= kept.DropEta.Milliseconds,
                _ => false,
            };

        public bool AllowsBudget(CommitmentBudgetWitness witness) =>
            keptBudget.Any(
                value => StringComparer.Ordinal.Equals(value.Dimension, witness.Dimension)
                    && witness.After <= value.After);
    }

    private static string CreateForcedBreachId(
        CommitmentValidationContext context,
        RequestId requestId)
    {
        var material = Encoding.UTF8.GetBytes(
            $"RideBound.ForcedReferenceBreach.v1\0{context.PublicationScope}\0{requestId.Value}"
            + $"\0{context.CandidateState.Run.AppliedEpoch}\0{context.SourceEventSequence}");
        return $"forced-{Convert.ToHexStringLower(SHA256.HashData(material))}";
    }

    private static CommitmentValidationWitness? ValidateStateBoundary(
        CommitmentValidationContext context)
    {
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
                context.CandidateState.Run)
            || !VehiclesMatchDecisionBoundary(
                context.ReducedState.Run,
                context.CandidateState.Run))
        {
            return new CommitmentValidationWitness(
                CommitmentValidationStage.State,
                "COMMITMENT_STATE_BOUNDARY_MISMATCH",
                "Candidate state may only decide pending requests and replace " +
                "vehicle mutable route suffixes at the shared epoch, travel, " +
                "incident, and commitment-ledger boundary.");
        }

        return null;
    }

    private static bool RequestsMatchDecisionBoundary(
        RideBoundRun reduced,
        RideBoundRun candidate)
    {
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
                    return false;
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
        RideBoundRun candidate)
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
        CommitmentValidationContext context)
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
                context.ReducedState.Run.SimulationTime);

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
