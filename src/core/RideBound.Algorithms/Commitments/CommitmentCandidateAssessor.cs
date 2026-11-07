using RideBound.Application.Commitments;
using RideBound.Application.State;
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
