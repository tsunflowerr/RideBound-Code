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
            PolicyJson("\"dropEtaDeadlineSlackMs\":30000,\"dropEtaDeadlineRelaxed\":true"));

        Assert.True(configuration.TryGetPolicy("uniform-v1", out var policy));
        Assert.Equal(new Duration(30_000), policy.DropEtaDeadlineSlack);
    }

    [Fact]
    public void Drop_eta_deadline_is_disabled_when_absent()
    {
        var configuration = CommitmentPolicyConfiguration.Decode(PolicyJson(null));

        Assert.True(configuration.TryGetPolicy("uniform-v1", out var policy));
        Assert.Null(policy.DropEtaDeadlineSlack);
    }

    [Theory]
    [InlineData("\"dropEtaDeadlineSlackMs\":0", "must be positive")]
    [InlineData("\"dropEtaDeadlineSlackMs\":-1", "canonical non-negative integer")]
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
                PolicyJson("\"dropEtaDeadlineSlackMs\":\"30000\"")));
    }

    [Theory]
    [InlineData("\"dropEtaDeadlineSlackMs\":null")]
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
        var relaxed = CommitmentPolicyConfiguration.Decode(
            PolicyJson("\"dropEtaDeadlineSlackMs\":30000,\"dropEtaDeadlineRelaxed\":true"));

        Assert.True(relaxed.TryGetPolicy("uniform-v1", out var deadline));
        Assert.Equal(new Duration(30_000), deadline.DropEtaDeadlineSlack);
    }

    [Theory]
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
                PolicyJson("\"dropEtaDeadlineSlackMs\":30000,\"dropEtaDeadlineRelaxed\":null")));

        Assert.Contains("Null is not permitted", error.Message);
    }

    [Fact]
    public void The_archived_reference_configuration_keeps_its_hash_and_the_relaxed_deadline_off()
    {
        // wp14-c1-h6-reference-v1.json is byte-identical to the C-30 configuration of every
        // archived T3.8 run (ADR-078); its canonical hash must not move. Adding a deadline
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
        Assert.Null(policy.DropEtaDeadlineSlack);

        var deadline = JsonNode.Parse(reference)!;
        deadline["policies"]![0]!["dropEtaDeadlineSlackMs"] = 30_000;
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
        Assert.Null(rce.DropEtaDeadlineSlack);
        Assert.True(deadline.TryGetPolicy("uniform-v1", out var eohz));
        Assert.Equal(350, eohz.DropEtaDeadlineSlackPermille);
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
    public void The_customer_visible_budget_basis_is_refused_with_a_pointer_to_the_archive()
    {
        // Only decisionInduced is accepted. An unknown basis keeps its message, and so does a
        // check that runs before the basis is read.
        byte[] WithBasis(string basis, string? extraFields = null) =>
            Encoding.UTF8.GetBytes(
                Encoding.UTF8.GetString(PolicyJson(extraFields)).Replace(
                    "\"budgetBasis\":\"decisionInduced\"",
                    $"\"budgetBasis\":\"{basis}\"",
                    StringComparison.Ordinal));

        var visible = Assert.Throws<InvalidDataException>(
            () => CommitmentPolicyConfiguration.Decode(WithBasis("customerVisible")));
        var unknown = Assert.Throws<InvalidDataException>(
            () => CommitmentPolicyConfiguration.Decode(WithBasis("visible")));
        var earlier = Assert.Throws<InvalidDataException>(
            () => CommitmentPolicyConfiguration.Decode(
                WithBasis("customerVisible", "\"dropEtaDeadlineRelaxed\":true")));

        Assert.Equal(
            "Commitment budgetBasis 'customerVisible' (the customer-visible budget basis): removed; see commit "
            + "ebca912 (branch research/final-real-runs-2026-11-06)",
            visible.Message);
        Assert.Equal("Unknown commitment budgetBasis.", unknown.Message);
        Assert.StartsWith("'dropEtaDeadlineRelaxed' requires", earlier.Message, StringComparison.Ordinal);
        Assert.True(CommitmentPolicyConfiguration.Decode(WithBasis("decisionInduced"))
            .TryGetPolicy("uniform-v1", out _));
    }

    [Fact]
    public void The_removed_freeze_horizon_is_refused_with_a_pointer_to_the_archive()
    {
        // Either key is refused, whatever its value. The check runs last in the policy, so a check
        // that runs before it keeps its message.
        const string Removed =
            "Commitment 'freezeHorizonMs' and 'freezeHorizonLocks' (the freeze horizon): removed; see commit "
            + "ebca912 (branch research/final-real-runs-2026-11-06)";
        const string Freeze = "\"freezeHorizonMs\":300000,\"freezeHorizonLocks\":[\"pickupEta\"]";
        string Message(string extraFields) =>
            Assert.ThrowsAny<Exception>(
                () => CommitmentPolicyConfiguration.Decode(PolicyJson(extraFields))).Message;

        Assert.Equal(Removed, Message(Freeze));
        Assert.Equal(Removed, Message("\"freezeHorizonMs\":300000"));
        Assert.Equal(Removed, Message("\"freezeHorizonLocks\":[]"));
        Assert.Equal(
            Removed,
            Message(
                "\"waitingRiderRelocation\":true,\"finalConfirmationLocks\":[\"pickupStop\"],"
                + "\"freezeHorizonMs\":60000,\"freezeHorizonLocks\":[\"vehicle\"]"));

        Assert.StartsWith(
            "'dropEtaDeadlineRelaxed' requires",
            Message(Freeze + ",\"dropEtaDeadlineRelaxed\":true"),
            StringComparison.Ordinal);
        Assert.Equal(
            "'waitingRiderRelocation' requires 'finalConfirmationLocks' without 'vehicle'.",
            Message(Freeze + ",\"waitingRiderRelocation\":true,\"finalConfirmationLocks\":[\"vehicle\"]"));
        Assert.StartsWith(
            "Only pickup and drop ETA locks can be relaxed to a ratchet.",
            Message(Freeze + ",\"ratchetLocks\":[\"vehicle\"]"),
            StringComparison.Ordinal);
        Assert.Equal(
            "Unknown commitment budgetBasis.",
            Assert.Throws<InvalidDataException>(
                () => CommitmentPolicyConfiguration.Decode(
                    Encoding.UTF8.GetBytes(
                        Encoding.UTF8.GetString(PolicyJson(Freeze)).Replace(
                            "\"budgetBasis\":\"decisionInduced\"",
                            "\"budgetBasis\":\"visible\"",
                            StringComparison.Ordinal)))).Message);
        Assert.True(CommitmentPolicyConfiguration.Decode(PolicyJson(null)).TryGetPolicy("uniform-v1", out _));
    }

    [Fact]
    public void The_removed_revision_cap_is_refused_with_a_pointer_to_the_archive()
    {
        // The key is refused, whatever its value. The check runs last in the policy, so a check that
        // runs before it keeps its message.
        const string Removed =
            "Commitment 'dropEtaRevisionLimitMs' (the per-decision revision cap): removed; see commit "
            + "ebca912 (branch research/final-real-runs-2026-11-06)";
        const string Cap = "\"dropEtaRevisionLimitMs\":60000";
        string Message(string extraFields) =>
            Assert.ThrowsAny<Exception>(
                () => CommitmentPolicyConfiguration.Decode(PolicyJson(extraFields))).Message;

        Assert.Equal(Removed, Message(Cap));
        Assert.Equal(Removed, Message("\"dropEtaRevisionLimitMs\":0"));
        Assert.Equal(Removed, Message("\"dropEtaRevisionLimitMs\":\"30000\""));
        Assert.Equal(
            Removed,
            Message("\"dropEtaDeadlineSlackMs\":30000,\"dropEtaDeadlineRelaxed\":true," + Cap));

        Assert.Contains("not a canonical integer", Message("\"dropEtaRevisionLimitMs\":1.5"));
        Assert.Contains("Null is not permitted", Message("\"dropEtaRevisionLimitMs\":null"));
        Assert.StartsWith(
            "'dropEtaDeadlineRelaxed' requires",
            Message(Cap + ",\"dropEtaDeadlineRelaxed\":true"),
            StringComparison.Ordinal);
        Assert.StartsWith(
            "Commitment 'freezeHorizonMs' and 'freezeHorizonLocks' (the freeze horizon): removed",
            Message(Cap + ",\"freezeHorizonMs\":300000"),
            StringComparison.Ordinal);
        Assert.Equal(
            "Unknown commitment budgetBasis.",
            Assert.Throws<InvalidDataException>(
                () => CommitmentPolicyConfiguration.Decode(
                    Encoding.UTF8.GetBytes(
                        Encoding.UTF8.GetString(PolicyJson(Cap)).Replace(
                            "\"budgetBasis\":\"decisionInduced\"",
                            "\"budgetBasis\":\"visible\"",
                            StringComparison.Ordinal)))).Message);
    }

    [Fact]
    public void The_removed_two_sided_deadline_is_refused_with_a_pointer_to_the_archive()
    {
        // The key is refused, whatever its value; without it the deadline is one-sided. The check
        // runs last in the policy, so a check that runs before it keeps its message.
        const string Removed =
            "Commitment 'dropEtaDeadlineTwoSided' (the two-sided deadline): removed; see commit "
            + "ebca912 (branch research/final-real-runs-2026-11-06)";
        const string TwoSided = "\"dropEtaDeadlineTwoSided\":true";
        const string Slack = "\"dropEtaDeadlineSlackMs\":30000,";
        string Message(string extraFields) =>
            Assert.ThrowsAny<Exception>(
                () => CommitmentPolicyConfiguration.Decode(PolicyJson(extraFields))).Message;

        Assert.Equal(Removed, Message(Slack + TwoSided));
        Assert.Equal(Removed, Message("\"dropEtaDeadlineSlackPermille\":350," + TwoSided));
        Assert.Equal(Removed, Message(Slack + "\"dropEtaDeadlineTwoSided\":false"));
        Assert.Equal(Removed, Message("\"dropEtaDeadlineTwoSided\":false"));
        Assert.Equal(Removed, Message(Slack + "\"dropEtaDeadlineTwoSided\":\"yes\""));
        Assert.Equal(Removed, Message(Slack + "\"dropEtaDeadlineTwoSided\":1"));
        Assert.Equal(Removed, Message(TwoSided));
        Assert.Equal(Removed, Message(TwoSided + ",\"dropEtaBudgetSlackPermille\":350"));
        Assert.Equal(Removed, Message(Slack + "\"dropEtaDeadlineRelaxed\":true," + TwoSided));

        Assert.Contains("Null is not permitted", Message("\"dropEtaDeadlineTwoSided\":null"));
        Assert.StartsWith(
            "'dropEtaDeadlineRelaxed' requires",
            Message(TwoSided + ",\"dropEtaDeadlineRelaxed\":true"),
            StringComparison.Ordinal);
        Assert.StartsWith(
            "'dropEtaDeadlineSlackMs' cannot be combined with 'dropEtaDeadlineSlackPermille'",
            Message(Slack + "\"dropEtaDeadlineSlackPermille\":350," + TwoSided),
            StringComparison.Ordinal);
        Assert.StartsWith(
            "Commitment 'dropEtaRevisionLimitMs' (the per-decision revision cap): removed",
            Message(Slack + TwoSided + ",\"dropEtaRevisionLimitMs\":60000"),
            StringComparison.Ordinal);
        Assert.Equal(
            "Unknown commitment budgetBasis.",
            Assert.Throws<InvalidDataException>(
                () => CommitmentPolicyConfiguration.Decode(
                    Encoding.UTF8.GetBytes(
                        Encoding.UTF8.GetString(PolicyJson(Slack + TwoSided)).Replace(
                            "\"budgetBasis\":\"decisionInduced\"",
                            "\"budgetBasis\":\"visible\"",
                            StringComparison.Ordinal)))).Message);
        Assert.True(
            CommitmentPolicyConfiguration.Decode(PolicyJson(Slack + "\"dropEtaDeadlineRelaxed\":true"))
                .TryGetPolicy("uniform-v1", out _));
    }

    [Fact]
    public void The_removed_strict_deadline_is_refused_with_a_pointer_to_the_archive()
    {
        // A deadline is always relaxed: dropEtaDeadlineRelaxed must be true with a deadline, and false
        // is refused with or without one. The check runs last in the policy, so a check that runs
        // before it keeps its message.
        const string Removed =
            "Commitment 'dropEtaDeadlineRelaxed' false, or a deadline without it (the strict deadline): removed; "
            + "see commit ebca912 (branch research/final-real-runs-2026-11-06)";
        const string Slack = "\"dropEtaDeadlineSlackMs\":30000";
        const string Permille = "\"dropEtaDeadlineSlackPermille\":350";
        const string Strict = "\"dropEtaDeadlineRelaxed\":false";
        const string Relaxed = "\"dropEtaDeadlineRelaxed\":true";
        string Message(string extraFields) =>
            Assert.ThrowsAny<Exception>(
                () => CommitmentPolicyConfiguration.Decode(PolicyJson(extraFields))).Message;
        bool Decodes(string? extraFields) =>
            CommitmentPolicyConfiguration.Decode(PolicyJson(extraFields)).TryGetPolicy("uniform-v1", out _);

        Assert.Equal(Removed, Message(Slack));
        Assert.Equal(Removed, Message(Permille));
        Assert.Equal(Removed, Message(Slack + "," + Strict));
        Assert.Equal(Removed, Message(Permille + "," + Strict));
        Assert.Equal(Removed, Message(Strict));
        Assert.Equal(Removed, Message(Strict + ",\"dropEtaBudgetSlackPermille\":350"));
        Assert.Equal(
            Removed,
            Message("\"dropEtaDeadlineSlackPermille\":1000,\"dropEtaShadowAllocation\":true"));

        Assert.Contains("Null is not permitted", Message(Slack + ",\"dropEtaDeadlineRelaxed\":null"));
        Assert.StartsWith(
            "'dropEtaDeadlineRelaxed' must be a boolean",
            Message(Slack + ",\"dropEtaDeadlineRelaxed\":\"no\""),
            StringComparison.Ordinal);
        Assert.StartsWith(
            "'dropEtaDeadlineSlackMs' cannot be combined with 'dropEtaDeadlineSlackPermille'",
            Message(Slack + "," + Permille),
            StringComparison.Ordinal);
        Assert.StartsWith(
            "Commitment 'dropEtaDeadlineTwoSided' (the two-sided deadline): removed",
            Message(Slack + ",\"dropEtaDeadlineTwoSided\":false"),
            StringComparison.Ordinal);
        Assert.StartsWith(
            "Commitment 'dropEtaRevisionLimitMs' (the per-decision revision cap): removed",
            Message(Slack + ",\"dropEtaRevisionLimitMs\":60000"),
            StringComparison.Ordinal);
        Assert.StartsWith(
            "Commitment 'freezeHorizonMs' and 'freezeHorizonLocks' (the freeze horizon): removed",
            Message(Strict + ",\"freezeHorizonMs\":300000"),
            StringComparison.Ordinal);
        Assert.Equal(
            "Unknown commitment budgetBasis.",
            Assert.Throws<InvalidDataException>(
                () => CommitmentPolicyConfiguration.Decode(
                    Encoding.UTF8.GetBytes(
                        Encoding.UTF8.GetString(PolicyJson(Slack)).Replace(
                            "\"budgetBasis\":\"decisionInduced\"",
                            "\"budgetBasis\":\"visible\"",
                            StringComparison.Ordinal)))).Message);

        Assert.True(Decodes(Slack + "," + Relaxed));
        Assert.True(Decodes(Permille + "," + Relaxed));
        Assert.True(Decodes("\"dropEtaDeadlineSlackPermille\":1000,\"dropEtaShadowAllocation\":true," + Relaxed));
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
