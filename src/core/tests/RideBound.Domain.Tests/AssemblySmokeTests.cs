namespace RideBound.Domain.Tests;

public sealed class AssemblySmokeTests
{
    [Fact]
    public void Domain_assembly_has_expected_name()
    {
        Assert.Equal(
            "RideBound.Domain",
            AssemblyReference.Assembly.GetName().Name);
    }
}
