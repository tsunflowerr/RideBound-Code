using System.Text.Json;

namespace RideBound.Contracts.Protocol;

public sealed record ProtocolEnvelope(
    ProtocolVersion SchemaVersion,
    ProtocolMessageType MessageType,
    JsonElement Payload,
    RunId? RunId = null,
    ScenarioId? ScenarioId = null,
    EpochId? EpochId = null,
    SimulationTimeMilliseconds? SimTime = null);

public enum ProtocolEnvelopeErrorCode
{
    MalformedJson,
    RootMustBeObject,
    DuplicateField,
    UnknownField,
    MissingRequiredField,
    InvalidFieldType,
    InvalidSchemaVersion,
    UnknownMessageType,
    InvalidIdentifier,
    ValueOutOfRange,
    FieldNotAllowed,
    UnsupportedSchemaMajor,
    UnsupportedSchemaMinor,
}

public sealed record ProtocolEnvelopeError(
    ProtocolEnvelopeErrorCode Code,
    string Field,
    string Message,
    ProtocolVersion? SchemaVersion = null)
{
    public ProtocolFailureDisposition Disposition =>
        Code == ProtocolEnvelopeErrorCode.UnsupportedSchemaMajor
            ? ProtocolFailureDisposition.FailSession
            : ProtocolFailureDisposition.RejectMessage;
}

public enum ProtocolFailureDisposition
{
    RejectMessage,
    FailSession,
    TerminateProcess,
}

public sealed record ProtocolEnvelopeDecodeResult
{
    private ProtocolEnvelopeDecodeResult(
        ProtocolEnvelope? envelope,
        ProtocolEnvelopeError? error)
    {
        Envelope = envelope;
        Error = error;
    }

    public bool IsSuccess => Envelope is not null;

    public ProtocolEnvelope? Envelope { get; }

    public ProtocolEnvelopeError? Error { get; }

    internal static ProtocolEnvelopeDecodeResult Success(ProtocolEnvelope envelope)
    {
        return new ProtocolEnvelopeDecodeResult(envelope, null);
    }

    internal static ProtocolEnvelopeDecodeResult Failure(ProtocolEnvelopeError error)
    {
        return new ProtocolEnvelopeDecodeResult(null, error);
    }
}
