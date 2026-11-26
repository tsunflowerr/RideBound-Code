using System.Security.Cryptography;
using System.Text.Json;
using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Policies;
using RideBound.Algorithms.Shareability;
using RideBound.Application.Optimization;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Domain.Common;
using RideBound.Domain.Validation;
using RideBound.Solvers.OrTools;
using static RideBound.Runner.Configuration.JsonConfig;

namespace RideBound.Runner.Configuration;

public sealed class Wp4RunnerConfiguration
{
    private static readonly IReadOnlySet<string> RootFields = Fields(
        "configurationVersion",
        "policyId",
        "policyVersion",
        "initialPromiseTrigger",
        "candidateGeneration",
        "solverExecution",
        "emitSolverExecutionEvidence",
        "commitmentRecovery",
        "lateBoarding",
        "selectionObjectiveOrder",
        "auditHash",
        "planningForecast",
        "shadowAllocation");

    /// <summary>
    /// Optional; omitted means every projected leg is the snapshot
    /// time. `{ "profile": [f0, f1, …], "binMs": 900000 }`
    /// turns on the closed-loop planning forecast (<see cref="TravelTimeForecast"/>): the
    /// profile is the forecast traffic factor of each bin from the run start, held after the
    /// last value. Only the dispatcher's projections change; the simulator moves vehicles as
    /// it does without a forecast.
    /// </summary>
    private static readonly IReadOnlySet<string> PlanningForecastFields = Fields(
        "profile",
        "binMs");

    /// <summary>
    /// `auditHash` omitted or `full-v1` hashes the whole canonical online state for every
    /// decision. `light-v1` binds each decision to the event batch and the state
    /// identity only (epoch, next event sequence, simulation time), with its own decision-hash
    /// domain; the checkpoint hash still covers the whole state. No decision changes.
    /// </summary>
    public const string FullAuditHash = "full-v1";
    public const string LightAuditHash = "light-v1";

    /// <summary>
    /// Keys that the final files set to the one value the Runner supports. Each names a behaviour that is fixed in the
    /// code: a promise opens at the booking confirmation; a vehicle whose safety no-op a commitment gate rejects fails
    /// closed; a boarding after the latest pickup is recorded (the FleetPy adapter reads the same key and then leaves
    /// service windows to the Runner); the C1 ranking is cost-first.
    /// </summary>
    private static readonly (string Name, string Value)[] FixedRootValues =
    [
        ("initialPromiseTrigger", "booking-confirmation"),
        ("commitmentRecovery", "no-worse-than-reference-v1"),
        ("lateBoarding", "record-v1"),
        ("selectionObjectiveOrder", CostFirstSelectionOrder),
    ];

    public const string CostFirstSelectionOrder = "cost-first";
    private static readonly IReadOnlySet<string> GenerationRequiredFields = Fields(
        "maximumCandidatesPerVehicle",
        "maximumNewRequestsPerVehicle",
        "exactSmallMode",
        "scheduleStrategy",
        "maximumExplorationWorkUnits");
    private static readonly IReadOnlySet<string> GenerationFields = Fields(
        "maximumCandidatesPerVehicle",
        "maximumNewRequestsPerVehicle",
        "exactSmallMode",
        "scheduleStrategy",
        "maximumExplorationWorkUnits",
        "retentionStrategy",
        "waitingRiderRelocation");
    private static readonly IReadOnlySet<string> RelocationGenerationFields = Fields(
        "maximumTargetVehicles");
    private static readonly IReadOnlySet<string> SolverFields = Fields(
        "adapterVersion",
        "maximumGenerationWorkUnits",
        "maximumValidationWorkUnits",
        "maximumSolverWorkUnits",
        "maximumSolverDeterministicTimeMicros",
        "randomSeed");

    private Wp4RunnerConfiguration(
        Sha256Hex contentHash,
        string policyId,
        string policyVersion,
        CandidateGenerationOptions candidateGeneration,
        SolverBackedRidePoolingPolicyOptions solverPolicyOptions,
        bool usesLightAuditHash,
        TravelTimeForecast? planningForecast,
        ShadowAllocationInputs? shadowAllocation)
    {
        PlanningForecast = planningForecast;
        ShadowAllocation = shadowAllocation;
        ContentHash = contentHash;
        PolicyId = policyId;
        PolicyVersion = policyVersion;
        CandidateGeneration = candidateGeneration;
        SolverPolicyOptions = solverPolicyOptions;
        UsesLightAuditHash = usesLightAuditHash;
    }

    public Sha256Hex ContentHash { get; }

    public string PolicyId { get; }

    public string PolicyVersion { get; }

    public CandidateGenerationOptions CandidateGeneration { get; }

    public SolverBackedRidePoolingPolicyOptions SolverPolicyOptions { get; }

    /// <summary>True only for `auditHash: light-v1`.</summary>
    public bool UsesLightAuditHash { get; }

    /// <summary>The planning forecast of `planningForecast`; null when the key is omitted.</summary>
    public TravelTimeForecast? PlanningForecast { get; }

    /// <summary>
    /// Shadow Control inputs of `shadowAllocation`; null when the key is omitted. A
    /// commitment policy with `dropEtaShadowAllocation` needs them, and they need such a policy.
    /// </summary>
    public ShadowAllocationInputs? ShadowAllocation { get; }

    public Sha256Hex BindToCommitmentConfiguration(
        Sha256Hex commitmentConfigurationHash)
    {
        ArgumentNullException.ThrowIfNull(commitmentConfigurationHash);
        return Wp4PolicyConfigurationBinding.Calculate(
            commitmentConfigurationHash,
            ContentHash);
    }

    public SolverBackedRidePoolingPolicyOptions CreateSolverPolicyOptionsForRun(
        long manifestSolverSeed)
    {
        var source = SolverPolicyOptions;
        var sourceExecution = source.ExecutionBudget;
        var sourceSolver = sourceExecution.SolverBudget;
        var solver = DeterministicSolverBudget.Create(
            sourceSolver.MaximumWorkUnits,
            sourceSolver.MaximumDeterministicTimeMicros,
            manifestSolverSeed);

        if (!solver.IsSuccess)
        {
            throw new InvalidDataException(
                "Manifest solver seed is outside the deterministic solver range.");
        }

        var execution = DeterministicCandidateSelectionExecutionBudget.Create(
            sourceExecution.MaximumGenerationWorkUnits,
            sourceExecution.MaximumValidationWorkUnits,
            solver.Value!);

        if (!execution.IsSuccess)
        {
            throw new InvalidDataException(execution.Failure!.Message);
        }

        return new SolverBackedRidePoolingPolicyOptions(
            source.PolicyKind,
            execution.Value!);
    }

    public static Wp4RunnerConfiguration Decode(
        ReadOnlySpan<byte> utf8Json,
        CommitmentPolicyConfiguration commitmentConfiguration)
    {
        ArgumentNullException.ThrowIfNull(commitmentConfiguration);
        var canonical = CanonicalJson.Canonicalize(utf8Json);
        _ = Sha256Hex.TryCreate(
            Convert.ToHexStringLower(SHA256.HashData(canonical)),
            out var contentHash);
        using var document = JsonDocument.Parse(canonical);
        var root = document.RootElement;
        RequireObject(
            root,
            RootFields,
            Fields(
                "configurationVersion",
                "policyId",
                "policyVersion",
                "candidateGeneration",
                "solverExecution"),
            "$");

        if (Text(root, "configurationVersion") != "1.0.0")
        {
            throw new InvalidDataException(
                "WP4 runner configurationVersion must be '1.0.0'.");
        }

        var policyId = Text(root, "policyId");

        if (!RidePoolingPolicyRegistry.TryParse(policyId, out var policyKind))
        {
            throw new InvalidDataException($"Unknown WP4 policyId '{policyId}'.");
        }

        var policyVersion = Text(root, "policyVersion");

        if (!OpaqueIdentifier.IsValid(policyVersion))
        {
            throw new InvalidDataException(
                "WP4 policyVersion must be an opaque protocol identifier.");
        }

        foreach (var (name, value) in FixedRootValues)
        {
            RequireValue(root, name, value, "$");
        }

        // Optional; false is the only supported value (the final files set it).
        if (OptionalBoolean(root, "emitSolverExecutionEvidence"))
        {
            throw new InvalidDataException("'$.emitSolverExecutionEvidence' must be false.");
        }

        var generation = ReadGeneration(root.GetProperty("candidateGeneration"));

        var usesLightAuditHash = root.TryGetProperty("auditHash", out _)
            ? Text(root, "auditHash") switch
            {
                FullAuditHash => false,
                LightAuditHash => true,
                _ => throw new InvalidDataException("Unknown auditHash."),
            }
            : false;

        TravelTimeForecast? planningForecast = null;

        if (root.TryGetProperty("planningForecast", out var forecastElement))
        {
            RequireObject(
                forecastElement,
                PlanningForecastFields,
                PlanningForecastFields,
                "$.planningForecast");
            var profileElement = forecastElement.GetProperty("profile");

            if (profileElement.ValueKind != JsonValueKind.Array
                || profileElement.GetArrayLength() == 0)
            {
                throw new InvalidDataException(
                    "planningForecast.profile must be a non-empty array.");
            }

            // Canonical JSON holds integers only, so each factor is the shortest decimal string
            // of the double (as JavaScript writes it); parsing it returns that exact double.
            var profile = profileElement.EnumerateArray()
                .Select(value =>
                    value.ValueKind == JsonValueKind.String
                    && double.TryParse(
                        value.GetString(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var factor)
                    && double.IsFinite(factor)
                    && factor > 0
                        ? factor
                        : throw new InvalidDataException(
                            "planningForecast.profile holds positive decimal strings."))
                .ToArray();
            var bin = PositiveInteger(forecastElement.GetProperty("binMs"), "planningForecast.binMs");

            if (bin < TravelTimeForecast.MinimumBinMilliseconds)
            {
                throw new InvalidDataException(
                    $"'planningForecast.binMs' must be at least {TravelTimeForecast.MinimumBinMilliseconds}.");
            }

            planningForecast = new TravelTimeForecast(profile, bin);
        }

        ShadowAllocationInputs? shadowAllocation = null;

        if (root.TryGetProperty("shadowAllocation", out var shadowElement))
        {
            RequireObject(
                shadowElement,
                ShadowAllocationConfiguration.Fields,
                ShadowAllocationConfiguration.Fields,
                "$.shadowAllocation");
            shadowAllocation = ShadowAllocationConfiguration.Read(shadowElement);
        }

        // G2 needs both halves: candidates that move a waiting rider, and a policy that lets the
        // rider change vehicle. Either alone would be silently inert, so both are required
        // together.
        var relocatingPolicies = commitmentConfiguration.PolicyIds
            .Count(id => commitmentConfiguration.TryGetPolicy(id, out var policy)
                && policy.WaitingRiderRelocation);

        if (generation.WaitingRiderRelocation is not null
            && relocatingPolicies != commitmentConfiguration.PolicyIds.Count)
        {
            throw new InvalidDataException(
                "'candidateGeneration.waitingRiderRelocation' requires 'waitingRiderRelocation' "
                + "on every commitment policy.");
        }

        if (generation.WaitingRiderRelocation is null && relocatingPolicies > 0)
        {
            throw new InvalidDataException(
                "A commitment policy with 'waitingRiderRelocation' requires "
                + "'candidateGeneration.waitingRiderRelocation'.");
        }

        // Shadow classes are assigned for every rider of a run, so every policy is
        // shadow-allocated when the inputs are given, and the inputs are given when one is.
        var shadowPolicies = commitmentConfiguration.PolicyIds
            .Count(id => commitmentConfiguration.TryGetPolicy(id, out var policy)
                && policy.DropEtaShadowAllocation);

        if (root.TryGetProperty("shadowAllocation", out _)
            ? shadowPolicies != commitmentConfiguration.PolicyIds.Count
            : shadowPolicies > 0)
        {
            throw new InvalidDataException(
                "'shadowAllocation' and 'dropEtaShadowAllocation' on every commitment policy go together.");
        }

        var solverOptions = new SolverBackedRidePoolingPolicyOptions(
            policyKind,
            ReadSolverBudget(root.GetProperty("solverExecution")));

        return new Wp4RunnerConfiguration(
            contentHash!,
            policyId,
            policyVersion,
            generation,
            solverOptions,
            usesLightAuditHash,
            planningForecast,
            shadowAllocation);
    }

    private static CandidateGenerationOptions ReadGeneration(JsonElement element)
    {
        const string path = "$.candidateGeneration";
        RequireObject(
            element,
            GenerationFields,
            GenerationRequiredFields,
            path);

        // The schedule is always earliest-feasible and candidates are always kept by the service-set stability
        // portfolio; the final files name both.
        RequireValue(element, "scheduleStrategy", "earliest-feasible", path);
        RequireValue(element, "retentionStrategy", "service-set-stability-portfolio-v1", path);

        // G2 (waiting-rider relocation). Absent means off. The target cap is optional: absent means every other
        // vehicle.
        WaitingRiderRelocationGenerationOptions? relocation = null;

        if (element.TryGetProperty("waitingRiderRelocation", out var relocationElement))
        {
            RequireObject(
                relocationElement,
                RelocationGenerationFields,
                Fields(),
                "$.candidateGeneration.waitingRiderRelocation");
            relocation = new WaitingRiderRelocationGenerationOptions(
                relocationElement.TryGetProperty("maximumTargetVehicles", out var targets)
                    ? PositiveInt32(targets, "maximumTargetVehicles")
                    : null);
        }

        var maximumCandidatesPerVehicle = PositiveInt32(
            element.GetProperty("maximumCandidatesPerVehicle"),
            "maximumCandidatesPerVehicle");
        var maximumNewRequestsPerVehicle = NonNegativeInt32(
            element.GetProperty("maximumNewRequestsPerVehicle"),
            "maximumNewRequestsPerVehicle");

        // Required, and false is the only supported value (the final files set it).
        if (Boolean(element.GetProperty("exactSmallMode"), "exactSmallMode"))
        {
            throw new InvalidDataException("'$.candidateGeneration.exactSmallMode' must be false.");
        }

        return new CandidateGenerationOptions(
            maximumCandidatesPerVehicle,
            maximumNewRequestsPerVehicle,
            PositiveInteger(
                element.GetProperty("maximumExplorationWorkUnits"),
                "maximumExplorationWorkUnits"),
            waitingRiderRelocation: relocation);
    }

    private static DeterministicCandidateSelectionExecutionBudget
        ReadSolverBudget(JsonElement element)
    {
        RequireObject(
            element,
            SolverFields,
            SolverFields,
            "$.solverExecution");

        if (Text(element, "adapterVersion")
            != OrToolsCandidateSelectionSolver.AdapterVersion)
        {
            throw new InvalidDataException(
                $"solverExecution.adapterVersion must be '{OrToolsCandidateSelectionSolver.AdapterVersion}'.");
        }

        var solverBudget = DeterministicSolverBudget.Create(
            PositiveInteger(
                element.GetProperty("maximumSolverWorkUnits"),
                "maximumSolverWorkUnits"),
            PositiveInteger(
                element.GetProperty("maximumSolverDeterministicTimeMicros"),
                "maximumSolverDeterministicTimeMicros"),
            NonNegativeInt32(
                element.GetProperty("randomSeed"),
                "randomSeed"));

        if (!solverBudget.IsSuccess)
        {
            throw new InvalidDataException(solverBudget.Failure!.Message);
        }

        var execution = DeterministicCandidateSelectionExecutionBudget.Create(
            PositiveInteger(
                element.GetProperty("maximumGenerationWorkUnits"),
                "maximumGenerationWorkUnits"),
            PositiveInteger(
                element.GetProperty("maximumValidationWorkUnits"),
                "maximumValidationWorkUnits"),
            solverBudget.Value!);

        if (!execution.IsSuccess)
        {
            throw new InvalidDataException(execution.Failure!.Message);
        }

        return execution.Value!;
    }

    private static long PositiveInteger(JsonElement element, string name)
    {
        var value = NonNegativeInteger(element, name);
        return value > 0
            ? value
            : throw new InvalidDataException($"'{name}' must be positive.");
    }

    private static long NonNegativeInteger(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Number
            || !element.TryGetInt64(out var value)
            || value is < 0 or > DomainLimits.MaxCanonicalInteger)
        {
            throw new InvalidDataException(
                $"'{name}' must be a non-negative canonical integer.");
        }

        return value;
    }

    private static int PositiveInt32(JsonElement element, string name)
    {
        var value = NonNegativeInt32(element, name);
        return value > 0
            ? value
            : throw new InvalidDataException($"'{name}' must be positive.");
    }

    private static int NonNegativeInt32(JsonElement element, string name)
    {
        var value = NonNegativeInteger(element, name);
        return value <= int.MaxValue
            ? (int)value
            : throw new InvalidDataException($"'{name}' exceeds Int32.");
    }
}
