using System.Buffers;
using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using RideBound.Domain.Common;
using RideBound.Domain.Routes;

namespace RideBound.Application.State;

public sealed record CanonicalVehiclePlan
{
    public CanonicalVehiclePlan(VehicleId vehicleId, RoutePlan route)
    {
        ArgumentNullException.ThrowIfNull(route);
        VehicleId = vehicleId;
        Route = route;
    }

    public VehicleId VehicleId { get; }

    public RoutePlan Route { get; }
}

/// <summary>
/// An executable fleet-plan snapshot. Its identity binds route order, progress,
/// versions and stop semantics; solver/candidate IDs and transient scores are
/// deliberately excluded.
/// </summary>
public sealed class CanonicalFleetPlan
{
    private CanonicalFleetPlan(
        string planId,
        long sourceEpoch,
        IReadOnlyList<CanonicalVehiclePlan> vehiclePlans)
    {
        PlanId = planId;
        SourceEpoch = sourceEpoch;
        VehiclePlans = vehiclePlans;
    }

    public string PlanId { get; }

    public long SourceEpoch { get; }

    public IReadOnlyList<CanonicalVehiclePlan> VehiclePlans { get; }

    public static CanonicalFleetPlan Create(
        long sourceEpoch,
        IEnumerable<CanonicalVehiclePlan> vehiclePlans)
    {
        var ordered = ValidateAndOrder(sourceEpoch, vehiclePlans);
        return new CanonicalFleetPlan(
            CalculatePlanId(sourceEpoch, ordered),
            sourceEpoch,
            ordered);
    }

    public static CanonicalFleetPlan Rehydrate(
        string planId,
        long sourceEpoch,
        IEnumerable<CanonicalVehiclePlan> vehiclePlans)
    {
        if (!IsLowerSha256(planId))
        {
            throw new ArgumentException(
                "Plan ID must be lowercase SHA-256.",
                nameof(planId));
        }

        var ordered = ValidateAndOrder(sourceEpoch, vehiclePlans);
        var expected = CalculatePlanId(sourceEpoch, ordered);

        if (!StringComparer.Ordinal.Equals(planId, expected))
        {
            throw new ArgumentException(
                "Plan ID does not match its exact fleet-route semantics.",
                nameof(planId));
        }

        return new CanonicalFleetPlan(planId, sourceEpoch, ordered);
    }

    private static IReadOnlyList<CanonicalVehiclePlan> ValidateAndOrder(
        long sourceEpoch,
        IEnumerable<CanonicalVehiclePlan> vehiclePlans)
    {
        ArgumentNullException.ThrowIfNull(vehiclePlans);

        if (sourceEpoch is < 0 or > DomainLimits.MaxCanonicalInteger)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceEpoch));
        }

        var ordered = vehiclePlans
            .OrderBy(value => value.VehicleId.Value, StringComparer.Ordinal)
            .ToArray();

        if (ordered.Length == 0
            || ordered.Select(value => value.VehicleId).Distinct().Count()
                != ordered.Length)
        {
            throw new ArgumentException(
                "A fleet plan requires one unique route per vehicle.",
                nameof(vehiclePlans));
        }

        return new ReadOnlyCollection<CanonicalVehiclePlan>(ordered);
    }

    private static string CalculatePlanId(
        long sourceEpoch,
        IEnumerable<CanonicalVehiclePlan> vehiclePlans)
    {
        var buffer = new ArrayBufferWriter<byte>();
        buffer.Write("RideBound.CanonicalFleetPlan.v1\0"u8);
        WriteInteger(buffer, sourceEpoch);

        foreach (var vehicle in vehiclePlans)
        {
            WriteText(buffer, vehicle.VehicleId.Value);
            WriteInteger(buffer, vehicle.Route.Version.Value);
            WriteInteger(buffer, vehicle.Route.ExecutedStopCount);
            WriteInteger(buffer, vehicle.Route.FrozenPrefix.Count);

            foreach (var stop in vehicle.Route.FrozenPrefix)
            {
                WriteStop(buffer, stop);
            }

            WriteInteger(buffer, vehicle.Route.MutableSuffix.Count);

            foreach (var stop in vehicle.Route.MutableSuffix)
            {
                WriteStop(buffer, stop);
            }
        }

        return Convert.ToHexStringLower(SHA256.HashData(buffer.WrittenSpan));
    }

    private static void WriteStop(ArrayBufferWriter<byte> buffer, RouteStop stop)
    {
        WriteText(buffer, stop.StopId.Value);
        WriteText(buffer, stop.NodeId.Value);
        WriteInteger(buffer, (long)stop.Kind);
        WriteText(buffer, stop.RequestId?.Value ?? string.Empty);
        WriteInteger(buffer, stop.ServiceDuration.Milliseconds);
    }

    private static void WriteText(ArrayBufferWriter<byte> buffer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var length = buffer.GetSpan(sizeof(uint));
        BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)bytes.Length));
        buffer.Advance(sizeof(uint));
        buffer.Write(bytes);
    }

    private static void WriteInteger(ArrayBufferWriter<byte> buffer, long value) =>
        WriteText(buffer, value.ToString(CultureInfo.InvariantCulture));

    private static bool IsLowerSha256(string value) =>
        value is { Length: 64 }
        && value.All(
            character => character is >= '0' and <= '9'
                or >= 'a' and <= 'f');
}

/// <summary>
/// Canonical, checkpointable plan pool. Version zero is the sole empty value;
/// every non-empty replacement advances the previous version exactly once.
/// </summary>
public sealed class VersionedPlanPool
{
    private VersionedPlanPool(
        long version,
        long sourceEpoch,
        string? distinguishedPlanId,
        IReadOnlyList<CanonicalFleetPlan> plans)
    {
        Version = version;
        SourceEpoch = sourceEpoch;
        DistinguishedPlanId = distinguishedPlanId;
        Plans = plans;
    }

    public static VersionedPlanPool Empty { get; } =
        new(0, 0, null, Array.Empty<CanonicalFleetPlan>());

    public long Version { get; }

    public long SourceEpoch { get; }

    public string? DistinguishedPlanId { get; }

    public IReadOnlyList<CanonicalFleetPlan> Plans { get; }

    public CanonicalFleetPlan? DistinguishedPlan => DistinguishedPlanId is null
        ? null
        : Plans.Single(value => StringComparer.Ordinal.Equals(
            value.PlanId,
            DistinguishedPlanId));

    public static VersionedPlanPool CreateNext(
        VersionedPlanPool previous,
        long sourceEpoch,
        string distinguishedPlanId,
        IEnumerable<CanonicalFleetPlan> plans)
    {
        ArgumentNullException.ThrowIfNull(previous);

        if (previous.Version == DomainLimits.MaxCanonicalInteger)
        {
            throw new OverflowException("Plan-pool version cannot advance.");
        }

        return Create(
            previous.Version + 1,
            sourceEpoch,
            distinguishedPlanId,
            plans,
            requireNonEmpty: true);
    }

    public static VersionedPlanPool Rehydrate(
        long version,
        long sourceEpoch,
        string? distinguishedPlanId,
        IEnumerable<CanonicalFleetPlan> plans) =>
        Create(
            version,
            sourceEpoch,
            distinguishedPlanId,
            plans,
            requireNonEmpty: version != 0);

    private static VersionedPlanPool Create(
        long version,
        long sourceEpoch,
        string? distinguishedPlanId,
        IEnumerable<CanonicalFleetPlan> plans,
        bool requireNonEmpty)
    {
        ArgumentNullException.ThrowIfNull(plans);

        if (version is < 0 or > DomainLimits.MaxCanonicalInteger
            || sourceEpoch is < 0 or > DomainLimits.MaxCanonicalInteger)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }

        var ordered = plans
            .OrderBy(value => value.PlanId, StringComparer.Ordinal)
            .ToArray();
        var duplicate = ordered
            .GroupBy(value => value.PlanId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ArgumentException(
                "Plan-pool IDs must be unique.",
                nameof(plans));
        }

        if (!requireNonEmpty)
        {
            if (version != 0
                || sourceEpoch != 0
                || distinguishedPlanId is not null
                || ordered.Length != 0)
            {
                throw new ArgumentException(
                    "Version zero is the sole canonical empty plan pool.",
                    nameof(plans));
            }

            return Empty;
        }

        if (version == 0
            || ordered.Length == 0
            || distinguishedPlanId is null
            || !ordered.Any(value => StringComparer.Ordinal.Equals(
                value.PlanId,
                distinguishedPlanId))
            || ordered.Any(value => value.SourceEpoch != sourceEpoch))
        {
            throw new ArgumentException(
                "A non-empty pool requires one distinguished same-epoch plan.",
                nameof(plans));
        }

        return new VersionedPlanPool(
            version,
            sourceEpoch,
            distinguishedPlanId,
            new ReadOnlyCollection<CanonicalFleetPlan>(ordered));
    }
}
