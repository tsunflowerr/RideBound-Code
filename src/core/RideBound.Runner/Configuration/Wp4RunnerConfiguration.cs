using System.Security.Cryptography;
using System.Text.Json;
using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Policies;
using RideBound.Algorithms.Shareability;
using RideBound.Application.Commitments;
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
    public const string RetainedPortfolioEvidenceProfile =
        "retained-portfolio-v1";

    /// <summary>
    /// RB-WP14-003. Everything `retained-portfolio-v1` records, plus the complete
    /// commitment witness set. It is a separate profile on purpose: the frozen E1
    /// configurations declare `retained-portfolio-v1`, and widening that profile
    /// in place would change their recorded evidence and therefore their decision
    /// hashes, breaking the E1 freeze chain.
    /// </summary>
    public const string RetainedPortfolioFullWitnessEvidenceProfile =
        "retained-portfolio-full-witness-v1";

    private static readonly IReadOnlySet<string> RootFields = Fields(
        "configurationVersion",
        "policyId",
        "policyVersion",
        "initialPromiseTrigger",
        "candidateGeneration",
        "solverExecution",
        "emitSolverExecutionEvidence",
        "solverExecutionEvidenceProfile",
        "commitmentRecovery",
        "lateBoarding",
        "selectionObjectiveOrder",
        "auditHash",
        "planningForecast",
        "shadowAllocation");

    /// <summary>The pointer that every refused removed feature carries.</summary>
    private const string RemovedFeature =
        "removed; see commit ebca912 (branch research/final-real-runs-2026-11-06)";

    /// <summary>The removed WP4 policyIds, each with the name its refusal gives.</summary>
    private static readonly IReadOnlyDictionary<string, string> RemovedPolicies =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["rolling-cost"] = "the rolling-cost policy",
            ["rolling-penalty"] = "the rolling-penalty policy",
        };

    /// <summary>
    /// ADR-089 GĐ4 (exploratory). Optional; omitted means every projected leg is the snapshot
    /// time (as before, same content hash). `{ "profile": [f0, f1, …], "binMs": 900000 }`
    /// turns on the closed-loop planning forecast (<see cref="TravelTimeForecast"/>): the
    /// profile is the forecast traffic factor of each bin from the run start, held after the
    /// last value. Only the dispatcher's projections change; the simulator moves vehicles as
    /// before.
    /// </summary>
    private static readonly IReadOnlySet<string> PlanningForecastFields = Fields(
        "profile",
        "binMs");

    /// <summary>
    /// ADR-075 (exploratory). Optional; omitted means fail-closed, so every
    /// configuration written before it keeps its content hash and its behaviour.
    /// </summary>
    public const string ForcedReferenceRecovery = "forced-reference-v1";
    public const string FailClosedRecovery = "fail-closed";

    /// <summary>
    /// Exploratory extension of `forced-reference-v1`: a forced vehicle may also choose a changed
    /// route that makes none of the kept route's gate overruns worse (no worse than reference).
    /// </summary>
    public const string NoWorseRecovery = "no-worse-than-reference-v1";

    /// <summary>
    /// ADR-076 (exploratory). Optional; omitted means a boarding after the latest pickup
    /// fails the batch, as before. `record-v1` accepts it as a fact and records it. The
    /// FleetPy adapter reads the same key and then leaves service windows to the Runner.
    /// </summary>
    public const string RecordLateBoarding = "record-v1";
    public const string RejectLateBoarding = "reject";

    /// <summary>
    /// ADR-089. `auditHash` omitted or `full-v1` hashes the whole canonical online state for every
    /// decision, as before. `light-v1` binds each decision to the event batch and the state
    /// identity only (epoch, next event sequence, simulation time), with its own decision-hash
    /// domain; the checkpoint hash still covers the whole state. No decision changes.
    /// </summary>
    public const string FullAuditHash = "full-v1";
    public const string LightAuditHash = "light-v1";

    /// <summary>
    /// ADR-078. Required, and the only accepted value: the C1 ranking is cost-first. The stability-first
    /// ranking, which "stability-first" or an absent key selected, is refused.
    /// </summary>
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
        "waitingRiderRelocation",
        "stopServiceDurationMs");
    private static readonly IReadOnlySet<string> RelocationGenerationFields = Fields(
        "maximumRidersPerVehicle",
        "maximumTargetVehicles");
    private static readonly IReadOnlySet<string> SolverRequiredFields = Fields(
        "adapterVersion",
        "maximumGenerationWorkUnits",
        "maximumValidationWorkUnits",
        "maximumSolverWorkUnits",
        "maximumSolverDeterministicTimeMicros",
        "randomSeed");

    /// <summary>
    /// `skipConstantObjectiveLevels` is allowed but not required, so every
    /// configuration written before RB-WP14-002 keeps parsing unchanged and keeps
    /// the solver work it recorded.
    /// </summary>
    private static readonly IReadOnlySet<string> SolverFields = Fields(
        "adapterVersion",
        "maximumGenerationWorkUnits",
        "maximumValidationWorkUnits",
        "maximumSolverWorkUnits",
        "maximumSolverDeterministicTimeMicros",
        "randomSeed",
        "skipConstantObjectiveLevels");

    private Wp4RunnerConfiguration(
        Sha256Hex contentHash,
        string policyId,
        string policyVersion,
        InitialPromiseTrigger initialPromiseTrigger,
        CandidateGenerationOptions candidateGeneration,
        SolverBackedRidePoolingPolicyOptions? solverPolicyOptions,
        bool emitSolverExecutionEvidence,
        string? solverExecutionEvidenceProfile,
        bool recordsLateBoarding,
        bool usesLightAuditHash,
        TravelTimeForecast? planningForecast,
        ShadowAllocationInputs? shadowAllocation)
    {
        PlanningForecast = planningForecast;
        ShadowAllocation = shadowAllocation;
        ContentHash = contentHash;
        PolicyId = policyId;
        PolicyVersion = policyVersion;
        InitialPromiseTrigger = initialPromiseTrigger;
        CandidateGeneration = candidateGeneration;
        SolverPolicyOptions = solverPolicyOptions;
        EmitSolverExecutionEvidence = emitSolverExecutionEvidence;
        SolverExecutionEvidenceProfile = solverExecutionEvidenceProfile;
        RecordsLateBoarding = recordsLateBoarding;
        UsesLightAuditHash = usesLightAuditHash;
    }

    public Sha256Hex ContentHash { get; }

    public string PolicyId { get; }

    public string PolicyVersion { get; }

    public InitialPromiseTrigger InitialPromiseTrigger { get; }

    public CandidateGenerationOptions CandidateGeneration { get; }

    public SolverBackedRidePoolingPolicyOptions? SolverPolicyOptions { get; }

    public bool EmitSolverExecutionEvidence { get; }

    public string? SolverExecutionEvidenceProfile { get; }

    /// <summary>True only for `lateBoarding: record-v1` (ADR-076).</summary>
    public bool RecordsLateBoarding { get; }

    /// <summary>True only for `auditHash: light-v1` (ADR-089).</summary>
    public bool UsesLightAuditHash { get; }

    /// <summary>The planning forecast of `planningForecast`; null when the key is omitted.</summary>
    public TravelTimeForecast? PlanningForecast { get; }

    /// <summary>
    /// ADR-089 GĐ4: Shadow Control inputs of `shadowAllocation`; null when the key is omitted. A
    /// commitment policy with `dropEtaShadowAllocation` needs them, and they need such a policy.
    /// </summary>
    public ShadowAllocationInputs? ShadowAllocation { get; }

    /// <summary>
    /// RB-WP14-003. The retained-portfolio evidence profile is the only caller
    /// that needs a complete prune attribution, so the extra validator work is
    /// tied to it. With the profile off the validator keeps its fail-fast path
    /// and its cost unchanged.
    /// </summary>
    public bool CollectAllCommitmentWitnesses =>
        StringComparer.Ordinal.Equals(
            SolverExecutionEvidenceProfile,
            RetainedPortfolioFullWitnessEvidenceProfile);

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
        var source = SolverPolicyOptions
            ?? throw new InvalidOperationException(
                "The selected WP4 policy does not use the solver-backed path.");
        var sourceExecution = source.ExecutionBudget;
        var sourceSolver = sourceExecution.SolverBudget;
        var solver = DeterministicSolverBudget.Create(
            sourceSolver.MaximumWorkUnits,
            sourceSolver.MaximumDeterministicTimeMicros,
            manifestSolverSeed,
            sourceSolver.SkipConstantObjectiveLevels);

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
            execution.Value!,
            source.CaptureCandidatePortfolioEvidence,
            source.ForcedReferenceRecovery,
            source.ForcedNoWorseRecovery);
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
        RejectRemovedFeatures(root);
        RequireObject(
            root,
            RootFields,
            Fields(
                "configurationVersion",
                "policyId",
                "policyVersion",
                "candidateGeneration"),
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

        var initialPromiseTrigger = root.TryGetProperty(
            "initialPromiseTrigger",
            out _)
                ? Text(root, "initialPromiseTrigger") switch
                {
                    "initial-acceptance" =>
                        InitialPromiseTrigger.InitialAcceptance,
                    "booking-confirmation" =>
                        InitialPromiseTrigger.BookingConfirmation,
                    _ => throw new InvalidDataException(
                        "Unknown initialPromiseTrigger."),
                }
                : InitialPromiseTrigger.InitialAcceptance;

        var generation = ReadGeneration(root.GetProperty("candidateGeneration"));
        var hasSolver = root.TryGetProperty("solverExecution", out var solverElement);
        var emitSolverExecutionEvidence = root.TryGetProperty(
            "emitSolverExecutionEvidence",
            out var evidenceElement)
                && Boolean(evidenceElement, "emitSolverExecutionEvidence");
        var solverExecutionEvidenceProfile = root.TryGetProperty(
            "solverExecutionEvidenceProfile",
            out _)
                ? Text(root, "solverExecutionEvidenceProfile")
                : null;

        if (solverExecutionEvidenceProfile is not null
            && !StringComparer.Ordinal.Equals(
                solverExecutionEvidenceProfile,
                RetainedPortfolioEvidenceProfile)
            && !StringComparer.Ordinal.Equals(
                solverExecutionEvidenceProfile,
                RetainedPortfolioFullWitnessEvidenceProfile))
        {
            throw new InvalidDataException(
                "Unknown solverExecutionEvidenceProfile.");
        }

        if (solverExecutionEvidenceProfile is not null
            && (!emitSolverExecutionEvidence || !hasSolver))
        {
            throw new InvalidDataException(
                "A retained-portfolio evidence profile requires solver-backed "
                + "execution and emitSolverExecutionEvidence=true.");
        }

        RequireVariantFields(hasSolver);

        var (forcedReferenceRecovery, forcedNoWorseRecovery) = root.TryGetProperty(
            "commitmentRecovery",
            out _)
                ? Text(root, "commitmentRecovery") switch
                {
                    FailClosedRecovery => (false, false),
                    ForcedReferenceRecovery => (true, false),
                    NoWorseRecovery => (true, true),
                    _ => throw new InvalidDataException(
                        "Unknown commitmentRecovery."),
                }
                : (false, false);

        if (forcedReferenceRecovery
            && (!hasSolver
                || policyKind is not RidePoolingPolicyKind.RideBoundHardVector))
        {
            throw new InvalidDataException(
                $"commitmentRecovery '{Text(root, "commitmentRecovery")}' requires the "
                + "solver-backed C1 or C2 policy.");
        }

        // A late boarding is an observation, not a policy decision, so every policy may
        // record it; the arms of one comparison should share the same setting.
        var recordsLateBoarding = root.TryGetProperty("lateBoarding", out _)
            ? Text(root, "lateBoarding") switch
            {
                RejectLateBoarding => false,
                RecordLateBoarding => true,
                _ => throw new InvalidDataException("Unknown lateBoarding."),
            }
            : false;

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

        // The C1 ranking is cost-first only. Any other value, or no key (which selected
        // stability-first), is refused instead of silently ranking cost-first.
        if (!root.TryGetProperty("selectionObjectiveOrder", out var orderElement)
            || orderElement.ValueKind != JsonValueKind.String
            || orderElement.GetString() != CostFirstSelectionOrder)
        {
            throw new InvalidDataException(
                $"WP4 selectionObjectiveOrder must be '{CostFirstSelectionOrder}'. The stability-first ranking, "
                + $"which 'stability-first' or an absent key selected: {RemovedFeature}");
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

        // ADR-089 GĐ4: Shadow classes are assigned for every rider of a run, so every policy is
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

        // Shadow scores a route of rider stops; origin-hold adds waypoints it does not score.
        if (shadowAllocation is not null
            && generation.ScheduleStrategy != CandidateScheduleStrategy.EarliestFeasible)
        {
            throw new InvalidDataException(
                "'shadowAllocation' requires scheduleStrategy 'earliest-feasible'.");
        }

        SolverBackedRidePoolingPolicyOptions? solverOptions = null;

        if (hasSolver)
        {
            var budget = ReadSolverBudget(solverElement);
            solverOptions = new SolverBackedRidePoolingPolicyOptions(
                policyKind,
                budget,
                solverExecutionEvidenceProfile is not null,
                forcedReferenceRecovery,
                forcedNoWorseRecovery);
        }

        return new Wp4RunnerConfiguration(
            contentHash!,
            policyId,
            policyVersion,
            initialPromiseTrigger,
            generation,
            solverOptions,
            emitSolverExecutionEvidence,
            solverExecutionEvidenceProfile,
            recordsLateBoarding,
            usesLightAuditHash,
            planningForecast,
            shadowAllocation);
    }

    private static CandidateGenerationOptions ReadGeneration(JsonElement element)
    {
        RequireObject(
            element,
            GenerationFields,
            GenerationRequiredFields,
            "$.candidateGeneration");
        var strategy = Text(element, "scheduleStrategy") switch
        {
            "earliest-feasible" => CandidateScheduleStrategy.EarliestFeasible,
            "origin-hold-relocated-wait" =>
                CandidateScheduleStrategy.OriginHoldRelocatedWait,
            _ => throw new InvalidDataException(
                "Unknown candidate scheduleStrategy."),
        };
        var retentionStrategy = element.TryGetProperty(
            "retentionStrategy",
            out _)
                ? Text(element, "retentionStrategy") switch
                {
                    "legacy-accepted-count-cost-slack" =>
                        CandidateRetentionStrategy.LegacyAcceptedCountCostSlack,
                    "service-set-stability-portfolio-v1" =>
                        CandidateRetentionStrategy.ServiceSetStabilityPortfolioV1,
                    _ => throw new InvalidDataException(
                        "Unknown candidate retentionStrategy."),
                }
                : CandidateRetentionStrategy.LegacyAcceptedCountCostSlack;

        // G2 (waiting-rider relocation). Absent means off, so every earlier configuration keeps
        // its canonical bytes and content hash. Each cap is optional: absent means unbounded.
        WaitingRiderRelocationGenerationOptions? relocation = null;

        if (element.TryGetProperty("waitingRiderRelocation", out var relocationElement))
        {
            RequireObject(
                relocationElement,
                RelocationGenerationFields,
                Fields(),
                "$.candidateGeneration.waitingRiderRelocation");

            if (strategy != CandidateScheduleStrategy.EarliestFeasible)
            {
                throw new InvalidDataException(
                    "'waitingRiderRelocation' requires scheduleStrategy 'earliest-feasible'.");
            }

            relocation = new WaitingRiderRelocationGenerationOptions(
                relocationElement.TryGetProperty("maximumRidersPerVehicle", out var riders)
                    ? PositiveInt32(riders, "maximumRidersPerVehicle")
                    : null,
                relocationElement.TryGetProperty("maximumTargetVehicles", out var targets)
                    ? PositiveInt32(targets, "maximumTargetVehicles")
                    : null);
        }

        // Dwell of inserted stops (bridge step 1.3). Absent means 0, so every earlier configuration
        // keeps its canonical bytes, content hash and candidates.
        var stopServiceDurationMs = 0;

        if (element.TryGetProperty("stopServiceDurationMs", out var dwellElement))
        {
            stopServiceDurationMs = NonNegativeInt32(dwellElement, "stopServiceDurationMs");

            if (stopServiceDurationMs > MaximumStopServiceDurationMs)
            {
                throw new InvalidDataException(
                    "'candidateGeneration.stopServiceDurationMs' must be at most 3600000 (one hour).");
            }
        }

        return new CandidateGenerationOptions(
            PositiveInt32(
                element.GetProperty("maximumCandidatesPerVehicle"),
                "maximumCandidatesPerVehicle"),
            NonNegativeInt32(
                element.GetProperty("maximumNewRequestsPerVehicle"),
                "maximumNewRequestsPerVehicle"),
            Boolean(
                element.GetProperty("exactSmallMode"),
                "exactSmallMode"),
            strategy,
            PositiveInteger(
                element.GetProperty("maximumExplorationWorkUnits"),
                "maximumExplorationWorkUnits"),
            retentionStrategy: retentionStrategy,
            waitingRiderRelocation: relocation,
            stopServiceDurationMs: stopServiceDurationMs);
    }

    private const int MaximumStopServiceDurationMs = 3_600_000;

    private static DeterministicCandidateSelectionExecutionBudget
        ReadSolverBudget(JsonElement element)
    {
        RequireObject(
            element,
            SolverFields,
            SolverRequiredFields,
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
                "randomSeed"),
            OptionalBoolean(element, "skipConstantObjectiveLevels"));

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

    /// <summary>
    /// Runs before the field checks, so a file that still selects a removed feature is always refused with the
    /// pointer to the commit that has it: the multiple-plan policy (policyId 'least-commitment-consensus', B5)
    /// and its 'multiplePlan' block, the policies in <see cref="RemovedPolicies"/>: rolling-cost (B1) and
    /// rolling-penalty (B2), the fixed-freeze-horizon policy (B3) and its 'fixedFreeze' block, the
    /// no-reassignment-repair policy (B4) and its 'repair' block, and the soft-hard hybrid policy (policyId
    /// 'commit-soft-hard-hybrid', C2) and its 'warningProfiles' block. The checks run in the order the features were
    /// removed, so a file that an earlier check refused keeps its message.
    /// </summary>
    private static void RejectRemovedFeatures(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object
            && (root.TryGetProperty("multiplePlan", out _)
                || root.TryGetProperty("policyId", out var policyId)
                    && policyId.ValueKind == JsonValueKind.String
                    && policyId.GetString() == "least-commitment-consensus"))
        {
            throw new InvalidDataException(
                "WP4 policyId 'least-commitment-consensus' and the 'multiplePlan' block "
                + $"(the multiple-plan policy): {RemovedFeature}");
        }

        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("policyId", out var removedPolicyId)
            && removedPolicyId.ValueKind == JsonValueKind.String
            && RemovedPolicies.TryGetValue(removedPolicyId.GetString()!, out var removedPolicy))
        {
            throw new InvalidDataException(
                $"WP4 policyId '{removedPolicyId.GetString()}' ({removedPolicy}): {RemovedFeature}");
        }

        if (root.ValueKind == JsonValueKind.Object
            && (root.TryGetProperty("fixedFreeze", out _)
                || root.TryGetProperty("policyId", out var freezePolicyId)
                    && freezePolicyId.ValueKind == JsonValueKind.String
                    && freezePolicyId.GetString() == "fixed-freeze-horizon"))
        {
            throw new InvalidDataException(
                "WP4 policyId 'fixed-freeze-horizon' and the 'fixedFreeze' block "
                + $"(the fixed-freeze-horizon policy): {RemovedFeature}");
        }

        if (root.ValueKind == JsonValueKind.Object
            && (root.TryGetProperty("repair", out _)
                || root.TryGetProperty("policyId", out var repairPolicyId)
                    && repairPolicyId.ValueKind == JsonValueKind.String
                    && repairPolicyId.GetString() == "no-reassignment-repair"))
        {
            throw new InvalidDataException(
                "WP4 policyId 'no-reassignment-repair' and the 'repair' block "
                + $"(the no-reassignment-repair policy): {RemovedFeature}");
        }

        if (root.ValueKind == JsonValueKind.Object
            && (root.TryGetProperty("warningProfiles", out _)
                || root.TryGetProperty("policyId", out var hybridPolicyId)
                    && hybridPolicyId.ValueKind == JsonValueKind.String
                    && hybridPolicyId.GetString() == "commit-soft-hard-hybrid"))
        {
            throw new InvalidDataException(
                "WP4 policyId 'commit-soft-hard-hybrid' and the 'warningProfiles' block "
                + $"(the soft-hard hybrid policy): {RemovedFeature}");
        }
    }

    private static void RequireVariantFields(bool hasSolver)
    {
        if (!hasSolver)
        {
            throw new InvalidDataException(
                "WP4 policy variant fields do not match the selected policyId.");
        }
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
