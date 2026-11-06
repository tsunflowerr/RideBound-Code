using System.Collections.Frozen;
using RideBound.Algorithms.Candidates;
using RideBound.Application.Commitments;
using RideBound.Application.State;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Validation;

namespace RideBound.Algorithms.Commitments;

public sealed record CommitmentMechanismContext(
    OnlineState BeforeEventState,
    OnlineState ReducedState,
    ICommitmentPolicyProvider Policies,
    IStopDistanceLookup StopDistances,
    string PublicationScope,
    long SourceEventSequence,
    InitialPromiseTrigger InitialPromiseTrigger =
        InitialPromiseTrigger.InitialAcceptance,
    bool CollectAllCommitmentWitnesses = false);

/// <summary>
/// Rebuilds a named mechanism baseline from the configured material-revision
/// rule while deliberately removing cumulative limits. Freeze locks are added
/// only when the caller explicitly supplies both horizon and lock fields.
/// </summary>
public sealed class MechanismCommitmentPolicyProvider
    : ICommitmentPolicyProvider
{
    private readonly ICommitmentPolicyProvider _source;
    private readonly Duration? _freezeHorizon;
    private readonly PromiseLock _freezeLocks;
    private readonly object _gate = new();
    private readonly Dictionary<string, CommitmentPolicy> _cache =
        new(StringComparer.Ordinal);

    private MechanismCommitmentPolicyProvider(
        ICommitmentPolicyProvider source,
        Duration? freezeHorizon,
        PromiseLock freezeLocks)
    {
        _source = source;
        _freezeHorizon = freezeHorizon;
        _freezeLocks = freezeLocks;
    }

    public static MechanismCommitmentPolicyProvider RevisionPenalty(
        ICommitmentPolicyProvider source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new MechanismCommitmentPolicyProvider(
            source,
            null,
            PromiseLock.None);
    }

    public static MechanismCommitmentPolicyProvider FixedFreeze(
        ICommitmentPolicyProvider source,
        Duration freezeHorizon,
        PromiseLock freezeLocks)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (freezeHorizon.Milliseconds == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(freezeHorizon));
        }

        if (freezeLocks == PromiseLock.None
            || (freezeLocks & ~AllPromiseLocks) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(freezeLocks));
        }

        return new MechanismCommitmentPolicyProvider(
            source,
            freezeHorizon,
            freezeLocks);
    }

    public bool TryGetPolicy(string policyId, out CommitmentPolicy policy)
    {
        lock (_gate)
        {
            if (_cache.TryGetValue(policyId, out policy!))
            {
                return true;
            }

            if (!_source.TryGetPolicy(policyId, out var sourcePolicy)
                || !StringComparer.Ordinal.Equals(sourcePolicy.PolicyId, policyId))
            {
                policy = null!;
                return false;
            }

            policy = new CommitmentPolicy(
                sourcePolicy.PolicyId,
                sourcePolicy.BudgetBasis,
                CommitmentDimensionVocabulary.Ordered.Select(
                    dimension => new CommitmentDimensionLimit(
                        dimension,
                        hardLimit: null,
                        CommitmentPhase.AllActive)),
                sourcePolicy.MaterialRevisionRule,
                _freezeHorizon,
                _freezeLocks,
                finalConfirmationLocks: PromiseLock.None);
            _cache.Add(policyId, policy);
            return true;
        }
    }

    private const PromiseLock AllPromiseLocks = PromiseLock.Vehicle
        | PromiseLock.PickupStop
        | PromiseLock.DropStop
        | PromiseLock.PickupEta
        | PromiseLock.DropEta;
}

public sealed record CandidateCommitmentAssessment(
    string CandidateId,
    CommitmentVector DecisionInducedRevision);

/// <summary>
/// A machine-readable assessment failure. The optional fields mirror the
/// vocabulary of <c>CommitmentValidationWitness</c> so that a caller never has
/// to parse prose out of <paramref name="Message"/> to learn which request,
/// dimension or bound produced the failure.
/// </summary>
public sealed record CommitmentAssessmentWitness(
    string Code,
    string Message,
    string? CandidateId = null,
    VehicleId? VehicleId = null,
    RequestId? RequestId = null,
    string? Dimension = null,
    string? UnderlyingCode = null,
    long? Before = null,
    long? After = null,
    int? GeneratedCandidateCount = null,
    int? RejectedCandidateCount = null);

public sealed record CandidateCommitmentAssessmentResult
{
    private CandidateCommitmentAssessmentResult(
        IReadOnlyDictionary<string, CandidateCommitmentAssessment>? assessments,
        CommitmentAssessmentWitness? witness)
    {
        Assessments = assessments;
        Witness = witness;
    }

    public bool IsSuccess => Assessments is not null;

    public IReadOnlyDictionary<string, CandidateCommitmentAssessment>? Assessments
    {
        get;
    }

    public CommitmentAssessmentWitness? Witness { get; }

    public static CandidateCommitmentAssessmentResult Success(
        IEnumerable<CandidateCommitmentAssessment> assessments) =>
        new(
            assessments.ToFrozenDictionary(
                assessment => assessment.CandidateId,
                StringComparer.Ordinal),
            null);

    public static CandidateCommitmentAssessmentResult Failure(
        CommitmentAssessmentWitness witness) =>
        new(null, witness);
}

public sealed class CommitmentCandidateAssessor
{
    private readonly CommitmentDecisionValidator _validator;

    public CommitmentCandidateAssessor(
        CommitmentDecisionValidator? validator = null)
    {
        _validator = validator ?? new CommitmentDecisionValidator();
    }

    public CandidateCommitmentAssessmentResult AssessRevisionPenalty(
        CommitmentMechanismContext context,
        IReadOnlyList<VehicleCandidateSet> candidateSets)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(candidateSets);
        var policies = MechanismCommitmentPolicyProvider.RevisionPenalty(
            context.Policies);
        var assessments = new List<CandidateCommitmentAssessment>();

        foreach (var set in candidateSets.OrderBy(
                     value => value.VehicleId.Value,
                     StringComparer.Ordinal))
        {
            foreach (var candidate in set.Candidates.OrderBy(
                         value => value.CandidateId,
                         StringComparer.Ordinal))
            {
                var updated = CandidateStateApplicator.Apply(
                    context.ReducedState.Run,
                    candidate);

                if (!updated.IsSuccess)
                {
                    return Failure(
                        updated.Failure!.Code,
                        updated.Failure.Message,
                        candidate,
                        dimension: updated.Failure.Dimension);
                }

                var validation = _validator.Validate(
                    new CommitmentValidationContext(
                        context.BeforeEventState,
                        context.ReducedState,
                        context.ReducedState with { Run = updated.Value! },
                        policies,
                        context.StopDistances,
                        context.PublicationScope,
                        context.SourceEventSequence,
                        RevisionReasonCode: "B2_REVISION_PENALTY",
                        ScopedVehicleId: set.VehicleId,
                        InitialPromiseTrigger: context.InitialPromiseTrigger));

                if (!validation.IsValid)
                {
                    var value = validation.Witnesses[0];
                    return Failure(
                        value.Code,
                        value.Message,
                        candidate,
                        value.RequestId,
                        value.Dimension);
                }

                var aggregate = CommitmentVector.Zero;

                foreach (var publication in validation.Publications)
                {
                    var added = aggregate.Add(
                        publication.Entry.Deltas.DecisionInduced);

                    if (!added.IsSuccess)
                    {
                        return Failure(
                            added.Failure!.Code,
                            added.Failure.Message,
                            candidate,
                            dimension: added.Failure.Dimension);
                    }

                    aggregate = added.Value!;
                }

                assessments.Add(
                    new CandidateCommitmentAssessment(
                        candidate.CandidateId,
                        aggregate));
            }
        }

        return CandidateCommitmentAssessmentResult.Success(assessments);
    }

    private static CandidateCommitmentAssessmentResult Failure(
        string code,
        string message,
        InsertionCandidate candidate,
        RequestId? requestId = null,
        string? dimension = null) =>
        CandidateCommitmentAssessmentResult.Failure(
            new CommitmentAssessmentWitness(
                code,
                message,
                candidate.CandidateId,
                candidate.VehicleId,
                requestId,
                dimension));
}
