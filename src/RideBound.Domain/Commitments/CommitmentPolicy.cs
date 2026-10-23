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
        Duration? dropEtaRevisionLimit = null,
        int? dropEtaBudgetSlackPermille = null,
        int? dropEtaDeadlineSlackPermille = null)
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

        if (dropEtaBudgetSlackPermille is int budgetPermille
            && budgetPermille is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dropEtaBudgetSlackPermille),
                "A slack-proportional budget must be 1 to 1000 permille.");
        }

        if (dropEtaDeadlineSlackPermille is int deadlinePermille
            && deadlinePermille is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dropEtaDeadlineSlackPermille),
                "A slack-proportional deadline must be 1 to 1000 permille.");
        }

        // A rider has one drop deadline slack: either the fixed one or the share of its slack.
        if (dropEtaDeadlineSlack is not null && dropEtaDeadlineSlackPermille is not null)
        {
            throw new ArgumentException(
                "A deadline slack is either fixed or slack-proportional, not both.",
                nameof(dropEtaDeadlineSlackPermille));
        }

        var hasDeadline = dropEtaDeadlineSlack is not null
            || dropEtaDeadlineSlackPermille is not null;

        if (dropEtaDeadlineTwoSided && !hasDeadline)
        {
            throw new ArgumentException(
                "A two-sided deadline requires a deadline slack.",
                nameof(dropEtaDeadlineTwoSided));
        }

        if (dropEtaDeadlineRelaxed && !hasDeadline)
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

        // The slack-proportional budget is the drop_eta_total_ms hard limit of each rider, so a
        // fixed hard limit on the same dimension would leave two limits for one ledger value.
        if (dropEtaBudgetSlackPermille is not null
            && materialized.Single(value => value.Dimension == CommitmentDimension.DropEtaTotalMs)
                .HardLimit is not null)
        {
            throw new ArgumentException(
                "A slack-proportional budget replaces the fixed drop_eta_total_ms hard limit.",
                nameof(dropEtaBudgetSlackPermille));
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
        DropEtaBudgetSlackPermille = dropEtaBudgetSlackPermille;
        DropEtaDeadlineSlackPermille = dropEtaDeadlineSlackPermille;
    }

    /// <summary>
    /// The rider's drop slack at the first published promise, scaled by a permille share
    /// (Elmi et al. 2026, Ordinary Control, as formalised in the wf14 preregistration):
    /// round(permille / 1000 * max(0, pickup0 + maxRide - drop0)), half away from zero.
    /// The slack is the room the ride-time limit leaves after the first promise, so it is
    /// fixed when the promise opens and never re-measured. The arithmetic is in IEEE double,
    /// exactly as the emulator's Math.round(alpha * slack), and the value is non-negative.
    /// </summary>
    public static long SlackProportionalLimit(
        PromiseProjection initialPromise,
        Duration maximumRideTime,
        int permille)
    {
        ArgumentNullException.ThrowIfNull(initialPromise);

        if (permille is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(permille));
        }

        // Canonical non-negative integers below 2^53, so the sums fit in a long. The slack
        // converts to double without loss only below 2^53 ms (about 285,000 years); beyond
        // that the limit may leave the canonical range and the Duration constructor throws.
        var slack = Math.Max(
            0L,
            initialPromise.PickupEta.Milliseconds
                + maximumRideTime.Milliseconds
                - initialPromise.DropEta.Milliseconds);

        return (long)Math.Round(
            permille / 1000.0 * slack,
            MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Effective drop deadline slack of one rider: the fixed slack, or the slack-proportional
    /// share of the rider's first promise; null when the policy has no deadline.
    /// </summary>
    public Duration? DeadlineSlackFor(PromiseProjection? initialPromise, Duration maximumRideTime)
    {
        if (DropEtaDeadlineSlack is Duration fixedSlack)
        {
            return fixedSlack;
        }

        if (DropEtaDeadlineSlackPermille is not int permille)
        {
            return null;
        }

        if (initialPromise is null)
        {
            throw new ArgumentException(
                "A slack-proportional deadline requires the initial promise.",
                nameof(initialPromise));
        }

        return new Duration(SlackProportionalLimit(initialPromise, maximumRideTime, permille));
    }

    /// <summary>
    /// Per-rider drop_eta_total_ms hard limit of a slack-proportional budget; null when the
    /// policy uses the dimension's fixed limit (or none).
    /// </summary>
    public long? DropEtaTotalLimitFor(PromiseProjection? initialPromise, Duration maximumRideTime)
    {
        if (DropEtaBudgetSlackPermille is not int permille)
        {
            return null;
        }

        if (initialPromise is null)
        {
            throw new ArgumentException(
                "A slack-proportional budget requires the initial promise.",
                nameof(initialPromise));
        }

        return SlackProportionalLimit(initialPromise, maximumRideTime, permille);
    }

    /// <summary>True when a rule of this policy reads the rider's initial promise.</summary>
    public bool ReadsInitialPromise =>
        DropEtaDeadlineSlack is not null
        || DropEtaDeadlineSlackPermille is not null
        || DropEtaBudgetSlackPermille is not null;

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
    /// triggers forced-reference recovery. When the kept route overruns no other gate,
    /// each candidate passes it for a rider exactly when the strict deadline passes it
    /// under no-worse-than-reference recovery, but without forced vehicles or breach
    /// records. No-worse recovery also exempts budget overruns of the kept route, and its
    /// forced options add a ranking level, so beyond that case, and for the selection
    /// itself, the two can differ. Requires a slack; cannot be two-sided.
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

    /// <summary>
    /// Slack-proportional cumulative budget (rule C with the budget of Elmi et al. 2026,
    /// RCE in the study notation): each rider's drop_eta_total_ms hard limit is
    /// <see cref="SlackProportionalLimit"/> of its first promise. Replaces the dimension's
    /// fixed hard limit, which must then be absent. Research probe; null disables the rule.
    /// </summary>
    public int? DropEtaBudgetSlackPermille { get; }

    /// <summary>
    /// Slack-proportional deadline (Elmi et al. 2026 Ordinary Control; EO, or EOHZ when
    /// relaxed): each rider's deadline slack is <see cref="SlackProportionalLimit"/> of its
    /// first promise. Exclusive with the fixed <see cref="DropEtaDeadlineSlack"/>.
    /// Research probe; null disables the rule.
    /// </summary>
    public int? DropEtaDeadlineSlackPermille { get; }
}
