using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using static H;

static class Light
{
    static readonly string Repo = Environment.GetEnvironmentVariable("RIDEBOUND_REPO")!;
    static byte[] B(string s) => Encoding.UTF8.GetBytes(s);

    public static int Run()
    {
        var cfgs = new[]
        {
            ("light", $"{Repo}/research/continuation/2026-11-04/byte-harness/cfg/wp4-rolling-light.json", "c984ba907e953f824ae241aed21d520ed2bb11d71bddeb3eea2788c699dabbe4"),
            ("full", $"{Repo}/research/continuation/2026-11-04/byte-harness/cfg/wp4-rolling-full.json", "ab55da5f48b7d9659140a2f2be519b195a67efa3d2490804c21b21c63f45acab"),
        };
        foreach (var envV2 in new[] { false, true })
        {
            Env.Clear();
            if (envV2) Env["RIDEBOUND_STATE_HASH"] = "v2";
            foreach (var (name, path, binding) in cfgs)
            {
                var raw = File.ReadAllLines($"{Repo}/benchmarks/fixtures/wp6/e2e/tiny/paired.input.ndjson");
                var lines = new List<byte[]>();
                string runId = null, scenarioId = null;
                foreach (var l in raw)
                {
                    if (l.Contains("\"initializeRun\""))
                    {
                        var n = JsonNode.Parse(l)!;
                        var m = n["payload"]!["manifest"]!;
                        m["policyId"] = "rolling-cost"; m["policyVersion"] = "wp4-boundary-v1"; m["policyConfigurationHash"] = binding;
                        runId = n["runId"]!.GetValue<string>(); scenarioId = n["scenarioId"]!.GetValue<string>();
                        lines.Add(B(n.ToJsonString()));
                        lines.Add(Cp(runId, scenarioId));
                    }
                    else if (l.Contains("\"eventBatch\"")) { lines.Add(B(l)); lines.Add(B("@APPLY")); lines.Add(Cp(runId, scenarioId)); }
                    else lines.Add(B(l));
                }
                var args = new[] { "--mode", "commitment", "--policy-config", $"{Repo}/benchmarks/configurations/wp3-boundary-test-v1.json", "--wp4-config", path, "--solver-seed-source", "manifest-master-seed" };
                var run = Drive(New, args, lines, "\n"u8.ToArray());
                var outLines = Split(run.Stdout);
                Console.WriteLine($"{name} v2env={envV2}: exit {run.Exit} decisions {outLines.Count(x => x.Contains("\"messageType\":\"decision\""))} checkpoints {outLines.Count(x => x.Contains("\"messageType\":\"checkpoint\""))} errors {outLines.Count(x => x.Contains("\"messageType\":\"error\""))}");
                foreach (var e in outLines.Where(x => x.Contains("\"messageType\":\"error\"")).Take(2)) Console.WriteLine("   err: " + (e.Length > 300 ? e[..300] : e));
                var decisionsFull = outLines.Where(x => x.Contains("\"messageType\":\"decision\"")).ToList();
                var cps = outLines.Where(x => x.Contains("\"messageType\":\"checkpoint\"")).ToList();
                var batches = lines.Where(x => Encoding.UTF8.GetString(x).Contains("\"eventBatch\"")).ToList();
                for (int k = 1; k < cps.Count; k++)
                {
                    using var doc = JsonDocument.Parse(cps[k]);
                    var payload = doc.RootElement.GetProperty("payload").GetRawText();
                    var restore = B($"{{\"schemaVersion\":\"1.0.0\",\"messageType\":\"restore\",\"runId\":\"{runId}\",\"scenarioId\":\"{scenarioId}\",\"payload\":{payload}}}");
                    var input = new List<byte[]> { lines[0], lines[1], restore };
                    foreach (var b in batches.Skip(k)) { input.Add(b); input.Add(B("@APPLY")); input.Add(Cp(runId, scenarioId)); }
                    var r2 = Drive(New, args, input, "\n"u8.ToArray());
                    var o2 = Split(r2.Stdout);
                    var dec2 = o2.Where(x => x.Contains("\"messageType\":\"decision\"")).ToList();
                    var cps2 = o2.Where(x => x.Contains("\"messageType\":\"checkpoint\"")).ToList();
                    var sameDec = dec2.SequenceEqual(decisionsFull.Skip(k));
                    var sameCp = cps2.SequenceEqual(cps.Skip(k + 1));
                    Console.WriteLine($"   restore@{k}: restored {o2.Any(x => x.Contains("\"messageType\":\"restore\""))} errors {o2.Count(x => x.Contains("\"messageType\":\"error\""))} decisions-equal-suffix {sameDec} checkpoints-equal-suffix {sameCp}");
                    foreach (var e in o2.Where(x => x.Contains("\"messageType\":\"error\"")).Take(1)) Console.WriteLine("     err: " + (e.Length > 300 ? e[..300] : e));
                }
            }
        }
        return 0;
    }

    static byte[] Cp(string runId, string scenarioId) =>
        B($"{{\"schemaVersion\":\"1.0.0\",\"messageType\":\"checkpoint\",\"runId\":\"{runId}\",\"scenarioId\":\"{scenarioId}\",\"payload\":{{}}}}");
}
