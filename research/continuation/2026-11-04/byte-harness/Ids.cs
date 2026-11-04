using System.Text;
using System.Text.Json;
using static H;

static class Ids
{
    static readonly string Repo = Environment.GetEnvironmentVariable("RIDEBOUND_REPO")!;
    static byte[] B(string s) => Encoding.UTF8.GetBytes(s);

    public static int Run()
    {
        var variants = new (string Name, (string From, string To)[] Subs)[]
        {
            ("ascii-escapes", new[] { ("\"n-0\"", "\"n-0\\\"\\\\\\u0001\\t/<>&'+\""), ("\"r-1\"", "\"r-1\\\"onlineState\\\":{}\""), ("\"v-1\"", "\"v-1\\u007f\"") }),
            ("unicode", new[] { ("\"n-0\"", "\"n-\u00e9\u4e2d\\ud83d\\ude00\\u2028\\u2029\""), ("\"n-1\"", "\"n-\\uff01\""), ("\"n-2\"", "\"n-\\ud83d\\ude01\""), ("\"r-1\"", "\"r-\\ufeff\u00e9\""), ("\"r-2\"", "\"r-\\ud83d\\ude00\"") }),
            ("runid-marker", new[] { ("\"wp3-demo-run\"", "\"x\\\"onlineState\\\":{}\""), ("\"wp3-commitment-tiny\"", "\"s\\\\\\\"onlineState\\\\\\\":{}\"") }),
        };
        var args = new[] { "--mode", "commitment", "--policy-config", $"{Repo}/benchmarks/configurations/wp3-boundary-test-v1.json" };
        foreach (var (name, subs) in variants)
        {
            var raw = File.ReadAllLines($"{Repo}/benchmarks/scenarios/wp3-commitment-tiny/commitment-demo.input.ndjson");
            var lines = new List<byte[]>();
            string runJson = "\"wp3-demo-run\"", scenJson = "\"wp3-commitment-tiny\"";
            foreach (var (from, to) in subs)
            {
                if (from == runJson) runJson = to;
                if (from == scenJson) scenJson = to;
            }
            var cp = B($"{{\"schemaVersion\":\"1.0.0\",\"messageType\":\"checkpoint\",\"runId\":{runJson},\"scenarioId\":{scenJson},\"payload\":{{}}}}");
            foreach (var l0 in raw)
            {
                var l = l0;
                foreach (var (from, to) in subs) l = l.Replace(from, to);
                if (l.Contains("\"decisionApplied\"")) { lines.Add(B("@APPLY")); lines.Add(cp); continue; }
                lines.Add(B(l));
                if (l.Contains("\"initializeRun\"")) lines.Add(cp);
            }
            Compare($"ids {name}", args, lines);
            var outLines = Split(LastNew.Stdout);
            Console.WriteLine($"   decisions {outLines.Count(x => x.Contains("\"messageType\":\"decision\""))}, errors {outLines.Count(x => x.Contains("\"messageType\":\"error\""))}, checkpoints {outLines.Count(x => x.Contains("\"messageType\":\"checkpoint\""))}");
            foreach (var e in outLines.Where(x => x.Contains("\"messageType\":\"error\"")).Take(2)) Console.WriteLine("   err: " + (e.Length > 300 ? e[..300] : e));
            var cps = outLines.Where(x => x.Contains("\"messageType\":\"checkpoint\"")).ToList();
            var batches = lines.Where(x => Encoding.UTF8.GetString(x).Contains("\"eventBatch\"")).ToList();
            for (int k = 1; k < cps.Count; k++)
            {
                using var doc = JsonDocument.Parse(cps[k]);
                var payload = doc.RootElement.GetProperty("payload").GetRawText();
                var restore = B($"{{\"schemaVersion\":\"1.0.0\",\"messageType\":\"restore\",\"runId\":{runJson},\"scenarioId\":{scenJson},\"payload\":{payload}}}");
                var input = new List<byte[]> { lines[0], lines[1], restore };
                foreach (var b in batches.Skip(k)) { input.Add(b); input.Add(B("@APPLY")); input.Add(cp); }
                Compare($"   restore@{k}", args, input);
                if (k == 1)
                {
                    var o = Split(LastNew.Stdout);
                    Console.WriteLine($"     restored? {o.Any(x => x.Contains("\"messageType\":\"restore\""))} errors {o.Count(x => x.Contains("\"messageType\":\"error\""))}");
                }
            }
        }
        Console.WriteLine($"FAILS {Fails}");
        return 0;
    }
}
