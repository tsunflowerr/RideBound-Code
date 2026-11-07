using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Commitments;
using RideBound.Algorithms.Policies;
using RideBound.Application.Commitments;
using RideBound.Application.Events;
using RideBound.Application.Optimization;
using RideBound.Application.State;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Incidents;
using RideBound.Domain.Runs;
using RideBound.Domain.Validation;
using RideBound.Runner.Configuration;
using RideBound.Runner.Online;
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
    private readonly ProtocolEnvelope? _response = Response;

    public RunnerSessionResult(StreamedResponse streamed)
        : this((ProtocolEnvelope?)null)
    {
        Streamed = streamed;
    }

    /// <summary>
    /// ADR-089: set when the response is written part by part (the checkpoint);
    /// <see cref="Response"/> then decodes it on demand.
    /// </summary>
    public StreamedResponse? Streamed { get; }

    public ProtocolEnvelope? Response
    {
        get => _response ?? Streamed?.ToEnvelope();
        init => _response = value;
    }
}

public sealed class RunnerSession
{
    public const int RetainedBatchResponseCount = 1;

    private readonly CapabilityRequirementProfile _requirements;
    private readonly OnlineEventMapper _onlineEventMapper = new();
    private readonly ICommitmentPolicyProvider _commitmentPolicies;
    private readonly IStopDistanceLookup _stopDistances;
    private readonly Sha256Hex _commitmentPolicyConfigurationHash;
    private readonly CommitmentDecisionValidator _commitmentValidator = new();
    private readonly ExogenousServiceQualityBreachBridge _breachBridge;
    private readonly Wp4RunnerConfiguration _wp4Configuration;
    private readonly RideBound.Algorithms.Shareability.ShadowAllocator? _shadowAllocator;
    private readonly SolverBackedRidePoolingPolicy? _solverBackedPolicy;
    private readonly bool _useManifestSolverSeed;
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

    // ADR-089: `auditHash: light-v1` in the WP4 configuration.
    private readonly bool _lightAudit;

    private CanonicalStateBytes? _committedCanonical;
    private CanonicalStateBytes? _stagedCanonical;

    /// <summary>
    /// An online commitment session. The commitment policy file supplies the policy catalog and the stop distances,
    /// the WP4 file the dispatch policy, and <paramref name="commitmentPolicyConfigurationHash"/> is the binding of
    /// the two files that initializeRun must carry. <paramref name="useManifestSolverSeed"/> seeds the solver with the
    /// manifest's masterSeed instead of the WP4 file's randomSeed (the command line always does);
    /// <paramref name="solverBackedPolicy"/> replaces the OR-Tools policy in tests.
    /// </summary>
    public RunnerSession(
        CapabilityRequirementProfile requirements,
        ICommitmentPolicyProvider commitmentPolicies,
        IStopDistanceLookup stopDistances,
        Sha256Hex commitmentPolicyConfigurationHash,
        Wp4RunnerConfiguration wp4Configuration,
        SolverBackedRidePoolingPolicy? solverBackedPolicy = null,
        bool useManifestSolverSeed = false)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(commitmentPolicies);
        ArgumentNullException.ThrowIfNull(stopDistances);
        ArgumentNullException.ThrowIfNull(commitmentPolicyConfigurationHash);
        ArgumentNullException.ThrowIfNull(wp4Configuration);
        _requirements = requirements;
        _commitmentPolicies = commitmentPolicies;
        _stopDistances = stopDistances;
        _commitmentPolicyConfigurationHash = commitmentPolicyConfigurationHash;
        _breachBridge = new ExogenousServiceQualityBreachBridge();
        _wp4Configuration = wp4Configuration;
        _shadowAllocator = wp4Configuration.ShadowAllocation is { } shadowInputs
            ? new RideBound.Algorithms.Shareability.ShadowAllocator(shadowInputs)
            : null;
        _lightAudit = wp4Configuration.UsesLightAuditHash;
        _useManifestSolverSeed = useManifestSolverSeed;
        _solverBackedPolicy = wp4Configuration.SolverPolicyOptions is not null
            ? solverBackedPolicy
                ?? new SolverBackedRidePoolingPolicy(
                    new OrToolsCandidateSelectionSolver(),
                    commitmentValidator: _commitmentValidator)
            : null;

        if (useManifestSolverSeed
            && wp4Configuration.SolverPolicyOptions is null)
        {
            throw new ArgumentException(
                "Manifest solver seeding requires a solver-backed WP4 configuration.",
                nameof(useManifestSolverSeed));
        }
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
            "restore" => ProcessRestore(envelope),
            _ => Error(
                "INVALID_SESSION_STATE",
                ProtocolFailureDisposition.RejectMessage,
                $"Message '{envelope.MessageType.Value}' is not valid in state '{Status}'.",
                envelope),
        };
    }

    private RunnerSessionResult ProcessHello(ProtocolEnvelope envelope)
    {
        if (Status != RunnerSessionStatus.New)
        {
            return Error(
                "INVALID_SESSION_STATE",
                ProtocolFailureDisposition.RejectMessage,
                "hello is valid only for a new session.",
                envelope);
        }

        var payloadResult = HelloPayloadCodec.Decode(envelope.Payload);

        if (!payloadResult.IsSuccess)
        {
            return PayloadError(payloadResult.Error!, envelope);
        }

        var negotiation = CapabilityNegotiator.Negotiate(
            payloadResult.Value!,
            _requirements);

        if (!negotiation.IsSuccess)
        {
            var error = negotiation.Error!;
            ProtocolErrorCodes.TryGetDisposition(
                error.ProtocolCode,
                out var disposition);
            return Error(
                error.ProtocolCode,
                disposition,
                error.Message,
                envelope);
        }

        _hello = payloadResult.Value;
        _helloAcknowledgement = negotiation.Acknowledgement;
        Status = RunnerSessionStatus.Negotiated;

        return new RunnerSessionResult(
            CreateEnvelope(
                "helloAck",
                HelloAckPayloadCodec.Encode(_helloAcknowledgement!)));
    }

    private RunnerSessionResult ProcessInitialize(ProtocolEnvelope envelope)
    {
        if (Status != RunnerSessionStatus.Negotiated
            || _hello is null
            || _helloAcknowledgement is null)
        {
            return Error(
                "INVALID_SESSION_STATE",
                ProtocolFailureDisposition.RejectMessage,
                "initializeRun requires a successful hello negotiation.",
                envelope);
        }

        var payloadResult = InitializeRunPayloadCodec.Decode(envelope.Payload);

        if (!payloadResult.IsSuccess)
        {
            return PayloadError(payloadResult.Error!, envelope);
        }

        var validation = InitializeRunValidator.Validate(
            envelope,
            payloadResult.Value!,
            new InitializeRunValidationContext(
                _hello,
                _helloAcknowledgement,
                envelope.RunId,
                envelope.ScenarioId,
                _identity));

        if (!validation.IsSuccess)
        {
            return Error(
                validation.Error!.ProtocolCode,
                ProtocolFailureDisposition.RejectMessage,
                validation.Error.Message,
                envelope);
        }

        if (payloadResult.Value!.Manifest.PolicyConfigurationHash
            != _commitmentPolicyConfigurationHash)
        {
            return Error(
                "HASH_MISMATCH",
                ProtocolFailureDisposition.FailSession,
                "Manifest policyConfigurationHash does not match the " +
                "loaded commitment policy configuration.",
                envelope);
        }

        if (!StringComparer.Ordinal.Equals(
                payloadResult.Value.Manifest.PolicyId,
                _wp4Configuration.PolicyId)
            || !StringComparer.Ordinal.Equals(
                payloadResult.Value.Manifest.PolicyVersion,
                _wp4Configuration.PolicyVersion))
        {
            return Error(
                "HASH_MISMATCH",
                ProtocolFailureDisposition.FailSession,
                "Manifest policyId/policyVersion does not match the loaded WP4 policy configuration.",
                envelope);
        }
        if (_useManifestSolverSeed
            && payloadResult.Value!.Manifest.MasterSeed > int.MaxValue)
        {
            return Error(
                "SCHEMA_VALIDATION_FAILED",
                ProtocolFailureDisposition.FailSession,
                "Manifest solver seed exceeds the deterministic Int32 range.",
                envelope);
        }

        EpochId.TryCreate(0, out var epochId);
        EventSequence.TryCreate(1, out var nextEventSequence);
        SimulationTimeMilliseconds.TryCreate(0, out var simTime);
        _manifestHash = ProtocolHash.CalculateManifestHash(
            payloadResult.Value!.Manifest);
        _stateHash = ProtocolHash.CalculateStateIdentityHash(
            epochId,
            nextEventSequence,
            simTime);
        _identity = validation.Identity;

        var run = RideBoundRun.Create(
            new RunIdentifier(_identity!.RunId.Value),
            new ScenarioIdentifier(_identity.ScenarioId.Value),
            new SimTime(0));
        _onlineCoordinator = new EventReductionCoordinator(
            OnlineState.Create(
                run,
                _identity.Manifest.TravelTimeSnapshotHash.Value),
            CreateEventReducer());
        _stagedCanonical = null;

        if (_lightAudit)
        {
            // ADR-089 light audit: the state hash stays the identity hash computed above.
            _committedCanonical = null;
        }
        else
        {
            var initialCanonical = CanonicalOf(_onlineCoordinator.CommittedState);
            _committedCanonical = new CanonicalStateBytes(
                _onlineCoordinator.CommittedState,
                initialCanonical);
            _stateHash = OnlineStateCanonicalWriter.HashV1(initialCanonical);
        }

        Status = RunnerSessionStatus.Initialized;

        var initialized = new InitializedPayload(
            _manifestHash,
            new InitialStateIdentity(
                epochId,
                nextEventSequence,
                simTime,
                _stateHash));

        return new RunnerSessionResult(
            CreateEnvelope(
                "initialized",
                InitializedPayloadCodec.Encode(initialized),
                envelope.RunId,
                envelope.ScenarioId));
    }

    private RunnerSessionResult ProcessEventBatch(ProtocolEnvelope envelope)
    {
        if (Status is not (
            RunnerSessionStatus.Initialized
            or RunnerSessionStatus.AwaitingDecisionApplied))
        {
            return Error(
                "INVALID_SESSION_STATE",
                ProtocolFailureDisposition.RejectMessage,
                "eventBatch requires an initialized session.",
                envelope);
        }

        if (!IdentityMatches(envelope))
        {
            return Error(
                "IDENTITY_MISMATCH",
                ProtocolFailureDisposition.RejectMessage,
                "Event context differs from initialized run identity.",
                envelope);
        }

        var payloadResult = EventBatchPayloadCodec.Decode(envelope.Payload);

        if (!payloadResult.IsSuccess)
        {
            return PayloadError(payloadResult.Error!, envelope);
        }

        var payload = payloadResult.Value!;
        var firstSequence = payload.Events[0].EventSequence.Value;
        var lastSequence = payload.Events[^1].EventSequence.Value;
        var key = new BatchKey(
            envelope.RunId!,
            envelope.ScenarioId!,
            envelope.EpochId!.Value.Value,
            firstSequence,
            lastSequence);
        var canonicalBatch = CanonicalJson.Canonicalize(
            ProtocolEnvelopeCodec.Encode(envelope));
        var canonicalBatchHash = Convert.ToHexStringLower(SHA256.HashData(canonicalBatch));

        if (_lastBatch is not null && _lastBatch.Key == key)
        {
            if (string.Equals(
                    _lastBatch.CanonicalBatchHash,
                    canonicalBatchHash,
                    StringComparison.Ordinal))
            {
                return new RunnerSessionResult(_lastBatch.Response);
            }

            return Error(
                "DUPLICATE_PAYLOAD_CONFLICT",
                ProtocolFailureDisposition.FailSession,
                "A retried batch key contains different canonical payload bytes.",
                envelope);
        }

        if (_lastBatch is not null
            && firstSequence <= _lastBatch.Key.LastEventSequence)
        {
            return Error(
                "EVENT_SEQUENCE_OVERLAP",
                ProtocolFailureDisposition.FailSession,
                "A partial event batch overlap is not a valid retry.",
                envelope);
        }

        if (Status == RunnerSessionStatus.AwaitingDecisionApplied)
        {
            return Error(
                "INVALID_SESSION_STATE",
                ProtocolFailureDisposition.RejectMessage,
                "The previous decision must be acknowledged before another batch.",
                envelope);
        }

        var orderingError = EventBatchOrderingValidator.Validate(
            envelope,
            payload,
            new EventBatchOrderingState(
                _appliedEpoch,
                _nextEventSequence,
                _simulationTimeMilliseconds));

        if (orderingError is not null)
        {
            ProtocolErrorCodes.TryGetDisposition(
                orderingError.ProtocolCode,
                out var disposition);
            return Error(
                orderingError.ProtocolCode,
                disposition,
                orderingError.Message,
                envelope);
        }

        var nextSequence = checked(lastSequence + 1);
        var nextEpoch = envelope.EpochId!.Value.Value;
        var nextSimTime = envelope.SimTime!.Value.Value;
        EpochId.TryCreate(nextEpoch, out var nextEpochId);
        EventSequence.TryCreate(nextSequence, out var nextEventSequence);
        SimulationTimeMilliseconds.TryCreate(nextSimTime, out var nextSimulationTime);

        // ADR-089 light audit: the staged state is identified, not hashed.
        var lightStateAfterHash = _lightAudit
            ? ProtocolHash.CalculateStateIdentityHash(
                nextEpochId!,
                nextEventSequence!,
                nextSimulationTime!)
            : null;
        var online = BuildOnlineDecision(
            envelope,
            canonicalBatchHash,
            lightStateAfterHash);

        if (online.Error is not null)
        {
            return Error(
                online.Error.Code,
                online.Error.Disposition,
                online.Error.Message,
                envelope);
        }

        var stateAfterHash = online.StateAfterHash!;
        var shell = new DecisionPayload(
            DecisionProductionStatus.Produced,
            online.ReasonCode!,
            online.Actions!,
            online.Certificate!,
            online.Solver!,
            _stateHash,
            stateAfterHash,
            _previousDecisionHash,
            ProtocolHash.ZeroHash);
        var canonicalDecision = CanonicalJson.Canonicalize(
            DecisionPayloadCodec.Encode(shell, hashProjection: true));
        var decisionHash = _lightAudit
            ? ProtocolHash.CalculateDecisionHashLight(
                _previousDecisionHash,
                _manifestHash,
                _identity!.Manifest.PolicyVersion,
                CreateCanonicalInputParts(canonicalBatch, includeOnlineState: false),
                canonicalDecision)
            : ProtocolHash.CalculateDecisionHash(
                _previousDecisionHash,
                _manifestHash,
                _identity!.Manifest.PolicyVersion,
                CreateCanonicalInputParts(canonicalBatch, includeOnlineState: true),
                canonicalDecision);
        var decision = shell with { DecisionHash = decisionHash };
        var response = CreateEnvelope(
            "decision",
            DecisionPayloadCodec.Encode(decision),
            envelope.RunId,
            envelope.ScenarioId,
            envelope.EpochId,
            envelope.SimTime);

        _pending = new PendingDecision(
            nextEpoch,
            nextSequence,
            nextSimTime,
            stateAfterHash,
            decisionHash);
        _lastBatch = new CachedBatch(key, canonicalBatchHash, response);
        Status = RunnerSessionStatus.AwaitingDecisionApplied;

        return new RunnerSessionResult(response);
    }

    private RunnerSessionResult ProcessDecisionApplied(ProtocolEnvelope envelope)
    {
        if (Status != RunnerSessionStatus.AwaitingDecisionApplied
            || _pending is null)
        {
            return Error(
                "INVALID_SESSION_STATE",
                ProtocolFailureDisposition.RejectMessage,
                "decisionApplied requires a pending decision.",
                envelope);
        }

        if (!IdentityMatches(envelope)
            || envelope.EpochId!.Value.Value != _pending.Epoch
            || envelope.SimTime!.Value.Value != _pending.SimulationTimeMilliseconds)
        {
            return Error(
                "IDENTITY_MISMATCH",
                ProtocolFailureDisposition.RejectMessage,
                "decisionApplied context differs from the pending decision.",
                envelope);
        }

        var payloadResult = DecisionAppliedPayloadCodec.Decode(envelope.Payload);

        if (!payloadResult.IsSuccess)
        {
            return PayloadError(payloadResult.Error!, envelope);
        }

        if (payloadResult.Value!.DecisionHash != _pending.DecisionHash)
        {
            return Error(
                "HASH_MISMATCH",
                ProtocolFailureDisposition.FailSession,
                "decisionApplied hash differs from the pending decision hash.",
                envelope);
        }

        var acknowledged =
            _onlineCoordinator!.ApplyDecisionAcknowledgement(
                _pending.Epoch);

        if (!acknowledged.IsSuccess)
        {
            return Error(
                "INTERNAL_ERROR",
                ProtocolFailureDisposition.FailSession,
                acknowledged.Witness!.Message,
                envelope);
        }

        // The acknowledged state is the staged object itself
        // (EventReductionCoordinator.ApplyDecisionAcknowledgement), so its canonical
        // bytes are the ones hashed for stateAfterHash.
        _committedCanonical =
            _stagedCanonical is { } staged
                && ReferenceEquals(staged.State, _onlineCoordinator.CommittedState)
                ? staged
                : null;
        _stagedCanonical = null;

        _appliedEpoch = _pending.Epoch;
        _nextEventSequence = _pending.NextEventSequence;
        _simulationTimeMilliseconds = _pending.SimulationTimeMilliseconds;
        _stateHash = _pending.StateHash;
        _previousDecisionHash = _pending.DecisionHash;
        _pending = null;
        Status = RunnerSessionStatus.Initialized;
        return new RunnerSessionResult((ProtocolEnvelope?)null);
    }

    private RunnerSessionResult ProcessCheckpoint(ProtocolEnvelope envelope)
    {
        if (Status != RunnerSessionStatus.Initialized
            || _onlineCoordinator is null
            || _pending is not null
            || !IdentityMatches(envelope))
        {
            return Error(
                "INVALID_SESSION_STATE",
                ProtocolFailureDisposition.RejectMessage,
                "checkpoint requires an initialized online session with no pending decision.",
                envelope);
        }

        if (envelope.Payload.EnumerateObject().Any())
        {
            return Error(
                "UNKNOWN_FIELD",
                ProtocolFailureDisposition.RejectMessage,
                "checkpoint request payload must be empty.",
                envelope);
        }

        var committed = CanonicalOf(_onlineCoordinator.CommittedState);

        // The checkpoint re-derives the committed state's bytes without any cache: a stale
        // fragment (an entity mutated in place) fails the session instead of passing silently.
        if (!OnlineStateCanonicalWriter.Write(_onlineCoordinator.CommittedState, null)
                .ContentEquals(committed))
        {
            return Error(
                "INTERNAL_ERROR",
                ProtocolFailureDisposition.FailSession,
                "Cached canonical state differs from its full recomputation.",
                envelope);
        }

        // ADR-089: the response is the canonical envelope line the envelope path wrote, built
        // around the cached canonical state instead of from copies of it. The shell is the same
        // checkpoint with an empty online state; its canonical line is split at that empty
        // object and the state's own canonical bytes are written there. Canonical JSON writes an
        // object member's value in place, so the line equals the canonical line of the full
        // envelope, and the hash streams the same canonical content.
        using var emptyState = JsonDocument.Parse("{}"u8.ToArray());
        var shell = new CheckpointContent(
            _manifestHash,
            _stateHash,
            _previousDecisionHash,
            _appliedEpoch,
            _nextEventSequence,
            _simulationTimeMilliseconds,
            emptyState.RootElement.Clone());
        var stateParts = committed.Parts
            .Select(part => (ReadOnlyMemory<byte>)part.AsMemory())
            .ToArray();
        var hash = CheckpointPayloadCodec.CalculateHash(shell, stateParts);
        var line = CanonicalJson.Serialize(
            CreateEnvelope(
                "checkpoint",
                CheckpointPayloadCodec.EncodeShell(hash, shell),
                envelope.RunId,
                envelope.ScenarioId));
        var (before, after) = CheckpointPayloadCodec.SplitAtEmptyOnlineState(line);

        return new RunnerSessionResult(
            new StreamedResponse(
                "checkpoint",
                [before, .. stateParts, after]));
    }

    private RunnerSessionResult ProcessRestore(ProtocolEnvelope envelope)
    {
        if (Status != RunnerSessionStatus.Initialized
            || _onlineCoordinator is null
            || _pending is not null
            || _appliedEpoch != 0
            || !IdentityMatches(envelope))
        {
            return Error(
                "INVALID_SESSION_STATE",
                ProtocolFailureDisposition.RejectMessage,
                "restore requires a newly initialized online session with no pending decision.",
                envelope);
        }

        var decoded = CheckpointPayloadCodec.Decode(envelope.Payload);

        if (!decoded.IsSuccess)
        {
            return PayloadError(decoded.Error!, envelope);
        }

        var checkpoint = decoded.Value!;

        if (checkpoint.Content.ManifestHash != _manifestHash)
        {
            return Error(
                "MANIFEST_MUTATION",
                ProtocolFailureDisposition.FailSession,
                "Checkpoint manifest hash differs from the initialized run.",
                envelope);
        }

        var restored = OnlineStateCheckpointCodec.Decode(
            checkpoint.Content.OnlineState,
            shadowAllocation: _shadowAllocator is not null);

        if (!restored.IsSuccess)
        {
            return Error(
                "SCHEMA_VALIDATION_FAILED",
                ProtocolFailureDisposition.RejectMessage,
                restored.Error!,
                envelope);
        }

        var state = restored.State!;

        if (_wp4Configuration.PlanningForecast is { } forecast && state.TravelTimes is not null)
        {
            state = state with { TravelTimes = state.TravelTimes.WithForecast(forecast) };
        }

        var restoredCanonical = CanonicalOf(state);
        var calculatedStateHash = _lightAudit
            ? LightStateIdentityHash(state)
            : OnlineStateCanonicalWriter.HashV1(restoredCanonical);

        if (state.Run.Id.Value != _identity!.RunId.Value
            || state.Run.ScenarioId.Value != _identity.ScenarioId.Value
            || state.Run.AppliedEpoch != checkpoint.Content.AppliedEpoch
            || state.NextEventSequence != checkpoint.Content.NextEventSequence
            || state.Run.SimulationTime.Milliseconds
                != checkpoint.Content.SimulationTimeMs
            || calculatedStateHash != checkpoint.Content.StateHash
            || !string.Equals(
                state.ExpectedInitialTravelTimeSnapshotHash,
                _identity.Manifest.TravelTimeSnapshotHash.Value,
                StringComparison.Ordinal))
        {
            return Error(
                "HASH_MISMATCH",
                ProtocolFailureDisposition.FailSession,
                "Checkpoint state identity does not match its content or initialized run.",
                envelope);
        }

        _onlineCoordinator = new EventReductionCoordinator(state, CreateEventReducer());
        _committedCanonical = new CanonicalStateBytes(state, restoredCanonical);
        _stagedCanonical = null;
        _appliedEpoch = checkpoint.Content.AppliedEpoch;
        _nextEventSequence = checkpoint.Content.NextEventSequence;
        _simulationTimeMilliseconds = checkpoint.Content.SimulationTimeMs;
        _stateHash = checkpoint.Content.StateHash;
        _previousDecisionHash = checkpoint.Content.PreviousDecisionHash;
        _lastBatch = null;

        return new RunnerSessionResult(
            CreateEnvelope(
                "restore",
                RestoreAcknowledgedPayloadCodec.Encode(
                    new RestoreAcknowledgedPayload(
                        "restored",
                        checkpoint.CheckpointHash)),
                envelope.RunId,
                envelope.ScenarioId));
    }

    private bool IdentityMatches(ProtocolEnvelope envelope) =>
        _identity is not null
        && envelope.RunId == _identity.RunId
        && envelope.ScenarioId == _identity.ScenarioId;

    /// <summary>
    /// The canonicalInputState of decisionHash v1 as ordered byte ranges. The original
    /// CreateCanonicalInput built {"eventBatch", "stateIdentityBefore", "onlineStateBefore"}
    /// with Utf8JsonWriter, re-parsed both documents and canonicalized the whole object.
    /// Canonical JSON v1 is compositional (CanonicalJson.cs:78-176: an object is its members
    /// sorted by ordinal name, each value canonicalized on its own), so the same bytes are
    /// '{"eventBatch":' + canonical(batch envelope) + ',"onlineStateBefore":' +
    /// canonical(committed state) + ',"stateIdentityBefore":' + canonical(identity) + '}'.
    /// </summary>
    private IReadOnlyList<ReadOnlyMemory<byte>> CreateCanonicalInputParts(
        byte[] canonicalBatch,
        bool includeOnlineState)
    {
        var parts = new List<ReadOnlyMemory<byte>>
        {
            "{\"eventBatch\":"u8.ToArray(),
            canonicalBatch,
        };

        if (includeOnlineState)
        {
            parts.Add(",\"onlineStateBefore\":"u8.ToArray());

            foreach (var part in CanonicalOf(_onlineCoordinator!.CommittedState).Parts)
            {
                parts.Add(part);
            }
        }

        var identity = new CanonicalBuffer(160);
        identity.Raw(",\"stateIdentityBefore\":"u8);
        var item = new CanonicalObject(identity);
        item.Member("epochId"u8).Integer(_appliedEpoch);
        item.Member("nextEventSeq"u8).Integer(_nextEventSequence);
        item.Member("simTimeMs"u8).Integer(_simulationTimeMilliseconds);
        item.Member("stateHash"u8).String(_stateHash.Value);
        item.End();
        identity.Byte((byte)'}');
        parts.Add(identity.ToArray());
        return parts;
    }

    /// <summary>
    /// ADR-089 GĐ4: the configured planning forecast rides on every travel snapshot the adapter
    /// sends (and on a restored one, see ProcessCheckpoint). One instance per snapshot, so the
    /// reference comparisons of snapshots keep their meaning; without a forecast the batch is
    /// returned as it is.
    /// </summary>
    private InternalEventBatch WithPlanningForecast(InternalEventBatch batch)
    {
        if (_wp4Configuration.PlanningForecast is not { } forecast
            || !batch.Events.Any(value => value is TravelTimesUpdated))
        {
            return batch;
        }

        return batch with
        {
            Events = batch.Events
                .Select(value => value is TravelTimesUpdated travel
                    ? travel with { Snapshot = travel.Snapshot.WithForecast(forecast) }
                    : value)
                .ToArray(),
        };
    }

    /// <summary>
    /// ADR-089: under the light audit a state is identified by its epoch, next event sequence
    /// and simulation time; the checkpoint hash is what binds a restored state's content.
    /// </summary>
    private static Sha256Hex? LightStateIdentityHash(OnlineState state) =>
        EpochId.TryCreate(state.Run.AppliedEpoch, out var epoch)
        && EventSequence.TryCreate(state.NextEventSequence, out var sequence)
        && SimulationTimeMilliseconds.TryCreate(state.Run.SimulationTime.Milliseconds, out var time)
            ? ProtocolHash.CalculateStateIdentityHash(epoch!, sequence!, time!)
            : null;

    /// <summary>
    /// stateAfterHash of the staged state: OnlineStateHash.v1 over cached canonical segments
    /// (exact), or under the light audit (ADR-089) the state identity hash the caller
    /// computed, without writing the state.
    /// </summary>
    private Sha256Hex HashStagedState(
        OnlineState state,
        Sha256Hex? lightStateHash,
        ref CanonicalSegments? canonical)
    {
        if (lightStateHash is not null)
        {
            return lightStateHash;
        }

        canonical = CanonicalOf(state);
        return OnlineStateCanonicalWriter.HashV1(canonical);
    }

    /// <summary>
    /// Canonical bytes of a state: reused when the state is the committed or staged object
    /// already hashed, otherwise built with the per-entity fragment cache.
    /// </summary>
    private CanonicalSegments CanonicalOf(OnlineState state)
    {
        if (_committedCanonical is { } committed && ReferenceEquals(committed.State, state))
        {
            return committed.Canonical;
        }

        if (_stagedCanonical is { } staged && ReferenceEquals(staged.State, state))
        {
            return staged.Canonical;
        }

        return OnlineStateCanonicalWriter.Write(state, _canonicalCache);
    }

    private OnlineDecisionBuildResult BuildOnlineDecision(
        ProtocolEnvelope envelope,
        string publicationScope,
        Sha256Hex? lightStateAfterHash)
    {
        if (_onlineCoordinator is null)
        {
            return OnlineDecisionBuildResult.Fail(
                "INTERNAL_ERROR",
                ProtocolFailureDisposition.FailSession,
                "Online coordinator was not initialized.");
        }

        var beforeEventState = _onlineCoordinator.CommittedState;
        var mapped = _onlineEventMapper.Map(envelope);

        if (!mapped.IsSuccess)
        {
            return OnlineDecisionBuildResult.Fail(
                "SCHEMA_VALIDATION_FAILED",
                ProtocolFailureDisposition.RejectMessage,
                mapped.Witness!.Message);
        }

        var batch = WithPlanningForecast(mapped.Batch!);
        var reduced = _onlineCoordinator.Propose(batch);

        if (!reduced.IsSuccess)
        {
            return OnlineDecisionBuildResult.Fail(
                "SCHEMA_VALIDATION_FAILED",
                ProtocolFailureDisposition.RejectMessage,
                reduced.Witness!.Message);
        }

        var sourceEventSequence = batch.Events[^1].EventSequence;
        var context = new CommitmentMechanismContext(
            beforeEventState,
            reduced.ProposedState!,
            _commitmentPolicies,
            _stopDistances,
            publicationScope,
            sourceEventSequence,
            InitialPromiseTrigger: _wp4Configuration.InitialPromiseTrigger,
            CollectAllCommitmentWitnesses:
                _wp4Configuration.CollectAllCommitmentWitnesses);

        var solved = _solverBackedPolicy!.Decide(
            context,
            _wp4Configuration.CandidateGeneration,
            _useManifestSolverSeed
                ? _wp4Configuration.CreateSolverPolicyOptionsForRun(
                    _identity!.Manifest.MasterSeed)
                : _wp4Configuration.SolverPolicyOptions!);

        if (!solved.IsSuccess)
        {
            _onlineCoordinator.DiscardPendingProposal();
            return OnlineDecisionBuildResult.Fail(
                "INTERNAL_ERROR",
                ProtocolFailureDisposition.FailSession,
                solved.Witness!.Message);
        }

        var decision = RollingCostDecisionResult.Success(
            solved.Decision!.Decision);
        var validationPolicies = solved.Decision.EffectivePolicies;

        if (!decision.IsSuccess)
        {
            _onlineCoordinator.DiscardPendingProposal();
            return OnlineDecisionBuildResult.Fail(
                "INTERNAL_ERROR",
                ProtocolFailureDisposition.FailSession,
                decision.Witness!.Message);
        }

        var validation = _commitmentValidator.Validate(
            new CommitmentValidationContext(
                beforeEventState,
                reduced.ProposedState!,
                decision.Decision!.ProposedState,
                validationPolicies,
                _stopDistances,
                publicationScope,
                sourceEventSequence,
                InitialPromiseTrigger: _wp4Configuration.InitialPromiseTrigger,
                ForcedReferenceVehicles:
                    decision.Decision.ForcedReferenceVehicles,
                ForcedNoWorse:
                    _wp4Configuration.SolverPolicyOptions?.ForcedNoWorseRecovery == true));

        if (!validation.IsValid)
        {
            // The policy already validated this decision, so a rejection here is an
            // internal inconsistency. The commitment code is not a protocol error
            // code, and encoding it used to throw; it is kept in the message.
            _onlineCoordinator.DiscardPendingProposal();
            var witness = validation.Witnesses[0];
            return OnlineDecisionBuildResult.Fail(
                "INTERNAL_ERROR",
                ProtocolFailureDisposition.FailSession,
                $"{witness.Code}: {witness.Message}");
        }

        var stateToStage = validation.ValidatedState!;
        IReadOnlyList<PromisePublication> publications = validation.Publications;

        if (decision.Decision.GenerationDiagnostics is { } diagnostics
            && diagnostics.ExogenousServiceQualityBreaches.Count != 0)
        {
            var bridged = _breachBridge.Apply(
                reduced.ProposedState!,
                stateToStage,
                diagnostics,
                validationPolicies,
                _stopDistances,
                sourceEventSequence);

            if (!bridged.IsSuccess)
            {
                _onlineCoordinator.DiscardPendingProposal();
                return OnlineDecisionBuildResult.Fail(
                    "INTERNAL_ERROR",
                    ProtocolFailureDisposition.FailSession,
                    $"EXOGENOUS_BREACH_BRIDGE_FAILED: {bridged.Error}");
            }

            stateToStage = bridged.State!;
        }

        // ADR-089 GĐ4: Shadow Control. Every rider whose first promise this decision publishes
        // gets its frozen class now, from this staged state, so the next decision reads it and
        // the state hash and checkpoint carry it (engine17.js:742-776).
        if (_shadowAllocator is not null)
        {
            var opened = publications
                .Where(value => value.Entry.Kind == CommitmentLedgerEntryKind.InitialPromise)
                .Select(value => value.Entry.PublishedPromise.Projection.RequestId)
                .ToArray();
            stateToStage = _shadowAllocator.Allocate(
                stateToStage,
                opened,
                stateToStage.Run.SimulationTime);
        }

        // One canonicalization and one hash of the staged state per epoch.
        CanonicalSegments? stagedCanonical = null;
        var stagedHash = HashStagedState(stateToStage, lightStateAfterHash, ref stagedCanonical);
        var breachWitnesses = ForcedBreachWitnesses(validation.ForcedExemptions);
        var certificate = new CertificateShell(
            CertificateStatus.Produced,
            "VALIDATED",
            new CommitmentCertificateBody(
                "1.0.0",
                "commitment-validator-v1",
                breachWitnesses.Length == 0,
                _stateHash,
                stagedHash,
                publications.Select(value => value.PublicationId).ToArray(),
                stateToStage.Run.Vehicles.Count,
                stateToStage.Run.Requests.Values.Count(
                    value => value.IsAcceptedActive),
                breachWitnesses));

        var staged = _onlineCoordinator.StageDecisionState(
            reduced.ProposedState!,
            stateToStage);

        if (!staged.IsSuccess)
        {
            _onlineCoordinator.DiscardPendingProposal();
            return OnlineDecisionBuildResult.Fail(
                "INTERNAL_ERROR",
                ProtocolFailureDisposition.FailSession,
                staged.Witness!.Message);
        }

        _stagedCanonical = stagedCanonical is null
            ? null
            : new CanonicalStateBytes(stateToStage, stagedCanonical);

        var actions = OnlineDecisionActionMapper.Map(
                decision.Decision,
                beforeEventState,
                _wp4Configuration.InitialPromiseTrigger
                    == InitialPromiseTrigger.BookingConfirmation)
            .Concat(OnlineDecisionActionMapper.MapPublications(publications))
            .ToArray();
        var reasonCode = decision.Decision.RequestActions.Any(
            value => value.Outcome == RequestDecisionOutcome.Accepted)
            || decision.Decision.RequestActions.Count == 0
                ? DecisionReasonCodes.Accepted
                : DecisionReasonCodes.NoFeasibleInsertion;
        var solverStatus = decision.Decision.SelectionExecution?.SolveResult.Status
            switch
        {
            CandidateSelectionSolveStatus.SafeFallback =>
                SolverStatus.SafeFallback,
            CandidateSelectionSolveStatus.Optimal
                or CandidateSelectionSolveStatus.Feasible =>
                SolverStatus.Completed,
            _ => SolverStatus.NotRun,
        };
        var solverExecutionEvidence =
            _wp4Configuration.EmitSolverExecutionEvidence
                ? SolverExecutionEvidenceMapper.Map(decision.Decision)
                : null;
        return OnlineDecisionBuildResult.Success(
            stagedHash,
            reasonCode,
            actions,
            certificate,
            new SolverStatusShell(solverStatus, solverExecutionEvidence));
    }

    /// <summary>ADR-076: late boardings are recorded only when the WP4 configuration asks.</summary>
    private EventReducer CreateEventReducer() =>
        new(recordLatePickups: _wp4Configuration.RecordsLateBoarding);

    /// <summary>
    /// ADR-075: every decision that keeps a gate-rejected route is certified as
    /// non-normal operation, with one witness per breached gate code of each rider,
    /// including a decision that repeats an exemption without a new breach record.
    /// Under no-worse recovery a rider exempted on a changed route is certified with the stage
    /// `forcedNoWorse`, so a kept (frozen) vehicle stays distinguishable from one still serving.
    /// </summary>
    private static CertificateWitnessContract[] ForcedBreachWitnesses(
        IReadOnlyList<ForcedReferenceExemption> exemptions) =>
        exemptions
            .OrderBy(value => value.RequestId.Value, StringComparer.Ordinal)
            .SelectMany(
                exemption => exemption.WitnessCodes.Select(
                    code => new CertificateWitnessContract(
                        exemption.NoWorse ? "forcedNoWorse" : "forcedReference",
                        code,
                        exemption.VehicleId.Value,
                        exemption.RequestId.Value)))
            .ToArray();

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

    private sealed record BatchKey(
        RunId RunId,
        ScenarioId ScenarioId,
        long Epoch,
        long FirstEventSequence,
        long LastEventSequence);

    private sealed record CachedBatch(
        BatchKey Key,
        string CanonicalBatchHash,
        ProtocolEnvelope Response);

    /// <summary>The canonical v1 bytes of one immutable state object.</summary>
    private sealed record CanonicalStateBytes(
        OnlineState State,
        CanonicalSegments Canonical);

    private sealed record PendingDecision(
        long Epoch,
        long NextEventSequence,
        long SimulationTimeMilliseconds,
        Sha256Hex StateHash,
        Sha256Hex DecisionHash);

    private sealed record OnlineDecisionBuildResult(
        Sha256Hex? StateAfterHash,
        string? ReasonCode,
        IReadOnlyList<JsonElement>? Actions,
        CertificateShell? Certificate,
        SolverStatusShell? Solver,
        OnlineDecisionBuildError? Error)
    {
        public static OnlineDecisionBuildResult Success(
            Sha256Hex stateAfterHash,
            string reasonCode,
            IReadOnlyList<JsonElement> actions,
            CertificateShell certificate,
            SolverStatusShell solver) =>
            new(stateAfterHash, reasonCode, actions, certificate, solver, null);

        public static OnlineDecisionBuildResult Fail(
            string code,
            ProtocolFailureDisposition disposition,
            string message) =>
            new(
                null,
                null,
                null,
                null,
                null,
                new OnlineDecisionBuildError(code, disposition, message));
    }

    private sealed record OnlineDecisionBuildError(
        string Code,
        ProtocolFailureDisposition Disposition,
        string Message);
}
