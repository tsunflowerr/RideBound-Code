using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Validation;

namespace RideBound.Domain.Incidents;

/// <summary>
/// A rider observed boarding after the latest pickup of the accepted
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

public sealed record CommitmentBreachRecord
{
    private CommitmentBreachRecord(
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

        if (!ProjectionEquals(exogenousProjection, safetyProjection)
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

        BreachId = DomainIdentifier.Require(breachId, nameof(breachId));
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
    private readonly IReadOnlyList<CommitmentBreachRecord> _breaches;
    private readonly IReadOnlyList<ObservedLatePickup> _latePickups;

    private OperationalIncidentLedger(
        IEnumerable<CommitmentBreachRecord> breaches,
        IEnumerable<ObservedLatePickup> latePickups)
    {
        _breaches = Array.AsReadOnly(breaches.ToArray());
        _latePickups = Array.AsReadOnly(latePickups.ToArray());
    }

    public static OperationalIncidentLedger Empty { get; } = new([], []);

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
                _breaches,
                _latePickups.Append(latePickup)));
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

        return IncidentLedgerResult.Success(
            new OperationalIncidentLedger(
                _breaches.Append(breach),
                _latePickups));
    }
}

public static class IncidentFailureCodes
{
    public const string DuplicateBreach = "DUPLICATE_COMMITMENT_BREACH";
    public const string DuplicateLatePickup = "DUPLICATE_LATE_PICKUP";
}
