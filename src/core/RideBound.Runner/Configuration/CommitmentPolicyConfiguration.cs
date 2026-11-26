using System.Security.Cryptography;
using System.Text.Json;
using RideBound.Application.Commitments;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Validation;
using static RideBound.Runner.Configuration.JsonConfig;

namespace RideBound.Runner.Configuration;

public sealed class CommitmentPolicyConfiguration :
    ICommitmentPolicyProvider,
    IStopDistanceLookup
{
    private static readonly IReadOnlySet<string> RootFields = Fields(
        "configurationVersion",
        "policies",
        "stopDistances");
    private static readonly IReadOnlySet<string> PolicyFields = Fields(
        "policyId",
        "budgetBasis",
        "limits",
        "materialRevisionRule",
        // Optional keys.
        "finalConfirmationLocks",
        "ratchetLocks",
        "dropEtaDeadlineRelaxed",
        "dropEtaBudgetSlackPermille",
        "dropEtaDeadlineSlackPermille",
        "waitingRiderRelocation",
        "dropEtaShadowAllocation");
    private static readonly IReadOnlySet<string> LimitFields = Fields(
        "dimension",
        "hardLimit",
        "applicablePhases");
    private static readonly IReadOnlySet<string> RevisionFields = Fields(
        "rawEtaThresholdMs",
        "displayBucketWidthMs");
    private static readonly IReadOnlySet<string> DistanceFields = Fields(
        "fromNodeId",
        "toNodeId",
        "distanceMm");
    private readonly CommitmentPolicyCatalog _policies;
    private readonly IReadOnlyList<string> _policyIds;
    private readonly IReadOnlyDictionary<StopArc, long> _stopDistances;

    private CommitmentPolicyConfiguration(
        Sha256Hex contentHash,
        IEnumerable<CommitmentPolicy> policies,
        IReadOnlyDictionary<StopArc, long> stopDistances)
    {
        ContentHash = contentHash;
        var materializedPolicies = policies.ToArray();
        _policies = new CommitmentPolicyCatalog(materializedPolicies);
        _policyIds = Array.AsReadOnly(
            materializedPolicies
                .Select(value => value.PolicyId)
                .Order(StringComparer.Ordinal)
                .ToArray());
        _stopDistances = stopDistances;
    }

    public Sha256Hex ContentHash { get; }

    public IReadOnlyList<string> PolicyIds => _policyIds;

    public static CommitmentPolicyConfiguration Decode(
        ReadOnlySpan<byte> utf8Json)
    {
        var canonical = CanonicalJson.Canonicalize(utf8Json);
        _ = Sha256Hex.TryCreate(
            Convert.ToHexStringLower(SHA256.HashData(canonical)),
            out var contentHash);

        using var document = JsonDocument.Parse(canonical);
        var root = document.RootElement;
        RequireObject(root, RootFields, RootFields, "$");

        if (Text(root, "configurationVersion") != "1.0.0")
        {
            throw new InvalidDataException(
                "Commitment configurationVersion must be '1.0.0'.");
        }

        var policies = root.GetProperty("policies")
            .EnumerateArray()
            .Select(ReadPolicy)
            .ToArray();

        if (policies.Length == 0)
        {
            throw new InvalidDataException(
                "Commitment configuration requires at least one policy.");
        }

        var distances = new Dictionary<StopArc, long>();

        foreach (var element in root.GetProperty("stopDistances").EnumerateArray())
        {
            RequireObject(element, DistanceFields, DistanceFields, "$.stopDistances[]");
            var arc = new StopArc(
                new NodeId(Text(element, "fromNodeId")),
                new NodeId(Text(element, "toNodeId")));
            var distance = NonNegativeInteger(
                element.GetProperty("distanceMm"),
                "distanceMm");

            if (arc.From == arc.To)
            {
                throw new InvalidDataException(
                    "Same-node stop distance is canonically zero and must be omitted.");
            }

            if (!distances.TryAdd(arc, distance))
            {
                throw new InvalidDataException(
                    $"Duplicate directed stop-distance arc '{arc.From}' -> '{arc.To}'.");
            }
        }

        return new CommitmentPolicyConfiguration(
            contentHash!,
            policies,
            distances);
    }

    public bool TryGetPolicy(string policyId, out CommitmentPolicy policy) =>
        _policies.TryGetPolicy(policyId, out policy);

    public bool TryGetDistanceMillimeters(
        NodeId fromNodeId,
        NodeId toNodeId,
        out long distanceMillimeters)
    {
        if (fromNodeId == toNodeId)
        {
            distanceMillimeters = 0;
            return true;
        }

        return _stopDistances.TryGetValue(
            new StopArc(fromNodeId, toNodeId),
            out distanceMillimeters);
    }

    private static CommitmentPolicy ReadPolicy(JsonElement element)
    {
        RequireObject(
            element,
            PolicyFields,
            Fields("policyId", "budgetBasis", "limits", "materialRevisionRule"),
            "$.policies[]");
        var limits = element.GetProperty("limits")
            .EnumerateArray()
            .Select(ReadLimit)
            .ToArray();
        var revision = element.GetProperty("materialRevisionRule");
        RequireObject(
            revision,
            RevisionFields,
            Fields(),
            "$.policies[].materialRevisionRule");
        var rawThreshold = OptionalPositiveInteger(
            revision,
            "rawEtaThresholdMs");
        var displayBucket = OptionalPositiveInteger(
            revision,
            "displayBucketWidthMs");
        var deadlineRelaxed = OptionalBoolean(element, "dropEtaDeadlineRelaxed");
        var budgetPermille = OptionalPermille(element, "dropEtaBudgetSlackPermille");
        var deadlinePermille = OptionalPermille(element, "dropEtaDeadlineSlackPermille");
        var relocation = OptionalBoolean(element, "waitingRiderRelocation");

        // Shadow Control. Optional; omitted means false: the policy has no Shadow classes.
        var shadowAllocation = OptionalBoolean(element, "dropEtaShadowAllocation");

        if (shadowAllocation && !CommitmentPolicy.ShadowScalesOneLimit(budgetPermille, deadlinePermille))
        {
            throw new InvalidDataException(
                "'dropEtaShadowAllocation' requires exactly one of 'dropEtaBudgetSlackPermille' or "
                + "'dropEtaDeadlineSlackPermille', of 1000.");
        }

        var hasDeadline = deadlinePermille is not null;

        // A deadline is always relaxed: the key must be true exactly when there is a deadline.
        if (deadlineRelaxed != hasDeadline
            || (!hasDeadline && element.TryGetProperty("dropEtaDeadlineRelaxed", out _)))
        {
            throw new InvalidDataException(
                "'dropEtaDeadlineRelaxed' must be true exactly when 'dropEtaDeadlineSlackPermille' is set.");
        }

        if (budgetPermille is not null
            && limits.FirstOrDefault(value => value.Dimension == CommitmentDimension.DropEtaTotalMs)
                ?.HardLimit is not null)
        {
            throw new InvalidDataException(
                "'dropEtaBudgetSlackPermille' replaces the drop_eta_total_ms hardLimit; remove one.");
        }

        if (relocation
            && (ReadLocks(element, "finalConfirmationLocks") & PromiseLock.Vehicle) != 0)
        {
            throw new InvalidDataException(
                "'waitingRiderRelocation' requires 'finalConfirmationLocks' without 'vehicle'.");
        }

        if (relocation
            && limits.FirstOrDefault(value => value.Dimension == CommitmentDimension.VehicleSwitchCount)
                is { HardLimit: 0 } switchLimit
            && (switchLimit.ApplicablePhases & CommitmentPhase.WaitingPickup) != 0)
        {
            throw new InvalidDataException(
                "'waitingRiderRelocation' cannot run under a vehicle_switch_count hardLimit of 0 for waiting riders.");
        }

        var policyId = Text(element, "policyId");

        // Every revision is charged by its decision-induced delta.
        RequireValue(element, "budgetBasis", "decisionInduced", "$.policies[]");

        return new CommitmentPolicy(
            policyId,
            limits,
            new MaterialRevisionRule(rawThreshold, displayBucket),
            ReadLocks(element, "finalConfirmationLocks"),
            ReadLocks(element, "ratchetLocks"),
            budgetPermille,
            deadlinePermille,
            relocation,
            shadowAllocation);
    }

    private static int? OptionalPermille(JsonElement element, string propertyName)
    {
        if (OptionalPositiveInteger(element, propertyName) is not long value)
        {
            return null;
        }

        return value <= 1000
            ? (int)value
            : throw new InvalidDataException($"'{propertyName}' must be 1 to 1000.");
    }

    private static CommitmentDimensionLimit ReadLimit(JsonElement element)
    {
        RequireObject(
            element,
            LimitFields,
            Fields("dimension", "applicablePhases"),
            "$.policies[].limits[]");
        var dimensionText = Text(element, "dimension");
        var dimension = CommitmentDimensionVocabulary.Ordered
            .Cast<CommitmentDimension?>()
            .SingleOrDefault(
                value => CommitmentDimensionVocabulary.ToProtocolValue(
                        value!.Value)
                    == dimensionText)
            ?? throw new InvalidDataException(
                $"Unknown commitment dimension '{dimensionText}'.");
        var phases = CommitmentPhase.None;

        foreach (var value in element.GetProperty("applicablePhases")
                     .EnumerateArray())
        {
            var parsed = value.GetString() switch
            {
                "accepted" => CommitmentPhase.Accepted,
                "waitingPickup" => CommitmentPhase.WaitingPickup,
                "onboard" => CommitmentPhase.Onboard,
                _ => throw new InvalidDataException(
                    "Unknown commitment applicable phase."),
            };

            if ((phases & parsed) != 0)
            {
                throw new InvalidDataException(
                    $"Duplicate commitment phase '{value.GetString()}'.");
            }

            phases |= parsed;
        }

        return new CommitmentDimensionLimit(
            dimension,
            element.TryGetProperty("hardLimit", out var hardLimit)
                ? NonNegativeInteger(hardLimit, "hardLimit")
                : null,
            phases);
    }

    private static PromiseLock ReadLocks(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var values))
        {
            return PromiseLock.None;
        }

        var locks = PromiseLock.None;

        foreach (var value in values.EnumerateArray())
        {
            var parsed = value.GetString() switch
            {
                "vehicle" => PromiseLock.Vehicle,
                "pickupStop" => PromiseLock.PickupStop,
                "dropStop" => PromiseLock.DropStop,
                "pickupEta" => PromiseLock.PickupEta,
                "dropEta" => PromiseLock.DropEta,
                _ => throw new InvalidDataException("Unknown promise lock."),
            };

            if ((locks & parsed) != 0)
            {
                throw new InvalidDataException(
                    $"Duplicate promise lock '{value.GetString()}'.");
            }

            locks |= parsed;
        }

        return locks;
    }

    // The commitment integer readers keep their own error text ("canonical non-negative integer"),
    // which differs from the WP4 parser's.
    private static long NonNegativeInteger(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetInt64(out var value)
            || value is < 0 or > DomainLimits.MaxCanonicalInteger)
        {
            throw new InvalidDataException(
                $"'{propertyName}' must be a canonical non-negative integer.");
        }

        return value;
    }

    private static long PositiveInteger(JsonElement element, string propertyName)
    {
        var value = NonNegativeInteger(element, propertyName);
        return value > 0
            ? value
            : throw new InvalidDataException($"'{propertyName}' must be positive.");
    }

    private static long? OptionalPositiveInteger(
        JsonElement element,
        string propertyName) =>
        element.TryGetProperty(propertyName, out var value)
            ? PositiveInteger(value, propertyName)
            : null;

    private readonly record struct StopArc(NodeId From, NodeId To);
}
