using RideBound.Contracts.Protocol;

namespace RideBound.Contracts.Tests.Protocol;

public sealed class Wp4PolicyConfigurationBindingTests
{
    [Fact]
    public void Canonical_document_and_domain_separated_identity_are_exact()
    {
        Assert.True(Sha256Hex.TryCreate(new string('1', 64), out var commitment));
        Assert.True(Sha256Hex.TryCreate(new string('2', 64), out var wp4));

        var calculated = Wp4PolicyConfigurationBinding.Calculate(
            commitment!,
            wp4!);

        Assert.Equal(
            "613e242ce961cd4e8572a0638b964a21d82c7374836e1b7a7f49359ff12cb1c0",
            calculated.Value);
    }
}
