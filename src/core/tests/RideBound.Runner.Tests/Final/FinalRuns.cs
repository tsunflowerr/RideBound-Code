using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using RideBound.Contracts.Protocol;
using RideBound.Runner.Configuration;
using RideBound.Runner.Protocol;

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
        new("FS-G2-SHRC71-CL.light-v1", "FS-G2-SHRC71-CL", "SHRC71", "G2-CL-light-v1", "ab1e8c39c3b321fb8de078365eb55a83535d9e356ddcc1a587e0654a4b80381b"),
        new("FS-G2-SHRC71-CL.full-v1", "FS-G2-SHRC71-CL", "SHRC71", "G2-CL-full-v1", "381109d57da32a1e472bf973557ad1bc61f77d60071144fcd9bc87bb2fe56af6"),
        new("FS-G2-SHHZ71-N.light-v1", "FS-G2-SHHZ71-N", "SHHZ71", "G2-N-light-v1", "9d72e874694afbe8d0d856f3991c3c095c286be5fd2387e7cdd330f7edffc28f"),
        new("FS-G2-SHHZ71-N.full-v1", "FS-G2-SHHZ71-N", "SHHZ71", "G2-N-full-v1", "04b1d808886cec44fa5322b290c18b6f7e13bed04401da497a77dcbaa34e7e68"),
        new("FS-G2-SHHZ71-CL.light-v1", "FS-G2-SHHZ71-CL", "SHHZ71", "G2-CL-light-v1", "733ffc2358344341b0d9c9054408eb5c7f4abf7d09a720372345e56a22c1c76c"),
        new("FS-G2-SHHZ71-CL.full-v1", "FS-G2-SHHZ71-CL", "SHHZ71", "G2-CL-full-v1", "1ceaca803bbd1133483df9f40da4de13c8f250e5630aa4529b84cf69cd4b1731"),
        new("FS-G2-SHRC71-N.light-v1", "FS-G2-SHRC71-N", "SHRC71", "G2-N-light-v1", "b7e5f804a8fc5880b6c4915c64aa57581c16120c87c8fc7c209207b1979af5dc"),
        new("FS-G2-SHRC71-N.full-v1", "FS-G2-SHRC71-N", "SHRC71", "G2-N-full-v1", "e7f8c80af3b27aa5c1a562fecdff5988524f6d78636a76fc2ae09732c1f4afa6"),
        new("INS-SHRC-CL.light-v1", "INS-SHRC-CL", "INS-SHRC", "INS-CL-light-v1", "1567534168b3afd6ead777c508f866e51b5c2e56fe829ee20ede1d10d9fabc95"),
        new("INS-SHRC-CL.full-v1", "INS-SHRC-CL", "INS-SHRC", "INS-CL-full-v1", "edf66c008ebd55ccd132ac2e651f5986d915ddca0169f80d128e2831f03c4e39"),
        new("INS-SHHZ-N.light-v1", "INS-SHHZ-N", "INS-SHHZ", "INS-N-light-v1", "f2d160a16485fe205df847c64aad0b41ea13f3a103aa3aa2e734369a2786b8c0"),
        new("INS-SHHZ-N.full-v1", "INS-SHHZ-N", "INS-SHHZ", "INS-N-full-v1", "108d93f17e114f8f09d85ba2cece128418327aceadc692687bcdea4627230c0b"),
    ];

    private static readonly ConcurrentDictionary<string, Lazy<Task<RunnerProcessResult>>> Sessions =
        new(StringComparer.Ordinal);

    public static TheoryData<string> Names => new(All.Select(run => run.Name));

    public static FinalRun Get(string name) => All.Single(run => run.Name == name);

    public static string FixtureDirectory { get; } = Path.Combine(
        RepositoryRoot(), "tests", "RideBound.Runner.Tests", "Final", "Fixtures");

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
            stopDistances: commitment,
            commitmentPolicyConfigurationHash: binding,
            wp4Configuration: wp4,
            useManifestSolverSeed: true);
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
            stopDistances: commitment,
            commitmentPolicyConfigurationHash: binding,
            wp4Configuration: wp4,
            useManifestSolverSeed: true);
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

    private static string RepositoryRoot()
    {
        var configured = typeof(FinalRuns).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(attribute => attribute.Key == "RideBoundRepositoryRoot")
            ?.Value;

        if (configured is not null && File.Exists(Path.Combine(configured, "RideBound.slnx")))
        {
            return configured;
        }

        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "RideBound.slnx")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find the folder that holds RideBound.slnx.");
    }

    [GeneratedRegex("\"policyConfigurationHash\":\"[0-9a-f]{64}\"")]
    private static partial Regex BindingField();

    [GeneratedRegex(@"^# \S+ stdout: (?<bytes>[0-9]+) bytes, sha256 (?<sha>[0-9a-f]{64})$")]
    private static partial Regex HeaderLine();
}
