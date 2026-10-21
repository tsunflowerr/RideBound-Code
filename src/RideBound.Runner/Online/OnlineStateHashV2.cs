using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using RideBound.Application.State;
using RideBound.Application.Travel;
using RideBound.Contracts.Protocol;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Incidents;
using RideBound.Domain.Requests;
using RideBound.Domain.Vehicles;

namespace RideBound.Runner.Online;

/// <summary>
/// PROTOTYPE (wf9 option b): OnlineStateHash.v2, a two-level commitment to the same canonical
/// v1 entity bytes. Each entity's leaf digest D(x) = SHA-256(LeafDomain || canonical v1 bytes of x)
/// is cached by object identity (32 bytes, the bytes themselves are not kept); a rider history
/// digest covers its entry digests; the root frames the header and the ordered digest lists:
/// <code>
/// v2 = SHA-256("RideBound.OnlineStateHash.v2\0"
///   || Frame("header", canonical {appliedEpoch, expectedInitialTravelTimeSnapshotHash,
///                                 nextEventSeq, runId, scenarioId, simulationTimeMs})
///   || Frame("requests", D(request)* by requestId)   || Frame("vehicles", D(vehicle)* by vehicleId)
///   || Frame("travelTimes", D(travelTimes) or empty) || Frame("planPool", D(planPool) or empty)
///   || Frame("commitmentLedger", H(history)* by requestId)
///   || Frame("breaches", D(breach)* in ledger order) || Frame("incidents", D(incident)* by id)
///   || Frame("latePickups", D(latePickups array) or empty))
/// H(history) = SHA-256("RideBound.LedgerHistory.v2\0" || Frame("requestId", UTF8) || Frame("entries", D(entry)*))
/// </code>
/// Per epoch the cost is O(changed entities' bytes + 32 B x (requests + vehicles + riders +
/// breaches + incidents)), independent of the ledger length and of the travel-arc count.
/// </summary>
public sealed class OnlineStateHashV2
{
    private static readonly byte[] RootDomain = "RideBound.OnlineStateHash.v2\0"u8.ToArray();
    private static readonly byte[] LeafDomain = "RideBound.OnlineStateLeaf.v2\0"u8.ToArray();
    private static readonly byte[] HistoryDomain = "RideBound.LedgerHistory.v2\0"u8.ToArray();

    private readonly ConditionalWeakTable<object, byte[]>? _digests;

    /// <param name="cached">False computes every digest from scratch (the checkpoint check).</param>
    public OnlineStateHashV2(bool cached = true)
    {
        _digests = cached ? new ConditionalWeakTable<object, byte[]>() : null;
    }

    public long LeafMisses { get; private set; }

    // Bench hook: drop one cached digest (simulates a changed entity).
    internal void Forget(object key) => _digests?.Remove(key);

    public Sha256Hex Calculate(OnlineState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        using var root = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        root.AppendData(RootDomain);

        var header = new CanonicalBuffer(512);
        var item = new CanonicalObject(header);
        item.Member("appliedEpoch"u8).Integer(state.Run.AppliedEpoch);
        item.Member("expectedInitialTravelTimeSnapshotHash"u8).String(state.ExpectedInitialTravelTimeSnapshotHash);
        item.Member("nextEventSeq"u8).Integer(state.NextEventSequence);
        item.Member("runId"u8).String(state.Run.Id.Value);
        item.Member("scenarioId"u8).String(state.Run.ScenarioId.Value);
        item.Member("simulationTimeMs"u8).Integer(state.Run.SimulationTime.Milliseconds);
        item.End();
        Frame(root, "header", header.WrittenSpan);

        var requests = state.Run.Requests.Values
            .OrderBy(value => value.Id.Value, StringComparer.Ordinal)
            .ToArray();
        FrameHeader(root, "requests", 32L * requests.Length);

        foreach (var request in requests)
        {
            root.AppendData(Leaf(request, OnlineStateCanonicalWriter.WriteRequest));
        }

        var vehicles = state.Run.Vehicles.Values
            .OrderBy(value => value.Id.Value, StringComparer.Ordinal)
            .ToArray();
        FrameHeader(root, "vehicles", 32L * vehicles.Length);

        foreach (var vehicle in vehicles)
        {
            root.AppendData(Leaf(vehicle, OnlineStateCanonicalWriter.WriteVehicle));
        }

        if (state.TravelTimes is null)
        {
            Frame(root, "travelTimes", []);
        }
        else
        {
            Frame(root, "travelTimes", Leaf(state.TravelTimes, OnlineStateCanonicalWriter.WriteTravelTimes));
        }

        if (state.PlanPool.Version == 0)
        {
            Frame(root, "planPool", []);
        }
        else
        {
            Frame(root, "planPool", Leaf(state.PlanPool, OnlineStateCanonicalWriter.WritePlanPool));
        }

        var histories = state.Commitments.Histories.Values
            .OrderBy(value => value.RequestId.Value, StringComparer.Ordinal)
            .ToArray();
        FrameHeader(root, "commitmentLedger", 32L * histories.Length);

        foreach (var history in histories)
        {
            root.AppendData(History(history));
        }

        var breaches = state.Incidents.Breaches;
        FrameHeader(root, "breaches", 32L * breaches.Count);

        foreach (var breach in breaches)
        {
            root.AppendData(Leaf(breach, OnlineStateCanonicalWriter.WriteBreach));
        }

        var incidents = state.Incidents.Incidents.Values
            .OrderBy(value => value.Id.Value, StringComparer.Ordinal)
            .ToArray();
        FrameHeader(root, "incidents", 32L * incidents.Length);

        foreach (var incident in incidents)
        {
            root.AppendData(Leaf(incident, OnlineStateCanonicalWriter.WriteIncident));
        }

        if (state.Incidents.LatePickups.Count == 0)
        {
            Frame(root, "latePickups", []);
        }
        else
        {
            var late = new CanonicalBuffer(256);
            WriteLatePickups(late, state.Incidents.LatePickups);
            Frame(root, "latePickups", Digest(LeafDomain, late.WrittenSpan));
        }

        Sha256Hex.TryCreate(Convert.ToHexStringLower(root.GetHashAndReset()), out var result);
        return result!;
    }

    private byte[] History(RiderCommitmentHistory history)
    {
        if (_digests is not null && _digests.TryGetValue(history, out var cached))
        {
            return cached;
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(HistoryDomain);
        Frame(hash, "requestId", Encoding.UTF8.GetBytes(history.RequestId.Value));
        FrameHeader(hash, "entries", 32L * history.Entries.Count);

        foreach (var entry in history.Entries)
        {
            hash.AppendData(Leaf(entry, OnlineStateCanonicalWriter.WriteLedgerEntry));
        }

        var digest = hash.GetHashAndReset();
        _digests?.AddOrUpdate(history, digest);
        return digest;
    }

    private byte[] Leaf<T>(T value, Action<CanonicalBuffer, T> write)
        where T : class
    {
        if (_digests is not null && _digests.TryGetValue(value, out var cached))
        {
            return cached;
        }

        LeafMisses++;
        var buffer = new CanonicalBuffer(1024);
        write(buffer, value);
        var digest = Digest(LeafDomain, buffer.WrittenSpan);
        _digests?.AddOrUpdate(value, digest);
        return digest;
    }

    private static byte[] Digest(ReadOnlySpan<byte> domain, ReadOnlySpan<byte> value)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(domain);
        hash.AppendData(value);
        return hash.GetHashAndReset();
    }

    private static void Frame(IncrementalHash hash, string tag, ReadOnlySpan<byte> value)
    {
        FrameHeader(hash, tag, value.Length);
        hash.AppendData(value);
    }

    private static void FrameHeader(IncrementalHash hash, string tag, long length)
    {
        var tagBytes = Encoding.ASCII.GetBytes(tag);
        Span<byte> header = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16BigEndian(header, (ushort)tagBytes.Length);
        hash.AppendData(header);
        hash.AppendData(tagBytes);
        Span<byte> size = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(size, (ulong)length);
        hash.AppendData(size);
    }

    // OnlineStateCanonicalizer.cs:532-550 (the latePickups array, written only when non-empty).
    private static void WriteLatePickups(CanonicalBuffer buffer, IReadOnlyList<ObservedLatePickup> latePickups)
    {
        buffer.Byte((byte)'[');
        var first = true;

        foreach (var late in latePickups)
        {
            if (!first)
            {
                buffer.Byte((byte)',');
            }

            first = false;
            var item = new CanonicalObject(buffer);
            item.Member("actualPickupMs"u8).Integer(late.ActualPickup.Milliseconds);
            item.Member("latestPickupMs"u8).Integer(late.LatestPickup.Milliseconds);
            item.Member("recordedEpoch"u8).Integer(late.RecordedEpoch);
            item.Member("requestId"u8).String(late.RequestId.Value);
            item.Member("sourceEventSeq"u8).Integer(late.SourceEventSequence);
            item.Member("vehicleId"u8).String(late.VehicleId.Value);
            item.End();
        }

        buffer.Byte((byte)']');
    }
}
