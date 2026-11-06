using System.Text;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Contracts.Tests.Fixtures;

namespace RideBound.Contracts.Tests.Serialization;

public sealed class ProtocolHashTests
{
    [Fact]
    public void Published_manifest_vector_matches_exact_sha256()
    {
        var manifest = ReadManifest();

        var hash = ProtocolHash.CalculateManifestHash(manifest);

        Assert.Equal(
            "1f5d3769c091b3ac9e20ac7eeecd386cc0f0235d03cceccd6422db8518ab4534",
            hash.Value);
    }

    [Fact]
    public void Published_decision_vector_matches_exact_sha256()
    {
        Sha256Hex.TryCreate(
            "1111111111111111111111111111111111111111111111111111111111111111",
            out var manifestHash);
        var input = CanonicalJson.Canonicalize(
            """{"epochId":1,"events":[1,2]}"""u8);
        var decision = CanonicalJson.Canonicalize(
            """{"actions":[],"status":"notProduced"}"""u8);

        var hash = ProtocolHash.CalculateDecisionHash(
            ProtocolHash.ZeroHash,
            manifestHash!,
            "1.0.0",
            input,
            decision);

        Assert.Equal(
            "9939c29a73091766d1acda00984496c5e54aa66fa56fcd258604852accb98c4f",
            hash.Value);
    }

    /// <summary>
    /// The light-v1 audit mode of the final runs: decision hash over {eventBatch, stateIdentityBefore} under its
    /// own domain. Expected values come from an independent implementation of the framing (which also gives the
    /// published v1 vector above); the canonical input is passed in parts, as the Runner does.
    /// </summary>
    [Fact]
    public void Published_light_decision_vector_matches_exact_sha256()
    {
        Sha256Hex.TryCreate(
            "1111111111111111111111111111111111111111111111111111111111111111",
            out var manifestHash);
        var decision = CanonicalJson.Canonicalize(
            """{"actions":[],"status":"notProduced"}"""u8);
        var input = CanonicalJson.Canonicalize(
            """{"stateIdentityBefore":{"simTimeMs":0,"stateHash":"6e628e8e7e2dd18386c05ce6c7c114f2ef617cfd8b122265f3bfdfb9a20052fa","nextEventSeq":1,"epochId":0},"eventBatch":{"events":[1,2],"epochId":1}}"""u8);
        var split = input.Length / 3;
        ReadOnlyMemory<byte>[] parts = [input.AsMemory(0, split), input.AsMemory(split, split), input.AsMemory(2 * split)];
        var sameInputAsV1 = CanonicalJson.Canonicalize("""{"epochId":1,"events":[1,2]}"""u8);

        var light = ProtocolHash.CalculateDecisionHashLight(
            ProtocolHash.ZeroHash,
            manifestHash!,
            "tier3-exploratory-v1",
            parts,
            decision);
        var lightOnV1Vector = ProtocolHash.CalculateDecisionHashLight(
            ProtocolHash.ZeroHash,
            manifestHash!,
            "1.0.0",
            [sameInputAsV1],
            decision);

        Assert.Equal(
            "03f97ed4e915c99ff394e32a993b2e1c6da8924130b59c7db113378f538fa654",
            light.Value);
        Assert.Equal(
            "6aa2853efb5cdb37de34085b3a4254dfae43df756a3b9f42c8bbbe64673ad33f",
            lightOnV1Vector.Value);
        Assert.NotEqual(
            "9939c29a73091766d1acda00984496c5e54aa66fa56fcd258604852accb98c4f",
            lightOnV1Vector.Value);
    }

    [Fact]
    public void Chain_changes_for_previous_hash_tamper_and_event_order()
    {
        Sha256Hex.TryCreate(
            "1111111111111111111111111111111111111111111111111111111111111111",
            out var manifestHash);
        var decision = CanonicalJson.Canonicalize(
            """{"actions":[],"status":"notProduced"}"""u8);
        var ordered = CanonicalJson.Canonicalize("""{"events":[1,2]}"""u8);
        var reordered = CanonicalJson.Canonicalize("""{"events":[2,1]}"""u8);
        var first = ProtocolHash.CalculateDecisionHash(
            ProtocolHash.ZeroHash,
            manifestHash!,
            "1.0.0",
            ordered,
            decision);
        var reorderedHash = ProtocolHash.CalculateDecisionHash(
            ProtocolHash.ZeroHash,
            manifestHash!,
            "1.0.0",
            reordered,
            decision);
        var chained = ProtocolHash.CalculateDecisionHash(
            first,
            manifestHash!,
            "1.0.0",
            ordered,
            decision);

        Assert.NotEqual(first, reorderedHash);
        Assert.NotEqual(first, chained);
        Assert.Equal(
            "30f1408d3b64055d80e108ac66214a161d4459bb2c11a94eb41fc2db0dcae5ea",
            first.Value);
        Assert.Equal(
            "bef7c661ca166a3da3c04d672adb1e1566ed87e6a0150a48e376a8b40bcd7529",
            chained.Value);
        Assert.Matches("^[0-9a-f]{64}$", chained.Value);
    }

    [Fact]
    public void Initial_state_identity_vector_is_stable()
    {
        EpochId.TryCreate(0, out var epoch);
        EventSequence.TryCreate(1, out var sequence);
        SimulationTimeMilliseconds.TryCreate(0, out var simTime);

        var hash = ProtocolHash.CalculateStateIdentityHash(epoch, sequence, simTime);

        Assert.Equal(
            "6e628e8e7e2dd18386c05ce6c7c114f2ef617cfd8b122265f3bfdfb9a20052fa",
            hash.Value);
    }

    private static RunManifestIdentity ReadManifest()
    {
        var fixture = FixtureLoader.ReadUtf8("initialize/valid-initialize-run.json");
        var envelope = ProtocolEnvelopeCodec.Decode(Encoding.UTF8.GetBytes(fixture));
        return InitializeRunPayloadCodec.Decode(envelope.Envelope!.Payload)
            .Value!
            .Manifest;
    }
}
