using System.Text.Json;

namespace RideBound.Contracts.Protocol;

public sealed record ErrorPayload(
    string Code,
    ProtocolFailureDisposition Disposition,
    string Message);

public static class ProtocolErrorCodes
{
    private static readonly IReadOnlyDictionary<string, ProtocolFailureDisposition>
        Dispositions =
            new Dictionary<string, ProtocolFailureDisposition>(StringComparer.Ordinal)
            {
                ["MALFORMED_UTF8"] = ProtocolFailureDisposition.RejectMessage,
                ["MALFORMED_JSON"] = ProtocolFailureDisposition.RejectMessage,
                ["MESSAGE_TOO_LARGE"] = ProtocolFailureDisposition.RejectMessage,
                ["INVALID_SCHEMA_VERSION"] = ProtocolFailureDisposition.RejectMessage,
                ["UNKNOWN_MESSAGE_TYPE"] = ProtocolFailureDisposition.RejectMessage,
                ["SCHEMA_VALIDATION_FAILED"] = ProtocolFailureDisposition.RejectMessage,
                ["UNKNOWN_FIELD"] = ProtocolFailureDisposition.RejectMessage,
                ["UNSUPPORTED_SCHEMA_MAJOR"] = ProtocolFailureDisposition.FailSession,
                ["UNSUPPORTED_SCHEMA_MINOR"] = ProtocolFailureDisposition.RejectMessage,
                ["INVALID_SESSION_STATE"] = ProtocolFailureDisposition.RejectMessage,
                ["IDENTITY_MISMATCH"] = ProtocolFailureDisposition.RejectMessage,
                ["CAPABILITY_REQUIRED_MISSING"] =
                    ProtocolFailureDisposition.RejectMessage,
                ["EVENT_SEQUENCE_GAP"] = ProtocolFailureDisposition.FailSession,
                ["EVENT_SEQUENCE_OVERLAP"] = ProtocolFailureDisposition.FailSession,
                ["EPOCH_GAP"] = ProtocolFailureDisposition.FailSession,
                ["DUPLICATE_PAYLOAD_CONFLICT"] =
                    ProtocolFailureDisposition.FailSession,
                ["HASH_MISMATCH"] = ProtocolFailureDisposition.FailSession,
                ["MANIFEST_MUTATION"] = ProtocolFailureDisposition.FailSession,
                ["INTERNAL_ERROR"] = ProtocolFailureDisposition.FailSession,
                ["INCOMPLETE_FRAME_EOF"] =
                    ProtocolFailureDisposition.TerminateProcess,
            };

    public static bool TryGetDisposition(
        string? code,
        out ProtocolFailureDisposition disposition) =>
        Dispositions.TryGetValue(code ?? string.Empty, out disposition);

    public static string ToProtocolValue(ProtocolFailureDisposition disposition) =>
        disposition switch
        {
            ProtocolFailureDisposition.RejectMessage => "rejectMessage",
            ProtocolFailureDisposition.FailSession => "failSession",
            ProtocolFailureDisposition.TerminateProcess => "terminateProcess",
            _ => throw new ArgumentOutOfRangeException(nameof(disposition)),
        };

    public static bool TryParseDisposition(
        string? value,
        out ProtocolFailureDisposition disposition)
    {
        disposition = value switch
        {
            "rejectMessage" => ProtocolFailureDisposition.RejectMessage,
            "failSession" => ProtocolFailureDisposition.FailSession,
            "terminateProcess" => ProtocolFailureDisposition.TerminateProcess,
            _ => default,
        };

        return value is "rejectMessage" or "failSession" or "terminateProcess";
    }
}

public static class ErrorPayloadCodec
{
    private static readonly IReadOnlySet<string> Fields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "code",
            "disposition",
            "message",
        };

    public static ProtocolPayloadDecodeResult<ErrorPayload> Decode(JsonElement payload)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            payload,
            "$.payload",
            Fields);

        if (objectError is not null)
        {
            return ProtocolPayloadDecodeResult<ErrorPayload>.Failure(objectError);
        }

        var code = ProtocolPayloadReader.ReadRequiredString(
            payload,
            "$.payload",
            "code");
        var dispositionText = ProtocolPayloadReader.ReadRequiredString(
            payload,
            "$.payload",
            "disposition");
        var message = ProtocolPayloadReader.ReadRequiredString(
            payload,
            "$.payload",
            "message",
            requireOpaqueValue: false);
        var error = HelloPayloadCodec.FirstError(
            code.Error,
            dispositionText.Error,
            message.Error);

        if (error is not null)
        {
            return ProtocolPayloadDecodeResult<ErrorPayload>.Failure(error);
        }

        if (!ProtocolErrorCodes.TryGetDisposition(code.Value, out var expected)
            || !ProtocolErrorCodes.TryParseDisposition(
                dispositionText.Value,
                out var actual)
            || actual != expected)
        {
            return ProtocolPayloadDecodeResult<ErrorPayload>.Failure(
                new ProtocolPayloadError(
                    ProtocolPayloadErrorCode.InvalidValue,
                    "$.payload",
                    "Error code is unknown or does not match its stable disposition."));
        }

        if (!IsSanitized(message.Value!))
        {
            return ProtocolPayloadDecodeResult<ErrorPayload>.Failure(
                new ProtocolPayloadError(
                    ProtocolPayloadErrorCode.InvalidValue,
                    "$.payload.message",
                    "Error message must be a single sanitized line."));
        }

        return ProtocolPayloadDecodeResult<ErrorPayload>.Success(
            new ErrorPayload(code.Value!, actual, message.Value!));
    }

    public static byte[] Encode(ErrorPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (!ProtocolErrorCodes.TryGetDisposition(payload.Code, out var expected)
            || expected != payload.Disposition)
        {
            throw new ArgumentException(
                "Error code and disposition do not match the v1 taxonomy.",
                nameof(payload));
        }

        var message = Sanitize(payload.Message);

        return ProtocolPayloadReader.Write(
            writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("code", payload.Code);
                writer.WriteString(
                    "disposition",
                    ProtocolErrorCodes.ToProtocolValue(payload.Disposition));
                writer.WriteString("message", message);
                writer.WriteEndObject();
            });
    }

    public static string Sanitize(string? message)
    {
        var value = string.IsNullOrWhiteSpace(message)
            ? "Protocol processing failed."
            : message.Replace('\r', ' ').Replace('\n', ' ').Trim();

        return value.Length <= 256 ? value : value[..256];
    }

    private static bool IsSanitized(string message) =>
        message.Length is > 0 and <= 256
        && message.IndexOfAny(['\r', '\n']) < 0;
}
