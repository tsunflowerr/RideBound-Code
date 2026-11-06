using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using RideBound.Application.State;
using RideBound.Domain.Common;
using RideBound.Domain.Routes;

namespace RideBound.Algorithms.Candidates;

internal static class CandidateIdentity
{
    private static readonly byte[] CandidateDomain =
        "RideBound.InsertionCandidate.v1\0"u8.ToArray();

    private static readonly byte[] StopDomain =
        "RideBound.GeneratedStop.v1\0"u8.ToArray();

    private static readonly byte[] RouteDomain =
        "RideBound.ScheduleRoute.v1\0"u8.ToArray();

    private static readonly byte[] HoldStopDomain =
        "RideBound.OriginHoldStop.v1\0"u8.ToArray();

    private static readonly byte[] OmissionDomain =
        "RideBound.CandidateOmission.v1\0"u8.ToArray();

    private static readonly byte[] SearchNodeDomain =
        "RideBound.CandidateSearchNode.v1\0"u8.ToArray();

    // Framed identity payloads are dominated by 64-hex stop and request IDs.
    // Sizing the writer up front removes the doubling reallocation chain from a
    // path that runs once per generated candidate and once per search node.
    private const int FrameEstimate = 96;
    private const int StopEstimate = 5 * FrameEstimate;

    private static ArrayBufferWriter<byte> CreateBuffer(int estimatedBytes) =>
        new(Math.Max(256, estimatedBytes));

    public static string Create(
        OnlineState state,
        VehicleId vehicleId,
        RoutePlan route,
        IReadOnlyList<RequestId> newRequestIds)
    {
        var buffer = CreateBuffer(
            CandidateDomain.Length
            + 256
            + newRequestIds.Count * FrameEstimate
            + (route.FrozenPrefix.Count + route.MutableSuffix.Count)
                * StopEstimate);
        buffer.Write(CandidateDomain);
        WriteFrame(buffer, "vehicleId", vehicleId.Value);
        WriteFrame(
            buffer,
            "evaluationTimeMs",
            state.Run.SimulationTime.Milliseconds);
        WriteFrame(
            buffer,
            "travelSnapshotHash",
            state.TravelTimes?.SnapshotHash ?? string.Empty);
        WriteFrame(buffer, "planVersion", route.Version.Value);
        WriteFrame(buffer, "executedStopCount", route.ExecutedStopCount);

        foreach (var requestId in newRequestIds.OrderBy(
                     value => value.Value,
                     StringComparer.Ordinal))
        {
            WriteFrame(buffer, "newRequestId", requestId.Value);
        }

        foreach (var stop in route.FrozenPrefix)
        {
            WriteStop(buffer, "frozenStop", stop);
        }

        foreach (var stop in route.MutableSuffix)
        {
            WriteStop(buffer, "mutableStop", stop);
        }

        return $"candidate-v1-{Convert.ToHexStringLower(
            SHA256.HashData(buffer.WrittenSpan))}";
    }

    public static StopId CreateStopId(RequestId requestId, RouteStopKind kind)
    {
        var buffer = new ArrayBufferWriter<byte>();
        buffer.Write(StopDomain);
        WriteFrame(
            buffer,
            "kind",
            kind == RouteStopKind.Pickup ? "pickup" : "dropOff");
        WriteFrame(buffer, "requestId", requestId.Value);
        var prefix = kind == RouteStopKind.Pickup ? "rb-p-" : "rb-d-";
        return new StopId(
            $"{prefix}{Convert.ToHexStringLower(
                SHA256.HashData(buffer.WrittenSpan))}");
    }

    public static string CreateRouteFingerprint(RoutePlan route)
    {
        ArgumentNullException.ThrowIfNull(route);
        var buffer = new ArrayBufferWriter<byte>();
        buffer.Write(RouteDomain);
        WriteFrame(buffer, "planVersion", route.Version.Value);
        WriteFrame(buffer, "executedStopCount", route.ExecutedStopCount);

        foreach (var stop in route.FrozenPrefix)
        {
            WriteStop(buffer, "frozenStop", stop);
        }

        foreach (var stop in route.MutableSuffix)
        {
            WriteStop(buffer, "mutableStop", stop);
        }

        return Convert.ToHexStringLower(SHA256.HashData(buffer.WrittenSpan));
    }

    public static StopId CreateHoldStopId(string sourceCandidateId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceCandidateId);
        var buffer = new ArrayBufferWriter<byte>();
        buffer.Write(HoldStopDomain);
        WriteFrame(buffer, "sourceCandidateId", sourceCandidateId);
        return new StopId(
            $"rb-h-{Convert.ToHexStringLower(
                SHA256.HashData(buffer.WrittenSpan))}");
    }

    public static string CreateOmissionDigest(IEnumerable<string> stableIds)
    {
        ArgumentNullException.ThrowIfNull(stableIds);
        var buffer = new ArrayBufferWriter<byte>();
        buffer.Write(OmissionDomain);

        foreach (var stableId in stableIds.Order(StringComparer.Ordinal))
        {
            WriteFrame(buffer, "omittedId", stableId);
        }

        return Convert.ToHexStringLower(SHA256.HashData(buffer.WrittenSpan));
    }

    public static string CreateSearchNodeDigest(
        IEnumerable<string> orderedTokens,
        int estimatedTokenCount = 0)
    {
        ArgumentNullException.ThrowIfNull(orderedTokens);
        var buffer = CreateBuffer(
            SearchNodeDomain.Length + estimatedTokenCount * FrameEstimate);
        buffer.Write(SearchNodeDomain);

        foreach (var token in orderedTokens)
        {
            WriteFrame(buffer, "orderedToken", token);
        }

        return Convert.ToHexStringLower(SHA256.HashData(buffer.WrittenSpan));
    }

    private static void WriteStop(
        IBufferWriter<byte> writer,
        string tag,
        RouteStop stop)
    {
        WriteFrame(writer, tag, stop.StopId.Value);
        WriteFrame(writer, "nodeId", stop.NodeId.Value);
        WriteFrame(writer, "kind", KindToken(stop.Kind));
        WriteFrame(writer, "requestId", stop.RequestId?.Value ?? string.Empty);
        WriteFrame(writer, "serviceDurationMs", stop.ServiceDuration.Milliseconds);
    }

    /// <summary>
    /// The exact strings <see cref="Enum.ToString()"/> produces for
    /// <see cref="RouteStopKind"/>. Framing them from constants keeps the bytes
    /// identical while removing an enum formatting allocation from a path that
    /// runs once per stop of every candidate route identity.
    /// </summary>
    private static string KindToken(RouteStopKind kind) => kind switch
    {
        RouteStopKind.Waypoint => "Waypoint",
        RouteStopKind.Pickup => "Pickup",
        RouteStopKind.DropOff => "DropOff",
        _ => kind.ToString(),
    };

    private static void WriteFrame(
        IBufferWriter<byte> writer,
        string tag,
        long value)
    {
        Span<char> digits = stackalloc char[20];

        if (!value.TryFormat(
                digits,
                out var written,
                provider: CultureInfo.InvariantCulture))
        {
            WriteFrame(
                writer,
                tag,
                value.ToString(CultureInfo.InvariantCulture));
            return;
        }

        WriteFrame(writer, tag, digits[..written]);
    }

    private static void WriteFrame(
        IBufferWriter<byte> writer,
        string tag,
        ReadOnlySpan<char> value)
    {
        // Encode straight into the writer's own buffer. The previous form
        // allocated one byte[] for the tag and one for the value on every
        // frame, so a single route fingerprint allocated roughly two arrays per
        // stop field. Candidate identities are computed millions of times per
        // decision, and the emitted bytes here are unchanged.
        var tagLength = Encoding.UTF8.GetByteCount(tag);
        var span = writer.GetSpan(sizeof(ushort) + tagLength);
        BinaryPrimitives.WriteUInt16BigEndian(
            span,
            checked((ushort)tagLength));
        Encoding.UTF8.GetBytes(tag, span[sizeof(ushort)..]);
        writer.Advance(sizeof(ushort) + tagLength);

        var valueLength = Encoding.UTF8.GetByteCount(value);
        span = writer.GetSpan(sizeof(ulong) + valueLength);
        BinaryPrimitives.WriteUInt64BigEndian(span, (ulong)valueLength);
        Encoding.UTF8.GetBytes(value, span[sizeof(ulong)..]);
        writer.Advance(sizeof(ulong) + valueLength);
    }
}
