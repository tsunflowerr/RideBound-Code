using System.Text.Json;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Contracts.Tests.Fixtures;

namespace RideBound.Contracts.Tests.Protocol;

public sealed class SchemaAssetTests
{
    [Fact]
    public void Every_schema_is_valid_json_with_stable_nonlocal_id()
    {
        var schemaDirectory = Path.GetDirectoryName(
            FixtureLoader.GetSchemaPath("v1/schema-inventory.json"))!;
        var schemaFiles = Directory.GetFiles(
            schemaDirectory,
            "*.schema.json",
            SearchOption.TopDirectoryOnly);

        Assert.NotEmpty(schemaFiles);

        foreach (var schemaFile in schemaFiles)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(schemaFile));
            var root = document.RootElement;
            Assert.Equal(
                "https://json-schema.org/draft/2020-12/schema",
                root.GetProperty("$schema").GetString());
            var id = root.GetProperty("$id").GetString();
            Assert.StartsWith(
                "https://raw.githubusercontent.com/tsunflowerr/RideBound/",
                id,
                StringComparison.Ordinal);
            Assert.DoesNotContain("E:\\", id, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("file:", id, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Every_relative_schema_reference_resolves_in_artifact_directory()
    {
        var schemaDirectory = Path.GetDirectoryName(
            FixtureLoader.GetSchemaPath("v1/schema-inventory.json"))!;

        foreach (var schemaFile in Directory.GetFiles(
                     schemaDirectory,
                     "*.schema.json",
                     SearchOption.TopDirectoryOnly))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(schemaFile));

            foreach (var reference in EnumerateReferences(document.RootElement))
            {
                if (reference.StartsWith('#')
                    || Uri.TryCreate(reference, UriKind.Absolute, out _))
                {
                    continue;
                }

                var filePart = reference.Split('#')[0];
                Assert.True(
                    File.Exists(Path.Combine(schemaDirectory, filePart)),
                    $"Schema reference '{reference}' from '{Path.GetFileName(schemaFile)}' " +
                    "does not resolve inside the artifact directory.");
            }
        }
    }

    [Fact]
    public void Schema_inventory_maps_implemented_contracts_and_fixtures()
    {
        using var document = JsonDocument.Parse(
            FixtureLoader.ReadSchemaUtf8("v1/schema-inventory.json"));
        var implementedEntries = document.RootElement
            .GetProperty("entries")
            .EnumerateArray()
            .Where(
                entry => entry.GetProperty("implementationStatus").GetString()
                    == "implemented")
            .ToArray();

        Assert.Equal(23, implementedEntries.Length);

        foreach (var entry in implementedEntries)
        {
            var schema = entry.GetProperty("schema").GetString()!;
            Assert.True(
                File.Exists(FixtureLoader.GetSchemaPath($"v1/{schema}")),
                $"Inventory schema '{schema}' is missing.");

            var contractType = entry.GetProperty("contractType").GetString()!;
            Assert.True(
                Type.GetType($"{contractType}, RideBound.Contracts") is not null,
                $"Inventory contract type '{contractType}' does not resolve.");
        }
    }

    [Fact]
    public void Strict_v1_payload_schemas_reject_unknown_properties_by_contract()
    {
        foreach (var schema in new[]
                 {
                     "hello-payload.schema.json",
                     "hello-ack-payload.schema.json",
                     "capability-selection.schema.json",
                     "manifest-identity.schema.json",
                     "initialize-run-payload.schema.json",
                     "initialized-payload.schema.json",
                     "event-batch-payload.schema.json",
                     "decision-action.schema.json",
                     "decision-payload.schema.json",
                     "decision-applied-payload.schema.json",
                     "error-payload.schema.json",
                     "golden-fixture-metadata.schema.json",
                     "request.schema.json",
                     "route-stop.schema.json",
                     "route-plan.schema.json",
                     "vehicle-snapshot.schema.json",
                     "travel-arc.schema.json",
                     "travel-time-snapshot.schema.json",
                     "request-arrived-event-payload.schema.json",
                     "request-reference-event-payload.schema.json",
                     "vehicle-advanced-event-payload.schema.json",
                     "vehicle-reached-stop-event-payload.schema.json",
                     "passenger-event-payload.schema.json",
                     "travel-times-updated-event-payload.schema.json",
                     "timer-tick-event-payload.schema.json",
                     "incident-opened-event-payload.schema.json",
                     "incident-resolved-event-payload.schema.json",
                     "request-accepted-action-payload.schema.json",
                     "request-outcome-action-payload.schema.json",
                     "vehicle-plan-updated-action-payload.schema.json",
                     "commitment-vector.schema.json",
                     "promise-service-token.schema.json",
                     "promise-projection.schema.json",
                     "promise-published-action-payload.schema.json",
                     "commitment-breach-declared-action-payload.schema.json",
                     "certificate-witness.schema.json",
                     "commitment-certificate-body.schema.json",
                     "empty-action-payload.schema.json",
                     "checkpoint-content.schema.json",
                     "checkpoint-payload.schema.json",
                     "restore-acknowledged-payload.schema.json",
                 })
        {
            using var document = JsonDocument.Parse(
                FixtureLoader.ReadSchemaUtf8($"v1/{schema}"));

            Assert.False(document.RootElement.GetProperty("additionalProperties").GetBoolean());
        }
    }

    [Fact]
    public void Every_decision_field_declares_hash_inclusion_or_exclusion()
    {
        using var document = JsonDocument.Parse(
            FixtureLoader.ReadSchemaUtf8("v1/decision-payload.schema.json"));
        var properties = document.RootElement.GetProperty("properties");

        foreach (var property in properties.EnumerateObject())
        {
            Assert.True(
                property.Value.TryGetProperty(
                    "x-ridebound-decision-hash",
                    out _),
                $"Decision field '{property.Name}' has no hash classification.");
        }
    }

    [Fact]
    public void Decision_schema_vocabularies_match_executable_contract()
    {
        using var actionDocument = JsonDocument.Parse(
            FixtureLoader.ReadSchemaUtf8("v1/decision-action.schema.json"));
        using var payloadDocument = JsonDocument.Parse(
            FixtureLoader.ReadSchemaUtf8("v1/decision-payload.schema.json"));
        var schemaActionTypes = actionDocument.RootElement
            .GetProperty("properties")
            .GetProperty("decisionType")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .Order(StringComparer.Ordinal);
        var runtimeActionTypes = Enum.GetValues<DecisionType>()
            .Select(DecisionTypeVocabulary.ToProtocolValue)
            .Order(StringComparer.Ordinal);
        var schemaReasons = payloadDocument.RootElement
            .GetProperty("properties")
            .GetProperty("reasonCode")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .Order(StringComparer.Ordinal);

        Assert.Equal(runtimeActionTypes, schemaActionTypes);
        Assert.Equal(
            DecisionReasonCodes.All.Order(StringComparer.Ordinal),
            schemaReasons);
    }

    [Fact]
    public void Event_schema_dispatches_every_runtime_event_type_to_a_strict_payload()
    {
        using var document = JsonDocument.Parse(
            FixtureLoader.ReadSchemaUtf8("v1/protocol-event.schema.json"));
        var mappings = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var branch in document.RootElement.GetProperty("allOf").EnumerateArray())
        {
            var eventType = branch
                .GetProperty("if")
                .GetProperty("properties")
                .GetProperty("eventType");
            var payloadReference = branch
                .GetProperty("then")
                .GetProperty("properties")
                .GetProperty("payload")
                .GetProperty("$ref")
                .GetString()!;
            var values = eventType.TryGetProperty("const", out var constant)
                ? [constant.GetString()!]
                : eventType.GetProperty("enum")
                    .EnumerateArray()
                    .Select(value => value.GetString()!)
                    .ToArray();

            foreach (var value in values)
            {
                Assert.True(
                    mappings.TryAdd(value, payloadReference),
                    $"Event type '{value}' has more than one schema payload mapping.");
            }
        }

        Assert.Equal(
            Enum.GetValues<EventType>()
                .Select(EventTypeVocabulary.ToProtocolValue)
                .Order(StringComparer.Ordinal),
            mappings.Keys.Order(StringComparer.Ordinal));

        foreach (var reference in mappings.Values.Distinct(StringComparer.Ordinal))
        {
            using var payload = JsonDocument.Parse(
                FixtureLoader.ReadSchemaUtf8($"v1/{reference}"));
            Assert.False(
                payload.RootElement.GetProperty("additionalProperties").GetBoolean());
        }
    }

    [Fact]
    public void Required_schema_fields_match_published_valid_fixtures()
    {
        using var helloFixture = JsonDocument.Parse(
            FixtureLoader.ReadUtf8("hello/valid-hello.json"));
        using var ackFixture = JsonDocument.Parse(
            FixtureLoader.ReadUtf8("hello/valid-hello-ack.json"));
        using var initializeFixture = JsonDocument.Parse(
            FixtureLoader.ReadUtf8("initialize/valid-initialize-run.json"));
        using var initializedFixture = JsonDocument.Parse(
            FixtureLoader.ReadUtf8("initialize/valid-initialized.json"));

        AssertRequiredFieldsMatch(
            "hello-payload.schema.json",
            helloFixture.RootElement.GetProperty("payload"));
        AssertRequiredFieldsMatch(
            "hello-ack-payload.schema.json",
            ackFixture.RootElement.GetProperty("payload"));
        AssertRequiredFieldsMatch(
            "capability-selection.schema.json",
            ackFixture.RootElement.GetProperty("payload")
                .GetProperty("capabilitySelection"));
        AssertRequiredFieldsMatch(
            "initialize-run-payload.schema.json",
            initializeFixture.RootElement.GetProperty("payload"));
        AssertRequiredFieldsMatch(
            "manifest-identity.schema.json",
            initializeFixture.RootElement.GetProperty("payload")
                .GetProperty("manifest"));
        AssertRequiredFieldsMatch(
            "initialized-payload.schema.json",
            initializedFixture.RootElement.GetProperty("payload"));
    }

    [Fact]
    public void Published_valid_protocol_fixtures_have_canonical_utf8_projection()
    {
        var fixtureRoot = Path.GetDirectoryName(
            FixtureLoader.GetSchemaPath("fixtures/README.md"))!;
        var validFixtures = Directory.GetFiles(
            fixtureRoot,
            "valid-*.json",
            SearchOption.AllDirectories);

        Assert.NotEmpty(validFixtures);

        foreach (var fixture in validFixtures)
        {
            var input = File.ReadAllBytes(fixture);
            var canonical = CanonicalJson.Canonicalize(input);

            Assert.NotEmpty(canonical);
            Assert.NotEqual((byte)'\n', canonical[^1]);
            Assert.DoesNotContain((byte)'\r', canonical);
        }
    }

    private static IEnumerable<string> EnumerateReferences(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals("$ref"))
                {
                    yield return property.Value.GetString()!;
                }

                foreach (var nested in EnumerateReferences(property.Value))
                {
                    yield return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var nested in EnumerateReferences(item))
                {
                    yield return nested;
                }
            }
        }
    }

    private static void AssertRequiredFieldsMatch(
        string schemaName,
        JsonElement fixtureObject)
    {
        using var schemaDocument = JsonDocument.Parse(
            FixtureLoader.ReadSchemaUtf8($"v1/{schemaName}"));
        var schema = schemaDocument.RootElement;
        var required = schema
            .GetProperty("required")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var fixtureFields = fixtureObject
            .EnumerateObject()
            .Select(property => property.Name)
            .Where(
                field => schema.GetProperty("properties")
                    .GetProperty(field)
                    .ValueKind != JsonValueKind.Undefined)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(required, fixtureFields);
    }
}
