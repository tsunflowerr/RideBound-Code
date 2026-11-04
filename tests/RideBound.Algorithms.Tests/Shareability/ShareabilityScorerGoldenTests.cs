using System.Text.Json;
using RideBound.Algorithms.Shareability;

namespace RideBound.Algorithms.Tests.Shareability;

public sealed class ShareabilityScorerGoldenTests
{
    [Fact]
    public void Scores_and_zone_weights_are_bit_identical_to_the_emulator_on_toy_networks()
    {
        // 24 cells x 10 contexts from the emulator's sscore.js / lambda.js (gen_golden.js toy, seed 20261103).
        using var stream = typeof(ShareabilityScorerGoldenTests).Assembly
            .GetManifestResourceStream("RideBound.Algorithms.Tests.Shareability.shareability-golden-toy.json")!;
        using var document = JsonDocument.Parse(stream);

        var outcome = ShareabilityGolden.Check(document.RootElement, ShareabilityGolden.ToyNetwork);

        Assert.Empty(outcome.Failures);
        Assert.Equal(24, outcome.Cells);
        Assert.Equal(240, outcome.Cases);
        Assert.Equal(167, outcome.NonZero);
        Assert.Equal(91, outcome.WithArc);
    }

    [Fact]
    public void Distance_thresholds_are_the_largest_integers_within_the_bound()
    {
        Assert.Equal(double.PositiveInfinity, ShareabilityScorer.DMaxMul(1000, double.PositiveInfinity));
        Assert.Equal(-1, ShareabilityScorer.DMaxMul(1000, -1));
        Assert.Equal(-1, ShareabilityScorer.DMaxMul(1000, double.NaN));
        Assert.Equal(0, ShareabilityScorer.DMaxMul(1000, 999));
        Assert.Equal(1, ShareabilityScorer.DMaxMul(1000, 1000));
        // fl(0.1 * 3) = 0.30000000000000004 > 0.3, so 2 is the largest d with fl(0.1 * d) <= 0.3
        Assert.Equal(2, ShareabilityScorer.DMaxMul(0.1, 0.3));

        Assert.Equal(-1, ShareabilityScorer.DMaxAdd(1000, 601, 600));
        Assert.Equal(0, ShareabilityScorer.DMaxAdd(1000, 600, 600));
        Assert.Equal(3, ShareabilityScorer.DMaxAdd(1000, 0, 3999));
    }

    [Fact]
    public void A_context_whose_drop_is_at_the_vehicle_start_is_refused()
    {
        var network = ShareabilityNetwork.Create(2, [0, 5, 5, 0], [0, 1, 2], [1, 0], [5, 5], [0, 0], 1);
        var rates = ZoneDemandRates.Create(network, [], [], [], [1], [1], 1, 0.5, 1);
        var scorer = new ShareabilityScorer(network, new ShareabilityCell(1, [1], [0, 1], 600_000, 0, rates, false));
        var stops = new[]
        {
            new ShareabilityStop(0, true, 0, 0, 1),
            new ShareabilityStop(0, false, 0, 0, 1),
        };

        Assert.Throws<InvalidOperationException>(
            () => scorer.Score(new ShareabilityContext(0, 0, 0, 0, null, 0, 2, stops, 0, 1, 0)));
    }
}
