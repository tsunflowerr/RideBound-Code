using System.Text;
using RideBound.Runner.Configuration;
using RideBound.Runner.Protocol;

namespace RideBound.Runner.Tests.Protocol;

/// <summary>Tests that change the process environment run alone, after every parallel test.</summary>
[CollectionDefinition(nameof(ProcessEnvironmentCollection), DisableParallelization = true)]
public sealed class ProcessEnvironmentCollection;

/// <summary>
/// ADR-089: `auditHash: light-v1` replaces the state hash with an identity hash, while the
/// RIDEBOUND_STATE_HASH=v2 prototype hashes the whole state, so a session refuses the two together
/// instead of failing at its first checkpoint.
/// </summary>
[Collection(nameof(ProcessEnvironmentCollection))]
public sealed class RunnerSessionAuditHashTests
{
    [Fact]
    public async Task Light_audit_is_refused_with_the_v2_state_hash_prototype()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "RideBound.slnx")))
        {
            root = Path.GetDirectoryName(root)!;
        }

        string Configuration(string name) => Path.Combine(root, "benchmarks", "configurations", name);
        var commitment = CommitmentPolicyConfiguration.Decode(
            File.ReadAllBytes(Configuration("wp3-boundary-test-v1.json")));
        var light = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(File.ReadAllText(Configuration("wp4-rolling-cost-boundary-v1.json")).Replace(
                "\"policyVersion\": \"wp4-boundary-v1\"",
                "\"policyVersion\": \"wp4-boundary-v1\",\n  \"auditHash\": \"light-v1\"",
                StringComparison.Ordinal)),
            commitment);
        RunnerSession Session() => new(
            RunnerDefaults.CapabilityRequirements,
            RunnerExecutionMode.OnlineCommitment,
            commitmentPolicies: commitment,
            stopDistances: commitment,
            commitmentPolicyConfigurationHash: commitment.ContentHash,
            wp4Configuration: light);

        var original = Environment.GetEnvironmentVariable("RIDEBOUND_STATE_HASH");
        try
        {
            Environment.SetEnvironmentVariable("RIDEBOUND_STATE_HASH", null);
            Session();

            Environment.SetEnvironmentVariable("RIDEBOUND_STATE_HASH", "v2");
            Assert.Contains("light-v1", Assert.Throws<ArgumentException>(Session).Message);

            // The process host reports it as a configuration error instead of crashing.
            using var diagnostics = new StringWriter();
            var exitCode = await RunnerHost.RunAsync(
                new MemoryStream(),
                new MemoryStream(),
                diagnostics,
                executionMode: RunnerExecutionMode.OnlineCommitment,
                commitmentPolicies: commitment,
                stopDistances: commitment,
                commitmentPolicyConfigurationHash: commitment.ContentHash,
                wp4Configuration: light);
            Assert.Equal(64, exitCode);
            Assert.Contains("light-v1", diagnostics.ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("RIDEBOUND_STATE_HASH", original);
        }
    }
}
