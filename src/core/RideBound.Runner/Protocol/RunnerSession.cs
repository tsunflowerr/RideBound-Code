using System.Text.Json;
using RideBound.Algorithms.Dispatch;
using RideBound.Algorithms.Policies;
using RideBound.Application.Commitments;
using RideBound.Application.State;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Domain.Validation;
using RideBound.Runner.Configuration;
using RideBound.Runner.Mapping;
using RideBound.Runner.StateAudit;
using RideBound.Solvers.OrTools;

namespace RideBound.Runner.Protocol;

public enum RunnerSessionStatus
{
    New,
    Negotiated,
    Initialized,
    AwaitingDecisionApplied,
    Failed,
    Shutdown,
}

public sealed record RunnerSessionResult(
    ProtocolEnvelope? Response,
    bool ShouldTerminate = false)
{
    public RunnerSessionResult(StreamedResponse streamed)
        : this((ProtocolEnvelope?)null)
    {
        Streamed = streamed;
    }

    /// <summary>
    /// Set when the response is written part by part (the checkpoint);
    /// <see cref="Response"/> is then null.
    /// </summary>
    public StreamedResponse? Streamed { get; }
}

// One protocol session of the Runner: it answers the adapter's messages in order. This file holds the session
// state, Process (which hands each message to its handler) and the error replies. The other parts of the class:
//   RunnerSession.Messages.cs    hello, initializeRun, eventBatch and decisionApplied;
//   RunnerSession.Decision.cs    the dispatch decision for one event batch;
//   RunnerSession.Hashing.cs     the canonical state bytes and the inputs of the hashes;
//   RunnerSession.Checkpoint.cs  checkpoint.
public sealed partial class RunnerSession
{
    private readonly CapabilityRequirementProfile _requirements;
    private readonly OnlineEventMapper _onlineEventMapper = new();
    private readonly ICommitmentPolicyProvider _commitmentPolicies;
    private readonly Sha256Hex _commitmentPolicyConfigurationHash;
    private readonly Wp4RunnerConfiguration _wp4Configuration;
    private readonly RideBound.Algorithms.Shareability.ShadowAllocator? _shadowAllocator;
    private readonly DispatchStep _dispatchStep;
    private HelloPayload? _hello;
    private HelloAckPayload? _helloAcknowledgement;
    private InitializedSessionIdentity? _identity;
    private PendingDecision? _pending;
    private CachedBatch? _lastBatch;
    private long _appliedEpoch;
    private long _nextEventSequence = 1;
    private long _simulationTimeMilliseconds;
    private Sha256Hex _manifestHash = ProtocolHash.ZeroHash;
    private Sha256Hex _stateHash = ProtocolHash.ZeroHash;
    private Sha256Hex _previousDecisionHash = ProtocolHash.ZeroHash;
    private EventReductionCoordinator? _onlineCoordinator;
    private readonly OnlineStateCanonicalCache _canonicalCache = new();

    private CanonicalStateBytes? _committedCanonical;

    /// <summary>
    /// An online commitment session. The commitment policy file supplies the policy catalog,
    /// the WP4 file the dispatch policy, and <paramref name="commitmentPolicyConfigurationHash"/> is the binding of
    /// the two files that initializeRun must carry. The solver is seeded with the manifest's masterSeed;
    /// <paramref name="solverBackedPolicy"/> replaces the OR-Tools policy in tests.
    /// </summary>
    public RunnerSession(
        CapabilityRequirementProfile requirements,
        ICommitmentPolicyProvider commitmentPolicies,
        Sha256Hex commitmentPolicyConfigurationHash,
        Wp4RunnerConfiguration wp4Configuration,
        SolverBackedRidePoolingPolicy? solverBackedPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(commitmentPolicies);
        ArgumentNullException.ThrowIfNull(commitmentPolicyConfigurationHash);
        ArgumentNullException.ThrowIfNull(wp4Configuration);
        _requirements = requirements;
        _commitmentPolicies = commitmentPolicies;
        _commitmentPolicyConfigurationHash = commitmentPolicyConfigurationHash;
        _wp4Configuration = wp4Configuration;
        _shadowAllocator = wp4Configuration.ShadowAllocation is { } shadowInputs
            ? new RideBound.Algorithms.Shareability.ShadowAllocator(shadowInputs)
            : null;
        var commitmentValidator = new CommitmentDecisionValidator();
        _dispatchStep = new DispatchStep(
            solverBackedPolicy
                ?? new SolverBackedRidePoolingPolicy(
                    new OrToolsCandidateSelectionSolver(),
                    commitmentValidator: commitmentValidator),
            commitmentValidator,
            _shadowAllocator);
    }

    public RunnerSessionStatus Status { get; private set; } = RunnerSessionStatus.New;

    public long AppliedEpoch => _appliedEpoch;

    public long NextEventSequence => _nextEventSequence;

    public Sha256Hex PreviousDecisionHash => _previousDecisionHash;

    public OnlineState? CommittedOnlineState => _onlineCoordinator?.CommittedState;

    public RunnerSessionResult Process(ReadOnlySpan<byte> utf8Json)
    {
        if (Status == RunnerSessionStatus.Shutdown)
        {
            return new RunnerSessionResult(null, ShouldTerminate: true);
        }

        var envelopeResult = ProtocolEnvelopeCodec.Decode(utf8Json);

        if (!envelopeResult.IsSuccess)
        {
            var envelopeError = envelopeResult.Error!;
            var protocolCode = MapEnvelopeError(envelopeError.Code);
            return Error(
                protocolCode,
                envelopeError.Disposition,
                envelopeError.Message);
        }

        var envelope = envelopeResult.Envelope!;

        if (envelope.MessageType.Value == "shutdown")
        {
            if (envelope.Payload.EnumerateObject().Any())
            {
                return Error(
                    "UNKNOWN_FIELD",
                    ProtocolFailureDisposition.RejectMessage,
                    "shutdown payload must be empty.",
                    envelope);
            }

            Status = RunnerSessionStatus.Shutdown;
            return new RunnerSessionResult(null, ShouldTerminate: true);
        }

        if (Status == RunnerSessionStatus.Failed)
        {
            return Error(
                "INVALID_SESSION_STATE",
                ProtocolFailureDisposition.RejectMessage,
                "A failed session accepts only shutdown.",
                envelope);
        }

        return envelope.MessageType.Value switch
        {
            "hello" => ProcessHello(envelope),
            "initializeRun" => ProcessInitialize(envelope),
            "eventBatch" => ProcessEventBatch(envelope),
            "decisionApplied" => ProcessDecisionApplied(envelope),
            "checkpoint" => ProcessCheckpoint(envelope),
            _ => Error(
                "INVALID_SESSION_STATE",
                ProtocolFailureDisposition.RejectMessage,
                $"Message '{envelope.MessageType.Value}' is not valid in state '{Status}'.",
                envelope),
        };
    }

    private bool IdentityMatches(ProtocolEnvelope envelope) =>
        _identity is not null
        && envelope.RunId == _identity.RunId
        && envelope.ScenarioId == _identity.ScenarioId;

    private RunnerSessionResult PayloadError(
        ProtocolPayloadError error,
        ProtocolEnvelope envelope)
    {
        var code = error.Code == ProtocolPayloadErrorCode.UnknownField
            ? "UNKNOWN_FIELD"
            : "SCHEMA_VALIDATION_FAILED";
        return Error(
            code,
            ProtocolFailureDisposition.RejectMessage,
            error.Message,
            envelope);
    }

    private RunnerSessionResult Error(
        string code,
        ProtocolFailureDisposition disposition,
        string message,
        ProtocolEnvelope? context = null)
    {
        if (disposition == ProtocolFailureDisposition.FailSession)
        {
            Status = RunnerSessionStatus.Failed;
        }

        var payload = new ErrorPayload(
            code,
            disposition,
            ErrorPayloadCodec.Sanitize(message));

        return new RunnerSessionResult(
            CreateEnvelope(
                "error",
                ErrorPayloadCodec.Encode(payload),
                context?.RunId,
                context?.ScenarioId,
                context?.EpochId,
                context?.SimTime),
            ShouldTerminate:
                disposition == ProtocolFailureDisposition.TerminateProcess);
    }

    private static ProtocolEnvelope CreateEnvelope(
        string messageType,
        byte[] payloadBytes,
        RunId? runId = null,
        ScenarioId? scenarioId = null,
        EpochId? epochId = null,
        SimulationTimeMilliseconds? simTime = null)
    {
        ProtocolMessageType.TryParse(messageType, out var parsedMessageType);
        using var document = JsonDocument.Parse(payloadBytes);

        return new ProtocolEnvelope(
            ProtocolVersion.Current,
            parsedMessageType!,
            document.RootElement.Clone(),
            runId,
            scenarioId,
            epochId,
            simTime);
    }

    private static string MapEnvelopeError(ProtocolEnvelopeErrorCode code) =>
        code switch
        {
            ProtocolEnvelopeErrorCode.MalformedJson => "MALFORMED_JSON",
            ProtocolEnvelopeErrorCode.UnknownField => "UNKNOWN_FIELD",
            ProtocolEnvelopeErrorCode.InvalidSchemaVersion =>
                "INVALID_SCHEMA_VERSION",
            ProtocolEnvelopeErrorCode.UnknownMessageType => "UNKNOWN_MESSAGE_TYPE",
            ProtocolEnvelopeErrorCode.UnsupportedSchemaMajor =>
                "UNSUPPORTED_SCHEMA_MAJOR",
            ProtocolEnvelopeErrorCode.UnsupportedSchemaMinor =>
                "UNSUPPORTED_SCHEMA_MINOR",
            _ => "SCHEMA_VALIDATION_FAILED",
        };
}
