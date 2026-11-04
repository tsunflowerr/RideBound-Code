using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RideBound.Runner.Configuration;

namespace RideBound.Runner.Tests.Configuration;

/// <summary>ADR-089 GĐ4: the shadowAllocation block, in particular its networkNodes map (Shadow gate, 2026-11-04).</summary>
public sealed class ShadowAllocationConfigurationTests : IDisposable
{
    private readonly DirectoryInfo _network = Directory.CreateTempSubdirectory("rb-shadow-network-");

    public ShadowAllocationConfigurationTests()
    {
        File.WriteAllBytes(Path.Combine(_network.FullName, "D.u16"), [0, 0, 5, 0, 5, 0, 0, 0]);
        File.WriteAllText(
            Path.Combine(_network.FullName, "network.json"),
            "{\"N\":2,\"nZones\":1,\"csrStart\":[0,1,2],\"csrTo\":[1,0],\"csrW\":[5,5],\"zoneIdx\":[0,0]}",
            new UTF8Encoding(false));
    }

    public void Dispose() => _network.Delete(recursive: true);

    private JsonObject Block(JsonNode? networkNodes)
    {
        string Sha(string name) => Convert.ToHexStringLower(
            SHA256.HashData(File.ReadAllBytes(Path.Combine(_network.FullName, name))));
        var block = new JsonObject
        {
            ["networkDirectory"] = _network.FullName,
            ["distancesSha256"] = Sha("D.u16"),
            ["graphSha256"] = Sha("network.json"),
            ["kappa"] = "3.0742501276388534",
            ["factors"] = new JsonArray("1", "1.063821"),
            ["pickupWindowMs"] = 600000,
            ["strideMs"] = 0,
            ["intervalMs"] = 600000,
            ["alpha"] = "0.15",
            ["phi"] = "0.11772821338238676",
            ["sampled"] = 512,
            ["priorOrigin"] = new JsonArray(3),
            ["priorDestination"] = new JsonArray(4),
            ["threshold"] = "66.72067841389695",
            ["binMs"] = 900000,
        };
        if (networkNodes is not null)
        {
            block["networkNodes"] = networkNodes;
        }

        return block;
    }

    private static JsonElement Element(JsonObject block) => JsonDocument.Parse(block.ToJsonString()).RootElement;

    [Fact]
    public void Network_nodes_map_run_identifiers_to_network_indices()
    {
        var inputs = ShadowAllocationConfiguration.Read(Element(Block(new JsonObject
        {
            ["node-825512e813deba6eb575fedefddfd4109f5cf0be2a99003d0958a506283f57f1"] = 1,
            ["node-a"] = 0,
        })));

        Assert.Equal(2, inputs.NetworkNodes.Count);
        Assert.Equal(1, inputs.NetworkNodes["node-825512e813deba6eb575fedefddfd4109f5cf0be2a99003d0958a506283f57f1"]);
        Assert.Equal(0, inputs.NetworkNodes["node-a"]);
        Assert.Equal(3.0742501276388534, inputs.Kappa);
        Assert.Contains("networkNodes", ShadowAllocationConfiguration.Fields);
    }

    [Fact]
    public void Network_nodes_are_required_well_formed_and_in_range()
    {
        Assert.Throws<InvalidDataException>(() => ShadowAllocationConfiguration.Read(Element(Block(null))));
        Assert.Throws<InvalidDataException>(() => ShadowAllocationConfiguration.Read(Element(Block(new JsonArray()))));
        Assert.Throws<InvalidDataException>(() => ShadowAllocationConfiguration.Read(Element(Block(new JsonObject()))));
        Assert.Throws<InvalidDataException>(() => ShadowAllocationConfiguration.Read(Element(Block(
            new JsonObject { ["node-a"] = 2 }))));                       // network has nodes 0 and 1
        Assert.Throws<InvalidDataException>(() => ShadowAllocationConfiguration.Read(Element(Block(
            new JsonObject { ["node-a"] = -1 }))));
        Assert.Throws<InvalidDataException>(() => ShadowAllocationConfiguration.Read(Element(Block(
            new JsonObject { ["node-a"] = "1" }))));
    }

    [Fact]
    public void The_rate_interval_and_the_score_bin_must_be_positive()
    {
        JsonObject With(string name, long value)
        {
            var block = Block(new JsonObject { ["node-a"] = 0 });
            block[name] = value;
            return block;
        }

        Assert.Contains("intervalMs", Assert.Throws<InvalidDataException>(
            () => ShadowAllocationConfiguration.Read(Element(With("intervalMs", 0)))).Message);
        Assert.Contains("binMs", Assert.Throws<InvalidDataException>(
            () => ShadowAllocationConfiguration.Read(Element(With("binMs", 0)))).Message);

        // A zero stride or pickup window is a defined, if degenerate, score.
        ShadowAllocationConfiguration.Read(Element(With("strideMs", 0)));
        ShadowAllocationConfiguration.Read(Element(With("pickupWindowMs", 0)));
    }

    [Fact]
    public void Shadow_is_refused_with_the_origin_hold_schedule()
    {
        var root = Path.GetDirectoryName(typeof(ShadowAllocationConfigurationTests).Assembly.Location)!;
        while (!File.Exists(Path.Combine(root, "RideBound.slnx")))
        {
            root = Path.GetDirectoryName(root)!;
        }

        string Read(string name) =>
            File.ReadAllText(Path.Combine(root, "benchmarks", "configurations", name));
        var shadowCommitment = CommitmentPolicyConfiguration.Decode(Encoding.UTF8.GetBytes(
            Read("wp3-boundary-test-v1.json").Replace(
                "\"budgetBasis\": \"decisionInduced\",",
                "\"budgetBasis\": \"decisionInduced\",\n      \"dropEtaBudgetSlackPermille\": 1000,\n"
                    + "      \"dropEtaShadowAllocation\": true,",
                StringComparison.Ordinal)));
        var plainCommitment = CommitmentPolicyConfiguration.Decode(
            Encoding.UTF8.GetBytes(Read("wp3-boundary-test-v1.json")));
        var block = Block(new JsonObject { ["node-a"] = 0 }).ToJsonString();
        string Wp4(string strategy, bool shadow) => Read("wp4-rolling-cost-boundary-v1.json")
            .Replace("\"earliest-feasible\"", $"\"{strategy}\"", StringComparison.Ordinal)
            .Replace(
                "\"policyVersion\": \"wp4-boundary-v1\"",
                "\"policyVersion\": \"wp4-boundary-v1\"" + (shadow ? ",\n  \"shadowAllocation\": " + block : ""),
                StringComparison.Ordinal);

        Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(Wp4("earliest-feasible", shadow: true)), shadowCommitment);
        Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(Wp4("origin-hold-relocated-wait", shadow: false)), plainCommitment);
        Assert.Contains("earliest-feasible", Assert.Throws<InvalidDataException>(
            () => Wp4RunnerConfiguration.Decode(
                Encoding.UTF8.GetBytes(Wp4("origin-hold-relocated-wait", shadow: true)),
                shadowCommitment)).Message);
    }

    [Fact]
    public void A_repeated_node_identifier_is_refused()
    {
        var json = Block(new JsonObject { ["node-a"] = 0 }).ToJsonString()
            .Replace("{\"node-a\":0}", "{\"node-a\":0,\"node-a\":1}", StringComparison.Ordinal);
        using var document = JsonDocument.Parse(json);

        Assert.Throws<InvalidDataException>(() => ShadowAllocationConfiguration.Read(document.RootElement));
    }
}
