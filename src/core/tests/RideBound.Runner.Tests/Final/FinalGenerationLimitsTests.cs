using System.Text.Json.Nodes;
using RideBound.Algorithms.Candidates;
using RideBound.Runner.Configuration;

namespace RideBound.Runner.Tests.Final;

// The final WP4 files decode to the candidate-generation limits the final runs used: at most 100 candidates and
// 2 new requests per vehicle, 10,000 exploration work units, and k = 8 relocation target vehicles for G2 (none for
// INS).
// The decoder reads the Shadow network relative to the process working directory, so the test points it at the
// fixture folder.
public sealed class FinalGenerationLimitsTests
{
    [Theory]
    [InlineData("SHHZ71.json", "G2-N-light-v1.json", 8)]
    [InlineData("SHRC71.json", "G2-CL-full-v1.json", 8)]
    [InlineData("INS-SHRC.json", "INS-CL-light-v1.json", null)]
    public void The_final_wp4_files_decode_to_the_final_generation_limits(string commitment, string wp4, int? targets)
    {
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(FinalRuns.FixtureDirectory, "wp4", wp4)))!.AsObject();
        json["shadowAllocation"]!["networkDirectory"] = Path.Combine(FinalRuns.FixtureDirectory, "network");
        var configuration = Wp4RunnerConfiguration.Decode(
            System.Text.Encoding.UTF8.GetBytes(json.ToJsonString()),
            CommitmentPolicyConfiguration.Decode(
                File.ReadAllBytes(Path.Combine(FinalRuns.FixtureDirectory, "commitment", commitment))));
        var generation = configuration.CandidateGeneration;

        Assert.Equal(100, generation.MaximumCandidatesPerVehicle);
        Assert.Equal(2, generation.MaximumNewRequestsPerVehicle);
        Assert.Equal(10_000, generation.MaximumExplorationWorkUnits);
        Assert.False(generation.ExactSmallMode);
        Assert.Equal(CandidateRetentionStrategy.ServiceSetStabilityPortfolioV1, generation.RetentionStrategy);
        Assert.Equal(targets, generation.WaitingRiderRelocation?.MaximumTargetVehicles);
        Assert.Null(generation.WaitingRiderRelocation?.MaximumRidersPerVehicle);
    }
}
