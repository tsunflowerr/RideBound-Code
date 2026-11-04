using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static H;

static class MainProgram
{
    static readonly string Repo = Environment.GetEnvironmentVariable("RIDEBOUND_REPO")!;
    static byte[] B(string s) => Encoding.UTF8.GetBytes(s);

    static int Main(string[] argv)
    {
        if (argv.Length > 0 && argv[0] == "ids") return Ids.Run();
        if (argv.Length > 0 && argv[0] == "light") return Light.Run();
        if (argv.Length > 0 && argv[0] == "big") return Big.Run();
        if (argv.Length > 0) return Wp4.Run(argv[0]);
        var wp3 = Lines($"{Repo}/benchmarks/scenarios/wp3-commitment-tiny/commitment-demo.input.ndjson");
        var wp3Args = new[] { "--mode", "commitment", "--policy-config", $"{Repo}/benchmarks/configurations/wp3-boundary-test-v1.json" };
        var cp3 = B("{\"schemaVersion\":\"1.0.0\",\"messageType\":\"checkpoint\",\"runId\":\"wp3-demo-run\",\"scenarioId\":\"wp3-commitment-tiny\",\"payload\":{}}");
        Compare("wp3 plain", wp3Args, wp3);

        var withCp = new List<byte[]>();
        for (int i = 0; i < wp3.Count; i++)
        {
            withCp.Add(wp3[i]);
            var t = Encoding.UTF8.GetString(wp3[i]);
            if (t.Contains("\"initializeRun\"") || t.Contains("\"decisionApplied\"")) withCp.Add(cp3);
        }
        Compare("wp3 checkpoints", wp3Args, withCp);
        var cpLines = Split(LastNew.Stdout).Where(l => l.Contains("\"messageType\":\"checkpoint\"")).ToList();
        Console.WriteLine($"  checkpoints: {cpLines.Count}, sizes {string.Join(",", cpLines.Select(c => c.Length))}");

        int[] applied = { 2, 4, 6, 8, 10 };
        for (int k = 0; k < cpLines.Count && k < applied.Length; k++)
        {
            using var doc = JsonDocument.Parse(cpLines[k]);
            var payload = doc.RootElement.GetProperty("payload").GetRawText();
            var restore = B("{\"schemaVersion\":\"1.0.0\",\"messageType\":\"restore\",\"runId\":\"wp3-demo-run\",\"scenarioId\":\"wp3-commitment-tiny\",\"payload\":" + payload + "}");
            var input = new List<byte[]> { wp3[0], wp3[1], restore };
            input.AddRange(wp3.Skip(applied[k]));
            var inp2 = new List<byte[]>();
            foreach (var l in input) { inp2.Add(l); if (Encoding.UTF8.GetString(l).Contains("\"decisionApplied\"")) inp2.Add(cp3); }
            Compare($"wp3 restore@{k}", wp3Args, inp2);
        }

        var wp2 = Lines($"{Repo}/benchmarks/scenarios/wp2-tiny/online-demo.input.ndjson");
        var cp2 = B("{\"schemaVersion\":\"1.0.0\",\"messageType\":\"checkpoint\",\"runId\":\"wp2-demo-run\",\"scenarioId\":\"wp2-tiny-demo\",\"payload\":{}}");
        var wp2cp = new List<byte[]>();
        foreach (var l in wp2) { wp2cp.Add(l); var t = Encoding.UTF8.GetString(l); if (t.Contains("\"initializeRun\"") || t.Contains("\"decisionApplied\"")) wp2cp.Add(cp2); }
        Compare("wp2 online checkpoints", new[] { "--mode", "online" }, wp2cp);
        Compare("wp2 conformance", new[] { "--mode", "conformance" }, wp2cp);
        var full = Lines($"{Repo}/benchmarks/schemas/fixtures/runner/full-tiny-transcript.input.ndjson");
        Compare("full-tiny default", Array.Empty<string>(), full);
        var paired = Lines($"{Repo}/benchmarks/fixtures/wp6/e2e/tiny/paired.input.ndjson");
        Compare("wp6 paired commitment", new[] { "--mode", "commitment", "--policy-config", $"{Repo}/benchmarks/configurations/wp6-public-mechanical-commitment-v1.json" }, paired);

        Compare("wp3 CRLF", wp3Args, withCp, "\r\n"u8.ToArray());
        Compare("wp3 CRCRLF", wp3Args, withCp, "\r\r\n"u8.ToArray());
        Compare("wp3 no final LF", wp3Args, withCp, null, finalTerm: false);
        var bom = new List<byte[]>(withCp); bom[0] = [0xEF, 0xBB, 0xBF, .. bom[0]];
        Compare("wp3 BOM first line", wp3Args, bom);
        var blanks = new List<byte[]>(); foreach (var l in withCp) { blanks.Add(l); blanks.Add([]); }
        Compare("wp3 blank lines", wp3Args, blanks);
        var bad = new List<byte[]>(withCp); bad.Insert(3, [(byte)'{', 0xC3, 0x28, (byte)'}']);
        Compare("wp3 invalid utf8 line", wp3Args, bad);
        var big = new List<byte[]>(withCp); big.Insert(3, Enumerable.Repeat((byte)' ', 1_048_577).ToArray());
        Compare("wp3 oversize line", wp3Args, big);
        var exact = new List<byte[]>(withCp); exact.Insert(3, [.. Enumerable.Repeat((byte)' ', 1_048_576 - 2), (byte)'{', (byte)'}']);
        Compare("wp3 exact-limit line", wp3Args, exact);
        var exactCr = new List<byte[]>(withCp); exactCr.Insert(3, [.. Enumerable.Repeat((byte)' ', 1_048_576 - 3), (byte)'{', (byte)'}', (byte)'\r']);
        Compare("wp3 exact-limit line incl CR", wp3Args, exactCr);
        var overCr = new List<byte[]>(withCp); overCr.Insert(3, [.. Enumerable.Repeat((byte)' ', 1_048_576 - 2), (byte)'{', (byte)'}', (byte)'\r']);
        Compare("wp3 limit+CR line", wp3Args, overCr);
        var nul = new List<byte[]>(withCp); nul.Insert(3, [0x00]);
        Compare("wp3 NUL line", wp3Args, nul);
        var midcr = new List<byte[]>(withCp); midcr.Insert(3, B("{\"a\":1}\r{\"b\":2}"));
        Compare("wp3 mid CR", wp3Args, midcr);
        var lgArg = new[] { "--mode", "commitment", "--policy-config", $"{Repo}/benchmarks/configurations/wp3-boundary-test-v1.json", "--maximum-line-bytes", "67108864" };
        Compare("wp3 64MiB limit", lgArg, withCp);
        var lgArg2 = new[] { "--mode", "commitment", "--policy-config", $"{Repo}/benchmarks/configurations/wp3-boundary-test-v1.json", "--maximum-line-bytes", "67108865" };
        Compare("wp3 64MiB+1 limit (expected diff)", lgArg2, withCp);

        if (cpLines.Count > 1)
        {
            var cpk = cpLines[1];
            using var d = JsonDocument.Parse(cpk);
            var content = d.RootElement.GetProperty("payload").GetProperty("content");
            var state = content.GetProperty("onlineState").GetRawText();
            var ledger = content.GetProperty("onlineState").GetProperty("commitmentLedger");
            Console.WriteLine($"  ledger histories at cp1: {ledger.GetArrayLength()}");
            if (ledger.GetArrayLength() > 0)
            {
                var reqId = ledger[0].GetProperty("requestId").GetString();
                var marker = ",\"simulationTimeMs\":";
                var at = state.LastIndexOf(marker, StringComparison.Ordinal);
                var newState = state[..at] + ",\"shadowAllocations\":[{\"class\":\"allowed\",\"requestId\":\"" + reqId + "\",\"score\":\"1\"}]" + state[at..];
                var nb = B(newState);
                var stateHash = Frame("RideBound.OnlineStateHash.v1\0", "canonicalOnlineState", nb);
                var c = $"{{\"appliedEpoch\":{content.GetProperty("appliedEpoch").GetInt64()},\"manifestHash\":\"{content.GetProperty("manifestHash").GetString()}\",\"nextEventSeq\":{content.GetProperty("nextEventSeq").GetInt64()},\"onlineState\":{newState},\"previousDecisionHash\":\"{content.GetProperty("previousDecisionHash").GetString()}\",\"simTimeMs\":{content.GetProperty("simTimeMs").GetInt64()},\"stateHash\":\"{stateHash}\"}}";
                var cph = Frame("RideBound.CheckpointHash.v1\0", "canonicalCheckpointContent", B(c));
                var restore = B("{\"schemaVersion\":\"1.0.0\",\"messageType\":\"restore\",\"runId\":\"wp3-demo-run\",\"scenarioId\":\"wp3-commitment-tiny\",\"payload\":{\"checkpointHash\":\"" + cph + "\",\"checkpointVersion\":\"1.0.0\",\"content\":" + c + "}}");
                var input = new List<byte[]> { wp3[0], wp3[1], restore, cp3 };
                input.AddRange(wp3.Skip(4));
                Compare("wp3 crafted restore with shadowAllocations (default config)", wp3Args, input);
                foreach (var l in Split(LastOld.Stdout).Skip(2).Take(3)) Console.WriteLine("   old> " + (l.Length > 260 ? l[..260] : l));
                foreach (var l in Split(LastNew.Stdout).Skip(2).Take(3)) Console.WriteLine("   new> " + (l.Length > 260 ? l[..260] : l));
            }
        }

        Console.WriteLine($"FAILS {Fails}");
        return 0;
    }

    static string Frame(string domain, string tag, byte[] body)
    {
        var ms = new MemoryStream();
        ms.Write(B(domain));
        var t = B(tag);
        ms.WriteByte((byte)(t.Length >> 8)); ms.WriteByte((byte)t.Length);
        ms.Write(t);
        var len = new byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(len, (ulong)body.Length);
        ms.Write(len);
        ms.Write(body);
        return Convert.ToHexStringLower(SHA256.HashData(ms.ToArray()));
    }
}
