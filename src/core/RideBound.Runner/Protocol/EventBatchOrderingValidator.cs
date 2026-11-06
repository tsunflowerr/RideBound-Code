using RideBound.Contracts.Protocol;

namespace RideBound.Runner.Protocol;

public sealed record EventBatchOrderingState(
    long PreviousAppliedEpoch,
    long NextEventSequence,
    long LastSimulationTimeMilliseconds);

public sealed record EventBatchOrderingError(
    string ProtocolCode,
    string Message);

public static class EventBatchOrderingValidator
{
    public static EventBatchOrderingError? Validate(
        ProtocolEnvelope envelope,
        EventBatchPayload payload,
        EventBatchOrderingState state)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(state);

        if (envelope.MessageType.Value != "eventBatch"
            || envelope.EpochId is null
            || envelope.SimTime is null)
        {
            return new EventBatchOrderingError(
                "SCHEMA_VALIDATION_FAILED",
                "eventBatch requires complete epoch context.");
        }

        var expectedEpoch = state.PreviousAppliedEpoch + 1;

        if (envelope.EpochId.Value.Value != expectedEpoch)
        {
            return new EventBatchOrderingError(
                "EPOCH_GAP",
                $"Expected epoch {expectedEpoch}.");
        }

        if (envelope.SimTime.Value.Value < state.LastSimulationTimeMilliseconds)
        {
            return new EventBatchOrderingError(
                "EPOCH_GAP",
                "Simulation time cannot decrease between epochs.");
        }

        var expectedSequence = state.NextEventSequence;

        foreach (var protocolEvent in payload.Events)
        {
            var actual = protocolEvent.EventSequence.Value;

            if (actual < expectedSequence)
            {
                return new EventBatchOrderingError(
                    "EVENT_SEQUENCE_OVERLAP",
                    $"Event sequence {actual} overlaps the processed transcript.");
            }

            if (actual > expectedSequence)
            {
                return new EventBatchOrderingError(
                    "EVENT_SEQUENCE_GAP",
                    $"Expected event sequence {expectedSequence}.");
            }

            if (actual == ProtocolLimits.MaxCanonicalInteger)
            {
                return new EventBatchOrderingError(
                    "EVENT_SEQUENCE_GAP",
                    "The event sequence exhausts the protocol v1 integer range.");
            }

            expectedSequence++;
        }

        return null;
    }
}
