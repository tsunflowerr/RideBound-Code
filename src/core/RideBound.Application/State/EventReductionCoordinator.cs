using RideBound.Application.Events;

namespace RideBound.Application.State;

public sealed class EventReductionCoordinator
{
    private readonly EventReducer _reducer;
    private OnlineState? _pendingState;

    public EventReductionCoordinator(
        OnlineState initialState,
        EventReducer? reducer = null)
    {
        ArgumentNullException.ThrowIfNull(initialState);
        CommittedState = initialState;
        _reducer = reducer ?? new EventReducer();
    }

    public OnlineState CommittedState { get; private set; }

    public OnlineState? PendingState => _pendingState;

    public EventReductionResult Propose(InternalEventBatch batch)
    {
        if (_pendingState is not null)
        {
            return EventReductionResult.Failure(
                new EventReductionWitness(
                    EventReductionFailureCodes.PendingTransitionExists,
                    "A proposed state is already waiting for decisionApplied."));
        }

        var result = _reducer.Reduce(CommittedState, batch);

        if (result.IsSuccess)
        {
            _pendingState = result.ProposedState;
        }

        return result;
    }

    public EventReductionResult StageDecisionState(
        OnlineState reducedState,
        OnlineState proposedDecisionState)
    {
        ArgumentNullException.ThrowIfNull(reducedState);
        ArgumentNullException.ThrowIfNull(proposedDecisionState);

        if (_pendingState is null)
        {
            return EventReductionResult.Failure(
                new EventReductionWitness(
                    EventReductionFailureCodes.NoPendingTransition,
                    "There is no reduced state waiting for a decision."));
        }

        if (!ReferenceEquals(_pendingState, reducedState))
        {
            return EventReductionResult.Failure(
                new EventReductionWitness(
                    EventReductionFailureCodes.DecisionStateMismatch,
                    "Decision input is not the pending reduced state."));
        }

        if (proposedDecisionState.Run.Id != reducedState.Run.Id
            || proposedDecisionState.Run.ScenarioId
                != reducedState.Run.ScenarioId
            || proposedDecisionState.Run.AppliedEpoch
                != reducedState.Run.AppliedEpoch
            || proposedDecisionState.Run.SimulationTime
                != reducedState.Run.SimulationTime
            || proposedDecisionState.NextEventSequence
                != reducedState.NextEventSequence
            || proposedDecisionState.TravelTimes != reducedState.TravelTimes
            || !string.Equals(
                proposedDecisionState.ExpectedInitialTravelTimeSnapshotHash,
                reducedState.ExpectedInitialTravelTimeSnapshotHash,
                StringComparison.Ordinal))
        {
            return EventReductionResult.Failure(
                new EventReductionWitness(
                    EventReductionFailureCodes.DecisionStateMismatch,
                    "A decision may change only core request/vehicle plan state " +
                    "inside the pending epoch."));
        }

        _pendingState = proposedDecisionState;
        return EventReductionResult.Success(proposedDecisionState);
    }

    public EventReductionResult DiscardPendingProposal()
    {
        if (_pendingState is null)
        {
            return EventReductionResult.Failure(
                new EventReductionWitness(
                    EventReductionFailureCodes.NoPendingTransition,
                    "There is no pending proposal to discard."));
        }

        _pendingState = null;
        return EventReductionResult.Success(CommittedState);
    }

    public EventReductionResult ApplyDecisionAcknowledgement(long epoch)
    {
        if (_pendingState is null)
        {
            return EventReductionResult.Failure(
                new EventReductionWitness(
                    EventReductionFailureCodes.NoPendingTransition,
                    "There is no proposed state to acknowledge."));
        }

        if (_pendingState.Run.AppliedEpoch != epoch)
        {
            return EventReductionResult.Failure(
                new EventReductionWitness(
                    EventReductionFailureCodes.AcknowledgementMismatch,
                    "decisionApplied epoch does not match the proposed state.",
                    Dimension: "epoch"));
        }

        CommittedState = _pendingState;
        _pendingState = null;
        return EventReductionResult.Success(CommittedState);
    }
}
