using System.Collections.Frozen;
using RideBound.Domain.Common;

namespace RideBound.Domain.Commitments;

[Flags]
public enum CommitmentPhase
{
    None = 0,
    Accepted = 1,
    WaitingPickup = 2,
    Onboard = 4,
    AllActive = Accepted | WaitingPickup | Onboard,
}

[Flags]
public enum PromiseLock
{
    None = 0,
    Vehicle = 1,
    PickupStop = 2,
    DropStop = 4,
    PickupEta = 8,
    DropEta = 16,
}

public enum CommitmentBudgetBasis
{
    DecisionInduced,
    CustomerVisible,
}

public sealed record CommitmentDimensionLimit
{
    public CommitmentDimensionLimit(
        CommitmentDimension dimension,
        long? hardLimit,
        CommitmentPhase applicablePhases)
    {
        if (!Enum.IsDefined(dimension))
        {
            throw new ArgumentOutOfRangeException(nameof(dimension));
        }

        if (hardLimit is < 0 or > DomainLimits.MaxCanonicalInteger)
        {
            throw new ArgumentOutOfRangeException(nameof(hardLimit));
        }

        if (applicablePhases == CommitmentPhase.None
            || (applicablePhases & ~CommitmentPhase.AllActive) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(applicablePhases));
        }

        Dimension = dimension;
        HardLimit = hardLimit;
        ApplicablePhases = applicablePhases;
    }

    public CommitmentDimension Dimension { get; }

    public long? HardLimit { get; }

    public CommitmentPhase ApplicablePhases { get; }
}

public sealed record MaterialRevisionRule
{
    public MaterialRevisionRule(
        long? rawEtaThresholdMs,
        long? displayBucketWidthMs)
    {
        if (rawEtaThresholdMs is < 1 or > DomainLimits.MaxCanonicalInteger)
        {
            throw new ArgumentOutOfRangeException(nameof(rawEtaThresholdMs));
        }

        if (displayBucketWidthMs is < 1 or > DomainLimits.MaxCanonicalInteger)
        {
            throw new ArgumentOutOfRangeException(nameof(displayBucketWidthMs));
        }

        if (rawEtaThresholdMs is null && displayBucketWidthMs is null)
        {
            throw new ArgumentException(
                "A material revision rule requires a raw threshold or display bucket.");
        }

        RawEtaThresholdMs = rawEtaThresholdMs;
        DisplayBucketWidthMs = displayBucketWidthMs;
    }

    public long? RawEtaThresholdMs { get; }

    public long? DisplayBucketWidthMs { get; }

    public bool IsMaterial(
        SimTime oldPickup,
        SimTime newPickup,
        SimTime oldDrop,
        SimTime newDrop)
    {
        var pickupDelta = AbsoluteDifference(oldPickup, newPickup);
        var dropDelta = AbsoluteDifference(oldDrop, newDrop);
        var thresholdChanged = RawEtaThresholdMs is long threshold
            && (pickupDelta >= threshold || dropDelta >= threshold);
        var bucketChanged = DisplayBucketWidthMs is long bucket
            && (oldPickup.Milliseconds / bucket
                    != newPickup.Milliseconds / bucket
                || oldDrop.Milliseconds / bucket
                    != newDrop.Milliseconds / bucket);

        return thresholdChanged || bucketChanged;
    }

    private static long AbsoluteDifference(SimTime left, SimTime right) =>
        left.Milliseconds >= right.Milliseconds
            ? left.Milliseconds - right.Milliseconds
            : right.Milliseconds - left.Milliseconds;
}

public sealed class CommitmentPolicy
{
    private readonly FrozenDictionary<
        CommitmentDimension,
        CommitmentDimensionLimit> _limits;

    public CommitmentPolicy(
        string policyId,
        CommitmentBudgetBasis budgetBasis,
        IEnumerable<CommitmentDimensionLimit> limits,
        MaterialRevisionRule materialRevisionRule,
        Duration? freezeHorizon = null,
        PromiseLock freezeHorizonLocks = PromiseLock.None,
        PromiseLock finalConfirmationLocks = PromiseLock.None,
        PromiseLock ratchetLocks = PromiseLock.None,
        Duration? dropEtaDeadlineSlack = null,
        bool dropEtaDeadlineTwoSided = false,
        bool dropEtaDeadlineRelaxed = false,
        Duration? dropEtaRevisionLimit = null)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(materialRevisionRule);
        PolicyId = DomainIdentifier.Require(policyId, nameof(policyId));
        var materialized = limits.ToArray();
        const PromiseLock allLocks =
            PromiseLock.Vehicle
            | PromiseLock.PickupStop
            | PromiseLock.DropStop
            | PromiseLock.PickupEta
            | PromiseLock.DropEta;

        if (!Enum.IsDefined(budgetBasis))
        {
            throw new ArgumentOutOfRangeException(nameof(budgetBasis));
        }

        if (materialized.Length != CommitmentDimensionVocabulary.Ordered.Count
            || materialized
                .Select(value => value.Dimension)
                .Distinct()
                .Count() != CommitmentDimensionVocabulary.Ordered.Count)
        {
            throw new ArgumentException(
                "A commitment policy must define every dimension exactly once.",
                nameof(limits));
        }

        if (freezeHorizon is null && freezeHorizonLocks != PromiseLock.None)
        {
            throw new ArgumentException(
                "Freeze-horizon locks require a freeze horizon.",
                nameof(freezeHorizonLocks));
        }

        if ((freezeHorizonLocks & ~allLocks) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(freezeHorizonLocks));
        }

        if ((finalConfirmationLocks & ~allLocks) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(finalConfirmationLocks));
        }

        const PromiseLock orderedLocks =
            PromiseLock.PickupEta | PromiseLock.DropEta;

        // A ratchet needs an order on the field. Vehicle and stop identities have
        // none, so only the two ETA fields can be relaxed this way.
        if ((ratchetLocks & ~orderedLocks) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ratchetLocks),
                "Only pickup and drop ETA locks can be relaxed to a ratchet.");
        }

        if (dropEtaDeadlineTwoSided && dropEtaDeadlineSlack is null)
        {
            throw new ArgumentException(
                "A two-sided deadline requires a deadline slack.",
                nameof(dropEtaDeadlineTwoSided));
        }

        if (dropEtaDeadlineRelaxed && dropEtaDeadlineSlack is null)
        {
            throw new ArgumentException(
                "A relaxed deadline requires a deadline slack.",
                nameof(dropEtaDeadlineRelaxed));
        }

        // The relaxation lifts only the cap side to the kept route; a floor relaxed the
        // same way is not defined, so the two options are exclusive.
        if (dropEtaDeadlineRelaxed && dropEtaDeadlineTwoSided)
        {
            throw new ArgumentException(
                "A relaxed deadline is one-sided and cannot be two-sided.",
                nameof(dropEtaDeadlineRelaxed));
        }

        BudgetBasis = budgetBasis;
        _limits = materialized.ToFrozenDictionary(value => value.Dimension);
        MaterialRevisionRule = materialRevisionRule;
        FreezeHorizon = freezeHorizon;
        FreezeHorizonLocks = freezeHorizonLocks;
        FinalConfirmationLocks = finalConfirmationLocks;
        RatchetLocks = ratchetLocks;
        DropEtaDeadlineSlack = dropEtaDeadlineSlack;
        DropEtaDeadlineTwoSided = dropEtaDeadlineTwoSided;
        DropEtaDeadlineRelaxed = dropEtaDeadlineRelaxed;
        DropEtaRevisionLimit = dropEtaRevisionLimit;
    }

    public string PolicyId { get; }

    public CommitmentBudgetBasis BudgetBasis { get; }

    public IReadOnlyDictionary<
        CommitmentDimension,
        CommitmentDimensionLimit> Limits => _limits;

    public MaterialRevisionRule MaterialRevisionRule { get; }

    public Duration? FreezeHorizon { get; }

    public PromiseLock FreezeHorizonLocks { get; }

    public PromiseLock FinalConfirmationLocks { get; }

    /// <summary>
    /// Locked ETA fields the decision may still improve. A field named here is
    /// violated only when the candidate moves it later than the exogenous
    /// projection; moving it earlier is allowed. This is the one-sided guarantee
    /// used in the ride-pooling literature, where a matched request's latest
    /// pickup time is tightened to the expected pickup time and never loosened.
    /// Empty by default, so a lock keeps its exact-equality meaning.
    /// </summary>
    public PromiseLock RatchetLocks { get; }

    /// <summary>
    /// Deadline on the drop ETA, measured from the rider's FIRST published promise
    /// (the deadline style of Schulz and Pfeiffer, 2026). Unlike every lock, this
    /// compares the candidate with the initial promise rather than with the
    /// exogenous projection, so exogenous drift alone can trip it, including on the
    /// safety no-op. Research probe; null disables the rule.
    /// </summary>
    public Duration? DropEtaDeadlineSlack { get; }

    /// <summary>
    /// When true the deadline is a window: the drop ETA may not move earlier than
    /// the initial promise by more than the slack either. Requires a slack.
    /// </summary>
    public bool DropEtaDeadlineTwoSided { get; }

    /// <summary>
    /// When true the one-sided deadline is relaxed to the later of the initial promise
    /// plus the slack and the exogenous (kept-route) projection: a candidate fails only
    /// if its drop ETA is later than both (DL-HZ in the study notation). Drift alone
    /// never trips it, so the safety no-op is never rejected by it and it never
    /// triggers forced-reference recovery. Per rider it accepts exactly what the strict
    /// deadline accepts under no-worse-than-reference recovery, without forced vehicles
    /// or breach records. Requires a slack; cannot be two-sided.
    /// </summary>
    public bool DropEtaDeadlineRelaxed { get; }

    /// <summary>
    /// Per-decision cap on how far a candidate may move a rider's drop ETA, in either
    /// direction, away from the exogenous (kept-route) projection; the reference is the
    /// one every lock uses, not the previous published promise (P-b in the study
    /// notation). Nothing accumulates across decisions, unlike the drop_eta_total_ms
    /// hard limit; drift is free and the kept route never trips the cap. It applies in
    /// the phases where the drop_eta_total_ms dimension applies, whether or not that
    /// dimension has a hard limit. Research probe; null disables the rule.
    /// </summary>
    public Duration? DropEtaRevisionLimit { get; }
}
