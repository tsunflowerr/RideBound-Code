using System.Text.Json;
using RideBound.Algorithms.Shareability;

namespace RideBound.Algorithms.Tests.Shareability;

/// <summary>
/// Reader and checker of golden shareability scores produced by the comparison emulator's own
/// scorer (gen_golden.js over sscore.js makeScorer and lambda.js; the source hashes are in the
/// file). A case passes only when every double is bit-identical and every count is equal.
/// Also linked into the full-network gate outside the repository.
/// </summary>
internal static class ShareabilityGolden
{
    public sealed record Outcome(int Cells, int Cases, int NonZero, int WithArc, int WeightVectors, IReadOnlyList<string> Failures);

    public static Outcome Check(JsonElement root, Func<JsonElement, ShareabilityNetwork> networkOf)
    {
        var failures = new List<string>();
        int cells = 0, cases = 0, nonZero = 0, withArc = 0, weightVectors = 0;
        foreach (var cellElement in root.GetProperty("cells").EnumerateArray())
        {
            var network = networkOf(cellElement.GetProperty("net"));
            var lambda = cellElement.GetProperty("lambda");
            var rates = ZoneDemandRates.Create(
                network,
                Doubles(lambda.GetProperty("arrivals")),
                Ints(lambda.GetProperty("zO")),
                Ints(lambda.GetProperty("zD")),
                Doubles(lambda.GetProperty("priorO")),
                Doubles(lambda.GetProperty("priorD")),
                lambda.GetProperty("phi").GetDouble(),
                lambda.GetProperty("alpha").GetDouble(),
                lambda.GetProperty("sampled").GetDouble(),
                lambda.GetProperty("intervalMs").GetDouble());

            var fallbacks = cellElement.GetProperty("fallbacks");
            if (rates.OriginFallbacks != fallbacks.GetProperty("O").GetInt32()
                || rates.DestinationFallbacks != fallbacks.GetProperty("D").GetInt32())
            {
                failures.Add($"cell {cells}: prior fallbacks differ");
            }

            foreach (var weights in cellElement.GetProperty("weights").EnumerateArray())
            {
                var interval = weights.GetProperty("i").GetInt64();
                var causal = rates.Causal(interval);
                var oracle = rates.Oracle(interval);
                Compare(failures, $"cell {cells} interval {interval} causal O", causal.Origin, weights.GetProperty("cO"));
                Compare(failures, $"cell {cells} interval {interval} causal D", causal.Destination, weights.GetProperty("cD"));
                Compare(failures, $"cell {cells} interval {interval} oracle O", oracle.Origin, weights.GetProperty("oO"));
                Compare(failures, $"cell {cells} interval {interval} oracle D", oracle.Destination, weights.GetProperty("oD"));
                weightVectors += 4;
            }

            var cellSpec = cellElement.GetProperty("cell");
            var scorer = new ShareabilityScorer(
                network,
                new ShareabilityCell(
                    cellSpec.GetProperty("kappa").GetDouble(),
                    Doubles(cellSpec.GetProperty("factors")),
                    Ints(cellSpec.GetProperty("globalOf")),
                    cellSpec.GetProperty("omegaP").GetDouble(),
                    cellSpec.GetProperty("strideMs").GetDouble(),
                    rates,
                    cellSpec.GetProperty("mode").GetString() == "oracle"));

            foreach (var caseElement in cellElement.GetProperty("cases").EnumerateArray())
            {
                var context = Context(caseElement.GetProperty("ctx"));
                var expected = caseElement.GetProperty("expected");
                var label = $"cell {cells} case {cases}";
                ShareabilityScore score;
                try
                {
                    score = scorer.Score(context);
                }
                catch (Exception exception)
                {
                    failures.Add($"{label}: {exception.GetType().Name} {exception.Message}");
                    cases++;
                    continue;
                }

                Same(failures, label, "S", score.Score, expected.GetProperty("S").GetDouble());
                Same(failures, label, "Sw", score.Waiting, expected.GetProperty("Sw").GetDouble());
                Same(failures, label, "Sd", score.Driving, expected.GetProperty("Sd").GetDouble());
                if (score.Instants != expected.GetProperty("n").GetInt32()
                    || score.PickupMainNode != expected.GetProperty("kP").GetInt32()
                    || score.DropMainNode != expected.GetProperty("kK").GetInt32()
                    || score.MainNodes != expected.GetProperty("nMain").GetInt32()
                    || score.ArcInstants != expected.GetProperty("nArc").GetInt32())
                {
                    failures.Add($"{label}: instant counts differ");
                }

                nonZero += expected.GetProperty("S").GetDouble() != 0 ? 1 : 0;
                withArc += context.Arc is null ? 0 : 1;
                cases++;
            }

            cells++;
        }

        return new Outcome(cells, cases, nonZero, withArc, weightVectors, failures);
    }

    public static ShareabilityNetwork ToyNetwork(JsonElement net) =>
        ShareabilityNetwork.Create(
            net.GetProperty("N").GetInt32(),
            net.GetProperty("D").EnumerateArray().Select(value => checked((ushort)value.GetInt32())).ToArray(),
            Ints(net.GetProperty("csrStart")),
            Ints(net.GetProperty("csrTo")),
            Ints(net.GetProperty("csrW")),
            Ints(net.GetProperty("zoneIdx")),
            net.GetProperty("nZones").GetInt32());

    private static ShareabilityContext Context(JsonElement ctx)
    {
        var start = ctx.GetProperty("start");
        var arc = ctx.GetProperty("arc");
        var stops = ctx.GetProperty("stops")
            .EnumerateArray()
            .Select(stop => new ShareabilityStop(
                stop.GetProperty("node").GetInt32(),
                stop.GetProperty("kind").GetInt32() == 0,
                stop.GetProperty("T").GetDouble(),
                stop.GetProperty("h").GetDouble(),
                stop.GetProperty("party").GetInt32()))
            .ToArray();
        return new ShareabilityContext(
            ctx.GetProperty("tEval").GetDouble(),
            ctx.GetProperty("bin").GetInt32(),
            start.GetProperty("node").GetInt32(),
            start.GetProperty("time").GetDouble(),
            arc.ValueKind == JsonValueKind.Null
                ? null
                : new ShareabilityArc(arc.GetProperty("from").GetInt32(), arc.GetProperty("pm").GetInt32()),
            ctx.GetProperty("load0").GetInt32(),
            ctx.GetProperty("cap").GetInt32(),
            stops,
            ctx.GetProperty("iP").GetInt32(),
            ctx.GetProperty("iD").GetInt32(),
            ctx.GetProperty("dwell").GetDouble());
    }

    private static void Same(List<string> failures, string label, string field, double actual, double expected)
    {
        if (BitConverter.DoubleToInt64Bits(actual) != BitConverter.DoubleToInt64Bits(expected))
        {
            failures.Add($"{label}: {field} {actual:R} != {expected:R}");
        }
    }

    private static void Compare(List<string> failures, string label, IReadOnlyList<double> actual, JsonElement expected)
    {
        var values = Doubles(expected);
        if (actual.Count != values.Length)
        {
            failures.Add($"{label}: length differs");
            return;
        }

        for (var z = 0; z < values.Length; z++)
        {
            Same(failures, label, $"[{z}]", actual[z], values[z]);
        }
    }

    private static double[] Doubles(JsonElement array) =>
        array.EnumerateArray().Select(value => value.GetDouble()).ToArray();

    private static int[] Ints(JsonElement array) =>
        array.EnumerateArray().Select(value => value.GetInt32()).ToArray();
}
