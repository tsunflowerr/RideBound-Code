namespace RideBound.Contracts.Tests.Fixtures;

public sealed class FixtureLoaderTests
{
    [Theory]
    [InlineData("harness/smoke.json")]
    [InlineData(@"harness\smoke.json")]
    public void Reads_utf8_fixture_with_portable_path_separator(string relativePath)
    {
        var content = FixtureLoader.ReadUtf8(relativePath);

        Assert.Contains("\"message\": \"RideBound — hợp đồng\"", content);
    }

    [Fact]
    public void Missing_fixture_reports_repository_relative_path()
    {
        var exception = Assert.Throws<FileNotFoundException>(
            () => FixtureLoader.ReadUtf8("missing/not-there.json"));

        Assert.Contains("missing/not-there.json", exception.Message);
        Assert.Contains("benchmarks/schemas/fixtures", exception.Message);
        Assert.DoesNotContain(AppContext.BaseDirectory, exception.Message);
    }

    [Theory]
    [InlineData("../outside.json")]
    [InlineData("harness/../../outside.json")]
    public void Rejects_fixture_path_that_escapes_fixture_root(string relativePath)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => FixtureLoader.ReadUtf8(relativePath));

        Assert.Contains("must stay relative", exception.Message);
    }

    [Fact]
    public void Reads_schema_asset_from_stable_repository_relative_root()
    {
        var content = FixtureLoader.ReadSchemaUtf8("v1/schema-inventory.json");

        Assert.Contains("\"schemaVersion\": \"1.0.0\"", content);
        Assert.EndsWith(
            Path.Combine("benchmarks", "schemas", "v1", "schema-inventory.json"),
            FixtureLoader.GetSchemaPath("v1/schema-inventory.json"),
            StringComparison.OrdinalIgnoreCase);
    }
}
