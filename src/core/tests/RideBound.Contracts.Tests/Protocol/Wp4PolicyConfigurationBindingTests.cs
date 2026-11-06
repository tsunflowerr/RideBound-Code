using RideBound.Contracts.Protocol;

namespace RideBound.Contracts.Tests.Protocol;

public sealed class Wp4PolicyConfigurationBindingTests
{
    [Fact]
    public void Canonical_document_and_domain_separated_identity_are_exact()
    {
        Assert.True(Sha256Hex.TryCreate(new string('1', 64), out var commitment));
        Assert.True(Sha256Hex.TryCreate(new string('2', 64), out var wp4));

        var document = Wp4PolicyConfigurationBinding.CreateCanonicalDocument(
            commitment!,
            wp4!);
        var calculated = Wp4PolicyConfigurationBinding.DecodeExactAndCalculate(
            document);

        Assert.Equal(
            "{\"bindingId\":\"ridebound-wp4-policy-binding-v1\",\"commitmentConfigurationSha256\":\"1111111111111111111111111111111111111111111111111111111111111111\",\"schemaVersion\":\"1.0.0\",\"wp4ConfigurationSha256\":\"2222222222222222222222222222222222222222222222222222222222222222\"}",
            System.Text.Encoding.UTF8.GetString(document));
        Assert.Equal(
            "613e242ce961cd4e8572a0638b964a21d82c7374836e1b7a7f49359ff12cb1c0",
            calculated.Value);
        Assert.Equal(
            Wp4PolicyConfigurationBinding.Calculate(commitment!, wp4!),
            calculated);
    }

    [Fact]
    public void Noncanonical_unknown_and_invalid_hash_documents_fail_closed()
    {
        Assert.Throws<InvalidDataException>(
            () => Wp4PolicyConfigurationBinding.DecodeExactAndCalculate(
                "{ \"bindingId\":\"ridebound-wp4-policy-binding-v1\" }"u8));
        Assert.Throws<InvalidDataException>(
            () => Wp4PolicyConfigurationBinding.DecodeExactAndCalculate(
                "{\"bindingId\":\"ridebound-wp4-policy-binding-v1\",\"commitmentConfigurationSha256\":\"1111111111111111111111111111111111111111111111111111111111111111\",\"extra\":\"x\",\"schemaVersion\":\"1.0.0\",\"wp4ConfigurationSha256\":\"2222222222222222222222222222222222222222222222222222222222222222\"}"u8));
        Assert.Throws<InvalidDataException>(
            () => Wp4PolicyConfigurationBinding.DecodeExactAndCalculate(
                "{\"bindingId\":\"ridebound-wp4-policy-binding-v1\",\"commitmentConfigurationSha256\":\"ABC\",\"schemaVersion\":\"1.0.0\",\"wp4ConfigurationSha256\":\"2222222222222222222222222222222222222222222222222222222222222222\"}"u8));
    }
}
