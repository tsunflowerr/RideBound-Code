using RideBound.Domain.Common;

namespace RideBound.Domain.Validation;

/// <summary>
/// One exogenous service-quality breach on one request. A breach is recorded
/// when the vehicle's own active route — the route the operator already
/// committed to and has not changed — no longer meets a service-quality
/// deadline under the current travel snapshot. The cause is therefore outside
/// any decision the policy is about to take.
/// </summary>
/// <param name="ContractualMilliseconds">
/// The published bound the request was accepted under.
/// </param>
/// <param name="ExogenousMilliseconds">
/// The value the unchanged active route now realizes. This is the anti-laundering
/// bound: no candidate may be worse than doing nothing.
/// </param>
public sealed record ServiceQualityBreach
{
    public ServiceQualityBreach(
        RequestId requestId,
        string code,
        string dimension,
        long contractualMilliseconds,
        long exogenousMilliseconds)
    {
        var isKnownPair = code == PhysicalViolationCodes.PickupWindow
                && dimension == "latestPickupMs"
            || code == PhysicalViolationCodes.MaxRideTime
                && dimension == "maxRideTimeMs";

        if (!isKnownPair
            || contractualMilliseconds is < 0 or > DomainLimits.MaxCanonicalInteger
            || exogenousMilliseconds is < 0 or > DomainLimits.MaxCanonicalInteger
            || exogenousMilliseconds <= contractualMilliseconds)
        {
            throw new ArgumentException(
                "A service-quality breach requires a known deadline dimension " +
                "and a canonical exogenous value above its contract.");
        }

        RequestId = requestId;
        Code = code;
        Dimension = dimension;
        ContractualMilliseconds = contractualMilliseconds;
        ExogenousMilliseconds = exogenousMilliseconds;
    }

    public RequestId RequestId { get; }

    public string Code { get; }

    public string Dimension { get; }

    public long ContractualMilliseconds { get; }

    public long ExogenousMilliseconds { get; }
}

/// <summary>
/// Per-request relaxation of the two service-quality dimensions
/// (<c>MAX_RIDE_TIME</c> and <c>PICKUP_WINDOW</c>), derived from the exogenous
/// projection of a vehicle's unchanged active route.
///
/// <para>ADR-045 splits the physical constraints into two classes. Structural
/// constraints (connectivity, precedence, capacity, frozen prefix, onboard and
/// accepted preservation, plan version, overflow) are invariants of a
/// well-formed plan: violating one is a defect, never a consequence of traffic,
/// so they stay strict everywhere. Service-quality constraints are promises
/// about time; worsening travel can breach them without anyone deciding
/// anything. Enforcing those continuously deletes the safety no-op and kills the
/// run, which is what blocked WP9.</para>
///
/// <para>The relaxed bound is <c>max(contractual, exogenous)</c>. Because the
/// exogenous value is what the unchanged active route already realizes, the
/// relaxation can never admit a candidate that is worse on that dimension than
/// doing nothing — a decision cannot launder its own damage through a breach it
/// did not cause. Requests with no stop on the active route (every newly
/// inserted request) have no entry and stay strictly contractual, so a request
/// can never be accepted into a route that cannot serve it.</para>
///
/// <para>ADR-047 narrows where the relief may be applied. The candidate
/// generator grants it to the safety no-op only; every changed candidate is
/// generated under <see cref="Strict"/>. The reason is external: the simulator
/// keeps its own hard feasibility check and applies it to any plan RideBound
/// proposes, but never re-checks a route it is already executing. Relief on the
/// no-op therefore costs nothing, while relief on a changed plan would make
/// RideBound propose plans the simulator rejects.</para>
///
/// <para>The relaxation is a pure function of
/// <c>(run, vehicle, travelSnapshot, evaluationTime)</c> and is applied
/// identically in every arm, so it moves no arm relative to another.</para>
/// </summary>
public sealed class ServiceQualityAllowance
{
    private static readonly IReadOnlyList<ServiceQualityBreach> NoBreaches = [];

    private readonly Dictionary<RequestId, long> _maxRideTimeMs;
    private readonly Dictionary<RequestId, long> _latestPickupMs;

    private ServiceQualityAllowance(IReadOnlyList<ServiceQualityBreach> breaches)
    {
        _maxRideTimeMs = [];
        _latestPickupMs = [];

        foreach (var breach in breaches)
        {
            var target = breach.Code == PhysicalViolationCodes.MaxRideTime
                ? _maxRideTimeMs
                : _latestPickupMs;

            // A repeated request/dimension pair keeps the tightest bound so a
            // duplicate observation can never widen the relaxation.
            target[breach.RequestId] =
                target.TryGetValue(breach.RequestId, out var existing)
                    ? Math.Min(existing, breach.ExogenousMilliseconds)
                    : breach.ExogenousMilliseconds;
        }

        Breaches = breaches;
        Digest = string.Join(
            ";",
            breaches
                .Select(
                    value => $"{value.RequestId.Value}|{value.Code}|" +
                        $"{value.ExogenousMilliseconds}")
                .Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// No relaxation: every service-quality bound is the published contractual
    /// one. This is the behaviour of every call site that does not probe.
    /// </summary>
    public static ServiceQualityAllowance Strict { get; } = new(NoBreaches);

    /// <summary>
    /// The exogenous breaches this relaxation was derived from, ordered by
    /// request then dimension. Empty for <see cref="Strict"/>.
    /// </summary>
    public IReadOnlyList<ServiceQualityBreach> Breaches { get; }

    public bool HasBreaches => Breaches.Count > 0;

    /// <summary>
    /// Process-local identity used to keep memoized schedule artifacts from
    /// crossing two different relaxations. Not a published identity.
    /// </summary>
    public string Digest { get; }

    public static ServiceQualityAllowance FromBreaches(
        IEnumerable<ServiceQualityBreach> breaches)
    {
        ArgumentNullException.ThrowIfNull(breaches);

        var ordered = breaches
            .OrderBy(value => value.RequestId.Value, StringComparer.Ordinal)
            .ThenBy(value => value.Code, StringComparer.Ordinal)
            .ToArray();

        return ordered.Length == 0 ? Strict : new ServiceQualityAllowance(ordered);
    }

    /// <summary>
    /// The admissible maximum ride time for <paramref name="requestId"/>, never
    /// below the contractual bound and never above what the unchanged active
    /// route already realizes.
    /// </summary>
    public long MaxRideTimeBound(RequestId requestId, long contractualMilliseconds) =>
        _maxRideTimeMs.TryGetValue(requestId, out var relaxed)
            ? Math.Max(contractualMilliseconds, relaxed)
            : contractualMilliseconds;

    /// <summary>
    /// The admissible latest pickup for <paramref name="requestId"/>, under the
    /// same two-sided rule as <see cref="MaxRideTimeBound"/>.
    /// </summary>
    public long LatestPickupBound(RequestId requestId, long contractualMilliseconds) =>
        _latestPickupMs.TryGetValue(requestId, out var relaxed)
            ? Math.Max(contractualMilliseconds, relaxed)
            : contractualMilliseconds;
}

/// <summary>
/// Result of probing a vehicle's unchanged active route for exogenous
/// service-quality breaches.
/// </summary>
/// <param name="Witness">
/// Non-null when the active route fails a <em>structural</em> constraint. That
/// is a defect, not traffic, and stays fail-closed.
/// </param>
public sealed record ServiceQualityProbeResult(
    PhysicalViolationWitness? Witness,
    ServiceQualityAllowance Allowance)
{
    public bool IsSuccess => Witness is null;

    public static ServiceQualityProbeResult Success(
        ServiceQualityAllowance allowance) =>
        new(null, allowance);

    public static ServiceQualityProbeResult Failure(
        PhysicalViolationWitness witness) =>
        new(witness, ServiceQualityAllowance.Strict);
}
