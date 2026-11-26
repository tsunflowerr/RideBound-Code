using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using RideBound.Contracts.Protocol;
using RideBound.Runner.Configuration;
using RideBound.Runner.Protocol;
using RideBound.Tests;

namespace RideBound.Runner.Tests.Final;

/// <summary>
/// The final experiment's Runner configuration on a tiny closed-loop scenario (Fixtures/README.md): the four final
/// arms (G2 = insertion plus waiting-rider relocation) and the two insertion-only (INS) arms, each in the final
/// runs' light-v1 audit mode and in full-v1, with solver evidence off.
/// </summary>
internal sealed record FinalRun(string Name, string Arm, string Commitment, string Wp4, string Binding)
{
    /// <summary>The final runs' exact argument vector (relative paths; the working directory is Fixtures/).</summary>
    public IReadOnlyList<string> Arguments =>
    [
        "--mode", "commitment",
        "--policy-config", $"commitment/{Commitment}.json",
        "--wp4-config", $"wp4/{Wp4}.json",
        "--solver-seed-source", "manifest-master-seed",
        "--maximum-line-bytes", "1073741824",
    ];

    public override string ToString() => Name;
}

internal static partial class FinalRuns
{
    // Binding = policyConfigurationHash of the manifest: SHA-256 over the canonical commitment and WP4 files
    // (Wp4PolicyConfigurationBinding). Recorded here so that a change of either file, or of the binding rule,
    // shows up by name (see FinalConfigGoldenTests.The_run_table_binds_each_wp4_file_to_its_commitment_file).
    public static readonly IReadOnlyList<FinalRun> All =
    [
        new("FS-G2-SHRC71-CL.light-v1", "FS-G2-SHRC71-CL", "SHRC71", "G2-CL-light-v1", "cbfc7ec9477a1fe18b7e8aeeef2643311c437f63a3542141a99541d0af9caf49"),
        new("FS-G2-SHRC71-CL.full-v1", "FS-G2-SHRC71-CL", "SHRC71", "G2-CL-full-v1", "cdbb708ee543f2911a5caf22fd888349a67135257d4f945505bfad4869de29b9"),
        new("FS-G2-SHHZ71-N.light-v1", "FS-G2-SHHZ71-N", "SHHZ71", "G2-N-light-v1", "33b3c31313db349f081e946501cfc952858b50f36b4a8ed2413c0fbae971cf79"),
        new("FS-G2-SHHZ71-N.full-v1", "FS-G2-SHHZ71-N", "SHHZ71", "G2-N-full-v1", "5831c89aa3404e53c634d1afa2e61651e292ae5c07a3b78a32025218c503b909"),
        new("FS-G2-SHHZ71-CL.light-v1", "FS-G2-SHHZ71-CL", "SHHZ71", "G2-CL-light-v1", "853f81fc3a44547b2f5423c5bdf367f301a18c65a99bdcf708541d656644f259"),
        new("FS-G2-SHHZ71-CL.full-v1", "FS-G2-SHHZ71-CL", "SHHZ71", "G2-CL-full-v1", "ef9b2f87b8b031b2e465efa566f6961eeda0cc43ab8b0ec90e825eef99a11a75"),
        new("FS-G2-SHRC71-N.light-v1", "FS-G2-SHRC71-N", "SHRC71", "G2-N-light-v1", "477379d625d2c66cf227b2bea07c74df9f5e0c48abaeb6172839a3b741668673"),
        new("FS-G2-SHRC71-N.full-v1", "FS-G2-SHRC71-N", "SHRC71", "G2-N-full-v1", "1d98063cc6931652a689eb25fcd7768760eac1ae8c51cf44b8e3177b27205272"),
        new("INS-SHRC-CL.light-v1", "INS-SHRC-CL", "INS-SHRC", "INS-CL-light-v1", "b324ff89bb1a8fa5910a25cf7fb0f66c548ba332c5212fb047f4515f46ed5458"),
        new("INS-SHRC-CL.full-v1", "INS-SHRC-CL", "INS-SHRC", "INS-CL-full-v1", "5ab138c359c44a7bd277bb2adb953b5277cde26ae03e58f57dffffed1a12e372"),
        new("INS-SHHZ-N.light-v1", "INS-SHHZ-N", "INS-SHHZ", "INS-N-light-v1", "cb5a783aeb0997f85b43fcfaad53931f56fcc8072fcc4f575823432cb58533f6"),
        new("INS-SHHZ-N.full-v1", "INS-SHHZ-N", "INS-SHHZ", "INS-N-full-v1", "491056298140afad6bf8ecdaffbd00d4ff5661192f3840dfb18f648c02d12af6"),
    ];

    /// <summary>
    /// The INS-SHRC-CL.light-v1 configuration on a scenario where a rider boards far earlier than promised, so the
    /// session ends in an error at batch 11 (Fixtures/README.md). It is not in <see cref="All"/>: the theories over
    /// <see cref="All"/> expect a session that runs to the end.
    /// </summary>
    public static readonly FinalRun EarlyBoarding = All.Single(run => run.Name == "INS-SHRC-CL.light-v1") with
    {
        Name = "INS-SHRC-CL-early-boarding.light-v1",
        Arm = "INS-SHRC-CL-early-boarding",
    };

    private static readonly ConcurrentDictionary<string, Lazy<Task<RunnerProcessResult>>> Sessions =
        new(StringComparer.Ordinal);

    public static TheoryData<string> Names => new(All.Select(run => run.Name));

    public static FinalRun Get(string name) => All.Single(run => run.Name == name);

    public static string FixtureDirectory { get; } = Path.Combine(
        TestPaths.Root(), "tests", "RideBound.Runner.Tests", "Final", "Fixtures");

    /// <summary>
    /// Adapter-to-Runner lines of the run: hello, initializeRun (with the run's binding), a checkpoint request,
    /// then eventBatch + checkpoint request per epoch, then shutdown. decisionApplied is added by the driver.
    /// </summary>
    public static IReadOnlyList<string> ScenarioLines(FinalRun run) =>
        File.ReadAllLines(Path.Combine(FixtureDirectory, "scenarios", run.Arm + ".ndjson"))
            .Where(line => line.Length > 0)
            .Select(line => RunnerProcess.MessageType(line) == "initializeRun"
                ? BindingField().Replace(line, $"\"policyConfigurationHash\":\"{run.Binding}\"")
                : line)
            .ToArray();

    /// <summary>The whole session through a child Runner, once per test run (T1 and T3 share it).</summary>
    public static Task<RunnerProcessResult> SessionAsync(FinalRun run) =>
        Sessions.GetOrAdd(
            run.Name,
            _ => new Lazy<Task<RunnerProcessResult>>(
                () => RunnerProcess.RunAsync(run.Arguments, FixtureDirectory, ScenarioLines(run))))
            .Value;

    /// <summary>
    /// A Runner session for the run inside the test process, configured the way Program.cs configures the Runner
    /// for the run's argument vector. The WP4 file names its network folder relative to the Runner's working
    /// directory (Fixtures/), so here the same file is decoded with that folder as an absolute path, and the session
    /// is bound to the shipped files (the run table's binding). It therefore answers with the bytes of the
    /// command-line run.
    /// </summary>
    public static RunnerSession NewSession(FinalRun run)
    {
        var (commitment, wp4, binding) = InProcessConfiguration(run);
        return new RunnerSession(
            RunnerDefaults.CapabilityRequirements,
            commitmentPolicies: commitment,
            commitmentPolicyConfigurationHash: binding,
            wp4Configuration: wp4);
    }

    /// <summary>The Runner host on in-memory streams, configured like <see cref="NewSession"/> and with the run's
    /// line limit.</summary>
    public static Task<int> RunHostAsync(FinalRun run, Stream input, Stream output, TextWriter diagnostics)
    {
        var (commitment, wp4, binding) = InProcessConfiguration(run);
        return RunnerHost.RunAsync(
            input,
            output,
            diagnostics,
            maximumLineBytes: int.Parse(run.Arguments[^1], CultureInfo.InvariantCulture),
            commitmentPolicies: commitment,
            commitmentPolicyConfigurationHash: binding,
            wp4Configuration: wp4);
    }

    /// <summary>
    /// The final C1 configuration for tests that build their own small scenario: Fixtures/wp4/INS-N-full-v1.json
    /// (policy ridebound-hard-vector with selectionObjectiveOrder cost-first, booking-confirmation promises, full-v1
    /// audit, solver evidence off) without its shadowAllocation block. Shadow needs a commitment configuration with
    /// dropEtaShadowAllocation and the fixture network, which those tests do not use.
    /// </summary>
    public static JsonObject FinalC1Wp4WithoutShadow()
    {
        var wp4 = JsonNode.Parse(File.ReadAllText(Path.Combine(FixtureDirectory, "wp4", "INS-N-full-v1.json")))!
            .AsObject();
        Assert.True(wp4.Remove("shadowAllocation"));
        return wp4;
    }

    /// <summary>The lines of Fixtures/expected/&lt;run&gt;.txt after its header (format: <see cref="AssertRecordedStdout"/>).</summary>
    public static IReadOnlyList<string> RecordedLines(FinalRun run) =>
        File.ReadAllLines(Path.Combine(FixtureDirectory, "expected", run.Name + ".txt")).Skip(1).ToArray();

    /// <summary>
    /// Compares standard output with Fixtures/expected/&lt;run&gt;.txt. A line there that starts with "{" is the exact
    /// stdout line; any other line is "&lt;messageType&gt; &lt;UTF-8 byte length&gt; &lt;sha256&gt;" of it (checkpoints, and the
    /// decisions of most runs, to keep the fixtures small). The header holds length and SHA-256 of all of stdout.
    /// </summary>
    public static void AssertRecordedStdout(FinalRun run, byte[] standardOutput)
    {
        var expected = File.ReadAllLines(Path.Combine(FixtureDirectory, "expected", run.Name + ".txt"));
        var header = HeaderLine().Match(expected[0]);
        Assert.True(header.Success, $"Bad header in expected/{run.Name}.txt: {expected[0]}");
        var actual = new RunnerProcessResult(0, standardOutput, string.Empty).Lines();
        var wanted = expected.Skip(1).ToArray();

        for (var index = 0; index < Math.Max(wanted.Length, actual.Count); index++)
        {
            var expectedLine = index < wanted.Length ? wanted[index] : "<no line>";
            var actualLine = index >= actual.Count
                ? "<no line>"
                : expectedLine.StartsWith('{') ? actual[index] : Digest(actual[index]);

            if (!string.Equals(expectedLine, actualLine, StringComparison.Ordinal))
            {
                Assert.Fail(
                    $"{run.Name}: stdout line {index} differs from the ebca912 recording.\n"
                    + $"expected: {Shorten(expectedLine)}\nactual:   {Shorten(actualLine)}\n"
                    + $"Whole stdout written to {SaveActual(run.Name, standardOutput)}");
            }
        }

        Assert.Equal(long.Parse(header.Groups["bytes"].Value, System.Globalization.CultureInfo.InvariantCulture), standardOutput.LongLength);
        Assert.Equal(header.Groups["sha"].Value, Sha256(standardOutput));
    }

    public static string Digest(string line) =>
        $"{RunnerProcess.MessageType(line)} {Encoding.UTF8.GetByteCount(line)} {Sha256(Encoding.UTF8.GetBytes(line))}";

    public static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static (CommitmentPolicyConfiguration Commitment, Wp4RunnerConfiguration Wp4, Sha256Hex Binding)
        InProcessConfiguration(FinalRun run)
    {
        var commitment = CommitmentPolicyConfiguration.Decode(
            File.ReadAllBytes(Path.Combine(FixtureDirectory, "commitment", run.Commitment + ".json")));
        var wp4File = JsonNode.Parse(File.ReadAllText(Path.Combine(FixtureDirectory, "wp4", run.Wp4 + ".json")))!;
        wp4File["shadowAllocation"]!["networkDirectory"] = Path.Combine(FixtureDirectory, "network");
        var wp4 = Wp4RunnerConfiguration.Decode(Encoding.UTF8.GetBytes(wp4File.ToJsonString()), commitment);
        Assert.True(Sha256Hex.TryCreate(run.Binding, out var binding));
        return (commitment, wp4, binding!);
    }

    private static string Shorten(string line) => line.Length <= 600 ? line : line[..600] + "...";

    private static string SaveActual(string name, byte[] standardOutput)
    {
        var directory = Path.Combine(Path.GetTempPath(), "ridebound-final-actual");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name + ".stdout");
        File.WriteAllBytes(path, standardOutput);
        return path;
    }

    [GeneratedRegex("\"policyConfigurationHash\":\"[0-9a-f]{64}\"")]
    private static partial Regex BindingField();

    [GeneratedRegex(@"^# \S+ stdout: (?<bytes>[0-9]+) bytes, sha256 (?<sha>[0-9a-f]{64})$")]
    private static partial Regex HeaderLine();
}
