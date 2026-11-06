using System.Text;
using System.Text.Json;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Contracts.Tests.Fixtures;

namespace RideBound.Contracts.Tests.Protocol;

public sealed class GoldenFixtureTests
{
    [Fact]
    public void Required_inventory_contains_exactly_ten_named_fixtures()
    {
        var root = FixtureLoader.GetSchemaPath("fixtures/golden/required");
        var directories = Directory.GetDirectories(root)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(10, directories.Length);
        Assert.Equal(
            [
                "01-one-vehicle-one-request-accept",
                "02-capacity-reject",
                "03-time-window-reject",
                "04-eta-revision-within-budget",
                "05-budget-reject",
                "06-vehicle-switch-quota",
                "07-traffic-only-eta-shift",
                "08-incident-override",
                "09-duplicate-event-idempotent",
                "10-checkpoint-restore-equivalence",
            ],
            directories.Select(Path.GetFileName));
    }

    [Fact]
    public void Required_metadata_uses_honest_wp1_support_taxonomy()
    {
        var root = FixtureLoader.GetSchemaPath("fixtures/golden/required");
        var metadataFiles = Directory.GetFiles(
                root,
                "metadata.json",
                SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var supportLevels = new List<string>();

        Assert.Equal(10, metadataFiles.Length);

        foreach (var metadataFile in metadataFiles)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(metadataFile));
            var rootElement = document.RootElement;
            var fields = rootElement.EnumerateObject()
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(
                [
                    "expectedOutcome",
                    "expectedValidator",
                    "fixtureId",
                    "minimumWorkPackage",
                    "supportLevel",
                ],
                fields);
            Assert.Equal("pass", rootElement.GetProperty("expectedOutcome").GetString());
            supportLevels.Add(rootElement.GetProperty("supportLevel").GetString()!);

            var directoryName = Path.GetFileName(
                Path.GetDirectoryName(metadataFile))!;
            Assert.Equal(
                directoryName[3..],
                rootElement.GetProperty("fixtureId").GetString());
        }

        Assert.Equal(9, supportLevels.Count(value => value == "future-behavior"));
        Assert.Single(
            supportLevels,
            value => value == "runner-executable");
        Assert.DoesNotContain("schema-only", supportLevels);
    }

    [Fact]
    public void Every_required_input_is_canonicalizable_event_batch_contract()
    {
        var root = FixtureLoader.GetSchemaPath("fixtures/golden/required");
        var inputs = Directory.GetFiles(
            root,
            "input.json",
            SearchOption.AllDirectories);

        Assert.Equal(10, inputs.Length);

        foreach (var input in inputs)
        {
            var bytes = File.ReadAllBytes(input);
            var canonical = CanonicalJson.Canonicalize(bytes);
            var envelope = ProtocolEnvelopeCodec.Decode(bytes);

            Assert.NotEmpty(canonical);
            Assert.True(envelope.IsSuccess, input);
            Assert.Equal("eventBatch", envelope.Envelope?.MessageType.Value);
            var payload = EventBatchPayloadCodec.Decode(envelope.Envelope!.Payload);
            Assert.True(payload.IsSuccess, input);
            Assert.Single(payload.Value!.Events);
        }
    }

    [Fact]
    public void Hash_vector_asset_is_valid_utf8_json_with_lowercase_hashes()
    {
        var json = FixtureLoader.ReadUtf8("hash/protocol-hash-vectors.json");
        using var document = JsonDocument.Parse(Encoding.UTF8.GetBytes(json));

        foreach (var section in document.RootElement.EnumerateObject())
        {
            var hash = section.Value.GetProperty("expectedSha256").GetString();
            Assert.Matches("^[0-9a-f]{64}$", hash!);
        }
    }
}
