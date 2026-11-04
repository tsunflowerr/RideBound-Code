using System.Text;
using static H;

static class Big
{
    static readonly string Repo = Environment.GetEnvironmentVariable("RIDEBOUND_REPO")!;

    public static int Run()
    {
        var wp3 = Lines($"{Repo}/benchmarks/scenarios/wp3-commitment-tiny/commitment-demo.input.ndjson");
        var cp3 = Encoding.UTF8.GetBytes("{\"schemaVersion\":\"1.0.0\",\"messageType\":\"checkpoint\",\"runId\":\"wp3-demo-run\",\"scenarioId\":\"wp3-commitment-tiny\",\"payload\":{}}");
        var args = new[] { "--mode", "commitment", "--policy-config", $"{Repo}/benchmarks/configurations/wp3-boundary-test-v1.json", "--maximum-line-bytes", "4194304" };
        var r = new Random(5);
        foreach (var pad in new[] { 65_535 - 2, 65_536 - 2, 65_536, 131_071, 3_000_001, 4_194_304 - 200, 4_194_304 })
        {
            var input = new List<byte[]>();
            for (int i = 0; i < wp3.Count; i++)
            {
                var l = wp3[i];
                // pad every eventBatch line with leading JSON whitespace so its length straddles chunk boundaries
                if (Encoding.UTF8.GetString(l).Contains("\"eventBatch\""))
                {
                    var p = Math.Max(0, pad - l.Length + r.Next(0, 3));
                    l = [.. Enumerable.Repeat((byte)' ', p), .. l];
                }
                input.Add(l);
                if (Encoding.UTF8.GetString(wp3[i]).Contains("\"decisionApplied\"")) input.Add(cp3);
            }
            Compare($"big pad {pad} LF", args, input);
            Compare($"big pad {pad} CRLF", args, input, "\r\n"u8.ToArray());
        }
        Console.WriteLine($"FAILS {Fails}");
        return 0;
    }
}
