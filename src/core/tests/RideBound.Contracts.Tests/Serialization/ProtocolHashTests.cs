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

    /// <summary>
    /// The light-v1 audit mode of the final runs: decision hash over {eventBatch, stateIdentityBefore} under its
    /// own domain. Expected values come from an independent implementation of the framing; the canonical input is
    /// passed in parts, as the Runner does.
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
