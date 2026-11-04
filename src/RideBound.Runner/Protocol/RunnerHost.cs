using System.Text.Json;
using RideBound.Application.Commitments;
using RideBound.Contracts.Protocol;
using RideBound.Domain.Validation;
using RideBound.Runner.Configuration;

namespace RideBound.Runner.Protocol;

public static class RunnerDefaults
{
    public static CapabilityRequirementProfile CapabilityRequirements { get; } =
        new(
            RequiredPositionModel: "nodeOnly",
            RequiredCapabilities: ["exactEventOrdering"],
            OptionalCapabilities:
            [
                "dynamicTravelTimes",
                "oldPlanProjection",
            ],
            MinimumFleetSize: 1,
            MinimumRequestCount: 1);
}

public static class RunnerHost
{
    public static async Task<int> RunAsync(
        Stream input,
        Stream output,
        TextWriter diagnostics,
        CancellationToken cancellationToken = default,
        int maximumLineBytes = NdjsonReader.DefaultMaximumLineBytes,
        RunnerExecutionMode executionMode =
            RunnerExecutionMode.StructuralConformance,
        ICommitmentPolicyProvider? commitmentPolicies = null,
        IStopDistanceLookup? stopDistances = null,
        Sha256Hex? commitmentPolicyConfigurationHash = null,
        Wp4RunnerConfiguration? wp4Configuration = null,
        bool useManifestSolverSeed = false)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(diagnostics);

        var reader = new NdjsonReader(input, maximumLineBytes);
        var writer = new NdjsonWriter(output);
        var session = new RunnerSession(
            RunnerDefaults.CapabilityRequirements,
            executionMode,
            commitmentPolicies: commitmentPolicies,
            stopDistances: stopDistances,
            commitmentPolicyConfigurationHash:
                commitmentPolicyConfigurationHash,
            wp4Configuration: wp4Configuration,
            useManifestSolverSeed: useManifestSolverSeed);

        while (true)
        {
            var read = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);

            if (read.Kind == NdjsonReadKind.EndOfStream)
            {
                return 0;
            }

            if (read.Kind == NdjsonReadKind.Error)
            {
                if (read.ErrorCode == "INCOMPLETE_FRAME_EOF")
                {
                    await diagnostics.WriteLineAsync(
                        ErrorPayloadCodec.Sanitize(read.ErrorMessage))
                        .ConfigureAwait(false);
                    return 2;
                }

                var errorEnvelope = CreateErrorEnvelope(
                    read.ErrorCode!,
                    read.ErrorMessage!);
                await diagnostics.WriteLineAsync(
                    $"Protocol framing error: {read.ErrorCode}")
                    .ConfigureAwait(false);
                await writer.WriteAsync(errorEnvelope, cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            RunnerSessionResult result;

            try
            {
                result = session.Process(read.Utf8Json!);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                await diagnostics.WriteLineAsync(
                    $"Runner internal error: {exception.GetType().Name}")
                    .ConfigureAwait(false);
                var errorEnvelope = CreateErrorEnvelope(
                    "INTERNAL_ERROR",
                    "Runner could not process the message safely.");
                await writer.WriteAsync(errorEnvelope, cancellationToken)
                    .ConfigureAwait(false);
                return 3;
            }

            if (result.Streamed is not null)
            {
                // Never read result.Response here: for a streamed result it decodes a copy.
                await writer.WriteAsync(result.Streamed, cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (result.Response is not null)
            {
                if (result.Response.MessageType.Value == "error")
                {
                    await diagnostics.WriteLineAsync(
                        $"Protocol error: {result.Response.Payload.GetProperty("code").GetString()}")
                        .ConfigureAwait(false);
                }

                await writer.WriteAsync(result.Response, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (result.ShouldTerminate)
            {
                return 0;
            }
        }
    }

    private static ProtocolEnvelope CreateErrorEnvelope(
        string code,
        string message)
    {
        ProtocolErrorCodes.TryGetDisposition(code, out var disposition);
        ProtocolMessageType.TryParse("error", out var messageType);
        var bytes = ErrorPayloadCodec.Encode(
            new ErrorPayload(
                code,
                disposition,
                ErrorPayloadCodec.Sanitize(message)));
        using var document = JsonDocument.Parse(bytes);

        return new ProtocolEnvelope(
            ProtocolVersion.Current,
            messageType!,
            document.RootElement.Clone());
    }
}
