using System.Text;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Tests.Fixtures;
using RideBound.Runner.Protocol;

namespace RideBound.Runner.Tests.Protocol;

public sealed class InitializeRunValidatorTests
{
    [Fact]
    public void Initialize_after_successful_handshake_returns_immutable_identity()
    {
        var input = ReadInput();

        var result = InitializeRunValidator.Validate(
            input.Envelope,
            input.Payload,
            input.Context);

        Assert.True(result.IsSuccess);
        Assert.Equal("run-001", result.Identity?.RunId.Value);
        Assert.Equal("manhattan-20260729-a", result.Identity?.ScenarioId.Value);
        Assert.Same(input.Payload.Manifest, result.Identity?.Manifest);
    }

    [Fact]
    public void Scenario_mismatch_does_not_create_initialized_identity()
    {
        var input = ReadInput();
        Assert.True(ScenarioId.TryCreate("different-scenario", out var expectedScenario));
        var mismatchedContext = input.Context with
        {
            ExpectedScenarioId = expectedScenario,
        };

        var result = InitializeRunValidator.Validate(
            input.Envelope,
            input.Payload,
            mismatchedContext);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Identity);
        Assert.Equal(
            InitializeRunValidationErrorCode.IdentityMismatch,
            result.Error?.Code);
        Assert.Equal("IDENTITY_MISMATCH", result.Error?.ProtocolCode);
        Assert.Equal("$.scenarioId", result.Error?.Field);
    }

    [Fact]
    public void Capability_selection_mismatch_does_not_create_initialized_identity()
    {
        var input = ReadInput();
        var changedSelection = input.Context.HelloAcknowledgement.CapabilitySelection with
        {
            Capabilities = [CapabilityId.ExactEventOrdering],
        };
        var mismatchedContext = input.Context with
        {
            HelloAcknowledgement = input.Context.HelloAcknowledgement with
            {
                CapabilitySelection = changedSelection,
            },
        };

        var result = InitializeRunValidator.Validate(
            input.Envelope,
            input.Payload,
            mismatchedContext);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Identity);
        Assert.Equal("IDENTITY_MISMATCH", result.Error?.ProtocolCode);
        Assert.Equal(
            "$.payload.manifest.capabilitySelection",
            result.Error?.Field);
    }

    [Fact]
    public void Hello_ack_cannot_select_capability_that_client_did_not_offer()
    {
        var input = ReadInput();
        var hello = input.Context.Hello with
        {
            Capabilities =
            [
                CapabilityId.DynamicTravelTimes,
                CapabilityId.ExactEventOrdering,
            ],
        };
        var context = input.Context with { Hello = hello };

        var result = InitializeRunValidator.Validate(
            input.Envelope,
            input.Payload,
            context);

        Assert.False(result.IsSuccess);
        Assert.Equal("IDENTITY_MISMATCH", result.Error?.ProtocolCode);
        Assert.Contains("did not offer", result.Error?.Message);
    }

    [Fact]
    public void Reinitialize_active_session_is_protocol_error_and_keeps_existing_identity()
    {
        var input = ReadInput();
        var first = InitializeRunValidator.Validate(
            input.Envelope,
            input.Payload,
            input.Context);
        var activeContext = input.Context with
        {
            ActiveSession = first.Identity,
        };

        var repeated = InitializeRunValidator.Validate(
            input.Envelope,
            input.Payload,
            activeContext);

        Assert.False(repeated.IsSuccess);
        Assert.Null(repeated.Identity);
        Assert.Equal(
            InitializeRunValidationErrorCode.InvalidSessionState,
            repeated.Error?.Code);
        Assert.Equal("INVALID_SESSION_STATE", repeated.Error?.ProtocolCode);
        Assert.Same(first.Identity, activeContext.ActiveSession);
    }

    [Fact]
    public void Manifest_adapter_must_match_hello_identity()
    {
        var input = ReadInput();
        var context = input.Context with
        {
            Hello = input.Context.Hello with { AdapterVersion = "2.0.0" },
        };

        var result = InitializeRunValidator.Validate(
            input.Envelope,
            input.Payload,
            context);

        Assert.False(result.IsSuccess);
        Assert.Equal("$.payload.manifest.adapter", result.Error?.Field);
        Assert.Equal("IDENTITY_MISMATCH", result.Error?.ProtocolCode);
    }

    private static InitializeInput ReadInput()
    {
        var hello = DecodeHello("hello/valid-hello.json");
        var acknowledgement = DecodeHelloAck("hello/valid-hello-ack.json");
        var fixture = FixtureLoader.ReadUtf8("initialize/valid-initialize-run.json");
        var envelopeResult = ProtocolEnvelopeCodec.Decode(Encoding.UTF8.GetBytes(fixture));
        var envelope = Assert.IsType<ProtocolEnvelope>(envelopeResult.Envelope);
        var payloadResult = InitializeRunPayloadCodec.Decode(envelope.Payload);
        var payload = Assert.IsType<InitializeRunPayload>(payloadResult.Value);

        return new InitializeInput(
            envelope,
            payload,
            new InitializeRunValidationContext(
                hello,
                acknowledgement,
                envelope.RunId,
                envelope.ScenarioId));
    }

    private static HelloPayload DecodeHello(string fixturePath)
    {
        var fixture = FixtureLoader.ReadUtf8(fixturePath);
        var envelope = ProtocolEnvelopeCodec.Decode(Encoding.UTF8.GetBytes(fixture));
        return Assert.IsType<HelloPayload>(
            HelloPayloadCodec.Decode(envelope.Envelope!.Payload).Value);
    }

    private static HelloAckPayload DecodeHelloAck(string fixturePath)
    {
        var fixture = FixtureLoader.ReadUtf8(fixturePath);
        var envelope = ProtocolEnvelopeCodec.Decode(Encoding.UTF8.GetBytes(fixture));
        return Assert.IsType<HelloAckPayload>(
            HelloAckPayloadCodec.Decode(envelope.Envelope!.Payload).Value);
    }

    private sealed record InitializeInput(
        ProtocolEnvelope Envelope,
        InitializeRunPayload Payload,
        InitializeRunValidationContext Context);
}
