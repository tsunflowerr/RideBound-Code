using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using RideBound.Contracts.Serialization;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Runner.Configuration;

namespace RideBound.Runner.Tests.Configuration;

public sealed class CommitmentPolicyConfigurationTests
{
    [Fact]
    public void Published_configuration_has_stable_canonical_hash_and_exact_policy()
    {
        var configuration = CommitmentPolicyConfiguration.Decode(
            File.ReadAllBytes(Path.Combine(
                RepositoryRoot(),
                "benchmarks",
                "configurations",
                "wp3-boundary-test-v1.json")));

        Assert.Equal(
            "d1be06163dd38de567e4489100acd05b74c41cc454300f7b7286b459355e928f",
            configuration.ContentHash.Value);
        Assert.True(configuration.TryGetPolicy("uniform-v1", out var policy));
        Assert.Equal(10, policy.Limits.Count);
        Assert.Equal(
            0,
            policy.Limits[
                CommitmentDimension.VehicleSwitchCount]
                .HardLimit);
        Assert.True(configuration.TryGetDistanceMillimeters(
            new NodeId("same"),
            new NodeId("same"),
            out var zero));
        Assert.Equal(0, zero);
        Assert.False(configuration.TryGetDistanceMillimeters(
            new NodeId("a"),
            new NodeId("b"),
            out _));
    }

    [Fact]
    public void Unknown_fields_are_rejected_before_policy_construction()
    {
        var json =
            """
            {
              "configurationVersion":"1.0.0",
              "policies":[],
              "stopDistances":[],
              "silentDefault":true
            }
            """;

        var error = Assert.Throws<InvalidDataException>(
            () => CommitmentPolicyConfiguration.Decode(
                Encoding.UTF8.GetBytes(json)));

        Assert.Contains("Unknown field 'silentDefault'", error.Message);
    }

    [Theory]
    [InlineData(
        "[\"accepted\",\"accepted\"]",
        "Duplicate commitment phase")]
    [InlineData(
        "[\"accepted\"]",
        "Same-node stop distance")]
    public void Ambiguous_phase_or_same_node_distance_is_rejected(
        string phases,
        string expectedMessage)
    {
        var distance = expectedMessage.StartsWith(
            "Same-node",
            StringComparison.Ordinal)
                ? "{\"fromNodeId\":\"n-1\",\"toNodeId\":\"n-1\",\"distanceMm\":7}"
                : string.Empty;
        var limits = string.Join(
            ",",
            CommitmentDimensionVocabulary.Ordered.Select(
                value =>
                    $"{{\"dimension\":\"{CommitmentDimensionVocabulary.ToProtocolValue(value)}\"," +
                    $"\"applicablePhases\":{phases}}}"));
        var json =
            $"{{\"configurationVersion\":\"1.0.0\",\"policies\":[{{" +
            "\"policyId\":\"uniform-v1\",\"budgetBasis\":\"decisionInduced\"," +
            $"\"limits\":[{limits}],\"materialRevisionRule\":{{\"rawEtaThresholdMs\":1}}" +
            $"}}],\"stopDistances\":[{distance}]}}";

        var error = Assert.Throws<InvalidDataException>(
            () => CommitmentPolicyConfiguration.Decode(
                Encoding.UTF8.GetBytes(json)));

        Assert.Contains(expectedMessage, error.Message);
    }

    [Fact]
    public void Drop_eta_deadline_is_read_when_present()
    {
        var configuration = CommitmentPolicyConfiguration.Decode(
            PolicyJson("\"dropEtaDeadlineSlackMs\":30000,\"dropEtaDeadlineTwoSided\":true"));

        Assert.True(configuration.TryGetPolicy("uniform-v1", out var policy));
        Assert.Equal(new Duration(30_000), policy.DropEtaDeadlineSlack);
        Assert.True(policy.DropEtaDeadlineTwoSided);
    }

    [Fact]
    public void Drop_eta_deadline_is_one_sided_unless_asked()
    {
        var configuration = CommitmentPolicyConfiguration.Decode(
            PolicyJson("\"dropEtaDeadlineSlackMs\":60000"));

        Assert.True(configuration.TryGetPolicy("uniform-v1", out var policy));
        Assert.Equal(new Duration(60_000), policy.DropEtaDeadlineSlack);
        Assert.False(policy.DropEtaDeadlineTwoSided);
    }

    [Fact]
    public void Drop_eta_deadline_is_disabled_when_absent()
    {
        var configuration = CommitmentPolicyConfiguration.Decode(PolicyJson(null));

        Assert.True(configuration.TryGetPolicy("uniform-v1", out var policy));
        Assert.Null(policy.DropEtaDeadlineSlack);
        Assert.False(policy.DropEtaDeadlineTwoSided);
    }

    [Theory]
    [InlineData("\"dropEtaDeadlineSlackMs\":0", "must be positive")]
    [InlineData("\"dropEtaDeadlineSlackMs\":-1", "canonical non-negative integer")]
    [InlineData(
        "\"dropEtaDeadlineSlackMs\":30000,\"dropEtaDeadlineTwoSided\":\"yes\"",
        "must be a boolean")]
    [InlineData(
        "\"dropEtaDeadlineSlackMs\":30000,\"dropEtaDeadlineTwoSided\":1",
        "must be a boolean")]
    [InlineData(
        "\"dropEtaDeadlineTwoSided\":true",
        "requires 'dropEtaDeadlineSlackMs'")]
    [InlineData(
        "\"dropEtaDeadlineSlack\":30000",
        "Unknown field 'dropEtaDeadlineSlack'")]
    public void Invalid_drop_eta_deadline_is_rejected(
        string extraFields,
        string expectedMessage)
    {
        var error = Assert.Throws<InvalidDataException>(
            () => CommitmentPolicyConfiguration.Decode(PolicyJson(extraFields)));

        Assert.Contains(expectedMessage, error.Message);
    }

    [Fact]
    public void Drop_eta_deadline_given_as_a_string_is_rejected()
    {
        // The shared integer reader used for every integer field of this file
        // (including freezeHorizonMs) calls TryGetInt64, which throws
        // InvalidOperationException on a string element. That is the reader's
        // existing behaviour; Program catches it and exits with code 64.
        Assert.ThrowsAny<InvalidOperationException>(
            () => CommitmentPolicyConfiguration.Decode(
                PolicyJson("\"dropEtaDeadlineSlackMs\":\"30000\"")));
    }

    [Theory]
    [InlineData("\"dropEtaDeadlineSlackMs\":null")]
    [InlineData("\"dropEtaDeadlineSlackMs\":30000,\"dropEtaDeadlineTwoSided\":null")]
    public void A_null_deadline_field_is_rejected_by_canonical_json(string extraFields)
    {
        // Canonicalisation rejects every JSON null before any field is read.
        var error = Assert.Throws<CanonicalJsonException>(
            () => CommitmentPolicyConfiguration.Decode(PolicyJson(extraFields)));

        Assert.Contains("Null is not permitted", error.Message);
    }

    [Fact]
    public void A_one_sided_flag_without_a_slack_leaves_the_deadline_disabled()
    {
        var configuration = CommitmentPolicyConfiguration.Decode(
            PolicyJson("\"dropEtaDeadlineTwoSided\":false"));

        Assert.True(configuration.TryGetPolicy("uniform-v1", out var policy));
        Assert.Null(policy.DropEtaDeadlineSlack);
        Assert.False(policy.DropEtaDeadlineTwoSided);
    }

    [Fact]
    public void Revision_limit_and_relaxed_deadline_are_read_when_present()
    {
        var capped = CommitmentPolicyConfiguration.Decode(
            PolicyJson("\"dropEtaRevisionLimitMs\":60000"));
        var relaxed = CommitmentPolicyConfiguration.Decode(
            PolicyJson("\"dropEtaDeadlineSlackMs\":30000,\"dropEtaDeadlineRelaxed\":true"));
        var both = CommitmentPolicyConfiguration.Decode(
            PolicyJson(
                "\"dropEtaDeadlineSlackMs\":30000,\"dropEtaDeadlineTwoSided\":false," +
                "\"dropEtaDeadlineRelaxed\":true,\"dropEtaRevisionLimitMs\":1"));

        Assert.True(capped.TryGetPolicy("uniform-v1", out var cap));
        Assert.Equal(new Duration(60_000), cap.DropEtaRevisionLimit);
        Assert.Null(cap.DropEtaDeadlineSlack);
        Assert.False(cap.DropEtaDeadlineRelaxed);
        Assert.True(relaxed.TryGetPolicy("uniform-v1", out var deadline));
        Assert.Equal(new Duration(30_000), deadline.DropEtaDeadlineSlack);
        Assert.True(deadline.DropEtaDeadlineRelaxed);
        Assert.False(deadline.DropEtaDeadlineTwoSided);
        Assert.Null(deadline.DropEtaRevisionLimit);
        Assert.True(both.TryGetPolicy("uniform-v1", out var combined));
        Assert.True(combined.DropEtaDeadlineRelaxed);
        Assert.Equal(new Duration(1), combined.DropEtaRevisionLimit);
    }

    [Fact]
    public void Revision_limit_and_relaxed_deadline_are_disabled_when_absent()
    {
        var configuration = CommitmentPolicyConfiguration.Decode(PolicyJson(null));
        var explicitOff = CommitmentPolicyConfiguration.Decode(
            PolicyJson("\"dropEtaDeadlineRelaxed\":false"));

        Assert.True(configuration.TryGetPolicy("uniform-v1", out var policy));
        Assert.Null(policy.DropEtaRevisionLimit);
        Assert.False(policy.DropEtaDeadlineRelaxed);
        Assert.True(explicitOff.TryGetPolicy("uniform-v1", out var off));
        Assert.Null(off.DropEtaDeadlineSlack);
        Assert.False(off.DropEtaDeadlineRelaxed);
        // Same behaviour, different bytes: an explicit false is a different configuration.
        Assert.NotEqual(configuration.ContentHash, explicitOff.ContentHash);
    }

    [Theory]
    [InlineData("\"dropEtaRevisionLimitMs\":0", "must be positive")]
    [InlineData("\"dropEtaRevisionLimitMs\":-1", "canonical non-negative integer")]
    [InlineData(
        "\"dropEtaDeadlineSlackMs\":30000,\"dropEtaDeadlineRelaxed\":\"yes\"",
        "'dropEtaDeadlineRelaxed' must be a boolean")]
    [InlineData(
        "\"dropEtaDeadlineSlackMs\":30000,\"dropEtaDeadlineRelaxed\":1",
        "'dropEtaDeadlineRelaxed' must be a boolean")]
    [InlineData(
        "\"dropEtaDeadlineRelaxed\":true",
        "'dropEtaDeadlineRelaxed' requires 'dropEtaDeadlineSlackMs'")]
    [InlineData(
        "\"dropEtaDeadlineSlackMs\":30000,\"dropEtaDeadlineTwoSided\":true," +
        "\"dropEtaDeadlineRelaxed\":true",
        "'dropEtaDeadlineRelaxed' cannot be combined with 'dropEtaDeadlineTwoSided'")]
    [InlineData(
        "\"dropEtaRevisionLimit\":30000",
        "Unknown field 'dropEtaRevisionLimit'")]
    [InlineData(
        "\"dropEtaDeadlineRelaxedMs\":30000",
        "Unknown field 'dropEtaDeadlineRelaxedMs'")]
    public void Invalid_revision_limit_or_relaxed_deadline_is_rejected(
        string extraFields,
        string expectedMessage)
    {
        var error = Assert.Throws<InvalidDataException>(
            () => CommitmentPolicyConfiguration.Decode(PolicyJson(extraFields)));

        Assert.Contains(expectedMessage, error.Message);
    }

    [Theory]
    [InlineData("\"dropEtaRevisionLimitMs\":1.5")]
    [InlineData("\"dropEtaRevisionLimitMs\":30000.0")]
    [InlineData("\"dropEtaRevisionLimitMs\":3e4")]
    public void A_non_integer_revision_limit_is_rejected_by_canonical_json(string extraFields)
    {
        var error = Assert.Throws<CanonicalJsonException>(
            () => CommitmentPolicyConfiguration.Decode(PolicyJson(extraFields)));

        Assert.Contains("not a canonical integer", error.Message);
    }

    [Theory]
    [InlineData("\"dropEtaRevisionLimitMs\":\"30000\"")]
    [InlineData("\"dropEtaRevisionLimitMs\":true")]
    public void A_revision_limit_that_is_not_a_number_is_rejected(string extraFields)
    {
        // The shared integer reader throws InvalidOperationException on a non-number
        // element, as it does for every other integer field of this file.
        Assert.ThrowsAny<InvalidOperationException>(
            () => CommitmentPolicyConfiguration.Decode(PolicyJson(extraFields)));
    }

    [Theory]
    [InlineData("\"dropEtaRevisionLimitMs\":null")]
    [InlineData("\"dropEtaDeadlineSlackMs\":30000,\"dropEtaDeadlineRelaxed\":null")]
    public void A_null_revision_limit_or_relaxed_flag_is_rejected_by_canonical_json(
        string extraFields)
    {
        var error = Assert.Throws<CanonicalJsonException>(
            () => CommitmentPolicyConfiguration.Decode(PolicyJson(extraFields)));

        Assert.Contains("Null is not permitted", error.Message);
    }

    [Fact]
    public void The_archived_reference_configuration_keeps_its_hash_and_both_options_off()
    {
        // wp14-c1-h6-reference-v1.json is byte-identical to the C-30 configuration of every
        // archived T3.8 run (ADR-078); its canonical hash must not move. Adding either new key
        // gives a different configuration, so the arms of a comparison stay distinguishable.
        var reference = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "benchmarks",
            "configurations",
            "wp14-c1-h6-reference-v1.json"));
        var archived = CommitmentPolicyConfiguration.Decode(Encoding.UTF8.GetBytes(reference));

        Assert.Equal(
            "f07395f5f2c967e0d89cceb4a55b23c4f896971db071d8c7876f05e20ef09884",
            archived.ContentHash.Value);
        var policyId = Assert.Single(archived.PolicyIds);
        Assert.True(archived.TryGetPolicy(policyId, out var policy));
        Assert.Null(policy.DropEtaRevisionLimit);
        Assert.False(policy.DropEtaDeadlineRelaxed);
        Assert.Null(policy.DropEtaDeadlineSlack);

        var capped = JsonNode.Parse(reference)!;
        capped["policies"]![0]!["dropEtaRevisionLimitMs"] = 30_000;
        var relaxed = JsonNode.Parse(reference)!;
        relaxed["policies"]![0]!["dropEtaDeadlineSlackMs"] = 30_000;
        relaxed["policies"]![0]!["dropEtaDeadlineRelaxed"] = true;
        var strict = JsonNode.Parse(reference)!;
        strict["policies"]![0]!["dropEtaDeadlineSlackMs"] = 30_000;
        var hashes = new[] { capped, relaxed, strict }
            .Select(
                value => CommitmentPolicyConfiguration.Decode(
                    Encoding.UTF8.GetBytes(value.ToJsonString())).ContentHash.Value)
            .Append(archived.ContentHash.Value)
            .ToArray();

        Assert.Equal(hashes.Length, hashes.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Slack_proportional_budget_and_deadline_are_read_when_present()
    {
        var budget = CommitmentPolicyConfiguration.Decode(PolicyJson("\"dropEtaBudgetSlackPermille\":350"));
        var deadline = CommitmentPolicyConfiguration.Decode(
            PolicyJson("\"dropEtaDeadlineRelaxed\":true,\"dropEtaDeadlineSlackPermille\":350"));
        var absent = CommitmentPolicyConfiguration.Decode(PolicyJson(null));

        Assert.True(budget.TryGetPolicy("uniform-v1", out var rce));
        Assert.Equal(350, rce.DropEtaBudgetSlackPermille);
        Assert.Null(rce.DropEtaDeadlineSlackPermille);
        Assert.Null(rce.DropEtaDeadlineSlack);
        Assert.True(deadline.TryGetPolicy("uniform-v1", out var eohz));
        Assert.Equal(350, eohz.DropEtaDeadlineSlackPermille);
        Assert.True(eohz.DropEtaDeadlineRelaxed);
        Assert.Null(eohz.DropEtaDeadlineSlack);
        Assert.Null(eohz.DropEtaBudgetSlackPermille);
        Assert.True(absent.TryGetPolicy("uniform-v1", out var plain));
        Assert.Null(plain.DropEtaBudgetSlackPermille);
        Assert.Null(plain.DropEtaDeadlineSlackPermille);
        Assert.False(plain.ReadsInitialPromise);
    }

    [Theory]
    [InlineData("\"dropEtaBudgetSlackPermille\":0", "must be positive")]
    [InlineData("\"dropEtaBudgetSlackPermille\":1001", "'dropEtaBudgetSlackPermille' must be 1 to 1000")]
    [InlineData("\"dropEtaDeadlineSlackPermille\":1001", "'dropEtaDeadlineSlackPermille' must be 1 to 1000")]
    [InlineData(
        "\"dropEtaDeadlineSlackMs\":30000,\"dropEtaDeadlineSlackPermille\":350",
        "'dropEtaDeadlineSlackMs' cannot be combined with 'dropEtaDeadlineSlackPermille'")]
    [InlineData(
        "\"dropEtaDeadlineTwoSided\":true,\"dropEtaBudgetSlackPermille\":350",
        "'dropEtaDeadlineTwoSided' requires 'dropEtaDeadlineSlackMs' or 'dropEtaDeadlineSlackPermille'")]
    [InlineData("\"dropEtaSlackPermille\":350", "Unknown field 'dropEtaSlackPermille'")]
    public void Invalid_slack_proportional_settings_are_rejected(string extraFields, string expectedMessage)
    {
        var error = Assert.Throws<InvalidDataException>(
            () => CommitmentPolicyConfiguration.Decode(PolicyJson(extraFields)));

        Assert.Contains(expectedMessage, error.Message);
    }

    [Fact]
    public void A_slack_proportional_permille_given_as_a_string_is_rejected()
    {
        // Same shared integer reader as Drop_eta_deadline_given_as_a_string_is_rejected.
        Assert.ThrowsAny<InvalidOperationException>(
            () => CommitmentPolicyConfiguration.Decode(PolicyJson("\"dropEtaBudgetSlackPermille\":\"350\"")));
    }

    [Fact]
    public void A_slack_proportional_budget_cannot_coexist_with_a_fixed_drop_eta_total_limit()
    {
        var withFixedLimit = Encoding.UTF8.GetString(PolicyJson("\"dropEtaBudgetSlackPermille\":350"))
            .Replace(
                "{\"dimension\":\"drop_eta_total_ms\",\"applicablePhases\":[\"accepted\"]}",
                "{\"dimension\":\"drop_eta_total_ms\",\"hardLimit\":120000,\"applicablePhases\":[\"accepted\"]}",
                StringComparison.Ordinal);
        Assert.Contains("\"hardLimit\":120000", withFixedLimit, StringComparison.Ordinal);

        var error = Assert.Throws<InvalidDataException>(
            () => CommitmentPolicyConfiguration.Decode(Encoding.UTF8.GetBytes(withFixedLimit)));

        Assert.Contains("'dropEtaBudgetSlackPermille' replaces the drop_eta_total_ms hardLimit", error.Message);
    }

    [Fact]
    public void Waiting_rider_relocation_is_read_when_present_and_off_when_absent()
    {
        var on = CommitmentPolicyConfiguration.Decode(PolicyJson(
            "\"waitingRiderRelocation\":true,\"finalConfirmationLocks\":[\"pickupStop\",\"pickupEta\"],"
            + "\"ratchetLocks\":[\"pickupEta\"]"));
        var off = CommitmentPolicyConfiguration.Decode(PolicyJson("\"waitingRiderRelocation\":false"));
        var absent = CommitmentPolicyConfiguration.Decode(PolicyJson(null));

        Assert.True(on.TryGetPolicy("uniform-v1", out var g2));
        Assert.True(g2.WaitingRiderRelocation);
        Assert.Equal(PromiseLock.PickupStop | PromiseLock.PickupEta, g2.FinalConfirmationLocks);
        Assert.Equal(PromiseLock.PickupEta, g2.RatchetLocks);
        Assert.True(off.TryGetPolicy("uniform-v1", out var explicitOff));
        Assert.False(explicitOff.WaitingRiderRelocation);
        Assert.True(absent.TryGetPolicy("uniform-v1", out var plain));
        Assert.False(plain.WaitingRiderRelocation);
        // Declaring the key is a different configuration, so archived bindings omit it.
        Assert.NotEqual(absent.ContentHash, off.ContentHash);
    }

    [Theory]
    [InlineData(
        "\"waitingRiderRelocation\":true,\"finalConfirmationLocks\":[\"vehicle\",\"pickupEta\"]",
        "'waitingRiderRelocation' requires 'finalConfirmationLocks' and 'freezeHorizonLocks' without 'vehicle'")]
    [InlineData(
        "\"waitingRiderRelocation\":true,\"freezeHorizonMs\":60000,\"freezeHorizonLocks\":[\"vehicle\"]",
        "'waitingRiderRelocation' requires 'finalConfirmationLocks' and 'freezeHorizonLocks' without 'vehicle'")]
    [InlineData("\"waitingRiderRelocation\":\"true\"", "waitingRiderRelocation")]
    public void Invalid_waiting_rider_relocation_settings_are_rejected(string extraFields, string expectedMessage)
    {
        var error = Assert.ThrowsAny<Exception>(
            () => CommitmentPolicyConfiguration.Decode(PolicyJson(extraFields)));

        Assert.True(error is InvalidDataException or InvalidOperationException, error.GetType().Name);
        Assert.Contains(expectedMessage, error.Message);
    }

    [Fact]
    public void Waiting_rider_relocation_cannot_run_under_a_zero_switch_limit_while_waiting()
    {
        var json = Encoding.UTF8.GetString(PolicyJson("\"waitingRiderRelocation\":true"));
        var waiting = json.Replace(
            "{\"dimension\":\"vehicle_switch_count\",\"applicablePhases\":[\"accepted\"]}",
            "{\"dimension\":\"vehicle_switch_count\",\"hardLimit\":0,\"applicablePhases\":[\"accepted\",\"waitingPickup\"]}",
            StringComparison.Ordinal);
        var acceptedOnly = json.Replace(
            "{\"dimension\":\"vehicle_switch_count\",\"applicablePhases\":[\"accepted\"]}",
            "{\"dimension\":\"vehicle_switch_count\",\"hardLimit\":0,\"applicablePhases\":[\"accepted\"]}",
            StringComparison.Ordinal);
        Assert.NotEqual(json, waiting);
        Assert.NotEqual(json, acceptedOnly);

        var error = Assert.Throws<InvalidDataException>(
            () => CommitmentPolicyConfiguration.Decode(Encoding.UTF8.GetBytes(waiting)));

        Assert.Contains("vehicle_switch_count hardLimit of 0 for waiting riders", error.Message);
        Assert.True(CommitmentPolicyConfiguration.Decode(Encoding.UTF8.GetBytes(acceptedOnly))
            .TryGetPolicy("uniform-v1", out var allowed));
        Assert.True(allowed.WaitingRiderRelocation);
    }

    private static byte[] PolicyJson(string? extraFields)
    {
        var limits = string.Join(
            ",",
            CommitmentDimensionVocabulary.Ordered.Select(
                value =>
                    $"{{\"dimension\":\"{CommitmentDimensionVocabulary.ToProtocolValue(value)}\"," +
                    "\"applicablePhases\":[\"accepted\"]}"));
        var extra = extraFields is null ? string.Empty : "," + extraFields;
        return Encoding.UTF8.GetBytes(
            $"{{\"configurationVersion\":\"1.0.0\",\"policies\":[{{" +
            "\"policyId\":\"uniform-v1\",\"budgetBasis\":\"decisionInduced\"," +
            $"\"limits\":[{limits}],\"materialRevisionRule\":{{\"rawEtaThresholdMs\":1}}" +
            $"{extra}}}],\"stopDistances\":[]}}");
    }

    private static string RepositoryRoot() =>
        typeof(CommitmentPolicyConfigurationTests)
            .Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(value => value.Key == "RideBoundRepositoryRoot")
            .Value!;
}
