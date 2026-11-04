using System.Collections.Immutable;
using RideBound.Domain.Common;

namespace RideBound.Domain.Commitments;

public enum CommitmentLedgerEntryKind
{
    InitialPromise,
    Revision,
}

public sealed record CommitmentLedgerEntry
{
    public CommitmentLedgerEntry(
        string publicationId,
        CommitmentLedgerEntryKind kind,
        PublishedPromise publishedPromise,
        PublishedPromise? previousPromise,
        PromiseProjection exogenousProjection,
        ThreeWayPromiseDelta deltas,
        CommitmentVector budgetBefore,
        CommitmentVector budgetAfter,
        CommitmentBudgetBasis? budgetBasis,
        string reasonCode,
        long sourceEventSequence)
    {
        ArgumentNullException.ThrowIfNull(publishedPromise);
        ArgumentNullException.ThrowIfNull(exogenousProjection);
        ArgumentNullException.ThrowIfNull(deltas);
        ArgumentNullException.ThrowIfNull(budgetBefore);
        ArgumentNullException.ThrowIfNull(budgetAfter);

        if (budgetBasis is not null && !Enum.IsDefined(budgetBasis.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(budgetBasis));
        }

        if (sourceEventSequence is < 1 or > DomainLimits.MaxCanonicalInteger)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceEventSequence));
        }

        PublicationId = DomainIdentifier.Require(
            publicationId,
            nameof(publicationId));
        ReasonCode = DomainIdentifier.Require(reasonCode, nameof(reasonCode));
        Kind = kind;
        PublishedPromise = publishedPromise;
        PreviousPromise = previousPromise;
        ExogenousProjection = exogenousProjection;
        Deltas = deltas;
        BudgetBefore = budgetBefore;
        BudgetAfter = budgetAfter;
        BudgetBasis = budgetBasis;
        SourceEventSequence = sourceEventSequence;
    }

    public string PublicationId { get; }

    public CommitmentLedgerEntryKind Kind { get; }

    public PublishedPromise PublishedPromise { get; }

    public PublishedPromise? PreviousPromise { get; }

    public PromiseProjection ExogenousProjection { get; }

    public ThreeWayPromiseDelta Deltas { get; }

    public CommitmentVector BudgetBefore { get; }

    public CommitmentVector BudgetAfter { get; }

    public CommitmentBudgetBasis? BudgetBasis { get; }

    public string ReasonCode { get; }

    public long SourceEventSequence { get; }
}

public sealed class RiderCommitmentHistory
{
    public RiderCommitmentHistory(IEnumerable<CommitmentLedgerEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var materialized = entries.ToArray();

        if (materialized.Length == 0)
        {
            throw new ArgumentException(
                "Rider commitment history cannot be empty.",
                nameof(entries));
        }

        RequestId = materialized[0].PublishedPromise.Projection.RequestId;

        if (materialized.Any(
                entry => entry.PublishedPromise.Projection.RequestId != RequestId)
            || materialized.Select(entry => entry.PublicationId).Distinct(
                    StringComparer.Ordinal).Count() != materialized.Length)
        {
            throw new ArgumentException(
                "Rider commitment entries require one request and unique publications.",
                nameof(entries));
        }

        Entries = Array.AsReadOnly(materialized);
    }

    public RequestId RequestId { get; }

    public IReadOnlyList<CommitmentLedgerEntry> Entries { get; }

    public CommitmentLedgerEntry Current => Entries[^1];
}

public sealed record CommitmentLedgerAppendResult
{
    private CommitmentLedgerAppendResult(
        CommitmentLedger? ledger,
        DomainFailure? failure)
    {
        Ledger = ledger;
        Failure = failure;
    }

    public bool IsSuccess => Ledger is not null;

    public CommitmentLedger? Ledger { get; }

    public DomainFailure? Failure { get; }

    public static CommitmentLedgerAppendResult Success(
        CommitmentLedger ledger) =>
        new(ledger, null);

    public static CommitmentLedgerAppendResult Fail(
        string message,
        string? entityId = null,
        string? dimension = null) =>
        new(
            null,
            new DomainFailure(
                CommitmentFailureCodes.LedgerConflict,
                message,
                entityId,
                dimension));
}

/// <remarks>
/// ADR-089: the ledger is persistent. Each append shares the unchanged histories and keeps the
/// set of every publication identifier, so an append no longer rebuilds the history map or scans
/// every entry of every rider for a duplicate publication (on a Via job, 79,000 entries and about
/// 27 appends per decision). The enumeration order of <see cref="Histories"/> was already
/// unspecified (hash order of the request identifiers); every reader sorts or looks up by key.
/// </remarks>
public sealed class CommitmentLedger
{
    private readonly ImmutableDictionary<RequestId, RiderCommitmentHistory> _histories;
    private readonly ImmutableHashSet<string> _publicationIds;
    private readonly ImmutableDictionary<RequestId, ShadowAllocation> _shadowAllocations;

    private CommitmentLedger(
        ImmutableDictionary<RequestId, RiderCommitmentHistory> histories,
        ImmutableHashSet<string> publicationIds,
        ImmutableDictionary<RequestId, ShadowAllocation> shadowAllocations)
    {
        _histories = histories;
        _publicationIds = publicationIds;
        _shadowAllocations = shadowAllocations;
    }

    public IReadOnlyDictionary<RequestId, RiderCommitmentHistory> Histories =>
        _histories;

    /// <summary>
    /// ADR-089 GĐ4: Shadow Control class of every rider with a promise, frozen at its first
    /// promise; empty unless a shadow-allocated policy runs (then the canonical state is as before).
    /// </summary>
    public IReadOnlyDictionary<RequestId, ShadowAllocation> ShadowAllocations =>
        _shadowAllocations;

    public static CommitmentLedger Empty { get; } = new(
        ImmutableDictionary<RequestId, RiderCommitmentHistory>.Empty,
        ImmutableHashSet.Create<string>(StringComparer.Ordinal),
        ImmutableDictionary<RequestId, ShadowAllocation>.Empty);

    /// <summary>Records the class of a rider whose initial promise is in this ledger; once only.</summary>
    public CommitmentLedger WithShadowAllocation(RequestId requestId, ShadowAllocation allocation)
    {
        ArgumentNullException.ThrowIfNull(allocation);

        if (!_histories.ContainsKey(requestId))
        {
            throw new InvalidOperationException("A shadow class needs the rider's initial promise.");
        }

        if (_shadowAllocations.ContainsKey(requestId))
        {
            throw new InvalidOperationException("A rider's shadow class is frozen at its first promise.");
        }

        return new CommitmentLedger(
            _histories,
            _publicationIds,
            _shadowAllocations.Add(requestId, allocation));
    }

    public ShadowAllocation? ShadowAllocationOf(RequestId requestId) =>
        _shadowAllocations.TryGetValue(requestId, out var allocation) ? allocation : null;

    public CommitmentLedgerAppendResult OpenInitial(
        string publicationId,
        PromiseProjection projection,
        long publishedEpoch,
        SimTime publishedAt,
        string reasonCode,
        long sourceEventSequence)
    {
        ArgumentNullException.ThrowIfNull(projection);

        if (ContainsPublication(publicationId))
        {
            return DuplicatePublication(publicationId);
        }

        if (_histories.ContainsKey(projection.RequestId))
        {
            return CommitmentLedgerAppendResult.Fail(
                "An initial promise already exists for the request.",
                projection.RequestId.Value,
                "promiseVersion");
        }

        var promise = new PublishedPromise(
            new PromiseVersion(1),
            publishedEpoch,
            publishedAt,
            projection);
        var entry = new CommitmentLedgerEntry(
            publicationId,
            CommitmentLedgerEntryKind.InitialPromise,
            promise,
            null,
            projection,
            new ThreeWayPromiseDelta(
                CommitmentVector.Zero,
                CommitmentVector.Zero,
                CommitmentVector.Zero),
            CommitmentVector.Zero,
            CommitmentVector.Zero,
            null,
            reasonCode,
            sourceEventSequence);
        var history = new RiderCommitmentHistory([entry]);

        return CommitmentLedgerAppendResult.Success(
            new CommitmentLedger(
                _histories.Add(projection.RequestId, history),
                _publicationIds.Add(publicationId),
                _shadowAllocations));
    }

    public CommitmentLedgerAppendResult AppendRevision(
        string publicationId,
        RequestId requestId,
        PromiseVersion expectedVersion,
        PromiseProjection exogenousProjection,
        PromiseProjection publishedProjection,
        ThreeWayPromiseDelta deltas,
        CommitmentBudgetBasis budgetBasis,
        long publishedEpoch,
        SimTime publishedAt,
        string reasonCode,
        long sourceEventSequence)
    {
        ArgumentNullException.ThrowIfNull(exogenousProjection);
        ArgumentNullException.ThrowIfNull(publishedProjection);
        ArgumentNullException.ThrowIfNull(deltas);

        if (!Enum.IsDefined(budgetBasis))
        {
            return CommitmentLedgerAppendResult.Fail(
                "Promise revision has an unknown budget basis.",
                requestId.Value,
                "budgetBasis");
        }

        if (ContainsPublication(publicationId))
        {
            return DuplicatePublication(publicationId);
        }

        if (!_histories.TryGetValue(requestId, out var history))
        {
            return CommitmentLedgerAppendResult.Fail(
                "The request has no initial promise.",
                requestId.Value,
                "promiseVersion");
        }

        var current = history.Current;

        if (current.PublishedPromise.Version != expectedVersion
            || exogenousProjection.RequestId != requestId
            || publishedProjection.RequestId != requestId
            || publishedEpoch < current.PublishedPromise.PublishedEpoch
            || publishedAt.Milliseconds
                < current.PublishedPromise.PublishedAt.Milliseconds)
        {
            return CommitmentLedgerAppendResult.Fail(
                "Promise revision conflicts with the current ledger version.",
                requestId.Value,
                "promiseVersion");
        }

        PromiseVersion nextVersion;

        try
        {
            nextVersion = expectedVersion.Next();
        }
        catch (OverflowException)
        {
            return CommitmentLedgerAppendResult.Fail(
                "Promise version cannot advance.",
                requestId.Value,
                "promiseVersion");
        }

        var charged = budgetBasis == CommitmentBudgetBasis.DecisionInduced
            ? deltas.DecisionInduced
            : deltas.Visible;
        var after = current.BudgetAfter.Add(charged);

        if (!after.IsSuccess)
        {
            return CommitmentLedgerAppendResult.Fail(
                after.Failure!.Message,
                requestId.Value,
                after.Failure.Dimension);
        }

        var promise = new PublishedPromise(
            nextVersion,
            publishedEpoch,
            publishedAt,
            publishedProjection);
        var entry = new CommitmentLedgerEntry(
            publicationId,
            CommitmentLedgerEntryKind.Revision,
            promise,
            current.PublishedPromise,
            exogenousProjection,
            deltas,
            current.BudgetAfter,
            after.Value!,
            budgetBasis,
            reasonCode,
            sourceEventSequence);
        var updatedHistory = new RiderCommitmentHistory(
            history.Entries.Append(entry));

        return CommitmentLedgerAppendResult.Success(
            new CommitmentLedger(
                _histories.SetItem(requestId, updatedHistory),
                _publicationIds.Add(publicationId),
                _shadowAllocations));
    }

    // Every entry enters the ledger through OpenInitial or AppendRevision, which add its
    // publication identifier here, so this is the set of every entry's identifier.
    private bool ContainsPublication(string publicationId) =>
        _publicationIds.Contains(publicationId);

    private static CommitmentLedgerAppendResult DuplicatePublication(
        string publicationId) =>
        CommitmentLedgerAppendResult.Fail(
            "A commitment publication identifier already exists.",
            publicationId,
            "publicationId");
}
