using System.Text;
using System.Text.Json.Nodes;
using RideBound.Contracts.Serialization;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Runner.Configuration;
using RideBound.Tests;

namespace RideBound.Runner.Tests.Configuration;

public sealed class CommitmentPolicyConfigurationTests
{
    [Fact]
    public void Published_configuration_has_stable_canonical_hash_and_exact_policy()
    {
        var configuration = CommitmentPolicyConfiguration.Decode(
            File.ReadAllBytes(Path.Combine(
                TestPaths.Root(),
                "configs",
                "test",
                "wp3-boundary-test-v1.json")));

        Assert.Equal(
            "4dc1fc22218ead45e71553bde7b84c293ee1e2fe3de7dd79134ed106edf3eb3e",
            configuration.ContentHash.Value);
        Assert.True(configuration.TryGetPolicy("uniform-v1", out var policy));
        Assert.Equal(5, policy.Limits.Count);
        Assert.Equal(
            0,
            policy.Limits[
                CommitmentDimension.VehicleSwitchCount]
                .HardLimit);
    }

    [Fact]
    public void Unknown_fields_are_rejected_before_policy_construction()
    {
        var json =
            """
            {
              "configurationVersion":"1.0.0",
              "policies":[],
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
    public void Ambiguous_phase_is_rejected(
        string phases,
        string expectedMessage)
    {
        var limits = string.Join(
            ",",
            CommitmentDimensionVocabulary.Ordered.Select(
                value =>
                    $"{{\"dimension\":\"{CommitmentDimensionVocabulary.ToProtocolValue(value)}\"," +
                    $"\"applicablePhases\":{phases}}}"));
        var json =
            $"{{\"configurationVersion\":\"1.0.0\",\"policies\":[{{" +
            "\"policyId\":\"uniform-v1\",\"budgetBasis\":\"decisionInduced\"," +
            $"\"limits\":[{limits}]" +
            "}]}";

        var error = Assert.Throws<InvalidDataException>(
            () => CommitmentPolicyConfiguration.Decode(
                Encoding.UTF8.GetBytes(json)));

        Assert.Contains(expectedMessage, error.Message);
    }

    [Fact]
    public void Drop_eta_deadline_is_read_when_present()
    {
        var configuration = CommitmentPolicyConfiguration.Decode(
            PolicyJson("\"dropEtaDeadlineSlackPermille\":1000,\"dropEtaDeadlineRelaxed\":true"));

        Assert.True(configuration.TryGetPolicy("uniform-v1", out var policy));
        Assert.Equal(1000, policy.DropEtaDeadlineSlackPermille);
    }

    [Fact]
    public void Drop_eta_deadline_is_disabled_when_absent()
    {
        var configuration = CommitmentPolicyConfiguration.Decode(PolicyJson(null));

        Assert.True(configuration.TryGetPolicy("uniform-v1", out var policy));
        Assert.Null(policy.DropEtaDeadlineSlackPermille);
    }

    [Theory]
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
        // calls TryGetInt64, which throws
        // InvalidOperationException on a string element. That is the reader's
        // existing behaviour; Program catches it and exits with code 64.
        Assert.ThrowsAny<InvalidOperationException>(
            () => CommitmentPolicyConfiguration.Decode(
                PolicyJson("\"dropEtaDeadlineSlackPermille\":\"350\"")));
    }

    [Theory]
    [InlineData("\"dropEtaDeadlineSlackPermille\":null")]
    public void A_null_deadline_field_is_rejected_by_canonical_json(string extraFields)
    {
        // Canonicalisation rejects every JSON null before any field is read.
        var error = Assert.Throws<CanonicalJsonException>(
            () => CommitmentPolicyConfiguration.Decode(PolicyJson(extraFields)));

        Assert.Contains("Null is not permitted", error.Message);
    }

    [Fact]
    public void Relaxed_deadline_is_read_when_present()
    {
        // The deadline keys of the final SHHZ71 policy.
        var relaxed = CommitmentPolicyConfiguration.Decode(
            PolicyJson(
                "\"dropEtaDeadlineSlackPermille\":1000,\"dropEtaDeadlineRelaxed\":true,"
                + "\"dropEtaShadowAllocation\":true"));

        Assert.True(relaxed.TryGetPolicy("uniform-v1", out var deadline));
        Assert.Equal(1000, deadline.DropEtaDeadlineSlackPermille);
        Assert.True(deadline.DropEtaShadowAllocation);
    }

    [Theory]
    [InlineData(
        "\"dropEtaDeadlineSlackPermille\":350,\"dropEtaDeadlineRelaxed\":\"yes\"",
        "'dropEtaDeadlineRelaxed' must be a boolean")]
    [InlineData(
        "\"dropEtaDeadlineSlackPermille\":350,\"dropEtaDeadlineRelaxed\":1",
        "'dropEtaDeadlineRelaxed' must be a boolean")]
    [InlineData(
        "\"dropEtaDeadlineRelaxed\":true",
        "'dropEtaDeadlineRelaxed' must be true exactly when 'dropEtaDeadlineSlackPermille' is set.")]
    [InlineData(
        "\"dropEtaDeadlineRelaxedMs\":30000",
        "Unknown field 'dropEtaDeadlineRelaxedMs'")]
    public void Invalid_relaxed_deadline_is_rejected(
        string extraFields,
        string expectedMessage)
    {
        var error = Assert.Throws<InvalidDataException>(
            () => CommitmentPolicyConfiguration.Decode(PolicyJson(extraFields)));

        Assert.Contains(expectedMessage, error.Message);
    }

    [Fact]
    public void A_null_relaxed_flag_is_rejected_by_canonical_json()
    {
        var error = Assert.Throws<CanonicalJsonException>(
            () => CommitmentPolicyConfiguration.Decode(
                PolicyJson("\"dropEtaDeadlineSlackPermille\":350,\"dropEtaDeadlineRelaxed\":null")));

        Assert.Contains("Null is not permitted", error.Message);
    }

    [Fact]
    public void The_archived_reference_configuration_keeps_its_hash_and_the_relaxed_deadline_off()
    {
        // wp14-c1-h6-reference-v1.json is the C-30 configuration of the archived T3.8 runs, without the
        // commitment dimensions that only recorded; its canonical hash must not move. Adding a deadline
        // gives a different configuration, so the arms of a comparison stay distinguishable.
        var reference = File.ReadAllText(Path.Combine(
            TestPaths.Root(),
            "configs",
            "test",
            "wp14-c1-h6-reference-v1.json"));
        var archived = CommitmentPolicyConfiguration.Decode(Encoding.UTF8.GetBytes(reference));

        Assert.Equal(
            "44926e6302862367fca6dc67a5a4551a47e873124f15cc21757a272fcf222d9f",
            archived.ContentHash.Value);
        var policyId = Assert.Single(archived.PolicyIds);
        Assert.True(archived.TryGetPolicy(policyId, out var policy));
        Assert.Null(policy.DropEtaDeadlineSlackPermille);

        var deadline = JsonNode.Parse(reference)!;
        deadline["policies"]![0]!["dropEtaDeadlineSlackPermille"] = 350;
        deadline["policies"]![0]!["dropEtaDeadlineRelaxed"] = true;

        Assert.NotEqual(
            archived.ContentHash.Value,
            CommitmentPolicyConfiguration.Decode(
                Encoding.UTF8.GetBytes(deadline.ToJsonString())).ContentHash.Value);
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
        Assert.True(deadline.TryGetPolicy("uniform-v1", out var eohz));
        Assert.Equal(350, eohz.DropEtaDeadlineSlackPermille);
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
        "'waitingRiderRelocation' requires 'finalConfirmationLocks' without 'vehicle'")]
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

    [Fact]
    public void Only_the_supported_basis_and_a_relaxed_deadline_are_accepted()
    {
        string Message(string? extraFields, string basis = "decisionInduced") =>
            Assert.Throws<InvalidDataException>(
                () => CommitmentPolicyConfiguration.Decode(
                    Encoding.UTF8.GetBytes(
                        Encoding.UTF8.GetString(PolicyJson(extraFields)).Replace(
                            "\"budgetBasis\":\"decisionInduced\"",
                            $"\"budgetBasis\":\"{basis}\"",
                            StringComparison.Ordinal)))).Message;
        bool Decodes(string? extraFields) =>
            CommitmentPolicyConfiguration.Decode(PolicyJson(extraFields)).TryGetPolicy("uniform-v1", out _);
        const string Relaxation =
            "'dropEtaDeadlineRelaxed' must be true exactly when 'dropEtaDeadlineSlackPermille' is set.";

        Assert.Equal("'$.policies[].budgetBasis' must be 'decisionInduced'.", Message(null, "customerVisible"));
        Assert.Equal("'$.policies[].budgetBasis' must be 'decisionInduced'.", Message(null, "visible"));
        Assert.Equal(Relaxation, Message("\"dropEtaDeadlineSlackPermille\":350"));
        Assert.Equal(Relaxation, Message("\"dropEtaDeadlineSlackPermille\":350,\"dropEtaDeadlineRelaxed\":false"));
        Assert.Equal(Relaxation, Message("\"dropEtaDeadlineRelaxed\":false"));
        Assert.Equal(Relaxation, Message("\"dropEtaDeadlineRelaxed\":true"));

        foreach (var removed in new[]
                 {
                     "freezeHorizonMs", "freezeHorizonLocks", "dropEtaRevisionLimitMs", "dropEtaDeadlineTwoSided",
                     "dropEtaDeadlineSlackMs",
                 })
        {
            Assert.Equal(
                $"Unknown field '{removed}' at '$.policies[]'.",
                Message($"\"{removed}\":1"));
        }

        Assert.True(Decodes("\"dropEtaDeadlineSlackPermille\":350,\"dropEtaDeadlineRelaxed\":true"));
        Assert.True(Decodes("\"dropEtaDeadlineSlackPermille\":1000,\"dropEtaShadowAllocation\":true,\"dropEtaDeadlineRelaxed\":true"));
        Assert.True(Decodes("\"dropEtaBudgetSlackPermille\":350"));
        Assert.True(Decodes(null));
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
            $"\"limits\":[{limits}]" +
            $"{extra}}}]}}");
    }
}
