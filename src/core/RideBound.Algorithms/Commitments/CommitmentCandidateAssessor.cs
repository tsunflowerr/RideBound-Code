using RideBound.Application.Commitments;
using RideBound.Application.State;
using RideBound.Domain.Validation;

namespace RideBound.Algorithms.Commitments;

public sealed record CommitmentMechanismContext(
    OnlineState BeforeEventState,
    OnlineState ReducedState,
    ICommitmentPolicyProvider Policies,
    string PublicationScope,
    long SourceEventSequence);

/// <summary>
/// An assessment failure: its code and the message the Runner reports.
/// </summary>
public sealed record CommitmentAssessmentWitness(
    string Code,
    string Message);
