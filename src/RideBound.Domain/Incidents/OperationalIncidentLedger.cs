using System.Collections.Frozen;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Validation;

namespace RideBound.Domain.Incidents;

public readonly record struct IncidentId
{
    public IncidentId(string value)
    {
        Value = DomainIdentifier.Require(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

/// <summary>
/// ADR-076 (exploratory). A rider observed boarding after the latest pickup of the accepted
/// window. The window is not relaxed: the record keeps both times, and the gap is a service
/// violation of its own, separate from commitment breaches.
/// </summary>
public sealed record ObservedLatePickup
{
    public ObservedLatePickup(
        RequestId requestId,
        VehicleId vehicleId,
        SimTime latestPickup,
        SimTime actualPickup,
        long sourceEventSequence,
        long recordedEpoch)
    {
        if (actualPickup.Milliseconds <= latestPickup.Milliseconds)
        {
            throw new ArgumentException(
                "A late pickup must happen after the latest pickup.",
                nameof(actualPickup));
        }

        if (sourceEventSequence is < 1 or > DomainLimits.MaxCanonicalInteger)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceEventSequence));
        }

        if (recordedEpoch is < 1 or > DomainLimits.MaxCanonicalInteger)
        {
            throw new ArgumentOutOfRangeException(nameof(recordedEpoch));
        }

        RequestId = requestId;
        VehicleId = vehicleId;
        LatestPickup = latestPickup;
        ActualPickup = actualPickup;
        SourceEventSequence = sourceEventSequence;
        RecordedEpoch = recordedEpoch;
    }

    public RequestId RequestId { get; }

    public VehicleId VehicleId { get; }

    public SimTime LatestPickup { get; }

    public SimTime ActualPickup { get; }

    public long SourceEventSequence { get; }

    public long RecordedEpoch { get; }

    public long LatenessMilliseconds => ActualPickup.Milliseconds - LatestPickup.Milliseconds;
}

public sealed record OperationalIncident
{
    public OperationalIncident(
        IncidentId id,
        string reasonCode,
        IEnumerable<VehicleId> affectedVehicleIds,
        IEnumerable<RequestId> affectedRequestIds,
        long openedEventSequence,
        SimTime openedAt,
        long? resolvedEventSequence = null,
        SimTime? resolvedAt = null)
    {
        ArgumentNullException.ThrowIfNull(affectedVehicleIds);
        ArgumentNullException.ThrowIfNull(affectedRequestIds);

        if (openedEventSequence is < 1 or > DomainLimits.MaxCanonicalInteger)
        {
            throw new ArgumentOutOfRangeException(nameof(openedEventSequence));
        }

        if ((resolvedEventSequence is null) != (resolvedAt is null)
            || resolvedEventSequence is not null
                && (resolvedEventSequence <= openedEventSequence
                    || resolvedEventSequence > DomainLimits.MaxCanonicalInteger)
            || resolvedAt is not null
                && resolvedAt.Value.Milliseconds < openedAt.Milliseconds)
        {
            throw new ArgumentException(
                "Incident resolution must be a later, complete event/time pair.");
        }

        var vehicles = Normalize(affectedVehicleIds, value => value.Value);
        var requests = Normalize(affectedRequestIds, value => value.Value);

        if (vehicles.Length == 0)
        {
            throw new ArgumentException(
                "An operational incident must affect at least one vehicle.",
                nameof(affectedVehicleIds));
        }

        Id = id;
        ReasonCode = DomainIdentifier.Require(reasonCode, nameof(reasonCode));
        AffectedVehicleIds = Array.AsReadOnly(vehicles);
        AffectedRequestIds = Array.AsReadOnly(requests);
        OpenedEventSequence = openedEventSequence;
        OpenedAt = openedAt;
        ResolvedEventSequence = resolvedEventSequence;
        ResolvedAt = resolvedAt;
    }

    public IncidentId Id { get; }

    public string ReasonCode { get; }

    public IReadOnlyList<VehicleId> AffectedVehicleIds { get; }

    public IReadOnlyList<RequestId> AffectedRequestIds { get; }

    public long OpenedEventSequence { get; }

    public SimTime OpenedAt { get; }

    public long? ResolvedEventSequence { get; }

    public SimTime? ResolvedAt { get; }

    public bool IsOpen => ResolvedEventSequence is null;

    public OperationalIncident Resolve(long eventSequence, SimTime resolvedAt) =>
        new(
            Id,
            ReasonCode,
            AffectedVehicleIds,
            AffectedRequestIds,
            OpenedEventSequence,
            OpenedAt,
            eventSequence,
            resolvedAt);

    private static T[] Normalize<T>(
        IEnumerable<T> values,
        Func<T, string> identifier)
    {
        var materialized = values
            .OrderBy(identifier, StringComparer.Ordinal)
            .ToArray();

        if (materialized.Select(identifier).Distinct(StringComparer.Ordinal).Count()
            != materialized.Length)
        {
            throw new ArgumentException("Incident entity sets cannot contain duplicates.");
        }

        return materialized;
    }
}

public enum CommitmentBreachKind
{
    OperationalIncident,
    ExogenousServiceQuality,

    /// <summary>
    /// A commitment gate rejected every option of a vehicle, including keeping its route, and the
    /// run kept the route anyway as a forced action. The record states which gate was overrun and
    /// by how much; the ledger is charged exactly as for any other publication and is never reset.
    /// </summary>
    ForcedReference,

    /// <summary>
    /// Exploratory "no worse than reference" recovery: on a vehicle whose kept route a gate
    /// rejected, the run chose a changed route of the same vehicle that overruns the same gates by
    /// no more than the kept route would. The published projection is the changed route's, so it
    /// differs from the kept (exogenous) one; the ledger is charged as for any publication. The
    /// "no more than" comparison needs the rider's anchor and policy, so the validator enforces it.
    /// </summary>
    ForcedNoWorse,
}

public sealed record CommitmentBreachRecord
{
    public CommitmentBreachRecord(
        string breachId,
        IncidentId incidentId,
        RequestId requestId,
        PublishedPromise previousPromise,
        PromiseProjection exogenousProjection,
        PromiseProjection safetyProjection,
        ThreeWayPromiseDelta deltas,
        CommitmentVector budgetBefore,
        CommitmentVector attemptedBudgetAfter,
        IEnumerable<string> witnessCodes,
        long sourceEventSequence,
        long recordedEpoch,
        SimTime recordedAt)
        : this(
            breachId,
            CommitmentBreachKind.OperationalIncident,
            incidentId,
            requestId,
            previousPromise,
            exogenousProjection,
            safetyProjection,
            deltas,
            budgetBefore,
            attemptedBudgetAfter,
            witnessCodes,
            [],
            sourceEventSequence,
            recordedEpoch,
            recordedAt)
    {
    }

    private CommitmentBreachRecord(
        string breachId,
        CommitmentBreachKind kind,
        IncidentId? incidentId,
        RequestId requestId,
        PublishedPromise previousPromise,
        PromiseProjection exogenousProjection,
        PromiseProjection safetyProjection,
        ThreeWayPromiseDelta deltas,
        CommitmentVector budgetBefore,
        CommitmentVector attemptedBudgetAfter,
        IEnumerable<string> witnessCodes,
        IEnumerable<ServiceQualityBreach> serviceQualityWitnesses,
        long sourceEventSequence,
        long recordedEpoch,
        SimTime recordedAt)
    {
        ArgumentNullException.ThrowIfNull(previousPromise);
        ArgumentNullException.ThrowIfNull(exogenousProjection);
        ArgumentNullException.ThrowIfNull(safetyProjection);
        ArgumentNullException.ThrowIfNull(deltas);
        ArgumentNullException.ThrowIfNull(budgetBefore);
        ArgumentNullException.ThrowIfNull(attemptedBudgetAfter);
        ArgumentNullException.ThrowIfNull(witnessCodes);
        ArgumentNullException.ThrowIfNull(serviceQualityWitnesses);

        if (previousPromise.Projection.RequestId != requestId
            || exogenousProjection.RequestId != requestId
            || safetyProjection.RequestId != requestId)
        {
            throw new ArgumentException(
                "Every breach projection must belong to the affected request.");
        }

        if (sourceEventSequence is < 1 or > DomainLimits.MaxCanonicalInteger
            || recordedEpoch is < 1 or > DomainLimits.MaxCanonicalInteger
            || recordedEpoch < previousPromise.PublishedEpoch
            || recordedAt.Milliseconds
                < previousPromise.PublishedAt.Milliseconds)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceEventSequence));
        }

        var witnesses = witnessCodes
            .Select(value => DomainIdentifier.Require(value, nameof(witnessCodes)))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (witnesses.Length == 0)
        {
            throw new ArgumentException(
                "A breach record requires at least one exact witness code.",
                nameof(witnessCodes));
        }

        var serviceWitnesses = serviceQualityWitnesses
            .OrderBy(value => value.RequestId.Value, StringComparer.Ordinal)
            .ThenBy(value => value.Code, StringComparer.Ordinal)
            .ThenBy(value => value.Dimension, StringComparer.Ordinal)
            .ToArray();

        if (kind == CommitmentBreachKind.ExogenousServiceQuality)
        {
            if (incidentId is not null
                || !ProjectionEquals(exogenousProjection, safetyProjection)
                || deltas.DecisionInduced != CommitmentVector.Zero
                || deltas.Exogenous != deltas.Visible
                || attemptedBudgetAfter != budgetBefore)
            {
                throw new ArgumentException(
                    "An exogenous breach requires identical no-op projections, " +
                    "zero decision delta and an unchanged budget.");
            }

            if (serviceWitnesses.Length == 0
                || serviceWitnesses.Any(
                    value => value.RequestId != requestId
                        || value.ContractualMilliseconds < 0
                        || value.ExogenousMilliseconds
                            <= value.ContractualMilliseconds
                        || value.Code == PhysicalViolationCodes.PickupWindow
                            && value.Dimension != "latestPickupMs"
                        || value.Code == PhysicalViolationCodes.MaxRideTime
                            && value.Dimension != "maxRideTimeMs"
                        || value.Code is not (
                            PhysicalViolationCodes.PickupWindow
                            or PhysicalViolationCodes.MaxRideTime))
                || serviceWitnesses
                    .Select(value => (value.RequestId, value.Code, value.Dimension))
                    .Distinct()
                    .Count() != serviceWitnesses.Length
                || !witnesses.SequenceEqual(
                    serviceWitnesses
                        .Select(value => value.Code)
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal),
                    StringComparer.Ordinal))
            {
                throw new ArgumentException(
                    "Exogenous breach witnesses must be exact, unique service-quality overruns.",
                    nameof(serviceQualityWitnesses));
            }
        }
        else if (kind == CommitmentBreachKind.ForcedReference)
        {
            // The forced action keeps the route, so the published projection is the kept-route
            // projection and the decision adds nothing. The charged delta follows the budget
            // basis: zero under decisionInduced, the visible delta under customerVisible.
            var decisionAfter = budgetBefore.Add(deltas.DecisionInduced);
            var visibleAfter = budgetBefore.Add(deltas.Visible);

            if (incidentId is not null
                || serviceWitnesses.Length != 0
                || !ProjectionEquals(exogenousProjection, safetyProjection)
                || deltas.DecisionInduced != CommitmentVector.Zero
                || deltas.Exogenous != deltas.Visible
                || (!decisionAfter.IsSuccess || decisionAfter.Value != attemptedBudgetAfter)
                    && (!visibleAfter.IsSuccess || visibleAfter.Value != attemptedBudgetAfter))
            {
                throw new ArgumentException(
                    "A forced-reference breach keeps the route: identical projections, zero " +
                    "decision delta, and a budget charged by the policy's basis.");
            }

            if (witnesses.Any(value => !ForcedReferenceWitnessCodes.Contains(value)))
            {
                throw new ArgumentException(
                    "A forced-reference breach names only commitment-gate codes.",
                    nameof(witnessCodes));
            }
        }
        else if (kind == CommitmentBreachKind.ForcedNoWorse)
        {
            // A changed route of the same vehicle: an unchanged one is a ForcedReference record.
            // The budget is charged by the policy's basis, as for any other publication.
            var decisionAfter = budgetBefore.Add(deltas.DecisionInduced);
            var visibleAfter = budgetBefore.Add(deltas.Visible);

            if (incidentId is not null
                || serviceWitnesses.Length != 0
                || ProjectionEquals(exogenousProjection, safetyProjection)
                || exogenousProjection.VehicleId != safetyProjection.VehicleId
                || (!decisionAfter.IsSuccess || decisionAfter.Value != attemptedBudgetAfter)
                    && (!visibleAfter.IsSuccess || visibleAfter.Value != attemptedBudgetAfter))
            {
                throw new ArgumentException(
                    "A no-worse forced breach changes the route of the same vehicle and charges the " +
                    "budget by the policy's basis.");
            }

            if (witnesses.Any(value => !ForcedReferenceWitnessCodes.Contains(value)))
            {
                throw new ArgumentException(
                    "A no-worse forced breach names only commitment-gate codes.",
                    nameof(witnessCodes));
            }
        }
        else
        {
            if (incidentId is null || serviceWitnesses.Length != 0)
            {
                throw new ArgumentException(
                    "An operational breach requires an incident and cannot carry exogenous witnesses.");
            }

            var decisionAfter = budgetBefore.Add(deltas.DecisionInduced);
            var visibleAfter = budgetBefore.Add(deltas.Visible);

            if ((!decisionAfter.IsSuccess
                    || decisionAfter.Value != attemptedBudgetAfter)
                && (!visibleAfter.IsSuccess
                    || visibleAfter.Value != attemptedBudgetAfter))
            {
                throw new ArgumentException(
                    "Attempted breach budget must equal budgetBefore plus the " +
                    "decision-induced or customer-visible charged delta.",
                    nameof(attemptedBudgetAfter));
            }
        }

        BreachId = DomainIdentifier.Require(breachId, nameof(breachId));
        Kind = kind;
        IncidentId = incidentId;
        RequestId = requestId;
        PreviousPromise = previousPromise;
        ExogenousProjection = exogenousProjection;
        SafetyProjection = safetyProjection;
        Deltas = deltas;
        BudgetBefore = budgetBefore;
        AttemptedBudgetAfter = attemptedBudgetAfter;
        WitnessCodes = Array.AsReadOnly(witnesses);
        ServiceQualityWitnesses = Array.AsReadOnly(serviceWitnesses);
        SourceEventSequence = sourceEventSequence;
        RecordedEpoch = recordedEpoch;
        RecordedAt = recordedAt;
    }

    public string BreachId { get; }

    public CommitmentBreachKind Kind { get; }

    public IncidentId? IncidentId { get; }

    public RequestId RequestId { get; }

    public PublishedPromise PreviousPromise { get; }

    public PromiseProjection ExogenousProjection { get; }

    public PromiseProjection SafetyProjection { get; }

    public ThreeWayPromiseDelta Deltas { get; }

    public CommitmentVector BudgetBefore { get; }

    public CommitmentVector AttemptedBudgetAfter { get; }

    public IReadOnlyList<string> WitnessCodes { get; }

    public IReadOnlyList<ServiceQualityBreach> ServiceQualityWitnesses { get; }

    public long SourceEventSequence { get; }

    public long RecordedEpoch { get; }

    public SimTime RecordedAt { get; }

    public bool NormalOperation => false;

    /// <summary>
    /// The gate codes a forced-reference breach may name. A phase lock is absent on
    /// purpose: locks compare a candidate with its exogenous projection, which a kept
    /// route equals, so a lock on a kept route would be a defect and must fail closed.
    /// The per-decision revision limit is absent for the same reason.
    /// </summary>
    public static IReadOnlySet<string> ForcedReferenceWitnessCodes { get; } =
        new[]
        {
            CommitmentFailureCodes.BudgetExceeded,
            CommitmentFailureCodes.DeadlineExceeded,
        }.ToFrozenSet(StringComparer.Ordinal);

    /// <param name="exogenousProjection">The projection of the reduced route.</param>
    /// <param name="keptRouteProjection">
    /// The projection that is published for the kept route; it must equal the exogenous one.
    /// </param>
    public static CommitmentBreachRecord CreateForcedReference(
        string breachId,
        RequestId requestId,
        PublishedPromise previousPromise,
        PromiseProjection exogenousProjection,
        PromiseProjection keptRouteProjection,
        ThreeWayPromiseDelta deltas,
        CommitmentVector budgetBefore,
        CommitmentVector budgetAfter,
        IEnumerable<string> witnessCodes,
        long sourceEventSequence,
        long recordedEpoch,
        SimTime recordedAt) =>
        new(
            breachId,
            CommitmentBreachKind.ForcedReference,
            null,
            requestId,
            previousPromise,
            exogenousProjection,
            keptRouteProjection,
            deltas,
            budgetBefore,
            budgetAfter,
            witnessCodes,
            [],
            sourceEventSequence,
            recordedEpoch,
            recordedAt);

    /// <param name="exogenousProjection">The projection of the reduced (kept) route.</param>
    /// <param name="publishedProjection">
    /// The projection published for the changed route; it must differ from the exogenous one.
    /// </param>
    public static CommitmentBreachRecord CreateForcedNoWorse(
        string breachId,
        RequestId requestId,
        PublishedPromise previousPromise,
        PromiseProjection exogenousProjection,
        PromiseProjection publishedProjection,
        ThreeWayPromiseDelta deltas,
        CommitmentVector budgetBefore,
        CommitmentVector budgetAfter,
        IEnumerable<string> witnessCodes,
        long sourceEventSequence,
        long recordedEpoch,
        SimTime recordedAt) =>
        new(
            breachId,
            CommitmentBreachKind.ForcedNoWorse,
            null,
            requestId,
            previousPromise,
            exogenousProjection,
            publishedProjection,
            deltas,
            budgetBefore,
            budgetAfter,
            witnessCodes,
            [],
            sourceEventSequence,
            recordedEpoch,
            recordedAt);

    public static CommitmentBreachRecord CreateExogenousServiceQuality(
        string breachId,
        RequestId requestId,
        PublishedPromise previousPromise,
        PromiseProjection exogenousProjection,
        PromiseProjection safetyProjection,
        ThreeWayPromiseDelta deltas,
        CommitmentVector budgetBefore,
        CommitmentVector attemptedBudgetAfter,
        IEnumerable<string> witnessCodes,
        IEnumerable<ServiceQualityBreach> serviceQualityWitnesses,
        long sourceEventSequence,
        long recordedEpoch,
        SimTime recordedAt)
    {
        ArgumentNullException.ThrowIfNull(witnessCodes);
        ArgumentNullException.ThrowIfNull(serviceQualityWitnesses);
        var materialized = serviceQualityWitnesses.ToArray();

        return new CommitmentBreachRecord(
            breachId,
            CommitmentBreachKind.ExogenousServiceQuality,
            null,
            requestId,
            previousPromise,
            exogenousProjection,
            safetyProjection,
            deltas,
            budgetBefore,
            attemptedBudgetAfter,
            witnessCodes,
            materialized,
            sourceEventSequence,
            recordedEpoch,
            recordedAt);
    }

    /// <summary>Field-by-field equality of two projections, as the breach invariants use it.</summary>
    public static bool ProjectionsEqual(
        PromiseProjection left,
        PromiseProjection right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return ProjectionEquals(left, right);
    }

    private static bool ProjectionEquals(
        PromiseProjection left,
        PromiseProjection right) =>
        left.RequestId == right.RequestId
        && left.VehicleId == right.VehicleId
        && left.PickupStopId == right.PickupStopId
        && left.PickupNodeId == right.PickupNodeId
        && left.DropStopId == right.DropStopId
        && left.DropNodeId == right.DropNodeId
        && left.PickupEta == right.PickupEta
        && left.DropEta == right.DropEta
        && left.ServiceOrder.SequenceEqual(right.ServiceOrder);
}

public sealed record IncidentLedgerResult
{
    private IncidentLedgerResult(
        OperationalIncidentLedger? ledger,
        DomainFailure? failure)
    {
        Ledger = ledger;
        Failure = failure;
    }

    public bool IsSuccess => Ledger is not null;

    public OperationalIncidentLedger? Ledger { get; }

    public DomainFailure? Failure { get; }

    public static IncidentLedgerResult Success(OperationalIncidentLedger ledger) =>
        new(ledger, null);

    public static IncidentLedgerResult Fail(
        string code,
        string message,
        string? entityId = null,
        string? dimension = null) =>
        new(null, new DomainFailure(code, message, entityId, dimension));
}

public sealed class OperationalIncidentLedger
{
    private readonly FrozenDictionary<IncidentId, OperationalIncident> _incidents;
    private readonly IReadOnlyList<CommitmentBreachRecord> _breaches;
    private readonly IReadOnlyList<ObservedLatePickup> _latePickups;

    private OperationalIncidentLedger(
        IEnumerable<KeyValuePair<IncidentId, OperationalIncident>> incidents,
        IEnumerable<CommitmentBreachRecord> breaches,
        IEnumerable<ObservedLatePickup> latePickups)
    {
        _incidents = incidents.ToFrozenDictionary();
        _breaches = Array.AsReadOnly(breaches.ToArray());
        _latePickups = Array.AsReadOnly(latePickups.ToArray());
    }

    public static OperationalIncidentLedger Empty { get; } = new([], [], []);

    public IReadOnlyDictionary<IncidentId, OperationalIncident> Incidents =>
        _incidents;

    public IReadOnlyList<CommitmentBreachRecord> Breaches => _breaches;

    /// <summary>Late boardings in recording order; at most one per rider.</summary>
    public IReadOnlyList<ObservedLatePickup> LatePickups => _latePickups;

    public IncidentLedgerResult RecordLatePickup(ObservedLatePickup latePickup)
    {
        ArgumentNullException.ThrowIfNull(latePickup);

        if (_latePickups.Any(value => value.RequestId == latePickup.RequestId))
        {
            return IncidentLedgerResult.Fail(
                IncidentFailureCodes.DuplicateLatePickup,
                "A rider boards once, so at most one late pickup is recorded.",
                latePickup.RequestId.Value,
                "requestId");
        }

        return IncidentLedgerResult.Success(
            new OperationalIncidentLedger(
                _incidents,
                _breaches,
                _latePickups.Append(latePickup)));
    }

    public IncidentLedgerResult Open(
        IncidentId incidentId,
        string reasonCode,
        IEnumerable<VehicleId> affectedVehicleIds,
        IEnumerable<RequestId> affectedRequestIds,
        long eventSequence,
        SimTime openedAt)
    {
        if (_incidents.ContainsKey(incidentId))
        {
            return IncidentLedgerResult.Fail(
                IncidentFailureCodes.DuplicateIncident,
                "An incident identifier cannot be opened more than once.",
                incidentId.Value,
                "incidentId");
        }

        var incident = new OperationalIncident(
            incidentId,
            reasonCode,
            affectedVehicleIds,
            affectedRequestIds,
            eventSequence,
            openedAt);

        return IncidentLedgerResult.Success(
            new OperationalIncidentLedger(
                _incidents.Append(
                    new KeyValuePair<IncidentId, OperationalIncident>(
                        incidentId,
                        incident)),
                _breaches,
                _latePickups));
    }

    public IncidentLedgerResult Resolve(
        IncidentId incidentId,
        long eventSequence,
        SimTime resolvedAt)
    {
        if (!_incidents.TryGetValue(incidentId, out var incident))
        {
            return IncidentLedgerResult.Fail(
                IncidentFailureCodes.UnknownIncident,
                "Only a known open incident can be resolved.",
                incidentId.Value,
                "incidentId");
        }

        if (!incident.IsOpen)
        {
            return IncidentLedgerResult.Fail(
                IncidentFailureCodes.StaleIncidentTransition,
                "The incident has already been resolved.",
                incidentId.Value,
                "incidentId");
        }

        return IncidentLedgerResult.Success(
            new OperationalIncidentLedger(
                _incidents
                    .Where(pair => pair.Key != incidentId)
                    .Append(
                        new KeyValuePair<IncidentId, OperationalIncident>(
                            incidentId,
                            incident.Resolve(eventSequence, resolvedAt))),
                _breaches,
                _latePickups));
    }

    public IncidentLedgerResult AppendBreach(CommitmentBreachRecord breach)
    {
        ArgumentNullException.ThrowIfNull(breach);

        if (_breaches.Any(
                value => string.Equals(
                    value.BreachId,
                    breach.BreachId,
                    StringComparison.Ordinal)))
        {
            return IncidentLedgerResult.Fail(
                IncidentFailureCodes.DuplicateBreach,
                "A breach identifier cannot be appended more than once.",
                breach.BreachId,
                "breachId");
        }

        if (breach.Kind is CommitmentBreachKind.ExogenousServiceQuality
            or CommitmentBreachKind.ForcedReference
            or CommitmentBreachKind.ForcedNoWorse)
        {
            return IncidentLedgerResult.Success(
                new OperationalIncidentLedger(
                    _incidents,
                    _breaches.Append(breach),
                    _latePickups));
        }

        var incidentId = breach.IncidentId!.Value;

        if (!_incidents.TryGetValue(incidentId, out var incident)
            || !incident.IsOpen)
        {
            return IncidentLedgerResult.Fail(
                IncidentFailureCodes.IncidentNotOpen,
                "A breach can only be recorded against an open incident.",
                incidentId.Value,
                "incidentId");
        }

        if (!incident.AffectedRequestIds.Contains(breach.RequestId))
        {
            return IncidentLedgerResult.Fail(
                IncidentFailureCodes.RiderNotAffected,
                "The breach request is not in the incident's affected-rider set.",
                breach.RequestId.Value,
                "requestId");
        }

        if (!incident.AffectedVehicleIds.Contains(
                breach.PreviousPromise.Projection.VehicleId)
            || !incident.AffectedVehicleIds.Contains(
                breach.ExogenousProjection.VehicleId)
            || breach.SourceEventSequence < incident.OpenedEventSequence
            || breach.RecordedAt.Milliseconds < incident.OpenedAt.Milliseconds)
        {
            return IncidentLedgerResult.Fail(
                IncidentFailureCodes.BreachIncidentMismatch,
                "The breach must follow the incident and originate from one " +
                "of its affected vehicles.",
                breach.BreachId,
                "incidentId");
        }

        return IncidentLedgerResult.Success(
            new OperationalIncidentLedger(
                _incidents,
                _breaches.Append(breach),
                _latePickups));
    }
}

public static class IncidentFailureCodes
{
    public const string DuplicateIncident = "DUPLICATE_INCIDENT";
    public const string UnknownIncident = "UNKNOWN_INCIDENT";
    public const string StaleIncidentTransition = "STALE_INCIDENT_TRANSITION";
    public const string UnknownIncidentVehicle = "UNKNOWN_INCIDENT_VEHICLE";
    public const string DuplicateBreach = "DUPLICATE_COMMITMENT_BREACH";
    public const string IncidentNotOpen = "INCIDENT_NOT_OPEN";
    public const string RiderNotAffected = "INCIDENT_RIDER_NOT_AFFECTED";
    public const string BreachIncidentMismatch = "BREACH_INCIDENT_MISMATCH";
    public const string DuplicateLatePickup = "DUPLICATE_LATE_PICKUP";
}
