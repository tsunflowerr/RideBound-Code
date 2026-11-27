using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RideBound.Contracts.Serialization;

namespace RideBound.Contracts.Protocol;

public sealed record CheckpointContent(
    Sha256Hex ManifestHash,
    Sha256Hex StateHash,
    Sha256Hex PreviousDecisionHash,
    long AppliedEpoch,
    long NextEventSequence,
    long SimulationTimeMs,
    JsonElement OnlineState);

public sealed record CheckpointPayload(
    string CheckpointVersion,
    Sha256Hex CheckpointHash,
    CheckpointContent Content);

public static class CheckpointPayloadCodec
{
    public const string CurrentVersion = "1.0.0";

    private static readonly byte[] HashDomain =
        "RideBound.CheckpointHash.v1\0"u8.ToArray();

    public static byte[] Encode(CheckpointPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ValidateContent(payload.Content);

        if (!string.Equals(
                payload.CheckpointVersion,
                CurrentVersion,
                StringComparison.Ordinal)
            || CalculateHash(payload.Content) != payload.CheckpointHash)
        {
            throw new ArgumentException(
                "Checkpoint version/hash does not match its content.",
                nameof(payload));
        }

        return ProtocolPayloadReader.Write(
            writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("checkpointVersion", payload.CheckpointVersion);
                writer.WriteString("checkpointHash", payload.CheckpointHash.Value);
                writer.WritePropertyName("content");
                WriteContent(writer, payload.Content);
                writer.WriteEndObject();
            });
    }

    public static Sha256Hex CalculateHash(CheckpointContent content)
    {
        ValidateContent(content);
        var canonical = CanonicalJson.Canonicalize(
            ProtocolPayloadReader.Write(writer => WriteContent(writer, content)));
        var buffer = new ArrayBufferWriter<byte>();
        buffer.Write(HashDomain);
        var tag = Encoding.UTF8.GetBytes("canonicalCheckpointContent");
        var header = buffer.GetSpan(sizeof(ushort));
        BinaryPrimitives.WriteUInt16BigEndian(header, checked((ushort)tag.Length));
        buffer.Advance(sizeof(ushort));
        buffer.Write(tag);
        header = buffer.GetSpan(sizeof(ulong));
        BinaryPrimitives.WriteUInt64BigEndian(header, (ulong)canonical.Length);
        buffer.Advance(sizeof(ulong));
        buffer.Write(canonical);
        var text = Convert.ToHexStringLower(SHA256.HashData(buffer.WrittenSpan));
        Sha256Hex.TryCreate(text, out var hash);
        return hash!;
    }

    /// <summary>
    /// The same hash as <see cref="CalculateHash(CheckpointContent)"/> for a content whose
    /// online state is given as canonical bytes, in parts, so a Via-sized state is hashed without
    /// being copied. <paramref name="shell"/> carries an empty object as its online state; the
    /// canonical content is the shell's with that empty object replaced by
    /// <paramref name="canonicalOnlineState"/>, which must already be canonical JSON.
    /// </summary>
    public static Sha256Hex CalculateHash(
        CheckpointContent shell,
        IReadOnlyList<ReadOnlyMemory<byte>> canonicalOnlineState)
    {
        ArgumentNullException.ThrowIfNull(canonicalOnlineState);
        RequireEmptyOnlineState(shell);
        var canonical = CanonicalJson.Canonicalize(
            ProtocolPayloadReader.Write(writer => WriteContent(writer, shell)));
        var (before, after) = SplitAtEmptyOnlineState(canonical);
        var length = (long)before.Length + after.Length;

        foreach (var part in canonicalOnlineState)
        {
            length += part.Length;
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(HashDomain);
        var tag = Encoding.UTF8.GetBytes("canonicalCheckpointContent");
        Span<byte> header = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt16BigEndian(header, checked((ushort)tag.Length));
        hash.AppendData(header[..sizeof(ushort)]);
        hash.AppendData(tag);
        BinaryPrimitives.WriteUInt64BigEndian(header, (ulong)length);
        hash.AppendData(header);
        hash.AppendData(before.Span);

        foreach (var part in canonicalOnlineState)
        {
            hash.AppendData(part.Span);
        }

        hash.AppendData(after.Span);
        var text = Convert.ToHexStringLower(hash.GetHashAndReset());
        Sha256Hex.TryCreate(text, out var result);
        return result!;
    }

    /// <summary>
    /// The payload of <paramref name="shell"/> under a hash computed for the full
    /// content (<see cref="CalculateHash(CheckpointContent, IReadOnlyList{ReadOnlyMemory{byte}})"/>).
    /// <see cref="Encode"/> cannot be used because it checks the hash against the shell.
    /// </summary>
    public static byte[] EncodeShell(Sha256Hex checkpointHash, CheckpointContent shell)
    {
        ArgumentNullException.ThrowIfNull(checkpointHash);
        RequireEmptyOnlineState(shell);
        return ProtocolPayloadReader.Write(
            writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("checkpointVersion", CurrentVersion);
                writer.WriteString("checkpointHash", checkpointHash.Value);
                writer.WritePropertyName("content");
                WriteContent(writer, shell);
                writer.WriteEndObject();
            });
    }

    /// <summary>
    /// Splits canonical JSON at the value of its one <c>"onlineState":{}</c> member: the bytes up
    /// to and including <c>"onlineState":</c>, and the bytes after <c>{}</c>. In canonical JSON a
    /// quote inside a string is escaped, so the member text can only be the member itself.
    /// </summary>
    public static (ReadOnlyMemory<byte> Before, ReadOnlyMemory<byte> After) SplitAtEmptyOnlineState(
        byte[] canonical)
    {
        ArgumentNullException.ThrowIfNull(canonical);
        var marker = "\"onlineState\":{}"u8;
        var first = canonical.AsSpan().IndexOf(marker);

        if (first < 0 || canonical.AsSpan().LastIndexOf(marker) != first)
        {
            throw new ArgumentException(
                "Canonical checkpoint JSON must contain exactly one empty onlineState member.",
                nameof(canonical));
        }

        var valueStart = first + marker.Length - 2;
        return (canonical.AsMemory(0, valueStart), canonical.AsMemory(valueStart + 2));
    }

    private static void RequireEmptyOnlineState(CheckpointContent shell)
    {
        ValidateContent(shell);

        if (shell.OnlineState.EnumerateObject().Any())
        {
            throw new ArgumentException(
                "A checkpoint shell carries an empty onlineState object.",
                nameof(shell));
        }
    }

    private static void WriteContent(
        Utf8JsonWriter writer,
        CheckpointContent content)
    {
        writer.WriteStartObject();
        writer.WriteString("manifestHash", content.ManifestHash.Value);
        writer.WriteString("stateHash", content.StateHash.Value);
        writer.WriteString(
            "previousDecisionHash",
            content.PreviousDecisionHash.Value);
        writer.WriteNumber("appliedEpoch", content.AppliedEpoch);
        writer.WriteNumber("nextEventSeq", content.NextEventSequence);
        writer.WriteNumber("simTimeMs", content.SimulationTimeMs);
        writer.WritePropertyName("onlineState");
        content.OnlineState.WriteTo(writer);
        writer.WriteEndObject();
    }

    private static void ValidateContent(CheckpointContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.AppliedEpoch is < 0 or > ProtocolLimits.MaxCanonicalInteger
            || content.NextEventSequence is < 1 or > ProtocolLimits.MaxCanonicalInteger
            || content.SimulationTimeMs is < 0 or > ProtocolLimits.MaxCanonicalInteger
            || content.OnlineState.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Checkpoint content is not canonical.", nameof(content));
        }
    }
}
