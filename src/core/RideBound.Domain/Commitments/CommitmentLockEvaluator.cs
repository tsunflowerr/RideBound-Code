using RideBound.Domain.Common;
using RideBound.Domain.Requests;

namespace RideBound.Domain.Commitments;

public sealed record CommitmentLockWitness(
    RequestId RequestId,
    string Dimension,
    string Rule);

public sealed class CommitmentLockEvaluator
{
    /// <summary>
    /// Rule of a drop ETA later than both the initial promise plus the slack and the
    /// exogenous projection.
    /// </summary>
    public const string DeadlineCapRule = "deadline_cap";

    public IReadOnlyList<CommitmentLockWitness> Evaluate(
        RideRequest request,
        PublishedPromise previous,
        PromiseProjection exogenous,
        PromiseProjection candidate,
        CommitmentPolicy policy,
        PromiseProjection? anchor = null,
        ShadowAllocation? shadowAllocation = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(exogenous);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(policy);

        if (previous.Projection.RequestId != request.Id
            || exogenous.RequestId != request.Id
            || candidate.RequestId != request.Id)
        {
            throw new ArgumentException(
                "Lock evaluation request and promise identities must match.");
        }

        // Waiting-rider relocation lifts only this rule's vehicle lock, and only for a
        // confirmed waiting rider; the onboard and configured rules still apply.
        var activeLocks = policy.WaitingRiderRelocation
            && request.Lifecycle == RequestLifecycle.WaitingPickup
                ? PromiseLock.None
                : PromiseLock.Vehicle;
        var rules = new List<(PromiseLock Locks, string Rule)>();

        if (request.Lifecycle == RequestLifecycle.Onboard)
        {
            rules.Add(
                (
                    PromiseLock.Vehicle
                    | PromiseLock.PickupStop
                    | PromiseLock.PickupEta,
                    "onboard"));
        }

        if (request.Lifecycle == RequestLifecycle.WaitingPickup
            && policy.FinalConfirmationLocks != PromiseLock.None)
        {
            rules.Add((policy.FinalConfirmationLocks, "final_confirmation"));
        }

        rules.Insert(0, (activeLocks, "accepted_assignment"));
        var witnesses = new List<CommitmentLockWitness>();

        foreach (var (locks, rule) in rules)
        {
            AddWitnesses(
                witnesses,
                request.Id,
                exogenous,
                candidate,
                locks,
                rule,
                policy.RatchetLocks);
        }

        var ordered = witnesses
            .Distinct()
            .OrderBy(value => value.Dimension, StringComparer.Ordinal)
            .ThenBy(value => value.Rule, StringComparer.Ordinal)
            .ToList();

        // The deadline witness goes last, so a lock witness keeps its position
        // and callers reading the first witness still see the lock.
        if (policy.DropEtaDeadlineSlack is not null
            || policy.DropEtaDeadlineSlackPermille is not null)
        {
            AddDeadlineWitness(
                ordered,
                request,
                exogenous,
                candidate,
                anchor,
                policy,
                shadowAllocation);
        }

        return ordered.ToArray();
    }

    private static void AddWitnesses(
        ICollection<CommitmentLockWitness> witnesses,
        RequestId requestId,
        PromiseProjection previous,
        PromiseProjection candidate,
        PromiseLock locks,
        string rule,
        PromiseLock ratchetLocks)
    {
        if ((locks & PromiseLock.Vehicle) != 0
            && previous.VehicleId != candidate.VehicleId)
        {
            witnesses.Add(
                new CommitmentLockWitness(requestId, "vehicle_id", rule));
        }

        if ((locks & PromiseLock.PickupStop) != 0
            && (previous.PickupStopId != candidate.PickupStopId
                || previous.PickupNodeId != candidate.PickupNodeId))
        {
            witnesses.Add(
                new CommitmentLockWitness(requestId, "pickup_stop", rule));
        }

        if ((locks & PromiseLock.DropStop) != 0
            && (previous.DropStopId != candidate.DropStopId
                || previous.DropNodeId != candidate.DropNodeId))
        {
            witnesses.Add(
                new CommitmentLockWitness(requestId, "drop_stop", rule));
        }

        if ((locks & PromiseLock.PickupEta) != 0
            && Violates(
                previous.PickupEta,
                candidate.PickupEta,
                (ratchetLocks & PromiseLock.PickupEta) != 0))
        {
            witnesses.Add(
                new CommitmentLockWitness(requestId, "pickup_eta_ms", rule));
        }

        if ((locks & PromiseLock.DropEta) != 0
            && Violates(
                previous.DropEta,
                candidate.DropEta,
                (ratchetLocks & PromiseLock.DropEta) != 0))
        {
            witnesses.Add(
                new CommitmentLockWitness(requestId, "drop_eta_ms", rule));
        }
    }

    /// <summary>
    /// Every lock above compares the candidate with the exogenous projection, so
    /// exogenous drift never trips a lock. The deadline compares it with the
    /// rider's initial promise instead. It is a gate that lives next to the locks,
    /// not a lock. The deadline is relaxed: it also requires the candidate to be
    /// later than the exogenous projection, so it fails only past
    /// max(initial + slack, kept route) and drift alone never trips it.
    /// </summary>
    private static void AddDeadlineWitness(
        ICollection<CommitmentLockWitness> witnesses,
        RideRequest request,
        PromiseProjection exogenous,
        PromiseProjection candidate,
        PromiseProjection? anchor,
        CommitmentPolicy policy,
        ShadowAllocation? shadowAllocation)
    {
        if (anchor is null)
        {
            throw new ArgumentException(
                "A drop ETA deadline requires the initial promise anchor.",
                nameof(anchor));
        }

        if (anchor.RequestId != request.Id)
        {
            throw new ArgumentException(
                "Deadline anchor identity must match the request.",
                nameof(anchor));
        }

        // Fixed slack, or the slack-proportional share of this rider's first promise (scaled by
        // its Shadow class under a shadow-allocated policy).
        var slack = policy.DeadlineSlackFor(anchor, request.MaxRideTime, shadowAllocation)
            ?? throw new InvalidOperationException("A deadline rule has no slack.");

        // Both values are canonical non-negative integers below 2^53, so the
        // difference fits in a long.
        var shift = candidate.DropEta.Milliseconds - anchor.DropEta.Milliseconds;

        // The kept route has candidate == exogenous, so the deadline never rejects it.
        if (shift > slack.Milliseconds
            && candidate.DropEta.Milliseconds > exogenous.DropEta.Milliseconds)
        {
            witnesses.Add(
                new CommitmentLockWitness(request.Id, "drop_eta_ms", DeadlineCapRule));
        }
    }

    /// <summary>
    /// An exact lock rejects any movement. A ratcheted lock rejects only movement
    /// that makes the promise later, so the decision may still improve it.
    /// </summary>
    private static bool Violates(SimTime before, SimTime after, bool ratcheted) =>
        ratcheted
            ? after.Milliseconds > before.Milliseconds
            : after != before;
}
