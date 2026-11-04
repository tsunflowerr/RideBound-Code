using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

static class H
{
    // OLD_RUNNER / NEW_RUNNER: folders of two `dotnet publish` outputs of RideBound.Runner.
    public static string Old => Path.Combine(Environment.GetEnvironmentVariable("OLD_RUNNER")!, "RideBound.Runner.dll");
    public static string New => Path.Combine(Environment.GetEnvironmentVariable("NEW_RUNNER")!, "RideBound.Runner.dll");

    public sealed class Run
    {
        public byte[] Stdout = [];
        public string Stderr = "";
        public int Exit;
        public List<byte[]> Lines = new();
    }

    // Interactive driver: lines are raw byte lines (without terminator). A line equal to
    // "@APPLY" becomes a decisionApplied for the last decision. "@RAW:" prefix: bytes sent as-is.
    public static Run Drive(string dll, string[] args, List<byte[]> input, byte[] terminator, bool finalTerminator = true)
    {
        var psi = new ProcessStartInfo("dotnet") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        psi.ArgumentList.Add(dll);
        foreach (var kv in Env) psi.Environment[kv.Key] = kv.Value;
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var lines = new BlockingCollection<byte[]>();
        var all = new MemoryStream();
        var reader = Task.Run(() =>
        {
            var s = p.StandardOutput.BaseStream;
            var buf = new byte[65536];
            var cur = new MemoryStream();
            int n;
            while ((n = s.Read(buf, 0, buf.Length)) > 0)
            {
                all.Write(buf, 0, n);
                for (int i = 0; i < n; i++)
                {
                    if (buf[i] == (byte)'\n') { lines.Add(cur.ToArray()); cur = new MemoryStream(); }
                    else cur.WriteByte(buf[i]);
                }
            }
            if (cur.Length > 0) lines.Add(cur.ToArray());
            lines.CompleteAdding();
        });
        var errTask = p.StandardError.ReadToEndAsync();
        var stdin = p.StandardInput.BaseStream;
        string lastDecision = null; string lastRun = null, lastScenario = null; long lastEpoch = 0, lastSim = 0;
        var got = new List<byte[]>();
        try
        {
            for (int idx = 0; idx < input.Count; idx++)
            {
                var line = input[idx];
                var text = Encoding.UTF8.GetString(line);
                if (text == "@APPLY")
                {
                    if (lastDecision == null) { text = "{\"schemaVersion\":\"1.0.0\",\"messageType\":\"decisionApplied\",\"payload\":{}}"; }
                    else text = $"{{\"schemaVersion\":\"1.0.0\",\"messageType\":\"decisionApplied\",\"runId\":{JsonSerializer.Serialize(lastRun)},\"scenarioId\":{JsonSerializer.Serialize(lastScenario)},\"epochId\":{lastEpoch},\"simTimeMs\":{lastSim},\"payload\":{{\"decisionHash\":\"{lastDecision}\"}}}}";
                    line = Encoding.UTF8.GetBytes(text);
                }
                stdin.Write(line);
                if (idx < input.Count - 1 || finalTerminator) stdin.Write(terminator);
                stdin.Flush();
                if (text.Contains("\"messageType\":\"eventBatch\"") && !text.StartsWith("@"))
                {
                    long batchEpoch = -1;
                    try { using var bd = JsonDocument.Parse(line); batchEpoch = bd.RootElement.GetProperty("epochId").GetInt64(); } catch { }
                    while (lines.TryTake(out var resp, 60000))
                    {
                        got.Add(resp);
                        bool done = false;
                        try
                        {
                            using var d = JsonDocument.Parse(resp);
                            var r = d.RootElement;
                            var mt = r.GetProperty("messageType").GetString();
                            if (mt == "decision")
                            {
                                lastDecision = r.GetProperty("payload").GetProperty("decisionHash").GetString();
                                lastRun = r.GetProperty("runId").GetString(); lastScenario = r.GetProperty("scenarioId").GetString();
                                lastEpoch = r.GetProperty("epochId").GetInt64(); lastSim = r.GetProperty("simTimeMs").GetInt64();
                                done = true;
                            }
                            else if (mt == "error" && (!r.TryGetProperty("epochId", out var ee) || ee.GetInt64() == batchEpoch))
                            {
                                done = true;
                            }
                        }
                        catch { }
                        if (done) break;
                    }
                }
            }
            stdin.Close();
        }
        catch (IOException) { }
        reader.Wait(120000);
        p.WaitForExit(120000);
        var run = new Run { Stdout = all.ToArray(), Stderr = errTask.Result, Exit = p.ExitCode };
        return run;
    }

    public static List<byte[]> Lines(string path) => File.ReadAllLines(path).Select(l => Encoding.UTF8.GetBytes(l)).ToList();

    public static int Fails = 0;
    public static Dictionary<string, string> Env = new();
    public static void Compare(string name, string[] args, List<byte[]> input, byte[] term = null, bool finalTerm = true)
    {
        term ??= "\n"u8.ToArray();
        var a = Drive(Old, args, input, term, finalTerm);
        var b = Drive(New, args, input, term, finalTerm);
        bool same = a.Stdout.AsSpan().SequenceEqual(b.Stdout) && a.Stderr == b.Stderr && a.Exit == b.Exit;
        Console.WriteLine($"{(same ? "SAME" : "DIFF")} {name}: exit {a.Exit}/{b.Exit} stdout {a.Stdout.Length}/{b.Stdout.Length} sha {Convert.ToHexString(SHA256.HashData(a.Stdout))[..12]}/{Convert.ToHexString(SHA256.HashData(b.Stdout))[..12]} lines {Count(a.Stdout)}");
        if (!same)
        {
            Fails++;
            var la = Split(a.Stdout); var lb = Split(b.Stdout);
            for (int i = 0; i < Math.Max(la.Count, lb.Count); i++)
            {
                var x = i < la.Count ? la[i] : "<none>"; var y = i < lb.Count ? lb[i] : "<none>";
                if (x != y) { Console.WriteLine($"  line {i}:\n   old: {Trunc(x)}\n   new: {Trunc(y)}"); break; }
            }
            if (a.Stderr != b.Stderr) Console.WriteLine($"  stderr old: {Trunc(a.Stderr)}\n  stderr new: {Trunc(b.Stderr)}");
        }
        LastOld = a; LastNew = b;
    }
    public static Run LastOld, LastNew;
    static int Count(byte[] b) => b.Count(x => x == (byte)'\n');
    public static List<string> Split(byte[] b) => Encoding.UTF8.GetString(b).Split('\n').ToList();
    static string Trunc(string s) => s.Length > 400 ? s[..400] + "..." : s;
}
