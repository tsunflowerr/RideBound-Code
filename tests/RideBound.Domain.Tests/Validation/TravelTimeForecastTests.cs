using System.Text.Json;
using RideBound.Domain.Common;
using RideBound.Domain.Validation;

namespace RideBound.Domain.Tests.Validation;

public sealed class TravelTimeForecastTests
{
    [Fact]
    public void Arrivals_equal_the_adapter_mode_emulator_leg_function()
    {
        // 40 random profiles x 75 legs from legF of engine_wf15_clint.js (gen_forecast_golden.js, seed 20261103).
        using var stream = typeof(TravelTimeForecastTests).Assembly
            .GetManifestResourceStream("RideBound.Domain.Tests.Validation.travel-forecast-golden.json")!;
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        var bin = root.GetProperty("binMs").GetInt64();
        var failures = new List<string>();
        var cases = 0;
        var stretched = 0;

        foreach (var entry in root.GetProperty("profiles").EnumerateArray())
        {
            var forecast = new TravelTimeForecast(
                entry.GetProperty("profile").EnumerateArray().Select(value => value.GetDouble()).ToArray(),
                bin);

            foreach (var item in entry.GetProperty("cases").EnumerateArray())
            {
                var values = item.EnumerateArray().Select(value => value.GetInt64()).ToArray();
                var arrival = forecast.Arrive(
                    new SimTime(values[0]),
                    new SimTime(values[1]),
                    new Duration(values[2]));

                if (arrival.Milliseconds != values[3])
                {
                    failures.Add($"case {cases}: {arrival.Milliseconds} != {values[3]}");
                }

                stretched += values[3] != values[1] + values[2] ? 1 : 0;
                cases++;
            }
        }

        Assert.Empty(failures);
        Assert.Equal(3000, cases);
        Assert.True(stretched > 1000, $"only {stretched} legs differ from the flat sum");
    }

    [Fact]
    public void A_leg_inside_the_evaluation_bin_is_not_stretched()
    {
        var forecast = new TravelTimeForecast([1.0, 2.0], 900_000);

        Assert.Equal(400_000, forecast.Arrive(new SimTime(0), new SimTime(100_000), new Duration(300_000)).Milliseconds);
        // 800 s left in bin 0 at weight 1, the remaining 100 s at weight 2: 900 s + 200 s.
        Assert.Equal(1_100_000, forecast.Arrive(new SimTime(0), new SimTime(100_000), new Duration(900_000)).Milliseconds);
        // Evaluated in bin 1 (factor 2): bin 1 has weight 1 and bin 0 weight 1 / 2.
        Assert.Equal(500_000, forecast.Arrive(new SimTime(900_000), new SimTime(0), new Duration(1_000_000)).Milliseconds);
    }

    [Fact]
    public void A_plain_lookup_adds_the_leg_time()
    {
        ITravelTimeLookup lookup = new FlatLookup();

        Assert.Equal(1_234, lookup.Arrive(new SimTime(5), new SimTime(1_000), new Duration(234)).Milliseconds);
    }

    private sealed class FlatLookup : ITravelTimeLookup
    {
        public bool TryGetTravelTime(NodeId fromNodeId, NodeId toNodeId, out Duration travelTime)
        {
            travelTime = new Duration(0);
            return true;
        }
    }
}
