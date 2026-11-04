using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using RideBound.Algorithms.Shareability;

namespace RideBound.Runner.Configuration;

/// <summary>
/// ADR-089 GĐ4 (exploratory): the `shadowAllocation` key of the WP4 configuration. Per-cell
/// Shadow Control inputs (the comparison emulator's makeCellCtx and frozen threshold) and the full
/// free-flow network, read from a directory whose two files must match the given SHA-256 values:
/// D.u16 (N*N little-endian uint16 seconds) and network.json (N, nZones, csrStart, csrTo, csrW,
/// zoneIdx). Doubles are decimal strings because canonical JSON holds integers only.
/// `networkNodes` maps every run node identifier of the cell (the simulator adapter's opaque ids) to its
/// network node index; made from the adapter's own identifier function (ADR-089 GĐ4 gate, 2026-11-04).
/// </summary>
public static class ShadowAllocationConfiguration
{
    public static readonly IReadOnlySet<string> Fields = new HashSet<string>(StringComparer.Ordinal)
    {
        "networkDirectory",
        "distancesSha256",
        "graphSha256",
        "kappa",
        "factors",
        "pickupWindowMs",
        "strideMs",
        "intervalMs",
        "alpha",
        "phi",
        "sampled",
        "priorOrigin",
        "priorDestination",
        "threshold",
        "binMs",
        "networkNodes",
    };

    public static ShadowAllocationInputs Read(JsonElement element)
    {
        var directory = Text(element, "networkDirectory");
        var distancesBytes = File.ReadAllBytes(Path.Combine(directory, "D.u16"));
        var graphBytes = File.ReadAllBytes(Path.Combine(directory, "network.json"));
        RequireSha256(distancesBytes, Text(element, "distancesSha256"), "D.u16");
        RequireSha256(graphBytes, Text(element, "graphSha256"), "network.json");

        using var graph = JsonDocument.Parse(graphBytes);
        var root = graph.RootElement;
        int[] Ints(string name) => root.GetProperty(name).EnumerateArray().Select(value => value.GetInt32()).ToArray();
        var distances = new ushort[distancesBytes.Length / 2];
        Buffer.BlockCopy(distancesBytes, 0, distances, 0, distances.Length * 2);
        var network = ShareabilityNetwork.Create(
            root.GetProperty("N").GetInt32(),
            distances,
            Ints("csrStart"),
            Ints("csrTo"),
            Ints("csrW"),
            Ints("zoneIdx"),
            root.GetProperty("nZones").GetInt32());

        var priorOrigin = Counts(element, "priorOrigin");
        var priorDestination = Counts(element, "priorDestination");

        if (priorOrigin.Length != network.ZoneCount || priorDestination.Length != network.ZoneCount)
        {
            throw new InvalidDataException("shadowAllocation priors need one count per network zone.");
        }

        return new ShadowAllocationInputs(
            network,
            Decimal(element.GetProperty("kappa"), "kappa"),
            element.GetProperty("factors").EnumerateArray().Select(value => Decimal(value, "factors")).ToArray(),
            Integer(element, "pickupWindowMs"),
            Integer(element, "strideMs"),
            AtLeastAMinute(element, "intervalMs"),
            Decimal(element.GetProperty("alpha"), "alpha"),
            Decimal(element.GetProperty("phi"), "phi"),
            Integer(element, "sampled"),
            priorOrigin,
            priorDestination,
            Decimal(element.GetProperty("threshold"), "threshold"),
            AtLeastAMinute(element, "binMs"),
            NetworkNodes(element, network.NodeCount));
    }

    private static IReadOnlyDictionary<string, int> NetworkNodes(JsonElement element, int nodeCount)
    {
        if (!element.TryGetProperty("networkNodes", out var value) || value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("shadowAllocation.networkNodes must be an object.");
        }

        var nodes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (property.Name.Length == 0
                || property.Value.ValueKind != JsonValueKind.Number
                || !property.Value.TryGetInt32(out var index)
                || index < 0
                || index >= nodeCount)
            {
                throw new InvalidDataException(
                    "shadowAllocation.networkNodes maps node identifiers to network node indices.");
            }

            if (!nodes.TryAdd(property.Name, index))
            {
                throw new InvalidDataException("shadowAllocation.networkNodes repeats a node identifier.");
            }
        }

        return nodes.Count != 0
            ? nodes
            : throw new InvalidDataException("shadowAllocation.networkNodes must not be empty.");
    }

    private static void RequireSha256(byte[] bytes, string expected, string name)
    {
        if (!string.Equals(Convert.ToHexStringLower(SHA256.HashData(bytes)), expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"shadowAllocation {name} does not match its SHA-256.");
        }
    }

    private static string Text(JsonElement element, string name) =>
        element.GetProperty(name).ValueKind == JsonValueKind.String
            ? element.GetProperty(name).GetString()!
            : throw new InvalidDataException($"shadowAllocation.{name} must be a string.");

    private static long Integer(JsonElement element, string name) =>
        element.GetProperty(name) is { ValueKind: JsonValueKind.Number } value
        && value.TryGetInt64(out var result)
        && result >= 0
            ? result
            : throw new InvalidDataException($"shadowAllocation.{name} must be a non-negative integer.");

    // intervalMs and binMs divide time into rate intervals and factor bins (the emulator's 600 s and
    // 900 s). A floor of a minute keeps them away from zero and from millions of empty intervals.
    private static long AtLeastAMinute(JsonElement element, string name) =>
        Integer(element, name) is >= 60_000 and var result
            ? result
            : throw new InvalidDataException($"shadowAllocation.{name} must be at least 60000.");

    private static double[] Counts(JsonElement element, string name) =>
        element.GetProperty(name).EnumerateArray()
            .Select(value => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var count) && count >= 0
                ? (double)count
                : throw new InvalidDataException($"shadowAllocation.{name} holds non-negative integers."))
            .ToArray();

    // A decimal string parses to the exact double JavaScript wrote; "+Infinity"/"-Infinity" for theta 0 / 100 %.
    private static double Decimal(JsonElement value, string name)
    {
        var text = value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : throw new InvalidDataException($"shadowAllocation.{name} holds decimal strings.");

        return text switch
        {
            "+Infinity" => double.PositiveInfinity,
            "-Infinity" => double.NegativeInfinity,
            _ when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
                && double.IsFinite(result) => result,
            _ => throw new InvalidDataException($"shadowAllocation.{name} holds decimal strings."),
        };
    }
}
