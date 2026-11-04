using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using static H;

static class Wp4
{
    static readonly string Repo = Environment.GetEnvironmentVariable("RIDEBOUND_REPO")!;
    static byte[] B(string s) => Encoding.UTF8.GetBytes(s);

    public static int Run(string bindingsPath)
    {
        foreach (var row in File.ReadAllLines(bindingsPath))
        {
            var f = row.Split('|');
            if (f.Length != 5) continue;
            var (commitment, wp4, policyId, policyVersion, binding) = (f[0], f[1], f[2], f[3], f[4]);
            var policySub = commitment.StartsWith("wp3") ? null : "wp6-synthetic-policy-overlay-v1";
            foreach (var scen in new[] { "wp3", "wp6" })
            {
                var src = scen == "wp3"
                    ? $"{Repo}/benchmarks/scenarios/wp3-commitment-tiny/commitment-demo.input.ndjson"
                    : $"{Repo}/benchmarks/fixtures/wp6/e2e/tiny/paired.input.ndjson";
                var raw = File.ReadAllLines(src);
                var lines = new List<byte[]>();
                string runId = null, scenarioId = null;
                foreach (var l0 in raw)
                {
                    var l = policySub is null ? l0 : l0.Replace("\"uniform-v1\"", $"\"{policySub}\"");
                    if (l.Contains("\"initializeRun\""))
                    {
                        var n = JsonNode.Parse(l)!;
                        var m = n["payload"]!["manifest"]!;
                        m["policyId"] = policyId; m["policyVersion"] = policyVersion; m["policyConfigurationHash"] = binding;
                        runId = n["runId"]!.GetValue<string>(); scenarioId = n["scenarioId"]!.GetValue<string>();
                        lines.Add(B(n.ToJsonString()));
                        lines.Add(Cp(runId, scenarioId));
                    }
                    else if (l.Contains("\"decisionApplied\"")) { }
                    else if (l.Contains("\"eventBatch\""))
                    {
                        lines.Add(B(l)); lines.Add(B("@APPLY")); lines.Add(Cp(runId, scenarioId));
                    }
                    else lines.Add(B(l));
                }
                var args = new[] { "--mode", "commitment", "--policy-config", $"{Repo}/benchmarks/configurations/{commitment}", "--wp4-config", $"{Repo}/benchmarks/configurations/{wp4}", "--solver-seed-source", "manifest-master-seed" };
                var name = $"{scen} {commitment} + {wp4}";
                Compare(name, args, lines);
                var outLines = Split(LastNew.Stdout);
                var decisions = outLines.Count(x => x.Contains("\"messageType\":\"decision\""));
                var errors = outLines.Count(x => x.Contains("\"messageType\":\"error\""));
                var accepted = outLines.Count(x => x.Contains("\"accepted\"") || x.Contains("ACCEPTED"));
                Console.WriteLine($"   decisions {decisions}, errors {errors}");
                if (errors > 0) Console.WriteLine("   first error: " + outLines.First(x => x.Contains("\"messageType\":\"error\"")));
                // restore from each checkpoint
                var cps = outLines.Where(x => x.Contains("\"messageType\":\"checkpoint\"")).ToList();
                var batches = lines.Where(x => Encoding.UTF8.GetString(x).Contains("\"eventBatch\"")).ToList();
                for (int k = 1; k < cps.Count; k++)
                {
                    using var doc = JsonDocument.Parse(cps[k]);
                    var payload = doc.RootElement.GetProperty("payload").GetRawText();
                    var restore = B($"{{\"schemaVersion\":\"1.0.0\",\"messageType\":\"restore\",\"runId\":\"{runId}\",\"scenarioId\":\"{scenarioId}\",\"payload\":{payload}}}");
                    var input = new List<byte[]> { lines[0], lines[1], restore };
                    foreach (var b in batches.Skip(k)) { input.Add(b); input.Add(B("@APPLY")); input.Add(Cp(runId, scenarioId)); }
                    Compare($"   restore@{k}", args, input);
                }
            }
        }
        Console.WriteLine($"FAILS {Fails}");
        return 0;
    }

    static byte[] Cp(string runId, string scenarioId) =>
        B($"{{\"schemaVersion\":\"1.0.0\",\"messageType\":\"checkpoint\",\"runId\":\"{runId}\",\"scenarioId\":\"{scenarioId}\",\"payload\":{{}}}}");
}
