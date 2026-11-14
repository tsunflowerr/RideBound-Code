using RideBound.Domain.Commitments;
using RideBound.Runner.Configuration;

namespace RideBound.Runner.Tests.Configuration;

/// <summary>
/// RB-WP14-005. Each declared factor level must differ from the H6 reference in
/// exactly the one way it claims, otherwise the frontier cannot be attributed to a
/// factor.
/// </summary>
public sealed class Wp14FactorConfigurationTests
{
    private const string PolicyId = "wp6-synthetic-policy-overlay-v1";

    public static TheoryData<string> FactorConfigurations() =>
    [
        "wp14-c1-h6-reference-v1",
        "wp14-c1-f2-ratchet-v1",
        "wp14-c1-nopickuplock-v1",
        "wp14-c1-budget60-v1",
        "wp14-c1-budget120-v1",
        "wp14-c1-nobudget-v1",
    ];

    [Theory]
    [MemberData(nameof(FactorConfigurations))]
    public void Every_factor_configuration_decodes(string name)
    {
        var policy = Decode(name);

        Assert.Equal(PolicyId, policy.PolicyId);

        // The shared invariants hold at every level: no reassignment and no stop
        // switching, so the frontier only ever moves the ETA commitment.
        Assert.Equal(0, policy.Limits[CommitmentDimension.VehicleSwitchCount].HardLimit);
        Assert.Equal(
            0,
            policy.Limits[CommitmentDimension.PickupStopSwitchCount].HardLimit);
        Assert.Equal(
            0,
            policy.Limits[CommitmentDimension.DropStopSwitchCount].HardLimit);
    }

    [Fact]
    public void The_reference_level_reproduces_the_h6_tight_policy()
    {
        var reference = Decode("wp14-c1-h6-reference-v1");
        var h6 = Decode("wp8-drop-eta-budget-tight-v1");

        Assert.Equal(
            h6.Limits[CommitmentDimension.DropEtaTotalMs].HardLimit,
            reference.Limits[CommitmentDimension.DropEtaTotalMs].HardLimit);
        Assert.Equal(h6.FinalConfirmationLocks, reference.FinalConfirmationLocks);
        Assert.Equal(h6.RatchetLocks, reference.RatchetLocks);
        Assert.Equal(PromiseLock.None, reference.RatchetLocks);
    }

    [Theory]
    [InlineData("wp14-c1-budget60-v1", 60_000L)]
    [InlineData("wp14-c1-budget120-v1", 120_000L)]
    public void A_budget_level_only_moves_the_drop_eta_limit(string name, long limit)
    {
        var reference = Decode("wp14-c1-h6-reference-v1");
        var level = Decode(name);

        Assert.Equal(limit, level.Limits[CommitmentDimension.DropEtaTotalMs].HardLimit);
        Assert.Equal(reference.FinalConfirmationLocks, level.FinalConfirmationLocks);
        Assert.Equal(reference.RatchetLocks, level.RatchetLocks);
    }

    [Fact]
    public void The_unbudgeted_level_removes_only_the_drop_eta_limit()
    {
        var level = Decode("wp14-c1-nobudget-v1");

        Assert.Null(level.Limits[CommitmentDimension.DropEtaTotalMs].HardLimit);
        Assert.Equal(
            Decode("wp14-c1-h6-reference-v1").FinalConfirmationLocks,
            level.FinalConfirmationLocks);
    }

    [Fact]
    public void The_ratchet_level_keeps_the_lock_and_only_allows_improvement()
    {
        var level = Decode("wp14-c1-f2-ratchet-v1");
        var reference = Decode("wp14-c1-h6-reference-v1");

        Assert.Equal(reference.FinalConfirmationLocks, level.FinalConfirmationLocks);
        Assert.Equal(PromiseLock.PickupEta, level.RatchetLocks);
    }

    [Fact]
    public void The_no_pickup_lock_level_drops_only_the_pickup_eta_lock()
    {
        var level = Decode("wp14-c1-nopickuplock-v1");

        Assert.Equal(
            PromiseLock.Vehicle | PromiseLock.PickupStop,
            level.FinalConfirmationLocks);
        Assert.Equal(PromiseLock.None, level.RatchetLocks);
        Assert.Equal(
            30_000,
            level.Limits[CommitmentDimension.DropEtaTotalMs].HardLimit);
    }

    [Fact]
    public void The_freeze_horizon_levels_are_refused_with_a_pointer_to_the_archive()
    {
        // The freeze horizon is removed, so the three archived levels that set it no longer decode.
        foreach (var name in new[]
                 {
                     "wp14-c1-f1-freeze300-v1",
                     "wp14-c1-f1-freeze600-v1",
                     "wp14-c1-f1f2-freeze300-ratchet-v1",
                 })
        {
            var error = Assert.Throws<InvalidDataException>(() => Load(name));

            Assert.Equal(
                "Commitment 'freezeHorizonMs' and 'freezeHorizonLocks' (the freeze horizon): removed; see commit "
                + "ebca912 (branch research/final-real-runs-2026-11-06)",
                error.Message);
        }
    }

    [Fact]
    public void Every_level_is_a_distinct_configuration()
    {
        var hashes = FactorConfigurations()
            .Cast<object[]>()
            .Select(row => Load((string)row[0]).ContentHash.Value)
            .ToArray();

        Assert.Equal(hashes.Length, hashes.Distinct(StringComparer.Ordinal).Count());
    }

    private static CommitmentPolicyConfiguration Load(string name)
    {
        var path = Path.Combine(
            RepositoryRoot(),
            "benchmarks",
            "configurations",
            $"{name}.json");
        return CommitmentPolicyConfiguration.Decode(File.ReadAllBytes(path));
    }

    private static CommitmentPolicy Decode(string name)
    {
        Assert.True(Load(name).TryGetPolicy(PolicyId, out var policy));
        return policy;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "RideBound.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Repository root not found.");
    }
}
