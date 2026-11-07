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
/// rule while deliberately removing cumulative limits.
/// </summary>
public sealed class MechanismCommitmentPolicyProvider
    : ICommitmentPolicyProvider
{
    private readonly ICommitmentPolicyProvider _source;
    private readonly object _gate = new();
    private readonly Dictionary<string, CommitmentPolicy> _cache =
        new(StringComparer.Ordinal);

    private MechanismCommitmentPolicyProvider(ICommitmentPolicyProvider source)
    {
        _source = source;
    }

    public static MechanismCommitmentPolicyProvider RevisionPenalty(
        ICommitmentPolicyProvider source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new MechanismCommitmentPolicyProvider(source);
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
                finalConfirmationLocks: PromiseLock.None);
            _cache.Add(policyId, policy);
            return true;
        }
    }
}

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
